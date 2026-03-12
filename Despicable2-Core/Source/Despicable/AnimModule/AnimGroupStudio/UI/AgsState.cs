using RimWorld;
// Guardrail-Reason: Core AGS editor state stays together while dirtiness, selection, and save queue lifecycles remain one authoring surface.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;
using Verse;
using Despicable.AnimGroupStudio;
using Despicable.AnimModule.AnimGroupStudio.Model;

namespace Despicable.AnimModule.AnimGroupStudio.UI;
public partial class Dialog_AnimGroupStudio
{
    private void MarkAuthorPreviewDirty()
    {
        MarkAuthorPreviewStructureDirty();
            }

    private void MarkAuthorPreviewStructureDirty()
    {
        authorRuntime.StructureDirty = true;
        authorRuntime.SelectionDirty = true;
        authorPreviewSourceHash = int.MinValue;
            }

    private void MarkAuthorPreviewStageDirty(int stageIndex)
    {
        if (stageIndex >= 0)
            authorRuntime.DirtyStageIndices.Add(stageIndex);
        authorRuntime.SelectionDirty = true;
            }

    private void MarkAuthorPreviewSelectionDirty()
    {
        authorRuntime.SelectionDirty = true;
            }

    private void QueueAuthorSave()
    {
        authorRuntime.SavePending = true;
            }

    private void NormalizeAuthorSelection()
    {
        if (project == null)
        {
            authorStageIndex = 0;
            authorTrackIndex = -1;
            authorKeyIndex = -1;
            return;
        }

        EnsureRoles(project);
        EnsureAuthorRoleKeyValid(project);
        EnsureStages(project);

        if (project.stages == null || project.stages.Count == 0)
        {
            authorStageIndex = 0;
            authorTrackIndex = -1;
            authorKeyIndex = -1;
            return;
        }

        authorStageIndex = Mathf.Clamp(authorStageIndex, 0, project.stages.Count - 1);
        var stage = GetStage(project, authorStageIndex);
        var clip = GetClip(stage, authorRoleKey);
        EnsureClip(clip, stage?.durationTicks ?? 1);

        if (clip?.tracks == null || clip.tracks.Count == 0)
        {
            authorTrackIndex = -1;
            authorKeyIndex = -1;
            return;
        }

        if (authorTrackIndex < 0 || authorTrackIndex >= clip.tracks.Count)
        {
            authorTrackIndex = -1;
            authorKeyIndex = -1;
            return;
        }

        var track = clip.tracks[authorTrackIndex];
        if (track?.keys == null || track.keys.Count == 0)
        {
            authorKeyIndex = -1;
            return;
        }

        authorKeyIndex = Mathf.Clamp(authorKeyIndex, 0, track.keys.Count - 1);
            }

    private void ApplyAuthorEdit(Action edit, bool structureDirty = false, bool selectionDirty = false, int dirtyStageIndex = -1, bool queueSave = true)
    {
        edit?.Invoke();
        NormalizeAuthorSelection();
        if (structureDirty)
            MarkAuthorPreviewStructureDirty();
        if (dirtyStageIndex >= 0)
            MarkAuthorPreviewStageDirty(dirtyStageIndex);
        if (selectionDirty)
            MarkAuthorPreviewSelectionDirty();
        if (queueSave)
            QueueAuthorSave();
            }


    private AgsModel.Keyframe GetInspectorDisplayKeyframe(AgsModel.Track tr, AgsModel.StageSpec stage, out bool isImplicitDisplay)
    {
        isImplicitDisplay = false;
        if (tr == null || stage == null)
            return null;

        if (authorKeyIndex >= 0 && authorKeyIndex < tr.keys.Count)
            return tr.keys[authorKeyIndex];

        int tick = Mathf.Clamp(authorPreviewTick, 0, Mathf.Max(1, stage.durationTicks));
        isImplicitDisplay = true;
        return SampleTrackKeyframeAtTick(tr, tick);
    }

    private AgsModel.Keyframe EnsureInspectorEditKeyframe(AgsModel.Track tr, AgsModel.StageSpec stage)
    {
        if (tr == null || stage == null)
            return null;

        if (authorKeyIndex >= 0 && authorKeyIndex < tr.keys.Count)
            return tr.keys[authorKeyIndex];

        int tick = Mathf.Clamp(authorPreviewTick, 0, Mathf.Max(1, stage.durationTicks));
        var existing = FindKeyframeAtTick(tr, tick);
        if (existing == null)
        {
            existing = SampleTrackKeyframeAtTick(tr, tick);
            existing.tick = tick;
            tr.keys.Add(existing);
            SortClampKeys(tr, Mathf.Max(1, stage.durationTicks));
        }

        authorKeyIndex = tr.keys.IndexOf(existing);
        ShowAuthorStageAtTick(authorStageIndex, tick, seekIfPlaying: false);
        return existing;
    }

    private void CommitAuthorStageKeyEdit(AgsModel.StageSpec stage, bool durationMayHaveChanged = false)
    {
        if (stage != null && durationMayHaveChanged)
            RecalculateStageDurationFromKeys(stage);

        QueueAuthorSave();
        MarkAuthorPreviewStageDirty(authorStageIndex);

        if (stage != null)
            ShowAuthorStageAtTick(authorStageIndex, Mathf.Clamp(authorPreviewTick, 0, Mathf.Max(1, stage.durationTicks)), seekIfPlaying: false);
    }

    private static void EnsureRoles(AgsModel.Project p)
    {
        if (p == null) return;
        if (p.roles == null) p.roles = new List<AgsModel.RoleSpec>();
    
        if (p.roles.Count == 0)
        {
            p.roles.Add(AgsModel.RoleSpec.MakeDefault("male_1", "Male 1", AgsModel.RoleGenderReq.Male));
            p.roles.Add(AgsModel.RoleSpec.MakeDefault("female_1", "Female 1", AgsModel.RoleGenderReq.Female));
        }
    
        // Normalize missing fields + ensure unique keys
        var used = new HashSet<string>();
        int maleN = 0, femaleN = 0, uniN = 0;
        for (int i = 0; i < p.roles.Count; i++)
        {
            var r = p.roles[i];
            if (r == null) continue;
    
            // Back-compat: infer req from legacy gender/roleId
            if (!Enum.IsDefined(typeof(AgsModel.RoleGenderReq), r.genderReq))
            {
                if (r.gender == Gender.Female || r.roleId == (int)AgsModel.RoleId.Female) r.genderReq = AgsModel.RoleGenderReq.Female;
                else r.genderReq = AgsModel.RoleGenderReq.Male;
            }
    
            if (r.genderReq == AgsModel.RoleGenderReq.Female) femaleN++;
            else if (r.genderReq == AgsModel.RoleGenderReq.Unisex) uniN++;
            else maleN++;
    
            string prefix = r.genderReq == AgsModel.RoleGenderReq.Female ? "female" : (r.genderReq == AgsModel.RoleGenderReq.Unisex ? "unisex" : "male");
            int idx = r.genderReq == AgsModel.RoleGenderReq.Female ? femaleN : (r.genderReq == AgsModel.RoleGenderReq.Unisex ? uniN : maleN);
    
            if (r.roleKey.NullOrEmpty()) r.roleKey = $"{prefix}_{idx}";
            if (r.displayName.NullOrEmpty())
            {
                string dn = r.genderReq == AgsModel.RoleGenderReq.Female ? "Female" : (r.genderReq == AgsModel.RoleGenderReq.Unisex ? "Unisex" : "Male");
                r.displayName = $"{dn} {idx}";
            }
    
            // Ensure unique key
            string baseKey = r.roleKey;
            string key = baseKey;
            int bump = 2;
            while (used.Contains(key))
            {
                key = baseKey + "_" + bump;
                bump++;
            }
            r.roleKey = key;
            used.Add(key);
    
            // Default dummy body types: Unisex defaults to Male (per your request)
            string bt = r.genderReq == AgsModel.RoleGenderReq.Female ? "Female" : "Male";
            if (r.defaultDummyBodyTypeDefName.NullOrEmpty()) r.defaultDummyBodyTypeDefName = bt;
            if (r.previewDummyBodyTypeDefName.NullOrEmpty()) r.previewDummyBodyTypeDefName = r.defaultDummyBodyTypeDefName;
    
            // Legacy coherence
            if (r.genderReq == AgsModel.RoleGenderReq.Female) { r.roleId = (int)AgsModel.RoleId.Female; r.gender = Gender.Female; }
            else { r.roleId = (int)AgsModel.RoleId.Male; r.gender = Gender.Male; }
        }
    
        if (p.offsetsByRoleKey == null) p.offsetsByRoleKey = new Dictionary<string, Dictionary<string, AgsModel.BodyTypeOffset>>();
        for (int i = 0; i < p.roles.Count; i++)
        {
            var r = p.roles[i];
            if (r?.roleKey.NullOrEmpty() != false) continue;
            if (!p.offsetsByRoleKey.ContainsKey(r.roleKey))
                p.offsetsByRoleKey[r.roleKey] = new Dictionary<string, AgsModel.BodyTypeOffset>();
        }
            }

    private static AgsModel.RoleSpec GetRole(AgsModel.Project p, string roleKey)
    {
        if (p?.roles == null || roleKey.NullOrEmpty()) return null;
        for (int i = 0; i < p.roles.Count; i++)
        {
            var r = p.roles[i];
            if (r != null && r.roleKey == roleKey) return r;
        }
        return null;
            }

    private void EnsureAuthorRoleKeyValid(AgsModel.Project p)
    {
        EnsureRoles(p);
        if (p?.roles.NullOrEmpty() != false)
        {
            authorRoleKey = "male_1";
            return;
        }
    
        if (authorRoleKey.NullOrEmpty() || GetRole(p, authorRoleKey) == null)
            authorRoleKey = p.roles.FirstOrDefault(r => r != null && !r.roleKey.NullOrEmpty())?.roleKey ?? "male_1";
            }

    private void AddRole(AgsModel.Project p, AgsModel.RoleGenderReq req)
    {
        EnsureRoles(p);
    
        string prefix = req == AgsModel.RoleGenderReq.Female ? "female" : (req == AgsModel.RoleGenderReq.Unisex ? "unisex" : "male");
        string dn = req == AgsModel.RoleGenderReq.Female ? "Female" : (req == AgsModel.RoleGenderReq.Unisex ? "Unisex" : "Male");
    
        int next = 1;
        for (int i = 0; i < p.roles.Count; i++)
        {
            var r = p.roles[i];
            if (r == null) continue;
            if (r.genderReq != req) continue;
            next++;
        }
    
        string roleKey = $"{prefix}_{next}";
        // Guarantee uniqueness
        var keys = new HashSet<string>(p.roles.Where(r => r != null && !r.roleKey.NullOrEmpty()).Select(r => r.roleKey));
        int bump = 2;
        string baseKey = roleKey;
        while (keys.Contains(roleKey))
        {
            roleKey = baseKey + "_" + bump;
            bump++;
        }
    
        string defaultName = $"{dn} {next}";
        Find.WindowStack.Add(new Dialog_TextEntrySimple("Role name", defaultName, (name) =>
        {
            var r = new AgsModel.RoleSpec
            {
                roleKey = roleKey,
                displayName = name,
                genderReq = req,
                roleId = req == AgsModel.RoleGenderReq.Female ? (int)AgsModel.RoleId.Female : (int)AgsModel.RoleId.Male,
                gender = req == AgsModel.RoleGenderReq.Female ? Gender.Female : Gender.Male,
                defaultDummyBodyTypeDefName = req == AgsModel.RoleGenderReq.Female ? "Female" : "Male", // Unisex defaults to Male
                previewDummyBodyTypeDefName = req == AgsModel.RoleGenderReq.Female ? "Female" : "Male"
            };
    
            p.roles.Add(r);
    
            // Ensure offsets and clips exist for new role
            if (p.offsetsByRoleKey == null) p.offsetsByRoleKey = new Dictionary<string, Dictionary<string, AgsModel.BodyTypeOffset>>();
            if (!p.offsetsByRoleKey.ContainsKey(roleKey)) p.offsetsByRoleKey[roleKey] = new Dictionary<string, AgsModel.BodyTypeOffset>();
    
            EnsureStages(p);
            for (int si = 0; si < p.stages.Count; si++)
            {
                var st = p.stages[si];
                if (st == null) continue;
                if (st.variants == null) st.variants = new List<AgsModel.StageVariant>();
                for (int vi = 0; vi < st.variants.Count; vi++)
                {
                    var v = st.variants[vi];
                    if (v == null) continue;
                    if (v.clips == null) v.clips = new List<AgsModel.RoleClip>();
                    v.EnsureClip(roleKey);
                }
                // Ensure Base exists
                if (!st.variants.Any(v => v != null && v.variantId == "Base"))
                    st.variants.Add(CreateBaseVariantForProject(p));
            }
    
            authorRoleKey = roleKey;
            authorTrackIndex = -1;
            authorKeyIndex = -1;

            MarkAuthorPreviewStructureDirty();
            QueueAuthorSave();
        }, validator: (s) => s.NullOrEmpty() ? "Name cannot be empty." : null));
            }

    private void DeleteRole(AgsModel.Project p, string roleKey)
    {
        if (p == null || roleKey.NullOrEmpty()) return;
        EnsureRoles(p);
    
        // Remove role spec
        p.roles.RemoveAll(r => r == null || r.roleKey == roleKey);
    
        // Remove offsets
        p.offsetsByRoleKey?.Remove(roleKey);
    
        // Remove clips for role across stages/variants
        EnsureStages(p);
        for (int si = 0; si < p.stages.Count; si++)
        {
            var st = p.stages[si];
            if (st?.variants == null) continue;
            for (int vi = 0; vi < st.variants.Count; vi++)
            {
                var v = st.variants[vi];
                if (v == null) continue;
                if (v.clips != null)
                    v.clips.RemoveAll(rc => rc == null || rc.roleKey == roleKey);
            }
        }
    
        // If we deleted the current role, reset selection
        if (authorRoleKey == roleKey)
            authorRoleKey = p.roles.FirstOrDefault(r => r != null && !r.roleKey.NullOrEmpty())?.roleKey ?? "male_1";
    
        // Drop preview slot
        if (authorSlotsByKey.TryGetValue(roleKey, out var slot))
        {
            try { slot?.Renderer?.Dispose(); } catch (Exception ex) { Despicable.Core.DebugLogger.WarnExceptionOnce("AgsState:1", "AgsState ignored a non-fatal editor exception.", ex); }
            authorSlotsByKey.Remove(roleKey);
        }

        MarkAuthorPreviewStructureDirty();
        QueueAuthorSave();
            }

    private static AgsModel.StageVariant CreateBaseVariantForProject(AgsModel.Project p)
    {
        var v = new AgsModel.StageVariant { variantId = "Base", clips = new List<AgsModel.RoleClip>() };
        if (p?.roles != null)
        {
            for (int i = 0; i < p.roles.Count; i++)
            {
                var r = p.roles[i];
                if (r == null || r.roleKey.NullOrEmpty()) continue;
                v.clips.Add(new AgsModel.RoleClip { roleKey = r.roleKey, clip = new AgsModel.ClipSpec() });
            }
        }
        // Fallback if roles missing
        if (v.clips.Count == 0)
        {
            v.clips.Add(new AgsModel.RoleClip { roleKey = "male_1", clip = new AgsModel.ClipSpec() });
            v.clips.Add(new AgsModel.RoleClip { roleKey = "female_1", clip = new AgsModel.ClipSpec() });
        }
        return v;
            }

    private static void EnsureStages(AgsModel.Project p)
    {
        if (p.stages == null) p.stages = new List<AgsModel.StageSpec>();
            }

    private static AgsModel.StageSpec GetStage(AgsModel.Project p, int idx)
    {
        if (p?.stages == null || p.stages.Count == 0) return null;
        idx = Mathf.Clamp(idx, 0, p.stages.Count - 1);
        return p.stages[idx];
            }

}
