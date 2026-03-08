using System;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace Despicable.NSFW.Integrations.Intimacy;
/// <summary>
/// Placeholder: We are intentionally NOT patching Intimacy pregnancy mechanics.
///
/// This patch is deliberately disabled via Prepare() returning false.
/// We still log a single message so you know it was intentionally skipped.
/// </summary>
[HarmonyPatch]
internal static class HarmonyPatch_Intimacy_PregnancyGate
{
    public static bool Prepare()
    {
        Log.Message("[Despicable2.NSFW] PregnancyGate patch is intentionally disabled (no pregnancy mechanics patching). Skipping.");
        return false;
    }

    public static MethodBase TargetMethod()
    {
        return null;
    }
}
