using System;
using System.Collections.Generic;

namespace Despicable;
/// <summary>
/// Lightweight thread-local re-entry guard keyed by call-site name.
/// Use a disposable scope so enter/exit stays paired even through exceptions.
/// </summary>
public static class ReentryGuard
{
    [ThreadStatic]
    private static HashSet<string> _activeKeys;

    private static HashSet<string> ActiveKeys
    {
        get
        {
            if (_activeKeys == null)
                _activeKeys = new HashSet<string>();
            return _activeKeys;
        }
    }

    public static Scope Enter(string key)
    {
        bool isEntered = !string.IsNullOrEmpty(key) && ActiveKeys.Add(key);
        return new Scope(key, isEntered);
    }

    public static void ResetRuntimeState()
    {
        _activeKeys = null;
    }

    public readonly struct Scope : IDisposable
    {
        private readonly string _key;

        internal Scope(string key, bool isEntered)
        {
            _key = key;
            IsEntered = isEntered;
        }

        public bool IsEntered { get; }

        public void Dispose()
        {
            if (!IsEntered || string.IsNullOrEmpty(_key) || _activeKeys == null)
                return;

            _activeKeys.Remove(_key);
        }
    }
}
