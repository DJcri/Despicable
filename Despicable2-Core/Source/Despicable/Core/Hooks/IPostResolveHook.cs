namespace Despicable.Core
{
    public interface IPostResolveHook
    {
        /// <summary>
        /// Called after Resolver produces a resolution.
        /// Hooks may adjust res (e.g. tag animation plan, clamp values).
        /// Must not start jobs or apply outcomes directly.
        /// </summary>
        void PostResolve(InteractionRequest req, InteractionContext ctx, InteractionResolution res);
    }
}
