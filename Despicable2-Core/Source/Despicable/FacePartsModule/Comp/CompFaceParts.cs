using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Reflection;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

// Guardrail-Reason: CompFaceParts remains the single owner of face-part runtime state and render-facing orchestration, so pre-release splits would widen risk.
namespace Despicable;
/// <summary>
/// Handles logic of RENDERING FACES and FACIAL ANIMATION
/// Very badly written, I know, but the whole system needs a rewrite.
/// A rewrite would be insanely complex and time consuming,
/// minor patches/changes should suffice.
/// </summary>

public partial class CompFaceParts : ThingComp
{
    // Default constants
    public static readonly string DEFAULT_GENDER_PATH = "Male/";
    public const string EMPTY_DETAIL_TEX_PATH = "FaceParts/Details/detail_empty";
    public const string DEFAULT_EYE_TEX_PATH = "FaceParts/Eyes/eye_normal";
    public const string DEFAULT_BROW_TEX_PATH = "FaceParts/Brows/Thick/brow_thick_flat";
    public const string DEFAULT_MOUTH_TEX_PATH = "FaceParts/Mouths/mouth_serious";
    public const string DARK_CIRCLES_DETAIL_TEX_PATH = "FaceParts/Details/Eye/eyedetail_darkcircles";
    public const string TEARS_DETAIL_TEX_PATH = "FaceParts/Details/Eye/eyedetail_tears";

    private static readonly Dictionary<string, bool> CachedTextureExistsByPath = new(StringComparer.OrdinalIgnoreCase);

    private static bool IsFacePartsEnabledInSettings => CommonUtil.GetSettings()?.facialPartsExtensionEnabled ?? false;

    internal static int GlobalWarmupNeededCount;

    // State variables
    public bool enabled;
    public bool shouldUpdate = false;
    // Legacy save shim: retain the saved field name for compatibility, but do not advance a dead per-tick counter anymore.
    public int ticks = 0;
    public int blinkTick = FacePartsUtil.BlinkInterval + Rand.Range(-FacePartsUtil.BlinkTickVariance, FacePartsUtil.BlinkTickVariance);
    public int nextBlinkGameTick = -1;

    // Style paths
    public string genderPath = DEFAULT_GENDER_PATH;
    public FacePartStyleDef eyeStyleDef = null;
    public FacePartStyleDef browStyleDef = null;
    public FacePartStyleDef mouthStyleDef = null;
    public FacePartStyleDef eyeDetailStyleDef = null;
    public FacePartSideMode eyeDetailSideMode = FacePartSideMode.LeftOnly;
    // Legacy save shim: face details are retired in Core, but the field stays loadable so older saves deserialize cleanly.
    public FacePartStyleDef faceDetailStyleDef = null;

    // Animation
    public ExpressionDef baseExpression;
    public ExpressionDef animExpression;
    public FacialAnimDef facialAnim;
    public int curKeyframe = 0;
    public int animTicks = 0;
    public int facialAnimStartGameTick = -1;
    public bool faceWarmInitialized = false;
    public bool faceStructureDirty = true;
    public string baseDetailTexPath = EMPTY_DETAIL_TEX_PATH;

    private int lastVisualStateSignature = int.MinValue;
    private bool cachedVisualStateValid = false;
    private const int StableEyeVisualPollIntervalTicks = 15;
    private const FacePartsEventMask StructuralRuntimeFaceEvents = FacePartsEventMask.Health | FacePartsEventMask.LifeStage | FacePartsEventMask.Structure;
    private const int RuntimeCrowdBlinkIntervalMultiplier = 4;
    private const int VisibleRuntimeCompTickIntervalTicks = 4;
    private const int IdleRuntimeCompTickIntervalTicks = 250;
    private int lastEyeVisualStateCheckTick = int.MinValue;
    private int nextEyeVisualStateRecheckTick = int.MinValue;
    private int cachedEyeVisualGeneCount = -1;
    private int cachedEyeVisualHediffCount = -1;
    private int cachedEyeVisualInputSignature = int.MinValue;
    private bool cachedEyeVisualStateEligibleForSlowPolling;
    private bool cachedHasBlockingEyeGene;
    private bool cachedShouldSuppressForeignEyeGeneGraphics;
    private bool cachedHasPotentialForeignEyeHealthVisual;
    private Color cachedResolvedEyeTint = Color.black;
    [Flags]
    private enum ForeignEyeBlockMask
    {
        None = 0,
        Left = 1 << 0,
        Right = 1 << 1,
        Both = Left | Right
    }

    private int lastForeignEyeVisualCheckTick = int.MinValue;
    private int cachedForeignEyeVisualInputSignature = int.MinValue;
    private int cachedForeignEyeVisualRootSignature = int.MinValue;
    private ForeignEyeBlockMask cachedForeignEyeBlockMask = ForeignEyeBlockMask.None;
    private bool foreignEyeBlockMaskDirty = true;
    private readonly HashSet<PawnRenderNode> foreignEyeVisitedPool = new();
    private int lastSpecialFaceStateCheckTick = int.MinValue;
    private bool cachedHasVoidTouched;
    private bool cachedHasBabyCryMentalState;
    private bool cachedHasBlockingNoseGene;
    private bool cachedHasBlockingNoseGeneValid;
    private int lastPortraitWarmupAttemptTick = int.MinValue;
    private bool countedInGlobalPortraitWarmupNeeded;
    private int nextScheduledCompTick = int.MinValue;
    private int localEditorHeartbeatUntilFrame = int.MinValue;
    private int registeredRuntimeGeneration = int.MinValue;
    private bool localEditorActive;
    private bool localExtendedAnimatorActive;
    private bool hasPendingAutoEyePatchFaceRefresh;
    private FacePartsEventMask pendingRuntimeFaceEvents = FacePartsEventMask.None;
    private const int RuntimeRenderGraceTicks = 15;
    private bool lastRuntimeFaceDynamicsAllowed = true;
    private bool nlFacialDisableLatched = false;
    private int lastFaceGraphicsDirtyRequestTick = int.MinValue;
    private int cachedRenderExpressionTick = int.MinValue;
    private bool cachedRenderExpressionWorkshopActive;
    private bool cachedRenderExpressionPortraitContext;
    private bool cachedRenderExpressionUseDynamic;
    private ExpressionDef cachedRenderExpressionAnimSource;
    private ExpressionDef cachedRenderExpressionBaseSource;
    private ExpressionDef cachedRenderExpressionValue;
    private FaceExpressionState cachedExpressionState;
    private bool cachedExpressionStateInitialized = false;
    private HeadTypeDef cachedEligibleHeadType;
    private bool cachedEligibleHeadResult;
    private bool cachedEligibleHeadValid = false;

    private sealed class FacialAnimSampleCache
    {
        public int[] Ticks;
        public ExpressionDef[] Frames;
    }

    private static readonly Dictionary<FacialAnimDef, FacialAnimSampleCache> CachedFacialAnimSamples = new();

    private struct FaceExpressionState
    {
        public bool HasHead;
        public bool Asleep;
        public bool Berserk;
        public bool MentalBreak;
        public bool Infant;
        public bool Drunk;
        public bool Tired;
        public bool Drafted;

        public int GetSignature()
        {
            int signature = 0;
            if (HasHead) signature |= 1 << 0;
            if (Asleep) signature |= 1 << 1;
            if (Berserk) signature |= 1 << 2;
            if (MentalBreak) signature |= 1 << 3;
            if (Infant) signature |= 1 << 4;
            if (Drunk) signature |= 1 << 5;
            if (Tired) signature |= 1 << 6;
            if (Drafted) signature |= 1 << 7;
            return signature;
        }
    }

    private void DisableFacePartsInstance()
    {
        enabled = false;
        shouldUpdate = false;
        facialAnim = null;
        animExpression = null;
        baseExpression = null;
        baseDetailTexPath = EMPTY_DETAIL_TEX_PATH;
        cachedVisualStateValid = false;
        lastVisualStateSignature = int.MinValue;
        lastEyeVisualStateCheckTick = int.MinValue;
        nextEyeVisualStateRecheckTick = int.MinValue;
        cachedEyeVisualGeneCount = -1;
        cachedEyeVisualHediffCount = -1;
        cachedEyeVisualInputSignature = int.MinValue;
        cachedEyeVisualStateEligibleForSlowPolling = false;
        cachedHasBlockingEyeGene = false;
        cachedShouldSuppressForeignEyeGeneGraphics = false;
        cachedHasPotentialForeignEyeHealthVisual = false;
        cachedResolvedEyeTint = Color.black;
        lastForeignEyeVisualCheckTick = int.MinValue;
        cachedForeignEyeVisualInputSignature = int.MinValue;
        cachedForeignEyeVisualRootSignature = int.MinValue;
        cachedForeignEyeBlockMask = ForeignEyeBlockMask.None;
        foreignEyeBlockMaskDirty = true;
        lastSpecialFaceStateCheckTick = int.MinValue;
        cachedHasVoidTouched = false;
        cachedHasBabyCryMentalState = false;
        cachedHasBlockingNoseGene = false;
        cachedHasBlockingNoseGeneValid = false;
        nextBlinkGameTick = -1;
        nextScheduledCompTick = int.MinValue;
        localEditorHeartbeatUntilFrame = int.MinValue;
        localEditorActive = false;
        localExtendedAnimatorActive = false;
        hasPendingAutoEyePatchFaceRefresh = false;
        pendingRuntimeFaceEvents = FacePartsEventMask.None;
        curKeyframe = 0;
        animTicks = 0;
        facialAnimStartGameTick = -1;
        lastFaceGraphicsDirtyRequestTick = int.MinValue;
        InvalidateRenderExpressionCache();
        cachedEligibleHeadType = null;
        cachedEligibleHeadResult = false;
        cachedEligibleHeadValid = false;
        cachedExpressionState = default(FaceExpressionState);
        cachedExpressionStateInitialized = false;
        faceWarmInitialized = false;
        faceStructureDirty = true;
        ClearGlobalPortraitWarmupNeededCountIfTracked();
    }

    private bool ShouldCountTowardGlobalPortraitWarmupNeeded()
    {
        return enabled
            && (!faceWarmInitialized
                || faceStructureDirty
                || !AreStyleSlotsAssigned()
                || !cachedVisualStateValid);
    }

    private void ReconcileGlobalPortraitWarmupNeededCount()
    {
        bool shouldCount = ShouldCountTowardGlobalPortraitWarmupNeeded();
        if (countedInGlobalPortraitWarmupNeeded == shouldCount)
            return;

        if (shouldCount)
        {
            GlobalWarmupNeededCount++;
            countedInGlobalPortraitWarmupNeeded = true;
            return;
        }

        ClearGlobalPortraitWarmupNeededCountIfTracked();
    }

    private void ClearGlobalPortraitWarmupNeededCountIfTracked()
    {
        if (!countedInGlobalPortraitWarmupNeeded)
            return;

        GlobalWarmupNeededCount = Math.Max(0, GlobalWarmupNeededCount - 1);
        countedInGlobalPortraitWarmupNeeded = false;
    }

    private bool HasActiveFaceRenderState()
    {
        return faceWarmInitialized
            || facialAnim != null
            || animExpression != null
            || baseExpression != null
            || eyeStyleDef != null
            || browStyleDef != null
            || mouthStyleDef != null
            || eyeDetailStyleDef != null;
    }

    public bool HasPendingPortraitWarmupFlags()
    {
        return !faceWarmInitialized
            || faceStructureDirty
            || !AreStyleSlotsAssigned()
            || !cachedVisualStateValid;
    }

    public bool IsRenderActiveNow()
    {
        if (ModMain.IsNlFacialInstalled || pawn == null)
            return false;

        if (!IsFacePartsEnabledInSettings)
            return false;

        if (pawn.RaceProps?.Humanlike != true)
            return false;

        HeadTypeDef headType = pawn.story?.headType;
        if (headType == null)
            return false;

        return !FacePartsUtil.IsHeadBlacklisted(headType);
    }

    private int CurrentGameTick => Find.TickManager?.TicksGame ?? 0;

    private Settings FaceSettings => CommonUtil.GetSettings();

    private bool ArePortraitFaceDynamicsEnabled()
    {
        return FaceSettings?.facialDynamicsInPortraits ?? false;
    }

    private bool IsRuntimeFaceDynamicsZoomGateEnabled()
    {
        return FaceSettings?.runtimeFacialDynamicsZoomGateEnabled ?? true;
    }

    private bool IsRuntimeHostileFaceDynamicsGateEnabled()
    {
        return FaceSettings?.runtimeFacialDynamicsGateHostilePawns ?? false;
    }

    private bool IsRuntimeVisitorFaceDynamicsGateEnabled()
    {
        return FaceSettings?.runtimeFacialDynamicsGateVisitorsAndTraders ?? false;
    }

    private float RuntimeFaceDynamicsMaxZoomRootSize()
    {
        float configured = FaceSettings?.runtimeFacialDynamicsMaxZoomRootSize ?? 10f;
        return Mathf.Max(1f, configured);
    }

    private static class RuntimeFaceDynamicsZoomGateCache
    {
        private static int cachedFrameCount = int.MinValue;
        private static Map cachedMap;
        private static CameraDriver cachedCameraDriver;
        private static bool cachedZoomGateEnabled;
        private static float cachedMaxZoomRootSize = float.NaN;
        private static bool cachedAllowed = true;

        public static bool IsAllowed(Map currentMap, CameraDriver currentCameraDriver, bool zoomGateEnabled, float maxZoomRootSize)
        {
            int currentFrameCount = Time.frameCount;
            if (cachedFrameCount == currentFrameCount
                && ReferenceEquals(cachedMap, currentMap)
                && ReferenceEquals(cachedCameraDriver, currentCameraDriver)
                && cachedZoomGateEnabled == zoomGateEnabled
                && Mathf.Approximately(cachedMaxZoomRootSize, maxZoomRootSize))
            {
                return cachedAllowed;
            }

            cachedFrameCount = currentFrameCount;
            cachedMap = currentMap;
            cachedCameraDriver = currentCameraDriver;
            cachedZoomGateEnabled = zoomGateEnabled;
            cachedMaxZoomRootSize = maxZoomRootSize;
            cachedAllowed = ComputeAllowed(currentCameraDriver, zoomGateEnabled, maxZoomRootSize);
            return cachedAllowed;
        }

        private static bool ComputeAllowed(CameraDriver currentCameraDriver, bool zoomGateEnabled, float maxZoomRootSize)
        {
            if (!zoomGateEnabled)
                return true;

            if (currentCameraDriver == null)
                return true;

            return currentCameraDriver.ZoomRootSize <= maxZoomRootSize;
        }

    }

    private bool IsSelectedPawn()
    {
        if (pawn == null)
            return false;

        try
        {
            Selector selector = Find.Selector;
            return selector?.IsSelected(pawn) == true;
        }
        catch (InvalidCastException)
        {
            return false;
        }
    }

    private bool HasActiveExtendedAnimation()
    {
        return localExtendedAnimatorActive;
    }

    private bool IsHostileToPlayer()
    {
        Faction ofPlayer = Faction.OfPlayer;
        return pawn != null && ofPlayer != null && pawn.HostileTo(ofPlayer);
    }

    private bool IsVisitorOrTraderLikePawn()
    {
        if (pawn == null)
            return false;

        Faction faction = pawn.Faction;
        if (faction == null || faction.IsPlayer)
            return false;

        if (IsHostileToPlayer())
            return false;

        if (PawnAffiliation.IsPrisonerLike(pawn) || PawnAffiliation.IsSlaveLike(pawn))
            return false;

        return PawnAffiliation.IsGuestLike(pawn) || pawn.trader != null;
    }

    private bool ShouldAlwaysAllowRuntimeFaceDynamics()
    {
        if (WorkshopRenderContext.Active)
            return true;

        if (IsSelectedPawn())
            return true;

        if (localEditorActive || HasLocalEditorHeartbeat(Time.frameCount) || localExtendedAnimatorActive)
            return true;

        return false;
    }

    private bool IsSuppressedByRuntimeFaceDynamicsAffiliationGate(bool alwaysAllowRuntimeDynamics)
    {
        if (alwaysAllowRuntimeDynamics)
            return false;

        if (IsRuntimeHostileFaceDynamicsGateEnabled() && IsHostileToPlayer())
            return true;

        if (IsRuntimeVisitorFaceDynamicsGateEnabled() && IsVisitorOrTraderLikePawn())
            return true;

        return false;
    }

    private bool ShouldRunRuntimeFaceDynamicsAtCurrentZoom()
    {
        if (WorkshopRenderContext.Active)
            return true;

        bool zoomGateEnabled = IsRuntimeFaceDynamicsZoomGateEnabled();
        if (!zoomGateEnabled)
            return true;

        if (pawn?.Spawned != true)
            return true;

        Map currentMap = Find.CurrentMap;
        if (currentMap == null || pawn.Map != currentMap)
            return true;

        float maxZoomRootSize = RuntimeFaceDynamicsMaxZoomRootSize();
        CameraDriver cameraDriver = Current.CameraDriver;
        return RuntimeFaceDynamicsZoomGateCache.IsAllowed(currentMap, cameraDriver, zoomGateEnabled, maxZoomRootSize);
    }

    private bool ShouldRunRuntimeFaceDynamics()
    {
        bool alwaysAllowRuntimeDynamics = ShouldAlwaysAllowRuntimeFaceDynamics();
        if (alwaysAllowRuntimeDynamics)
            return true;

        if (IsSuppressedByRuntimeFaceDynamicsAffiliationGate(alwaysAllowRuntimeDynamics))
            return false;

        return ShouldRunRuntimeFaceDynamicsAtCurrentZoom();
    }

    private bool ShouldUseDynamicFaceRenderForParms(PawnDrawParms parms)
    {
        if (WorkshopRenderContext.Active)
            return true;

        if (parms.Portrait)
            return ArePortraitFaceDynamicsEnabled();

        return ShouldRunRuntimeFaceDynamics();
    }

    private bool ShouldUseDynamicFaceRenderForCurrentContext()
    {
        if (WorkshopRenderContext.Active)
            return true;

        if (FacePartsPortraitRenderContext.NonWorkshopPortraitActive)
            return ArePortraitFaceDynamicsEnabled();

        return ShouldRunRuntimeFaceDynamics();
    }

    private void InvalidateRenderExpressionCache()
    {
        cachedRenderExpressionTick = int.MinValue;
        cachedRenderExpressionWorkshopActive = false;
        cachedRenderExpressionPortraitContext = false;
        cachedRenderExpressionUseDynamic = false;
        cachedRenderExpressionAnimSource = null;
        cachedRenderExpressionBaseSource = null;
        cachedRenderExpressionValue = null;
    }

    private ExpressionDef GetCachedRenderExpression(bool workshopActive, bool portraitContext, bool useDynamic)
    {
        int currentGameTick = CurrentGameTick;
        bool isSelected = IsSelectedPawn();
        if (cachedRenderExpressionTick == currentGameTick
            && cachedRenderExpressionWorkshopActive == workshopActive
            && cachedRenderExpressionPortraitContext == portraitContext
            && cachedRenderExpressionUseDynamic == useDynamic
            && ReferenceEquals(cachedRenderExpressionAnimSource, animExpression)
            && ReferenceEquals(cachedRenderExpressionBaseSource, baseExpression))
        {
            return cachedRenderExpressionValue;
        }

        ExpressionDef resolved = (useDynamic ? animExpression : null) ?? baseExpression;
        if (resolved == baseExpression)
        {
            ExpressionDef derivedCrowdBlinkExpression = GetDerivedCrowdBlinkExpression(currentGameTick, workshopActive, portraitContext, useDynamic, isSelected);
            if (derivedCrowdBlinkExpression != null)
                resolved = derivedCrowdBlinkExpression;
        }

        cachedRenderExpressionTick = currentGameTick;
        cachedRenderExpressionWorkshopActive = workshopActive;
        cachedRenderExpressionPortraitContext = portraitContext;
        cachedRenderExpressionUseDynamic = useDynamic;
        cachedRenderExpressionAnimSource = animExpression;
        cachedRenderExpressionBaseSource = baseExpression;
        cachedRenderExpressionValue = resolved;
        return resolved;
    }

    private bool ShouldBypassFaceGraphicsDirtyDebounce(bool bypassSameTickDebounce)
    {
        return bypassSameTickDebounce || WorkshopRenderContext.Active || FacePartsPortraitRenderContext.NonWorkshopPortraitActive;
    }

    private bool BeginFaceGraphicsDirtyRequest(bool bypassSameTickDebounce)
    {
        InvalidateRenderExpressionCache();

        int currentGameTick = CurrentGameTick;
        if (!ShouldBypassFaceGraphicsDirtyDebounce(bypassSameTickDebounce) && lastFaceGraphicsDirtyRequestTick == currentGameTick)
            return false;

        lastFaceGraphicsDirtyRequestTick = currentGameTick;
        return true;
    }

    private void RequestFaceGraphicsDirty(PawnRenderer renderer, bool bypassSameTickDebounce = false)
    {
        if (renderer == null)
            return;

        if (!BeginFaceGraphicsDirtyRequest(bypassSameTickDebounce))
            return;

        foreignEyeBlockMaskDirty = true;
        FaceRuntimeActivityManager.NotifyExplicitWake(pawn, CurrentGameTick, VisibleRuntimeCompTickIntervalTicks);
        WakeCompTickNow();
        renderer.renderTree?.SetDirty();
        if (pawn != null)
            GlobalTextureAtlasManager.TryMarkPawnFrameSetDirty(pawn);
    }

    private void RequestFaceGraphicsAndPortraitDirty(PawnRenderer renderer, bool bypassSameTickDebounce = false)
    {
        if (renderer == null)
            return;

        if (!BeginFaceGraphicsDirtyRequest(bypassSameTickDebounce))
            return;

        foreignEyeBlockMaskDirty = true;
        FaceRuntimeActivityManager.NotifyExplicitWake(pawn, CurrentGameTick, VisibleRuntimeCompTickIntervalTicks);
        WakeCompTickNow();
        renderer.renderTree?.SetDirty();
        if (pawn != null)
        {
            GlobalTextureAtlasManager.TryMarkPawnFrameSetDirty(pawn);
            PortraitsCache.SetDirty(pawn);
        }
    }

    public ExpressionDef GetRenderExpressionForParms(PawnDrawParms parms)
    {
        bool workshopActive = WorkshopRenderContext.Active;
        bool portraitContext = !workshopActive && parms.Portrait;
        bool useDynamic = workshopActive || (portraitContext ? ArePortraitFaceDynamicsEnabled() : ShouldRunRuntimeFaceDynamics());
        return GetCachedRenderExpression(workshopActive, portraitContext, useDynamic);
    }

    public ExpressionDef GetRenderExpressionForCurrentContext()
    {
        bool workshopActive = WorkshopRenderContext.Active;
        bool portraitContext = !workshopActive && FacePartsPortraitRenderContext.NonWorkshopPortraitActive;
        bool useDynamic = workshopActive || (portraitContext ? ArePortraitFaceDynamicsEnabled() : ShouldRunRuntimeFaceDynamics());
        return GetCachedRenderExpression(workshopActive, portraitContext, useDynamic);
    }

    private void HandleRuntimeFaceDynamicsTransition(bool runtimeDynamicsAllowed, int currentGameTick)
    {
        if (runtimeDynamicsAllowed == lastRuntimeFaceDynamicsAllowed)
            return;

        lastRuntimeFaceDynamicsAllowed = runtimeDynamicsAllowed;

        if (runtimeDynamicsAllowed)
        {
            RefreshCachedVisualState(false);
            if (enabled)
                ScheduleNextBlink(currentGameTick);
            shouldUpdate = true;
            return;
        }

        bool hadTransientDynamics = facialAnim != null || animExpression != null;
        ClearRuntimeFacialAnimState();
        ClearBlinkScheduling();
        if (hadTransientDynamics)
            shouldUpdate = true;
    }

    private void EnsureBlinkScheduling()
    {
        int currentGameTick = CurrentGameTick;
        if (nextBlinkGameTick < 0)
            ScheduleNextBlink(currentGameTick);
    }

    private int StableDerivedCrowdBlinkHash(int salt)
    {
        int pawnId = pawn?.thingIDNumber ?? 0;
        unchecked
        {
            int hash = pawnId;
            hash = (hash * 397) ^ salt;
            hash ^= 0x5f356495;
            return hash & int.MaxValue;
        }
    }

    private static int PositiveMod(int value, int modulus)
    {
        if (modulus <= 0)
            return 0;

        int remainder = value % modulus;
        return remainder < 0 ? remainder + modulus : remainder;
    }

    private int GetDerivedCrowdBlinkLocalTick(int currentGameTick)
    {
        FacialAnimDef blinkAnim = FacePartsModule_FacialAnimDefOf.FacialAnim_Blink;
        if (blinkAnim == null || pawn == null)
            return -1;

        int animDuration = Mathf.Max(1, blinkAnim.durationTicks);
        int varianceRange = Math.Max(0, FacePartsUtil.BlinkTickVariance * RuntimeCrowdBlinkIntervalMultiplier);
        int intervalBase = Math.Max(animDuration + 1, FacePartsUtil.BlinkInterval * RuntimeCrowdBlinkIntervalMultiplier);
        int varianceOffset = varianceRange > 0
            ? PositiveMod(StableDerivedCrowdBlinkHash(0x2441), varianceRange * 2 + 1) - varianceRange
            : 0;
        int cycleLength = Math.Max(animDuration + 1, intervalBase + varianceOffset);
        int phaseOffset = PositiveMod(StableDerivedCrowdBlinkHash(0x37A9), cycleLength);
        int phaseTick = PositiveMod(currentGameTick + phaseOffset, cycleLength);
        return phaseTick <= animDuration ? phaseTick : -1;
    }

    private ExpressionDef GetDerivedCrowdBlinkExpression(int currentGameTick, bool workshopActive, bool portraitContext, bool useDynamic, bool isSelected)
    {
        if (workshopActive || portraitContext || !useDynamic)
            return null;

        if (pawn == null || facialAnim != null || animExpression != null)
            return null;

        if (!FaceRuntimeActivityManager.ShouldUseCrowdBlinkCadence(pawn, currentGameTick, isSelected))
            return null;

        int localTick = GetDerivedCrowdBlinkLocalTick(currentGameTick);
        if (localTick < 0)
            return null;

        return SampleFacialExpressionAt(FacePartsModule_FacialAnimDefOf.FacialAnim_Blink, localTick);
    }

    private bool ShouldMaintainRuntimeMicroDynamics(int currentGameTick, bool runtimeDynamicsAllowed, bool isSelected, int currentFrameCount)
    {
        if (!runtimeDynamicsAllowed)
            return false;

        if (WorkshopRenderContext.Active || FacePartsPortraitRenderContext.NonWorkshopPortraitActive)
            return false;

        if (isSelected)
            return true;

        return WasRuntimeRenderedRecently(currentGameTick)
            || localEditorActive
            || HasLocalEditorHeartbeat(currentFrameCount)
            || localExtendedAnimatorActive
            || facialAnim != null
            || animExpression != null;
    }

    private bool ShouldThrottleRuntimeBlinkCadence(int currentGameTick, bool isSelected, int currentFrameCount)
    {
        if (WorkshopRenderContext.Active || FacePartsPortraitRenderContext.NonWorkshopPortraitActive)
            return false;

        if (!ShouldMaintainRuntimeMicroDynamics(currentGameTick, true, isSelected, currentFrameCount))
            return false;

        if (isSelected)
            return false;

        return !localEditorActive && !HasLocalEditorHeartbeat(currentFrameCount) && !localExtendedAnimatorActive;
    }

    private void ClearBlinkScheduling()
    {
        nextBlinkGameTick = -1;
    }

    private void ScheduleNextBlink(int currentGameTick)
    {
        ScheduleNextBlink(currentGameTick, IsSelectedPawn(), Time.frameCount);
    }

    private void ScheduleNextBlink(int currentGameTick, bool isSelected, int currentFrameCount)
    {
        int intervalMultiplier = ShouldThrottleRuntimeBlinkCadence(currentGameTick, isSelected, currentFrameCount)
            ? RuntimeCrowdBlinkIntervalMultiplier
            : 1;
        int blinkInterval = FacePartsUtil.BlinkInterval * intervalMultiplier;
        int blinkVariance = FacePartsUtil.BlinkTickVariance * intervalMultiplier;
        blinkTick = blinkInterval + Rand.Range(-blinkVariance, blinkVariance);
        if (blinkTick <= 0)
        {
            nextBlinkGameTick = int.MaxValue;
            return;
        }

        nextBlinkGameTick = currentGameTick + blinkTick;
    }

    private bool HasLocalEditorHeartbeat(int currentFrameCount)
    {
        return localEditorHeartbeatUntilFrame >= currentFrameCount;
    }

    private bool IsLocallyHighPriorityRuntimeRelevant()
    {
        return IsLocallyHighPriorityRuntimeRelevant(Time.frameCount);
    }

    private bool IsLocallyHighPriorityRuntimeRelevant(int currentFrameCount)
    {
        return localEditorActive
            || HasLocalEditorHeartbeat(currentFrameCount)
            || localExtendedAnimatorActive
            || shouldUpdate
            || facialAnim != null
            || hasPendingAutoEyePatchFaceRefresh
            || pendingRuntimeFaceEvents != FacePartsEventMask.None;
    }

    private bool IsLocallyRuntimeRelevant(int currentGameTick)
    {
        return IsLocallyRuntimeRelevant(currentGameTick, IsLocallyHighPriorityRuntimeRelevant());
    }

    private bool IsLocallyRuntimeRelevant(int currentGameTick, bool hasImmediateLocalWork)
    {
        return hasImmediateLocalWork || WasRuntimeRenderedRecently(currentGameTick);
    }

    private bool ShouldUseDormantCompFastPath(int currentGameTick, bool isSelected, bool hasImmediateLocalWork)
    {
        if (WorkshopRenderContext.Active || FacePartsPortraitRenderContext.NonWorkshopPortraitActive)
            return false;

        if (isSelected)
            return false;

        return !IsLocallyRuntimeRelevant(currentGameTick, hasImmediateLocalWork);
    }

    internal void NotifyEditorHeartbeat(int currentFrameCount, int graceFrames = 2)
    {
        int clampedGraceFrames = graceFrames < 1 ? 1 : graceFrames;
        int untilFrame = currentFrameCount + clampedGraceFrames;
        if (localEditorHeartbeatUntilFrame < untilFrame)
            localEditorHeartbeatUntilFrame = untilFrame;

        WakeCompTickNow();
    }

    internal void NotifyEditorActiveStateChanged(bool isActive)
    {
        localEditorActive = isActive;
        if (isActive)
            WakeCompTickNow();
    }

    internal void NotifyExtendedAnimatorStateChanged(bool isActive)
    {
        localExtendedAnimatorActive = isActive;
        if (isActive)
            WakeCompTickNow();
    }

    public void NotifyRuntimeRendered(int currentGameTick)
    {
        NotifyRuntimeRendered(currentGameTick, IsSelectedPawn());
    }

    public void NotifyRuntimeRendered(int currentGameTick, bool isSelected)
    {
        EnsureRuntimeRegistered();
        FaceRuntimeActivityManager.NotifyRuntimeRendered(pawn, currentGameTick, RuntimeRenderGraceTicks);
        int scheduledTick = (isSelected || HasActiveExtendedAnimation())
            ? currentGameTick + 1
            : currentGameTick + VisibleRuntimeCompTickIntervalTicks;
        WakeCompTickAtOrBefore(scheduledTick);
    }

    private bool WasRuntimeRenderedRecently(int currentGameTick)
    {
        return FaceRuntimeActivityManager.WasRuntimeRenderedRecently(pawn, currentGameTick);
    }

    private void WakeCompTickAtOrBefore(int scheduledTick)
    {
        if (nextScheduledCompTick == int.MinValue || nextScheduledCompTick > scheduledTick)
            nextScheduledCompTick = scheduledTick;
    }

    private void WakeCompTickNow()
    {
        WakeCompTickAtOrBefore(CurrentGameTick);
    }


    private void EnsureRuntimeRegistered()
    {
        if (ModMain.IsNlFacialInstalled)
            return;

        if (pawn?.RaceProps?.Humanlike != true)
            return;

        int runtimeGeneration = FaceRuntimeActivityManager.RuntimeGeneration;
        if (registeredRuntimeGeneration == runtimeGeneration && FaceRuntimeActivityManager.IsRegistered(this))
            return;

        FaceRuntimeActivityManager.EnsureRegistered(this);
        registeredRuntimeGeneration = runtimeGeneration;
    }

    internal void NotifyRuntimeFaceEventQueued(FacePartsEventMask mask)
    {
        if (mask == FacePartsEventMask.None)
            return;

        EnsureRuntimeRegistered();
        pendingRuntimeFaceEvents |= mask;
        FaceRuntimeActivityManager.NotifyExplicitWake(pawn, CurrentGameTick);
        WakeCompTickNow();
    }

    internal void NotifyPendingAutoEyePatchFaceRefreshQueued()
    {
        EnsureRuntimeRegistered();
        hasPendingAutoEyePatchFaceRefresh = true;
        FaceRuntimeActivityManager.NotifyExplicitWake(pawn, CurrentGameTick);
        WakeCompTickNow();
    }

    private bool ShouldSkipScheduledCompTick(int currentGameTick)
    {
        return currentGameTick < nextScheduledCompTick;
    }

    private void ScheduleNextCompTick(int currentGameTick)
    {
        ScheduleNextCompTick(currentGameTick, IsSelectedPawn(), IsLocallyHighPriorityRuntimeRelevant());
    }

    private void ScheduleNextCompTick(int currentGameTick, bool isSelected, bool hasImmediateLocalWork)
    {
        if (ModMain.IsNlFacialInstalled)
        {
            nextScheduledCompTick = int.MinValue;
            return;
        }

        int nextOffset;
        if (isSelected || hasImmediateLocalWork)
            nextOffset = 1;
        else if (WasRuntimeRenderedRecently(currentGameTick))
            nextOffset = VisibleRuntimeCompTickIntervalTicks;
        else
            nextOffset = IdleRuntimeCompTickIntervalTicks;

        nextScheduledCompTick = currentGameTick + nextOffset;
    }

    private bool ShouldConsumeRuntimeDynamicsNow(int currentGameTick, bool runtimeDynamicsAllowed)
    {
        return ShouldConsumeRuntimeDynamicsNow(currentGameTick, runtimeDynamicsAllowed, IsSelectedPawn(), IsLocallyRuntimeRelevant(currentGameTick));
    }

    private bool ShouldConsumeRuntimeDynamicsNow(int currentGameTick, bool runtimeDynamicsAllowed, bool isSelected, bool isLocallyRuntimeRelevant)
    {
        if (WorkshopRenderContext.Active)
            return true;

        if (FacePartsPortraitRenderContext.NonWorkshopPortraitActive)
            return true;

        if (!runtimeDynamicsAllowed)
            return false;

        if (isSelected)
            return true;

        return isLocallyRuntimeRelevant;
    }


    public override void CompTick()
    {
        // GameComponent_FacePartsTick handles all normal-path work for registered humanlike pawns.
        // CompTick only needs to run for the NLFacial compatibility latch.
        if (!ModMain.IsNlFacialInstalled)
        {
            base.CompTick();
            return;
        }

        if (!nlFacialDisableLatched)
        {
            bool needsRefresh = enabled || facialAnim != null || animExpression != null || baseExpression != null || shouldUpdate || faceWarmInitialized;
            DisableFacePartsInstance();
            nlFacialDisableLatched = true;
            if (needsRefresh)
                RequestFaceGraphicsDirty(pawn?.Drawer?.renderer);
        }

        base.CompTick();
    }

    /// <summary>
    /// Called by GameComponent_FacePartsTick once per game tick for every registered humanlike pawn.
    /// Contains all logic previously in CompTick for non-NLFacial pawns.
    /// </summary>
    public void RunFacePartsTick(int currentGameTick)
    {
        nlFacialDisableLatched = false;
        EnsureRuntimeRegistered();

        int currentFrameCount = Time.frameCount;
        bool isSelected = IsSelectedPawn();

        if (ShouldSkipScheduledCompTick(currentGameTick))
            return;

        bool hasImmediateLocalWork = IsLocallyHighPriorityRuntimeRelevant(currentFrameCount);
        if (ShouldUseDormantCompFastPath(currentGameTick, isSelected, hasImmediateLocalWork))
        {
            ClearBlinkScheduling();
            ScheduleNextCompTick(currentGameTick, isSelected, hasImmediateLocalWork);
            return;
        }

        // Catch error when ticking to prevent completely breaking mid save
        try
        {
            if (hasPendingAutoEyePatchFaceRefresh)
            {
                hasPendingAutoEyePatchFaceRefresh = false;
                RefreshFaceHard(true);
            }

            bool runtimeDynamicsAllowed = ShouldRunRuntimeFaceDynamics();
            FaceExpressionState pendingVisualState = default(FaceExpressionState);
            bool visualStateDue = false;
            if (enabled && pendingRuntimeFaceEvents != FacePartsEventMask.None)
            {
                FacePartsEventMask pendingEvents = pendingRuntimeFaceEvents;
                FacePartsEventMask processableEvents = pendingEvents & StructuralRuntimeFaceEvents;
                if (runtimeDynamicsAllowed)
                    processableEvents |= pendingEvents & ~StructuralRuntimeFaceEvents;

                if (processableEvents != FacePartsEventMask.None)
                {
                    pendingRuntimeFaceEvents = pendingEvents & ~processableEvents;
                    visualStateDue = TryApplyPendingFaceEvents(processableEvents, out pendingVisualState);
                }
            }

            // Recompute once after event processing — pendingRuntimeFaceEvents may have been cleared above,
            // and hasPendingAutoEyePatchFaceRefresh may have been consumed. All further uses in this tick
            // share this single evaluation.
            hasImmediateLocalWork = IsLocallyHighPriorityRuntimeRelevant(currentFrameCount);
            bool isLocallyRuntimeRelevant = IsLocallyRuntimeRelevant(currentGameTick, hasImmediateLocalWork);
            bool consumeRuntimeDynamicsNow = ShouldConsumeRuntimeDynamicsNow(currentGameTick, runtimeDynamicsAllowed, isSelected, isLocallyRuntimeRelevant);
            HandleRuntimeFaceDynamicsTransition(runtimeDynamicsAllowed, currentGameTick);

            if (!enabled && facialAnim == null && !shouldUpdate && !visualStateDue)
            {
                ClearBlinkScheduling();
                ScheduleNextCompTick(currentGameTick, isSelected, hasImmediateLocalWork);
                return;
            }

            if (!consumeRuntimeDynamicsNow && !visualStateDue)
            {
                ClearBlinkScheduling();
                ScheduleNextCompTick(currentGameTick, isSelected, hasImmediateLocalWork);
                return;
            }

            bool maintainRuntimeMicroDynamics = ShouldMaintainRuntimeMicroDynamics(currentGameTick, runtimeDynamicsAllowed, isSelected, currentFrameCount);
            if (enabled && maintainRuntimeMicroDynamics)
                EnsureBlinkScheduling();
            else
                ClearBlinkScheduling();

            bool blinkDue = maintainRuntimeMicroDynamics && enabled && facialAnim == null && nextBlinkGameTick >= 0 && currentGameTick >= nextBlinkGameTick;

            if (facialAnim == null && !shouldUpdate && !blinkDue && !visualStateDue)
            {
                ScheduleNextCompTick(currentGameTick, isSelected, hasImmediateLocalWork);
                return;
            }

            if (enabled)
            {
                if (runtimeDynamicsAllowed && facialAnim != null)
                {
                    int localAnimTick = GetCurrentFacialAnimLocalTick(currentGameTick);
                    animTicks = localAnimTick;

                    ExpressionDef sampled = SampleCurrentFacialExpression();
                    if (!AreExpressionsEquivalent(animExpression, sampled))
                    {
                        animExpression = sampled;
                        shouldUpdate = true;
                    }

                    // If animation finishes, reset
                    if (localAnimTick > facialAnim.durationTicks)
                    {
                        ClearRuntimeFacialAnimState();
                        shouldUpdate = true;
                    }
                }
                // Blinking animation if facial animation isn't already playing
                else if (blinkDue)
                {
                    if (!PawnStateUtil.IsAsleep(pawn))
                    {
                        // Babies drool sometimes
                        if (PawnStateUtil.isInfant(pawn) && Rand.Bool)
                            PlayFacialAnim(FacePartsModule_FacialAnimDefOf.FacialAnim_Drool);

                        // Everyone needs to blink
                        else
                            PlayFacialAnim(FacePartsModule_FacialAnimDefOf.FacialAnim_Blink);

                        shouldUpdate = true;
                    }

                    ScheduleNextBlink(currentGameTick, isSelected, currentFrameCount);
                }

                if (visualStateDue)
                    RefreshCachedVisualState(pendingVisualState, facialAnim == null && animExpression == null);
            }

            if (shouldUpdate)
                RefreshFaceSoftFast(false);
        }
        catch (Exception e)
        {
            Log.Error($"[Despicable] - Error in CompFaceParts RunFacePartsTick: {e}");
            DisableFacePartsInstance();
        }

        ScheduleNextCompTick(currentGameTick, isSelected, IsLocallyHighPriorityRuntimeRelevant(currentFrameCount));
    }

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);

        if (ModMain.IsNlFacialInstalled)
            return;

        if (pawn?.RaceProps?.Humanlike != true)
            return;

        EnsureRuntimeRegistered();

        RefreshEnabledFromSettings();
        InitializeFacePartState();

        if (FacePartsEventRuntime.TryConsume(pawn, out FacePartsEventMask consumedEvents))
            pendingRuntimeFaceEvents |= consumedEvents;

        if (AutoEyePatchRuntime.TryConsumePendingFaceRefresh(pawn))
            hasPendingAutoEyePatchFaceRefresh = true;

        if (enabled && pawn.Drawer?.renderer != null)
        {
            AutoEyePatchRuntime.RequestHeadGeneration(pawn.story?.headType, pawn);
            AutoEyePatchRuntime.QueuePendingFaceRefresh(pawn);
        }
    }

    internal void RehydrateAfterRuntimeReset()
    {
        nlFacialDisableLatched = false;

        if (ModMain.IsNlFacialInstalled)
            return;

        if (pawn?.Spawned != true || pawn.RaceProps?.Humanlike != true)
            return;

        EnsureRuntimeRegistered();

        nextScheduledCompTick = int.MinValue;
        lastFaceGraphicsDirtyRequestTick = int.MinValue;

        RefreshEnabledFromSettings();
        InitializeFacePartState();

        if (FacePartsEventRuntime.TryConsume(pawn, out FacePartsEventMask consumedEvents))
            pendingRuntimeFaceEvents |= consumedEvents;

        if (AutoEyePatchRuntime.TryConsumePendingFaceRefresh(pawn))
            hasPendingAutoEyePatchFaceRefresh = true;

        if (enabled && pawn.Drawer?.renderer != null)
        {
            AutoEyePatchRuntime.RequestHeadGeneration(pawn.story?.headType, pawn);
            AutoEyePatchRuntime.QueuePendingFaceRefresh(pawn);
            WakeCompTickNow();
        }
    }

    public void RefreshEnabledFromSettings()
    {
        if (ModMain.IsNlFacialInstalled || !IsFacePartsEnabledInSettings)
        {
            enabled = false;
            ReconcileGlobalPortraitWarmupNeededCount();
            return;
        }

        if (pawn?.RaceProps?.Humanlike != true || pawn.story == null)
        {
            enabled = false;
            ReconcileGlobalPortraitWarmupNeededCount();
            return;
        }

        HeadTypeDef headType = pawn.story.headType;
        cachedEligibleHeadType = headType;
        cachedEligibleHeadResult = headType != null && !FacePartsUtil.IsHeadBlacklisted(headType);
        cachedEligibleHeadValid = true;
        enabled = cachedEligibleHeadResult;
        ReconcileGlobalPortraitWarmupNeededCount();
    }

    // Check at initialization whether to enable faces
    // (Settings check if NL is installed and turns off my faces if they are)
    public override void Initialize(CompProperties props)
    {
        base.Initialize(props);
        RefreshEnabledFromSettings();
        InitializeFacePartState();
    }

    public void InitializeFacePartState()
    {
        // Determine gender path
        genderPath = PawnStateUtil.ComparePawnGenderToByte(pawn, (byte)Gender.Female) ? "Female/" : DEFAULT_GENDER_PATH;

        // Check if enabled, if not, continue to do nothing for performance
        if (!enabled)
        {
            ReconcileGlobalPortraitWarmupNeededCount();
            return;
        }

        if (pawn == null || pawn.RaceProps?.Humanlike != true || pawn.story == null)
        {
            DisableFacePartsInstance();
            return;
        }

        // Alpha gene headtypes are not designed for faces to be plastered on them
        // So disable them here
        if (!IsCurrentHeadEligible())
        {
            DisableFacePartsInstance();
            return;
        }

        SeedExpressionStateFromPawn();
        RefreshBlockingNoseGeneCache();
        RefreshCachedVisualState(false);
        SanitizeRetiredEyeDetailStyle();
        ReconcileGlobalPortraitWarmupNeededCount();
    }

    private bool IsCurrentHeadEligible()
    {
        HeadTypeDef headType = pawn?.story?.headType;
        if (headType == null)
        {
            cachedEligibleHeadType = null;
            cachedEligibleHeadResult = false;
            cachedEligibleHeadValid = true;
            return false;
        }

        if (cachedEligibleHeadValid && cachedEligibleHeadType == headType)
            return cachedEligibleHeadResult;

        cachedEligibleHeadType = headType;
        cachedEligibleHeadResult = !FacePartsUtil.IsHeadBlacklisted(headType);
        cachedEligibleHeadValid = true;
        return cachedEligibleHeadResult;
    }

    private static ExpressionDef CloneExpression(ExpressionDef source)
    {
        if (source == null)
            return null;

        return new ExpressionDef
        {
            texPathEyes = source.texPathEyes,
            texPathBrows = source.texPathBrows,
            browVariant = source.browVariant,
            texPathMouth = source.texPathMouth,
            texPathDetail = source.texPathDetail,
            texPathEyeDetailState = source.texPathEyeDetailState,
            texPathFaceDetailState = source.texPathFaceDetailState,
            eyesOffset = source.eyesOffset,
            browsOffset = source.browsOffset,
            mouthOffset = source.mouthOffset,
            detailOffset = source.detailOffset,
            eyeDetailOffset = source.eyeDetailOffset,
            faceDetailOffset = source.faceDetailOffset
        };
    }

    private static void OverlayExpression(ExpressionDef destination, ExpressionDef source)
    {
        if (destination == null || source == null)
            return;

        destination.eyesOffset = source.eyesOffset ?? destination.eyesOffset ?? Vector3.zero;
        destination.browsOffset = source.browsOffset ?? destination.browsOffset ?? destination.eyesOffset ?? Vector3.zero;
        destination.mouthOffset = source.mouthOffset ?? destination.mouthOffset ?? Vector3.zero;
        destination.detailOffset = source.detailOffset ?? destination.detailOffset ?? Vector3.zero;
        destination.eyeDetailOffset = source.eyeDetailOffset ?? destination.eyeDetailOffset ?? destination.detailOffset ?? Vector3.zero;
        destination.faceDetailOffset = source.faceDetailOffset ?? destination.faceDetailOffset ?? destination.mouthOffset ?? Vector3.zero;

        if (!source.texPathEyes.NullOrEmpty())
            destination.texPathEyes = source.texPathEyes;
        if (!source.texPathBrows.NullOrEmpty())
            destination.texPathBrows = source.texPathBrows;
        if (!source.browVariant.NullOrEmpty())
            destination.browVariant = source.browVariant;
        if (!source.texPathMouth.NullOrEmpty())
            destination.texPathMouth = source.texPathMouth;
        if (!source.texPathDetail.NullOrEmpty())
            destination.texPathDetail = source.texPathDetail;
        if (!source.texPathEyeDetailState.NullOrEmpty())
            destination.texPathEyeDetailState = source.texPathEyeDetailState;
        if (!source.texPathFaceDetailState.NullOrEmpty())
            destination.texPathFaceDetailState = source.texPathFaceDetailState;
    }

    private void ClearRuntimeFacialAnimState()
    {
        facialAnim = null;
        animExpression = null;
        animTicks = 0;
        facialAnimStartGameTick = -1;
        curKeyframe = 0;
    }

    private int GetCurrentFacialAnimLocalTick(int currentGameTick)
    {
        if (facialAnim == null)
            return -1;

        if (facialAnimStartGameTick < 0)
            facialAnimStartGameTick = Math.Max(0, currentGameTick - Math.Max(animTicks, 0));

        int localTick = currentGameTick - facialAnimStartGameTick;
        return localTick < 0 ? 0 : localTick;
    }

    private static FacialAnimSampleCache GetOrBuildFacialAnimSampleCache(FacialAnimDef anim)
    {
        if (anim == null || anim.keyframes.NullOrEmpty())
            return null;

        if (CachedFacialAnimSamples.TryGetValue(anim, out FacialAnimSampleCache cache))
            return cache;

        int count = anim.keyframes.Count;
        int[] ticks = new int[count];
        ExpressionDef[] frames = new ExpressionDef[count];
        ExpressionDef running = null;

        for (int i = 0; i < count; i++)
        {
            FacialAnimKeyframeDef keyframe = anim.keyframes[i];
            ticks[i] = keyframe?.tick ?? int.MaxValue;

            ExpressionDef source = keyframe?.expression;
            if (source != null)
            {
                ExpressionDef merged = running != null ? CloneExpression(running) : new ExpressionDef();
                OverlayExpression(merged, source);
                running = merged;
            }

            frames[i] = running;
        }

        cache = new FacialAnimSampleCache
        {
            Ticks = ticks,
            Frames = frames
        };

        CachedFacialAnimSamples[anim] = cache;
        return cache;
    }

    private ExpressionDef SampleCurrentFacialExpression()
    {
        FacialAnimSampleCache cache = GetOrBuildFacialAnimSampleCache(facialAnim);
        if (cache == null || cache.Ticks == null || cache.Frames == null || cache.Ticks.Length == 0 || animTicks < 0)
        {
            curKeyframe = 0;
            return null;
        }

        if (curKeyframe < 0)
            curKeyframe = 0;

        if (curKeyframe >= cache.Ticks.Length || cache.Ticks[curKeyframe] > animTicks)
            curKeyframe = 0;

        while (curKeyframe + 1 < cache.Ticks.Length && cache.Ticks[curKeyframe + 1] <= animTicks)
            curKeyframe++;

        if (cache.Ticks[curKeyframe] > animTicks)
            return null;

        return cache.Frames[curKeyframe];
    }

    public void TryInitActions()
    {
        InitializeFacePartState();
    }

    public void InvalidateFaceStructure()
    {
        faceStructureDirty = true;
        faceWarmInitialized = false;
        InvalidateCachedVisualState();
        ReconcileGlobalPortraitWarmupNeededCount();
    }

    private void InvalidateCachedEyeOverlayState()
    {
        lastEyeVisualStateCheckTick = int.MinValue;
        nextEyeVisualStateRecheckTick = int.MinValue;
        cachedEyeVisualGeneCount = -1;
        cachedEyeVisualHediffCount = -1;
        cachedEyeVisualInputSignature = int.MinValue;
        cachedEyeVisualStateEligibleForSlowPolling = false;
        cachedHasBlockingEyeGene = false;
        cachedShouldSuppressForeignEyeGeneGraphics = false;
        cachedHasPotentialForeignEyeHealthVisual = false;
        cachedResolvedEyeTint = Color.black;
        lastForeignEyeVisualCheckTick = int.MinValue;
        cachedForeignEyeVisualInputSignature = int.MinValue;
        cachedForeignEyeVisualRootSignature = int.MinValue;
        cachedForeignEyeBlockMask = ForeignEyeBlockMask.None;
        foreignEyeBlockMaskDirty = true;
    }

    public void InvalidateCachedVisualState()
    {
        cachedVisualStateValid = false;
        lastVisualStateSignature = int.MinValue;
        cachedExpressionStateInitialized = false;
        cachedExpressionState = default(FaceExpressionState);
        baseExpression = null;
        baseDetailTexPath = EMPTY_DETAIL_TEX_PATH;
        InvalidateCachedEyeOverlayState();
        ReconcileGlobalPortraitWarmupNeededCount();
    }

    public string GetBaseDetailTexPath()
    {
        return baseDetailTexPath.NullOrEmpty() ? EMPTY_DETAIL_TEX_PATH : baseDetailTexPath;
    }

    private static bool DoesTextureExist(string texPath)
    {
        if (texPath.NullOrEmpty())
            return false;

        if (CachedTextureExistsByPath.TryGetValue(texPath, out bool cachedExists))
            return cachedExists;

        bool exists = ContentFinder<Texture2D>.Get(texPath, false) != null;
        CachedTextureExistsByPath[texPath] = exists;
        return exists;
    }

    private static string FirstRenderableTexturePath(string candidate)
    {
        if (candidate.NullOrEmpty())
            return null;

        return DoesTextureExist(candidate) ? candidate : null;
    }

    private static string FirstRenderableTexturePath(string candidateA, string candidateB)
    {
        return FirstRenderableTexturePath(candidateA)
            ?? FirstRenderableTexturePath(candidateB);
    }

    private static string FirstRenderableTexturePath(string candidateA, string candidateB, string candidateC)
    {
        return FirstRenderableTexturePath(candidateA)
            ?? FirstRenderableTexturePath(candidateB, candidateC);
    }

    private static string FirstRenderableTexturePath(string candidateA, string candidateB, string candidateC, string candidateD)
    {
        return FirstRenderableTexturePath(candidateA)
            ?? FirstRenderableTexturePath(candidateB, candidateC, candidateD);
    }

    private static string FirstRenderableTexturePath(string candidateA, string candidateB, string candidateC, string candidateD, string candidateE)
    {
        return FirstRenderableTexturePath(candidateA)
            ?? FirstRenderableTexturePath(candidateB, candidateC, candidateD, candidateE);
    }

    private static bool IsEmptyDetailPath(string texPath)
    {
        return texPath.NullOrEmpty() || string.Equals(texPath, EMPTY_DETAIL_TEX_PATH, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeFlatBrowTexPath(string texPath)
    {
        if (texPath.NullOrEmpty())
            return texPath;

        const string angledSuffix = "_angled";
        const string flatSuffix = "_flat";
        const string sShapedSuffix = "_s_shaped";

        if (texPath.EndsWith(flatSuffix, StringComparison.OrdinalIgnoreCase))
            return texPath;

        if (texPath.EndsWith(angledSuffix, StringComparison.OrdinalIgnoreCase))
            return texPath.Substring(0, texPath.Length - angledSuffix.Length) + flatSuffix;

        if (texPath.EndsWith(sShapedSuffix, StringComparison.OrdinalIgnoreCase))
            return texPath.Substring(0, texPath.Length - sShapedSuffix.Length) + flatSuffix;

        return texPath;
    }

    private static string NormalizeBrowVariantToken(string browVariant)
    {
        if (browVariant.NullOrEmpty())
            return null;

        string normalized = browVariant.Trim().Replace('-', '_').Replace(' ', '_').ToLowerInvariant();
        return normalized switch
        {
            "flat" => "flat",
            "angled" => "angled",
            "s_shaped" => "s_shaped",
            "sshaped" => "s_shaped",
            _ => null,
        };
    }

    private static string BuildBrowVariantTexPath(string baseStyleTexPath, string browVariant)
    {
        string normalizedBase = NormalizeFlatBrowTexPath(baseStyleTexPath);
        string normalizedVariant = NormalizeBrowVariantToken(browVariant);
        if (normalizedBase.NullOrEmpty() || normalizedVariant.NullOrEmpty())
            return null;

        const string flatSuffix = "_flat";
        if (!normalizedBase.EndsWith(flatSuffix, StringComparison.OrdinalIgnoreCase))
            return null;

        return normalizedBase.Substring(0, normalizedBase.Length - flatSuffix.Length) + "_" + normalizedVariant;
    }

    private string ResolveBrowExpressionTexPath(ExpressionDef expression)
    {
        if (expression == null)
            return null;

        string styleBaseTexPath = browStyleDef?.texPath;
        return FirstRenderableTexturePath(
            expression.texPathBrows,
            BuildBrowVariantTexPath(styleBaseTexPath, expression.browVariant),
            NormalizeFlatBrowTexPath(styleBaseTexPath),
            DEFAULT_BROW_TEX_PATH);
    }

    private static string GetEyeDetailStatePath(ExpressionDef expression)
    {
        if (expression == null)
            return null;

        return !expression.texPathEyeDetailState.NullOrEmpty()
            ? expression.texPathEyeDetailState
            : expression.texPathDetail;
    }

    private static string GetFaceDetailStatePath(ExpressionDef expression)
    {
        if (expression == null)
            return null;

        return expression.texPathFaceDetailState;
    }

    private static bool IsRightSideDebugLabel(string debugLabel)
    {
        return !debugLabel.NullOrEmpty()
            && debugLabel.EndsWith("_R", StringComparison.OrdinalIgnoreCase);
    }

    public static FacePartSideMode ResolveStyleSideMode(FacePartStyleDef style, FacePartSideMode selectedSideMode = FacePartSideMode.Both)
    {
        return style?.ResolveEffectiveSideMode(selectedSideMode) ?? FacePartSideMode.Both;
    }

    private static string ResolveSideAwareStyleTexturePath(FacePartStyleDef style, FacePartSideMode selectedSideMode, string debugLabel, string fallbackTexPath)
    {
        bool isRightSide = IsRightSideDebugLabel(debugLabel);
        if (style != null && !style.AllowsSide(isRightSide, selectedSideMode))
            return EMPTY_DETAIL_TEX_PATH;

        return FirstRenderableTexturePath(
            style?.texPath,
            fallbackTexPath,
            EMPTY_DETAIL_TEX_PATH) ?? EMPTY_DETAIL_TEX_PATH;
    }

    public string ResolveEyeDetailStyleTexturePath(string fallbackTexPath = null)
    {
        return ResolveEyeDetailStyleTexturePathForDebugLabel(null, fallbackTexPath);
    }

    public string ResolveEyeDetailBaseStyleTexturePath(string fallbackTexPath = null)
    {
        return FirstRenderableTexturePath(
            eyeDetailStyleDef?.texPath,
            fallbackTexPath,
            EMPTY_DETAIL_TEX_PATH) ?? EMPTY_DETAIL_TEX_PATH;
    }

    public FacePartSideMode GetResolvedEyeDetailSideMode()
    {
        return ResolveStyleSideMode(eyeDetailStyleDef, eyeDetailSideMode);
    }

    private static bool TryResolveVisibleEyeDetailSideForFacing(string debugLabel, Rot4 facing, out FacePartSideMode visibleSideMode)
    {
        visibleSideMode = FacePartSideMode.Both;
        switch (facing.AsInt)
        {
            case 0: // North
                return false;
            case 1: // East
                if (IsRightSideDebugLabel(debugLabel))
                    return false;
                visibleSideMode = FacePartSideMode.LeftOnly;
                return true;
            case 2: // South
                visibleSideMode = IsRightSideDebugLabel(debugLabel)
                    ? FacePartSideMode.RightOnly
                    : FacePartSideMode.LeftOnly;
                return true;
            case 3: // West
                if (IsRightSideDebugLabel(debugLabel))
                    return false;
                visibleSideMode = FacePartSideMode.RightOnly;
                return true;
            default:
                return false;
        }
    }

    public bool ShouldRenderEyeDetailStyleForFacing(string debugLabel, Rot4 facing)
    {
        if (eyeDetailStyleDef == null)
            return false;

        FacePartSideMode effectiveSideMode = GetResolvedEyeDetailSideMode();
        if (effectiveSideMode == FacePartSideMode.Both)
            return true;

        if (!TryResolveVisibleEyeDetailSideForFacing(debugLabel, facing, out FacePartSideMode visibleSideMode))
            return false;

        return effectiveSideMode == visibleSideMode;
    }

    public string ResolveEyeDetailStyleTexturePathForDebugLabel(string debugLabel, string fallbackTexPath = null)
    {
        return ResolveSideAwareStyleTexturePath(eyeDetailStyleDef, eyeDetailSideMode, debugLabel, fallbackTexPath);
    }

    public string ResolveEyeDetailStateTexturePath()
    {
        ExpressionDef renderExpression = GetRenderExpressionForCurrentContext();
        string baseDetailPath = GetBaseDetailTexPath();
        string specialStatePath = GetSpecialEyeDetailStateTexturePathThisTick();
        return FirstRenderableTexturePath(
            GetEyeDetailStatePath(renderExpression),
            GetEyeDetailStatePath(baseExpression),
            specialStatePath,
            IsEmptyDetailPath(baseDetailPath) ? null : baseDetailPath,
            EMPTY_DETAIL_TEX_PATH) ?? EMPTY_DETAIL_TEX_PATH;
    }

    public string ResolveFaceDetailStyleTexturePath(string fallbackTexPath = null)
    {
        return EMPTY_DETAIL_TEX_PATH;
    }

    public string ResolveFaceDetailStateTexturePath()
    {
        return EMPTY_DETAIL_TEX_PATH;
    }

    public string ResolveTexturePathForDebugLabel(string debugLabel, string fallbackTexPath)
    {
        ExpressionDef renderExpression = GetRenderExpressionForCurrentContext();
        string label = debugLabel ?? string.Empty;
        switch (label)
        {
            case "FacePart_Eye_L":
            case "FacePart_Eye_R":
                return FirstRenderableTexturePath(
                    FacePartsUtil.GetEyePath(pawn, renderExpression?.texPathEyes),
                    FacePartsUtil.GetEyePath(pawn, baseExpression?.texPathEyes),
                    FacePartsUtil.GetEyePath(pawn, eyeStyleDef?.texPath),
                    FacePartsUtil.GetEyePath(pawn, fallbackTexPath),
                    DEFAULT_EYE_TEX_PATH) ?? DEFAULT_EYE_TEX_PATH;
            case "FacePart_Brow_L":
            case "FacePart_Brow_R":
                return FirstRenderableTexturePath(
                    ResolveBrowExpressionTexPath(renderExpression),
                    ResolveBrowExpressionTexPath(baseExpression),
                    NormalizeFlatBrowTexPath(browStyleDef?.texPath),
                    NormalizeFlatBrowTexPath(fallbackTexPath),
                    DEFAULT_BROW_TEX_PATH) ?? DEFAULT_BROW_TEX_PATH;
            case "FacePart_EyeDetail_L":
            case "FacePart_EyeDetail_R":
                return FirstRenderableTexturePath(
                    ResolveEyeDetailStateTexturePath(),
                    ResolveEyeDetailStyleTexturePathForDebugLabel(label, fallbackTexPath),
                    EMPTY_DETAIL_TEX_PATH) ?? EMPTY_DETAIL_TEX_PATH;
            case "FacePart_FaceDetail":
                return EMPTY_DETAIL_TEX_PATH;
            case "FacePart_Mouth":
                return FirstRenderableTexturePath(
                    renderExpression?.texPathMouth,
                    baseExpression?.texPathMouth,
                    mouthStyleDef?.texPath,
                    fallbackTexPath,
                    DEFAULT_MOUTH_TEX_PATH) ?? DEFAULT_MOUTH_TEX_PATH;
            case "FacePart_Detail_L":
            case "FacePart_Detail_R":
            case "FacePart_SecondaryDetail_L":
            case "FacePart_SecondaryDetail_R":
                return EMPTY_DETAIL_TEX_PATH;
            default:
                return FirstRenderableTexturePath(fallbackTexPath, EMPTY_DETAIL_TEX_PATH) ?? EMPTY_DETAIL_TEX_PATH;
        }
    }

    public bool AreStyleSlotsAssigned()
    {
        return eyeStyleDef != null
            && browStyleDef != null
            && mouthStyleDef != null
            && eyeDetailStyleDef != null;
    }

    private static bool IsPawnAsleepForFace(Pawn pawn)
    {
        if (pawn == null)
            return false;

        JobDef curJobDef = pawn.CurJobDef;
        return curJobDef == JobDefOf.LayDownResting || curJobDef == JobDefOf.LayDown;
    }

    private static bool IsPawnInfantForFace(Pawn pawn)
    {
        int lifeStage = pawn?.ageTracker?.CurLifeStageIndex ?? -1;
        return lifeStage == 0 || lifeStage == 1;
    }

    private FaceExpressionState BuildExpressionStateFromPawn()
    {
        FaceExpressionState state = default(FaceExpressionState);
        if (pawn == null)
            return state;

        state.HasHead = pawn.health?.hediffSet?.HasHead == true;
        state.Asleep = IsPawnAsleepForFace(pawn);
        state.Berserk = pawn.InAggroMentalState;
        state.MentalBreak = pawn.InMentalState;
        state.Infant = IsPawnInfantForFace(pawn);
        state.Drunk = pawn.health?.hediffSet?.HasHediff(HediffDefOf.AlcoholHigh) == true;
        state.Tired = pawn.needs?.rest?.CurLevelPercentage <= 0.3f;
        state.Drafted = pawn.Drafted;
        return state;
    }

    private void SeedExpressionStateFromPawn()
    {
        cachedExpressionState = BuildExpressionStateFromPawn();
        cachedExpressionStateInitialized = true;
    }

    private void EnsureExpressionStateSeeded()
    {
        if (!cachedExpressionStateInitialized)
            SeedExpressionStateFromPawn();
    }

    private bool TrySetCachedState(ref bool field, bool value)
    {
        if (field == value)
            return false;

        field = value;
        return true;
    }

    private bool TryApplyPendingFaceEvents(FacePartsEventMask pendingEvents, out FaceExpressionState state)
    {
        state = default(FaceExpressionState);
        if (!enabled || pawn == null || pendingEvents == FacePartsEventMask.None)
            return false;

        EnsureExpressionStateSeeded();

        bool changed = false;
        bool structureChanged = false;

        if ((pendingEvents & FacePartsEventMask.Drafted) != 0)
            changed |= TrySetCachedState(ref cachedExpressionState.Drafted, pawn.Drafted);

        if ((pendingEvents & FacePartsEventMask.Job) != 0)
            changed |= TrySetCachedState(ref cachedExpressionState.Asleep, IsPawnAsleepForFace(pawn));

        if ((pendingEvents & FacePartsEventMask.Mental) != 0)
        {
            changed |= TrySetCachedState(ref cachedExpressionState.Berserk, pawn.InAggroMentalState);
            changed |= TrySetCachedState(ref cachedExpressionState.MentalBreak, pawn.InMentalState);
        }

        if ((pendingEvents & FacePartsEventMask.Health) != 0)
        {
            InvalidateBlockingNoseGeneCache();

            bool hasHead = pawn.health?.hediffSet?.HasHead == true;
            if (TrySetCachedState(ref cachedExpressionState.HasHead, hasHead))
            {
                changed = true;
                structureChanged = true;
            }

            changed |= TrySetCachedState(ref cachedExpressionState.Drunk, pawn.health?.hediffSet?.HasHediff(HediffDefOf.AlcoholHigh) == true);
        }

        if ((pendingEvents & FacePartsEventMask.Rest) != 0)
            changed |= TrySetCachedState(ref cachedExpressionState.Tired, pawn.needs?.rest?.CurLevelPercentage <= 0.3f);

        if ((pendingEvents & FacePartsEventMask.LifeStage) != 0)
            changed |= TrySetCachedState(ref cachedExpressionState.Infant, IsPawnInfantForFace(pawn));

        if ((pendingEvents & FacePartsEventMask.Structure) != 0)
        {
            InvalidateBlockingNoseGeneCache();
            structureChanged = true;
        }

        if (structureChanged)
        {
            InvalidateFaceStructure();
            SeedExpressionStateFromPawn();
            RefreshBlockingNoseGeneCache();
        }

        state = cachedExpressionState;
        return changed || !cachedVisualStateValid;
    }

    private int ComputeVisualStateSignature(FaceExpressionState state)
    {
        return state.GetSignature();
    }

    private void RefreshCachedEyeVisualStateThisTick()
    {
        int currentTick = Find.TickManager?.TicksGame ?? -1;
        int currentGeneCount = pawn?.genes?.GenesListForReading?.Count ?? 0;
        int currentHediffCount = pawn?.health?.hediffSet?.hediffs?.Count ?? 0;

        if (currentTick >= 0 && lastEyeVisualStateCheckTick == currentTick)
            return;

        if (currentTick >= 0
            && cachedEyeVisualStateEligibleForSlowPolling
            && currentTick < nextEyeVisualStateRecheckTick
            && cachedEyeVisualGeneCount == currentGeneCount
            && cachedEyeVisualHediffCount == currentHediffCount)
        {
            return;
        }

        int currentInputSignature = ComputeEyeVisualInputSignature(out bool hasUnsupportedEyeGene, out bool hasRedEyesGene, out bool hasGrayEyesGene, out bool hasVoidTouched, out bool hasPotentialForeignEyeHealthVisual);
        lastEyeVisualStateCheckTick = currentTick;
        cachedEyeVisualGeneCount = currentGeneCount;
        cachedEyeVisualHediffCount = currentHediffCount;
        cachedEyeVisualInputSignature = currentInputSignature;
        cachedEyeVisualStateEligibleForSlowPolling = false;
        nextEyeVisualStateRecheckTick = int.MinValue;
        lastForeignEyeVisualCheckTick = int.MinValue;
        cachedForeignEyeVisualInputSignature = int.MinValue;
        cachedForeignEyeVisualRootSignature = int.MinValue;
        cachedForeignEyeBlockMask = ForeignEyeBlockMask.None;
        foreignEyeBlockMaskDirty = true;
        cachedHasBlockingEyeGene = false;
        cachedShouldSuppressForeignEyeGeneGraphics = false;
        cachedHasPotentialForeignEyeHealthVisual = hasPotentialForeignEyeHealthVisual;
        cachedResolvedEyeTint = Color.black;

        if (hasUnsupportedEyeGene)
        {
            cachedHasBlockingEyeGene = true;
            return;
        }

        if (hasVoidTouched)
        {
            cachedResolvedEyeTint = Color.white;
            return;
        }

        if (hasRedEyesGene)
        {
            cachedShouldSuppressForeignEyeGeneGraphics = true;
            cachedResolvedEyeTint = Color.red;
            return;
        }

        if (hasGrayEyesGene)
        {
            cachedShouldSuppressForeignEyeGeneGraphics = true;
            cachedResolvedEyeTint = Color.gray;
            return;
        }

        if (currentTick >= 0 && !hasPotentialForeignEyeHealthVisual)
        {
            cachedEyeVisualStateEligibleForSlowPolling = true;
            nextEyeVisualStateRecheckTick = currentTick + StableEyeVisualPollIntervalTicks;
        }
    }

    private int ComputeEyeVisualInputSignature(out bool hasUnsupportedEyeGene, out bool hasRedEyesGene, out bool hasGrayEyesGene, out bool hasVoidTouched, out bool hasPotentialForeignEyeHealthVisual)
    {
        hasUnsupportedEyeGene = false;
        hasRedEyesGene = false;
        hasGrayEyesGene = false;
        hasVoidTouched = false;
        hasPotentialForeignEyeHealthVisual = false;

        int signature = 17;

        List<Gene> genes = pawn?.genes?.GenesListForReading;
        if (genes != null)
        {
            for (int i = 0; i < genes.Count; i++)
            {
                string defName = genes[i]?.def?.defName;
                if (defName.NullOrEmpty() || !defName.StartsWith("eyes_", StringComparison.OrdinalIgnoreCase))
                    continue;

                signature = (signature * 31) + StringComparer.OrdinalIgnoreCase.GetHashCode(defName);

                if (defName.Equals("Eyes_Red", StringComparison.OrdinalIgnoreCase))
                {
                    hasRedEyesGene = true;
                    continue;
                }

                if (defName.Equals("Eyes_Gray", StringComparison.OrdinalIgnoreCase))
                {
                    hasGrayEyesGene = true;
                    continue;
                }

                hasUnsupportedEyeGene = true;
            }
        }

        List<Hediff> hediffs = pawn?.health?.hediffSet?.hediffs;
        if (hediffs != null)
        {
            for (int i = 0; i < hediffs.Count; i++)
            {
                Hediff hediff = hediffs[i];
                string defName = hediff?.def?.defName;
                if (!defName.NullOrEmpty() && defName.Equals("VoidTouched", StringComparison.OrdinalIgnoreCase))
                {
                    hasVoidTouched = true;
                    signature = (signature * 31) + StringComparer.OrdinalIgnoreCase.GetHashCode(defName);
                }

                if (!CouldHediffProduceForeignEyeVisual(hediff))
                    continue;

                hasPotentialForeignEyeHealthVisual = true;
                signature = (signature * 31) + GetForeignEyeHediffSignatureContribution(hediff);
            }
        }

        if (hasUnsupportedEyeGene)
            signature = (signature * 31) + 1;

        if (hasRedEyesGene)
            signature = (signature * 31) + 2;

        if (hasGrayEyesGene)
            signature = (signature * 31) + 3;

        if (hasVoidTouched)
            signature = (signature * 31) + 4;

        if (hasPotentialForeignEyeHealthVisual)
            signature = (signature * 31) + 5;

        return signature;
    }

    private bool HasPotentialForeignEyeVisualSourceThisTick()
    {
        return cachedShouldSuppressForeignEyeGeneGraphics || cachedHasPotentialForeignEyeHealthVisual;
    }

    private void RefreshCachedSpecialFaceStateThisTick()
    {
        int currentTick = Find.TickManager?.TicksGame ?? -1;
        if (currentTick >= 0 && lastSpecialFaceStateCheckTick == currentTick)
            return;

        if (currentTick >= 0)
            lastSpecialFaceStateCheckTick = currentTick;

        cachedHasVoidTouched = false;
        cachedHasBabyCryMentalState = false;

        List<Hediff> hediffs = pawn?.health?.hediffSet?.hediffs;
        if (hediffs != null)
        {
            for (int i = 0; i < hediffs.Count; i++)
            {
                string defName = hediffs[i]?.def?.defName;
                if (!defName.NullOrEmpty() && defName.Equals("VoidTouched", StringComparison.OrdinalIgnoreCase))
                {
                    cachedHasVoidTouched = true;
                    break;
                }
            }
        }

        MentalState mentalState = pawn?.mindState?.mentalStateHandler?.CurState;
        string mentalStateDefName = mentalState?.def?.defName;
        cachedHasBabyCryMentalState = (!mentalStateDefName.NullOrEmpty() && mentalStateDefName.Equals("BabyCry", StringComparison.OrdinalIgnoreCase))
            || mentalState is MentalState_BabyCry;

        if (currentTick < 0)
            lastSpecialFaceStateCheckTick = int.MinValue;
    }

    private bool HasVoidTouchedThisTick()
    {
        RefreshCachedSpecialFaceStateThisTick();
        return cachedHasVoidTouched;
    }

    private string GetSpecialEyeDetailStateTexturePathThisTick()
    {
        RefreshCachedSpecialFaceStateThisTick();

        if (cachedHasBabyCryMentalState)
            return TEARS_DETAIL_TEX_PATH;

        if (cachedHasVoidTouched)
            return DARK_CIRCLES_DETAIL_TEX_PATH;

        return null;
    }

    private bool HasBabyCryMentalStateThisTick()
    {
        RefreshCachedSpecialFaceStateThisTick();
        return cachedHasBabyCryMentalState;
    }

    public Color GetResolvedEyeTintThisTick()
    {
        RefreshCachedEyeVisualStateThisTick();
        return cachedResolvedEyeTint;
    }

    public bool HasBlockingEyeGeneThisTick()
    {
        RefreshCachedEyeVisualStateThisTick();
        return cachedHasBlockingEyeGene;
    }

    public bool ShouldSuppressForeignEyeGeneGraphicsThisTick()
    {
        RefreshCachedEyeVisualStateThisTick();
        return cachedShouldSuppressForeignEyeGeneGraphics;
    }

    public bool HasBlockingForeignEyeVisualThisTick()
    {
        RefreshCachedForeignEyeVisualStateThisTick();
        return cachedForeignEyeBlockMask != ForeignEyeBlockMask.None;
    }

    public bool IsForeignEyeVisualBlockedForNodeThisTick(PawnRenderNode node, Rot4 facing)
    {
        if (node == null)
            return false;

        RefreshCachedForeignEyeVisualStateThisTick();

        if (!FacePartRenderNodeContextCache.IsEyeFacingNode(node))
            return false;

        ForeignEyeBlockMask requestedMask = ResolveFacePartEyeSideMask(node, facing);
        if (requestedMask == ForeignEyeBlockMask.None)
            return false;

        return (cachedForeignEyeBlockMask & requestedMask) != 0;
    }

    public bool IsForeignEyeVisualBlockedForLabelThisTick(string debugLabel, Rot4 facing)
    {
        RefreshCachedForeignEyeVisualStateThisTick();

        ForeignEyeBlockMask requestedMask = ResolveFacePartEyeSideMask(debugLabel, facing);
        if (requestedMask == ForeignEyeBlockMask.None)
            return false;

        return (cachedForeignEyeBlockMask & requestedMask) != 0;
    }

    private void RefreshCachedForeignEyeVisualStateThisTick()
    {
        RefreshCachedEyeVisualStateThisTick();

        int currentEyeInputSignature = cachedEyeVisualInputSignature;
        PawnRenderTree renderTree = pawn?.Drawer?.renderer?.renderTree;
        PawnRenderNode rootNode = renderTree?.rootNode;
        int currentRootSignature = rootNode == null ? 0 : RuntimeHelpers.GetHashCode(rootNode);

        if (!foreignEyeBlockMaskDirty
            && cachedForeignEyeVisualInputSignature == currentEyeInputSignature
            && cachedForeignEyeVisualRootSignature == currentRootSignature)
        {
            return;
        }

        lastForeignEyeVisualCheckTick = Find.TickManager?.TicksGame ?? -1;
        cachedForeignEyeVisualInputSignature = currentEyeInputSignature;
        cachedForeignEyeVisualRootSignature = currentRootSignature;
        cachedForeignEyeBlockMask = ForeignEyeBlockMask.None;
        foreignEyeBlockMaskDirty = false;

        if (rootNode == null || !HasPotentialForeignEyeVisualSourceThisTick())
            return;

        HashSet<PawnRenderNode> visited = foreignEyeVisitedPool;
        visited.Clear();
        cachedForeignEyeBlockMask = GetBlockingForeignEyeVisualMask(rootNode, visited);
    }

    private ForeignEyeBlockMask GetBlockingForeignEyeVisualMask(PawnRenderNode node, HashSet<PawnRenderNode> visited)
    {
        if (node == null || visited == null || !visited.Add(node))
            return ForeignEyeBlockMask.None;

        ForeignEyeBlockMask mask = GetBlockingForeignEyeVisualNodeMask(node);
        if (mask == ForeignEyeBlockMask.Both)
            return mask;

        PawnRenderNode[] children = node.children;
        if (children == null)
            return mask;

        for (int i = 0; i < children.Length; i++)
        {
            mask |= GetBlockingForeignEyeVisualMask(children[i], visited);
            if (mask == ForeignEyeBlockMask.Both)
                break;
        }

        return mask;
    }

    private ForeignEyeBlockMask GetBlockingForeignEyeVisualNodeMask(PawnRenderNode node)
    {
        if (!IsBlockingForeignEyeVisualNode(node))
            return ForeignEyeBlockMask.None;

        return InferForeignEyeBlockMask(node);
    }

    private bool IsBlockingForeignEyeVisualNode(PawnRenderNode node)
    {
        if (node == null)
            return false;

        if (node.tree?.pawn != pawn)
            return false;

        if (node is PawnRenderNode_EyeAddon)
            return false;

        string nodeNamespace = node.GetType().Namespace ?? string.Empty;
        if (nodeNamespace.StartsWith("Despicable", StringComparison.Ordinal))
            return false;

        if (ShouldSuppressForeignEyeGeneGraphicsThisTick()
            && HarmonyPatch_ForeignEyeGeneGraphics.MatchesSupportedEyeGeneNode(node))
        {
            return false;
        }

        Type nodeType = node.GetType();
        Type workerType = node.Worker?.GetType();
        Type propsType = node.Props?.GetType();

        if (MatchesForeignEyeVisualTypeName(nodeType?.Name)
            || MatchesForeignEyeVisualTypeName(workerType?.Name)
            || MatchesForeignEyeVisualTypeName(propsType?.Name))
        {
            return true;
        }

        return MatchesForeignEyeVisualText(node.Props?.debugLabel)
            || MatchesForeignEyeVisualText(node.Props?.texPath);
    }

    private ForeignEyeBlockMask InferForeignEyeBlockMask(PawnRenderNode node)
    {
        ForeignEyeBlockMask mask = TryInferEyeSideMaskFromText(node?.Props?.debugLabel);
        if (mask != ForeignEyeBlockMask.None)
            return mask;

        mask = TryInferEyeSideMaskFromText(node?.Props?.texPath);
        if (mask != ForeignEyeBlockMask.None)
            return mask;

        Type nodeType = node?.GetType();
        Type workerType = node?.Worker?.GetType();
        Type propsType = node?.Props?.GetType();

        mask = TryInferEyeSideMaskFromText(nodeType?.Name)
            | TryInferEyeSideMaskFromText(workerType?.Name)
            | TryInferEyeSideMaskFromText(propsType?.Name);
        if (mask != ForeignEyeBlockMask.None)
            return NormalizeForeignEyeBlockMask(mask);

        mask = TryInferEyeSideMaskFromObject(node, 0);
        if (mask != ForeignEyeBlockMask.None)
            return NormalizeForeignEyeBlockMask(mask);

        mask = TryInferEyeSideMaskFromObject(node?.Props, 0);
        if (mask != ForeignEyeBlockMask.None)
            return NormalizeForeignEyeBlockMask(mask);

        return ForeignEyeBlockMask.Both;
    }

    private static ForeignEyeBlockMask ResolveFacePartEyeSideMask(PawnRenderNode node, Rot4 facing)
    {
        if (node == null || !FacePartRenderNodeContextCache.IsEyeFacingNode(node))
            return ForeignEyeBlockMask.None;

        bool isRightLabel = FacePartRenderNodeContextCache.IsRightCounterpartNode(node);
        bool isLeftLabel = FacePartRenderNodeContextCache.IsLeftCounterpartNode(node);

        if (facing == Rot4.South)
        {
            if (isRightLabel)
                return ForeignEyeBlockMask.Left;
            if (isLeftLabel)
                return ForeignEyeBlockMask.Right;
            return ForeignEyeBlockMask.Both;
        }

        if (facing == Rot4.East)
            return isRightLabel ? ForeignEyeBlockMask.None : ForeignEyeBlockMask.Right;

        if (facing == Rot4.West)
            return isRightLabel ? ForeignEyeBlockMask.None : ForeignEyeBlockMask.Left;

        return ForeignEyeBlockMask.None;
    }

    private static ForeignEyeBlockMask ResolveFacePartEyeSideMask(string debugLabel, Rot4 facing)
    {
        if (debugLabel.NullOrEmpty())
            return ForeignEyeBlockMask.None;

        bool isEyeFacingNode = debugLabel.Equals("FacePart_Eye_L", StringComparison.OrdinalIgnoreCase)
            || debugLabel.Equals("FacePart_Eye_R", StringComparison.OrdinalIgnoreCase)
            || debugLabel.Equals("FacePart_EyeBase_L", StringComparison.OrdinalIgnoreCase)
            || debugLabel.Equals("FacePart_EyeBase_R", StringComparison.OrdinalIgnoreCase)
            || debugLabel.Equals("FacePart_EyeDetail_L", StringComparison.OrdinalIgnoreCase)
            || debugLabel.Equals("FacePart_EyeDetail_R", StringComparison.OrdinalIgnoreCase)
            || debugLabel.Equals("FacePart_AutoEyePatch_L", StringComparison.OrdinalIgnoreCase)
            || debugLabel.Equals("FacePart_AutoEyePatch_R", StringComparison.OrdinalIgnoreCase);
        if (!isEyeFacingNode)
            return ForeignEyeBlockMask.None;

        bool isRightLabel = debugLabel.EndsWith("_R", StringComparison.OrdinalIgnoreCase);
        bool isLeftLabel = debugLabel.EndsWith("_L", StringComparison.OrdinalIgnoreCase);

        if (facing == Rot4.South)
        {
            // South-facing face-part labels are laid out in viewer space, while foreign eye visuals are inferred
            // in anatomical pawn space. Swap the mapping here so a left-eye implant blocks the face-part node
            // drawn over the pawn's anatomical left eye instead of the opposite on-screen half.
            if (isRightLabel)
                return ForeignEyeBlockMask.Left;
            if (isLeftLabel)
                return ForeignEyeBlockMask.Right;
            return ForeignEyeBlockMask.Both;
        }

        if (facing == Rot4.East)
            return isRightLabel ? ForeignEyeBlockMask.None : ForeignEyeBlockMask.Right;

        if (facing == Rot4.West)
            return isRightLabel ? ForeignEyeBlockMask.None : ForeignEyeBlockMask.Left;

        return ForeignEyeBlockMask.None;
    }

    private static ForeignEyeBlockMask TryInferEyeSideMaskFromObject(object source, int depth)
    {
        if (source == null || depth > 2)
            return ForeignEyeBlockMask.None;

        if (source is string text)
            return TryInferEyeSideMaskFromText(text);

        Type type = source.GetType();
        ForeignEyeBlockMask mask = TryInferEyeSideMaskFromText(type.Name);
        if (mask != ForeignEyeBlockMask.None)
            return NormalizeForeignEyeBlockMask(mask);

        if (!(source is ValueType))
        {
            string sourceText = source.ToString();
            mask = TryInferEyeSideMaskFromText(sourceText);
            if (mask != ForeignEyeBlockMask.None)
                return NormalizeForeignEyeBlockMask(mask);
        }

        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        FieldInfo[] fields = type.GetFields(Flags);
        for (int i = 0; i < fields.Length; i++)
        {
            FieldInfo field = fields[i];
            if (!LooksLikeEyeSideMetadata(field?.Name))
                continue;

            object value;
            try
            {
                value = field.GetValue(source);
            }
            catch
            {
                continue;
            }

            mask |= TryInferEyeSideMaskFromMemberValue(value, depth + 1);
            if (mask == ForeignEyeBlockMask.Both)
                return mask;
        }

        PropertyInfo[] properties = type.GetProperties(Flags);
        for (int i = 0; i < properties.Length; i++)
        {
            PropertyInfo property = properties[i];
            if (property == null || property.GetIndexParameters().Length != 0 || !property.CanRead || !LooksLikeEyeSideMetadata(property.Name))
                continue;

            object value;
            try
            {
                value = property.GetValue(source, null);
            }
            catch
            {
                continue;
            }

            mask |= TryInferEyeSideMaskFromMemberValue(value, depth + 1);
            if (mask == ForeignEyeBlockMask.Both)
                return mask;
        }

        return NormalizeForeignEyeBlockMask(mask);
    }

    private static ForeignEyeBlockMask TryInferEyeSideMaskFromMemberValue(object value, int depth)
    {
        if (value == null)
            return ForeignEyeBlockMask.None;

        if (value is string text)
            return TryInferEyeSideMaskFromText(text);

        if (value is Enum enumValue)
            return TryInferEyeSideMaskFromText(enumValue.ToString());

        Type type = value.GetType();
        if (type.IsPrimitive)
            return ForeignEyeBlockMask.None;

        return TryInferEyeSideMaskFromObject(value, depth);
    }

    private static bool LooksLikeEyeSideMetadata(string memberName)
    {
        if (memberName.NullOrEmpty())
            return false;

        return memberName.IndexOf("eye", StringComparison.OrdinalIgnoreCase) >= 0
            || memberName.IndexOf("part", StringComparison.OrdinalIgnoreCase) >= 0
            || memberName.IndexOf("side", StringComparison.OrdinalIgnoreCase) >= 0
            || memberName.IndexOf("hediff", StringComparison.OrdinalIgnoreCase) >= 0
            || memberName.IndexOf("label", StringComparison.OrdinalIgnoreCase) >= 0
            || memberName.IndexOf("path", StringComparison.OrdinalIgnoreCase) >= 0
            || memberName.IndexOf("def", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static ForeignEyeBlockMask TryInferEyeSideMaskFromText(string text)
    {
        if (text.NullOrEmpty())
            return ForeignEyeBlockMask.None;

        string normalized = NormalizeEyeSideSearchText(text);
        if (normalized.NullOrEmpty())
            return ForeignEyeBlockMask.None;

        string[] tokens = normalized.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
            return ForeignEyeBlockMask.None;

        bool eyeContext = text.IndexOf("eye", StringComparison.OrdinalIgnoreCase) >= 0;
        for (int i = 0; i < tokens.Length && !eyeContext; i++)
        {
            string token = tokens[i];
            if (token.Equals("eye", StringComparison.Ordinal)
                || token.Equals("eyes", StringComparison.Ordinal)
                || token.Equals("eyecover", StringComparison.Ordinal)
                || token.Equals("missingeye", StringComparison.Ordinal)
                || token.Equals("hediffeye", StringComparison.Ordinal)
                || token.Equals("eyebase", StringComparison.Ordinal)
                || token.Equals("eyedetail", StringComparison.Ordinal)
                || token.Equals("autoeyepatch", StringComparison.Ordinal))
            {
                eyeContext = true;
            }
        }

        bool left = false;
        bool right = false;
        for (int i = 0; i < tokens.Length; i++)
        {
            string token = tokens[i];
            if (token.Equals("left", StringComparison.Ordinal)
                || token.Equals("lefteye", StringComparison.Ordinal)
                || token.Equals("leftside", StringComparison.Ordinal)
                || (eyeContext && token.Equals("l", StringComparison.Ordinal)))
            {
                left = true;
            }

            if (token.Equals("right", StringComparison.Ordinal)
                || token.Equals("righteye", StringComparison.Ordinal)
                || token.Equals("rightside", StringComparison.Ordinal)
                || (eyeContext && token.Equals("r", StringComparison.Ordinal)))
            {
                right = true;
            }
        }

        if (left && right)
            return ForeignEyeBlockMask.Both;
        if (left)
            return ForeignEyeBlockMask.Left;
        if (right)
            return ForeignEyeBlockMask.Right;
        return ForeignEyeBlockMask.None;
    }

    private static string NormalizeEyeSideSearchText(string text)
    {
        if (text.NullOrEmpty())
            return string.Empty;

        System.Text.StringBuilder builder = new(text.Length * 2);
        char previous = '\0';
        for (int i = 0; i < text.Length; i++)
        {
            char current = text[i];
            bool splitCamel = i > 0 && char.IsUpper(current) && (char.IsLower(previous) || char.IsDigit(previous));
            if (splitCamel)
                builder.Append(' ');

            if (char.IsLetterOrDigit(current))
                builder.Append(char.ToLowerInvariant(current));
            else
                builder.Append(' ');

            previous = current;
        }

        return builder.ToString();
    }


    private static bool CouldHediffProduceForeignEyeVisual(Hediff hediff)
    {
        if (hediff == null)
            return false;

        string defName = hediff.def?.defName;
        if (MatchesForeignEyeVisualText(defName) || MatchesForeignEyeVisualTypeName(hediff.GetType().Name))
            return true;

        BodyPartRecord part = hediff.Part;
        return MatchesForeignEyeVisualText(part?.def?.defName)
            || MatchesForeignEyeVisualText(part?.Label)
            || MatchesForeignEyeVisualText(hediff.LabelBase);
    }

    private static int GetForeignEyeHediffSignatureContribution(Hediff hediff)
    {
        unchecked
        {
            int signature = 17;
            signature = (signature * 31) + StringComparer.OrdinalIgnoreCase.GetHashCode(hediff?.def?.defName ?? string.Empty);
            signature = (signature * 31) + StringComparer.OrdinalIgnoreCase.GetHashCode(hediff?.Part?.def?.defName ?? string.Empty);
            signature = (signature * 31) + StringComparer.OrdinalIgnoreCase.GetHashCode(hediff?.Part?.Label ?? string.Empty);
            signature = (signature * 31) + StringComparer.OrdinalIgnoreCase.GetHashCode(hediff?.LabelBase ?? string.Empty);
            return signature;
        }
    }
    private static ForeignEyeBlockMask NormalizeForeignEyeBlockMask(ForeignEyeBlockMask mask)
    {
        if ((mask & ForeignEyeBlockMask.Both) == ForeignEyeBlockMask.Both)
            return ForeignEyeBlockMask.Both;
        if ((mask & ForeignEyeBlockMask.Left) != 0)
            return ForeignEyeBlockMask.Left;
        if ((mask & ForeignEyeBlockMask.Right) != 0)
            return ForeignEyeBlockMask.Right;
        return ForeignEyeBlockMask.None;
    }

    private static bool MatchesForeignEyeVisualTypeName(string typeName)
    {
        if (typeName.NullOrEmpty())
            return false;

        return typeName.IndexOf("HediffEye", StringComparison.OrdinalIgnoreCase) >= 0
            || typeName.IndexOf("EyeCover", StringComparison.OrdinalIgnoreCase) >= 0
            || typeName.IndexOf("Blind", StringComparison.OrdinalIgnoreCase) >= 0
            || typeName.Equals("PawnRenderNodeWorker_Eye", StringComparison.OrdinalIgnoreCase)
            || typeName.Equals("PawnRenderNodeProperties_Eye", StringComparison.OrdinalIgnoreCase)
            || typeName.Equals("PawnRenderNode_Eye", StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesForeignEyeVisualText(string text)
    {
        if (text.NullOrEmpty())
            return false;

        return text.IndexOf("EyeCover", StringComparison.OrdinalIgnoreCase) >= 0
            || text.IndexOf("blind", StringComparison.OrdinalIgnoreCase) >= 0
            || text.IndexOf("missingeye", StringComparison.OrdinalIgnoreCase) >= 0
            || text.IndexOf("missing_eye", StringComparison.OrdinalIgnoreCase) >= 0
            || text.IndexOf("hediffeye", StringComparison.OrdinalIgnoreCase) >= 0
            || text.IndexOf("eyes/", StringComparison.OrdinalIgnoreCase) >= 0
            || text.IndexOf("/eye", StringComparison.OrdinalIgnoreCase) >= 0
            || text.IndexOf("eye_", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static bool IsSupportedEyeGeneDefName(string defName)
    {
        return !defName.NullOrEmpty()
            && (defName.Equals("Eyes_Red", StringComparison.OrdinalIgnoreCase)
                || defName.Equals("Eyes_Gray", StringComparison.OrdinalIgnoreCase));
    }

    private void InvalidateBlockingNoseGeneCache()
    {
        cachedHasBlockingNoseGene = false;
        cachedHasBlockingNoseGeneValid = false;
    }

    private void RefreshBlockingNoseGeneCache()
    {
        cachedHasBlockingNoseGene = false;
        cachedHasBlockingNoseGeneValid = true;

        List<Gene> genes = pawn?.genes?.GenesListForReading;
        if (genes == null)
            return;

        for (int i = 0; i < genes.Count; i++)
        {
            string defName = genes[i]?.def?.defName;
            if (!defName.NullOrEmpty() && defName.StartsWith("nose_", StringComparison.OrdinalIgnoreCase))
            {
                cachedHasBlockingNoseGene = true;
                return;
            }
        }
    }

    public bool HasBlockingNoseGeneThisTick()
    {
        if (!cachedHasBlockingNoseGeneValid)
            RefreshBlockingNoseGeneCache();

        return cachedHasBlockingNoseGene;
    }

    private void ResolveBaseVisualState(FaceExpressionState state, out ExpressionDef resolvedBaseExpression, out string resolvedBaseDetailTexPath)
    {
        resolvedBaseExpression = null;
        resolvedBaseDetailTexPath = "FaceParts/Details/detail_empty";

        if (!state.HasHead)
            return;

        if (state.Asleep)
        {
            resolvedBaseExpression = FacePartsModule_ExpressionDefOf.FacialExpression_EyesClosed;
            return;
        }

        if (state.Berserk)
        {
            resolvedBaseExpression = FacePartsModule_ExpressionDefOf.FacialExpression_Berserk;
            return;
        }

        if (state.MentalBreak)
        {
            resolvedBaseExpression = FacePartsModule_ExpressionDefOf.FacialExpression_Distressed;
            return;
        }

        if (state.Infant)
        {
            resolvedBaseExpression = FacePartsModule_ExpressionDefOf.FacialExpression_Infant;
            return;
        }

        if (state.Drunk)
        {
            resolvedBaseExpression = FacePartsModule_ExpressionDefOf.FacialExpression_Drunk;
            resolvedBaseDetailTexPath = EMPTY_DETAIL_TEX_PATH;
            return;
        }

        if (state.Tired)
        {
            resolvedBaseExpression = FacePartsModule_ExpressionDefOf.FacialExpression_Tired;
            resolvedBaseDetailTexPath = EMPTY_DETAIL_TEX_PATH;
            return;
        }

        if (state.Drafted)
            resolvedBaseExpression = FacePartsModule_ExpressionDefOf.FacialExpression_Drafted;
    }

    private bool RefreshCachedVisualState(bool markDirtyWhenVisible)
    {
        if (!enabled || pawn == null)
            return false;

        EnsureExpressionStateSeeded();
        return RefreshCachedVisualState(cachedExpressionState, markDirtyWhenVisible);
    }

    private bool RefreshCachedVisualState(FaceExpressionState state, bool markDirtyWhenVisible)
    {
        if (!enabled || pawn == null)
            return false;

        int signature = ComputeVisualStateSignature(state);
        if (cachedVisualStateValid && signature == lastVisualStateSignature)
            return false;

        ResolveBaseVisualState(state, out ExpressionDef resolvedBaseExpression, out string resolvedBaseDetailTexPath);

        bool changed = !AreExpressionsEquivalent(baseExpression, resolvedBaseExpression)
            || baseDetailTexPath != resolvedBaseDetailTexPath;

        cachedExpressionState = state;
        cachedExpressionStateInitialized = true;
        baseExpression = resolvedBaseExpression;
        baseDetailTexPath = resolvedBaseDetailTexPath;
        cachedVisualStateValid = true;
        lastVisualStateSignature = signature;

        if (changed && markDirtyWhenVisible)
            shouldUpdate = true;

        return changed;
    }

    private bool CanWarmPortraitFastPath()
    {
        if (pawn == null || ModMain.IsNlFacialInstalled)
            return false;

        RefreshEnabledFromSettings();
        if (!enabled)
            return false;

        if (pawn.RaceProps?.Humanlike != true)
            return false;

        if (pawn.story == null)
            return false;

        if (!IsCurrentHeadEligible())
            return false;

        if (pawn.health?.hediffSet == null)
            return false;

        if (pawn.Drawer?.renderer == null)
            return false;

        return true;
    }

    public bool NeedsPortraitWarmup()
    {
        if (!CanWarmPortraitFastPath())
            return false;

        return !faceWarmInitialized
            || faceStructureDirty
            || !AreStyleSlotsAssigned()
            || !cachedVisualStateValid;
    }

    public bool TryWarmPortraitFastThisTick(bool markPortraitDirty = false)
    {
        int currentTick = Find.TickManager?.TicksGame ?? -1;
        if (lastPortraitWarmupAttemptTick == currentTick)
            return false;

        lastPortraitWarmupAttemptTick = currentTick;
        WarmPortraitFast(markPortraitDirty);
        return true;
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref ticks, "ticks", 0);
        Scribe_Values.Look(ref blinkTick, "blinkTick");
        Scribe_Values.Look(ref nextBlinkGameTick, "nextBlinkGameTick", -1);
        Scribe_Values.Look(ref genderPath, "genderPath", DEFAULT_GENDER_PATH);
        Scribe_Defs.Look(ref eyeStyleDef, "eyeStyleDef");
        Scribe_Defs.Look(ref browStyleDef, "browStyleDef");
        Scribe_Defs.Look(ref mouthStyleDef, "mouthStyleDef");
        Scribe_Defs.Look(ref eyeDetailStyleDef, "eyeDetailStyleDef");
        Scribe_Values.Look(ref eyeDetailSideMode, "eyeDetailSideMode", FacePartSideMode.LeftOnly);
        Scribe_Defs.Look(ref faceDetailStyleDef, "faceDetailStyleDef");
        Scribe_Deep.Look(ref baseExpression, "baseExpression");
        Scribe_Values.Look(ref baseDetailTexPath, "baseDetailTexPath", EMPTY_DETAIL_TEX_PATH);
        Scribe_Deep.Look(ref animExpression, "animExpression");
        Scribe_Defs.Look(ref facialAnim, "facialAnim");
        Scribe_Values.Look(ref animTicks, "animTicks", 0);
        Scribe_Values.Look(ref facialAnimStartGameTick, "facialAnimStartGameTick", -1);

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            faceDetailStyleDef = null;
            if (facialAnim != null && facialAnimStartGameTick < 0)
                facialAnimStartGameTick = Math.Max(0, CurrentGameTick - Math.Max(animTicks, 0));
            SanitizeRetiredEyeDetailStyle();
            eyeDetailSideMode = eyeDetailSideMode == FacePartSideMode.RightOnly ? FacePartSideMode.RightOnly : FacePartSideMode.LeftOnly;
            InvalidateFaceStructure();
        }
    }

    private Pawn pawn => parent as Pawn;

    public static ExpressionDef SampleFacialExpressionAt(FacialAnimDef anim, int localTick)
    {
        if (localTick < 0)
            return null;

        FacialAnimSampleCache cache = GetOrBuildFacialAnimSampleCache(anim);
        if (cache == null || cache.Ticks == null || cache.Frames == null || cache.Ticks.Length == 0)
            return null;

        int low = 0;
        int high = cache.Ticks.Length - 1;
        int resultIndex = -1;
        while (low <= high)
        {
            int mid = low + ((high - low) / 2);
            if (cache.Ticks[mid] <= localTick)
            {
                resultIndex = mid;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        if (resultIndex < 0)
            return null;

        return cache.Frames[resultIndex];
    }

    private static bool AreExpressionsEquivalent(ExpressionDef a, ExpressionDef b)
    {
        if (ReferenceEquals(a, b))
            return true;
        if (a == null || b == null)
            return false;

        return a.texPathEyes == b.texPathEyes
            && a.texPathBrows == b.texPathBrows
            && a.browVariant == b.browVariant
            && a.texPathMouth == b.texPathMouth
            && a.texPathDetail == b.texPathDetail
            && a.texPathEyeDetailState == b.texPathEyeDetailState
            && a.texPathFaceDetailState == b.texPathFaceDetailState
            && a.eyesOffset == b.eyesOffset
            && a.browsOffset == b.browsOffset
            && a.mouthOffset == b.mouthOffset
            && a.detailOffset == b.detailOffset
            && a.eyeDetailOffset == b.eyeDetailOffset
            && a.faceDetailOffset == b.faceDetailOffset;
    }

    public void ApplyPreviewFacialAt(FacialAnimDef anim, int localTick)
    {
        if (ModMain.IsNlFacialInstalled || pawn == null)
            return;

        ExpressionDef sampled = SampleFacialExpressionAt(anim, localTick);
        if (AreExpressionsEquivalent(animExpression, sampled))
            return;

        facialAnim = null;
        animTicks = 0;
        facialAnimStartGameTick = -1;
        curKeyframe = 0;
        animExpression = sampled;

        // SetAllGraphicsDirty is required here even though PawnRenderNodeWorker_FacePart.OffsetFor
        // reads animExpression directly (no cache). The *texture* is a different story: GraphicFor on
        // PawnRenderNode_Mouth and PawnRenderNode_EyeAddon IS cached behind requestRecache. Without
        // dirtying the tree the face sprite never updates during live playback — only at stage
        // transitions (where ApplyStageAnimations → SetAnimation already triggers a tree dirty).
        // This call happens before st.Renderer.Render() in RenderViewportSlots, so AppendRequests
        // sees requestRecache=true and rebuilds GraphicFor in the same render pass. No frame lag.
        RequestFaceGraphicsDirty(pawn.Drawer?.renderer, bypassSameTickDebounce: true);
    }

    public void ClearPreviewFacialOverride()
    {
        if (ModMain.IsNlFacialInstalled || pawn == null || animExpression == null)
            return;

        facialAnim = null;
        animTicks = 0;
        facialAnimStartGameTick = -1;
        curKeyframe = 0;
        animExpression = null;

        // Same reasoning as ApplyPreviewFacialAt: dirty the tree so face nodes rebuild
        // GraphicFor back to the base/style expression on the next render pass.
        RequestFaceGraphicsDirty(pawn.Drawer?.renderer, bypassSameTickDebounce: true);
    }

    public void PlayFacialAnim(FacialAnimDef anim)
    {
        if (ModMain.IsNlFacialInstalled)
            return;
        EnsureRuntimeRegistered();
        if (anim == null)
            return;
        if (pawn == null)
            return;
        if (!IsRenderActiveNow())
            return;
        // For things that shouldn't animate faces
        if (pawn.Dead)
            return;
        if (pawn?.pather == null)
            return;
        if (pawn?.pather?.debugDisabled == true)
            return;

        facialAnim = anim;
        facialAnimStartGameTick = CurrentGameTick;
        curKeyframe = 0;
        animTicks = 0;
        WakeCompTickNow();
    }

    public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
    {
        FaceRuntimeActivityManager.UnregisterComp(this);
        registeredRuntimeGeneration = int.MinValue;
        base.PostDeSpawn(map, mode);
    }

    public override void PostDestroy(DestroyMode mode, Map previousMap)
    {
        FaceRuntimeActivityManager.UnregisterComp(this);
        registeredRuntimeGeneration = int.MinValue;
        base.PostDestroy(mode, previousMap);
    }

}


internal static class FacePartsPortraitRenderContext
{
    [ThreadStatic]
    private static int _nonWorkshopPortraitDepth;

    public static bool NonWorkshopPortraitActive => _nonWorkshopPortraitDepth > 0 && !WorkshopRenderContext.Active;

    public readonly struct Scope : IDisposable
    {
        private readonly bool _entered;

        public Scope(bool active)
        {
            _entered = active;
            if (_entered)
                _nonWorkshopPortraitDepth++;
        }

        public void Dispose()
        {
            if (_entered && _nonWorkshopPortraitDepth > 0)
                _nonWorkshopPortraitDepth--;
        }
    }
}
