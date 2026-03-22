using System.Collections.Generic;
using Despicable.AnimGroupStudio.Preview;
using RimWorld;
using UnityEngine;
using Verse;

namespace Despicable;
public class CompAnatomyBootstrap : ThingComp, IDetachedPreviewPawnInitializer, IDetachedPreviewPawnMirrorFromSource
{
    private const int HiddenTrackerVersion = 8;

    private bool anatomySeeded;
    private int seedRetryCount;
    private bool sawGenderWorksChangeSignal;
    private bool syncInProgress;

    private bool hiddenStateResolved;
    private bool hiddenHasPenis;
    private bool hiddenHasVagina;
    private List<string> hiddenResolvedPartDefNames;
    private List<AnatomyPartInstance> hiddenResolvedPartInstances;
    private int hiddenTrackerVersion;

    internal bool HasResolvedAnatomy => hiddenStateResolved;

    internal bool TryGetResolvedPartInstances(out List<AnatomyPartInstance> instances)
    {
        instances = null;
        if (!hiddenStateResolved)
            return false;

        if (hiddenResolvedPartInstances == null)
        {
            instances = new List<AnatomyPartInstance>();
            return true;
        }

        instances = new List<AnatomyPartInstance>(hiddenResolvedPartInstances.Count);
        for (int i = 0; i < hiddenResolvedPartInstances.Count; i++)
        {
            AnatomyPartInstance instance = hiddenResolvedPartInstances[i];
            if (instance?.partDef == null)
                continue;

            instances.Add(instance);
        }

        return true;
    }

    internal bool TryGetResolvedParts(out List<AnatomyPartDef> parts)
    {
        parts = null;
        if (!TryGetResolvedPartInstances(out List<AnatomyPartInstance> instances) || instances == null)
            return false;

        parts = new List<AnatomyPartDef>(instances.Count);
        for (int i = 0; i < instances.Count; i++)
        {
            AnatomyPartDef part = instances[i]?.partDef;
            if (part != null)
                parts.Add(part);
        }

        return true;
    }

    internal bool TryGetResolvedPartInstance(AnatomyPartDef part, out AnatomyPartInstance instance)
    {
        instance = null;
        if (part == null)
            return false;

        if (!TryGetResolvedPartInstances(out List<AnatomyPartInstance> instances) || instances == null)
            return false;

        for (int i = 0; i < instances.Count; i++)
        {
            AnatomyPartInstance current = instances[i];
            if (current?.partDef == part)
            {
                instance = current;
                return true;
            }
        }

        return false;
    }

    internal bool TryGetResolvedAnatomy(out bool hasPenis, out bool hasVagina)
    {
        hasPenis = false;
        hasVagina = false;

        if (!TryGetResolvedParts(out List<AnatomyPartDef> parts) || parts == null)
            return false;

        for (int i = 0; i < parts.Count; i++)
        {
            AnatomyPartDef part = parts[i];
            if (part?.HasTag("Penis") == true)
                hasPenis = true;

            if (part?.HasTag("Vagina") == true)
                hasVagina = true;
        }

        return true;
    }

    internal void SetResolvedParts(IEnumerable<AnatomyPartDef> parts)
    {
        Dictionary<string, AnatomyPartInstance> preserved = BuildPreservedInstanceLookup();

        if (hiddenResolvedPartDefNames == null)
            hiddenResolvedPartDefNames = new List<string>();
        else
            hiddenResolvedPartDefNames.Clear();

        if (hiddenResolvedPartInstances == null)
            hiddenResolvedPartInstances = new List<AnatomyPartInstance>();
        else
            hiddenResolvedPartInstances.Clear();

        if (parts != null)
        {
            foreach (AnatomyPartDef part in parts)
            {
                if (part == null || part.defName.NullOrEmpty())
                    continue;

                if (hiddenResolvedPartDefNames.Contains(part.defName))
                    continue;

                hiddenResolvedPartDefNames.Add(part.defName);
                hiddenResolvedPartInstances.Add(GetOrCreateInstance(preserved, part));
            }
        }

        SyncLegacyFlags();
        hiddenStateResolved = true;
        hiddenTrackerVersion = HiddenTrackerVersion;
    }

    internal void SetResolvedPartInstances(IEnumerable<AnatomyPartInstance> instances)
    {
        if (hiddenResolvedPartDefNames == null)
            hiddenResolvedPartDefNames = new List<string>();
        else
            hiddenResolvedPartDefNames.Clear();

        if (hiddenResolvedPartInstances == null)
            hiddenResolvedPartInstances = new List<AnatomyPartInstance>();
        else
            hiddenResolvedPartInstances.Clear();

        if (instances != null)
        {
            foreach (AnatomyPartInstance instance in instances)
            {
                AnatomyPartInstance clone = CloneResolvedInstance(instance);
                string defName = clone?.partDef?.defName;
                if (clone == null || defName.NullOrEmpty() || hiddenResolvedPartDefNames.Contains(defName))
                    continue;

                hiddenResolvedPartDefNames.Add(defName);
                hiddenResolvedPartInstances.Add(clone);
            }
        }

        SyncLegacyFlags();
        hiddenStateResolved = true;
        hiddenTrackerVersion = HiddenTrackerVersion;
    }

    internal void SetResolvedAnatomy(bool hasPenis, bool hasVagina)
    {
        List<AnatomyPartDef> parts = new List<AnatomyPartDef>(2);
        if (hasPenis && LovinModule_GenitalDefOf.Genital_Penis != null)
            parts.Add(LovinModule_GenitalDefOf.Genital_Penis);

        if (hasVagina && LovinModule_GenitalDefOf.Genital_Vagina != null)
            parts.Add(LovinModule_GenitalDefOf.Genital_Vagina);

        SetResolvedParts(parts);
    }

    internal void ClearResolvedAnatomy()
    {
        hiddenHasPenis = false;
        hiddenHasVagina = false;
        hiddenResolvedPartDefNames?.Clear();
        hiddenResolvedPartInstances?.Clear();
        hiddenStateResolved = false;
    }


    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        IEnumerable<Gizmo> baseGizmos = base.CompGetGizmosExtra();
        if (baseGizmos != null)
        {
            foreach (Gizmo gizmo in baseGizmos)
                yield return gizmo;
        }

        Pawn pawn = parent as Pawn;
        if (!Prefs.DevMode || pawn == null)
            yield break;

        yield return new Command_Action
        {
            defaultLabel = "Anatomy Debug",
            defaultDesc = "Show resolved anatomy parts, sizes, fluids, anchors, textures, and apparel coverage for this pawn.",
            action = () =>
            {
                string report = AnatomyDebugReportBuilder.Build(pawn, this);
                Find.WindowStack?.Add(new Dialog_AnatomyDebugReport(report));
            }
        };
    }

    public override string CompInspectStringExtra()
    {
        string baseText = base.CompInspectStringExtra();
        if (!Prefs.DevMode)
            return baseText;

        string anatomyText = !TryGetResolvedPartInstances(out List<AnatomyPartInstance> instances) || instances == null
            ? "Anatomy: unresolved"
            : $"Anatomy parts: {instances.Count}";

        return baseText.NullOrEmpty() ? anatomyText : baseText + "\n" + anatomyText;
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref anatomySeeded, "d2_anatomySeeded", false);
        Scribe_Values.Look(ref seedRetryCount, "d2_anatomySeedRetryCount", 0);
        Scribe_Values.Look(ref sawGenderWorksChangeSignal, "d2_sawGenderWorksChangeSignal", false);
        Scribe_Values.Look(ref hiddenStateResolved, "d2_hiddenAnatomyResolved", false);
        Scribe_Values.Look(ref hiddenHasPenis, "d2_hiddenHasPenis", false);
        Scribe_Values.Look(ref hiddenHasVagina, "d2_hiddenHasVagina", false);
        Scribe_Collections.Look(ref hiddenResolvedPartDefNames, "d2_hiddenResolvedPartDefs", LookMode.Value);
        Scribe_Collections.Look(ref hiddenResolvedPartInstances, "d2_hiddenResolvedPartInstances", LookMode.Deep);
        Scribe_Values.Look(ref hiddenTrackerVersion, "d2_hiddenAnatomyVersion", 0);

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            MigrateLegacyTrackerState();
            anatomySeeded = false;
            TrySeedNow(forceResync: true);
        }
    }

    public override void PostPostMake()
    {
        base.PostPostMake();
        TrySeedNow();
    }

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        bool forceResync = respawningAfterLoad
            ? !hiddenStateResolved
            : ShouldForceFreshSpawnResync();
        TrySeedNow(forceResync: forceResync);
    }

    public void MirrorDetachedPreviewFrom(Pawn source)
    {
        Pawn pawn = parent as Pawn;
        if (pawn == null || source == null)
            return;

        CompAnatomyBootstrap sourceTracker = source.TryGetComp<CompAnatomyBootstrap>();
        if (sourceTracker != null && sourceTracker.TryGetResolvedPartInstances(out List<AnatomyPartInstance> instances))
        {
            SetResolvedPartInstances(instances);
            anatomySeeded = true;
            seedRetryCount = 0;
            sawGenderWorksChangeSignal = false;
            MarkGraphicsDirty(pawn);
            return;
        }

        InitializeForDetachedPreview();
    }

    public void InitializeForDetachedPreview()
    {
        Pawn pawn = parent as Pawn;
        if (pawn == null)
            return;

        if (!AnatomyBootstrapper.ForcePreviewSeedFromCurrentGender(pawn))
            return;

        anatomySeeded = true;
        MarkGraphicsDirty(pawn);
    }

    public override void CompTickRare()
    {
        base.CompTickRare();
        TrySeedNow();
        AnatomyFluidRuntime.TickRare(parent as Pawn, this);
    }

    internal void NotifyPotentialAnatomyChange()
    {
        if (syncInProgress)
            return;

        anatomySeeded = false;
        seedRetryCount = 0;
        sawGenderWorksChangeSignal = true;
        ClearResolvedAnatomy();
        TrySeedNow(forceResync: true);
    }

    private bool ShouldForceFreshSpawnResync()
    {
        if (!hiddenStateResolved)
            return true;

        Pawn pawn = parent as Pawn;
        if (pawn == null)
            return false;

        if (!AnatomyResolver.TryResolveDesiredParts(pawn, out List<AnatomyPartDef> parts) || parts == null)
            return false;

        AnatomyQuery.RemoveMissingResolvedParts(pawn, parts);
        return !MatchesResolvedPartDefs(parts);
    }

    private bool MatchesResolvedPartDefs(List<AnatomyPartDef> parts)
    {
        int hiddenCount = hiddenResolvedPartDefNames?.Count ?? 0;
        int partCount = parts?.Count ?? 0;
        if (hiddenCount != partCount)
            return false;

        if (partCount == 0)
            return true;

        for (int i = 0; i < partCount; i++)
        {
            string defName = parts[i]?.defName;
            if (defName.NullOrEmpty() || hiddenResolvedPartDefNames == null || !hiddenResolvedPartDefNames.Contains(defName))
                return false;
        }

        return true;
    }

    private void MigrateLegacyTrackerState()
    {
        if (hiddenTrackerVersion >= HiddenTrackerVersion)
            return;

        if (hiddenStateResolved)
        {
            if (hiddenResolvedPartInstances == null || hiddenResolvedPartInstances.Count == 0)
                RebuildInstancesFromLegacyState();
            else
            {
                NormalizeResolvedInstances(hiddenTrackerVersion);
                SyncLegacyNamesFromInstances();
            }
        }
        else
        {
            ClearResolvedAnatomy();
        }

        hiddenTrackerVersion = HiddenTrackerVersion;
    }

    private void RebuildInstancesFromLegacyState()
    {
        List<AnatomyPartDef> parts = new List<AnatomyPartDef>();
        if (hiddenResolvedPartDefNames != null && hiddenResolvedPartDefNames.Count > 0)
        {
            for (int i = 0; i < hiddenResolvedPartDefNames.Count; i++)
            {
                string defName = hiddenResolvedPartDefNames[i];
                if (defName.NullOrEmpty())
                    continue;

                AnatomyPartDef part = ResolvePartDef(defName);
                if (part != null && !parts.Contains(part))
                    parts.Add(part);
            }
        }
        else
        {
            if (hiddenHasPenis && LovinModule_GenitalDefOf.Genital_Penis != null)
                parts.Add(LovinModule_GenitalDefOf.Genital_Penis);

            if (hiddenHasVagina && LovinModule_GenitalDefOf.Genital_Vagina != null)
                parts.Add(LovinModule_GenitalDefOf.Genital_Vagina);
        }

        SetResolvedParts(parts);
    }

    private void TrySeedNow(bool forceResync = false)
    {
        if (anatomySeeded && !forceResync && hiddenStateResolved)
            return;

        Pawn pawn = parent as Pawn;
        if (pawn == null)
            return;

        syncInProgress = true;
        AnatomyBootstrapResult result;
        try
        {
            result = AnatomyBootstrapper.TryResolveAndApply(pawn, forceResync: forceResync);
        }
        finally
        {
            syncInProgress = false;
        }

        switch (result)
        {
            case AnatomyBootstrapResult.Resolved:
            case AnatomyBootstrapResult.Skipped:
                anatomySeeded = true;
                seedRetryCount = 0;
                sawGenderWorksChangeSignal = false;
                MarkGraphicsDirty(pawn);
                return;
            case AnatomyBootstrapResult.Pending:
                anatomySeeded = false;
                return;
        }
    }

    private Dictionary<string, AnatomyPartInstance> BuildPreservedInstanceLookup()
    {
        Dictionary<string, AnatomyPartInstance> preserved = new Dictionary<string, AnatomyPartInstance>();
        if (hiddenResolvedPartInstances == null)
            return preserved;

        for (int i = 0; i < hiddenResolvedPartInstances.Count; i++)
        {
            AnatomyPartInstance instance = hiddenResolvedPartInstances[i];
            string defName = instance?.partDef?.defName;
            if (defName.NullOrEmpty() || preserved.ContainsKey(defName))
                continue;

            preserved.Add(defName, instance);
        }

        return preserved;
    }

    private AnatomyPartInstance GetOrCreateInstance(Dictionary<string, AnatomyPartInstance> preserved, AnatomyPartDef part)
    {
        if (part == null)
            return null;

        Pawn pawn = parent as Pawn;
        if (preserved != null && preserved.TryGetValue(part.defName, out AnatomyPartInstance existing) && existing != null)
        {
            existing.partDef = part;
            existing.installedVariant = AnatomyVariantResolver.ResolveInstalledVariant(pawn, part);
            float baseSize = part.baseSize;
            float minSize = part.minSize;
            float maxSize = part.maxSize;
            AnatomyVariantResolver.ApplySizeGeneration(existing.installedVariant, ref baseSize, ref minSize, ref maxSize);
            existing.size = Mathf.Clamp(existing.size, minSize, maxSize);
            NormalizeFluidInstances(pawn, part, existing, HiddenTrackerVersion);
            return existing;
        }

        return AnatomyPartInstanceFactory.Create(pawn, part);
    }

    private void SyncLegacyFlags()
    {
        string penisDefName = LovinModule_GenitalDefOf.Genital_Penis?.defName;
        string vaginaDefName = LovinModule_GenitalDefOf.Genital_Vagina?.defName;
        hiddenHasPenis = !penisDefName.NullOrEmpty() && hiddenResolvedPartDefNames != null && hiddenResolvedPartDefNames.Contains(penisDefName);
        hiddenHasVagina = !vaginaDefName.NullOrEmpty() && hiddenResolvedPartDefNames != null && hiddenResolvedPartDefNames.Contains(vaginaDefName);
    }

    private void SyncLegacyNamesFromInstances()
    {
        if (hiddenResolvedPartDefNames == null)
            hiddenResolvedPartDefNames = new List<string>();
        else
            hiddenResolvedPartDefNames.Clear();

        if (hiddenResolvedPartInstances != null)
        {
            for (int i = 0; i < hiddenResolvedPartInstances.Count; i++)
            {
                string defName = hiddenResolvedPartInstances[i]?.partDef?.defName;
                if (!defName.NullOrEmpty() && !hiddenResolvedPartDefNames.Contains(defName))
                    hiddenResolvedPartDefNames.Add(defName);
            }
        }

        SyncLegacyFlags();
    }

    private static AnatomyPartInstance CloneResolvedInstance(AnatomyPartInstance source)
    {
        if (source == null)
            return null;

        AnatomyPartDef part = ResolvePartDef(source.partDef?.defName);
        if (part == null)
            return null;

        AnatomyPartVariantDef variant = source.installedVariant != null
            ? DefDatabase<AnatomyPartVariantDef>.GetNamedSilentFail(source.installedVariant.defName)
            : null;

        List<AnatomyFluidInstance> fluids = new List<AnatomyFluidInstance>();
        if (source.fluids != null)
        {
            for (int i = 0; i < source.fluids.Count; i++)
            {
                AnatomyFluidInstance fluid = source.fluids[i];
                FluidDef resolvedFluid = fluid?.fluidDef != null
                    ? DefDatabase<FluidDef>.GetNamedSilentFail(fluid.fluidDef.defName)
                    : null;

                if (resolvedFluid == null)
                    continue;

                float capacity = fluid.capacity;
                float amount = Mathf.Clamp(fluid.amount, 0f, Mathf.Max(0f, capacity));
                fluids.Add(new AnatomyFluidInstance(resolvedFluid, capacity, amount));
            }
        }

        return new AnatomyPartInstance(part, variant, source.size, fluids);
    }

    private static AnatomyPartDef ResolvePartDef(string defName)
    {
        return DefDatabase<AnatomyPartDef>.GetNamedSilentFail(defName)
            ?? (AnatomyPartDef)DefDatabase<GenitalDef>.GetNamedSilentFail(defName);
    }

    private void NormalizeResolvedInstances(int previousVersion)
    {
        if (hiddenResolvedPartInstances == null || hiddenResolvedPartInstances.Count == 0)
            return;

        Pawn pawn = parent as Pawn;
        for (int i = 0; i < hiddenResolvedPartInstances.Count; i++)
        {
            AnatomyPartInstance instance = hiddenResolvedPartInstances[i];
            if (instance == null)
                continue;

            AnatomyPartDef resolvedPart = ResolvePartDef(instance.partDef?.defName);
            if (resolvedPart != null)
                instance.partDef = resolvedPart;

            AnatomyPartDef part = instance.partDef;
            if (part == null)
                continue;

            AnatomyPartVariantDef resolvedVariant = instance.installedVariant != null
                ? DefDatabase<AnatomyPartVariantDef>.GetNamedSilentFail(instance.installedVariant.defName)
                : null;
            instance.installedVariant = AnatomyVariantResolver.ResolveInstalledVariant(pawn, part) ?? resolvedVariant;

            float baseSize = part.baseSize;
            float minSize = part.minSize;
            float maxSize = part.maxSize;
            AnatomyVariantResolver.ApplySizeGeneration(instance.installedVariant, ref baseSize, ref minSize, ref maxSize);
            instance.size = Mathf.Clamp(instance.size, minSize, maxSize);
            NormalizeFluidInstances(pawn, part, instance, previousVersion);
        }
    }

    private static void NormalizeFluidInstances(Pawn pawn, AnatomyPartDef part, AnatomyPartInstance instance, int previousVersion)
    {
        if (part == null || instance == null)
            return;

        List<AnatomyFluidInstance> normalized = new List<AnatomyFluidInstance>();
        Dictionary<string, AnatomyFluidInstance> existingByFluidDef = new Dictionary<string, AnatomyFluidInstance>();
        if (instance.fluids != null)
        {
            for (int i = 0; i < instance.fluids.Count; i++)
            {
                AnatomyFluidInstance fluid = instance.fluids[i];
                string fluidDefName = fluid?.fluidDef?.defName;
                if (fluid == null || fluidDefName.NullOrEmpty())
                    continue;

                FluidDef resolvedFluid = DefDatabase<FluidDef>.GetNamedSilentFail(fluidDefName);
                if (resolvedFluid != null)
                    fluid.fluidDef = resolvedFluid;

                if (!existingByFluidDef.ContainsKey(fluidDefName))
                    existingByFluidDef.Add(fluidDefName, fluid);
            }
        }

        float legacyFluidCapacity = previousVersion < HiddenTrackerVersion ? instance.LegacyFluidCapacity : 0f;
        bool consumedLegacyCapacity = false;

        if (part.fluidTemplates != null)
        {
            for (int i = 0; i < part.fluidTemplates.Count; i++)
            {
                AnatomyFluidTemplate template = part.fluidTemplates[i];
                FluidDef templateFluid = template?.fluid;
                if (templateFluid == null)
                    continue;

                string fluidDefName = templateFluid.defName;
                AnatomyFluidInstance fluidInstance = null;
                if (!fluidDefName.NullOrEmpty() && existingByFluidDef.TryGetValue(fluidDefName, out AnatomyFluidInstance existing) && existing != null)
                {
                    existing.fluidDef = templateFluid;
                    fluidInstance = existing;
                }
                else if (!consumedLegacyCapacity && legacyFluidCapacity > 0f)
                {
                    float capacity = Mathf.Max(0f, legacyFluidCapacity);
                    float amount = capacity * Mathf.Clamp01(template.initialFillPercent);
                    fluidInstance = new AnatomyFluidInstance(templateFluid, capacity, amount);
                    consumedLegacyCapacity = true;
                }
                else
                {
                    fluidInstance = AnatomyPartInstanceFactory.GenerateInitialFluid(pawn, part, template);
                }

                if (fluidInstance == null)
                    continue;

                fluidInstance.capacity = Mathf.Max(0f, fluidInstance.capacity);
                fluidInstance.amount = Mathf.Clamp(fluidInstance.amount, 0f, fluidInstance.capacity);
                normalized.Add(fluidInstance);
            }
        }

        foreach (KeyValuePair<string, AnatomyFluidInstance> kvp in existingByFluidDef)
        {
            AnatomyFluidInstance fluidInstance = kvp.Value;
            if (fluidInstance == null)
                continue;

            bool alreadyAdded = false;
            for (int i = 0; i < normalized.Count; i++)
            {
                if (normalized[i]?.fluidDef == fluidInstance.fluidDef)
                {
                    alreadyAdded = true;
                    break;
                }
            }

            if (alreadyAdded)
                continue;

            fluidInstance.capacity = Mathf.Max(0f, fluidInstance.capacity);
            fluidInstance.amount = Mathf.Clamp(fluidInstance.amount, 0f, fluidInstance.capacity);
            normalized.Add(fluidInstance);
        }

        if (normalized.Count == 0)
            instance.fluids = new List<AnatomyFluidInstance>();
        else
            instance.fluids = normalized;

        instance.ClearLegacyFluidCapacity();
    }

    private static void MarkGraphicsDirty(Pawn pawn)
    {
        if (pawn == null)
            return;

        LongEventHandler.ExecuteWhenFinished(delegate
        {
            try
            {
                if (pawn.DestroyedOrNull())
                    return;

                pawn.Drawer?.renderer?.renderTree?.SetDirty();
                pawn.Drawer?.renderer?.SetAllGraphicsDirty();
                PortraitsCache.SetDirty(pawn);
            }
            catch (System.Exception ex)
            {
                Log.Error($"[Despicable NSFW] Failed to mark anatomy graphics dirty for {pawn}:\n{ex}");
            }
        });
    }
}
