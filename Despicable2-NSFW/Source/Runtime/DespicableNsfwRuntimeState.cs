using Despicable.NSFW.Integrations;
using Despicable.NSFW.Integrations.Intimacy;

namespace Despicable.NSFW.Runtime;
/// <summary>
/// Central lifecycle hub for ephemeral NSFW-side runtime state.
/// New game and load boundaries should clear NSFW-owned caches here instead of relying on Core to know about them.
/// </summary>
internal static class DespicableNsfwRuntimeState
{
    internal static void ResetRuntimeState()
    {
        LovinContextBridge.ResetRuntimeState();
        IntimacyReflectionUtil.ResetRuntimeState();
        IntimacyApplyUtil.ResetRuntimeState();
    }

    internal static void ResetAll()
    {
        ResetRuntimeState();
    }
}
