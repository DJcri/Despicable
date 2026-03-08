using System;
using HarmonyLib;
using Verse;
using Despicable.Core.Bootstrap;
using Despicable.Core.Staging.Backends;
using Despicable.Core.Staging.Providers;

namespace Despicable;
/// <summary>
/// RimWorld 1.6 no longer supports &lt;modClass&gt; in About.xml. This bootstrap is the single
/// source of truth for Core startup: staging registrations first, Harmony patching second.
/// Any other startup trigger should delegate here rather than reimplementing init logic.
/// </summary>
[StaticConstructorOnStartup]
public static class DespicableBootstrap
{
    public const string HarmonyId = "com.DCSzar.Despicable";

    private static readonly BasicStagePawnTags BasicTagProvider = new();
    private static readonly StageClipTimelineBackend ClipTimelineBackend = new();
    private static readonly StagePlaybackBackend PlaybackBackend = new();

    // Guardrail-Allow-Static: One-time core bootstrap gate owned by static startup lifecycle.
    private static bool _initialized;

    static DespicableBootstrap()
    {
        EnsureInitialized();
    }

    public static void EnsureInitialized()
    {
        if (_initialized)
            return;

        _initialized = true;

        try
        {
            RegisterCoreRuntime();
        }
        catch (Exception e)
        {
            Log.Error($"[Despicable2.Core] Bootstrap staging init failed: {e}");
        }

        try
        {
            ModMain.harmony = BootstrapUtil.GetOrCreateHarmony(ModMain.harmony, HarmonyId);
            BootstrapUtil.PatchAssemblySafely(ModMain.harmony, typeof(DespicableBootstrap).Assembly, "[Despicable2.Core]");
            try
            {
                Despicable.HeroKarma.Patches.HeroKarma.HKPatchGuard.Finalize(ModMain.harmony, HarmonyId);
            }
            catch (Exception e2)
            {
                Log.Warning("[Despicable2.Core] HKPatchGuard finalize failed: " + e2);
            }

            Log.Message("[Despicable2.Core] Bootstrap initialized.");
        }
        catch (Exception e)
        {
            Log.Error($"[Despicable2.Core] Bootstrap harmony init failed: {e}");
        }
    }

    private static void RegisterCoreRuntime()
    {
        Despicable.Core.Staging.StageTagProviders.Register(BasicTagProvider);
        Despicable.Core.Staging.StagePlaybackBackends.Register(ClipTimelineBackend);
        Despicable.Core.Staging.StagePlaybackBackends.Register(PlaybackBackend);
    }
}
