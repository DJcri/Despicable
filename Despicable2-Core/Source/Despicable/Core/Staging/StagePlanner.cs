using Despicable; // AnimGroupDef/AnimRoleDef live here
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Despicable.Core.Staging;
// Guardrail-Reason: Stage planning stays together while provider selection and slot assignment remain one decision pipeline.
public static class StagePlanner
{
    /// <summary>
    /// Internal candidate representation. Either comes from StageClipDef (future)
    /// or is a legacy adapter projection of AnimGroupDef (current).
    /// </summary>
    public sealed class StageCandidate
    {
        public string DebugId;
        public string BackendKey;
        public string PlaybackBackendKey;
        public StageAnchorMode AnchorMode;
        public List<string> StageTags;
        public List<StageSlotDef> Slots;
    }

    public static bool TryPlan(
        Map map,
        StageRequest req,
        Despicable.Core.InteractionContext interactionContext,
        out StagePlan plan,
        out string reason)
    {
        plan = null;
        reason = null;

        if (map == null) { reason = "NoMap"; return false; }
        if (req == null) { reason = "NoRequest"; return false; }
        if (req.Participants == null || req.Participants.Count == 0) { reason = "NoParticipants"; return false; }

        var ctx = new StagePlanContext(map, req, interactionContext);

        // Build candidates via providers (StageClipDef, legacy adapters, etc.)
        var candidates = new List<StageCandidate>(64);
        StageCandidateProviders.AddCandidates(req, candidates);


        if (candidates.Count == 0)
        {
            reason = "NoCandidates";
            return false;
        }

        // Anchor mode check
        bool anchorIsBed = req.Anchor.Thing is Building_Bed;
        candidates.RemoveAll(c =>
            (c.AnchorMode == StageAnchorMode.BedOnly && !anchorIsBed) ||
            (c.AnchorMode == StageAnchorMode.StandingOnly && anchorIsBed));

        if (candidates.Count == 0)
        {
            reason = "NoCandidatesAfterAnchorFilter";
            return false;
        }

        // Tag context for providers
        var tagCtx = new StageTagContext(map, req, interactionContext);

        // Precompute pawn tags
        var pawns = req.Participants;
        var pawnTags = new HashSet<string>[pawns.Count];
        for (int i = 0; i < pawns.Count; i++)
        {
            pawnTags[i] = new HashSet<string>();
            StageTagProviders.GetTagsForPawn(pawns[i], tagCtx, pawnTags[i]);
        }

        StageCandidate best = null;
        Dictionary<string, Pawn> bestAssign = null;
        float bestScore = float.NegativeInfinity;

        var hooks = StagePlanHooks.SnapshotUnsafe();

        for (int i = 0; i < candidates.Count; i++)
        {
            var cand = candidates[i];
            if (cand?.Slots == null) continue;

            if (cand.Slots.Count != pawns.Count) continue;

            // Allow hooks to veto candidate
            bool allowed = true;
            for (int h = 0; h < hooks.Count; h++)
            {
                var hook = hooks[h];
                if (hook == null) continue;

                if (!hook.AllowCandidate(cand, ctx, out var why))
                {
                    allowed = false;
                    reason = why ?? "BlockedByStagePlanHook";
                    break;
                }
            }
            if (!allowed) continue;

            // Try assignment
            if (!TryAssignSlots(cand.Slots, pawns, pawnTags, out var assignment))
                continue;

            // Score
            float score = 0f;
            for (int h = 0; h < hooks.Count; h++)
            {
                var hook = hooks[h];
                if (hook == null) continue;
                score += hook.Score(cand, assignment, ctx);
            }

            // If nobody scores anything, pick randomly among valid ones (stable-ish).
            if (hooks.Count == 0) score = Rand.Value;

            if (score > bestScore)
            {
                bestScore = score;
                best = cand;
                bestAssign = assignment;
            }
        }

        if (best == null || bestAssign == null)
        {
            reason ??= "NoValidAssignments";
            return false;
        }

        plan = new StagePlan
        {
            StageTag = req.StageTag,
            BackendKey = best.BackendKey,
            PlaybackBackendKey = best.PlaybackBackendKey,
            Anchor = req.Anchor
        };

        foreach (var kv in bestAssign)
            plan.SlotAssignments[kv.Key] = kv.Value;

        return true;
    }

    // --------------------------------------------------------------------
    // Candidate providers
    // --------------------------------------------------------------------

    public interface IStageCandidateProvider
    {
        /// <summary>Add candidate stage clips for this request. Providers may append any number of candidates.</summary>
        void AddCandidates(StageRequest req, List<StageCandidate> into);
    }

    public static class StageCandidateProviders
    {
        private static readonly List<IStageCandidateProvider> providers = new(4);

        static StageCandidateProviders()
        {
            // Default providers (order matters: newer/native first, then legacy adapters).
            Register(new StageClipDefCandidateProvider());
            Register(new LegacyAnimGroupCandidateProvider());
        }

        public static void Register(IStageCandidateProvider provider)
        {
            if (provider == null) return;
            if (providers.Contains(provider)) return;
            providers.Add(provider);
        }

        public static void AddCandidates(StageRequest req, List<StageCandidate> into)
        {
            if (into == null) return;
            for (int i = 0; i < providers.Count; i++)
            {
                var p = providers[i];
                if (p == null) continue;
                p.AddCandidates(req, into);
            }
        }
    }

    private sealed class StageClipDefCandidateProvider : IStageCandidateProvider
    {
        public void AddCandidates(StageRequest req, List<StageCandidate> into)
        {
            var all = DefDatabase<StageClipDef>.AllDefsListForReading;
            if (all == null || all.Count == 0) return;

            for (int i = 0; i < all.Count; i++)
            {
                var def = all[i];
                if (def == null) continue;

                if (!req.StageTag.NullOrEmpty())
                {
                    if (def.stageTags == null || !def.stageTags.Contains(req.StageTag))
                        continue;
                }

                bool hasTimeline = def.stages != null && def.stages.Count > 0;

                // Payload key:
                // - Timeline clips: always use the StageClipDef.defName as the payload.
                // - v1 clips (no timeline): preserve legacy behavior (backendKey or defName).
                string payloadKey = hasTimeline ? def.defName : (def.backendKey.NullOrEmpty() ? def.defName : def.backendKey);

                // Playback backend selector:
                // - If author provided playbackBackendKey, honor it.
                // - Else, for timeline clips we default to stageClipTimeline.
                string playbackKey = !def.playbackBackendKey.NullOrEmpty()
                    ? def.playbackBackendKey
                    : (hasTimeline ? "stageClipTimeline" : null);

                into.Add(new StageCandidate
                {
                    DebugId = $"StageClipDef:{def.defName}",
                    BackendKey = payloadKey,
                    PlaybackBackendKey = playbackKey,
                    AnchorMode = def.anchorMode,
                    StageTags = def.stageTags,
                    Slots = def.slots
                });
            }
        }
    }

    private sealed class LegacyAnimGroupCandidateProvider : IStageCandidateProvider
    {
        public void AddCandidates(StageRequest req, List<StageCandidate> into)
        {
            var all = DefDatabase<AnimGroupDef>.AllDefsListForReading;
            if (all == null || all.Count == 0) return;

            for (int i = 0; i < all.Count; i++)
            {
                var g = all[i];
                if (g == null) continue;

                if (!req.StageTag.NullOrEmpty())
                {
                    if (g.stageTags == null || !g.stageTags.Contains(req.StageTag))
                        continue;
                }

                if (g.animRoles == null || g.animRoles.Count <= 0) continue;

                bool useReproTags = UsesReproTagsForStageTags(g.stageTags);
                string maleTag = useReproTags ? "repro_male" : "male";
                string femaleTag = useReproTags ? "repro_female" : "female";

                var slots = new List<StageSlotDef>(g.animRoles.Count);
                for (int r = 0; r < g.animRoles.Count; r++)
                {
                    var role = g.animRoles[r];
                    if (role == null) continue;

                    var slot = new StageSlotDef
                    {
                        slotId = role.defName,
                        offset = null,
                        facing = StageFacingRule.FaceAnchor
                    };

                    // Legacy gender constraint -> required tags
                    // RimWorld Gender: Male=1, Female=2
                    if (role.gender == 1)
                        slot.requiredPawnTags = new List<string> { maleTag };
                    else if (role.gender == 2)
                        slot.requiredPawnTags = new List<string> { femaleTag };

                    slots.Add(slot);
                }

                into.Add(new StageCandidate
                {
                    DebugId = $"AnimGroupDef:{g.defName}",
                    BackendKey = g.defName, // RAF backend uses AnimGroupDef defName
                    PlaybackBackendKey = "rafAnimGroup",
                    AnchorMode = StageAnchorMode.Any,
                    StageTags = g.stageTags,
                    Slots = slots
                });
            }
        }
    }

    private static bool UsesReproTagsForStageTags(List<string> stageTags)
    {
        if (stageTags == null || stageTags.Count <= 0) return false;

        for (int i = 0; i < stageTags.Count; i++)
        {
            var tag = stageTags[i];
            if (tag == "Vaginal" || tag == "Anal" || tag == "Blowjob" || tag == "Cunnilingus")
                return true;
        }

        return false;
    }

    private static bool TryAssignSlots(
        List<StageSlotDef> slots,
        List<Pawn> pawns,
        HashSet<string>[] pawnTags,
        out Dictionary<string, Pawn> assignment)
    {
        // IMPORTANT: don't capture an out param in a local function (C# disallows it).
        // Use a local variable instead, and assign it to the out param at the end.
        var result = new Dictionary<string, Pawn>(slots.Count);

        var used = new bool[pawns.Count];

        bool Recurse(int slotIndex)
        {
            if (slotIndex >= slots.Count) return true;

            var slot = slots[slotIndex];
            if (slot == null || slot.slotId.NullOrEmpty()) return false;

            for (int p = 0; p < pawns.Count; p++)
            {
                if (used[p]) continue;

                var pawn = pawns[p];
                if (pawn == null) continue;

                if (!PawnFitsSlot(slot, pawnTags[p])) continue;

                used[p] = true;
                result[slot.slotId] = pawn;

                if (Recurse(slotIndex + 1)) return true;

                used[p] = false;
                result.Remove(slot.slotId);
            }

            return false;
        }

        var ok = Recurse(0);
        assignment = ok ? result : null;
        return ok;
    }

    private static bool PawnFitsSlot(StageSlotDef slot, HashSet<string> tags)
    {
        if (slot.requiredPawnTags != null)
        {
            for (int i = 0; i < slot.requiredPawnTags.Count; i++)
            {
                var t = slot.requiredPawnTags[i];
                if (t.NullOrEmpty()) continue;
                if (!tags.Contains(t)) return false;
            }
        }

        if (slot.forbiddenPawnTags != null)
        {
            for (int i = 0; i < slot.forbiddenPawnTags.Count; i++)
            {
                var t = slot.forbiddenPawnTags[i];
                if (t.NullOrEmpty()) continue;
                if (tags.Contains(t)) return false;
            }
        }

        return true;
    }
}
