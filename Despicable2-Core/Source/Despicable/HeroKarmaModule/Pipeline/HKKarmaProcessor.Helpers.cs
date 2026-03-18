using System;
using System.Collections.Generic;
using Despicable.HeroKarma.Patches.HeroKarma;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

// Guardrail-Reason: Karma processor helpers stay together because token building and downstream scoring share one event-processing pass.
namespace Despicable.HeroKarma;
public static partial class HKKarmaProcessor
{
    private static IEnumerable<IHKEffectToken> BuildTokens(KarmaEvent ev, string canonicalEventKey)
    {
        var list = new List<IHKEffectToken>();

        if (!HKSettingsUtil.EnableLocalRep)
            return list;

        try
        {
            switch (canonicalEventKey)
            {
                case "TendOutsider":
                    AddPawnRep(list, ev, HKBalanceTuning.LocalRepEvents.TendOutsiderPawn, "Helped");
                    AddFactionRep(list, ev.targetFactionId, HKBalanceTuning.LocalRepEvents.TendOutsiderFaction, ev.eventKey, "Helped faction member");
                    AddSettlementRepFromEventContext(list, ev, HKBalanceTuning.LocalRepEvents.TendOutsiderSettlement, ev.eventKey, "Help noted locally");
                    break;

                case "ReleasePrisoner":
                    AddPawnRep(list, ev, HKBalanceTuning.LocalRepEvents.ReleasePrisonerPawn, "Released");
                    AddFactionRep(list, ev.targetFactionId, HKBalanceTuning.LocalRepEvents.ReleasePrisonerFaction, ev.eventKey, "Released their member");
                    AddSettlementRepFromEventContext(list, ev, HKBalanceTuning.LocalRepEvents.ReleasePrisonerSettlement, ev.eventKey, "Release noted locally");
                    break;

                case "ExecutePrisoner":
                    bool executeTargetIsGuilty = HKHookUtil.IsCurrentlyGuilty(ev.targetPawn);
                    AddFactionRep(list, ev.targetFactionId, HKBalanceTuning.GetExecutePrisonerPenalty(HKBalanceTuning.LocalRepEvents.ExecutePrisonerFaction, executeTargetIsGuilty), ev.eventKey, "Executed their member");
                    AddPawnRep(list, ev, HKBalanceTuning.GetExecutePrisonerPenalty(HKBalanceTuning.LocalRepEvents.ExecutePrisonerPawn, executeTargetIsGuilty), "Executed");
                    AddSettlementRepFromEventContext(list, ev, HKBalanceTuning.GetExecutePrisonerPenalty(HKBalanceTuning.LocalRepEvents.ExecutePrisonerSettlement, executeTargetIsGuilty), ev.eventKey, "Execution noted locally");
                    break;

                case "EnslaveAttempt":
                    AddPawnRep(list, ev, HKBalanceTuning.LocalRepEvents.EnslaveAttemptPawn, "Attempted enslavement");
                    AddFactionRep(list, ev.targetFactionId, HKBalanceTuning.LocalRepEvents.EnslaveAttemptFaction, ev.eventKey, "Attempted enslavement");
                    AddSettlementRepFromEventContext(list, ev, HKBalanceTuning.LocalRepEvents.EnslaveAttemptSettlement, ev.eventKey, "Coercion noted locally");
                    break;

                case "OrganHarvest":
                    AddFactionRep(list, ev.targetFactionId, HKBalanceTuning.LocalRepEvents.OrganHarvestFaction, ev.eventKey, "Organ harvested");
                    AddPawnRep(list, ev, HKBalanceTuning.LocalRepEvents.OrganHarvestPawn, "Organ harvested");
                    break;

                case "CharityGift":
                    int delta = HKBalanceTuning.GetCharityGiftLocalRepFactionDelta(ev.amount);
                    if (delta != 0)
                        AddFactionRep(list, ev.targetFactionId, +delta, ev.eventKey, "Gift given");

                    int settlementDelta = HKBalanceTuning.GetCharityGiftLocalRepSettlementDelta(ev.amount);
                    if (settlementDelta != 0)
                        AddSettlementRepFromEventContext(list, ev, +settlementDelta, ev.eventKey, "Gift noted locally");
                    break;

                case "ArrestNeutral":
                    AddPawnRep(list, ev, HKBalanceTuning.LocalRepEvents.ArrestNeutralPawn, "Arrested");
                    AddFactionRep(list, ev.targetFactionId, HKBalanceTuning.LocalRepEvents.ArrestNeutralFaction, ev.eventKey, "Arrested their member");
                    AddSettlementRepFromEventContext(list, ev, HKBalanceTuning.LocalRepEvents.ArrestNeutralSettlement, ev.eventKey, "Arrest noted locally");
                    break;

                case "RescueOutsider":
                    AddPawnRep(list, ev, HKBalanceTuning.LocalRepEvents.RescueOutsiderPawn, "Rescued");
                    AddFactionRep(list, ev.targetFactionId, HKBalanceTuning.LocalRepEvents.RescueOutsiderFaction, ev.eventKey, "Rescued their member");
                    AddSettlementRepFromEventContext(list, ev, HKBalanceTuning.LocalRepEvents.RescueOutsiderSettlement, ev.eventKey, "Rescue noted locally");
                    break;

                case "StabilizeOutsider":
                    AddPawnRep(list, ev, HKBalanceTuning.LocalRepEvents.StabilizeOutsiderPawn, "Stabilized");
                    AddFactionRep(list, ev.targetFactionId, HKBalanceTuning.LocalRepEvents.StabilizeOutsiderFaction, ev.eventKey, "Stabilized their member");
                    AddSettlementRepFromEventContext(list, ev, HKBalanceTuning.LocalRepEvents.StabilizeOutsiderSettlement, ev.eventKey, "Aid noted locally");
                    break;

                case "KillDownedNeutral":
                    bool downedTargetIsGuilty = HKHookUtil.IsCurrentlyGuilty(ev.targetPawn);
                    AddPawnRep(list, ev, HKBalanceTuning.GetKillDownedNeutralPenalty(HKBalanceTuning.LocalRepEvents.KillDownedNeutralPawn, downedTargetIsGuilty), "Killed while downed");
                    AddFactionRep(list, ev.targetFactionId, HKBalanceTuning.GetKillDownedNeutralPenalty(HKBalanceTuning.LocalRepEvents.KillDownedNeutralFaction, downedTargetIsGuilty), ev.eventKey, "Killed their downed member");
                    AddSettlementRepFromEventContext(list, ev, HKBalanceTuning.GetKillDownedNeutralPenalty(HKBalanceTuning.LocalRepEvents.KillDownedNeutralSettlement, downedTargetIsGuilty), ev.eventKey, "Killing noted locally");
                    break;

                case "HarmGuest":
                    int gw = HKBalanceTuning.GetAttackSeverityWeight(ev.stage);
                    bool guestTargetIsGuilty = HKHookUtil.IsCurrentlyGuilty(ev.targetPawn);
                    AddPawnRep(list, ev, HKBalanceTuning.GetHarmGuestPenalty(-3 * gw, guestTargetIsGuilty), "Harmed guest");
                    AddFactionRep(list, ev.targetFactionId, HKBalanceTuning.GetHarmGuestPenalty(-4 * gw, guestTargetIsGuilty), ev.eventKey, "Harmed their guest");
                    AddSettlementRepFromEventContext(list, ev, HKBalanceTuning.GetHarmGuestPenalty(HKBalanceTuning.GetHarmGuestLocalRepSettlementDelta(ev.stage), guestTargetIsGuilty), ev.eventKey, "Violence noted locally");
                    break;

                case "HarmColonyAnimal":
                    AddPawnRep(list, ev.targetPawnId, HKBalanceTuning.GetHarmColonyAnimalPawnDelta(ev.stage), ev.eventKey, "Harmed colony animal");
                    AddSettlementRepFromEventContext(list, ev, GetAnimalPersonhoodAdjustedSettlementRepDelta(ev, HKBalanceTuning.GetHarmColonyAnimalSettlementDelta(ev.stage)), ev.eventKey, "Animal cruelty noted locally");
                    break;

                case "FreeSlave":
                    AddPawnRep(list, ev, HKBalanceTuning.LocalRepEvents.FreeSlavePawn, "Freed");
                    AddFactionRep(list, ev.targetFactionId, HKBalanceTuning.LocalRepEvents.FreeSlaveFaction, ev.eventKey, "Freed their member");
                    AddSettlementRepFromEventContext(list, ev, HKBalanceTuning.LocalRepEvents.FreeSlaveSettlement, ev.eventKey, "Emancipation noted locally");
                    break;

                case "DonateToBeggars":
                    int bdelta = HKBalanceTuning.GetDonateToBeggarsLocalRepFactionDelta(ev.amount);
                    if (bdelta != 0)
                        AddFactionRep(list, ev.targetFactionId, +bdelta, ev.eventKey, "Donation");

                    int bSettlementDelta = HKBalanceTuning.GetDonateToBeggarsLocalRepSettlementDelta(ev.amount);
                    if (bSettlementDelta != 0)
                        AddSettlementRepFromEventContext(list, ev, +bSettlementDelta, ev.eventKey, "Donation noted locally");
                    break;

                case HKSettingsUtil.EventSellCaptive:
                    AddPawnRep(list, ev, HKBalanceTuning.LocalRepEvents.SellCaptivePawn, "Sold captive");
                    AddFactionRep(list, ev.targetFactionId, HKBalanceTuning.LocalRepEvents.SellCaptiveFaction, ev.eventKey, "Sold their captive member");
                    AddSettlementRepFromEventContext(list, ev, HKBalanceTuning.LocalRepEvents.SellCaptiveSettlement, ev.eventKey, "Captive sale noted locally");
                    break;

                case "AttackNeutral":
                    int w = HKBalanceTuning.GetAttackSeverityWeight(ev.stage);
                    AddPawnRep(list, ev, -2 * w, "Attacked");
                    AddFactionRep(list, ev.targetFactionId, -3 * w, ev.eventKey, "Attacked their member");
                    AddSettlementRepFromEventContext(list, ev, HKBalanceTuning.GetAttackNeutralLocalRepSettlementDelta(ev.stage), ev.eventKey, "Violence noted locally");
                    break;
            }
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce("HKKarmaProcessor.BuildTokens", "Hero Karma failed to build one or more local reputation tokens.", ex);
        }

        return list;
    }

    private static void AddPawnRep(List<IHKEffectToken> list, KarmaEvent ev, int baseDelta, string reason)
    {
        if (ev == null)
            return;

        int delta = GetIdeologyAdjustedPawnRepDelta(ev, baseDelta, out string affectedByLabel);
        AddPawnRep(list, ev.targetPawnId, delta, ev.eventKey, reason, baseDelta, affectedByLabel);
    }

    private static void AddPawnRep(List<IHKEffectToken> list, string targetPawnId, int delta, string eventKey, string reason, int baseDelta = 0, string affectedByLabel = null)
    {
        if (list == null) return;
        if (targetPawnId.NullOrEmpty()) return;
        if (delta == 0) return;
        list.Add(new HKToken_LocalRepPawn(targetPawnId, delta, eventKey, reason, baseDelta, affectedByLabel));
    }

    private static int GetIdeologyAdjustedPawnRepDelta(KarmaEvent ev, int baseDelta, out string affectedByLabel)
    {
        affectedByLabel = null;
        if (ev == null || baseDelta == 0 || ev.targetPawn == null)
            return baseDelta;

        HKRepSemantic semantic = HKRepSemanticResolver.Classify(ev);
        if (semantic == HKRepSemantic.None)
            return baseDelta;

        HKRepIdeologyModifier modifier = HKRepIdeologyModifierResolver.GetModifier(ev.targetPawn, semantic, baseDelta, ev.eventKey, out string matchedPreceptDefName);
        int finalDelta = HKRepIdeologyMath.ApplyAndRound(baseDelta, modifier);
        affectedByLabel = ResolveIdeologyRuleLabel(matchedPreceptDefName);

        HKIdeologyEvaluationTrace trace = HKIdeologyEvaluationTrace.GetOrCreate(ev);
        if (trace != null)
        {
            trace.RecordReputation(semantic, baseDelta, finalDelta, matchedPreceptDefName, modifier);

            if (semantic == HKRepSemantic.HarshPunishment)
            {
                try
                {
                    trace.WasGuiltyContext = HKHookUtil.IsCurrentlyGuilty(ev.targetPawn);
                }
                catch (Exception ex)
                {
                    trace.AddNote("guilt resolver error: " + ex.GetType().Name);
                }
            }

            if (matchedPreceptDefName.NullOrEmpty())
                trace.AddNote("reputation used no exact ideology rule");
            else if (matchedPreceptDefName.StartsWith("meme:", StringComparison.OrdinalIgnoreCase))
                trace.AddNote("reputation used meme baseline");
        }

        return finalDelta;
    }

    private static int GetAnimalPersonhoodAdjustedSettlementRepDelta(KarmaEvent ev, int baseDelta)
    {
        if (ev == null || baseDelta == 0)
            return baseDelta;

        HKRepIdeologyModifier modifier = HKRepIdeologyModifierResolver.GetPlayerFactionSettlementModifierForAnimalCruelty(baseDelta, out string matchedRuleDefName);
        int finalDelta = HKRepIdeologyMath.ApplyAndRound(baseDelta, modifier);

        if (!matchedRuleDefName.NullOrEmpty())
        {
            HKIdeologyEvaluationTrace trace = HKIdeologyEvaluationTrace.GetOrCreate(ev);
            string label = ResolveIdeologyRuleLabel(matchedRuleDefName);
            if (trace != null && !label.NullOrEmpty())
                trace.AddNote("settlement reaction affected by " + label);
        }

        return finalDelta;
    }

    private static string ResolveIdeologyRuleLabel(string matchedRuleDefName)
    {
        if (matchedRuleDefName.NullOrEmpty())
            return null;

        try
        {
            if (matchedRuleDefName.StartsWith("meme:", StringComparison.OrdinalIgnoreCase))
            {
                string memeDefName = matchedRuleDefName.Substring("meme:".Length);
                MemeDef memeDef = DefDatabase<MemeDef>.GetNamedSilentFail(memeDefName);
                return memeDef?.LabelCap ?? memeDef?.label ?? memeDefName;
            }

            PreceptDef preceptDef = DefDatabase<PreceptDef>.GetNamedSilentFail(matchedRuleDefName);
            return preceptDef?.LabelCap ?? preceptDef?.label ?? matchedRuleDefName;
        }
        catch
        {
            return matchedRuleDefName;
        }
    }

    private static void AddFactionRep(List<IHKEffectToken> list, int factionId, int delta, string eventKey, string reason)
    {
        if (list == null) return;
        if (factionId <= 0) return;
        if (delta == 0) return;
        list.Add(new HKToken_LocalRepFaction(factionId, delta, eventKey, reason));
    }

    private static void AddSettlementRepFromEventContext(List<IHKEffectToken> list, KarmaEvent ev, int delta, string eventKey, string reason)
    {
        if (list == null) return;
        if (delta == 0) return;
        if (Mathf.Approximately(HKBalanceTuning.LocalRep.SettlementEchoToPawnFactor, 0f)) return;

        if (ev != null && !ev.settlementUniqueId.NullOrEmpty())
        {
            list.Add(new HKToken_LocalRepSettlement(ev.settlementUniqueId, delta, eventKey, reason, recordRecent: false));
            HKIdeologyEvaluationTrace.GetOrCreate(ev)?.AddSettlementDelta(delta);
            return;
        }

        if (TryAddSettlementRepFromPawnContext(list, ev, ev?.actor, delta, eventKey, reason))
            return;

        if (!TryAddSettlementRepFromPawnContext(list, ev, ev?.targetPawn, delta, eventKey, reason))
            HKIdeologyEvaluationTrace.GetOrCreate(ev)?.AddNote("settlement lane skipped: unresolved context");
    }

    private static bool TryAddSettlementRepFromPawnContext(List<IHKEffectToken> list, KarmaEvent ev, Pawn pawn, int delta, string eventKey, string reason)
    {
        if (list == null) return false;
        if (pawn == null) return false;
        if (delta == 0) return false;
        if (Mathf.Approximately(HKBalanceTuning.LocalRep.SettlementEchoToPawnFactor, 0f)) return false;

        if (!global::Despicable.PawnContext.TryResolveWordOfMouthSettlement(pawn, out string settlementUniqueId, out _)) return false;

        list.Add(new HKToken_LocalRepSettlement(settlementUniqueId, delta, eventKey, reason, recordRecent: false));
        HKIdeologyEvaluationTrace.GetOrCreate(ev)?.AddSettlementDelta(delta);
        return true;
    }

    private static string BuildDetail(KarmaEvent ev, string canonicalEventKey)
    {
        string core = "";
        try
        {
            if (ev.targetPawn != null)
            {
                string n = SafePawnName(ev.targetPawn);
                if (!n.NullOrEmpty()) core = n;
            }

            if (core.NullOrEmpty() && ev.targetFactionId > 0)
            {
                var f = HKResolve.TryResolveFactionById(ev.targetFactionId);
                if (f != null) core = f.Name;
            }
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce("HKKarmaProcessor.BuildDetail.Core", "Hero Karma failed to build the base event detail text.", ex);
        }

        try
        {
            switch (canonicalEventKey)
            {
                case "CharityGift":
                case "DonateToBeggars":
                    string giftDetail = ev.amount > 0
                        ? core + (core.NullOrEmpty() ? "" : " ") + "(value " + ev.amount.ToString() + ")"
                        : (core ?? "");
                    return AppendIdeologyExplainability(ev, AppendSettlementLabel(ev, giftDetail));

                case "AttackNeutral":
                case "HarmGuest":
                    string s = ev.stage >= 2 ? "kill" : (ev.stage == 1 ? "down" : "hit");
                    return AppendIdeologyExplainability(ev, AppendSettlementLabel(ev, core + (core.NullOrEmpty() ? "" : " ") + "(" + s + ")"));

                case "TendOutsider":
                case "RescueOutsider":
                case "StabilizeOutsider":
                case "KillDownedNeutral":
                case "ArrestNeutral":
                case "ExecutePrisoner":
                case "EnslaveAttempt":
                case HKSettingsUtil.EventSellCaptive:
                    return AppendIdeologyExplainability(ev, AppendSettlementLabel(ev, core));
            }
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce("HKKarmaProcessor.BuildDetail.Suffix", "Hero Karma failed to build the event detail suffix.", ex);
        }

        return AppendIdeologyExplainability(ev, core ?? "");
    }

    private static string AppendIdeologyExplainability(KarmaEvent ev, string detail)
    {
        HKIdeologyEvaluationTrace trace = ev?.ideologyTrace;
        if (trace == null || !trace.HasData)
            return detail ?? "";

        if (!ShouldExposeIdeologyExplainabilityInDetail())
            return detail ?? "";

        string explain = trace.BuildCompactDetailLine();
        if (explain.NullOrEmpty())
            return detail ?? "";

        if (detail.NullOrEmpty())
            return explain;

        return detail + "\n" + explain;
    }

    private static bool ShouldExposeIdeologyExplainabilityInDetail()
    {
        if (Prefs.DevMode)
            return true;

        try { return HKSettingsUtil.DebugUI; }
        catch { return false; }
    }

    private static string AppendSettlementLabel(KarmaEvent ev, string detail)
    {
        string settlementLabel = TryResolveEventSettlementLabel(ev);
        if (settlementLabel.NullOrEmpty())
            return detail ?? "";

        if (!detail.NullOrEmpty())
            return detail + " at " + settlementLabel;

        return settlementLabel;
    }

    private static string TryResolveEventSettlementLabel(KarmaEvent ev)
    {
        if (ev == null) return null;
        if (!ev.settlementLabel.NullOrEmpty()) return ev.settlementLabel;

        try
        {
            if (!ev.settlementUniqueId.NullOrEmpty())
            {
                var worldObjects = Find.WorldObjects?.AllWorldObjects;
                if (worldObjects != null)
                {
                    for (int i = 0; i < worldObjects.Count; i++)
                    {
                        if (worldObjects[i] is Settlement settlement && settlement.GetUniqueLoadID() == ev.settlementUniqueId)
                            return settlement.LabelCap;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "HKKarmaProcessor.ResolveEventSettlementLabel.UniqueId",
                "Hero Karma failed to resolve a settlement label for an event by world object id.",
                ex);
        }

        string fallback = TryResolveSettlementLabelFromPawn(ev.targetPawn) ?? TryResolveSettlementLabelFromPawn(ev.actor);
        return fallback;
    }

    private static string TryResolveSettlementLabelFromPawn(Pawn pawn)
    {
        try
        {
            if (global::Despicable.PawnContext.TryResolveWordOfMouthSettlement(pawn, out Settlement settlement) && settlement != null)
                return settlement.LabelCap;
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "HKKarmaProcessor.ResolveEventSettlementLabel.Pawn",
                "Hero Karma failed to resolve a settlement label from pawn context for an event.",
                ex);
        }

        return null;
    }

    private static string SafePawnName(Pawn p)
    {
        try
        {
            if (p == null) return null;
            if (p.Name != null) return p.Name.ToStringShort;
            return p.LabelShortCap;
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce("HKKarmaProcessor.SafePawnName", "Hero Karma failed to read a pawn name for event details.", ex);
            return null;
        }
    }
}
