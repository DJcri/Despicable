using System.Collections.Generic;

namespace Despicable.Core.Staging;
public static class StagePlanHooks
{
    private static readonly List<IStagePlanHook> hooks = new();

    public static void Register(IStagePlanHook hook)
    {
        if (hook == null) return;
        if (hooks.Contains(hook)) return;
        hooks.Add(hook);
        hooks.Sort((a, b) => b.Priority.CompareTo(a.Priority));
    }

    public static void Unregister(IStagePlanHook hook)
    {
        if (hook == null) return;
        hooks.Remove(hook);
    }

    internal static List<IStagePlanHook> SnapshotUnsafe() => hooks;
}
