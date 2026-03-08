using System;
using Verse;

namespace Despicable.HeroKarma.Patches.HeroKarma;
/// <summary>
/// Scoped context flag for attributing goodwill changes to a specific pawn action.
///
/// Many goodwill APIs do not carry an instigator pawn down to
/// Faction.TryAffectGoodwillWith(...). We therefore set a short-lived
/// context around known hero-initiated actions, and only apply the
/// goodwill perks when that context is active for the hero.
///
/// The live context is now thread-local and instance-owned to reduce the
/// risk of stale static state bleeding across unrelated execution paths.
/// </summary>
internal static class HKGoodwillContext
{
    [ThreadStatic]
    private static RuntimeState _state;

    private static RuntimeState State
    {
        get
        {
            if (_state == null)
                _state = new RuntimeState();
            return _state;
        }
    }

    public static bool IsActive
    {
        get
        {
            State.AutoExpireIfStale();
            return State.IsActive;
        }
    }

    public static Scope Enter(Pawn instigator)
    {
        State.Begin(instigator);
        return new Scope(true);
    }

    public static void Begin(Pawn instigator)
    {
        State.Begin(instigator);
    }

    public static void End()
    {
        State.End();
    }

    public static Pawn TryGetInstigator()
    {
        State.AutoExpireIfStale();
        if (!State.IsActive) return null;
        return HKResolve.TryResolvePawnById(State.InstigatorPawnId);
    }

    public static void ResetRuntimeState()
    {
        _state = null;
    }

    public readonly struct Scope : IDisposable
    {
        private readonly bool _active;

        internal Scope(bool active)
        {
            _active = active;
        }

        public void Dispose()
        {
            if (_active)
                End();
        }
    }

    private sealed class RuntimeState
    {
        private const int FallbackClearTickWindow = 300;
        private const int UnsetTick = -999999;

        private string _instigatorPawnId;
        private int _depth;
        private int _lastBeginTick = UnsetTick;

        internal string InstigatorPawnId => _instigatorPawnId;
        internal bool IsActive => _depth > 0 && !_instigatorPawnId.NullOrEmpty();

        internal void Begin(Pawn instigator)
        {
            AutoExpireIfStale();
            _depth++;
            if (instigator == null) return;

            try
            {
                if (Find.TickManager != null)
                    _lastBeginTick = Find.TickManager.TicksGame;
            }
            catch (Exception ex)
            {
                Despicable.Core.DebugLogger.WarnExceptionOnce("HKGoodwillContext:101", "HKGoodwillContext suppressed an exception.", ex);
            }

            if (_instigatorPawnId.NullOrEmpty())
                _instigatorPawnId = instigator.GetUniqueLoadID();
        }

        internal void End()
        {
            if (_depth > 0)
                _depth--;

            if (_depth <= 0)
                ForceClear();
        }

        internal void AutoExpireIfStale()
        {
            if (_depth <= 0)
                return;

            int now = 0;
            try
            {
                now = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            }
            catch (Exception ex)
            {
                Despicable.Core.DebugLogger.WarnExceptionOnce("HKGoodwillContext:102", "HKGoodwillContext suppressed an exception.", ex);
                now = 0;
            }

            if (now > 0 && _lastBeginTick > 0 && (now - _lastBeginTick) > FallbackClearTickWindow)
                ForceClear();
        }

        private void ForceClear()
        {
            _depth = 0;
            _instigatorPawnId = null;
            _lastBeginTick = UnsetTick;
        }
    }
}
