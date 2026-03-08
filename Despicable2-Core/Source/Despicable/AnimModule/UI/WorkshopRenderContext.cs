using System;

namespace Despicable;
/// <summary>
/// Marks a render pass as originating from the animation workshop preview.
/// We use this to selectively enable portrait-time animation behaviors
/// (like per-node facing) without affecting the rest of the game's UI portraits.
///
/// The active preview state is now grouped into a single thread-local object,
/// which keeps the global surface area smaller while preserving the same call pattern.
/// </summary>
public static class WorkshopRenderContext
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

    public static bool Active => State.Active;

    public static void ResetRuntimeState()
    {
        _state = null;
    }

    /// <summary>
    /// Current preview tick for workshop rendering. Only meaningful when Active == true.
    /// </summary>
    public static int Tick => State.Tick;

    public static void SetTick(int tick)
    {
        State.SetTick(tick);
    }

    public static void AdvanceTick(int delta = 1)
    {
        State.AdvanceTick(delta);
    }

    public readonly struct Scope : IDisposable
    {
        private readonly RuntimeState _state;
        private readonly bool _prevActive;
        private readonly int _prevTick;

        public Scope(bool active, int tick = 0)
        {
            _state = State;
            _prevActive = _state.Active;
            _prevTick = _state.Tick;
            _state.SetActive(active);
            if (active)
                _state.SetTick(tick);
        }

        public void Dispose()
        {
            _state.SetActive(_prevActive);
            _state.SetTick(_prevTick);
        }
    }

    private sealed class RuntimeState
    {
        internal bool Active { get; private set; }
        internal int Tick { get; private set; }

        internal void SetActive(bool active)
        {
            Active = active;
        }

        internal void SetTick(int tick)
        {
            Tick = tick;
        }

        internal void AdvanceTick(int delta)
        {
            Tick += delta;
        }
    }
}
