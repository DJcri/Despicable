using System;
using System.Collections.Generic;
using Verse;

namespace Despicable.AnimModule.Runtime.Graphics;
/// Resolves a requested state id to an available one (no Rot4 manipulation).
public static class GraphicStateResolver
{
    private static readonly HashSet<string> warnedFallbackKeys = new();

    /// <summary>
    /// Clears warn-once fallback registry state for a fresh runtime session.
    /// </summary>
    public static void ResetRuntimeState()
    {
        warnedFallbackKeys.Clear();
    }

    public static string ResolveStateId(string nodeDebugId, string requested, ICollection<string> available, bool logOnceOnFallback = true)
    {
        if (available == null || available.Count == 0) return null;

        if (requested.NullOrEmpty())
            return First(available);

        // exact
        if (Contains(available, requested))
            return requested;

        // case-insensitive
        var ci = FindCI(available, requested);
        if (!ci.NullOrEmpty())
        {
            WarnOnce(nodeDebugId, requested, ci, logOnceOnFallback);
            return ci;
        }

        // cardinal fallback chains
        if (IsCardinal(requested))
        {
            foreach (var cand in CardinalChain(requested))
            {
                if (Contains(available, cand))
                {
                    WarnOnce(nodeDebugId, requested, cand, logOnceOnFallback);
                    return cand;
                }

                var ci2 = FindCI(available, cand);
                if (!ci2.NullOrEmpty())
                {
                    WarnOnce(nodeDebugId, requested, ci2, logOnceOnFallback);
                    return ci2;
                }
            }
        }

        // last resort: any
        var any = First(available);
        WarnOnce(nodeDebugId, requested, any, logOnceOnFallback);
        return any;
    }

    private static bool IsCardinal(string s)
    {
        if (s.NullOrEmpty()) return false;
        return s.Equals("North", StringComparison.OrdinalIgnoreCase)
            || s.Equals("East", StringComparison.OrdinalIgnoreCase)
            || s.Equals("South", StringComparison.OrdinalIgnoreCase)
            || s.Equals("West", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> CardinalChain(string requested)
    {
        if (requested.Equals("North", StringComparison.OrdinalIgnoreCase))
        { yield return "North"; yield return "South"; yield return "East"; yield return "West"; yield break; }

        if (requested.Equals("South", StringComparison.OrdinalIgnoreCase))
        { yield return "South"; yield return "North"; yield return "East"; yield return "West"; yield break; }

        if (requested.Equals("East", StringComparison.OrdinalIgnoreCase))
        { yield return "East"; yield return "West"; yield return "South"; yield return "North"; yield break; }

        // West
        yield return "West"; yield return "East"; yield return "South"; yield return "North";
    }

    private static void WarnOnce(string nodeDebugId, string requested, string resolved, bool enabled)
    {
        if (!enabled) return;
        if (requested.NullOrEmpty() || resolved.NullOrEmpty()) return;
        if (requested == resolved) return;

        string key = (nodeDebugId ?? "Node") + "|" + requested + "|" + resolved;
        if (!warnedFallbackKeys.Add(key)) return;

        Log.Warning($"[Despicable.Anim] Graphic state fallback: node={nodeDebugId ?? "?"} requested='{requested}' resolved='{resolved}'");
    }

    private static bool Contains(ICollection<string> set, string value)
    {
        foreach (var s in set) if (s == value) return true;
        return false;
    }

    private static string FindCI(ICollection<string> set, string value)
    {
        foreach (var s in set)
            if (!s.NullOrEmpty() && s.Equals(value, StringComparison.OrdinalIgnoreCase))
                return s;
        return null;
    }

    private static string First(ICollection<string> set)
    {
        foreach (var s in set) if (!s.NullOrEmpty()) return s;
        return null;
    }
}
