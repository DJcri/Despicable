namespace Despicable.NSFW.Integrations;
/// <summary>
/// Centralized compat bootstrap for optional NSFW-side integrations.
/// Mutable initialization state now lives in a dedicated runtime state object so startup remains
/// predictable without static flags carrying the actual workflow state.
/// </summary>
internal static class NsfwCompatBootstrap
{
    private static readonly NsfwCompatRuntimeState runtimeState = new();

    internal static void EnsureInitialized()
    {
        runtimeState.EnsureInitialized();
    }
}
