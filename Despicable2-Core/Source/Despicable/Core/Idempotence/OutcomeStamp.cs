using Verse;

namespace Despicable.Core;
public class OutcomeStamp : IExposable
{
    public string channel;
    public int initiatorId;
    public int recipientId;
    public string interactionId;
    public int instanceId;

    public OutcomeStamp() { }

    public OutcomeStamp(string channel, int initiatorId, int recipientId, string interactionId, int instanceId)
    {
        this.channel = channel ?? "DEFAULT";
        this.initiatorId = initiatorId;
        this.recipientId = recipientId;
        this.interactionId = interactionId ?? "NULL";
        this.instanceId = instanceId;
    }

    public void ExposeData()
    {
        Scribe_Values.Look(ref channel, "channel");
        Scribe_Values.Look(ref initiatorId, "initiatorId");
        Scribe_Values.Look(ref recipientId, "recipientId");
        Scribe_Values.Look(ref interactionId, "interactionId");
        Scribe_Values.Look(ref instanceId, "instanceId");
    }

    public override string ToString()
        => $"{channel}|{initiatorId}->{recipientId}:{interactionId}#{instanceId}";
}
