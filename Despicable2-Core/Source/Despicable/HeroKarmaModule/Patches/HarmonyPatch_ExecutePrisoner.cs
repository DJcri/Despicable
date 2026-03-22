using System;
using HarmonyLib;
using Verse;

namespace Despicable.HeroKarma.Patches.HeroKarma;
[HarmonyPatch]
public static partial class HarmonyPatch_ExecutePrisoner
{
    private static void Prefix(object[] __args, ref HKGoodwillContext.Scope __state)
    {
        __state = default;
        try
        {
            if (!TryGetExecutionerAndVictim(__args, out Pawn executioner, out Pawn victim))
            {
                return;
            }

            if (!HKHookUtilSafe.ActorIsHero(executioner) || !IsValidExecutionVictim(victim))
            {
                return;
            }

            __state = HKGoodwillContext.Enter(executioner);
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

            if (!HKHookUtilSafe.ActorIsHero(executioner) || !IsValidExecutionVictim(victim))
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

    private static void Finalizer(Exception __exception, HKGoodwillContext.Scope __state)
    {
        try
        {
            __state.Dispose();
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
