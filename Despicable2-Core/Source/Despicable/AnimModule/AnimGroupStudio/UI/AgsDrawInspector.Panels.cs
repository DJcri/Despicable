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
// Guardrail-Reason: Co-located inspector panels share one AGS editing surface; split points stay by panel, not arbitrary line count.
public partial class Dialog_AnimGroupStudio
{
            private void DrawTrackList(Rect rect, AgsModel.StageSpec stage)
            {
                var ctx = frameworkCtx;
                var parts = D2Section.Layout(
                    ctx,
                    rect,
                    new D2Section.Spec(
                        "TrackList",
                        headerHeight: SubsectionHeaderHeight,
                        footerHeight: ctx.Style.RowHeight + 2f,
                        soft: true,
                        pad: true,
                        drawBackground: true,
                        padOverride: ctx.Style.Pad));
                D2Section.DrawCaptionStrip(ctx, parts.Header, "Tracks", "TrackList/Header", GameFont.Small);

                var clip = GetClip(stage, authorRoleKey);
                EnsureClip(clip, stage.durationTicks);

                int prevIndex = authorTrackIndex;
                if (clip.tracks == null) clip.tracks = new List<AgsModel.Track>();
                var authorTrackScrollLocal = authorTrackScroll;
                var authorTrackIndexLocal = authorTrackIndex;
                D2ListView.Draw(
                    ctx,
                    parts.Body,
                    ref authorTrackScrollLocal,
                    clip.tracks,
                    ref authorTrackIndexLocal,
                    (rowCtx, rowRect, item, index, selected) =>
                {
                    var labelRect = rowRect.ContractedBy(6f, 0f);
                    if (selected && rowCtx != null && rowCtx.Pass == UIPass.Draw)
                    {
                        var prev = GUI.color;
                        GUI.color = SelectedGreen;
                        Widgets.Label(labelRect, item?.nodeTag ?? "(no tag)");
                        GUI.color = prev;
                    }
                    else if (rowCtx == null || rowCtx.Pass == UIPass.Draw)
                    {
                        Widgets.Label(labelRect, item?.nodeTag ?? "(no tag)");
                    }
                    },
                    rowHeightOverride: 28f,
                    rowGap: 3f,
                    zebra: true,
                    label: "TrackList/Rows");
                authorTrackScroll = authorTrackScrollLocal;
                authorTrackIndex = authorTrackIndexLocal;
                if (authorTrackIndex != prevIndex)
                    authorKeyIndex = -1;

                var footerH = new HRow(ctx, parts.Footer);
                if (DrawIconButton(
                    ctx,
                    footerH.NextFixed(ctx.Style.RowHeight, UIRectTag.Button, "TrackList/Add"),
                    D2VanillaTex.Plus,
                    "Add track",
                    "TrackList/Add"))
                {
                    var opts = new List<FloatMenuOption>();
                    var tags = DefDatabase<PawnRenderNodeTagDef>.AllDefsListForReading;
                    if (!tags.NullOrEmpty())
                    {
                        foreach (var d in tags)
                        {
                            if (d == null) continue;
                            string tagName = d.defName;
                            opts.Add(new FloatMenuOption(tagName, () =>
                            {
                                clip.tracks.Add(new AgsModel.Track { nodeTag = tagName, keys = new List<AgsModel.Keyframe>() });
                                authorTrackIndex = clip.tracks.Count - 1;
                                authorKeyIndex = -1;
                                EnsureTrackDefaults(clip.tracks[authorTrackIndex], stage.durationTicks);
                                TrySaveProjects();
                            }));
                        }
                    }
                    else
                    {
                        opts.Add(new FloatMenuOption("(no tags)", null));
                    }
                    Find.WindowStack.Add(new FloatMenu(opts));
                }

                Rect removeRect = footerH.NextFixed(ctx.Style.RowHeight, UIRectTag.Button, "TrackList/Remove");
                bool canRemove = !clip.tracks.NullOrEmpty() && authorTrackIndex >= 0 && authorTrackIndex < clip.tracks.Count;
                if (canRemove)
                {
                    if (DrawIconButton(ctx, removeRect, D2VanillaTex.Delete, "Remove track", "TrackList/Remove"))
                    {
                        clip.tracks.RemoveAt(authorTrackIndex);
                        authorTrackIndex = Mathf.Clamp(authorTrackIndex, 0, clip.tracks.Count - 1);
                        authorKeyIndex = -1;
                        TrySaveProjects();
                    }
                }
                else
                {
                    DrawIconButton(ctx, removeRect, D2VanillaTex.Delete, "Remove track", "TrackList/RemoveDisabled", enabled: false, disabledReason: "Select a track to remove it.");
                }
            }
            private void DrawKeyframeList(Rect rect, AgsModel.StageSpec stage)
            {
                var ctx = frameworkCtx;
                var parts = D2Section.Layout(
                    ctx,
                    rect,
                    new D2Section.Spec(
                        "KeyframeList",
                        headerHeight: SubsectionHeaderHeight,
                        footerHeight: ctx.Style.RowHeight + 2f,
                        soft: true,
                        pad: true,
                        drawBackground: true,
                        padOverride: ctx.Style.Pad));
                D2Section.DrawCaptionStrip(ctx, parts.Header, "Keyframes", "KeyframeList/Header", GameFont.Small);

                var tr = GetSelectedTrack(stage, authorRoleKey);
                if (tr == null)
                {
                    D2Widgets.Label(ctx, parts.Body, "Select a track.", "KeyframeList/Empty");
                    return;
                }
                EnsureTrackDefaults(tr, stage.durationTicks);

                int prevKeyIndex = authorKeyIndex;
                var authorKeyScrollLocal = authorKeyScroll;
                var authorKeyIndexLocal = authorKeyIndex;
                D2ListView.Draw(
                    ctx,
                    parts.Body,
                    ref authorKeyScrollLocal,
                    tr.keys,
                    ref authorKeyIndexLocal,
                    (rowCtx, rowRect, item, index, selected) =>
                {
                    var labelRect = rowRect.ContractedBy(6f, 0f);
                    string label = item == null ? "(null)" : ("t=" + item.tick);
                    if (selected && rowCtx != null && rowCtx.Pass == UIPass.Draw)
                    {
                        var prev = GUI.color;
                        GUI.color = SelectedGreen;
                        Widgets.Label(labelRect, label);
                        GUI.color = prev;
                    }
                    else if (rowCtx == null || rowCtx.Pass == UIPass.Draw)
                    {
                        Widgets.Label(labelRect, label);
                    }
                    },
                    rowHeightOverride: 26f,
                    rowGap: 3f,
                    zebra: true,
                    label: "KeyframeList/Rows");
                authorKeyScroll = authorKeyScrollLocal;
                authorKeyIndex = authorKeyIndexLocal;

                if (authorKeyIndex != prevKeyIndex && authorKeyIndex >= 0 && authorKeyIndex < tr.keys.Count)
                    InspectSelectedAuthorKeyframe(tr, stage);

                var footerH = new HRow(ctx, parts.Footer);
                if (DrawIconButton(
                    ctx,
                    footerH.NextFixed(ctx.Style.RowHeight, UIRectTag.Button, "KeyframeList/Add"),
                    D2VanillaTex.Plus,
                    "Add keyframe",
                    "KeyframeList/Add"))
                {
                    AgsModel.Keyframe seed = null;
                    if (authorKeyIndex >= 0 && authorKeyIndex < tr.keys.Count)
                        seed = tr.keys[authorKeyIndex];
                    else if (!tr.keys.NullOrEmpty())
                        seed = tr.keys[tr.keys.Count - 1];

                    int newTick = ResolveNewKeyframeTick(tr, stage, seed);
                    var nk = CloneKeyframe(seed) ?? CreateDefaultKeyframe(newTick);
                    nk.tick = newTick;
                    tr.keys.Add(nk);
                    SortClampKeys(tr, stage.durationTicks);
                    authorKeyIndex = tr.keys.IndexOf(nk);
                    InspectSelectedAuthorKeyframe(tr, stage);
                    TrySaveProjects();
                }

                Rect removeRect = footerH.NextFixed(ctx.Style.RowHeight, UIRectTag.Button, "KeyframeList/Remove");
                bool canRemove = tr.keys.Count > 2 && authorKeyIndex >= 0 && authorKeyIndex < tr.keys.Count;
                if (canRemove)
                {
                    if (DrawIconButton(ctx, removeRect, D2VanillaTex.Delete, "Remove keyframe", "KeyframeList/Remove"))
                    {
                        tr.keys.RemoveAt(authorKeyIndex);
                        authorKeyIndex = Mathf.Clamp(authorKeyIndex, 0, tr.keys.Count - 1);
                        TrySaveProjects();
                    }
                }
                else
                {
                    DrawIconButton(ctx, removeRect, D2VanillaTex.Delete, "Remove keyframe", "KeyframeList/RemoveDisabled", enabled: false, disabledReason: "A track needs at least two keyframes.");
                }
            }
            private void InspectSelectedAuthorKeyframe(AgsModel.Track tr, AgsModel.StageSpec stage)
            {
                if (tr?.keys == null || stage == null || authorKeyIndex < 0 || authorKeyIndex >= tr.keys.Count)
                    return;

                authorPreviewTick = Mathf.Clamp(tr.keys[authorKeyIndex].tick, 0, Mathf.Max(1, stage.durationTicks));
                preview.SelectedStageIndex = authorStageIndex;
                preview.ShowSelectedStageAtTick(authorPreviewTick);
            }

            private void DrawKeyframeInspector(Rect rect, AgsModel.StageSpec stage)
            {
                var ctx = frameworkCtx;
                var parts = D2Section.Layout(
                    ctx,
                    rect,
                    new D2Section.Spec(
                        "KeyframeInspector",
                        headerHeight: SectionHeaderHeight,
                        toolbarHeight: ctx.Style.RowHeight,
                        soft: true,
                        pad: true,
                        drawBackground: true,
                        padOverride: ctx.Style.Pad));
                D2Section.DrawCaptionStrip(ctx, parts.Header, "Inspector", "KeyframeInspector/Header", GameFont.Medium);

                var tr = GetSelectedTrack(stage, authorRoleKey);
                bool hasSelection = tr != null && !tr.keys.NullOrEmpty() && authorKeyIndex >= 0 && authorKeyIndex < tr.keys.Count;
                AgsModel.Keyframe previousKey = hasSelection ? GetPreviousKeyframe(tr, authorKeyIndex) : null;
                bool canCopy = hasSelection;
                bool canPaste = hasSelection && authorKeyClipboard != null;
                bool canReset = hasSelection;

                var toolbarH = new HRow(ctx, parts.Toolbar);
                float resetWidth = Mathf.Clamp(parts.Toolbar.width * 0.36f, 92f, 128f);
                Rect resetRect = toolbarH.NextFixed(resetWidth, UIRectTag.Button, "KeyframeInspector/ToolbarReset");
                if (canReset)
                {
                    if (D2Widgets.ButtonText(ctx, resetRect, "Reset", "KeyframeInspector/ToolbarReset"))
                    {
                        ResetKeyframeFromPreviousOrDefault(tr, authorKeyIndex);
                        TrySaveProjects();
                    }
                    TooltipHandler.TipRegion(resetRect, previousKey != null
                        ? "Reset this keyframe to the previous keyframe's values."
                        : "Reset this keyframe to the default values used for the first keyframe on a track.");
                }
                else
                {
                    ctx.Record(resetRect, UIRectTag.Button, "KeyframeInspector/ToolbarResetDisabled");
                    if (ctx.Pass == UIPass.Draw)
                    {
                        using (new GUIEnabledScope(false)) Widgets.ButtonText(resetRect, "Reset");
                    }
                    TooltipHandler.TipRegion(resetRect, "Select a keyframe to reset it.");
                }

                Rect copyRect = toolbarH.NextFixed(ctx.Style.RowHeight, UIRectTag.Button, "KeyframeInspector/ToolbarCopy");
                if (canCopy)
                {
                    if (DrawIconButton(ctx, copyRect, D2VanillaTex.Copy, "Copy keyframe values", "KeyframeInspector/ToolbarCopy"))
                        authorKeyClipboard = CloneKeyframe(tr.keys[authorKeyIndex]);
                }
                else
                {
                    DrawIconButton(ctx, copyRect, D2VanillaTex.Copy, "Copy keyframe values", "KeyframeInspector/ToolbarCopyDisabled", enabled: false, disabledReason: "Select a keyframe to copy it.");
                }

                Rect pasteRect = toolbarH.NextFixed(ctx.Style.RowHeight, UIRectTag.Button, "KeyframeInspector/ToolbarPaste");
                if (canPaste)
                {
                    if (DrawIconButton(ctx, pasteRect, D2VanillaTex.Paste, "Paste copied keyframe values", "KeyframeInspector/ToolbarPaste"))
                    {
                        int keepTick = tr.keys[authorKeyIndex].tick;
                        CopyKeyframeData(authorKeyClipboard, tr.keys[authorKeyIndex], includeTick: false);
                        tr.keys[authorKeyIndex].tick = keepTick;
                        TrySaveProjects();
                    }
                }
                else
                {
                    string disabledReason = hasSelection
                        ? "Copy a keyframe first."
                        : "Select a keyframe to paste onto it.";
                    DrawIconButton(ctx, pasteRect, D2VanillaTex.Paste, "Paste copied keyframe values", "KeyframeInspector/ToolbarPasteDisabled", enabled: false, disabledReason: disabledReason);
                }

                if (!hasSelection)
                {
                    D2Widgets.Label(ctx, parts.Body, tr == null ? "Select a track." : "Select a keyframe.", "KeyframeInspector/Empty");
                    return;
                }

                var k = tr.keys[authorKeyIndex];
                if (k == null) return;

                var authorInspectorScrollLocal = authorInspectorScroll;
                var authorInspectorContentHeightLocal = authorInspectorContentHeight;
                D2ScrollView.Draw(ctx, parts.Body, ref authorInspectorScrollLocal, ref authorInspectorContentHeightLocal, (UIContext scrollCtx, ref VStack v) =>
                {
                    Rect tickRow = v.NextRow(UIRectTag.Input, "Inspector/TickRow");
                    var tickH = new HRow(scrollCtx, tickRow);
                    float tickLabelW = Mathf.Clamp(tickRow.width * 0.22f, 48f, 64f);
                    D2Widgets.Label(scrollCtx, tickH.NextFixed(tickLabelW, UIRectTag.Label, "Inspector/TickLabel"), "Tick", "Inspector/TickLabel");
                    int tVal = k.tick;
                    string tStr = tVal.ToString();
                    var tickRect = tickH.Remaining(UIRectTag.TextField, "Inspector/TickField");
                    scrollCtx.Record(tickRect, UIRectTag.TextField, "Inspector/TickField");
                    if (scrollCtx.Pass == UIPass.Draw)
                        Widgets.TextFieldNumeric(tickRect, ref tVal, ref tStr, 0, stage.durationTicks);
                    if (tVal != k.tick)
                    {
                        if (TrySetUniqueKeyframeTick(tr, k, tVal, stage.durationTicks, out var tickError))
                        {
                            authorKeyIndex = tr.keys.IndexOf(k);
                            TrySaveProjects();
                        }
                        else if (!tickError.NullOrEmpty())
                        {
                            Messages.Message(tickError, MessageTypeDefOf.RejectInput, false);
                        }
                    }
                    if (!authorPreviewPlaying && authorPreviewTick != k.tick)
                    {
                        authorPreviewTick = k.tick;
                        preview.SelectedStageIndex = authorStageIndex;
                        preview.ShowSelectedStageAtTick(authorPreviewTick);
                    }

                    DrawGroupedHeader(scrollCtx, ref v, "Inspector/Transform", "Transform");

                    Rect angleRow = v.NextRow(UIRectTag.Input, "Inspector/AngleRow");
                    var angleH = new HRow(scrollCtx, angleRow);
                    D2Widgets.Label(
                        scrollCtx,
                        angleH.NextFixed(
                            Mathf.Clamp(angleRow.width * 0.24f, 60f, 74f),
                            UIRectTag.Label,
                            "Inspector/AngleLabel"),
                        "Angle",
                        "Inspector/AngleLabel");
                    float newAngle = D2Widgets.HorizontalSlider(
                        scrollCtx,
                        angleH.Remaining(UIRectTag.Slider, "Inspector/AngleSlider"),
                        k.angle,
                        -90f,
                        90f,
                        showValueLabel: true,
                        label: "Inspector/AngleSlider");
                    if (!Mathf.Approximately(newAngle, k.angle))
                    {
                        k.angle = newAngle;
                        TrySaveProjects();
                    }

                    Rect offXRow = v.NextRow(UIRectTag.Input, "Inspector/OffsetXRow");
                    var offXH = new HRow(scrollCtx, offXRow);
                    D2Widgets.Label(
                        scrollCtx,
                        offXH.NextFixed(
                            Mathf.Clamp(offXRow.width * 0.24f, 60f, 74f),
                            UIRectTag.Label,
                            "Inspector/OffsetXLabel"),
                        "Offset X",
                        "Inspector/OffsetXLabel");
                    float newX = D2Widgets.HorizontalSlider(
                        scrollCtx,
                        offXH.Remaining(UIRectTag.Slider, "Inspector/OffsetXSlider"),
                        k.offset.x,
                        -0.6f,
                        0.6f,
                        showValueLabel: true,
                        label: "Inspector/OffsetXSlider");
                    if (!Mathf.Approximately(newX, k.offset.x))
                    {
                        k.offset.x = newX;
                        TrySaveProjects();
                    }

                    Rect offZRow = v.NextRow(UIRectTag.Input, "Inspector/OffsetZRow");
                    var offZH = new HRow(scrollCtx, offZRow);
                    D2Widgets.Label(
                        scrollCtx,
                        offZH.NextFixed(
                            Mathf.Clamp(offZRow.width * 0.24f, 60f, 74f),
                            UIRectTag.Label,
                            "Inspector/OffsetZLabel"),
                        "Offset Z",
                        "Inspector/OffsetZLabel");
                    float newZ = D2Widgets.HorizontalSlider(
                        scrollCtx,
                        offZH.Remaining(UIRectTag.Slider, "Inspector/OffsetZSlider"),
                        k.offset.z,
                        -0.6f,
                        0.6f,
                        showValueLabel: true,
                        label: "Inspector/OffsetZSlider");
                    if (!Mathf.Approximately(newZ, k.offset.z))
                    {
                        k.offset.z = newZ;
                        TrySaveProjects();
                    }

                    if (IsPropNodeTag(tr.nodeTag))
                    {
                        DrawGroupedHeader(scrollCtx, ref v, "Inspector/Scale", "Scale (prop)", topPadding: true);
                        bool lockVal = authorScaleLock;
                        D2Widgets.CheckboxLabeled(scrollCtx, v.NextRow(UIRectTag.Checkbox, "Inspector/ScaleLock"), "Lock X/Z", ref lockVal, "Inspector/ScaleLock");
                        authorScaleLock = lockVal;

                        const float sMin = 0.3f;
                        const float sMax = 2.0f;
                        if (k.scale == default(Vector3)) k.scale = Vector3.one;
                        k.scale.y = 1f;

                        Rect sxRow = v.NextRow(UIRectTag.Input, "Inspector/ScaleXRow");
                        var sxH = new HRow(scrollCtx, sxRow);
                        D2Widgets.Label(
                            scrollCtx,
                            sxH.NextFixed(
                                Mathf.Clamp(sxRow.width * 0.24f, 60f, 74f),
                                UIRectTag.Label,
                                "Inspector/ScaleXLabel"),
                            "Scale X",
                            "Inspector/ScaleXLabel");
                        float newSX = D2Widgets.HorizontalSlider(
                            scrollCtx,
                            sxH.Remaining(UIRectTag.Slider, "Inspector/ScaleXSlider"),
                            k.scale.x,
                            sMin,
                            sMax,
                            showValueLabel: true,
                            label: "Inspector/ScaleXSlider");
                        if (!Mathf.Approximately(newSX, k.scale.x))
                        {
                            k.scale.x = newSX;
                            if (authorScaleLock) k.scale.z = newSX;
                            TrySaveProjects();
                            MarkAuthorPreviewDirty();
                        }

                        Rect szRow = v.NextRow(UIRectTag.Input, "Inspector/ScaleZRow");
                        var szH = new HRow(scrollCtx, szRow);
                        D2Widgets.Label(
                            scrollCtx,
                            szH.NextFixed(
                                Mathf.Clamp(szRow.width * 0.24f, 60f, 74f),
                                UIRectTag.Label,
                                "Inspector/ScaleZLabel"),
                            "Scale Z",
                            "Inspector/ScaleZLabel");
                        float newSZ = D2Widgets.HorizontalSlider(
                            scrollCtx,
                            szH.Remaining(UIRectTag.Slider, "Inspector/ScaleZSlider"),
                            k.scale.z,
                            sMin,
                            sMax,
                            showValueLabel: true,
                            label: "Inspector/ScaleZSlider");
                        if (!Mathf.Approximately(newSZ, k.scale.z))
                        {
                            k.scale.z = newSZ;
                            if (authorScaleLock) k.scale.x = newSZ;
                            TrySaveProjects();
                            MarkAuthorPreviewDirty();
                        }
                    }

                    Rect faceRow = v.NextRow(UIRectTag.Input, "Inspector/FacingRow");
                    var faceH = new HRow(scrollCtx, faceRow);
                    D2Widgets.Label(
                        scrollCtx,
                        faceH.NextFixed(
                            Mathf.Clamp(faceRow.width * 0.24f, 60f, 74f),
                            UIRectTag.Label,
                            "Inspector/FacingLabel"),
                        "Facing",
                        "Inspector/FacingLabel");
                    if (D2Widgets.ButtonText(
                        scrollCtx,
                        faceH.RemainingMin(
                            Mathf.Clamp(faceRow.width * 0.34f, 96f, 136f),
                            UIRectTag.Button,
                            "Inspector/FacingButton"),
                        k.rotation.ToStringHuman(),
                        "Inspector/FacingButton"))
                    {
                        Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>
                        {
                            new FloatMenuOption("North", () => { k.rotation = Rot4.North; TrySaveProjects(); }),
                            new FloatMenuOption("East",  () => { k.rotation = Rot4.East;  TrySaveProjects(); }),
                            new FloatMenuOption("South", () => { k.rotation = Rot4.South; TrySaveProjects(); }),
                            new FloatMenuOption("West",  () => { k.rotation = Rot4.West;  TrySaveProjects(); })
                        }));
                    }

                    DrawGroupedHeader(scrollCtx, ref v, "Inspector/Display", "Display", topPadding: true);

                    bool visible = k.visible;
                    D2Widgets.CheckboxLabeled(scrollCtx, v.NextRow(UIRectTag.Checkbox, "Inspector/Visible"), "Visible", ref visible, "Inspector/Visible");
                    if (visible != k.visible)
                    {
                        k.visible = visible;
                        TrySaveProjects();
                    }

                    DrawGroupedHeader(scrollCtx, ref v, "Inspector/Advanced", "Advanced", topPadding: true);

                    Rect layerRow = v.NextRow(UIRectTag.Input, "Inspector/LayerBiasRow");
                    var layerH = new HRow(scrollCtx, layerRow);
                    D2Widgets.Label(
                        scrollCtx,
                        layerH.NextFixed(
                            Mathf.Clamp(layerRow.width * 0.24f, 60f, 74f),
                            UIRectTag.Label,
                            "Inspector/LayerBiasLabel"),
                        "Layer",
                        "Inspector/LayerBiasLabel");
                    float newLayerF = D2Widgets.HorizontalSlider(
                        scrollCtx,
                        layerH.Remaining(UIRectTag.Slider, "Inspector/LayerBiasSlider"),
                        k.layerBias,
                        -3f,
                        3f,
                        showValueLabel: true,
                        label: "Inspector/LayerBiasSlider");
                    int newLayer = Mathf.Clamp(Mathf.RoundToInt(newLayerF), -3, 3);
                    if (newLayer != k.layerBias)
                    {
                        k.layerBias = newLayer;
                        TrySaveProjects();
                    }

                    if (string.Equals(tr.nodeTag, "Root", StringComparison.Ordinal))
                    {
                        DrawGroupedHeader(scrollCtx, ref v, "Inspector/ClipEffects", "Clip Effects (Root)", topPadding: true);

                        Rect soundRow = v.NextRow(UIRectTag.Input, "Inspector/SoundRow");
                        var soundH = new HRow(scrollCtx, soundRow);
                        D2Widgets.Label(
                            scrollCtx,
                            soundH.NextFixed(
                                Mathf.Clamp(soundRow.width * 0.24f, 60f, 74f),
                                UIRectTag.Label,
                                "Inspector/SoundLabel"),
                            "Sound",
                            "Inspector/SoundLabel");

                        string soundLabel = k.soundDefName.NullOrEmpty() ? "(None)" : k.soundDefName;
                        if (D2Widgets.ButtonText(
                            scrollCtx,
                            soundH.RemainingMin(
                                Mathf.Clamp(soundRow.width * 0.34f, 120f, 180f),
                                UIRectTag.Button,
                                "Inspector/SoundButton"),
                            soundLabel,
                            "Inspector/SoundButton"))
                        {
                            var opts = new List<FloatMenuOption>
                            {
                                new FloatMenuOption("(None)", () =>
                                {
                                    k.soundDefName = null;
                                    TrySaveProjects();
                                })
                            };

                            var allDefs = DefDatabase<SoundDef>.AllDefsListForReading;
                            if (!allDefs.NullOrEmpty())
                            {
                                for (int si = 0; si < allDefs.Count; si++)
                                {
                                    var def = allDefs[si];
                                    if (def == null) continue;
                                    string defName = def.defName;
                                    string optionLabel = defName;
                                    opts.Add(new FloatMenuOption(optionLabel, () =>
                                    {
                                        k.soundDefName = defName;
                                        TrySaveProjects();
                                    }));
                                }
                            }

                            Find.WindowStack.Add(new FloatMenu(opts));
                        }

                        Rect facialRow = v.NextRow(UIRectTag.Input, "Inspector/FacialRow");
                        var facialH = new HRow(scrollCtx, facialRow);
                        D2Widgets.Label(
                            scrollCtx,
                            facialH.NextFixed(
                                Mathf.Clamp(facialRow.width * 0.24f, 60f, 74f),
                                UIRectTag.Label,
                                "Inspector/FacialLabel"),
                            "Facial",
                            "Inspector/FacialLabel");

                        string facialLabel = k.facialAnimDefName.NullOrEmpty() ? "(None)" : k.facialAnimDefName;
                        if (D2Widgets.ButtonText(
                            scrollCtx,
                            facialH.RemainingMin(
                                Mathf.Clamp(facialRow.width * 0.34f, 120f, 180f),
                                UIRectTag.Button,
                                "Inspector/FacialButton"),
                            facialLabel,
                            "Inspector/FacialButton"))
                        {
                            var opts = new List<FloatMenuOption>
                            {
                                new FloatMenuOption("(None)", () =>
                                {
                                    k.facialAnimDefName = null;
                                    TrySaveProjects();
                                })
                            };

                            var allDefs = DefDatabase<FacialAnimDef>.AllDefsListForReading;
                            if (!allDefs.NullOrEmpty())
                            {
                                for (int fi = 0; fi < allDefs.Count; fi++)
                                {
                                    var def = allDefs[fi];
                                    if (def == null) continue;
                                    string defName = def.defName;
                                    string optionLabel = def.label.NullOrEmpty() ? defName : $"{def.label} ({defName})";
                                    opts.Add(new FloatMenuOption(optionLabel, () =>
                                    {
                                        k.facialAnimDefName = defName;
                                        TrySaveProjects();
                                    }));
                                }
                            }

                            Find.WindowStack.Add(new FloatMenu(opts));
                        }
                    }
                }, label: "KeyframeInspectorScroll");
                authorInspectorScroll = authorInspectorScrollLocal;
                authorInspectorContentHeight = authorInspectorContentHeightLocal;
            }
}
