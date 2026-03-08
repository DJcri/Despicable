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
        private void UpdateAuthorPreview(float deltaTime)
        {
            preview.Update(deltaTime);
// Guardrail-Reason: Preview lifecycle and compilation stay co-located while author-preview state remains a single seam.
            authorPreviewPlaying = preview.IsPlaying;
            authorPreviewTick = preview.CurrentTick;
        }
        private void StopAuthorPreview(bool resetTick)
        {
            preview.Stop();
            authorPreviewPlaying = false;
            authorPreviewTickAcc = 0f;
            if (resetTick)
            {
                authorPreviewTick = 0;
                preview.SelectedStageIndex = authorStageIndex;
                preview.ShowSelectedStageAtTick(0);
            }
            else
            {
                authorPreviewTick = preview.CurrentTick;
            }
        }
        private void EnsureAuthorPreviewSlots()
        {
            EnsureRoles(project);
            EnsureAuthorRoleKeyValid(project);

            // Track which keys are still used.
            var alive = new HashSet<string>();
            var ordered = new List<AuthorPreviewSlot>();

            for (int i = 0; i < project.roles.Count; i++)
            {
                var r = project.roles[i];
                if (r == null || r.roleKey.NullOrEmpty()) continue;

                alive.Add(r.roleKey);

                if (!authorSlotsByKey.TryGetValue(r.roleKey, out var slot) || slot == null)
                {
                    slot = new AuthorPreviewSlot
                    {
                        RoleKey = r.roleKey,
                        Label = r.displayName ?? r.roleKey,
                        GenderReq = r.genderReq,
                        Renderer = new WorkshopPreviewRenderer(360, 520)
                    };
                    authorSlotsByKey[r.roleKey] = slot;
                }

                slot.RoleKey = r.roleKey;
                slot.Label = r.displayName ?? r.roleKey;
                slot.GenderReq = r.genderReq;

                int g = r.genderReq == AgsModel.RoleGenderReq.Female ? 1 : 0;
                string bt = r.previewDummyBodyTypeDefName.NullOrEmpty() ? (r.genderReq == AgsModel.RoleGenderReq.Female ? "Female" : "Male") : r.previewDummyBodyTypeDefName;

                slot.Pawn = authorPawnPool.GetOrCreate(r.roleKey, g, bt);
                slot.Animator = slot.Pawn?.TryGetComp<CompExtendedAnimator>();

                ordered.Add(slot);
            }

            // Dispose and remove stale slots.
            var toRemove = new List<string>();
            foreach (var kv in authorSlotsByKey)
            {
                if (kv.Key.NullOrEmpty()) continue;
                if (!alive.Contains(kv.Key)) toRemove.Add(kv.Key);
            }
            for (int i = 0; i < toRemove.Count; i++)
            {
                string k = toRemove[i];
                try { authorSlotsByKey[k]?.Renderer?.Dispose(); } catch (Exception ex) { Despicable.Core.DebugLogger.WarnExceptionOnce("Dialog_AnimGroupStudio:5", "Dialog_AnimGroupStudio ignored a non-fatal editor exception.", ex); }
                authorSlotsByKey.Remove(k);
            }

            authorSlots.Clear();
            authorSlots.AddRange(ordered);

            // Destroy any generated preview pawns for roles that no longer exist.
            authorPawnPool.ClearUnused(alive);
        }
        private void EnsureAuthorCompiled(AgsModel.StageSpec stage)
        {
            int hash = ComputeStageHash(stage);
            if (hash == authorPreviewStageHash)
            {
                bool allReady = true;
                for (int i = 0; i < authorSlots.Count; i++)
                {
                    if (authorSlots[i]?.CompiledAnim == null) { allReady = false; break; }
                }
                if (allReady) return;
            }

            authorPreviewStageHash = hash;

            EnsureRoles(project);
            EnsureAuthorRoleKeyValid(project);

            // Compile + apply per role.
            for (int i = 0; i < project.roles.Count; i++)
            {
                var r = project.roles[i];
                if (r == null || r.roleKey.NullOrEmpty()) continue;

                var clip = GetClip(stage, r.roleKey);
                EnsureClip(clip, stage.durationTicks);

                if (!authorSlotsByKey.TryGetValue(r.roleKey, out var slot) || slot == null) continue;

                string safeKey = r.roleKey.Replace(' ', '_');
                slot.CompiledAnim = AgsCompile.CompileClipToAnimationDef(clip, $"AGS_{project?.export?.baseDefName}_S{stage.stageIndex}_{safeKey}", stage.durationTicks);

                // IMPORTANT: the workshop preview uses portrait:false rendering so we can keep roles in a shared
                // scene. ExtendedKeyframe behaviors (facing/visibility/offset interpolation) are gated behind
                // CompExtendedAnimator.hasAnimPlaying for non-portrait renders.
                // So, for authoring preview we drive the compiled animation through the animator comp rather than
                // SetAnimation() directly; this makes author playback behave like existing-def playback.
                // ALSO IMPORTANT:
                // SetAnimation() captures a start tick from PawnRenderTree.AnimationTick. We need that start tick
                // to live in the workshop tick domain (not vanilla TicksGame), otherwise sampling at
                // WorkshopRenderContext.Tick will appear frozen.
                try
                {
                    using (new WorkshopRenderContext.Scope(active: true, tick: 0))
                    {
                        slot?.Pawn?.Drawer?.renderer?.SetAnimation(slot.CompiledAnim);
                        }
                }
                catch (Exception ex) { Despicable.Core.DebugLogger.WarnExceptionOnce("Dialog_AnimGroupStudio:6", "Dialog_AnimGroupStudio ignored a non-fatal editor exception.", ex); }
            }
        }
        private static int ComputeStageHash(AgsModel.StageSpec stage)
        {
            unchecked
            {
                int h = 17;
                h *= 31 + (stage?.durationTicks ?? 0);
                h *= 31 + (stage?.repeatCount ?? 1);

                if (stage?.variants != null)
                {
                    for (int v = 0; v < stage.variants.Count; v++)
                    {
                        var variant = stage.variants[v];
                        h *= 31 + (variant?.variantId?.GetHashCode() ?? 0);
                        if (variant?.clips != null)
                        {
                            // Stable ordering for hash.
                            var ordered = variant.clips.Where(c => c != null && !c.roleKey.NullOrEmpty())
                                .OrderBy(c => c.roleKey, StringComparer.Ordinal).ToList();
                            for (int c = 0; c < ordered.Count; c++)
                            {
                                var rc = ordered[c];
                                h *= 31 + (rc.roleKey?.GetHashCode() ?? 0);
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
                h *= 31 + clip.lengthTicks;
                for (int t = 0; t < clip.tracks.Count; t++)
                {
                    var tr = clip.tracks[t];
                    h *= 31 + (tr?.nodeTag?.GetHashCode() ?? 0);
                    if (tr?.keys == null) continue;
                    for (int k = 0; k < tr.keys.Count; k++)
                    {
                        var key = tr.keys[k];
                        if (key == null) continue;
                        h *= 31 + key.tick;
                        h *= 31 + key.rotation.AsInt;
                        h *= 31 + (key.visible ? 1 : 0);
                        h *= 31 + key.offset.GetHashCode();
                        h *= 31 + key.angle.GetHashCode();
                        h *= 31 + key.scale.GetHashCode();
                        h *= 31 + (key.graphicState?.GetHashCode() ?? 0);
                        // KeySpec.variant is int (-1 means unset).
                        h *= 31 + (key.variant == -1 ? 0 : key.variant.GetHashCode());
                        h *= 31 + key.layerBias;
                        h *= 31 + (key.soundDefName?.GetHashCode() ?? 0);
                        h *= 31 + (key.facialAnimDefName?.GetHashCode() ?? 0);
                    }
                }
                return h;
            }
        }
        private void EnsureAuthorPreviewSource()
        {
            if (project == null)
            {
                preview.ConfigureForRuntime("Author Preview", new List<AgsPreviewSession.RuntimeRole>(), new List<AgsPreviewSession.RuntimeStage>());
                authorPreviewPlaying = false;
                authorPreviewTick = 0;
                return;
            }

            EnsureRoles(project);
            EnsureAuthorRoleKeyValid(project);

            int stageTotal = project.stages != null ? project.stages.Count : 0;
            int hash = 17;
            unchecked
            {
                hash *= 31 + stageTotal;
                if (project.roles != null)
                {
                    for (int i = 0; i < project.roles.Count; i++)
                    {
                        var role = project.roles[i];
                        hash *= 31 + (role?.roleKey?.GetHashCode() ?? 0);
                        hash *= 31 + (role?.displayName?.GetHashCode() ?? 0);
                        hash *= 31 + (role != null ? (int)role.genderReq : 0);
                        hash *= 31 + (role?.previewDummyBodyTypeDefName?.GetHashCode() ?? 0);
                    }
                }
                if (project.stages != null)
                {
                    for (int i = 0; i < project.stages.Count; i++)
                        hash = HashClip(hash, null) ^ ComputeStageHash(project.stages[i]);
                }
            }

            bool needsRebuild = hash != authorPreviewStageHash || preview.StageCount != stageTotal;

            var alive = new HashSet<string>();
            if (project.roles != null)
            {
                for (int i = 0; i < project.roles.Count; i++)
                {
                    var r = project.roles[i];
                    if (r == null || r.roleKey.NullOrEmpty()) continue;
                    alive.Add(r.roleKey);

                    if (!authorSlotsByKey.TryGetValue(r.roleKey, out var slot) || slot == null)
                    {
                        slot = new AuthorPreviewSlot();
                        authorSlotsByKey[r.roleKey] = slot;
                        needsRebuild = true;
                    }

                    slot.RoleKey = r.roleKey;
                    slot.Label = r.displayName ?? r.roleKey;
                    slot.GenderReq = r.genderReq;
                    if (slot.CompiledByStage == null)
                    {
                        slot.CompiledByStage = new List<AnimationDef>();
                        needsRebuild = true;
                    }
                    if (slot.CompiledByStage.Count != stageTotal)
                        needsRebuild = true;
                }
            }

            var staleKeys = authorSlotsByKey.Keys.Where(k => !alive.Contains(k)).ToList();
            for (int i = 0; i < staleKeys.Count; i++)
            {
                authorSlotsByKey.Remove(staleKeys[i]);
                needsRebuild = true;
            }

            authorSlots.Clear();
            if (project.roles != null)
            {
                for (int i = 0; i < project.roles.Count; i++)
                {
                    var r = project.roles[i];
                    if (r == null || r.roleKey.NullOrEmpty()) continue;
                    if (authorSlotsByKey.TryGetValue(r.roleKey, out var slot) && slot != null)
                        authorSlots.Add(slot);
                }
            }

            if (needsRebuild)
            {
                for (int i = 0; i < authorSlots.Count; i++)
                {
                    var slot = authorSlots[i];
                    slot.CompiledByStage.Clear();
                    for (int s = 0; s < stageTotal; s++)
                        slot.CompiledByStage.Add(null);
                }

                for (int s = 0; s < stageTotal; s++)
                {
                    var st = project.stages[s];
                    if (st == null) continue;

                    for (int r = 0; r < authorSlots.Count; r++)
                    {
                        var slot = authorSlots[r];
                        var clip = GetClip(st, slot.RoleKey);
                        EnsureClip(clip, st.durationTicks);

                        string safeKey = (slot.RoleKey ?? ("Role" + r)).Replace(' ', '_');
                        slot.CompiledByStage[s] = AgsCompile.CompileClipToAnimationDef(clip, "AGS_" + (project?.export?.baseDefName ?? "Preview") + "_S" + st.stageIndex + "_" + safeKey, st.durationTicks);
                    }
                }

                var runtimeRoles = new List<AgsPreviewSession.RuntimeRole>();
                for (int i = 0; i < project.roles.Count; i++)
                {
                    var r = project.roles[i];
                    if (r == null || r.roleKey.NullOrEmpty()) continue;

                    int gender = 1;
                    if (r.genderReq == AgsModel.RoleGenderReq.Female) gender = 2;
                    else if (r.genderReq == AgsModel.RoleGenderReq.Male) gender = 1;
                    else gender = 1;

                    string bodyType = r.previewDummyBodyTypeDefName;
                    if (bodyType.NullOrEmpty())
                        bodyType = gender == 2 ? "Female" : "Male";

                    runtimeRoles.Add(new AgsPreviewSession.RuntimeRole
                    {
                        Key = r.roleKey,
                        Label = r.displayName ?? r.roleKey,
                        Gender = gender,
                        BodyTypeDefName = bodyType
                    });
                }

                var runtimeStages = new List<AgsPreviewSession.RuntimeStage>();
                for (int s = 0; s < stageTotal; s++)
                {
                    var st = project.stages[s];
                    var runtimeStage = new AgsPreviewSession.RuntimeStage
                    {
                        DurationTicks = Mathf.Max(1, st?.durationTicks ?? 60),
                        RepeatCount = Mathf.Max(1, st?.repeatCount ?? 1),
                        AnimationsByRole = new List<AnimationDef>()
                    };

                    for (int r = 0; r < authorSlots.Count; r++)
                        runtimeStage.AnimationsByRole.Add(authorSlots[r]?.CompiledByStage != null && s < authorSlots[r].CompiledByStage.Count ? authorSlots[r].CompiledByStage[s] : null);

                    runtimeStages.Add(runtimeStage);
                }

                preview.ConfigureForRuntime(project?.label ?? "Author Preview", runtimeRoles, runtimeStages);
                preview.SetSpeed(authorPreviewSpeed);
                authorPreviewStageHash = hash;
            }

            if (stageTotal > 0)
            {
                authorStageIndex = Mathf.Clamp(authorStageIndex, 0, stageTotal - 1);
                preview.SelectedStageIndex = authorStageIndex;
                if (!preview.IsPlaying)
                {
                    var stage = GetStage(project, authorStageIndex);
                    int tick = Mathf.Clamp(authorPreviewTick, 0, Mathf.Max(1, stage?.durationTicks ?? 1));
                    preview.ShowSelectedStageAtTick(tick);
                }
            }
            else
            {
                authorStageIndex = 0;
                authorPreviewTick = 0;
            }

            authorPreviewPlaying = preview.IsPlaying;
            authorPreviewTick = preview.CurrentTick;
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
