using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

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

    private static bool IsFacePartsEnabledInSettings => CommonUtil.GetSettings()?.facialPartsExtensionEnabled ?? false;

    // State variables
    public bool enabled;
    public bool shouldUpdate = false;
    public int ticks = 0;
    public int blinkTick = FacePartsUtil.BlinkInterval + Rand.Range(-FacePartsUtil.BlinkTickVariance, FacePartsUtil.BlinkTickVariance);
    public int nextBlinkGameTick = -1;

    // Style paths
    public string genderPath = DEFAULT_GENDER_PATH;
    public FacePartStyleDef eyeStyleDef = null;
    public FacePartStyleDef mouthStyleDef = null;

    // Animation
    public ExpressionDef baseExpression;
    public ExpressionDef animExpression;
    public FacialAnimDef facialAnim;
    public int curKeyframe = 0;
    public int animTicks = 0;
    public bool faceWarmInitialized = false;
    public bool faceStructureDirty = true;
    public string baseDetailTexPath = "FaceParts/Details/detail_empty";

    private int lastVisualStateSignature = int.MinValue;
    private bool cachedVisualStateValid = false;
    private int lastEyeGeneBlockCheckTick = int.MinValue;
    private bool cachedHasBlockingEyeGene;
    private int lastNoseGeneBlockCheckTick = int.MinValue;
    private bool cachedHasBlockingNoseGene;
    private int lastPortraitWarmupAttemptTick = int.MinValue;
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
        baseDetailTexPath = "FaceParts/Details/detail_empty";
        cachedVisualStateValid = false;
        lastVisualStateSignature = int.MinValue;
        lastEyeGeneBlockCheckTick = int.MinValue;
        cachedHasBlockingEyeGene = false;
        lastNoseGeneBlockCheckTick = int.MinValue;
        cachedHasBlockingNoseGene = false;
        nextBlinkGameTick = -1;
        curKeyframe = 0;
        animTicks = 0;
        cachedEligibleHeadType = null;
        cachedEligibleHeadResult = false;
        cachedEligibleHeadValid = false;
        cachedExpressionState = default(FaceExpressionState);
        cachedExpressionStateInitialized = false;
    }

    private int CurrentGameTick => Find.TickManager?.TicksGame ?? 0;

    private void EnsureBlinkScheduling()
    {
        int currentGameTick = CurrentGameTick;
        if (nextBlinkGameTick < 0)
            ScheduleNextBlink(currentGameTick);
    }

    private void ScheduleNextBlink(int currentGameTick)
    {
        blinkTick = FacePartsUtil.BlinkInterval + Rand.Range(-FacePartsUtil.BlinkTickVariance, FacePartsUtil.BlinkTickVariance);
        if (blinkTick <= 0)
        {
            nextBlinkGameTick = int.MaxValue;
            return;
        }

        nextBlinkGameTick = currentGameTick + blinkTick;
    }


    public override void CompTick()
    {
        if (ModMain.IsNlFacialInstalled)
        {
            bool needsRefresh = enabled || facialAnim != null || animExpression != null;
            DisableFacePartsInstance();
            if (needsRefresh)
                pawn?.Drawer?.renderer?.SetAllGraphicsDirty();
            base.CompTick();
            return;
        }

        // Catch error when ticking to prevent completely breaking
        // mid save
        try
        {
            if (AutoEyePatchRuntime.HasPendingFaceRefresh && AutoEyePatchRuntime.TryConsumePendingFaceRefresh(pawn))
                RefreshFaceHard(true);

            if (enabled)
                EnsureBlinkScheduling();

            if (enabled && pawn?.Spawned == true && pawn.IsHashIntervalTick(17) && NeedsPortraitWarmup())
                WarmPortraitFast(false);

            if (!enabled && facialAnim == null && !shouldUpdate)
            {
                ticks++;
                if (ticks > FacePartsUtil.UpdateTickResetOn)
                    ticks = 0;
                base.CompTick();
                return;
            }

            int currentGameTick = CurrentGameTick;
            bool blinkDue = enabled && facialAnim == null && nextBlinkGameTick >= 0 && currentGameTick >= nextBlinkGameTick;
            FaceExpressionState pendingVisualState = default(FaceExpressionState);
            bool visualStateDue = false;
            if (enabled && FacePartsEventRuntime.TryConsume(pawn, out FacePartsEventMask pendingEvents))
                visualStateDue = TryApplyPendingFaceEvents(pendingEvents, out pendingVisualState);
            else if (enabled && TryGetPendingVisualState(out pendingVisualState))
                visualStateDue = true;

            if (facialAnim == null && !shouldUpdate && !blinkDue && !visualStateDue)
            {
                ticks++;
                if (ticks > FacePartsUtil.UpdateTickResetOn)
                    ticks = 0;
                base.CompTick();
                return;
            }

            if (enabled)
            {
                if (facialAnim != null)
                {
                    ExpressionDef sampled = SampleCurrentFacialExpression();
                    if (!AreExpressionsEquivalent(animExpression, sampled))
                    {
                        animExpression = sampled;
                        shouldUpdate = true;
                    }

                    animTicks++;
                    // If animation finishes, reset
                    if (animTicks > facialAnim.durationTicks)
                    {
                        facialAnim = null;
                        animExpression = null;
                        animTicks = 0;
                        curKeyframe = 0;
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

                    ScheduleNextBlink(currentGameTick);
                }

                if (visualStateDue)
                    RefreshCachedVisualState(pendingVisualState, facialAnim == null && animExpression == null);
            }

            if (shouldUpdate)
            {
                RefreshFaceSoftFast(false);
            }
        }
        catch (Exception e)
        {
            Log.Error($"[Despicable] - Error in CompFaceParts CompTick: {e}");
            DisableFacePartsInstance();
        }

        ticks++;
        if (ticks > FacePartsUtil.UpdateTickResetOn)
            ticks = 0;
        base.CompTick();
    }

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);

        if (ModMain.IsNlFacialInstalled)
            return;

        if (pawn?.RaceProps?.Humanlike != true)
            return;

        RefreshEnabledFromSettings();
        InitializeFacePartState();

        if (enabled && pawn.Drawer?.renderer != null)
            AutoEyePatchRuntime.QueuePendingFaceRefresh(pawn);
    }

    public void RefreshEnabledFromSettings()
    {
        enabled = !ModMain.IsNlFacialInstalled && IsFacePartsEnabledInSettings;
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
        RefreshCachedVisualState(false);
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
            texPathMouth = source.texPathMouth,
            texPathDetail = source.texPathDetail,
            eyesOffset = source.eyesOffset,
            mouthOffset = source.mouthOffset,
            detailOffset = source.detailOffset
        };
    }

    private static void OverlayExpression(ExpressionDef destination, ExpressionDef source)
    {
        if (destination == null || source == null)
            return;

        destination.eyesOffset = source.eyesOffset ?? destination.eyesOffset ?? Vector3.zero;
        destination.mouthOffset = source.mouthOffset ?? destination.mouthOffset ?? Vector3.zero;
        destination.detailOffset = source.detailOffset ?? destination.detailOffset ?? Vector3.zero;

        if (!source.texPathEyes.NullOrEmpty())
            destination.texPathEyes = source.texPathEyes;
        if (!source.texPathMouth.NullOrEmpty())
            destination.texPathMouth = source.texPathMouth;
        if (!source.texPathDetail.NullOrEmpty())
            destination.texPathDetail = source.texPathDetail;
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
    }

    public void InvalidateCachedVisualState()
    {
        cachedVisualStateValid = false;
        lastVisualStateSignature = int.MinValue;
        cachedExpressionStateInitialized = false;
        cachedExpressionState = default(FaceExpressionState);
        baseExpression = null;
        baseDetailTexPath = "FaceParts/Details/detail_empty";
    }

    public string GetBaseDetailTexPath()
    {
        return baseDetailTexPath.NullOrEmpty() ? "FaceParts/Details/detail_empty" : baseDetailTexPath;
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
            structureChanged = true;

        if (structureChanged)
        {
            InvalidateFaceStructure();
            SeedExpressionStateFromPawn();
        }

        state = cachedExpressionState;
        return changed || !cachedVisualStateValid;
    }

    private bool TryGetPendingVisualState(out FaceExpressionState state)
    {
        state = default(FaceExpressionState);
        if (!enabled || pawn == null)
            return false;

        EnsureExpressionStateSeeded();
        state = cachedExpressionState;
        if (!cachedVisualStateValid)
            return true;

        return state.GetSignature() != lastVisualStateSignature;
    }

    private int ComputeVisualStateSignature(FaceExpressionState state)
    {
        return state.GetSignature();
    }

    public bool HasBlockingEyeGeneThisTick()
    {
        int currentTick = Find.TickManager?.TicksGame ?? -1;
        if (lastEyeGeneBlockCheckTick == currentTick)
            return cachedHasBlockingEyeGene;

        lastEyeGeneBlockCheckTick = currentTick;
        cachedHasBlockingEyeGene = false;

        List<Gene> genes = pawn?.genes?.GenesListForReading;
        if (genes == null)
            return false;

        for (int i = 0; i < genes.Count; i++)
        {
            Gene gene = genes[i];
            if (gene?.def?.defName != null && gene.def.defName.StartsWith("eyes_", StringComparison.OrdinalIgnoreCase))
            {
                cachedHasBlockingEyeGene = true;
                break;
            }
        }

        return cachedHasBlockingEyeGene;
    }

    public bool HasBlockingNoseGeneThisTick()
    {
        int currentTick = Find.TickManager?.TicksGame ?? -1;
        if (lastNoseGeneBlockCheckTick == currentTick)
            return cachedHasBlockingNoseGene;

        lastNoseGeneBlockCheckTick = currentTick;
        cachedHasBlockingNoseGene = false;

        List<Gene> genes = pawn?.genes?.GenesListForReading;
        if (genes == null)
            return false;

        for (int i = 0; i < genes.Count; i++)
        {
            Gene gene = genes[i];
            if (gene?.def?.defName != null && gene.def.defName.StartsWith("nose_", StringComparison.OrdinalIgnoreCase))
            {
                cachedHasBlockingNoseGene = true;
                break;
            }
        }

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
            resolvedBaseDetailTexPath = "FaceParts/Details/detail_cheekblush";
            return;
        }

        if (state.Tired)
        {
            resolvedBaseExpression = FacePartsModule_ExpressionDefOf.FacialExpression_Tired;
            resolvedBaseDetailTexPath = "FaceParts/Details/detail_darkcircles";
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
            || mouthStyleDef == null
            || eyeStyleDef == null
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
        Scribe_Defs.Look(ref mouthStyleDef, "mouthStyleDef");
        Scribe_Deep.Look(ref baseExpression, "baseExpression");
        Scribe_Values.Look(ref baseDetailTexPath, "baseDetailTexPath", "FaceParts/Details/detail_empty");
        Scribe_Deep.Look(ref animExpression, "animExpression");
        Scribe_Defs.Look(ref facialAnim, "facialAnim");
        Scribe_Values.Look(ref animTicks, "animTicks", 0);

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
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
            && a.texPathMouth == b.texPathMouth
            && a.texPathDetail == b.texPathDetail
            && a.eyesOffset == b.eyesOffset
            && a.mouthOffset == b.mouthOffset
            && a.detailOffset == b.detailOffset;
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
        curKeyframe = 0;
        animExpression = sampled;

        // SetAllGraphicsDirty is required here even though PawnRenderNodeWorker_FacePart.OffsetFor
        // reads animExpression directly (no cache). The *texture* is a different story: GraphicFor on
        // PawnRenderNode_Mouth and PawnRenderNode_EyeAddon IS cached behind requestRecache. Without
        // dirtying the tree the face sprite never updates during live playback — only at stage
        // transitions (where ApplyStageAnimations → SetAnimation already triggers a tree dirty).
        // This call happens before st.Renderer.Render() in RenderViewportSlots, so AppendRequests
        // sees requestRecache=true and rebuilds GraphicFor in the same render pass. No frame lag.
        pawn.Drawer?.renderer?.SetAllGraphicsDirty();
    }

    public void ClearPreviewFacialOverride()
    {
        if (ModMain.IsNlFacialInstalled || pawn == null || animExpression == null)
            return;

        facialAnim = null;
        animTicks = 0;
        curKeyframe = 0;
        animExpression = null;

        // Same reasoning as ApplyPreviewFacialAt: dirty the tree so face nodes rebuild
        // GraphicFor back to the base/style expression on the next render pass.
        pawn.Drawer?.renderer?.SetAllGraphicsDirty();
    }

    public void PlayFacialAnim(FacialAnimDef anim)
    {
        if (ModMain.IsNlFacialInstalled)
            return;
        if (anim == null)
            return;
        if (pawn == null)
            return;
        // For things that shouldn't animate faces
        if (pawn.Dead)
            return;
        if (pawn?.pather == null)
            return;
        if (pawn?.pather?.debugDisabled == true)
            return;

        facialAnim = anim;
        curKeyframe = 0;
        animTicks = 0;
    }

    public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
    {
        base.PostDeSpawn(map, mode);
    }

    public override void PostDestroy(DestroyMode mode, Map previousMap)
    {
        base.PostDestroy(mode, previousMap);
    }

}
