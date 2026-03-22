using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace Despicable
{
    /// <summary>
    /// Core mod entrypoint. Owns settings access and delegates all startup behavior to
    /// <see cref="DespicableBootstrap"/> so constructor and static initialization stay aligned.
    /// </summary>
    public class ModMain : Mod
    {
        public static ModMain Instance { get; internal set; }
        public static Harmony harmony { get; internal set; }
        public const string ModName = "Despicable";
        public Settings settings;
        private readonly D2ModSettingsRenderer settingsRenderer = new();

        // Mod check bools
        public static bool nlFacialInstalled => IsNlFacialInstalled;
        public static bool IsNlFacialInstalled { get; private set; }

        public ModMain(ModContentPack content) : base(content)
        {
            Instance = this;
            settings = GetSettings<Settings>();
            if (IsNlFacialInstalled)
                settings.facialPartsExtensionEnabled = false;

            // Delegate all assembly startup to the shared bootstrap so constructor and static-load
            // paths cannot drift apart over time.
            DespicableBootstrap.EnsureInitialized();

            LongEventHandler.ExecuteWhenFinished(() =>
            {
                try
                {
                    FacePartsUtil.LoadHeadTypeBlacklist();
                }
                catch (Exception e)
                {
                    Log.Error($"[Despicable] Failed to load head blacklist during deferred startup: {e}");
                }
            });
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            settingsRenderer.Draw(inRect, settings);
            base.DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory()
        {
            return "ModName".Translate();
        }

        static ModMain()
        {
            foreach (var mod in ModsConfig.ActiveModsInLoadOrder)
            {
                // For mod compatibility, let the mod instance know
                // Which mods are active, so it can adjust the proper settings
                // in the constructor
                switch (mod.PackageId.ToLower())
                {
                    case "nals.facialanimation":
                        IsNlFacialInstalled = true;
                        break;
                }
            }
        }

        // This is the Harmony patch that ensures your loading function is called at the right time.
        [HarmonyPatch(typeof(PlayDataLoader), "DoPlayLoad")]
        public static class PlayDataLoader_DoPlayLoad_Patch
        {
            [HarmonyPostfix]
            public static void Postfix()
            {
                // This line calls your loading function only after all Defs are loaded.
                FacePartsUtil.LoadHeadTypeBlacklist();
                AutoEyePatchRuntime.EnsureGenerated();
                Despicable.HeroKarma.HKBalanceTuning.ApplyPerkDefOverrides();
            }
        }
    }


// ---- 1.6 bootstrap: ensure Harmony patches run even without <modClass> in About.xml ----
[StaticConstructorOnStartup]
internal static class DespicableCoreBootstrap
{
    static DespicableCoreBootstrap()
    {
        // Secondary entrypoint kept for compatibility, but all real startup work now lives in
        // DespicableBootstrap so there is only one initialization path to maintain.
        DespicableBootstrap.EnsureInitialized();
    }
}

// Add the Core interaction menu entrypoint into the vanilla right-click menu.
[HarmonyPatch]
internal static class HarmonyPatch_FloatMenuMakerMap_DespicableCore
{
    private static MethodBase TargetMethod()
    {
        // 1.6 adds a FloatMenuContext (by-ref). Use a runtime lookup so we work across signatures.
        var m = AccessTools.Method(typeof(FloatMenuMakerMap), "GetOptions",
            new[] { typeof(List<Pawn>), typeof(Vector3), typeof(FloatMenuContext).MakeByRefType() });

        return m ?? AccessTools.Method(typeof(FloatMenuMakerMap), "GetOptions",
            new[] { typeof(List<Pawn>), typeof(Vector3) });
    }

    private static void Postfix(List<Pawn> selectedPawns, Vector3 clickPos, ref List<FloatMenuOption> __result)
    {
        if (__result == null) return;
        if (selectedPawns.NullOrEmpty()) return;

        var pawn = selectedPawns[0];
        if (pawn == null) return;

        try
        {
            InteractionMenu.InitInteractionMenu(pawn, __result, clickPos);
        }
        catch (Exception e)
        {
            Log.Error($"[Despicable2.Core] Error adding Interaction menu options: {e}");
        }
    }
}

// Ensure face parts are ready before any portrait consumer (vanilla or modded) requests a render.
[HarmonyPatch]
internal static class HarmonyPatch_PortraitsCache_Get_DespicableFaceWarmup
{
    private const string PortraitWarmupGuardKey = "PortraitsCache.Get";

    private static MethodBase TargetMethod()
    {
        List<MethodInfo> methods = AccessTools.GetDeclaredMethods(typeof(PortraitsCache));
        for (int i = 0; i < methods.Count; i++)
        {
            MethodInfo method = methods[i];
            if (method == null || method.Name != "Get")
                continue;

            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length == 0)
                continue;

            if (parameters[0].ParameterType == typeof(Pawn))
                return method;
        }

        return null;
    }

    private static void Prefix(Pawn pawn, out FacePartsPortraitRenderContext.Scope __state)
    {
        __state = new FacePartsPortraitRenderContext.Scope(!WorkshopRenderContext.Active);

        if (pawn == null)
            return;

        if (CompFaceParts.GlobalWarmupNeededCount <= 0)
            return;

        using ReentryGuard.Scope guard = ReentryGuard.Enter(PortraitWarmupGuardKey);
        if (!guard.IsEntered)
            return;

        if (ModMain.IsNlFacialInstalled)
            return;

        Settings settings = ModMain.Instance?.settings;
        if (settings != null && !settings.facialPartsExtensionEnabled)
            return;

        if (pawn.RaceProps?.Humanlike != true)
            return;

        if (pawn.health?.hediffSet == null)
            return;

        if (pawn.Drawer?.renderer == null)
            return;

        CompFaceParts faceParts = pawn.TryGetComp<CompFaceParts>();
        if (faceParts == null)
            return;

        if (!faceParts.NeedsPortraitWarmup())
            return;

        try
        {
            faceParts.TryWarmPortraitFastThisTick(false);
        }
        catch (Exception e)
        {
            Log.Error($"[Despicable] - Error warming facial parts before portrait render: {e}");
        }
    }

    private static void Finalizer(Exception __exception, FacePartsPortraitRenderContext.Scope __state)
    {
        __state.Dispose();
    }
}



}