using System;

namespace Despicable;
/// <summary>
/// Thread-local scope for temporarily suppressing draw-tracker postfix behavior while we query
/// another pawn's vanilla draw position.
/// </summary>
public static class DrawTrackerSuppressionScope
{
    [ThreadStatic]
    private static int _depth;

    public static bool Active => _depth > 0;

    public static Scope Enter()
    {
        _depth++;
        return new Scope(true);
    }

    public static void ResetRuntimeState()
    {
        _depth = 0;
    }

    public readonly struct Scope : IDisposable
    {
        private readonly bool _entered;

        internal Scope(bool entered)
        {
            _entered = entered;
        }

        public void Dispose()
        {
            if (_entered && _depth > 0)
                _depth--;
        }
    }
}
