using System;
using HarmonyLib;
using Verse;

namespace Despicable.HeroKarma.Patches.HeroKarma;

[HarmonyPatch(typeof(Pawn), nameof(Pawn.SpawnSetup), new Type[] { typeof(Map), typeof(bool) })]
public static class HarmonyPatch_Pawn_SpawnSetup_EncounterEcho
{
    public static void Postfix(Pawn __instance, Map map, bool respawningAfterLoad)
    {
        try
        {
            if (!HKSettingsUtil.ModuleEnabled)
                return;

            Pawn target = __instance;
            if (target == null || target.RaceProps == null || !target.RaceProps.Humanlike)
                return;

            Pawn hero = HKRuntime.GetHeroPawnSafe();
            if (hero == null || hero == target)
                return;

            Map heroMap = hero.MapHeld ?? hero.Map;
            Map targetMap = map ?? target.MapHeld ?? target.Map;
            if (heroMap == null || targetMap == null || heroMap != targetMap)
                return;

            LocalReputationUtility.TryEnsureEncounterEchoRecord(hero, target);
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce("HarmonyPatch_Pawn_SpawnSetup_EncounterEcho", "Hero Karma encounter echo hydration suppressed an exception.", ex);
        }
    }
}
