using Despicable.AnimGroupStudio.Preview;
using Despicable.FacePartsModule.Compatibility.PawnEditorCompat;
using Despicable.AnimModule.Runtime.Graphics;
using Despicable.Core;
using Despicable.HeroKarma;
using Despicable.HeroKarma.Patches.HeroKarma;
using Despicable.UIFramework;
using Despicable.UIFramework.Controls;

namespace Despicable;
/// <summary>
/// Central lifecycle hub for ephemeral cross-system runtime state.
/// New game and load boundaries should funnel through this type so individual
/// cache owners do not drift into bespoke reset paths.
/// </summary>
public static class DespicableRuntimeState
{
    public static void ResetRuntimeState()
    {
        VisualActivityTracker.ResetRuntimeState();
        HarmonyPatch_DrawTracker_DrawPos.ResetRuntimeState();
        HarmonyPatch_PawnRenderTree_TryGetMatrix.ResetRuntimeState();
        GraphicStateResolver.ResetRuntimeState();
        WorkshopRenderContext.ResetRuntimeState();
        AgsPreviewNodeCapture.ResetRuntimeState();
        PreviewPawnIdAllocator.ResetRuntimeState();
        ReentryGuard.ResetRuntimeState();
        SingleFrameDialogGate.ResetRuntimeState();
        HarmonyPatch_AttackNeutral.ResetRuntimeState();
        HKEventDebouncer.ResetRuntimeState();
        InteractionRegistry.ResetRuntimeState();
        HKGoodwillContext.ResetRuntimeState();
        HKGiftContext.ResetRuntimeState();
        HKReleaseContext.ResetRuntimeState();
        D2CommandBar.ResetRuntimeState();
        D2Fields.ResetRuntimeState();
        HKDiagnostics.ResetRuntimeState();
        UIRectRegistry.ResetRuntimeState();
        DebugLogger.ResetRuntimeState();
        HarmonyPatch_PawnEditor_AppearanceEditor.ResetRuntimeState();
        AutoEyePatchRuntime.ResetRuntimeState();
        FacePartsEventRuntime.ResetRuntimeState();
        HKPerkEffects.ResetRuntimeState();
        HKBackendBridge.ResetRuntimeState();
        GameComponent_ExtendedAnimatorRuntime.ResetRuntimeState();
    }

    public static void ResetAll()
    {
        ResetRuntimeState();
    }
}
