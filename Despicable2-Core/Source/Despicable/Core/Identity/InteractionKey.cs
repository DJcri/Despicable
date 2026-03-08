using System;

namespace Despicable.Core;
public readonly struct InteractionKey : IEquatable<InteractionKey>
{
    public readonly int InitiatorThingId;
    public readonly int RecipientThingId;
    public readonly string InteractionId;
    public readonly int TickBucket;

    public InteractionKey(int initiatorThingId, int recipientThingId, string interactionId, int tickBucket)
    {
        InitiatorThingId = initiatorThingId;
        RecipientThingId = recipientThingId;
        InteractionId = interactionId ?? "NULL";
        TickBucket = tickBucket;
    }

    public bool Equals(InteractionKey other)
        => InitiatorThingId == other.InitiatorThingId
           && RecipientThingId == other.RecipientThingId
           && TickBucket == other.TickBucket
           && string.Equals(InteractionId, other.InteractionId, StringComparison.Ordinal);

    public override bool Equals(object obj) => obj is InteractionKey other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = (hash * 31) + InitiatorThingId;
            hash = (hash * 31) + RecipientThingId;
            hash = (hash * 31) + TickBucket;
            hash = (hash * 31) + (InteractionId != null ? InteractionId.GetHashCode() : 0);
            return hash;
        }
    }

    public override string ToString() => $"{InitiatorThingId}->{RecipientThingId}:{InteractionId}@{TickBucket}";
}
