using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace Despicable.Core.Bootstrap;
/// <summary>
/// Shared startup helpers for safe Harmony application and one-time bootstrap wiring.
/// Keeping this in Core lets dependent assemblies (ex: NSFW) reuse the exact same patching rules.
/// </summary>
public static class BootstrapUtil
{
    public static Harmony GetOrCreateHarmony(Harmony existing, string harmonyId)
    {
        return existing ?? new Harmony(harmonyId);
    }

    public static void PatchAssemblySafely(Harmony harmony, Assembly asm, string logPrefix)
    {
        int patched = 0;
        int failed = 0;
        int skipped = 0;

        Type[] types;
        try
        {
            types = asm.GetTypes();
        }
        catch (ReflectionTypeLoadException rtle)
        {
            types = rtle.Types.Where(x => x != null).ToArray();
            Log.Warning($"{logPrefix} Assembly.GetTypes() had loader exceptions; continuing with {types.Length} types.");
        }

        for (int i = 0; i < types.Length; i++)
        {
            var t = types[i];
            if (t == null)
                continue;

            if (!IsHarmonyPatchType(t))
            {
                skipped++;
                continue;
            }

            try
            {
                new PatchClassProcessor(harmony, t).Patch();
                patched++;
            }
            catch (Exception ex)
            {
                failed++;
                Log.Error($"{logPrefix} Harmony patch failed for {t.FullName}: {ex}");
            }
        }

        Log.Message($"{logPrefix} Harmony patching complete. Patched={patched} Failed={failed} Skipped(non-patch)={skipped}");
    }

    private static bool IsHarmonyPatchType(Type t)
    {
        if (t.GetCustomAttributes(typeof(HarmonyPatch), true).Any())
            return true;

        var methods = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
        for (int i = 0; i < methods.Length; i++)
        {
            if (methods[i].GetCustomAttributes(typeof(HarmonyPatch), true).Any())
                return true;
        }

        return false;
    }
}
