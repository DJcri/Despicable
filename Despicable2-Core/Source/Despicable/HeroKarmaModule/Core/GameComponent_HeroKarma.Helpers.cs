using System;
using Verse;

namespace Despicable.HeroKarma;
public partial class GameComponent_HeroKarma
{
    private void TryRegisterBridge()
    {
        // Always refresh the runtime bridge for the current GameComponent instance.
        // New game / load boundaries create a new GameComponent_HeroKarma, and the UI
        // reads hero state through HKRuntime -> HKBackendBridge first. Re-registering
        // here keeps the bridge aligned with the current save instead of a stale one
        // from a previous game session.
        HKBackendBridge.Register(new BackendBridgeImpl(this));
    }

    private Pawn ResolvePreviousHeroForCleanup()
    {
        if (heroPawnId.NullOrEmpty()) return null;

        try
        {
            return HKResolve.TryResolvePawnById(heroPawnId);
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "HK.GameComponent.ResolvePreviousHeroForCleanup",
                "Failed to resolve the previous hero pawn for cleanup; skipping cleanup for the stale reference.",
                ex);

            return null;
        }
    }

    private bool IsGlobalKarmaEnabled()
    {
        try
        {
            return HKSettingsUtil.EnableGlobalKarma;
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "HK.GameComponent.IsGlobalKarmaEnabled",
                "Failed to read HeroKarma global settings; using the safe enabled fallback.",
                ex);

            return true;
        }
    }

    private bool IsStandingEnabled()
    {
        return HKIdeologyCompat.IsStandingEnabled;
    }

    private void TrimLedgerToCap()
    {
        if (ledger == null || ledger.Count <= LedgerCap) return;

        int extra = ledger.Count - LedgerCap;
        ledger.RemoveRange(0, extra);
    }

    private sealed class BackendBridgeImpl : IHKBackendBridge
    {
        private readonly GameComponent_HeroKarma gc;

        public BackendBridgeImpl(GameComponent_HeroKarma gc)
        {
            this.gc = gc;
        }

        public Pawn GetHeroPawnSafe()
        {
            return gc.ResolveHeroPawnSafe();
        }

        public int GetGlobalKarma(Pawn hero)
        {
            return gc.GlobalKarma;
        }

        public int GetGlobalStanding(Pawn hero)
        {
            return gc.GlobalStanding;
        }

        public System.Collections.Generic.IEnumerable<HKLedgerRow> GetLedgerRows(Pawn hero, int cap)
        {
            return gc.BuildLedgerRowsForUI(hero, cap);
        }

        public System.Collections.Generic.IEnumerable<HKPerkDef> GetActivePerksFor(int karma)
        {
            return gc.GetActivePerksForUI(karma);
        }
    }
}
