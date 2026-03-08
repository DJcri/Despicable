using System.Collections.Generic;
using Verse;

namespace Despicable.HeroKarma;
/// <summary>
/// Step 3/4+ bridge: lets the UI (and any other presentation layer) read from
/// the rebuilt Hero+Karma backend without referencing its concrete types.
///
/// Why this exists:
/// - Keeps UI stable and compile-safe even while backend evolves.
/// - Avoids hard dependencies / type-name guessing.
///
/// Your rebuilt backend should register an implementation during init:
/// HKBackendBridge.Register(new MyBackendBridge());
/// </summary>
public interface IHKBackendBridge
{
    Pawn GetHeroPawnSafe();
    int GetGlobalKarma(Pawn hero);
    int GetGlobalStanding(Pawn hero);
    IEnumerable<HKLedgerRow> GetLedgerRows(Pawn hero, int cap);
    IEnumerable<HKPerkDef> GetActivePerksFor(int karma);
}

public static class HKBackendBridge
{
    private static readonly RuntimeState State = new();

    public static IHKBackendBridge Bridge => State.Bridge;

    /// <summary>
    /// Register a bridge implementation (typically from the rebuilt backend).
    /// Calling twice replaces the previous registration.
    /// </summary>
    public static void Register(IHKBackendBridge bridge)
    {
        State.Register(bridge);
    }

    /// <summary>
    /// Clears any stale bridge instance across new game / load boundaries.
    /// Safe because GameComponent_HeroKarma re-registers the current bridge during
    /// StartedNewGame / LoadedGame.
    /// </summary>
    public static void ResetRuntimeState()
    {
        State.Reset();
    }

    private sealed class RuntimeState
    {
        private IHKBackendBridge _bridge;

        internal IHKBackendBridge Bridge => _bridge;

        internal void Register(IHKBackendBridge bridge)
        {
            _bridge = bridge;
        }

        internal void Reset()
        {
            _bridge = null;
        }
    }
}
