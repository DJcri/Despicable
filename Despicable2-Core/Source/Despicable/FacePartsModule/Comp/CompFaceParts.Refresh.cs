using System;
using System.Collections.Generic;
using System.Linq;
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
    private static readonly List<FacePartStyleDef> CachedMaleBrowStyles = new();
    private static readonly List<FacePartStyleDef> CachedFemaleBrowStyles = new();
    private static readonly List<FacePartStyleDef> CachedMaleMouthStyles = new();
    private static readonly List<FacePartStyleDef> CachedFemaleMouthStyles = new();
    private static readonly List<FacePartStyleDef> CachedMaleEyeDetailStyles = new();
    private static readonly List<FacePartStyleDef> CachedFemaleEyeDetailStyles = new();
    // Guardrail-Allow-Static: Cached empty eye-detail fallback is load-scoped shared definition data, not pawn-specific visual state.
    private static FacePartStyleDef CachedEmptyEyeDetailStyle;
    private static readonly HashSet<string> RetiredEyeDetailStyleDefNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "EyeDetail_CheekBlush",
        "EyeDetail_DarkCircles",
        "EyeDetail_Tears",
        "EyeDetail_Tired"
    };

    public static bool IsRetiredEyeDetailStyle(FacePartStyleDef style)
    {
        return style != null
            && style.renderNodeTag?.defName == "FacePart_EyeDetail"
            && !style.defName.NullOrEmpty()
            && RetiredEyeDetailStyleDefNames.Contains(style.defName);
    }

    private void SanitizeRetiredEyeDetailStyle()
    {
        if (!IsRetiredEyeDetailStyle(eyeDetailStyleDef))
            return;

        EnsureStylePoolsInitialized();
        eyeDetailStyleDef = CachedEmptyEyeDetailStyle;
    }

    private static void EnsureStylePoolsInitialized()
    {
        if (stylePoolsInitialized)
            return;

        CachedMaleEyeStyles.Clear();
        CachedFemaleEyeStyles.Clear();
        CachedMaleBrowStyles.Clear();
        CachedFemaleBrowStyles.Clear();
        CachedMaleMouthStyles.Clear();
        CachedFemaleMouthStyles.Clear();
        CachedMaleEyeDetailStyles.Clear();
        CachedFemaleEyeDetailStyles.Clear();
        CachedEmptyEyeDetailStyle = null;

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

            if (tagName == "FacePart_Brow")
            {
                if (allowMale)
                    CachedMaleBrowStyles.Add(style);
                if (allowFemale)
                    CachedFemaleBrowStyles.Add(style);
                continue;
            }

            if (tagName == "FacePart_Mouth")
            {
                if (allowMale)
                    CachedMaleMouthStyles.Add(style);
                if (allowFemale)
                    CachedFemaleMouthStyles.Add(style);
                continue;
            }

            if (tagName == "FacePart_EyeDetail")
            {
                if (string.Equals(style.texPath, EMPTY_DETAIL_TEX_PATH, StringComparison.OrdinalIgnoreCase))
                    CachedEmptyEyeDetailStyle ??= style;

                if (IsRetiredEyeDetailStyle(style))
                    continue;

                if (allowMale)
                    CachedMaleEyeDetailStyles.Add(style);
                if (allowFemale)
                    CachedFemaleEyeDetailStyles.Add(style);
                continue;
            }

        }

        stylePoolsInitialized = true;
    }

    public static bool IsStyleEligibleForPawn(Pawn pawn, FacePartStyleDef style)
    {
        if (style == null)
            return false;

        if (style.requiredGender != null)
        {
            Gender? pawnGender = pawn?.gender;
            if (pawnGender == null || style.requiredGender.Value != (byte)pawnGender.Value)
                return false;
        }

        List<string> requiredGenes = style.requiredGenes;
        if (requiredGenes == null || requiredGenes.Count == 0)
            return true;

        List<Gene> pawnGenes = pawn?.genes?.GenesListForReading;
        if (pawnGenes == null || pawnGenes.Count == 0)
            return false;

        for (int i = 0; i < requiredGenes.Count; i++)
        {
            string requiredGeneDefName = requiredGenes[i];
            if (requiredGeneDefName.NullOrEmpty())
                continue;

            bool found = false;
            for (int geneIndex = 0; geneIndex < pawnGenes.Count; geneIndex++)
            {
                string pawnGeneDefName = pawnGenes[geneIndex]?.def?.defName;
                if (!pawnGeneDefName.NullOrEmpty() && pawnGeneDefName.Equals(requiredGeneDefName, StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
                return false;
        }

        return true;
    }

    private List<FacePartStyleDef> GetBaseStylePool(string renderNodeTagDefName)
    {
        EnsureStylePoolsInitialized();
        bool female = pawn != null && pawn.gender == Gender.Female;

        return renderNodeTagDefName switch
        {
            "FacePart_Eye" => female ? CachedFemaleEyeStyles : CachedMaleEyeStyles,
            "FacePart_Brow" => female ? CachedFemaleBrowStyles : CachedMaleBrowStyles,
            "FacePart_Mouth" => female ? CachedFemaleMouthStyles : CachedMaleMouthStyles,
            "FacePart_EyeDetail" => female ? CachedFemaleEyeDetailStyles : CachedMaleEyeDetailStyles,
            _ => null
        };
    }

    private FacePartStyleDef GetRandomEligibleStyle(string renderNodeTagDefName)
    {
        return WeightedRandomEligibleStyle(GetBaseStylePool(renderNodeTagDefName));
    }

    private bool SanitizeStyleEligibility(ref FacePartStyleDef selectedStyle, string renderNodeTagDefName, FacePartStyleDef fallbackStyle = null)
    {
        if (selectedStyle == null || IsStyleEligibleForPawn(pawn, selectedStyle))
            return false;

        FacePartStyleDef replacementStyle = GetRandomEligibleStyle(renderNodeTagDefName) ?? fallbackStyle;
        if (selectedStyle == replacementStyle)
            return false;

        selectedStyle = replacementStyle;
        return true;
    }

    private bool SanitizeStyleRequirementSelections()
    {
        EnsureStylePoolsInitialized();

        bool changed = false;
        changed |= SanitizeStyleEligibility(ref eyeStyleDef, "FacePart_Eye");
        changed |= SanitizeStyleEligibility(ref browStyleDef, "FacePart_Brow");
        changed |= SanitizeStyleEligibility(ref mouthStyleDef, "FacePart_Mouth");
        changed |= SanitizeStyleEligibility(ref eyeDetailStyleDef, "FacePart_EyeDetail", CachedEmptyEyeDetailStyle);

        if (!changed)
            return false;

        InvalidateFaceStructure();
        shouldUpdate = true;
        return true;
    }

    private FacePartStyleDef WeightedRandomEligibleStyle(List<FacePartStyleDef> styles)
    {
        if (styles == null || styles.Count == 0)
            return null;

        int totalWeight = 0;
        for (int i = 0; i < styles.Count; i++)
        {
            FacePartStyleDef style = styles[i];
            if (style == null || style.weight <= 0 || !IsStyleEligibleForPawn(pawn, style))
                continue;

            totalWeight += style.weight;
        }

        if (totalWeight <= 0)
            return null;

        int roll = Rand.Range(0, totalWeight);
        for (int i = 0; i < styles.Count; i++)
        {
            FacePartStyleDef style = styles[i];
            if (style == null || style.weight <= 0 || !IsStyleEligibleForPawn(pawn, style))
                continue;

            roll -= style.weight;
            if (roll < 0)
                return style;
        }

        for (int i = styles.Count - 1; i >= 0; i--)
        {
            FacePartStyleDef style = styles[i];
            if (style != null && style.weight > 0 && IsStyleEligibleForPawn(pawn, style))
                return style;
        }

        return null;
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
            ClearRuntimeFacialAnimState();

            if (needsRefresh)
                RequestFaceGraphicsDirty(pawn?.Drawer?.renderer);

            shouldUpdate = false;
            return;
        }

        RefreshEnabledFromSettings();

        PawnRenderer renderer = pawn.Drawer?.renderer;
        if (!enabled || pawn.RaceProps?.Humanlike != true)
        {
            if (hadRenderableState)
            {
                if (markPortraitDirty)
                    RequestFaceGraphicsAndPortraitDirty(renderer);
                else
                    RequestFaceGraphicsDirty(renderer);
            }

            DisableFacePartsInstance();
            return;
        }
        if (renderer == null)
        {
            shouldUpdate = false;
            return;
        }

        if (SanitizeStyleRequirementSelections())
        {
            RefreshFaceHard(markPortraitDirty);
            return;
        }

        if (faceStructureDirty || !AreStyleSlotsAssigned())
        {
            RefreshFaceHard(markPortraitDirty);
            return;
        }

        if (!cachedVisualStateValid || !faceWarmInitialized)
        {
            WarmPortraitFast(markPortraitDirty);
            return;
        }

        if (markPortraitDirty)
            RequestFaceGraphicsAndPortraitDirty(renderer);
        else
            RequestFaceGraphicsDirty(renderer);

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
            ClearRuntimeFacialAnimState();

            if (needsRefresh)
                RequestFaceGraphicsDirty(pawn?.Drawer?.renderer);

            shouldUpdate = false;
            return;
        }

        RefreshEnabledFromSettings();
        InitializeFacePartState();

        if (!enabled || !pawn.RaceProps.Humanlike)
        {
            if (hadRenderableState)
            {
                if (markPortraitDirty)
                    RequestFaceGraphicsAndPortraitDirty(pawn.Drawer?.renderer);
                else
                    RequestFaceGraphicsDirty(pawn.Drawer?.renderer);
            }

            DisableFacePartsInstance();
            return;
        }

        RefreshCachedVisualState(false);
        SanitizeRetiredEyeDetailStyle();

        if (SanitizeStyleRequirementSelections())
        {
            RefreshFaceHard(markPortraitDirty);
            return;
        }

        if (!AreStyleSlotsAssigned())
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

        if (markPortraitDirty)
            RequestFaceGraphicsAndPortraitDirty(renderer);
        else
            RequestFaceGraphicsDirty(renderer);

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
            ClearRuntimeFacialAnimState();

            if (needsRefresh)
                RequestFaceGraphicsDirty(pawn?.Drawer?.renderer);

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

        if (markPortraitDirty)
            RequestFaceGraphicsAndPortraitDirty(renderer);
        else
            RequestFaceGraphicsDirty(renderer);

        shouldUpdate = false;
    }

    public void WarmPortraitFast(bool markPortraitDirty = false)
    {
        if (!CanWarmPortraitFastPath())
            return;

        bool visualStateChanged = RefreshCachedVisualState(false);

        bool needsStructuralRebuild = faceStructureDirty;
        if (SanitizeStyleRequirementSelections())
            needsStructuralRebuild = true;

        if (!AreStyleSlotsAssigned())
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
            if (markPortraitDirty)
                RequestFaceGraphicsAndPortraitDirty(renderer);
            else
                RequestFaceGraphicsDirty(renderer);
        }
        else if (visualStateChanged)
        {
            if (markPortraitDirty)
                RequestFaceGraphicsAndPortraitDirty(renderer);
            else
                RequestFaceGraphicsDirty(renderer);
        }

        if (!faceWarmInitialized || needsStructuralRebuild)
            renderer.EnsureGraphicsInitialized();

        faceWarmInitialized = true;
        faceStructureDirty = false;
        shouldUpdate = false;
        ReconcileGlobalPortraitWarmupNeededCount();
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
            ClearRuntimeFacialAnimState();

            if (needsRefresh)
            {
                pawn?.Drawer?.renderer?.renderTree?.SetDirty();
                RequestFaceGraphicsDirty(pawn?.Drawer?.renderer);
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
                if (markPortraitDirty)
                    RequestFaceGraphicsAndPortraitDirty(pawn.Drawer?.renderer);
                else
                    RequestFaceGraphicsDirty(pawn.Drawer?.renderer);
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
        SanitizeRetiredEyeDetailStyle();
        SanitizeStyleRequirementSelections();

        if (!AreStyleSlotsAssigned())
            AssignStylesRandomByWeight();

        PawnRenderer renderer = pawn.Drawer?.renderer;
        if (renderer == null)
        {
            shouldUpdate = false;
            return;
        }

        if (markPortraitDirty)
            RequestFaceGraphicsAndPortraitDirty(renderer);
        else
            RequestFaceGraphicsDirty(renderer);

        if (!faceWarmInitialized)
            renderer.EnsureGraphicsInitialized();

        faceWarmInitialized = true;
        faceStructureDirty = false;
        shouldUpdate = false;
        ReconcileGlobalPortraitWarmupNeededCount();
    }

    public void RefreshFaceNow(bool markPortraitDirty = true)
    {
        RefreshFaceHard(markPortraitDirty);
    }

    public void AssignStylesRandomByWeight()
    {
        FacePartStyleDef selectedEye = GetRandomEligibleStyle("FacePart_Eye");
        FacePartStyleDef selectedBrow = GetRandomEligibleStyle("FacePart_Brow");
        FacePartStyleDef selectedMouth = GetRandomEligibleStyle("FacePart_Mouth");
        FacePartStyleDef selectedEyeDetail = GetRandomEligibleStyle("FacePart_EyeDetail");

        if (selectedEye != null)
            eyeStyleDef = selectedEye;

        if (selectedBrow != null)
            browStyleDef = selectedBrow;

        if (selectedMouth != null)
            mouthStyleDef = selectedMouth;

        if (selectedEyeDetail != null)
        {
            eyeDetailStyleDef = selectedEyeDetail;
            eyeDetailSideMode = selectedEyeDetail.allowSideSelection
                ? (Rand.Bool ? FacePartSideMode.RightOnly : FacePartSideMode.LeftOnly)
                : selectedEyeDetail.ResolveEffectiveSideMode();
        }

        faceDetailStyleDef = null;

        InvalidateFaceStructure();
        shouldUpdate = true;
    }
}
