using Verse;

namespace Despicable.Core.Staging;
public sealed class StageTagContext
{
    public readonly Map Map;
    public readonly StageRequest Request;
    public readonly Despicable.Core.InteractionContext InteractionContext;

    public StageTagContext(Map map, StageRequest request, Despicable.Core.InteractionContext interactionContext)
    {
        Map = map;
        Request = request;
        InteractionContext = interactionContext;
    }
}
