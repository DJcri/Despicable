using System;
using System.Collections.Generic;
using Verse;

namespace Despicable.Core;
public static class DebugLogger
{
    public static bool Enabled => Despicable.DespicableRuntimeConfig.DebugLoggingEnabled;

    private static readonly HashSet<string> warnedMessageKeys = new();
    private static readonly object warnedMessageLock = new();

    public static void Debug(string msg)
    {
        if (!Enabled) return;
        Log.Message($"[Despicable.Core] {msg}");
    }

    public static void Warn(string msg)
    {
        if (!Enabled) return;
        Log.Warning($"[Despicable.Core] {msg}");
    }

    public static void WarnException(string context, Exception ex)
    {
        if (!Enabled) return;

        string message = string.IsNullOrEmpty(context) ? "Operation failed." : context;
        if (ex == null)
        {
            Log.Warning($"[Despicable.Core] {message}");
            return;
        }

        Log.Warning($"[Despicable.Core] {message} Exception: {ex}");
    }

    public static void WarnExceptionOnce(string onceKey, string context, Exception ex)
    {
        if (!Enabled) return;

        if (!string.IsNullOrEmpty(onceKey))
        {
            lock (warnedMessageLock)
            {
                if (!warnedMessageKeys.Add(onceKey)) return;
            }
        }

        WarnException(context, ex);
    }

    public static void ResetRuntimeState()
    {
        lock (warnedMessageLock)
        {
            warnedMessageKeys.Clear();
        }
    }
}
