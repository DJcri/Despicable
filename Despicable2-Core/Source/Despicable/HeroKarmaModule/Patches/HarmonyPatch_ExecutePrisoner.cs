using System;
using HarmonyLib;
using Verse;

namespace Despicable.HeroKarma.Patches.HeroKarma;
[HarmonyPatch]
public static partial class HarmonyPatch_ExecutePrisoner
{
    public static void Prefix(object[] __args, ref bool __state)
    {
        __state = false;
        try
        {
            if (!TryGetExecutionerAndVictim(__args, out Pawn executioner, out _))
            {
                return;
            }

            if (!HKHookUtilSafe.ActorIsHero(executioner))
            {
                return;
            }

            HKGoodwillContext.Begin(executioner);
            __state = true;
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "HarmonyPatch_ExecutePrisoner:1",
                "HarmonyPatch_ExecutePrisoner suppressed an exception.",
                ex);
        }
    }

    public static void Postfix(object[] __args)
    {
        if (!HKSettingsUtil.HookEnabled("ExecutePrisoner"))
        {
            return;
        }

        try
        {
            if (!TryGetExecutionerAndVictim(__args, out Pawn executioner, out Pawn victim))
            {
                return;
            }

            if (!HKHookUtilSafe.ActorIsHero(executioner))
            {
                return;
            }

            int factionId = HKHookUtil.GetFactionIdSafe(victim);
            var karmaEvent = KarmaEvent.Create(
                "ExecutePrisoner",
                executioner,
                victim,
                factionId);
            HKKarmaProcessor.Process(karmaEvent);
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "HarmonyPatch_ExecutePrisoner:2",
                "HarmonyPatch_ExecutePrisoner suppressed an exception.",
                ex);
        }
    }

    public static void Finalizer(Exception __exception, bool __state)
    {
        if (!__state)
        {
            return;
        }

        try
        {
            HKGoodwillContext.End();
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "HarmonyPatch_ExecutePrisoner:3",
                "HarmonyPatch_ExecutePrisoner suppressed an exception.",
                ex);
        }
    }
}
