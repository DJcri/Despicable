using RimWorld;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;
using Verse;
using Despicable.AnimGroupStudio.Preview;
using Despicable.AnimGroupStudio;
using Verse.Sound;
using Despicable.UIFramework;
using Despicable.UIFramework.Controls;
using Despicable.UIFramework.Layout;
using Despicable.AnimModule.AnimGroupStudio.Model;

namespace Despicable.AnimModule.AnimGroupStudio.UI;
/// <summary>
/// Clean-slate Animation Group Studio.
///
/// This pass:
/// - Preview Existing is aligned to existing AnimGroupDef structure where each variation is its own AnimGroupDef.
/// - Author is intentionally a skeleton; we will flesh it out in split tasks.
/// </summary>
public partial class Dialog_AnimGroupStudio : Window
{
    private enum SourceMode
    {
        AuthorProject,
        ExistingDef
    }

    private SourceMode sourceMode = SourceMode.ExistingDef;

    // Author state
    private readonly AgsRepository repo = new();
    private readonly AgsEditorSessionState session = new();
    private readonly AgsAuthorPreviewRuntimeState authorRuntime = new();

    private List<AgsModel.Project> projects { get => session.Projects; set => session.Projects = value; }
    private AgsModel.Project project { get => session.Project; set => session.Project = value; }
    private string authorRoleKey { get => session.AuthorRoleKey; set => session.AuthorRoleKey = value; }
    private int authorStageIndex { get => session.AuthorStageIndex; set => session.AuthorStageIndex = value; }
    private int authorTrackIndex { get => session.AuthorTrackIndex; set => session.AuthorTrackIndex = value; }
    private int authorKeyIndex { get => session.AuthorKeyIndex; set => session.AuthorKeyIndex = value; }
    private Vector2 authorStageScroll { get => session.AuthorStageScroll; set => session.AuthorStageScroll = value; }
    private Vector2 authorTrackScroll { get => session.AuthorTrackScroll; set => session.AuthorTrackScroll = value; }
    private Vector2 authorKeyScroll { get => session.AuthorKeyScroll; set => session.AuthorKeyScroll = value; }
    private Vector2 authorInspectorScroll { get => session.AuthorInspectorScroll; set => session.AuthorInspectorScroll = value; }
    private Vector2 authorLeftScroll { get => session.AuthorLeftScroll; set => session.AuthorLeftScroll = value; }
    private float authorLeftContentHeight { get => session.AuthorLeftContentHeight; set => session.AuthorLeftContentHeight = value; }
    private float authorInspectorContentHeight { get => session.AuthorInspectorContentHeight; set => session.AuthorInspectorContentHeight = value; }
    private int authorRightPaneTab { get => session.AuthorRightPaneTab; set => session.AuthorRightPaneTab = value; }
    private float authorAngleSliderWindowCenter { get => session.AuthorAngleSliderWindowCenter; set => session.AuthorAngleSliderWindowCenter = value; }
    private int authorAngleSliderTrackIndex { get => session.AuthorAngleSliderTrackIndex; set => session.AuthorAngleSliderTrackIndex = value; }
    private int authorAngleSliderKeyTick { get => session.AuthorAngleSliderKeyTick; set => session.AuthorAngleSliderKeyTick = value; }
    private bool authorAngleSliderDragging { get => session.AuthorAngleSliderDragging; set => session.AuthorAngleSliderDragging = value; }

    private UIContext frameworkCtx;

    // Inspector UX state
    private bool authorScaleLock { get => session.AuthorScaleLock; set => session.AuthorScaleLock = value; }
    private AgsModel.Keyframe authorKeyClipboard { get => session.AuthorKeyClipboard; set => session.AuthorKeyClipboard = value; }
    private Dictionary<string, bool> isPropTagCache => session.PropTagCache;

    // Export UX state
    private string lastExportFolder { get => session.LastExportFolder; set => session.LastExportFolder = value; }
    private int lastExportWritten { get => session.LastExportWritten; set => session.LastExportWritten = value; }
    private int lastExportOverwritten { get => session.LastExportOverwritten; set => session.LastExportOverwritten = value; }

    private enum AuthorPlayMode { Stage, Group }
    private AuthorPlayMode authorPlayMode = AuthorPlayMode.Stage;
    private bool authorLoopGroup = true;

    internal sealed class AuthorPreviewSlot
    {
        public string RoleKey;
        public string Label;
        public AgsModel.RoleGenderReq GenderReq;
        public Pawn Pawn;
        public CompExtendedAnimator Animator;
        public WorkshopPreviewRenderer Renderer;
        public AnimationDef CompiledAnim;
        public List<AnimationDef> CompiledByStage;
    }

    private List<AuthorPreviewSlot> authorSlots => authorRuntime.Slots;
    private Dictionary<string, AuthorPreviewSlot> authorSlotsByKey => authorRuntime.SlotsByKey;
    private readonly AgsPreviewPawnPool authorPawnPool;
    private bool authorPreviewPlaying { get => authorRuntime.IsPlaying; set => authorRuntime.IsPlaying = value; }
    private float authorPreviewSpeed { get => authorRuntime.Speed; set => authorRuntime.Speed = value; }
    private float authorPreviewTickAcc { get => authorRuntime.TickAccumulator; set => authorRuntime.TickAccumulator = value; }
    private int authorPreviewTick { get => authorRuntime.CurrentTick; set => authorRuntime.CurrentTick = value; }
    private int authorPreviewSourceHash { get => authorRuntime.SourceHash; set => authorRuntime.SourceHash = value; }

    // Preview Existing state
    private Dictionary<string, AgsModel.ExistingFamily> families { get => session.Families; set => session.Families = value; }
    private List<string> familyKeysSorted { get => session.FamilyKeysSorted; set => session.FamilyKeysSorted = value; }
    private string selectedFamilyKey { get => session.SelectedFamilyKey; set => session.SelectedFamilyKey = value; }
    private AnimGroupDef selectedGroup { get => session.SelectedGroup; set => session.SelectedGroup = value; }
    private int selectedStageIndex { get => session.SelectedStageIndex; set => session.SelectedStageIndex = value; }
    private bool loopCurrentStage { get => session.LoopCurrentStage; set => session.LoopCurrentStage = value; }

    private readonly AgsPreviewSession preview;
    private Texture2D authorGizmoDiscTexture;
    private Texture2D authorGizmoRingTexture;
    private bool authorPreviewGizmosEnabled = true;

    public override Vector2 InitialSize => new Vector2(1480f, 800f);

    private const float WindowHeaderHeight = 58f;
    private const float SectionHeaderHeight = 28f;
    private const float SubsectionHeaderHeight = 22f;

    private static readonly D2UIStyle StudioUiStyle = D2UIStyle.Default.With(s =>
    {
        s.HeaderHeight = WindowHeaderHeight;
        s.FooterHeight = 0f;
        s.BodyTopPadY = 6f;
        s.BodyBottomPadY = 6f;
        s.RowHeight = 28f;
        s.ButtonHeight = 28f;
    });

    private static readonly Color SelectedGreen = new(0.25f, 0.85f, 0.35f, 1f);
    private static readonly Color SelectedGreenBg = new(0.25f, 0.85f, 0.35f, 0.18f);


    public Dialog_AnimGroupStudio()
    {
        doCloseX = true;
        doCloseButton = false;
        absorbInputAroundWindow = true;
        draggable = true;
        sourceMode = SourceMode.ExistingDef;
        preview = new AgsPreviewSession();
        authorPawnPool = new AgsPreviewPawnPool();

        // Load projects for Author.
        try { projects = repo.LoadAll(); } catch (Exception ex) { Despicable.Core.DebugLogger.WarnExceptionOnce("Dialog_AnimGroupStudio:ctor-load", "Dialog_AnimGroupStudio failed to load AGS projects; using an empty project list.", ex); projects = new List<AgsModel.Project>(); }
        if (projects.NullOrEmpty())
        {
            project = repo.CreateNewProject();
            projects = new List<AgsModel.Project> { project };
            try { repo.SaveAll(projects); } catch (Exception ex) { Despicable.Core.DebugLogger.WarnExceptionOnce("Dialog_AnimGroupStudio:1", "Dialog_AnimGroupStudio ignored a non-fatal editor exception.", ex); }
        }
        else
        {
            project = projects[0];
        }

        // Build family index once for this window instance.
        RebuildFamilies();

        authorPreviewSourceHash = int.MinValue;
    }

    public override void PreClose()
    {
        FlushQueuedSaveIfNeeded(force: true);
        base.PreClose();
        try { preview?.Dispose(); } catch (Exception ex) { Despicable.Core.DebugLogger.WarnExceptionOnce("Dialog_AnimGroupStudio:2", "Dialog_AnimGroupStudio ignored a non-fatal editor exception.", ex); }
        try { authorPawnPool?.Dispose(); } catch (Exception ex) { Despicable.Core.DebugLogger.WarnExceptionOnce("Dialog_AnimGroupStudio:3", "Dialog_AnimGroupStudio ignored a non-fatal editor exception.", ex); }
        try { if (authorGizmoDiscTexture != null) UnityEngine.Object.Destroy(authorGizmoDiscTexture); } catch (Exception ex) { Despicable.Core.DebugLogger.WarnExceptionOnce("Dialog_AnimGroupStudio:GizmoTex", "Dialog_AnimGroupStudio failed to release the AGS gizmo texture cleanly.", ex); }
        finally { authorGizmoDiscTexture = null; }
        try { if (authorGizmoRingTexture != null) UnityEngine.Object.Destroy(authorGizmoRingTexture); } catch (Exception ex) { Despicable.Core.DebugLogger.WarnExceptionOnce("Dialog_AnimGroupStudio:GizmoRingTex", "Dialog_AnimGroupStudio failed to release the AGS gizmo ring texture cleanly.", ex); }
        finally { authorGizmoRingTexture = null; }

        for (int i = 0; i < authorSlots.Count; i++)
        {
            try { authorSlots[i]?.Renderer?.Dispose(); } catch (Exception ex) { Despicable.Core.DebugLogger.WarnExceptionOnce("Dialog_AnimGroupStudio:4", "Dialog_AnimGroupStudio ignored a non-fatal editor exception.", ex); }
        }
        authorSlots.Clear();
        authorSlotsByKey.Clear();
    }

    public override void DoWindowContents(Rect inRect)
    {
        if (Event.current == null || Event.current.type == EventType.Repaint)
        {
            preview.Update(Time.deltaTime);
            SyncExistingStageSelectionToPlayback();
            if (sourceMode == SourceMode.AuthorProject)
            {
                authorPreviewPlaying = preview.IsPlaying;
                authorPreviewTick = preview.CurrentTick;
            }
        }

        frameworkCtx = new UIContext(StudioUiStyle, null, nameof(Dialog_AnimGroupStudio), UIPass.Draw);

        var shell = D2EditorShell.Layout(
            frameworkCtx,
            inRect,
            new D2EditorShell.Spec(
                "AnimStudioShell",
                headerHeight: WindowHeaderHeight,
                footerHeight: 0f,
                drawBackground: false,
                soft: false,
                pad: true,
                padOverride: frameworkCtx.Style.Pad));

        DrawStudioHeader(shell.Header);

        int selectedSource = sourceMode == SourceMode.AuthorProject ? 0 : 1;
        var attached = D2Tabs.VanillaAttachedTabBody(
            frameworkCtx,
            shell.Workspace,
            ref selectedSource,
            new[] { "Author", "Existing" },
            "AnimStudio/SourceTabs",
            innerPad: 6f,
            forcedRows: 1);

        if (selectedSource == 0 && sourceMode != SourceMode.AuthorProject)
        {
            ActivateAuthorSourceMode();
        }
        else if (selectedSource == 1 && sourceMode != SourceMode.ExistingDef)
        {
            ActivateExistingSourceMode();
        }

        if (sourceMode == SourceMode.AuthorProject)
            RefreshAuthorPreviewIfNeeded();

        using (frameworkCtx.PushScope("Body"))
        {
            DrawAuthor(attached.InnerRect);
        }

        if (Event.current == null || Event.current.type == EventType.Repaint)
            FlushQueuedSaveIfNeeded();
    }

    private void ActivateAuthorSourceMode()
    {
        preview.Stop();
        sourceMode = SourceMode.AuthorProject;
        MarkAuthorPreviewStructureDirty();
        authorRuntime.SelectionDirty = true;
        RebindAuthorPreviewFromState();
    }

    private void ActivateExistingSourceMode()
    {
        bool dragChangedData = authorRuntime.GizmoDragChangedData;
        ClearAuthorPreviewGizmoPointerState(preserveCycleState: false);
        if (dragChangedData)
            QueueAuthorSave();

        int preservedAuthorTick = authorPreviewTick;
        preview.Stop();
        authorPreviewPlaying = false;
        authorPreviewTickAcc = 0f;
        authorPreviewTick = preservedAuthorTick;
        sourceMode = SourceMode.ExistingDef;
        RebindExistingPreviewFromSelection();
    }

    private void RebindExistingPreviewFromSelection()
    {
        preview.ConfigureFor(selectedGroup);
        int stageCount = preview.StageCount;
        selectedStageIndex = Mathf.Clamp(selectedStageIndex, 0, Mathf.Max(0, stageCount - 1));
        preview.SelectedStageIndex = selectedStageIndex;
        if (selectedGroup != null && stageCount > 0)
            preview.ShowSelectedStageAtTick(0);
    }

    private void RebindAuthorPreviewFromState()
    {
        if (project == null)
            return;

        RefreshAuthorPreviewIfNeeded();

        var stage = GetStage(project, authorStageIndex);
        int tick = Mathf.Clamp(authorPreviewTick, 0, Mathf.Max(1, stage?.durationTicks ?? 1));
        ShowAuthorStageAtTick(authorStageIndex, tick, seekIfPlaying: false);
    }

    private void DrawAuthor(Rect rect)
    {
        var ctx = frameworkCtx ?? new UIContext(D2UIStyle.Default, null, nameof(Dialog_AnimGroupStudio), UIPass.Draw);

        if (sourceMode == SourceMode.ExistingDef)
        {
            var panes = D2PaneLayout.Columns(
                ctx,
                rect,
                new[]
                {
                    new D2PaneLayout.PaneSpec("Left", 332f, 372f, 0.2f, canCollapse: false, priority: 0),
                    new D2PaneLayout.PaneSpec("Center", 620f, 900f, 2.0f, canCollapse: false, priority: 0),
                    new D2PaneLayout.PaneSpec("Right", 260f, 320f, 0.3f, canCollapse: false, priority: 0),
                },
                gap: ctx.Style.Gap * 1.5f,
                fallback: D2PaneLayout.FallbackMode.Stack,
                label: "AnimGroupStudio/ExistingPanes");

            if (panes.Rects.Length > 0 && panes.Rects[0].width > 0f && panes.Rects[0].height > 0f)
                DrawAuthorLeft(panes.Rects[0]);
            if (panes.Rects.Length > 1 && panes.Rects[1].width > 0f && panes.Rects[1].height > 0f)
                DrawAuthorCenter(panes.Rects[1]);
            if (panes.Rects.Length > 2 && panes.Rects[2].width > 0f && panes.Rects[2].height > 0f)
                DrawExistingInfo(panes.Rects[2]);
            return;
        }

        var authorPanes = D2PaneLayout.Columns(
            ctx,
            rect,
            new[]
            {
                new D2PaneLayout.PaneSpec("Left", 300f, 330f, 0f, canCollapse: false, priority: 0),
                new D2PaneLayout.PaneSpec("Center", 500f, 700f, 2.0f, canCollapse: false, priority: 0),
                new D2PaneLayout.PaneSpec("Data", 260f, 300f, 0.45f, canCollapse: false, priority: 0),
                new D2PaneLayout.PaneSpec("Inspector", 300f, 360f, 0.6f, canCollapse: false, priority: 0),
            },
            gap: ctx.Style.Gap * 1.5f,
            fallback: D2PaneLayout.FallbackMode.Stack,
            label: "AnimGroupStudio/AuthorPanes");

        if (authorPanes.Rects.Length > 0 && authorPanes.Rects[0].width > 0f && authorPanes.Rects[0].height > 0f)
            DrawAuthorLeft(authorPanes.Rects[0]);
        if (authorPanes.Rects.Length > 1 && authorPanes.Rects[1].width > 0f && authorPanes.Rects[1].height > 0f)
            DrawAuthorCenter(authorPanes.Rects[1]);
        if (authorPanes.Rects.Length > 2 && authorPanes.Rects[2].width > 0f && authorPanes.Rects[2].height > 0f)
            DrawAuthorData(authorPanes.Rects[2]);
        if (authorPanes.Rects.Length > 3 && authorPanes.Rects[3].width > 0f && authorPanes.Rects[3].height > 0f)
            DrawAuthorInspectorColumn(authorPanes.Rects[3]);
    }


    

    





    private void SyncExistingStageSelectionToPlayback()
    {
        if (sourceMode != SourceMode.ExistingDef || !preview.IsPlaying || preview.StageCount <= 0)
            return;

        int playbackStage = preview.CurrentStageIndex;
        if (playbackStage == selectedStageIndex)
            return;

        selectedStageIndex = playbackStage;
        preview.SelectedStageIndex = playbackStage;
    }
}
