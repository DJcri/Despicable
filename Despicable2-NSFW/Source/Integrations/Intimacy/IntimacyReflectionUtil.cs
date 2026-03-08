using System;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace Despicable.NSFW.Integrations.Intimacy;
/// <summary>
/// Soft reflection helpers for Intimacy (LoveyDovey.Sex.WithEuterpe).
/// We use this ONLY to *read* data for animation selection (never to change Intimacy outcomes).
/// </summary>
internal static class IntimacyReflectionUtil
{
    private static readonly ReflectionCache Cache = new();

    internal static void PrimeCache()
    {
        try
        {
            EnsureCached();
        }
        catch (Exception e)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce("IntimacyReflectionUtil.PrimeCache", "Intimacy pregnancy chance cache warmup failed.", e);
        }
    }

    internal static bool TryGetPregnancyChance(Pawn a, Pawn b, out float chance)
    {
        chance = 0f;
        try
        {
            if (!IntegrationGuards.IsIntimacyLoaded()) return false;
            EnsureCached();
            if (Cache.PregnancyChanceForPartners == null) return false;
            chance = Cache.PregnancyChanceForPartners(a, b);
            if (chance < 0f) chance = 0f;
            if (chance > 1f) chance = 1f;
            return true;
        }
        catch (Exception e)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce("IntimacyReflectionUtil.TryGetPregnancyChance", "Intimacy pregnancy chance query failed.", e);
            return false;
        }
    }

    internal static void ResetRuntimeState()
    {
        Cache.HasSearched = false;
        Cache.PregnancyChanceForPartners = null;
    }

    private static void EnsureCached()
    {
        if (Cache.HasSearched) return;
        Cache.HasSearched = true;

        try
        {
            // Known type/method names from the Intimacy assembly:
            // LoveyDoveySexWithEuterpe.PregnancyUtility.PregnancyChanceForPartners(Pawn, Pawn) : float
            var t = AccessTools.TypeByName("LoveyDoveySexWithEuterpe.PregnancyUtility");
            if (t == null) return;

            var mi = AccessTools.Method(t, "PregnancyChanceForPartners", new[] { typeof(Pawn), typeof(Pawn) });
            if (mi == null) return;
            if (mi.ReturnType != typeof(float)) return;

            Cache.PregnancyChanceForPartners = (Func<Pawn, Pawn, float>)Delegate.CreateDelegate(typeof(Func<Pawn, Pawn, float>), mi);
        }
        catch (Exception e)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce("IntimacyReflectionUtil.EnsureCached", "Intimacy pregnancy utility cache setup failed; leaving cache empty.", e);
        }
    }

    private sealed class ReflectionCache
    {
        public bool HasSearched;
        public Func<Pawn, Pawn, float> PregnancyChanceForPartners;
    }
}
