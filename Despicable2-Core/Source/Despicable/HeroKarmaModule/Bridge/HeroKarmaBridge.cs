using System.Collections.Generic;
using Verse;

namespace Despicable.HeroKarma;
/// <summary>
/// Thin facade ("front desk") into the HeroKarma system.
/// No state, no gameplay logic, no patches.
/// </summary>
public static class HeroKarmaBridge
{
    public static GameComponent_HeroKarma GC
        => Current.Game?.GetComponent<GameComponent_HeroKarma>();

    public static Pawn GetHeroPawnSafe()
        => HKSettingsUtil.ModuleEnabled ? GC?.ResolveHeroPawnSafe() : null;

    public static void SetHero(Pawn pawn)
    {
        if (!HKSettingsUtil.ModuleEnabled) return;

        var gc = GC;
        if (gc == null) return;
        gc.SetHero(pawn);
    }

    public static int GetGlobalKarma()
        => HKSettingsUtil.ModuleEnabled ? (GC?.GlobalKarma ?? 0) : 0;

    public static int GetGlobalStanding()
        => HKSettingsUtil.ModuleEnabled ? (GC?.GlobalStanding ?? 0) : 0;

    public static IEnumerable<HKLedgerRow> GetLedgerRowsForUI(int cap = 40)
    {
        if (!HKSettingsUtil.ModuleEnabled)
            yield break;

        var gc = GC;
        if (gc == null) yield break;
        foreach (var r in gc.BuildLedgerRowsForUI(GetHeroPawnSafe(), cap))
            yield return r;
    }

    public static void ApplyOutcome(int karmaDelta, string eventKey, string label, string detail, string reason,
        IEnumerable<IHKEffectToken> tokens, string targetPawnId = null, int targetFactionId = 0)
    {
        ApplyOutcome(karmaDelta, 0, eventKey, label, detail, reason, null, tokens, targetPawnId, targetFactionId);
    }

    public static void ApplyOutcome(int karmaDelta, int standingDelta, string eventKey, string label, string detail,
        string karmaReason, string standingReason,
        IEnumerable<IHKEffectToken> tokens, string targetPawnId = null, int targetFactionId = 0)
    {
        if (!HKSettingsUtil.ModuleEnabled) return;

        var gc = GC;
        if (gc == null) return;
        gc.ApplyOutcome(GetHeroPawnSafe(), karmaDelta, standingDelta, eventKey, label, detail, karmaReason, standingReason, tokens, targetPawnId, targetFactionId);
    }
}
