using RimWorld;
// Guardrail-Reason: AGS preview panel keeps gizmo math, viewport projection, and playback interaction together because the editor hit-testing surface is tightly coupled.
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
public partial class Dialog_AnimGroupStudio
{
    private const float AuthorTrackOffsetClampXMin = -1.4f;
    private const float AuthorTrackOffsetClampXMax = 1.4f;
    private const float AuthorTrackOffsetClampZMin = -1.1f;
    private const float AuthorTrackOffsetClampZMax = 1.1f;

    // Prop nodes can legitimately sit further from the body origin.
    private const float AuthorPropOffsetClampXMin = -3.5f;
    private const float AuthorPropOffsetClampXMax = 3.5f;
    private const float AuthorPropOffsetClampZMin = -2.75f;
    private const float AuthorPropOffsetClampZMax = 2.75f;

    private void DrawAuthorCenter(Rect rect)
    {
        var ctx = frameworkCtx;
        var parts = D2Section.Layout(
            ctx,
            rect,
            new D2Section.Spec(
                "AuthorCenter",
                headerHeight: SectionHeaderHeight,
                soft: true,
                pad: true,
                drawBackground: true,
                padOverride: ctx.Style.Pad));

        D2Section.DrawCaptionStrip(ctx, parts.Header, "Preview & Playback", "AuthorCenter/Header", GameFont.Medium);

        var v = ctx.D2VStack(parts.Body);

        if (sourceMode == SourceMode.ExistingDef)
        {
            if (selectedGroup == null)
            {
                v.NextTextBlock(
                    ctx,
                    "Select an existing AnimGroupDef on the left to preview it. (Import it if you want to edit/create variations.)",
                    GameFont.Small,
                    padding: 2f,
                    label: "Center/Existing/Empty");
                return;
            }

            float transportH = authorPlayMode == AuthorPlayMode.Group
                ? (ctx.Style.RowHeight * 2f + ctx.Style.Gap)
                : ctx.Style.RowHeight;
            DrawExistingPreviewTransport(v.Next(transportH, UIRectTag.Input, "Center/Existing/Transport"));
            v.NextSpace(2f);

            if (authorPlayMode == AuthorPlayMode.Stage)
            {
                bool loopVal = loopCurrentStage;
                D2Widgets.CheckboxLabeled(
                    ctx,
                    v.NextRow(UIRectTag.Checkbox, "Center/Existing/LoopStage"),
                    "Loop selected stage",
                    ref loopVal,
                    "Center/Existing/LoopStage");
                loopCurrentStage = loopVal;
                preview.LoopCurrentStage = loopCurrentStage;
            }

            int stageCount = preview.StageCount;
            string stageInfo = stageCount > 0
                ? $"Stage {Mathf.Clamp(selectedStageIndex, 0, stageCount - 1):00} / {Mathf.Max(0, stageCount - 1):00}"
                : "No stages";
            string playInfo = preview.IsPlaying ? "Playback is driving stage selection." : "Click a stage on the left to inspect it.";
            v.NextTextBlock(ctx, selectedGroup.defName ?? "(unnamed)", GameFont.Small, padding: 2f, label: "Center/Existing/DefName");
            v.NextTextBlock(ctx, stageInfo + "   •   " + playInfo, GameFont.Small, padding: 2f, label: "Center/Existing/Info");

            Rect view = v.NextFill(UIRectTag.Body, "Center/Existing/Viewport");
            using (var previewPanel = ctx.GroupPanel("Center/Existing/PreviewSurface", view, soft: true, pad: true, padOverride: ctx.Style.Pad, drawBackground: true, label: "Center/Existing/PreviewSurface"))
                preview.DrawViewport(previewPanel.Inner, drawSection: false);
            return;
        }

        if (project == null)
        {
            v.NextTextBlock(ctx, "No project loaded.", GameFont.Small, padding: 2f, label: "Center/Author/NoProject");
            return;
        }

        var stage = GetStage(project, authorStageIndex);
        if (stage == null)
        {
            v.NextTextBlock(ctx, "No stage selected.", GameFont.Small, padding: 2f, label: "Center/Author/NoStage");
            return;
        }

        DrawAuthorPreviewTimeline(v.Next(46f, UIRectTag.Input, "Center/Author/Timeline"), stage);
        v.NextSpace(2f);

        float authorTransportH = authorPlayMode == AuthorPlayMode.Group
            ? (ctx.Style.RowHeight * 2f + ctx.Style.Gap)
            : ctx.Style.RowHeight;
        DrawAuthorPreviewTransport(v.Next(authorTransportH, UIRectTag.Input, "Center/Author/Transport"));
        v.NextSpace(2f);

        bool gizmoToggle = authorPreviewGizmosEnabled;
        D2Widgets.CheckboxLabeled(
            ctx,
            v.NextRow(UIRectTag.Checkbox, "Center/Author/ShowGizmos"),
            "Show gizmos",
            ref gizmoToggle,
            "Center/Author/ShowGizmos");
        SetAuthorPreviewGizmosEnabled(gizmoToggle);
        v.NextSpace(2f);

        Rect authorView = v.NextFill(UIRectTag.Body, "Center/Author/Viewport");
        using (var previewPanel = ctx.GroupPanel("Center/Author/PreviewSurface", authorView, soft: true, pad: true, padOverride: ctx.Style.Pad, drawBackground: true, label: "Center/Author/PreviewSurface"))
            DrawAuthorPreviewViewport(previewPanel.Inner);
    }

    private void SetAuthorPreviewGizmosEnabled(bool enabled)
    {
        if (authorPreviewGizmosEnabled == enabled)
            return;

        authorPreviewGizmosEnabled = enabled;
        if (!authorPreviewGizmosEnabled)
        {
            bool dragChangedData = authorRuntime.GizmoDragChangedData;
            ClearAuthorPreviewGizmoPointerState(preserveCycleState: false);
            authorRuntime.Gizmos.Clear();
            authorRuntime.GizmosByTag.Clear();
            if (dragChangedData)
                QueueAuthorSave();
        }
    }

    private void DrawAuthorPreviewTimeline(Rect rect, AgsModel.StageSpec stage)
    {
        int dur = Mathf.Max(1, stage.durationTicks);
        authorPreviewTick = Mathf.Clamp(preview.CurrentTick, 0, dur);
        authorPreviewPlaying = preview.IsPlaying;

        Widgets.DrawBoxSolid(rect, new Color(0f, 0f, 0f, 0.06f));
        Widgets.DrawHighlightIfMouseover(rect);

        if (Mouse.IsOver(rect)
            && (Event.current.type == EventType.MouseDown || Event.current.type == EventType.MouseDrag)
            && Event.current.button == 0)
        {
            if (authorKeyIndex >= 0)
                authorKeyIndex = -1;

            float pct = Mathf.InverseLerp(rect.x + 6f, rect.xMax - 6f, Event.current.mousePosition.x);
            int t = Mathf.RoundToInt(pct * dur);
            t = Mathf.Clamp(t, 0, dur);
            if (t != authorPreviewTick)
            {
                ShowAuthorStageAtTick(authorStageIndex, t, seekIfPlaying: true);
            }
            TryAutoSelectKeyframeAtTick(t);
            Event.current.Use();
        }

        float labelH = 22f;
        float gap = 4f;
        float barPadX = 10f;
        float barPadBottom = 6f;

        float barY = rect.y + labelH + gap;
        float barH = rect.height - (labelH + gap) - barPadBottom;
        if (barH < 10f)
            barH = 10f;

        var bar = new Rect(rect.x + barPadX, barY, rect.width - (barPadX * 2f), barH);
        float pctNow = dur <= 0 ? 0f : (authorPreviewTick / (float)dur);
        float x = Mathf.Lerp(bar.x, bar.xMax, pctNow);

        Widgets.DrawLineHorizontal(bar.x, bar.center.y, bar.width);
        Widgets.DrawBoxSolid(
            new Rect(bar.x, bar.y + (bar.height / 2f) - 1f, Mathf.Max(1f, x - bar.x), 3f),
            new Color(SelectedGreen.r, SelectedGreen.g, SelectedGreen.b, 0.25f));
        Widgets.DrawLineVertical(x, bar.y, bar.height);

        var leftLbl = new Rect(rect.x + 8f, rect.y + 2f, rect.width / 2f, labelH);
        var rightLbl = new Rect(rect.x, rect.y + 2f, rect.width - 8f, labelH);

        Text.Anchor = TextAnchor.UpperLeft;
        Widgets.Label(leftLbl, "Timeline");
        Text.Anchor = TextAnchor.UpperRight;
        Widgets.Label(rightLbl, authorPreviewTick + "/" + dur);
        Text.Anchor = TextAnchor.UpperLeft;
    }

    private void DrawAuthorPreviewViewport(Rect rect)
    {
        if (!authorPreviewGizmosEnabled)
        {
            preview.NodeCaptureEnabled = false;
            authorRuntime.Gizmos.Clear();
            authorRuntime.GizmosByTag.Clear();
            preview.DrawViewport(rect, drawSection: false, useFullRect: true, drawHeader: false);
            return;
        }

        preview.NodeCaptureEnabled = true;
        try
        {
            preview.DrawViewport(rect, drawSection: false, useFullRect: true, drawHeader: false);
            RebuildAuthorPreviewGizmoSnapshot();
            bool snapshotChanged = HandleAuthorPreviewGizmoInput();
            if (snapshotChanged)
                RebuildAuthorPreviewGizmoSnapshot();
            DrawAuthorPreviewGizmos();
        }
        finally
        {
            preview.NodeCaptureEnabled = false;
        }
    }

    private void RebuildAuthorPreviewGizmoSnapshot()
    {
        authorRuntime.Gizmos.Clear();
        authorRuntime.GizmosByTag.Clear();

        Rect textureRect = preview.LastViewportTextureRect;
        if (textureRect.width <= 1f || textureRect.height <= 1f || project == null)
            return;

        var stage = GetStage(project, authorStageIndex);
        var clip = GetClip(stage, authorRoleKey);
        if (clip?.tracks.NullOrEmpty() != false)
            return;

        List<AgsPreviewNodeCapture.RawNodeSample> rawSamples = null;
        preview.TryGetLastNodeSamples(authorRoleKey, out rawSamples);

        var allowedTags = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < clip.tracks.Count; i++)
        {
            string nodeTag = clip.tracks[i]?.nodeTag;
            if (!nodeTag.NullOrEmpty())
                allowedTags.Add(nodeTag);
        }

        if (allowedTags.Count == 0)
            return;

        var displayAggregateByTag = new Dictionary<string, GizmoAggregate>(StringComparer.Ordinal);
        var supportAggregateByTag = new Dictionary<string, GizmoAggregate>(StringComparer.Ordinal);

        if (rawSamples != null)
        {
            for (int i = 0; i < rawSamples.Count; i++)
            {
                var sample = rawSamples[i];
                if (sample == null || sample.NodeTag.NullOrEmpty())
                    continue;

                Vector2 uv = sample.ViewportUv;
                bool uvValid = uv.x >= -0.35f && uv.x <= 1.35f && uv.y >= -0.35f && uv.y <= 1.35f;
                if (!uvValid)
                    continue;

                AccumulateGizmoAggregate(supportAggregateByTag, sample.NodeTag, uv, sample.Depth, sample.CameraDepth, sample.ViewportBasisX, sample.ViewportBasisZ);
                if (allowedTags.Contains(sample.NodeTag))
                    AccumulateGizmoAggregate(displayAggregateByTag, sample.NodeTag, uv, sample.Depth, sample.CameraDepth, sample.ViewportBasisX, sample.ViewportBasisZ);
            }
        }

        EnsureSyntheticRootGizmoAggregate(allowedTags, displayAggregateByTag, supportAggregateByTag);

        string selectedNodeTag = null;
        if (authorTrackIndex >= 0 && authorTrackIndex < clip.tracks.Count)
            selectedNodeTag = clip.tracks[authorTrackIndex]?.nodeTag;

        BuildAuthorPreviewGizmoEntries(textureRect, displayAggregateByTag, selectedNodeTag);
    }

    private static void AccumulateGizmoAggregate(Dictionary<string, GizmoAggregate> aggregateByTag, string nodeTag, Vector2 uv, int depth, float cameraDepth, Vector2 basisX, Vector2 basisZ)
    {
        if (!aggregateByTag.TryGetValue(nodeTag, out GizmoAggregate aggregate))
        {
            aggregate = new GizmoAggregate();
            aggregateByTag[nodeTag] = aggregate;
        }

        aggregate.SumUv += uv;
        aggregate.SumCameraDepth += cameraDepth;
        aggregate.SumBasisX += basisX;
        aggregate.SumBasisZ += basisZ;
        aggregate.SampleCount++;
        aggregate.MinDepth = Mathf.Min(aggregate.MinDepth, depth);
    }

    private static void EnsureSyntheticRootGizmoAggregate(HashSet<string> allowedTags, Dictionary<string, GizmoAggregate> displayAggregateByTag, Dictionary<string, GizmoAggregate> supportAggregateByTag)
    {
        if (allowedTags == null || displayAggregateByTag == null)
            return;

        if (!allowedTags.Contains("Root") || displayAggregateByTag.ContainsKey("Root"))
            return;

        if (TryGetSupportAggregate(supportAggregateByTag, "Body", out GizmoAggregate body))
        {
            displayAggregateByTag["Root"] = CloneGizmoAggregate(body, body.MinDepth + 1, isSynthetic: true, dragBasisNodeTag: "Body");
            return;
        }

        GizmoAggregate average = TryCreateAverageAggregate(supportAggregateByTag, excludeNodeTag: "Root");
        if (average != null)
        {
            average.IsSynthetic = true;
            average.DragBasisNodeTag = FindBestSyntheticRootDragBasisNodeTag(supportAggregateByTag);
            displayAggregateByTag["Root"] = average;
            return;
        }

        displayAggregateByTag["Root"] = CreateViewportCenteredRootAggregate();
    }

    private static bool TryGetSupportAggregate(Dictionary<string, GizmoAggregate> aggregateByTag, string nodeTag, out GizmoAggregate aggregate)
    {
        aggregate = null;
        if (aggregateByTag == null || nodeTag.NullOrEmpty())
            return false;

        if (!aggregateByTag.TryGetValue(nodeTag, out aggregate) || aggregate == null || aggregate.SampleCount <= 0)
        {
            aggregate = null;
            return false;
        }

        return true;
    }

    private static GizmoAggregate CloneGizmoAggregate(GizmoAggregate source, int minDepth, bool isSynthetic = false, string dragBasisNodeTag = null)
    {
        if (source == null)
            return null;

        return new GizmoAggregate
        {
            SumUv = source.SumUv,
            SumBasisX = source.SumBasisX,
            SumBasisZ = source.SumBasisZ,
            SumCameraDepth = source.SumCameraDepth,
            SampleCount = source.SampleCount,
            MinDepth = minDepth,
            IsSynthetic = isSynthetic,
            DragBasisNodeTag = dragBasisNodeTag
        };
    }

    private static GizmoAggregate TryCreateAverageAggregate(Dictionary<string, GizmoAggregate> aggregateByTag, string excludeNodeTag)
    {
        if (aggregateByTag == null || aggregateByTag.Count == 0)
            return null;

        var average = new GizmoAggregate();
        bool foundAny = false;
        foreach (KeyValuePair<string, GizmoAggregate> kvp in aggregateByTag)
        {
            if (string.Equals(kvp.Key, excludeNodeTag, StringComparison.Ordinal))
                continue;

            GizmoAggregate aggregate = kvp.Value;
            if (aggregate == null || aggregate.SampleCount <= 0)
                continue;

            average.SumUv += aggregate.SumUv;
            average.SumBasisX += aggregate.SumBasisX;
            average.SumBasisZ += aggregate.SumBasisZ;
            average.SumCameraDepth += aggregate.SumCameraDepth;
            average.SampleCount += aggregate.SampleCount;
            average.MinDepth = Mathf.Min(average.MinDepth, aggregate.MinDepth + 1);
            foundAny = true;
        }

        if (!foundAny)
            return null;

        if (average.MinDepth == int.MaxValue)
            average.MinDepth = 1;

        return average;
    }

    private static string FindBestSyntheticRootDragBasisNodeTag(Dictionary<string, GizmoAggregate> aggregateByTag)
    {
        if (aggregateByTag == null || aggregateByTag.Count == 0)
            return null;

        if (aggregateByTag.TryGetValue("Body", out GizmoAggregate body) && body?.SampleCount > 0)
            return "Body";

        foreach (KeyValuePair<string, GizmoAggregate> kvp in aggregateByTag.OrderBy(k => k.Value.MinDepth).ThenBy(k => k.Key, StringComparer.Ordinal))
        {
            if (string.Equals(kvp.Key, "Root", StringComparison.Ordinal))
                continue;

            GizmoAggregate aggregate = kvp.Value;
            if (aggregate == null || aggregate.SampleCount <= 0)
                continue;

            if (aggregate.SumBasisX.sqrMagnitude > 0.0000001f || aggregate.SumBasisZ.sqrMagnitude > 0.0000001f)
                return kvp.Key;
        }

        return null;
    }

    private static GizmoAggregate CreateViewportCenteredRootAggregate()
    {
        return new GizmoAggregate
        {
            SumUv = new Vector2(0.5f, 0.5f),
            SumBasisX = Vector2.zero,
            SumBasisZ = Vector2.zero,
            SumCameraDepth = 0f,
            SampleCount = 1,
            MinDepth = 1,
            IsSynthetic = true,
            DragBasisNodeTag = null
        };
    }

    private void BuildAuthorPreviewGizmoEntries(Rect textureRect, Dictionary<string, GizmoAggregate> aggregateByTag, string selectedNodeTag)
    {
        foreach (KeyValuePair<string, GizmoAggregate> kvp in aggregateByTag.OrderBy(k => k.Value.MinDepth).ThenBy(k => k.Key, StringComparer.Ordinal))
        {
            if (kvp.Value.SampleCount <= 0)
                continue;

            Vector2 uv = kvp.Value.SumUv / kvp.Value.SampleCount;
            Vector2 basisX = kvp.Value.SumBasisX / kvp.Value.SampleCount;
            Vector2 basisZ = kvp.Value.SumBasisZ / kvp.Value.SampleCount;
            float cameraDepth = kvp.Value.SumCameraDepth / kvp.Value.SampleCount;
            bool isRoot = string.Equals(kvp.Key, "Root", StringComparison.Ordinal);

            // Root's offset is in world space, but synthetic root borrows its basis
            // from Body, which is in root-local space (rotated by root's angle).
            // Replace with world-aligned axes: basis vectors should point along world
            // X and Z. The correct scale (viewport UV per world unit) is invariant to
            // rotation, so we can read it from any child node's basis magnitude.
            if (isRoot && kvp.Value.IsSynthetic)
            {
                float scaleX = 0f, scaleZ = 0f;
                foreach (KeyValuePair<string, GizmoAggregate> kv2 in aggregateByTag)
                {
                    if (string.Equals(kv2.Key, "Root", StringComparison.Ordinal) || kv2.Value == null || kv2.Value.SampleCount <= 0)
                        continue;
                    Vector2 childBX = kv2.Value.SumBasisX / kv2.Value.SampleCount;
                    Vector2 childBZ = kv2.Value.SumBasisZ / kv2.Value.SampleCount;
                    float candidateScaleX = childBX.magnitude;
                    float candidateScaleZ = childBZ.magnitude;
                    if (candidateScaleX > 0.0000001f && candidateScaleZ > 0.0000001f)
                    {
                        // Prefer Body as reference node; otherwise take the first valid one.
                        scaleX = candidateScaleX;
                        scaleZ = candidateScaleZ;
                        if (string.Equals(kv2.Key, "Body", StringComparison.Ordinal))
                            break;
                    }
                }
                if (scaleX > 0.0000001f && scaleZ > 0.0000001f)
                {
                    basisX = new Vector2(scaleX, 0f);
                    basisZ = new Vector2(0f, scaleZ);
                }
            }
            float radius = Mathf.Clamp(13f - (kvp.Value.MinDepth * 1.8f), 5f, 13f);
            if (isRoot)
                radius = Mathf.Clamp(radius + 3f, 8f, 16f);

            bool isSelected = !selectedNodeTag.NullOrEmpty() && string.Equals(kvp.Key, selectedNodeTag, StringComparison.Ordinal);
            if (isSelected)
                radius += 1.5f;

            float centerX = Mathf.Lerp(textureRect.x, textureRect.xMax, uv.x);
            float centerY = Mathf.Lerp(textureRect.yMax, textureRect.y, uv.y);
            Color baseColor = GetAuthorPreviewGizmoColor(kvp.Key);
            Color outlineColor = isSelected ? Color.white : new Color(baseColor.r * 0.55f, baseColor.g * 0.55f, baseColor.b * 0.55f, 0.98f);
            Color fillColor = new Color(baseColor.r, baseColor.g, baseColor.b, isSelected ? 0.92f : 0.74f);

            var entry = new AgsAuthorPreviewGizmoEntry
            {
                NodeTag = kvp.Key,
                ViewportUv = uv,
                ScreenRect = new Rect(centerX - radius, centerY - radius, radius * 2f, radius * 2f),
                Radius = radius,
                Depth = kvp.Value.MinDepth,
                SourceCount = kvp.Value.SampleCount,
                CameraDepth = cameraDepth,
                ViewportBasisX = basisX,
                ViewportBasisZ = basisZ,
                FillColor = fillColor,
                OutlineColor = outlineColor,
                IsSelected = isSelected,
                IsSynthetic = kvp.Value.IsSynthetic,
                DragBasisNodeTag = kvp.Value.DragBasisNodeTag,
                VisualKind = isRoot ? AgsAuthorPreviewGizmoVisualKind.Ring : AgsAuthorPreviewGizmoVisualKind.Disc
            };

            authorRuntime.Gizmos.Add(entry);
            if (!authorRuntime.GizmosByTag.ContainsKey(kvp.Key))
                authorRuntime.GizmosByTag[kvp.Key] = entry;
        }
    }

    private bool HandleAuthorPreviewGizmoInput()
    {
        Event ev = Event.current;
        if (ev == null || (!authorRuntime.GizmoPressActive && authorRuntime.Gizmos.Count == 0))
            return false;

        bool snapshotChanged = false;

        if (ev.type == EventType.MouseDown && ev.button == 0)
        {
            string hitNodeTag = PickAuthorPreviewGizmoAt(ev.mousePosition);
            if (!hitNodeTag.NullOrEmpty())
            {
                snapshotChanged |= SelectAuthorTrackByNodeTag(hitNodeTag, clearKeySelection: false);
                if (authorRuntime.GizmosByTag.TryGetValue(hitNodeTag, out AgsAuthorPreviewGizmoEntry gizmo) && gizmo != null)
                {
                    authorRuntime.GizmoPressActive = true;
                    authorRuntime.GizmoDragging = false;
                    authorRuntime.ActiveGizmoNodeTag = hitNodeTag;
                    authorRuntime.GizmoDragMode = AgsAuthorPreviewGizmoDragMode.Translate;
                    authorRuntime.GizmoPressMousePosition = ev.mousePosition;
                    authorRuntime.GizmoDragStartViewportUv = gizmo.ViewportUv;
                    authorRuntime.GizmoTranslateGrabViewportOffset = Vector2.zero;
                    authorRuntime.GizmoDragPivotScreenPosition = gizmo.ScreenRect.center;
                    authorRuntime.GizmoDragChangedData = false;
                }
                ev.Use();
            }
            else if (TryBeginAuthorPreviewRotationPress(ev.mousePosition))
            {
                ev.Use();
                return false;
            }
            else
            {
                ClearAuthorPreviewGizmoPointerState(preserveCycleState: false);
            }
        }
        else if (ev.type == EventType.MouseDrag && authorRuntime.GizmoPressActive)
        {
            if (!authorRuntime.GizmoDragging)
            {
                if ((ev.mousePosition - authorRuntime.GizmoPressMousePosition).sqrMagnitude >= 9f && BeginAuthorPreviewGizmoDrag())
                    snapshotChanged = true;
            }

            if (authorRuntime.GizmoDragging)
            {
                UpdateAuthorPreviewGizmoDrag(ev.mousePosition);
                ev.Use();
            }
        }
        else if (ev.type == EventType.MouseUp && ev.button == 0 && authorRuntime.GizmoPressActive)
        {
            EndAuthorPreviewGizmoDrag();
            ev.Use();
        }

        return snapshotChanged;
    }

    private bool TryBeginAuthorPreviewRotationPress(Vector2 mousePosition)
    {
        if (project == null || authorTrackIndex < 0)
            return false;

        AgsAuthorPreviewGizmoEntry selectedGizmo = GetSelectedAuthorPreviewGizmo();
        if (selectedGizmo == null || !IsPointInAuthorPreviewRotationRing(selectedGizmo, mousePosition))
            return false;

        authorRuntime.GizmoPressActive = true;
        authorRuntime.GizmoDragging = false;
        authorRuntime.ActiveGizmoNodeTag = selectedGizmo.NodeTag;
        authorRuntime.GizmoDragMode = AgsAuthorPreviewGizmoDragMode.Rotate;
        authorRuntime.GizmoPressMousePosition = mousePosition;
        authorRuntime.GizmoDragPivotScreenPosition = selectedGizmo.ScreenRect.center;
        authorRuntime.GizmoDragStartViewportUv = selectedGizmo.ViewportUv;
        authorRuntime.GizmoTranslateGrabViewportOffset = Vector2.zero;
        authorRuntime.GizmoDragChangedData = false;
        return true;
    }

    private AgsAuthorPreviewGizmoEntry GetSelectedAuthorPreviewGizmo()
    {
        if (project == null || authorTrackIndex < 0)
            return null;

        var stage = GetStage(project, authorStageIndex);
        var clip = GetClip(stage, authorRoleKey);
        if (clip?.tracks == null || authorTrackIndex >= clip.tracks.Count)
            return null;

        string selectedNodeTag = clip.tracks[authorTrackIndex]?.nodeTag;
        if (selectedNodeTag.NullOrEmpty())
            return null;

        authorRuntime.GizmosByTag.TryGetValue(selectedNodeTag, out AgsAuthorPreviewGizmoEntry gizmo);
        return gizmo;
    }

    private static bool IsPointInAuthorPreviewRotationRing(AgsAuthorPreviewGizmoEntry gizmo, Vector2 mousePosition)
    {
        if (gizmo == null)
            return false;

        Vector2 delta = mousePosition - gizmo.ScreenRect.center;
        float distanceSq = delta.sqrMagnitude;
        float innerRadius = GetAuthorPreviewRotationRingInnerRadius(gizmo);
        float outerRadius = GetAuthorPreviewRotationRingOuterRadius(gizmo);
        return distanceSq >= (innerRadius * innerRadius) && distanceSq <= (outerRadius * outerRadius);
    }

    private static float GetAuthorPreviewRotationRingInnerRadius(AgsAuthorPreviewGizmoEntry gizmo)
    {
        return GetAuthorPreviewGizmoHitRadius(gizmo) + Mathf.Max(5f, gizmo.Radius * 0.35f);
    }

    private static float GetAuthorPreviewRotationRingOuterRadius(AgsAuthorPreviewGizmoEntry gizmo)
    {
        float innerRadius = GetAuthorPreviewRotationRingInnerRadius(gizmo);
        return innerRadius + Mathf.Max(9f, gizmo.Radius * 0.85f);
    }

    private static float GetAuthorPreviewMouseAngle(Vector2 pivotScreenPosition, Vector2 mousePosition)
    {
        Vector2 delta = mousePosition - pivotScreenPosition;
        return Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
    }

    private static Vector2 ViewportUvToPanelPoint(Rect textureRect, Vector2 viewportUv)
    {
        return new Vector2(
            Mathf.Lerp(textureRect.x, textureRect.xMax, viewportUv.x),
            Mathf.Lerp(textureRect.yMax, textureRect.y, viewportUv.y));
    }

    private static Vector2 ViewportBasisToPanelDelta(Rect textureRect, Vector2 viewportBasis)
    {
        return new Vector2(viewportBasis.x * textureRect.width, -viewportBasis.y * textureRect.height);
    }

    private static bool IsPointInsideAuthorPreviewViewport(Rect textureRect, Vector2 panelPoint)
    {
        Rect viewportBounds = textureRect.ContractedBy(2f);
        return viewportBounds.width > 0f && viewportBounds.height > 0f
            ? viewportBounds.Contains(panelPoint)
            : textureRect.Contains(panelPoint);
    }

    private bool TryResolveAuthorPreviewTranslationBasis(string nodeTag, out Vector2 basisX, out Vector2 basisZ)
    {
        if (!nodeTag.NullOrEmpty()
            && authorRuntime.GizmosByTag.TryGetValue(nodeTag, out AgsAuthorPreviewGizmoEntry activeGizmo))
        {
            bool isRoot = string.Equals(activeGizmo.NodeTag, "Root", StringComparison.Ordinal);
            if (!isRoot
                && !activeGizmo.DragBasisNodeTag.NullOrEmpty()
                && !string.Equals(activeGizmo.DragBasisNodeTag, activeGizmo.NodeTag, StringComparison.Ordinal)
                && authorRuntime.GizmosByTag.TryGetValue(activeGizmo.DragBasisNodeTag, out AgsAuthorPreviewGizmoEntry borrowedBasisGizmo)
                && TryGetAuthorPreviewTranslationBasisFromGizmo(borrowedBasisGizmo, out basisX, out basisZ))
            {
                return true;
            }

            if (TryGetAuthorPreviewTranslationBasisFromGizmo(activeGizmo, out basisX, out basisZ))
                return true;
        }

        if (authorRuntime.GizmosByTag.TryGetValue("Body", out AgsAuthorPreviewGizmoEntry bodyGizmo)
            && TryGetAuthorPreviewTranslationBasisFromGizmo(bodyGizmo, out basisX, out basisZ))
        {
            return true;
        }

        if (authorRuntime.GizmosByTag.TryGetValue("Root", out AgsAuthorPreviewGizmoEntry rootGizmo)
            && TryGetAuthorPreviewTranslationBasisFromGizmo(rootGizmo, out basisX, out basisZ))
        {
            return true;
        }

        for (int i = 0; i < authorRuntime.Gizmos.Count; i++)
        {
            if (TryGetAuthorPreviewTranslationBasisFromGizmo(authorRuntime.Gizmos[i], out basisX, out basisZ))
                return true;
        }

        basisX = new Vector2(0.02f, 0f);
        basisZ = new Vector2(0f, 0.02f);
        return true;
    }

    private static bool TryGetAuthorPreviewTranslationBasisFromGizmo(AgsAuthorPreviewGizmoEntry gizmo, out Vector2 basisX, out Vector2 basisZ)
    {
        basisX = Vector2.zero;
        basisZ = Vector2.zero;
        if (gizmo == null)
            return false;

        if (gizmo.ViewportBasisX.sqrMagnitude <= 0.0000001f && gizmo.ViewportBasisZ.sqrMagnitude <= 0.0000001f)
            return false;

        basisX = gizmo.ViewportBasisX;
        basisZ = gizmo.ViewportBasisZ;
        return true;
    }

    private string PickAuthorPreviewGizmoAt(Vector2 mousePosition)
    {
        List<AgsAuthorPreviewGizmoEntry> hits = GetAuthorPreviewGizmoHits(mousePosition);
        if (hits.Count == 0)
            return null;

        string signature = string.Join("|", hits.Select(h => h.NodeTag).OrderBy(tag => tag, StringComparer.Ordinal));
        Vector2 clusterCenter = GetAuthorPreviewGizmoHitClusterCenter(hits);
        bool continueCycle =
            signature == authorRuntime.LastOverlapSignature
            && (clusterCenter - authorRuntime.LastOverlapMousePosition).sqrMagnitude <= 100f
            && (mousePosition - clusterCenter).sqrMagnitude <= GetAuthorPreviewGizmoCycleRadiusSq(hits)
            && (Time.realtimeSinceStartup - authorRuntime.LastOverlapClickTime) <= 1.1f;

        int pickIndex = continueCycle
            ? (authorRuntime.LastOverlapCycleIndex + 1) % hits.Count
            : 0;

        authorRuntime.LastOverlapSignature = signature;
        authorRuntime.LastOverlapCycleIndex = pickIndex;
        authorRuntime.LastOverlapMousePosition = clusterCenter;
        authorRuntime.LastOverlapClickTime = Time.realtimeSinceStartup;
        return hits[pickIndex]?.NodeTag;
    }

    private List<AgsAuthorPreviewGizmoEntry> GetAuthorPreviewGizmoHits(Vector2 mousePosition)
    {
        var hits = new List<AgsAuthorPreviewGizmoEntry>();
        for (int i = 0; i < authorRuntime.Gizmos.Count; i++)
        {
            AgsAuthorPreviewGizmoEntry gizmo = authorRuntime.Gizmos[i];
            if (gizmo == null)
                continue;

            Vector2 center = gizmo.ScreenRect.center;
            float hitRadius = GetAuthorPreviewGizmoHitRadius(gizmo);
            if ((mousePosition - center).sqrMagnitude > (hitRadius * hitRadius))
                continue;

            hits.Add(gizmo);
        }

        hits.Sort((a, b) =>
        {
            int depthCompare = a.Depth.CompareTo(b.Depth);
            if (depthCompare != 0)
                return depthCompare;

            int radiusCompare = b.Radius.CompareTo(a.Radius);
            if (radiusCompare != 0)
                return radiusCompare;

            int selectedCompare = (b.IsSelected ? 1 : 0).CompareTo(a.IsSelected ? 1 : 0);
            if (selectedCompare != 0)
                return selectedCompare;

            return string.CompareOrdinal(a.NodeTag, b.NodeTag);
        });

        return hits;
    }

    private static float GetAuthorPreviewGizmoHitRadius(AgsAuthorPreviewGizmoEntry gizmo)
    {
        return Mathf.Max(gizmo.Radius + 4f, gizmo.Radius * 1.2f);
    }

    private static Vector2 GetAuthorPreviewGizmoHitClusterCenter(List<AgsAuthorPreviewGizmoEntry> hits)
    {
        if (hits == null || hits.Count == 0)
            return Vector2.zero;

        Vector2 sum = Vector2.zero;
        for (int i = 0; i < hits.Count; i++)
            sum += hits[i].ScreenRect.center;
        return sum / hits.Count;
    }

    private static float GetAuthorPreviewGizmoCycleRadiusSq(List<AgsAuthorPreviewGizmoEntry> hits)
    {
        if (hits == null || hits.Count == 0)
            return 0f;

        Vector2 center = GetAuthorPreviewGizmoHitClusterCenter(hits);
        float radius = 0f;
        for (int i = 0; i < hits.Count; i++)
        {
            AgsAuthorPreviewGizmoEntry gizmo = hits[i];
            float hitRadius = GetAuthorPreviewGizmoHitRadius(gizmo);
            float candidate = (gizmo.ScreenRect.center - center).magnitude + hitRadius + 2f;
            if (candidate > radius)
                radius = candidate;
        }

        radius = Mathf.Max(radius, 10f);
        return radius * radius;
    }

    // If the currently selected track has an exact keyframe at the given tick,
    // select it automatically. Called after track changes and scrubber moves so
    // the inspector always reflects the existing keyframe rather than creating a
    // phantom one on the next edit.
    private void TryAutoSelectKeyframeAtTick(int tick)
    {
        if (project == null || authorTrackIndex < 0)
            return;

        var stage = GetStage(project, authorStageIndex);
        var clip = GetClip(stage, authorRoleKey);
        if (clip?.tracks == null || authorTrackIndex >= clip.tracks.Count)
            return;

        var tr = clip.tracks[authorTrackIndex];
        var kf = FindKeyframeAtTick(tr, tick);
        if (kf != null)
        {
            int idx = tr.keys.IndexOf(kf);
            if (idx >= 0)
            {
                authorKeyIndex = idx;
                MarkAuthorPreviewSelectionDirty();
            }
        }
    }

    private bool SelectAuthorTrackByNodeTag(string nodeTag, bool clearKeySelection)
    {
        if (project == null || nodeTag.NullOrEmpty())
            return false;

        var stage = GetStage(project, authorStageIndex);
        var clip = GetClip(stage, authorRoleKey);
        if (clip?.tracks == null)
            return false;

        int nextIndex = -1;
        for (int i = 0; i < clip.tracks.Count; i++)
        {
            if (string.Equals(clip.tracks[i]?.nodeTag, nodeTag, StringComparison.Ordinal))
            {
                nextIndex = i;
                break;
            }
        }

        if (nextIndex < 0)
            return false;

        bool changed = authorTrackIndex != nextIndex;
        authorTrackIndex = nextIndex;
        if (changed || clearKeySelection)
            authorKeyIndex = -1;

        if (changed || clearKeySelection)
            MarkAuthorPreviewSelectionDirty();

        // Always attempt auto-select regardless of whether the track changed —
        // the user may have scrubbed to a different tick on the same track.
        // Reset authorKeyIndex first so we don't skip a keyframe that differs
        // from the currently selected one.
        authorKeyIndex = -1;
        TryAutoSelectKeyframeAtTick(authorPreviewTick);

        return changed || clearKeySelection;
    }

    private bool BeginAuthorPreviewGizmoDrag()
    {
        if (project == null || authorRuntime.ActiveGizmoNodeTag.NullOrEmpty())
            return false;

        if (preview.IsPlaying)
            preview.Pause();

        if (!SelectAuthorTrackByNodeTag(authorRuntime.ActiveGizmoNodeTag, clearKeySelection: false))
        {
            // keep the currently selected track if this gizmo already owns it
        }

        var stage = GetStage(project, authorStageIndex);
        var clip = GetClip(stage, authorRoleKey);
        if (clip?.tracks == null || authorTrackIndex < 0 || authorTrackIndex >= clip.tracks.Count)
            return false;

        AgsModel.Track track = clip.tracks[authorTrackIndex];
        AgsModel.Keyframe editKey = EnsureInspectorEditKeyframe(track, stage);
        if (editKey == null)
            return false;

        authorRuntime.GizmoDragging = true;
        authorRuntime.GizmoDragChangedData = false;
        authorRuntime.GizmoDragIsPropNode = IsPropNodeTag(authorRuntime.ActiveGizmoNodeTag);
        if (authorRuntime.GizmosByTag.TryGetValue(authorRuntime.ActiveGizmoNodeTag, out AgsAuthorPreviewGizmoEntry activeGizmo) && activeGizmo != null)
        {
            authorRuntime.GizmoDragPivotScreenPosition = activeGizmo.ScreenRect.center;
            authorRuntime.GizmoDragStartViewportUv = activeGizmo.ViewportUv;
            if (authorRuntime.GizmoDragMode == AgsAuthorPreviewGizmoDragMode.Translate)
            {
                authorRuntime.GizmoTranslateGrabViewportOffset = Vector2.zero;

                // Snapshot the basis vectors now so UpdateAuthorPreviewGizmoDrag can use
                // a fixed coordinate frame for the entire drag. Re-reading the basis each
                // frame lets it rotate as the node moves (because root angle is in the
                // projection), which turns the incremental solver into an unstable feedback
                // loop that spirals. Freezing it here eliminates that entirely.
                //
                // Special case: if we're dragging the Root node, ignore the captured or
                // borrowed basis entirely. The root's offset is in world space, but every
                // other node's basis is in root-local space (already rotated by root's
                // angle). If root IS directly captured, AgsPreviewNodeCapture now probes
                // world-space axes for it, so its own basis is correct. But if it's
                // synthetic (borrowed from Body), the borrowed basis is rotated and wrong.
                // Force world-aligned axes from the viewport rect for the synthetic case.
                // Snapshot the basis from the gizmo entry. For synthetic root,
                // BuildAuthorPreviewGizmoEntries has already overridden the basis with
                // world-aligned axes at the correct scale. For all other nodes the
                // captured local-space basis is correct.
                Vector2 snapBasisX = activeGizmo.ViewportBasisX;
                Vector2 snapBasisZ = activeGizmo.ViewportBasisZ;

                // For prop nodes: they may be absoluteTransform (world-space) or plain children.
                // Either way, use the prop's own captured basis rather than borrowing from a
                // parent node — borrowing a root-local basis onto a world-space prop produces
                // a misaligned coordinate frame that makes drag feel like it moves in the wrong
                // direction or at the wrong scale.
                //
                // However: the captured basis vectors include the prop's own angle rotation,
                // because they are sampled from the node's actual world transform. The drag
                // offset is written in PARENT-local space (or world space for absoluteTransform),
                // not prop-local space, so using the rotated prop basis maps mouse movement to
                // prop-local axes rather than the expected world/parent axes. We counter-rotate
                // the basis by -editKey.angle to remove the prop's self-rotation and recover
                // parent-space drag axes.
                bool draggingProp = IsPropNodeTag(authorRuntime.ActiveGizmoNodeTag);
                bool draggingRoot = string.Equals(authorRuntime.ActiveGizmoNodeTag, "Root", StringComparison.Ordinal);
                if (draggingProp && snapBasisX.sqrMagnitude > 0.0000001f && snapBasisZ.sqrMagnitude > 0.0000001f)
                {
                    // Good: prop has its own valid basis, use it as-is.
                    // Angle correction applied below.
                }
                else if (!draggingProp && (snapBasisX.sqrMagnitude <= 0.0000001f || snapBasisZ.sqrMagnitude <= 0.0000001f))
                {
                    string basisTag = !activeGizmo.DragBasisNodeTag.NullOrEmpty() ? activeGizmo.DragBasisNodeTag : activeGizmo.NodeTag;
                    TryResolveAuthorPreviewTranslationBasis(basisTag, out snapBasisX, out snapBasisZ);
                }
                else if (snapBasisX.sqrMagnitude <= 0.0000001f || snapBasisZ.sqrMagnitude <= 0.0000001f)
                {
                    string basisTag = !activeGizmo.DragBasisNodeTag.NullOrEmpty() ? activeGizmo.DragBasisNodeTag : activeGizmo.NodeTag;
                    TryResolveAuthorPreviewTranslationBasis(basisTag, out snapBasisX, out snapBasisZ);
                }

                // The captured viewport basis includes the node's own angle rotation because it
                // is sampled from the node's actual world transform. But editKey.offset is written
                // in parent-local space (or world space for absoluteTransform props), not node-local
                // space. Counter-rotate the basis by +editKey.angle (RimWorld angles are clockwise,
                // which cancels the standard CCW rotation matrix sign) so drag axes align with the
                // space that offset actually lives in. Root nodes are exempt: their basis is already
                // world-aligned by construction and carries no self-rotation to strip.
                if (!draggingRoot
                    && Mathf.Abs(editKey.angle) > 0.0001f
                    && snapBasisX.sqrMagnitude > 0.0000001f
                    && snapBasisZ.sqrMagnitude > 0.0000001f)
                {
                    float rad = editKey.angle * Mathf.Deg2Rad;
                    float cos = Mathf.Cos(rad);
                    float sin = Mathf.Sin(rad);
                    snapBasisX = new Vector2(snapBasisX.x * cos - snapBasisX.y * sin,
                                             snapBasisX.x * sin + snapBasisX.y * cos);
                    snapBasisZ = new Vector2(snapBasisZ.x * cos - snapBasisZ.y * sin,
                                             snapBasisZ.x * sin + snapBasisZ.y * cos);
                }
                authorRuntime.GizmoDragStartBasisX = snapBasisX;
                authorRuntime.GizmoDragStartBasisZ = snapBasisZ;
            }
        }

        switch (authorRuntime.GizmoDragMode)
        {
            case AgsAuthorPreviewGizmoDragMode.Rotate:
                authorRuntime.GizmoRotateDragAccumulatedAngle = editKey.angle;
                authorRuntime.GizmoRotateDragLastMouseAngle = GetAuthorPreviewMouseAngle(authorRuntime.GizmoDragPivotScreenPosition, authorRuntime.GizmoPressMousePosition);
                break;
            case AgsAuthorPreviewGizmoDragMode.Translate:
            default:
                authorRuntime.GizmoDragStartOffset = editKey.offset;
                break;
        }

        MarkAuthorPreviewSelectionDirty();
        return true;
    }

    private void UpdateAuthorPreviewGizmoDrag(Vector2 mousePosition)
    {
        if (!authorRuntime.GizmoDragging || project == null || authorRuntime.ActiveGizmoNodeTag.NullOrEmpty())
            return;

        var stage = GetStage(project, authorStageIndex);
        var clip = GetClip(stage, authorRoleKey);
        if (clip?.tracks == null || authorTrackIndex < 0 || authorTrackIndex >= clip.tracks.Count)
            return;

        AgsModel.Track track = clip.tracks[authorTrackIndex];
        AgsModel.Keyframe editKey = EnsureInspectorEditKeyframe(track, stage);
        if (editKey == null)
            return;

        if (authorRuntime.GizmoDragMode == AgsAuthorPreviewGizmoDragMode.Rotate)
        {
            float currentMouseAngle = GetAuthorPreviewMouseAngle(authorRuntime.GizmoDragPivotScreenPosition, mousePosition);
            float stepDelta = Mathf.DeltaAngle(authorRuntime.GizmoRotateDragLastMouseAngle, currentMouseAngle);
            if (Mathf.Abs(stepDelta) <= 0.0001f)
                return;

            float nextAngle = authorRuntime.GizmoRotateDragAccumulatedAngle + stepDelta;
            if (Mathf.Abs(nextAngle - editKey.angle) <= 0.0001f)
            {
                authorRuntime.GizmoRotateDragLastMouseAngle = currentMouseAngle;
                authorRuntime.GizmoRotateDragAccumulatedAngle = nextAngle;
                return;
            }

            authorRuntime.GizmoRotateDragLastMouseAngle = currentMouseAngle;
            authorRuntime.GizmoRotateDragAccumulatedAngle = nextAngle;
            editKey.angle = NormalizeAngleDeg(nextAngle);
            authorRuntime.GizmoDragChangedData = true;
            MarkAuthorPreviewStageDirty(authorStageIndex);
            ShowAuthorStageAtTick(authorStageIndex, Mathf.Clamp(authorPreviewTick, 0, Mathf.Max(1, stage?.durationTicks ?? 1)), seekIfPlaying: false);
            return;
        }

        Rect textureRect = preview.LastViewportTextureRect;
        if (textureRect.width <= 1f || textureRect.height <= 1f)
            return;

        const float solveThresholdSq = 0.36f;

        // Use the basis frozen at drag-start so the coordinate frame doesn't
        // rotate as the node moves. Reading the live basis each frame lets root
        // angle corrupt the gain matrix incrementally, causing the spiral.
        Vector2 basisX = authorRuntime.GizmoDragStartBasisX;
        Vector2 basisZ = authorRuntime.GizmoDragStartBasisZ;
        if (basisX.sqrMagnitude <= 0.0000001f && basisZ.sqrMagnitude <= 0.0000001f)
        {
            // Fallback: resolve live if we somehow have no snapshot.
            AgsAuthorPreviewGizmoEntry solveGizmo = null;
            if (!authorRuntime.ActiveGizmoNodeTag.NullOrEmpty())
                authorRuntime.GizmosByTag.TryGetValue(authorRuntime.ActiveGizmoNodeTag, out solveGizmo);
            if (solveGizmo == null)
                solveGizmo = GetSelectedAuthorPreviewGizmo();
            if (solveGizmo == null)
                return;
            string fallbackTag = !solveGizmo.DragBasisNodeTag.NullOrEmpty() ? solveGizmo.DragBasisNodeTag : solveGizmo.NodeTag;
            if (!TryResolveAuthorPreviewTranslationBasis(fallbackTag, out basisX, out basisZ))
                return;
        }

        // Compute total screen displacement from the moment the drag started,
        // then resolve that to an absolute world offset from GizmoDragStartOffset.
        // This replaces the per-frame incremental delta, which spiralled because
        // damping left residual error that got re-solved with a rotated basis.
        Vector2 totalScreenDisp = mousePosition - authorRuntime.GizmoPressMousePosition;
        if (totalScreenDisp.sqrMagnitude <= solveThresholdSq)
            return;

        Vector2 panelBasisX = ViewportBasisToPanelDelta(textureRect, basisX);
        Vector2 panelBasisZ = ViewportBasisToPanelDelta(textureRect, basisZ);
        if (!TrySolveAuthorPreviewOffsetDelta(totalScreenDisp, panelBasisX, panelBasisZ, out float totalDeltaX, out float totalDeltaZ))
            return;

        Vector3 nextOffset = authorRuntime.GizmoDragStartOffset;
        if (authorRuntime.GizmoDragIsPropNode)
        {
            nextOffset.x = Mathf.Clamp(nextOffset.x + totalDeltaX, AuthorPropOffsetClampXMin, AuthorPropOffsetClampXMax);
            nextOffset.z = Mathf.Clamp(nextOffset.z + totalDeltaZ, AuthorPropOffsetClampZMin, AuthorPropOffsetClampZMax);
        }
        else
        {
            nextOffset.x = Mathf.Clamp(nextOffset.x + totalDeltaX, AuthorTrackOffsetClampXMin, AuthorTrackOffsetClampXMax);
            nextOffset.z = Mathf.Clamp(nextOffset.z + totalDeltaZ, AuthorTrackOffsetClampZMin, AuthorTrackOffsetClampZMax);
        }

        if ((nextOffset - editKey.offset).sqrMagnitude <= 0.0000001f)
            return;

        editKey.offset = nextOffset;
        MarkAuthorPreviewStageDirty(authorStageIndex);
        int stageDuration = Mathf.Clamp(authorPreviewTick, 0, Mathf.Max(1, stage?.durationTicks ?? 1));
        ShowAuthorStageAtTick(authorStageIndex, stageDuration, seekIfPlaying: false);
        authorRuntime.GizmoDragChangedData = true;
    }

    private bool TryApplyValidatedAuthorPreviewTranslateOffset(AgsModel.Keyframe editKey, AgsModel.StageSpec stage, Vector3 candidateOffset)
    {
        Vector3 startOffset = editKey.offset;
        int stageDuration = Mathf.Clamp(authorPreviewTick, 0, Mathf.Max(1, stage?.durationTicks ?? 1));

        for (int attempt = 0; attempt < 4; attempt++)
        {
            float t = 1f / (1 << attempt);
            Vector3 trialOffset = Vector3.Lerp(startOffset, candidateOffset, t);
            if ((trialOffset - editKey.offset).sqrMagnitude <= 0.0000001f && attempt > 0)
                continue;

            editKey.offset = trialOffset;
            MarkAuthorPreviewStageDirty(authorStageIndex);
            ShowAuthorStageAtTick(authorStageIndex, stageDuration, seekIfPlaying: false);
            if (!preview.RefreshNodeCaptureFromLastViewportRect())
                continue;

            RebuildAuthorPreviewGizmoSnapshot();
            if (TryGetActiveAuthorPreviewGizmoScreenPoint(out Vector2 currentPoint) && IsPointInsideAuthorPreviewViewport(preview.LastViewportTextureRect, currentPoint))
                return true;
        }

        editKey.offset = startOffset;
        MarkAuthorPreviewStageDirty(authorStageIndex);
        ShowAuthorStageAtTick(authorStageIndex, stageDuration, seekIfPlaying: false);
        if (preview.RefreshNodeCaptureFromLastViewportRect())
            RebuildAuthorPreviewGizmoSnapshot();
        return false;
    }

    private bool TryGetActiveAuthorPreviewGizmoScreenPoint(out Vector2 screenPoint)
    {
        screenPoint = Vector2.zero;
        string activeNodeTag = !authorRuntime.ActiveGizmoNodeTag.NullOrEmpty()
            ? authorRuntime.ActiveGizmoNodeTag
            : GetSelectedAuthorPreviewGizmo()?.NodeTag;
        if (activeNodeTag.NullOrEmpty())
            return false;

        if (!authorRuntime.GizmosByTag.TryGetValue(activeNodeTag, out AgsAuthorPreviewGizmoEntry activeGizmo) || activeGizmo == null)
            return false;

        screenPoint = activeGizmo.ScreenRect.center;
        return true;
    }

    private static bool TrySolveAuthorPreviewOffsetDelta(Vector2 uvDelta, Vector2 basisX, Vector2 basisZ, out float deltaX, out float deltaZ)
    {
        float det = (basisX.x * basisZ.y) - (basisX.y * basisZ.x);
        if (Mathf.Abs(det) > 0.000001f)
        {
            deltaX = ((uvDelta.x * basisZ.y) - (uvDelta.y * basisZ.x)) / det;
            deltaZ = ((basisX.x * uvDelta.y) - (basisX.y * uvDelta.x)) / det;
            return true;
        }

        float bx = basisX.sqrMagnitude;
        float bz = basisZ.sqrMagnitude;
        if (bx >= bz && bx > 0.0000001f)
        {
            deltaX = Vector2.Dot(uvDelta, basisX) / bx;
            deltaZ = 0f;
            return true;
        }

        if (bz > 0.0000001f)
        {
            deltaX = 0f;
            deltaZ = Vector2.Dot(uvDelta, basisZ) / bz;
            return true;
        }

        deltaX = 0f;
        deltaZ = 0f;
        return false;
    }

    private void EndAuthorPreviewGizmoDrag()
    {
        bool changedData = authorRuntime.GizmoDragChangedData;
        ClearAuthorPreviewGizmoPointerState(preserveCycleState: true);
        if (changedData)
            QueueAuthorSave();
    }

    private void ClearAuthorPreviewGizmoPointerState(bool preserveCycleState)
    {
        authorRuntime.GizmoPressActive = false;
        authorRuntime.GizmoDragging = false;
        authorRuntime.ActiveGizmoNodeTag = null;
        authorRuntime.GizmoDragMode = AgsAuthorPreviewGizmoDragMode.None;
        authorRuntime.GizmoDragChangedData = false;
        authorRuntime.GizmoDragIsPropNode = false;
        authorRuntime.GizmoDragStartOffset = Vector3.zero;
        authorRuntime.GizmoDragStartViewportUv = Vector2.zero;
        authorRuntime.GizmoTranslateGrabViewportOffset = Vector2.zero;
        authorRuntime.GizmoDragStartBasisX = Vector2.zero;
        authorRuntime.GizmoDragStartBasisZ = Vector2.zero;
        authorRuntime.GizmoDragPivotScreenPosition = Vector2.zero;
        authorRuntime.GizmoRotateDragLastMouseAngle = 0f;
        authorRuntime.GizmoRotateDragAccumulatedAngle = 0f;
        authorRuntime.GizmoPressMousePosition = Vector2.zero;

        if (!preserveCycleState)
        {
            authorRuntime.LastOverlapSignature = null;
            authorRuntime.LastOverlapCycleIndex = -1;
            authorRuntime.LastOverlapMousePosition = Vector2.zero;
            authorRuntime.LastOverlapClickTime = -100f;
        }
    }

    private void DrawAuthorPreviewGizmos()
    {
        if (authorRuntime.Gizmos.Count == 0)
            return;

        AgsAuthorPreviewGizmoEntry selectedGizmo = GetSelectedAuthorPreviewGizmo();
        if (selectedGizmo != null)
            DrawAuthorPreviewRotationRing(selectedGizmo);

        for (int i = 0; i < authorRuntime.Gizmos.Count; i++)
        {
            AgsAuthorPreviewGizmoEntry gizmo = authorRuntime.Gizmos[i];
            if (gizmo == null)
                continue;

            Texture2D texture = GetAuthorPreviewGizmoTexture(gizmo.VisualKind);
            if (texture == null)
                continue;

            Rect outlineRect = new Rect(
                gizmo.ScreenRect.x - 1.5f,
                gizmo.ScreenRect.y - 1.5f,
                gizmo.ScreenRect.width + 3f,
                gizmo.ScreenRect.height + 3f);

            Color prevColor = GUI.color;
            GUI.color = gizmo.OutlineColor;
            GUI.DrawTexture(outlineRect, texture, ScaleMode.StretchToFill, true);
            GUI.color = gizmo.FillColor;
            GUI.DrawTexture(gizmo.ScreenRect, texture, ScaleMode.StretchToFill, true);
            GUI.color = prevColor;
        }
    }

    private void DrawAuthorPreviewRotationRing(AgsAuthorPreviewGizmoEntry gizmo)
    {
        Texture2D texture = GetAuthorPreviewGizmoRingTexture();
        if (gizmo == null || texture == null)
            return;

        float outerRadius = GetAuthorPreviewRotationRingOuterRadius(gizmo);
        Vector2 center = gizmo.ScreenRect.center;
        Rect ringRect = new Rect(center.x - outerRadius, center.y - outerRadius, outerRadius * 2f, outerRadius * 2f);
        Rect outlineRect = new Rect(ringRect.x - 1.5f, ringRect.y - 1.5f, ringRect.width + 3f, ringRect.height + 3f);
        Vector2 hoverMousePosition = Event.current != null ? Event.current.mousePosition : new Vector2(-9999f, -9999f);
        bool hover = IsPointInAuthorPreviewRotationRing(gizmo, hoverMousePosition);
        bool active = authorRuntime.GizmoPressActive
            && string.Equals(authorRuntime.ActiveGizmoNodeTag, gizmo.NodeTag, StringComparison.Ordinal)
            && authorRuntime.GizmoDragMode == AgsAuthorPreviewGizmoDragMode.Rotate;

        Color baseColor = gizmo.FillColor;
        Color outlineColor = active
            ? new Color(1f, 1f, 1f, 0.88f)
            : hover
                ? new Color(1f, 1f, 1f, 0.72f)
                : new Color(baseColor.r * 0.6f, baseColor.g * 0.6f, baseColor.b * 0.6f, 0.42f);
        Color fillColor = active
            ? new Color(baseColor.r, baseColor.g, baseColor.b, 0.34f)
            : hover
                ? new Color(baseColor.r, baseColor.g, baseColor.b, 0.24f)
                : new Color(baseColor.r, baseColor.g, baseColor.b, 0.14f);

        Color prevColor = GUI.color;
        GUI.color = outlineColor;
        GUI.DrawTexture(outlineRect, texture, ScaleMode.StretchToFill, true);
        GUI.color = fillColor;
        GUI.DrawTexture(ringRect, texture, ScaleMode.StretchToFill, true);
        GUI.color = prevColor;
    }

    private Texture2D GetAuthorPreviewGizmoTexture(AgsAuthorPreviewGizmoVisualKind visualKind)
    {
        switch (visualKind)
        {
            case AgsAuthorPreviewGizmoVisualKind.Ring:
                return GetAuthorPreviewGizmoRingTexture();
            default:
                return GetAuthorPreviewGizmoDiscTexture();
        }
    }

    private Texture2D GetAuthorPreviewGizmoDiscTexture()
    {
        if (authorGizmoDiscTexture != null)
            return authorGizmoDiscTexture;

        authorGizmoDiscTexture = BuildAuthorPreviewGizmoTexture("AgsAuthorPreviewGizmoDisc", ring: false);
        return authorGizmoDiscTexture;
    }

    private Texture2D GetAuthorPreviewGizmoRingTexture()
    {
        if (authorGizmoRingTexture != null)
            return authorGizmoRingTexture;

        authorGizmoRingTexture = BuildAuthorPreviewGizmoTexture("AgsAuthorPreviewGizmoRing", ring: true);
        return authorGizmoRingTexture;
    }

    private static Texture2D BuildAuthorPreviewGizmoTexture(string name, bool ring)
    {
        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.ARGB32, false)
        {
            name = name,
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        float outerRadius = (size - 2) * 0.5f;
        float innerRadius = outerRadius * 0.52f;
        float center = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float distance = Mathf.Sqrt((dx * dx) + (dy * dy));
                float alphaOuter = Mathf.Clamp01(outerRadius + 0.75f - distance);
                float alpha = alphaOuter;
                if (ring)
                {
                    float alphaInner = Mathf.Clamp01(distance - (innerRadius - 0.75f));
                    alpha *= alphaInner;
                }

                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply(updateMipmaps: false, makeNoLongerReadable: true);
        return tex;
    }

    private static Color GetAuthorPreviewGizmoColor(string nodeTag)
    {
        switch (nodeTag)
        {
            case "Root":
                return new Color(0.22f, 0.92f, 0.52f, 1f);
            case "Body":
                return new Color(0.28f, 0.72f, 1f, 1f);
            case "Head":
                return new Color(0.98f, 0.73f, 0.26f, 1f);
            case "Eyes":
                return new Color(0.84f, 0.40f, 0.98f, 1f);
            case "Mouth":
                return new Color(1f, 0.45f, 0.58f, 1f);
        }

        int hash = 17;
        if (!nodeTag.NullOrEmpty())
        {
            for (int i = 0; i < nodeTag.Length; i++)
                hash = (hash * 31) + nodeTag[i];
        }

        float hue = Mathf.Abs(hash % 1000) / 1000f;
        return Color.HSVToRGB(hue, 0.58f, 0.96f);
    }


    /// <summary>
    /// Wraps an angle in degrees to the range (-360, 360] so the stored value
    /// stays within one full rotation without changing the effective orientation.
    /// The accumulator used during drag is intentionally left unbounded so the
    /// drag stays smooth; only the committed keyframe value is normalised.
    /// </summary>
    private static float NormalizeAngleDeg(float angle)
    {
        angle = angle % 360f;
        // C# % preserves sign, giving a result in (-360, 360).
        // Fold into (-180, 180].
        if (angle > 180f)  angle -= 360f;
        else if (angle <= -180f) angle += 360f;
        // Snap -0 / tiny-epsilon edge case to 0.
        if (Mathf.Abs(angle) < 0.0001f)
            angle = 0f;
        return angle;
    }

    private sealed class GizmoAggregate
    {
        public Vector2 SumUv;
        public Vector2 SumBasisX;
        public Vector2 SumBasisZ;
        public float SumCameraDepth;
        public int SampleCount;
        public int MinDepth = int.MaxValue;
        public bool IsSynthetic;
        public string DragBasisNodeTag;
    }

    private void DrawAuthorPreviewSlot(Rect rect, AuthorPreviewSlot slot)
    {
        if (slot == null)
            return;

        var header = rect.TopPartPixels(24f);
        Widgets.Label(header, slot.Label);

        var body = rect;
        body.yMin += 26f;

        try
        {
            int w = Mathf.Clamp((int)body.width, 128, 2048);
            int h = Mathf.Clamp((int)body.height, 128, 2048);
            slot.Renderer.EnsureSize(w, h);
            using (new WorkshopRenderContext.Scope(active: true, tick: authorPreviewTick))
            {
                slot.Renderer.RenderPawn(slot.Pawn);
            }

            var tex = slot.Renderer.Texture;
            if (tex != null)
                GUI.DrawTexture(body, tex, ScaleMode.ScaleToFit);
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "AgsDrawPreviewPanel:1",
                "AgsDrawPreviewPanel ignored a non-fatal editor exception.",
                ex);
            Widgets.Label(body, "(render failed)");
        }
    }
}
