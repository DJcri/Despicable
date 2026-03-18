using System;
using Verse;

namespace Despicable.HeroKarma.Patches.HeroKarma;

/// <summary>
/// Scoped runtime context for hero ↔ animal interactions where a vanilla stat or chance
/// needs to become relationship-aware for one specific pair.
/// </summary>
internal static class HKAnimalInteractionContext
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

    public static Scope EnterTraining(Pawn trainer, Pawn animal)
    {
        if (trainer == null || animal == null)
            return default;

        State.Begin(trainer.GetUniqueLoadID(), animal.GetUniqueLoadID(), "Training");
        return new Scope(true);
    }

    public static bool IsActiveForTraining
    {
        get
        {
            State.AutoExpireIfStale();
            return State.IsActive && string.Equals(State.Mode, "Training", StringComparison.Ordinal);
        }
    }

    public static bool TryGetTrainingPair(out string trainerPawnId, out string animalPawnId)
    {
        trainerPawnId = null;
        animalPawnId = null;

        State.AutoExpireIfStale();
        if (!IsActiveForTraining)
            return false;

        trainerPawnId = State.TrainerPawnId;
        animalPawnId = State.AnimalPawnId;
        return !trainerPawnId.NullOrEmpty() && !animalPawnId.NullOrEmpty();
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
                State.End();
        }
    }

    private sealed class RuntimeState
    {
        private const int FallbackClearTickWindow = 300;
        private const int UnsetTick = -999999;

        private string _trainerPawnId;
        private string _animalPawnId;
        private string _mode;
        private int _depth;
        private int _lastBeginTick = UnsetTick;

        internal string TrainerPawnId => _trainerPawnId;
        internal string AnimalPawnId => _animalPawnId;
        internal string Mode => _mode;
        internal bool IsActive => _depth > 0 && !_trainerPawnId.NullOrEmpty() && !_animalPawnId.NullOrEmpty();

        internal void Begin(string trainerPawnId, string animalPawnId, string mode)
        {
            AutoExpireIfStale();
            _depth++;

            try
            {
                if (Find.TickManager != null)
                    _lastBeginTick = Find.TickManager.TicksGame;
            }
            catch (Exception ex)
            {
                Despicable.Core.DebugLogger.WarnExceptionOnce("HKAnimalInteractionContext:101", "HKAnimalInteractionContext suppressed an exception.", ex);
            }

            if (_trainerPawnId.NullOrEmpty())
                _trainerPawnId = trainerPawnId;
            if (_animalPawnId.NullOrEmpty())
                _animalPawnId = animalPawnId;
            if (_mode.NullOrEmpty())
                _mode = mode;
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
                Despicable.Core.DebugLogger.WarnExceptionOnce("HKAnimalInteractionContext:102", "HKAnimalInteractionContext suppressed an exception.", ex);
            }

            if (now > 0 && _lastBeginTick > 0 && (now - _lastBeginTick) > FallbackClearTickWindow)
                ForceClear();
        }

        private void ForceClear()
        {
            _trainerPawnId = null;
            _animalPawnId = null;
            _mode = null;
            _depth = 0;
            _lastBeginTick = UnsetTick;
        }
    }
}
