using System.Collections.Generic;
using Verse;

namespace Despicable.Core;
public class OutcomeHistory : MapComponent
{
    // Persisted list (for save/load)
    private List<OutcomeStamp> saved = new();

    // Runtime fast lookup
    private HashSet<string> applied = new();

    public OutcomeHistory(Map map) : base(map) { }

    public static OutcomeHistory Get(Map map)
        => map?.GetComponent<OutcomeHistory>();

    public static string MakeKey(string channel, int a, int b, string interactionId, int instanceId)
        => $"{channel ?? "DEFAULT"}|{a}->{b}:{interactionId ?? "NULL"}#{instanceId}";

    public bool TryMarkApplied(string channel, int a, int b, string interactionId, int instanceId)
    {
        string key = MakeKey(channel, a, b, interactionId, instanceId);
        if (!applied.Add(key)) return false;

        saved.Add(new OutcomeStamp(channel, a, b, interactionId, instanceId));
        return true;
    }

    public override void ExposeData()
    {
        base.ExposeData();

        Scribe_Collections.Look(ref saved, "despicableAppliedOutcomes", LookMode.Deep);

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            applied.Clear();
            if (saved != null)
            {
                for (int i = 0; i < saved.Count; i++)
                {
                    var s = saved[i];
                    if (s == null) continue;
                    applied.Add(MakeKey(s.channel, s.initiatorId, s.recipientId, s.interactionId, s.instanceId));
                }
            }
        }
    }

    /// <summary>
    /// Convenience API: allow applying an outcome once per (channel, a, b, interactionId, instanceId).
    /// Intended call sites: right before TryInteractWith / applying thoughts / hediffs / karma.
    /// </summary>
    public static bool TryApplyOnce(Map map, string channel, int a, int b, string interactionId, int instanceId)
    {
        var hist = Get(map);
        if (hist == null) return true; // fail-open: don't break gameplay if component missing
        return hist.TryMarkApplied(channel, a, b, interactionId, instanceId);
    }
}
