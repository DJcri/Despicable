using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Despicable.FacePartsModule.UI;

namespace Despicable.FacePartsModule.Compatibility.PawnEditorCompat;
internal static class HarmonyPatch_PawnEditor_AppearanceEditor
{
    private const string LogPrefix = "Pawn Editor compat";
    // Guardrail-Allow-Static: Cached reflected PawnEditor field handle, resolved once per compat patch application.
    private static FieldInfo pawnFieldInfo;
    private static ConditionalWeakTable<Pawn, AppearanceSignatureBox> appearanceSignatureByPawn = new();

    private sealed class AppearanceSignatureBox
    {
        public bool Initialized;
        public int Signature;
    }

    public static void ResetRuntimeState()
    {
        appearanceSignatureByPawn = new ConditionalWeakTable<Pawn, AppearanceSignatureBox>();
        // The Harmony postfix remains installed across save/load boundaries.
        // Keep the cached reflected field available when possible. If the dialog
        // shape changes, the postfix will lazily re-resolve it from the live instance.
    }

    public static void Apply(Harmony harmony)
    {
        ResetRuntimeState();

        if (harmony == null)
            return;

        var dialogType = AccessTools.TypeByName("PawnEditor.Dialog_AppearanceEditor");
        if (dialogType == null)
        {
            Despicable.Core.DebugLogger.Warn("D2C_CODE_45DCE70A".Translate(LogPrefix));
            return;
        }

        pawnFieldInfo = ResolvePawnField(dialogType);
        if (pawnFieldInfo == null)
        {
            Despicable.Core.DebugLogger.Warn($"{LogPrefix}: no Pawn field was found on {dialogType.FullName}. Face Parts button was not injected.");
            return;
        }

        var target = AccessTools.Method(dialogType, "DoLeftSection", new[] { typeof(Rect) });
        if (target == null)
        {
            Despicable.Core.DebugLogger.Warn("D2C_CODE_52F3F63E".Translate(LogPrefix));
            return;
        }

        var postfix = new HarmonyMethod(AccessTools.Method(typeof(HarmonyPatch_PawnEditor_AppearanceEditor), nameof(Postfix)));
        harmony.Patch(target, postfix: postfix);
        Despicable.Core.DebugLogger.Debug($"{LogPrefix}: patched {target.DeclaringType?.FullName}.{target.Name} for Face Parts button injection.");
    }

    private static FieldInfo ResolvePawnField(Type dialogType)
    {
        return PawnOwnerReflectionUtil.ResolvePawnField(dialogType);
    }

    private static FieldInfo GetOrResolvePawnField(object instance)
    {
        if (instance == null)
            return pawnFieldInfo;

        var instanceType = instance.GetType();
        if (pawnFieldInfo != null && pawnFieldInfo.DeclaringType != null && pawnFieldInfo.DeclaringType.IsAssignableFrom(instanceType))
            return pawnFieldInfo;

        pawnFieldInfo = ResolvePawnField(instanceType);
        return pawnFieldInfo;
    }

    private static void Postfix(object __instance, Rect inRect)
    {
        if (__instance == null)
            return;

        var activePawnField = GetOrResolvePawnField(__instance);
        if (activePawnField == null)
            return;

        Pawn pawn;
        try
        {
            pawn = activePawnField.GetValue(__instance) as Pawn;
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce("PawnEditorCompat/GetPawn", "Pawn Editor compat: failed to resolve the current pawn.", ex);
            return;
        }

        if (pawn == null || pawn.RaceProps == null || !pawn.RaceProps.Humanlike)
            return;

        FaceRuntimeActivityManager.NotifyEditorHeartbeat(pawn, Time.frameCount);

        Rect buttonRect = BuildFacePartsButtonRect(inRect);
        if (buttonRect.width <= 0f || buttonRect.height <= 0f)
            return;

        CompFaceParts comp = pawn.TryGetComp<CompFaceParts>();
        if (comp == null)
        {
            TooltipHandler.TipRegion(buttonRect, "D2C_CODE_AF6C0280".Translate(pawn.def?.defName ?? "Unknown"));
            bool wasEnabled = GUI.enabled;
            GUI.enabled = false;
            Widgets.ButtonText(buttonRect, "D2C_CODE_D5D334BD".Translate());
            GUI.enabled = wasEnabled;
            return;
        }

        comp.NotifyEditorHeartbeat(Time.frameCount);
        RefreshEditedPawnFaceIfNeeded(pawn, comp);

        TooltipHandler.TipRegion(buttonRect, "D2C_CODE_97865B7F".Translate());
        if (Widgets.ButtonText(buttonRect, "D2C_CODE_D5D334BD".Translate()))
            Find.WindowStack.Add(new Dialog_D2FacePartsCustomizer(pawn, Dialog_D2FacePartsCustomizer.PreviewRenderMode.LiveSquareSandbox));
    }

    private static void RefreshEditedPawnFaceIfNeeded(Pawn pawn, CompFaceParts comp)
    {
        if (pawn == null || comp == null)
            return;

        AppearanceSignatureBox signatureBox = appearanceSignatureByPawn.GetValue(pawn, static _ => new AppearanceSignatureBox());
        int signature = ComputeFaceAppearanceSignature(pawn);
        if (signatureBox.Initialized && signatureBox.Signature == signature)
            return;

        signatureBox.Initialized = true;
        signatureBox.Signature = signature;

        comp.RefreshFaceHard(true);
        RimWorld.PortraitsCache.SetDirty(pawn);
    }

    private static int ComputeFaceAppearanceSignature(Pawn pawn)
    {
        if (pawn == null)
            return 0;

        unchecked
        {
            int signature = 17;
            AddStringHash(ref signature, pawn.def?.defName);
            AddStringHash(ref signature, pawn.kindDef?.defName);
            AddStringHash(ref signature, pawn.story?.headType?.defName);
            AddStringHash(ref signature, pawn.story?.bodyType?.defName);
            AddStringHash(ref signature, pawn.ageTracker?.CurLifeStage?.defName);
            AddColorHash(ref signature, pawn.story?.HairColor ?? Color.white);
            AddColorHash(ref signature, pawn.story?.SkinColor ?? Color.white);
            AddStringHash(ref signature, pawn.gender.ToString());

            if (pawn.genes?.GenesListForReading != null)
            {
                var genes = pawn.genes.GenesListForReading;
                signature = (signature * 31) + genes.Count;
                for (int i = 0; i < genes.Count; i++)
                {
                    Gene gene = genes[i];
                    if (gene == null)
                        continue;

                    AddStringHash(ref signature, gene.def?.defName);
                    signature = (signature * 31) + (gene.Active ? 1 : 0);
                    signature = (signature * 31) + (gene.Overridden ? 1 : 0);
                }
            }

            if (pawn.health?.hediffSet?.hediffs != null)
            {
                var hediffs = pawn.health.hediffSet.hediffs;
                signature = (signature * 31) + hediffs.Count;
                for (int i = 0; i < hediffs.Count; i++)
                {
                    Hediff hediff = hediffs[i];
                    if (hediff == null)
                        continue;

                    AddStringHash(ref signature, hediff.def?.defName);
                    AddStringHash(ref signature, hediff.Part?.def?.defName);
                    AddStringHash(ref signature, hediff.Part?.Label);
                    AddStringHash(ref signature, hediff.LabelBase);
                }
            }

            return signature;
        }
    }

    private static void AddStringHash(ref int signature, string value)
    {
        signature = (signature * 31) + StringComparer.OrdinalIgnoreCase.GetHashCode(value ?? string.Empty);
    }

    private static void AddColorHash(ref int signature, Color color)
    {
        signature = (signature * 31) + color.GetHashCode();
    }

    private static Rect BuildFacePartsButtonRect(Rect inRect)
    {
        // In a Harmony postfix, the Rect parameter reflects the dialog method's
        // final mutated value after its layout helpers have already carved out
        // the top and bottom sections. At this point, inRect is the remaining
        // middle gap. Center the button inside that live gap instead of
        // reconstructing the original layout with hardcoded offsets.
        const float buttonHorizontalPadding = 3f;
        const float buttonVerticalPadding = 3f;
        const float preferredButtonHeight = 28f;
        const float minimumButtonHeight = 20f;

        float availableHeight = Mathf.Max(0f, inRect.height);
        float buttonWidth = Mathf.Max(0f, inRect.width - (buttonHorizontalPadding * 2f));
        if (buttonWidth <= 0f || availableHeight <= 0f)
            return Rect.zero;

        float usableHeight = Mathf.Max(0f, availableHeight - (buttonVerticalPadding * 2f));
        float buttonHeight = Mathf.Min(preferredButtonHeight, usableHeight);
        if (buttonHeight < minimumButtonHeight)
            return Rect.zero;

        float buttonY = inRect.y + ((availableHeight - buttonHeight) * 0.5f);
        return new Rect(inRect.x + buttonHorizontalPadding, buttonY, buttonWidth, buttonHeight);
    }
}
