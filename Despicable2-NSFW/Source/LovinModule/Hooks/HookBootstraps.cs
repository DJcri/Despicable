using Verse;
using Despicable.Core;
using Despicable.Core.Staging;

namespace Despicable.NSFW;
/// <summary>
/// Registers NSFW hooks and staging tag providers. Idempotent.
/// Called from NSFW startup so all hook wiring is centralized behind one method.
/// </summary>
public static class HookBootstraps
{
    private static readonly LovinPreResolveHook PreResolveHook = new();
    private static readonly LovinPostResolveHook PostResolveHook = new();
    private static readonly NsfwPawnTags PawnTags = new();

    // Guardrail-Allow-Static: One-time NSFW hook registration gate owned by app-domain startup lifecycle.
    private static bool _registered;

    public static void EnsureRegistered()
    {
        if (_registered)
            return;

        _registered = true;

        Hooks.RegisterPre(PreResolveHook);
        Hooks.RegisterPost(PostResolveHook);
        StageTagProviders.Register(PawnTags);

        Log.Message("[Despicable2.NSFW] Hooks registered.");
    }
}
