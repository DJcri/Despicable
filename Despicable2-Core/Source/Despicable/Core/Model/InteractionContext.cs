using Verse;

namespace Despicable.Core;
public sealed class InteractionContext
{
    public Map Map;
    public int Tick;

    public bool InitiatorDrafted;
    public bool RecipientDrafted;

    public bool InitiatorInBed;
    public bool RecipientInBed;

    public bool InitiatorHostileToRecipient;

    public InteractionContext() { }
}
