namespace Despicable.Core.Staging
{
    public interface IStagePlaybackBackend
    {
        /// <summary>
        /// Return true if this backend can interpret and play the plan.
        /// </summary>
        bool CanPlay(StagePlan plan);

        /// <summary>
        /// Start playback. Must be safe to call even if plan is partially invalid (no hard crashes).
        /// </summary>
        void Play(StagePlan plan);
    }
}
