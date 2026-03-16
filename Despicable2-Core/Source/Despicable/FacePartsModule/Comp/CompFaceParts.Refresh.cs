using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

// Guardrail-Reason: CompFaceParts refresh helpers stay grouped because style caching, refresh passes, and render invalidation share one component lifecycle.
namespace Despicable;
public partial class CompFaceParts
{
    // Guardrail-Allow-Static: One-time face-style pool initialization gate owned by CompFaceParts refresh flow; shared across pawns for the current load.
    private static bool stylePoolsInitialized;
    private static readonly List<FacePartStyleDef> CachedMaleEyeStyles = new();
    private static readonly List<FacePartStyleDef> CachedFemaleEyeStyles = new();
    private static readonly List<FacePartStyleDef> CachedMaleMouthStyles = new();
    private static readonly List<FacePartStyleDef> CachedFemaleMouthStyles = new();

    private static void EnsureStylePoolsInitialized()
    {
        if (stylePoolsInitialized)
            return;

        CachedMaleEyeStyles.Clear();
        CachedFemaleEyeStyles.Clear();
        CachedMaleMouthStyles.Clear();
        CachedFemaleMouthStyles.Clear();

        List<FacePartStyleDef> allStyles = DefDatabase<FacePartStyleDef>.AllDefsListForReading;
        for (int i = 0; i < allStyles.Count; i++)
        {
            FacePartStyleDef style = allStyles[i];
            string tagName = style?.renderNodeTag?.defName;
            if (tagName == null)
                continue;

            bool allowMale = style.requiredGender == null || style.requiredGender == (byte)Gender.Male;
            bool allowFemale = style.requiredGender == null || style.requiredGender == (byte)Gender.Female;

            if (tagName == "FacePart_Eye")
            {
                if (allowMale)
                    CachedMaleEyeStyles.Add(style);
                if (allowFemale)
                    CachedFemaleEyeStyles.Add(style);
                continue;
            }

            if (tagName == "FacePart_Mouth")
            {
                if (allowMale)
                    CachedMaleMouthStyles.Add(style);
                if (allowFemale)
                    CachedFemaleMouthStyles.Add(style);
            }
        }

        stylePoolsInitialized = true;
    }

    private List<FacePartStyleDef> GetEligibleStylePool(bool forEyes)
    {
        EnsureStylePoolsInitialized();
        bool female = pawn != null && pawn.gender == Gender.Female;
        if (forEyes)
            return female ? CachedFemaleEyeStyles : CachedMaleEyeStyles;

        return female ? CachedFemaleMouthStyles : CachedMaleMouthStyles;
    }

    private static FacePartStyleDef WeightedRandomStyle(List<FacePartStyleDef> styles)
    {
        if (styles == null || styles.Count == 0)
            return null;

        int totalWeight = 0;
        for (int i = 0; i < styles.Count; i++)
        {
            FacePartStyleDef style = styles[i];
            if (style == null || style.weight <= 0)
                continue;

            totalWeight += style.weight;
        }

        if (totalWeight <= 0)
            return null;

        int roll = Rand.Range(0, totalWeight);
        for (int i = 0; i < styles.Count; i++)
        {
            FacePartStyleDef style = styles[i];
            if (style == null || style.weight <= 0)
                continue;

            roll -= style.weight;
            if (roll < 0)
                return style;
        }

        return styles[styles.Count - 1];
    }

    public void RefreshFaceSoftFast(bool markPortraitDirty = false)
    {
        if (pawn == null)
            return;

        bool hadRenderableState = HasActiveFaceRenderState();

        if (ModMain.IsNlFacialInstalled)
        {
            bool needsRefresh = enabled || facialAnim != null || animExpression != null;
            enabled = false;
            facialAnim = null;
            animExpression = null;

            if (needsRefresh)
                pawn?.Drawer?.renderer?.SetAllGraphicsDirty();

            shouldUpdate = false;
            return;
        }

        RefreshEnabledFromSettings();

        PawnRenderer renderer = pawn.Drawer?.renderer;
        if (!enabled || pawn.RaceProps?.Humanlike != true)
        {
            if (hadRenderableState)
            {
                renderer?.renderTree?.SetDirty();
                renderer?.SetAllGraphicsDirty();
                if (markPortraitDirty)
                    PortraitsCache.SetDirty(pawn);
            }

            DisableFacePartsInstance();
            return;
        }
        if (renderer == null)
        {
            shouldUpdate = false;
            return;
        }

        if (faceStructureDirty || mouthStyleDef == null || eyeStyleDef == null)
        {
            RefreshFaceHard(markPortraitDirty);
            return;
        }

        if (!cachedVisualStateValid || !faceWarmInitialized)
        {
            WarmPortraitFast(markPortraitDirty);
            return;
        }

        renderer.SetAllGraphicsDirty();

        if (markPortraitDirty)
            PortraitsCache.SetDirty(pawn);

        shouldUpdate = false;
    }

    public void RefreshFaceSoft(bool markPortraitDirty = false)
    {
        if (pawn == null)
            return;

        bool hadRenderableState = HasActiveFaceRenderState();

        if (ModMain.IsNlFacialInstalled)
        {
            bool needsRefresh = enabled || facialAnim != null || animExpression != null;
            enabled = false;
            facialAnim = null;
            animExpression = null;

            if (needsRefresh)
                pawn?.Drawer?.renderer?.SetAllGraphicsDirty();

            shouldUpdate = false;
            return;
        }

        RefreshEnabledFromSettings();
        InitializeFacePartState();

        if (!enabled || !pawn.RaceProps.Humanlike)
        {
            if (hadRenderableState)
            {
                pawn.Drawer?.renderer?.renderTree?.SetDirty();
                pawn.Drawer?.renderer?.SetAllGraphicsDirty();
                if (markPortraitDirty)
                    PortraitsCache.SetDirty(pawn);
            }

            DisableFacePartsInstance();
            return;
        }

        RefreshCachedVisualState(false);

        if (mouthStyleDef == null || eyeStyleDef == null)
        {
            AssignStylesRandomByWeight();
            RefreshFaceHard(markPortraitDirty);
            return;
        }

        PawnRenderer renderer = pawn.Drawer?.renderer;
        if (renderer == null)
        {
            shouldUpdate = false;
            return;
        }

        renderer.SetAllGraphicsDirty();

        if (markPortraitDirty)
            PortraitsCache.SetDirty(pawn);

        shouldUpdate = false;
    }

    public void RefreshFaceExpressionOnly(bool markPortraitDirty = false)
    {
        if (pawn == null)
            return;

        if (ModMain.IsNlFacialInstalled)
        {
            bool needsRefresh = enabled || facialAnim != null || animExpression != null;
            enabled = false;
            facialAnim = null;
            animExpression = null;

            if (needsRefresh)
                pawn?.Drawer?.renderer?.SetAllGraphicsDirty();

            shouldUpdate = false;
            return;
        }

        if (!enabled || pawn.RaceProps?.Humanlike != true)
        {
            shouldUpdate = false;
            return;
        }

        PawnRenderer renderer = pawn.Drawer?.renderer;
        if (renderer == null)
        {
            shouldUpdate = false;
            return;
        }

        renderer.SetAllGraphicsDirty();

        if (markPortraitDirty)
            PortraitsCache.SetDirty(pawn);

        shouldUpdate = false;
    }

    public void WarmPortraitFast(bool markPortraitDirty = false)
    {
        if (!CanWarmPortraitFastPath())
            return;

        bool visualStateChanged = RefreshCachedVisualState(false);

        bool needsStructuralRebuild = faceStructureDirty;
        if (mouthStyleDef == null || eyeStyleDef == null)
        {
            AssignStylesRandomByWeight();
            needsStructuralRebuild = true;
        }

        PawnRenderer renderer = pawn.Drawer?.renderer;
        if (renderer == null)
        {
            shouldUpdate = false;
            return;
        }

        if (needsStructuralRebuild)
        {
            renderer.renderTree?.SetDirty();
            renderer.SetAllGraphicsDirty();
        }
        else if (visualStateChanged)
        {
            renderer.SetAllGraphicsDirty();
        }

        if (!faceWarmInitialized || needsStructuralRebuild)
            renderer.EnsureGraphicsInitialized();

        if (markPortraitDirty)
            PortraitsCache.SetDirty(pawn);

        faceWarmInitialized = true;
        faceStructureDirty = false;
        shouldUpdate = false;
    }

    public void RefreshFaceHard(bool markPortraitDirty = true)
    {
        if (pawn == null)
            return;

        bool hadRenderableState = HasActiveFaceRenderState();

        if (ModMain.IsNlFacialInstalled)
        {
            bool needsRefresh = enabled || facialAnim != null || animExpression != null;
            enabled = false;
            facialAnim = null;
            animExpression = null;

            if (needsRefresh)
            {
                pawn?.Drawer?.renderer?.renderTree?.SetDirty();
                pawn?.Drawer?.renderer?.SetAllGraphicsDirty();
            }

            faceWarmInitialized = false;
            faceStructureDirty = true;
            shouldUpdate = false;
            return;
        }

        RefreshEnabledFromSettings();
        InitializeFacePartState();

        if (!enabled || pawn.RaceProps == null || !pawn.RaceProps.Humanlike)
        {
            if (hadRenderableState)
            {
                pawn.Drawer?.renderer?.renderTree?.SetDirty();
                pawn.Drawer?.renderer?.SetAllGraphicsDirty();
                if (markPortraitDirty)
                    PortraitsCache.SetDirty(pawn);
            }

            DisableFacePartsInstance();
            return;
        }

        if (pawn.health?.hediffSet == null)
        {
            shouldUpdate = false;
            return;
        }

        RefreshCachedVisualState(false);

        if (mouthStyleDef == null || eyeStyleDef == null)
            AssignStylesRandomByWeight();

        PawnRenderer renderer = pawn.Drawer?.renderer;
        if (renderer == null)
        {
            shouldUpdate = false;
            return;
        }

        renderer.renderTree?.SetDirty();
        renderer.SetAllGraphicsDirty();

        if (!faceWarmInitialized)
            renderer.EnsureGraphicsInitialized();

        if (markPortraitDirty)
            PortraitsCache.SetDirty(pawn);

        faceWarmInitialized = true;
        faceStructureDirty = false;
        shouldUpdate = false;
    }

    public void RefreshFaceNow(bool markPortraitDirty = true)
    {
        RefreshFaceHard(markPortraitDirty);
    }

    public void AssignStylesRandomByWeight()
    {
        FacePartStyleDef selectedEye = WeightedRandomStyle(GetEligibleStylePool(true));
        FacePartStyleDef selectedMouth = WeightedRandomStyle(GetEligibleStylePool(false));

        if (selectedEye != null)
            eyeStyleDef = selectedEye;

        if (selectedMouth != null)
            mouthStyleDef = selectedMouth;

        InvalidateFaceStructure();
        shouldUpdate = true;
    }
}
