using System.Collections.Generic;

namespace Despicable.Core;
public static class Hooks
{
    private static readonly List<IPreResolveHook> preResolveHooks = new();
    private static readonly List<IPostResolveHook> postResolveHooks = new();

    public static void RegisterPost(IPostResolveHook hook)
    {
        if (hook == null) return;
        if (!postResolveHooks.Contains(hook))
            postResolveHooks.Add(hook);
    }

    public static void UnregisterPost(IPostResolveHook hook)
    {
        if (hook == null) return;
        postResolveHooks.Remove(hook);
    }

    public static void RunPostResolve(InteractionRequest req, InteractionContext ctx, InteractionResolution res)
    {
        for (int i = 0; i < postResolveHooks.Count; i++)
        {
            var hook = postResolveHooks[i];
            if (hook == null) continue;
            hook.PostResolve(req, ctx, res);
        }
    }

    public static void RegisterPre(IPreResolveHook hook)
    {
        if (hook == null) return;
        if (!preResolveHooks.Contains(hook))
            preResolveHooks.Add(hook);
    }

    public static void UnregisterPre(IPreResolveHook hook)
    {
        if (hook == null) return;
        preResolveHooks.Remove(hook);
    }

    public static bool RunPreResolve(InteractionRequest req, InteractionContext ctx, out string reason)
    {
        reason = null;

        for (int i = 0; i < preResolveHooks.Count; i++)
        {
            var hook = preResolveHooks[i];
            if (hook == null) continue;

            if (!hook.PreResolve(req, ctx, out reason))
            {
                if (reason == null)
                    reason = "BlockedByPreResolveHook";
                return false;
            }
        }

        return true;
    }
}
