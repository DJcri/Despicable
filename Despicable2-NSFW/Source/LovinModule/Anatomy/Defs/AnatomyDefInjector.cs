using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Despicable;
internal static class AnatomyDefInjector
{
    internal static void InjectForAllHumanlikes()
    {
        List<ThingDef> thingDefs = DefDatabase<ThingDef>.AllDefsListForReading;
        if (thingDefs == null)
            return;

        HashSet<BodyDef> patchedBodies = new HashSet<BodyDef>();
        for (int i = 0; i < thingDefs.Count; i++)
        {
            ThingDef thingDef = thingDefs[i];
            if (thingDef?.race == null || !thingDef.race.Humanlike)
                continue;

            EnsureThingComps(thingDef);

            BodyDef body = thingDef.race.body;
            if (body != null && patchedBodies.Add(body))
                EnsureExternalGenitalsPart(body);
        }
    }

    private static void EnsureThingComps(ThingDef thingDef)
    {
        if (thingDef.comps == null)
            thingDef.comps = new List<CompProperties>();

        if (!HasComp(thingDef.comps, typeof(CompProperties_AnatomyBootstrap)))
            thingDef.comps.Add(new CompProperties_AnatomyBootstrap());

        if (!HasComp(thingDef.comps, typeof(CompProperties_LovinParts)))
            thingDef.comps.Add(new CompProperties_LovinParts());
    }

    private static bool HasComp(List<CompProperties> comps, System.Type wantedType)
    {
        if (comps == null || wantedType == null)
            return false;

        for (int i = 0; i < comps.Count; i++)
        {
            CompProperties comp = comps[i];
            if (comp != null && wantedType.IsAssignableFrom(comp.GetType()))
                return true;
        }

        return false;
    }

    private static void EnsureExternalGenitalsPart(BodyDef body)
    {
        if (body?.corePart == null || LovinModule_AnatomyDefOf.D2_ExternalGenitals == null)
            return;

        BodyPartRecord pelvis = FindGenitalAnchor(body);
        if (pelvis == null)
            return;

        if (FindPart(pelvis, LovinModule_AnatomyDefOf.D2_ExternalGenitals) != null)
            return;

        if (pelvis.parts == null)
            pelvis.parts = new List<BodyPartRecord>();

        BodyPartRecord genitals = new BodyPartRecord();
        genitals.def = LovinModule_AnatomyDefOf.D2_ExternalGenitals;
        genitals.parent = pelvis;
        genitals.coverage = 0.012f;
        genitals.height = BodyPartHeight.Bottom;
        genitals.depth = BodyPartDepth.Outside;
        genitals.groups = new List<BodyPartGroupDef>();

        if (DefDatabase<BodyPartGroupDef>.GetNamedSilentFail("D2_Genitals") is BodyPartGroupDef groupDef)
            genitals.groups.Add(groupDef);

        pelvis.parts.Add(genitals);
    }

    private static BodyPartRecord FindGenitalAnchor(BodyDef body)
    {
        if (body?.corePart == null)
            return null;

        BodyPartDef pelvisDef = DefDatabase<BodyPartDef>.GetNamedSilentFail("Pelvis");
        if (pelvisDef != null)
        {
            BodyPartRecord pelvis = FindPart(body.corePart, pelvisDef);
            if (pelvis != null)
                return pelvis;
        }

        return FindPartByDefName(body.corePart, "Pelvis");
    }

    private static BodyPartRecord FindPartByDefName(BodyPartRecord root, string defName)
    {
        if (root == null || string.IsNullOrEmpty(defName))
            return null;

        if (root.def?.defName == defName)
            return root;

        List<BodyPartRecord> parts = root.parts;
        if (parts == null)
            return null;

        for (int i = 0; i < parts.Count; i++)
        {
            BodyPartRecord found = FindPartByDefName(parts[i], defName);
            if (found != null)
                return found;
        }

        return null;
    }

    private static BodyPartRecord FindPart(BodyPartRecord root, BodyPartDef def)
    {
        if (root == null || def == null)
            return null;

        if (root.def == def)
            return root;

        List<BodyPartRecord> parts = root.parts;
        if (parts == null)
            return null;

        for (int i = 0; i < parts.Count; i++)
        {
            BodyPartRecord found = FindPart(parts[i], def);
            if (found != null)
                return found;
        }

        return null;
    }
}
