using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Despicable.NSFW.Integrations.Intimacy;
/// <summary>
/// Soft helpers that borrow selected Intimacy tuning values without replacing its mechanics wholesale.
/// We only apply low-risk effects that Despicable does not already handle directly.
///
/// Reflection caches now live inside a small runtime-state object so the mutable
/// integration cache is grouped in one place instead of spread across static fields.
/// </summary>
internal static class IntimacyApplyUtil
{
    private static readonly ReflectionRuntimeState runtimeState = new();

    internal static void PrimeCache()
    {
        try
        {
            runtimeState.EnsureCached();
        }
        catch (Exception e)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce("IntimacyApplyUtil.PrimeCache", "Intimacy post-lovin cache warmup failed.", e);
        }
    }

    internal static void TryApplyPostLovinEffects(Pawn a, Pawn b)
    {
        if (!IntegrationGuards.IsIntimacyLoaded())
            return;

        if (a == null || b == null)
            return;

        try
        {
            runtimeState.EnsureCached();

            float intimacyFromLovin = runtimeState.GetConfiguredAmount(runtimeState.IntimacyFromLovinField, 0.2f);
            float recreationFromLovin = runtimeState.GetConfiguredAmount(runtimeState.RecreationFromLovinField, 0.2f);

            TryGainIntimacy(a, intimacyFromLovin);
            TryGainIntimacy(b, intimacyFromLovin);

            TryGainRecreation(a, recreationFromLovin);
            TryGainRecreation(b, recreationFromLovin);

            TryGenerateNextLovinTick(a);
            TryGenerateNextLovinTick(b);
        }
        catch (Exception e)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce("IntimacyApplyUtil.PostLovinEffects", "Intimacy soft integration failed during post-lovin effects; continuing without borrowed tuning.", e);
        }
    }

    private static void TryGainIntimacy(Pawn pawn, float amount)
    {
        if (pawn == null || amount <= 0f)
            return;

        try
        {
            runtimeState.GainIntimacy?.Invoke(pawn, amount);
        }
        catch (Exception e)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce("IntimacyApplyUtil.GainIntimacy", "Intimacy soft integration failed to apply intimacy gain.", e);
        }
    }

    private static void TryGainRecreation(Pawn pawn, float amount)
    {
        if (pawn == null || amount <= 0f)
            return;

        var joy = pawn.needs?.joy;
        if (joy == null)
            return;

        try
        {
            joy.GainJoy(amount, JoyKindDefOf.Social);
        }
        catch (Exception e)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce("IntimacyApplyUtil.GainRecreation", "Intimacy soft integration failed to apply recreation gain.", e);
        }
    }

    private static void TryGenerateNextLovinTick(Pawn pawn)
    {
        if (pawn == null)
            return;

        try
        {
            runtimeState.GenerateTicksToNextLovin?.Invoke(pawn);
        }
        catch (Exception e)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce("IntimacyApplyUtil.NextLovinTick", "Intimacy soft integration failed to refresh next lovin tick.", e);
        }
    }

    internal static void ResetRuntimeState()
    {
        runtimeState.ResetRuntimeState();
    }

    private sealed class ReflectionRuntimeState
    {
        private bool hasSearchedCache;
        private Action<Pawn, float> gainIntimacy;
        private Action<Pawn> generateTicksToNextLovin;
        private FieldInfo intimacyFromLovinField;
        private FieldInfo recreationFromLovinField;

        internal Action<Pawn, float> GainIntimacy => gainIntimacy;
        internal Action<Pawn> GenerateTicksToNextLovin => generateTicksToNextLovin;
        internal FieldInfo IntimacyFromLovinField => intimacyFromLovinField;
        internal FieldInfo RecreationFromLovinField => recreationFromLovinField;

        internal void ResetRuntimeState()
        {
            hasSearchedCache = false;
            gainIntimacy = null;
            generateTicksToNextLovin = null;
            intimacyFromLovinField = null;
            recreationFromLovinField = null;
        }

        internal float GetConfiguredAmount(FieldInfo field, float fallback)
        {
            try
            {
                if (field == null)
                    return fallback;

                var value = field.GetValue(null);
                if (value is float f)
                    return f;
            }
            catch (Exception e)
            {
                Despicable.Core.DebugLogger.WarnExceptionOnce("IntimacyApplyUtil.ConfigFallback", "Intimacy soft integration failed to read configured tuning; using fallback values.", e);
            }

            return fallback;
        }

        internal void EnsureCached()
        {
            if (hasSearchedCache)
                return;

            hasSearchedCache = true;

            try
            {
                var commonChecks = AccessTools.TypeByName("LoveyDoveySexWithEuterpe.CommonChecks");
                if (commonChecks != null)
                {
                    var gainIntimacyMethod = AccessTools.Method(commonChecks, "TryGainIntimacy", new[] { typeof(Pawn), typeof(float) });
                    if (gainIntimacyMethod != null)
                        gainIntimacy = (Action<Pawn, float>)Delegate.CreateDelegate(typeof(Action<Pawn, float>), gainIntimacyMethod);

                    intimacyFromLovinField = AccessTools.Field(commonChecks, "IntimacyFromLovin");
                    recreationFromLovinField = AccessTools.Field(commonChecks, "RecreationFromLovin");
                }

                var sexUtilities = AccessTools.TypeByName("LoveyDoveySexWithEuterpe.SexUtilities");
                if (sexUtilities != null)
                {
                    var nextLovinMethod = AccessTools.Method(sexUtilities, "GenerateRandomTicksToNextLovin", new[] { typeof(Pawn) });
                    if (nextLovinMethod != null)
                        generateTicksToNextLovin = (Action<Pawn>)Delegate.CreateDelegate(typeof(Action<Pawn>), nextLovinMethod);
                }
            }
            catch (Exception e)
            {
                Despicable.Core.DebugLogger.WarnExceptionOnce("IntimacyApplyUtil.CacheSetup", "Intimacy soft integration failed while caching reflected hooks; leaving caches empty.", e);
            }
        }
    }
}
