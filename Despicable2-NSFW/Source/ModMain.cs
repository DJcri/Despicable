using System;
using System.Linq;
using HarmonyLib;
using Verse;
using Despicable.Core.Bootstrap;
using Despicable.NSFW.Integrations;

namespace Despicable.NSFW;
public sealed class ModMain : Mod
{
    public const string HarmonyId = "dj.despicable2.nsfw";

    public static ModMain Instance { get; private set; }
    public static Harmony Harmony { get; private set; }

    // Guardrail-Allow-Static: One-time NSFW startup gate owned by mod initialization lifecycle.
    private static bool _initialized;

    public ModMain(ModContentPack content) : base(content)
    {
        Instance = this;
        EnsureInitialized();
    }

    public static void EnsureInitialized()
    {
        if (_initialized)
            return;

        _initialized = true;

        // Optional: sanity check Core is loaded (your assembly ref already enforces this,
        // but this gives nicer logs if someone messes up load order).
        if (!LoadedModManager.RunningModsListForReading.Any(m =>
                m.PackageIdPlayerFacing != null &&
                m.PackageIdPlayerFacing.ToLower().Contains("despicable2") &&
                m.PackageIdPlayerFacing.ToLower().Contains("core")))
        {
            Log.Warning("[Despicable2.NSFW] Core mod not detected in running mod list. " +
                        "If you see missing type errors, check load order (Core before NSFW)."
            );
        }

        Harmony = BootstrapUtil.GetOrCreateHarmony(Harmony, HarmonyId);
        BootstrapUtil.PatchAssemblySafely(Harmony, typeof(ModMain).Assembly, "[Despicable2.NSFW]");

        HookBootstraps.EnsureRegistered();
        NsfwCompatBootstrap.EnsureInitialized();

        Log.Message("[Despicable2.NSFW] Initialized.");
    }
}
