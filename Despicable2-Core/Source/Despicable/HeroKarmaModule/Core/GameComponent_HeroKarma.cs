using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Despicable.HeroKarma;
/// <summary>
/// The real HeroKarma backend authority (compiled).
/// Owns hero selection, global karma, and the primitive ledger.
/// </summary>
public partial class GameComponent_HeroKarma : GameComponent
{
    private const int LedgerCap = 40;

    private string heroPawnId; // Pawn.GetUniqueLoadID()
    private int globalKarma;   // -100..+100 (cosmic)
    private int globalStanding; // -100..+100 (ideology standing)

    // Schema version for save migration.
    private int hkSchemaVersion;
    private int lastStandingDecayDay;

    private List<HKLedgerEntry> ledger = new();


    public GameComponent_HeroKarma(Game game)
    {
        // Keep constructor lightweight. Registering in LoadedGame/StartedNewGame is safest.
    }

    public override void StartedNewGame()
    {
        base.StartedNewGame();
        TryRegisterBridge();
    }

    public override void LoadedGame()
    {
        base.LoadedGame();
        TryRegisterBridge();
    }

    public override void ExposeData()
    {
        base.ExposeData();

        Scribe_Values.Look(ref heroPawnId, "HK_heroPawnId", null);
        Scribe_Values.Look(ref globalKarma, "HK_globalKarma", 0);
        Scribe_Values.Look(ref globalStanding, "HK_globalStanding", 0);
        Scribe_Values.Look(ref hkSchemaVersion, "HK_schemaVersion", 0);
        Scribe_Values.Look(ref lastStandingDecayDay, "HK_lastStandingDecayDay", 0);
        Scribe_Collections.Look(ref ledger, "HK_ledger", LookMode.Deep);

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            if (ledger == null) ledger = new List<HKLedgerEntry>();

            // One-time migration to the 3-layer model.
            // Schema 0: legacy (karma only). Schema 1: karma + standing.
            if (hkSchemaVersion <= 0)
            {
                // Default: preserve legacy karma (so perk tiers remain stable),
                // and start standing at neutral. Players can opt-in to migrating
                // legacy karma into standing via mod settings before loading.
                bool migrateLegacyToStanding = false;
                try
                {
                    var s = Despicable.CommonUtil.GetSettings();
                    migrateLegacyToStanding = s != null && s.heroKarmaMigrateLegacyKarmaToStanding;
                }
                catch
                {
                    migrateLegacyToStanding = false;
                }

                if (migrateLegacyToStanding)
                {
                    globalStanding = globalKarma;
                    globalKarma = 0;
                }
                else
                {
                    globalStanding = 0;
                }

                hkSchemaVersion = 1;
            }

            globalKarma = Mathf.Clamp(globalKarma, HKRuntime.KarmaMin, HKRuntime.KarmaMax);
            globalStanding = Mathf.Clamp(globalStanding, HKRuntime.KarmaMin, HKRuntime.KarmaMax);

            // Keep decay day sane.
            if (lastStandingDecayDay <= 0) lastStandingDecayDay = GenDate.DaysPassed;
        }
    }

    public override void GameComponentTick()
    {
        base.GameComponentTick();

        if (!HKSettingsUtil.ModuleEnabled)
        {
            if ((Find.TickManager?.TicksGame ?? 0) % 60 == 0)
                HKPerkEffects.TryClearAllPerks(ResolveHeroPawnSafe());
            return;
        }

        // Standing decay is intentionally slow and only checks on day changes.
        if (!IsStandingEnabled()) return;

        int day = GenDate.DaysPassed;
        if (day <= 0) return;

        if (lastStandingDecayDay <= 0) lastStandingDecayDay = day;
        if (day == lastStandingDecayDay) return;

        int steps = day - lastStandingDecayDay;
        if (steps <= 0) { lastStandingDecayDay = day; return; }

        // Decay: 1 point per day toward zero.
        int decay = steps;
        if (globalStanding > 0)
            globalStanding = Mathf.Max(0, globalStanding - decay);
        else if (globalStanding < 0)
            globalStanding = Mathf.Min(0, globalStanding + decay);

        lastStandingDecayDay = day;
    }

    public string HeroPawnId => heroPawnId;

    public void SetHero(Pawn pawn)
    {
        var oldHero = ResolvePreviousHeroForCleanup();

        heroPawnId = pawn?.GetUniqueLoadID();

        if (oldHero != null && oldHero != pawn)
        {
            HKPerkEffects.TryClearAllPerks(oldHero);
        }

        HKPerkEffects.TrySyncHeroPerks(pawn, globalKarma);
    }

    public Pawn ResolveHeroPawnSafe()
    {
        if (heroPawnId.NullOrEmpty()) return null;
        return HKResolve.TryResolvePawnById(heroPawnId);
    }

    public int GlobalKarma => globalKarma;
    public int GlobalStanding => globalStanding;

    public void AddGlobalKarma(int delta)
    {
        globalKarma = Mathf.Clamp(globalKarma + delta, HKRuntime.KarmaMin, HKRuntime.KarmaMax);
    }

    public void AddGlobalStanding(int delta)
    {
        globalStanding = Mathf.Clamp(globalStanding + delta, HKRuntime.KarmaMin, HKRuntime.KarmaMax);
    }

    public IEnumerable<HKLedgerEntry> GetLedgerNewestFirst(int cap)
    {
        if (ledger == null) yield break;
        int n = Mathf.Min(cap, ledger.Count);

        for (int i = 0; i < n; i++)
        {
            yield return ledger[ledger.Count - 1 - i];
        }
    }

    public void AddLedgerEntry(
        string eventKey,
        int karmaDelta,
        int standingDelta,
        string label,
        string detail,
        string karmaReason,
        string standingReason,
        string targetPawnId = null,
        int targetFactionId = 0)
    {
        if (ledger == null) ledger = new List<HKLedgerEntry>();

        ledger.Add(new HKLedgerEntry
        {
            eventKey = eventKey,
            delta = karmaDelta,
            standingDelta = standingDelta,
            label = label,
            detail = detail,
            reason = karmaReason,
            standingReason = standingReason,
            tick = Find.TickManager?.TicksGame ?? 0,
            day = GenDate.DaysPassed,
            heroPawnId = heroPawnId,
            targetPawnId = targetPawnId,
            targetFactionId = targetFactionId
        });

        TrimLedgerToCap();
    }

    /// <summary>
    /// Single entry point to apply a karma outcome:
    /// global karma delta, ledger entry, and effect tokens (including local rep tokens).
    /// </summary>
    public void ApplyOutcome(
        Pawn hero,
        int karmaDelta,
        int standingDelta,
        string eventKey,
        string label,
        string detail,
        string karmaReason,
        string standingReason,
        IEnumerable<IHKEffectToken> tokens,
        string targetPawnId = null,
        int targetFactionId = 0)
    {
        if (!HKSettingsUtil.ModuleEnabled) return;

        if (hero == null) hero = ResolveHeroPawnSafe();
        if (hero == null) return;

        if (heroPawnId.NullOrEmpty())
        {
            heroPawnId = hero.GetUniqueLoadID();
        }

        if (karmaDelta != 0 && IsGlobalKarmaEnabled())
        {
            AddGlobalKarma(karmaDelta);
        }

        if (standingDelta != 0 && IsStandingEnabled())
        {
            AddGlobalStanding(standingDelta);
        }

        AddLedgerEntry(eventKey, karmaDelta, standingDelta, label, detail, karmaReason, standingReason, targetPawnId, targetFactionId);

        HKTokenApplier.ApplyAll(hero, tokens);

        HKPerkEffects.TrySyncHeroPerks(hero, globalKarma);
    }

    public IEnumerable<HKLedgerRow> BuildLedgerRowsForUI(Pawn hero, int cap)
    {
        foreach (var e in GetLedgerNewestFirst(cap))
        {
            yield return new HKLedgerRow
            {
                eventKey = e.eventKey,
                delta = e.delta,
                standingDelta = e.standingDelta,
                label = e.label ?? e.eventKey,
                detail = e.detail,
                reason = e.reason,
                standingReason = e.standingReason,
                ticks = e.tick
            };
        }
    }

    public IEnumerable<HKPerkDef> GetActivePerksForUI(int karma)
    {
        return HKPerkCatalog.GetPerksFor(HKRuntime.GetTierFor(karma));
    }
}
