namespace Despicable.Core
{
    public interface IPreResolveHook
    {
        /// <summary>
        /// Called before Resolver makes a decision.
        /// Return false to block the interaction (set reason via outReason).
        /// You may mutate req/ctx to influence resolution.
        /// </summary>
        bool PreResolve(InteractionRequest req, InteractionContext ctx, out string outReason);
    }
}
