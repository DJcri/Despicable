using System.Threading;

namespace Despicable;
/// <summary>
/// Stable owner for preview-pawn numeric suffixes used by workshop and AGS previews.
/// </summary>
public static class PreviewPawnIdAllocator
{
    private static readonly RuntimeState State = new();

    public static int NextId()
    {
        return Interlocked.Increment(ref State.NextId);
    }

    public static void ResetRuntimeState()
    {
        Interlocked.Exchange(ref State.NextId, 0);
    }

    private sealed class RuntimeState
    {
        internal int NextId;
    }
}
