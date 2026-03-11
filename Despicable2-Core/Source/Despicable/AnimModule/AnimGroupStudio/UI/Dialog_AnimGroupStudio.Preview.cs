using RimWorld;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;
using Verse;
using Despicable.AnimGroupStudio.Preview;
using Despicable.AnimGroupStudio;
using Verse.Sound;
using Despicable.UIFramework;
using Despicable.UIFramework.Controls;
using Despicable.UIFramework.Layout;
using Despicable.AnimModule.AnimGroupStudio.Model;

namespace Despicable.AnimModule.AnimGroupStudio.UI
{
    public partial class Dialog_AnimGroupStudio
    {
        private void StopAuthorPreview(bool resetTick)
        {
            preview.Stop();
            authorPreviewPlaying = false;
            authorPreviewTickAcc = 0f;
            if (resetTick)
            {
                ShowAuthorStageAtTick(authorStageIndex, 0, seekIfPlaying: false);
            }
            else
            {
                authorPreviewTick = preview.CurrentTick;
            }
        }

        private static int HashCombine(int hash, int value)
        {
            unchecked
            {
                return (hash * 31) + value;
            }
        }

        private static int ComputeStageHash(AgsModel.StageSpec stage)
        {
            unchecked
            {
                int h = 17;
                h = HashCombine(h, stage?.durationTicks ?? 0);
                h = HashCombine(h, stage?.repeatCount ?? 1);

                if (stage?.variants != null)
                {
                    for (int v = 0; v < stage.variants.Count; v++)
                    {
                        var variant = stage.variants[v];
                        h = HashCombine(h, variant?.variantId?.GetHashCode() ?? 0);
                        if (variant?.clips != null)
                        {
                            var ordered = variant.clips.Where(c => c != null && !c.roleKey.NullOrEmpty())
                                .OrderBy(c => c.roleKey, StringComparer.Ordinal).ToList();
                            for (int c = 0; c < ordered.Count; c++)
                            {
                                var rc = ordered[c];
                                h = HashCombine(h, rc.roleKey?.GetHashCode() ?? 0);
                                h = HashClip(h, rc.clip);
                            }
                        }
                    }
                }
                return h;
            }
        }

        private static int HashClip(int h, AgsModel.ClipSpec clip)
        {
            unchecked
            {
                if (clip?.tracks == null) return h;
                h = HashCombine(h, clip.lengthTicks);
                for (int t = 0; t < clip.tracks.Count; t++)
                {
                    var tr = clip.tracks[t];
                    h = HashCombine(h, tr?.nodeTag?.GetHashCode() ?? 0);
                    if (tr?.keys == null) continue;
                    for (int k = 0; k < tr.keys.Count; k++)
                    {
                        var key = tr.keys[k];
                        if (key == null) continue;
                        h = HashCombine(h, key.tick);
                        h = HashCombine(h, key.rotation.AsInt);
                        h = HashCombine(h, key.visible ? 1 : 0);
                        h = HashCombine(h, key.offset.GetHashCode());
                        h = HashCombine(h, key.angle.GetHashCode());
                        h = HashCombine(h, key.scale.GetHashCode());
                        h = HashCombine(h, key.graphicState?.GetHashCode() ?? 0);
                        h = HashCombine(h, key.variant == -1 ? 0 : key.variant.GetHashCode());
                        h = HashCombine(h, key.layerBias);
                        h = HashCombine(h, key.soundDefName?.GetHashCode() ?? 0);
                        h = HashCombine(h, key.facialAnimDefName?.GetHashCode() ?? 0);
                        h = HashCombine(h, key.prop?.propDefName?.GetHashCode() ?? 0);
                        h = HashCombine(h, key.prop != null && key.prop.propVisible ? 1 : 0);
                        h = HashCombine(h, key.prop != null ? key.prop.propOffset.GetHashCode() : 0);
                        h = HashCombine(h, key.prop != null ? key.prop.propAngle.GetHashCode() : 0);
                        h = HashCombine(h, key.prop != null ? key.prop.propScale.GetHashCode() : 0);
                    }
                }
                return h;
            }
        }

        private void RefreshAuthorPreviewIfNeeded()
        {
            if (project == null)
            {
                preview.ConfigureForRuntime("Author Preview", new List<AgsPreviewSession.RuntimeRole>(), new List<AgsPreviewSession.RuntimeStage>());
                authorPreviewPlaying = false;
                authorPreviewTick = 0;
                authorRuntime.StructureDirty = false;
                authorRuntime.SelectionDirty = false;
                authorRuntime.DirtyStageIndices.Clear();
                authorRuntime.CompiledStageHashes.Clear();
                authorPreviewSourceHash = int.MinValue;
                return;
            }

            EnsureRoles(project);
            EnsureAuthorRoleKeyValid(project);
            EnsureStages(project);

            int stageTotal = project.stages?.Count ?? 0;
            bool structureChanged = SyncAuthorPreviewSlots(stageTotal);
            structureChanged |= EnsureCompiledStageHashCacheSize(stageTotal);
            if (preview.StageCount != stageTotal)
                structureChanged = true;
            if (structureChanged)
                authorRuntime.StructureDirty = true;

            int sourceHash = ComputeAuthorPreviewSourceHash();
            bool sourceChanged = sourceHash != authorPreviewSourceHash;
            if (sourceChanged)
            {
                DetectDirtyAuthorStages();
                if (authorRuntime.DirtyStageIndices.Count == 0 && authorPreviewSourceHash == int.MinValue)
                {
                    for (int s = 0; s < stageTotal; s++)
                        authorRuntime.DirtyStageIndices.Add(s);
                }
            }

            bool needsRuntimeBinding = authorRuntime.StructureDirty || authorRuntime.DirtyStageIndices.Count > 0 || sourceChanged;
            if (needsRuntimeBinding)
            {
                CompileDirtyAuthorStages(stageTotal, authorRuntime.StructureDirty);
                ApplyAuthorPreviewBinding(stageTotal);
                preview.SetSpeed(authorPreviewSpeed);
                authorRuntime.StructureDirty = false;
                authorRuntime.DirtyStageIndices.Clear();
                authorPreviewSourceHash = sourceHash;
                authorRuntime.SelectionDirty = true;
            }

            if (authorRuntime.SelectionDirty || needsRuntimeBinding || !preview.IsPlaying)
                SyncAuthorPreviewSelection();

            // While playing, mirror the live stage index back onto the author selection so the
            // stage list highlights whichever stage is currently animating. We update the field
            // directly — NOT through ShowAuthorStageAtTick — to avoid seeking/interrupting playback.
            if (preview.IsPlaying)
                authorStageIndex = preview.CurrentStageIndex;

            authorPreviewPlaying = preview.IsPlaying;
            authorPreviewTick = preview.CurrentTick;
        }

        private int ComputeAuthorPreviewSourceHash()
        {
            int stageTotal = project?.stages?.Count ?? 0;
            int hash = 17;
            unchecked
            {
                hash = HashCombine(hash, stageTotal);
                if (project?.roles != null)
                {
                    for (int i = 0; i < project.roles.Count; i++)
                    {
                        var role = project.roles[i];
                        hash = HashCombine(hash, role?.roleKey?.GetHashCode() ?? 0);
                        hash = HashCombine(hash, role?.displayName?.GetHashCode() ?? 0);
                        hash = HashCombine(hash, role != null ? (int)role.genderReq : 0);
                        hash = HashCombine(hash, role?.previewDummyBodyTypeDefName?.GetHashCode() ?? 0);
                    }
                }
                if (project?.stages != null)
                {
                    for (int i = 0; i < project.stages.Count; i++)
                        hash = HashCombine(hash, ComputeStageHash(project.stages[i]));
                }
            }
            return hash;
        }

        private bool SyncAuthorPreviewSlots(int stageTotal)
        {
            bool changed = false;
            var alive = new HashSet<string>();

            if (project?.roles != null)
            {
                for (int i = 0; i < project.roles.Count; i++)
                {
                    var role = project.roles[i];
                    if (role == null || role.roleKey.NullOrEmpty())
                        continue;

                    alive.Add(role.roleKey);
                    if (!authorSlotsByKey.TryGetValue(role.roleKey, out var slot) || slot == null)
                    {
                        slot = new AuthorPreviewSlot();
                        authorSlotsByKey[role.roleKey] = slot;
                        changed = true;
                    }

                    slot.RoleKey = role.roleKey;
                    slot.Label = role.displayName ?? role.roleKey;
                    slot.GenderReq = role.genderReq;
                    if (slot.CompiledByStage == null)
                        slot.CompiledByStage = new List<AnimationDef>();
                    if (slot.CompiledByStage.Count != stageTotal)
                    {
                        ResizeCompiledStageList(slot, stageTotal);
                        changed = true;
                    }
                }
            }

            var staleKeys = authorSlotsByKey.Keys.Where(k => !alive.Contains(k)).ToList();
            for (int i = 0; i < staleKeys.Count; i++)
            {
                authorSlotsByKey.Remove(staleKeys[i]);
                changed = true;
            }

            var orderedSlots = new List<AuthorPreviewSlot>();
            if (project?.roles != null)
            {
                for (int i = 0; i < project.roles.Count; i++)
                {
                    var role = project.roles[i];
                    if (role == null || role.roleKey.NullOrEmpty())
                        continue;
                    if (authorSlotsByKey.TryGetValue(role.roleKey, out var slot) && slot != null)
                        orderedSlots.Add(slot);
                }
            }

            if (authorSlots.Count != orderedSlots.Count)
                changed = true;
            else
            {
                for (int i = 0; i < orderedSlots.Count; i++)
                {
                    if (!ReferenceEquals(authorSlots[i], orderedSlots[i]))
                    {
                        changed = true;
                        break;
                    }
                }
            }

            authorSlots.Clear();
            authorSlots.AddRange(orderedSlots);
            authorPawnPool.ClearUnused(alive);
            return changed;
        }

        private static void ResizeCompiledStageList(AuthorPreviewSlot slot, int stageTotal)
        {
            if (slot == null)
                return;
            if (slot.CompiledByStage == null)
                slot.CompiledByStage = new List<AnimationDef>();
            while (slot.CompiledByStage.Count < stageTotal)
                slot.CompiledByStage.Add(null);
            while (slot.CompiledByStage.Count > stageTotal)
                slot.CompiledByStage.RemoveAt(slot.CompiledByStage.Count - 1);
        }

        private bool EnsureCompiledStageHashCacheSize(int stageTotal)
        {
            bool changed = false;
            while (authorRuntime.CompiledStageHashes.Count < stageTotal)
            {
                authorRuntime.CompiledStageHashes.Add(int.MinValue);
                changed = true;
            }
            while (authorRuntime.CompiledStageHashes.Count > stageTotal)
            {
                authorRuntime.CompiledStageHashes.RemoveAt(authorRuntime.CompiledStageHashes.Count - 1);
                changed = true;
            }
            return changed;
        }

        private void DetectDirtyAuthorStages()
        {
            int stageTotal = project?.stages?.Count ?? 0;
            EnsureCompiledStageHashCacheSize(stageTotal);
            for (int s = 0; s < stageTotal; s++)
            {
                int stageHash = ComputeStageHash(project.stages[s]);
                if (authorRuntime.CompiledStageHashes[s] != stageHash)
                    authorRuntime.DirtyStageIndices.Add(s);
            }
        }

        private void CompileDirtyAuthorStages(int stageTotal, bool fullReset)
        {
            if (fullReset)
            {
                for (int i = 0; i < authorSlots.Count; i++)
                    ResizeCompiledStageList(authorSlots[i], stageTotal);
            }

            var dirty = authorRuntime.DirtyStageIndices.OrderBy(i => i).ToList();
            for (int di = 0; di < dirty.Count; di++)
            {
                int stageIndex = dirty[di];
                if (stageIndex < 0 || stageIndex >= stageTotal)
                    continue;

                var stage = project.stages[stageIndex];
                if (stage == null)
                    continue;

                int compiledStageHash = ComputeStageHash(stage);
                for (int r = 0; r < authorSlots.Count; r++)
                {
                    var slot = authorSlots[r];
                    if (slot == null)
                        continue;

                    ResizeCompiledStageList(slot, stageTotal);
                    var clip = GetClip(stage, slot.RoleKey);
                    EnsureClip(clip, stage.durationTicks);
                    string safeKey = (slot.RoleKey ?? ("Role" + r)).Replace(' ', '_');
                    slot.CompiledByStage[stageIndex] = AgsCompile.CompileClipToAnimationDef(clip, "AGS_" + (project?.export?.baseDefName ?? "Preview") + "_S" + stage.stageIndex + "_" + safeKey, stage.durationTicks);
                }

                authorRuntime.CompiledStageHashes[stageIndex] = compiledStageHash;
            }
        }

        private void ApplyAuthorPreviewBinding(int stageTotal)
        {
            var runtimeRoles = BuildAuthorRuntimeRoles();
            var runtimeStages = BuildAuthorRuntimeStages(stageTotal);
            preview.ConfigureForRuntime(project?.label ?? "Author Preview", runtimeRoles, runtimeStages);
        }

        private List<AgsPreviewSession.RuntimeRole> BuildAuthorRuntimeRoles()
        {
            var runtimeRoles = new List<AgsPreviewSession.RuntimeRole>();
            if (project?.roles == null)
                return runtimeRoles;

            for (int i = 0; i < project.roles.Count; i++)
            {
                var role = project.roles[i];
                if (role == null || role.roleKey.NullOrEmpty())
                    continue;

                int gender = role.genderReq == AgsModel.RoleGenderReq.Female ? 2 : 1;
                string bodyType = role.previewDummyBodyTypeDefName;
                if (bodyType.NullOrEmpty())
                    bodyType = gender == 2 ? "Female" : "Male";

                runtimeRoles.Add(new AgsPreviewSession.RuntimeRole
                {
                    Key = role.roleKey,
                    Label = role.displayName ?? role.roleKey,
                    Gender = gender,
                    BodyTypeDefName = bodyType
                });
            }

            return runtimeRoles;
        }

        private List<AgsPreviewSession.RuntimeStage> BuildAuthorRuntimeStages(int stageTotal)
        {
            var runtimeStages = new List<AgsPreviewSession.RuntimeStage>();
            for (int s = 0; s < stageTotal; s++)
            {
                var stage = project.stages[s];
                var runtimeStage = new AgsPreviewSession.RuntimeStage
                {
                    DurationTicks = Mathf.Max(1, stage?.durationTicks ?? 60),
                    RepeatCount = Mathf.Max(1, stage?.repeatCount ?? 1),
                    AnimationsByRole = new List<AnimationDef>()
                };

                for (int r = 0; r < authorSlots.Count; r++)
                {
                    var slot = authorSlots[r];
                    AnimationDef anim = null;
                    if (slot?.CompiledByStage != null && s < slot.CompiledByStage.Count)
                        anim = slot.CompiledByStage[s];
                    runtimeStage.AnimationsByRole.Add(anim);
                }

                runtimeStages.Add(runtimeStage);
            }
            return runtimeStages;
        }

        private void SyncAuthorPreviewSelection()
        {
            int stageTotal = project?.stages?.Count ?? 0;
            if (stageTotal <= 0)
            {
                authorStageIndex = 0;
                authorPreviewTick = 0;
                authorRuntime.SelectionDirty = false;
                return;
            }

            authorStageIndex = Mathf.Clamp(authorStageIndex, 0, stageTotal - 1);
            var stage = GetStage(project, authorStageIndex);
            int tick = Mathf.Clamp(authorPreviewTick, 0, Mathf.Max(1, stage?.durationTicks ?? 1));
            ShowAuthorStageAtTick(authorStageIndex, tick, seekIfPlaying: false);
        }

        private void ShowAuthorStageAtTick(int stageIndex, int tick, bool seekIfPlaying = true)
        {
            if (project == null || project.stages == null || project.stages.Count == 0)
            {
                authorStageIndex = 0;
                authorPreviewTick = 0;
                authorRuntime.SelectionDirty = false;
                return;
            }

            int clampedStage = Mathf.Clamp(stageIndex, 0, project.stages.Count - 1);
            var stage = GetStage(project, clampedStage);
            int clampedTick = Mathf.Clamp(tick, 0, Mathf.Max(1, stage?.durationTicks ?? 1));

            authorStageIndex = clampedStage;
            authorPreviewTick = clampedTick;
            preview.SelectedStageIndex = clampedStage;

            if (!preview.IsPlaying)
                preview.ShowSelectedStageAtTick(clampedTick);
            else if (seekIfPlaying)
                preview.Seek(clampedTick);

            authorRuntime.SelectionDirty = false;
        }

private sealed class NaturalComparer : IComparer<string>
        {
            public int Compare(string a, string b)
            {
                a ??= "";
                b ??= "";
                if (a == b) return 0;

                int ia = 0, ib = 0;
                while (ia < a.Length && ib < b.Length)
                {
                    char ca = a[ia];
                    char cb = b[ib];
                    bool da = char.IsDigit(ca);
                    bool db = char.IsDigit(cb);

                    if (da && db)
                    {
                        long na = 0;
                        while (ia < a.Length && char.IsDigit(a[ia])) { na = na * 10 + (a[ia] - '0'); ia++; }
                        long nb = 0;
                        while (ib < b.Length && char.IsDigit(b[ib])) { nb = nb * 10 + (b[ib] - '0'); ib++; }
                        int c = na.CompareTo(nb);
                        if (c != 0) return c;
                        continue;
                    }

                    // Compare char-insensitive
                    ca = char.ToUpperInvariant(ca);
                    cb = char.ToUpperInvariant(cb);
                    if (ca != cb) return ca.CompareTo(cb);
                    ia++; ib++;
                }

                return a.Length.CompareTo(b.Length);
            }
        }
        private static Rot4? SampleRootRotation(AnimationDef anim, int tick)
        {
            if (anim?.keyframeParts == null) return null;
            if (tick < 0) tick = 0;

            PawnRenderNodeTagDef rootTag = null;
            try { rootTag = DefDatabase<PawnRenderNodeTagDef>.GetNamedSilentFail("Root"); } catch (Exception ex) { Despicable.Core.DebugLogger.WarnExceptionOnce("Dialog_AnimGroupStudio:7", "Dialog_AnimGroupStudio ignored a non-fatal editor exception.", ex); }
            if (rootTag == null) return null;

            if (!anim.keyframeParts.TryGetValue(rootTag, out var part) || part?.keyframes == null || part.keyframes.Count == 0)
                return null;

            Verse.Keyframe prev = part.keyframes[0];
            Verse.Keyframe next = prev;

            for (int i = 0; i < part.keyframes.Count; i++)
            {
                var k = part.keyframes[i];
                if (k == null) continue;
                if (k.tick <= tick) prev = k;
                if (k.tick >= tick) { next = k; break; }
            }

            // Rotation lives on ExtendedKeyframe (base Verse.Keyframe does not expose it).
            // Old Workshop behavior is step-sampled from the nearest key.
            Rot4 rPrev = (prev as ExtendedKeyframe)?.rotation ?? Rot4.South;
            Rot4 rNext = (next as ExtendedKeyframe)?.rotation ?? Rot4.South;

            if (prev.tick == next.tick) return rPrev;

            float t = 0f;
            try { t = Mathf.InverseLerp(prev.tick, next.tick, tick); } catch (Exception ex) { Despicable.Core.DebugLogger.WarnExceptionOnce("Dialog_AnimGroupStudio:8", "Dialog_AnimGroupStudio ignored a non-fatal editor exception.", ex); }
            return (t < 0.5f) ? rPrev : rNext;
        }
    }
}
