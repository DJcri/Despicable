using System;
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

        for (int i = 0; i < thingDefs.Count; i++)
        {
            ThingDef thingDef = thingDefs[i];
            if (thingDef?.race == null || !thingDef.race.Humanlike)
                continue;

            EnsureThingComps(thingDef);
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

    private static bool HasComp(List<CompProperties> comps, Type wantedType)
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
}
