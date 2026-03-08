using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Despicable;

namespace Despicable.AnimGroupStudio.Preview;
/// <summary>
/// Live preview harness for Anim Group Studio.
///
/// Preview design goal:
/// - Behave like the old Animation Workshop preview: sampling-based.
/// - Do NOT rely on CompExtendedAnimator state/queues for preview playback.
/// - We drive WorkshopRenderContext.Tick and switch the currently sampled AnimationDef
///   when a stage duration elapses.
///
/// This session can preview:
/// - Existing AnimGroupDefs directly.
/// - Runtime-authored stage data (compiled temp AnimationDefs) via ConfigureForRuntime().
/// </summary>
public sealed partial class AgsPreviewSession : IDisposable
{
    public sealed partial class RuntimeRole
    {
        public string Key;
        public string Label;
        public int Gender;
        public string BodyTypeDefName;
    }

    public sealed partial class RuntimeStage
    {
        public int DurationTicks;
        public int RepeatCount = 1;
        public List<AnimationDef> AnimationsByRole = new();
    }

    private sealed class SlotState
    {
        public int RoleIndex;
        public string Key;
        public string Label;
        public Pawn Pawn;
        public WorkshopPreviewRenderer Renderer;
        public AnimRoleDef RoleDef;
        public List<AnimationDef> RuntimeAnimationsByStage;

        // Playback data (sampling-based):
        public List<AnimationDef> Queue;
    }

    private readonly AgsPreviewPawnPool pawnPool;
    private readonly List<SlotState> slots = new();

    private AnimGroupDef currentGroup;
    private string runtimeSourceName;
    private List<RuntimeStage> runtimeStages;
    private bool useRuntimeSource;
    private int stageCount;

    private int selectedStageIndex;
    private bool isPlaying;
    private bool hasPlayback;
    private bool loopCurrentStage;
    private bool loopSequence;
    private float speed = 1f;
    private float tickAccumulator;

    // Playback scheduler
    private int playStartStage;
    private int schedulerTick;
    private int playEndStage;
    private int currentStage;
    private int stageStartTick;
    private int stageDurationTicks;

    private int stageRepeatTarget = 1;
    private int stageRepeatCount = 1;

    public bool IsPlaying => isPlaying;
    public bool CanResume => !isPlaying && hasPlayback;
    public int CurrentTick => Mathf.Max(0, WorkshopRenderContext.Tick);

    public AnimGroupDef CurrentGroup => currentGroup;
    public int StageCount => stageCount;
    public int SelectedStageIndex { get => selectedStageIndex; set => selectedStageIndex = Mathf.Clamp(value, 0, Mathf.Max(0, stageCount - 1)); }
    public bool LoopCurrentStage { get => loopCurrentStage; set => loopCurrentStage = value; }

    public AgsPreviewSession()
    {
        pawnPool = new AgsPreviewPawnPool();
    }

    public void Dispose()
    {
        ResetSlots();
        try { pawnPool?.Dispose(); } catch (System.Exception e) { Despicable.Core.DebugLogger.WarnExceptionOnce("AgsPreviewSession.EmptyCatch:1", "AGS preview session best-effort step failed.", e); }
    }


}
