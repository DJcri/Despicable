using RimWorld;
using Verse;

namespace Despicable.Core;
/// <summary>
/// Caller-authored intent for an interaction.
///
/// Core must remain content-agnostic. Module-specific concepts (ex: "lovin type")
/// should be carried through as opaque identifiers that Core does not interpret.
/// </summary>
public sealed class InteractionRequest
{
    public Pawn Initiator;
    public Pawn Recipient;

    /// <summary>
    /// Optional opaque "variant/type" ID selected by the caller.
    /// Example: an external module can pass its own stage id string here.
    /// Core will not interpret this value.
    /// </summary>
    public string RequestedStageId;

    public InteractionDef RequestedInteractionDef;
    public string RequestedCommand;
    public string Channel;

    // For now, keep it simple. Later we can swap to defName strings or IDs if you prefer.
    public string RequestedInteractionId;

    public bool IsManual;

    public InteractionRequest(Pawn initiator, Pawn recipient, string requestedInteractionId, bool isManual)
    {
        Initiator = initiator;
        Recipient = recipient;
        RequestedInteractionId = requestedInteractionId;
        IsManual = isManual;
    }
}
