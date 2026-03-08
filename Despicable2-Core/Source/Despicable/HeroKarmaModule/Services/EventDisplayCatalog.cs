using System.Collections.Generic;
using Verse;

namespace Despicable.HeroKarma;
/// <summary>
/// UI safety rail: human-facing metadata for ledger/event keys.
/// Keeps UI and ledger from hardcoding switch statements.
///
/// Step 4: eventKey -> label/category/iconKey mapping (icons can be wired later).
/// </summary>
public interface IEventDisplayCatalog
{
    EventDisplayInfo Get(string eventKey);
    IEnumerable<EventDisplayInfo> All();
}

public struct EventDisplayInfo
{
    public string eventKey;
    public string label;
    public string category;
    public string iconKey; // optional; may be null/empty until icons are finalized

    public static EventDisplayInfo Fallback(string eventKey)
    {
        return new EventDisplayInfo
        {
            eventKey = eventKey,
            label = eventKey.NullOrEmpty() ? "Unknown" : eventKey,
            category = "Other",
            iconKey = null
        };
    }
}

/// <summary>
/// Default catalog for the MVP Step 3 event hooks.
/// Extend this list as you add new event keys or refine categories/icons.
/// </summary>
public class DefaultEventDisplayCatalog : IEventDisplayCatalog
{
    private readonly Dictionary<string, EventDisplayInfo> map = new();

    public DefaultEventDisplayCatalog()
    {
        // Violence / domination / atrocity
        Add("ExecutePrisoner", "Executed prisoner", "Violence", "execute");
        Add("AttackNeutral", "Attacked non-hostile", "Violence", "attack");
        Add("OrganHarvest", "Harvested organs", "Atrocity", "organ");
        Add("EnslaveAttempt", "Attempted enslavement", "Domination", "enslave");

        // Mercy / care
        Add("ReleasePrisoner", "Released prisoner", "Mercy", "release");
        Add("TendOutsider", "Tended outsider", "Mercy", "tend");

        // Diplomacy / charity
        Add("CharityGift", "Gave charity", "Charity", "gift");

// Expanded deeds
Add("ArrestNeutral", "Arrested non-hostile", "Domination", "arrest");
Add("RescueOutsider", "Rescued outsider", "Mercy", "rescue");
Add("StabilizeOutsider", "Stabilized outsider", "Mercy", "stabilize");
Add("KillDownedNeutral", "Killed downed non-hostile", "Atrocity", "finishoff");
Add("HarmGuest", "Harmed guest", "Betrayal", "guestharm");

// Future candidates (hooks may be added later)
Add("FreeSlave", "Freed slave", "Mercy", "freeslave");
Add("DonateToBeggars", "Helped beggars", "Charity", "beggars");
Add("SellCaptive", "Sold captive", "Domination", "sell");
Add("SellPrisoner", "Sold captive", "Domination", "sell"); // legacy event-key alias
    }

    private void Add(string key, string label, string category, string iconKey)
    {
        map[key] = new EventDisplayInfo
        {
            eventKey = key,
            label = label,
            category = category,
            iconKey = iconKey
        };
    }

    public EventDisplayInfo Get(string eventKey)
    {
        if (eventKey.NullOrEmpty()) return EventDisplayInfo.Fallback("Unknown");
        EventDisplayInfo info;
        return map.TryGetValue(eventKey, out info) ? info : EventDisplayInfo.Fallback(eventKey);
    }

    public IEnumerable<EventDisplayInfo> All()
    {
        return map.Values;
    }
}
