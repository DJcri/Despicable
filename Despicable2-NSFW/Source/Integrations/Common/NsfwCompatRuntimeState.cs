using Despicable.Core.Compatibility;
using Despicable.NSFW.Integrations.GenderWorks;
using Despicable.NSFW.Integrations.Intimacy;

namespace Despicable.NSFW.Integrations;
/// <summary>
/// Instance-owned compat bootstrap state for optional NSFW integrations.
/// Keeps one-time initialization flags out of static mutable fields.
/// </summary>
internal sealed class NsfwCompatRuntimeState
{
    private readonly IModCompat[] compatModules =
    {
        new IntimacyCompatModule(),
        new GenderWorksCompatModule()
    };

    private bool isInitialized;

    public void EnsureInitialized()
    {
        if (isInitialized)
            return;

        isInitialized = true;

        for (int i = 0; i < compatModules.Length; i++)
            ModCompatRegistry.EnsureRegistered(compatModules[i], "[Despicable2.NSFW]");
    }
}
