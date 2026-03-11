using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Despicable;

namespace Despicable.AnimGroupStudio.Preview;
public sealed partial class AgsPreviewSession
{
    public void DrawViewport(Rect rect, bool drawSection = true, bool useFullRect = false, bool drawHeader = true)
    {
        if (drawSection)
            Widgets.DrawMenuSection(rect);
        var inner = drawSection ? rect.ContractedBy(8f) : rect;

        if (slots.Count == 0)
        {
            LastViewportTextureRect = Rect.zero;
            lastNodeSamplesBySlot.Clear();
            lastNodeStatsBySlot.Clear();
            Widgets.Label(inner, "No preview pawns.");
            return;
        }

        var view = inner;
        if (drawHeader)
        {
            float headerH = 24f;
            var header = new Rect(inner.x, inner.y, inner.width, headerH);

            string sourceName = useRuntimeSource ? (runtimeSourceName ?? "Preview") : (currentGroup?.defName ?? "Preview");
            string roles = string.Join(" | ", slots.Select(s => s?.Label ?? "Role"));
            Widgets.Label(header, roles.NullOrEmpty() ? sourceName : (sourceName + "   " + roles));

            view.yMin += headerH + 4f;
        }

        Rect drawView = view;
        if (!useFullRect)
        {
            // Keep the preview framing stable across pages by rendering into a centered,
            // portrait-friendly sub-rect instead of stretching to whatever space remains.
            // The authoring page opts into full-rect rendering so gizmo interaction uses the
            // entire visible preview surface rather than an inner portrait boundary.
            const float targetAspect = 0.8f; // width / height (4:5)
            if (drawView.width > 1f && drawView.height > 1f)
            {
                float currentAspect = drawView.width / Mathf.Max(1f, drawView.height);
                if (currentAspect > targetAspect)
                {
                    float targetWidth = drawView.height * targetAspect;
                    drawView.x += (drawView.width - targetWidth) * 0.5f;
                    drawView.width = targetWidth;
                }
                else if (currentAspect < targetAspect)
                {
                    float targetHeight = drawView.width / targetAspect;
                    drawView.y += (drawView.height - targetHeight) * 0.5f;
                    drawView.height = targetHeight;
                }
            }
        }

        LastViewportTextureRect = drawView;
        RenderViewportSlots(drawView, drawTextures: true, captureNodes: NodeCaptureEnabled);
    }

    internal bool RefreshNodeCaptureFromLastViewportRect()
    {
        Rect drawView = LastViewportTextureRect;
        if (drawView.width <= 1f || drawView.height <= 1f || slots.Count == 0)
        {
            lastNodeSamplesBySlot.Clear();
            lastNodeStatsBySlot.Clear();
            return false;
        }

        RenderViewportSlots(drawView, drawTextures: false, captureNodes: true);
        return true;
    }

    private void RenderViewportSlots(Rect drawView, bool drawTextures, bool captureNodes)
    {
        float ss = 2f;
        int rtW = Mathf.RoundToInt(drawView.width * Prefs.UIScale * ss);
        int rtH = Mathf.RoundToInt(drawView.height * Prefs.UIScale * ss);
        rtW = Mathf.Clamp(rtW, 128, 2048);
        rtH = Mathf.Clamp(rtH, 128, 2048);

        int tick = WorkshopRenderContext.Tick;

        if (captureNodes)
            AgsPreviewNodeCapture.BeginFrame();
        else
        {
            lastNodeSamplesBySlot.Clear();
            lastNodeStatsBySlot.Clear();
        }

        try
        {
            using (new WorkshopRenderContext.Scope(active: true, tick: tick))
            {
                for (int i = 0; i < slots.Count; i++)
                {
                    var st = slots[i];
                    if (st?.Pawn == null || st.Renderer == null) continue;

                    try
                    {
                        st.Renderer.EnsureSize(rtW, rtH);
                        var anim = GetAnimationForSlotAtStage(st, currentStage);
                        // WorkshopRenderContext.Tick is already stage-local (set to
                        // schedulerTick - stageStartTick in Update). Subtracting
                        // stageStartTick again would double-subtract and give the wrong
                        // value on every loop after the first.
                        int relTick = Mathf.Max(0, WorkshopRenderContext.Tick);
                        Rot4 rot = SampleRootRotation(anim, relTick) ?? Rot4.South;
                        ApplyPreviewFacial(st.Pawn, anim, relTick);

                        if (captureNodes)
                            AgsPreviewNodeCapture.BeginSlot(st.Key, st.Pawn);

                        try
                        {
                            st.Renderer.Render(st.Pawn, rot, 0f, new Vector3(0f, -0.08f, 0f), renderHeadgear: true, portrait: false, scale: 0.65f);
                        }
                        finally
                        {
                            if (captureNodes)
                                AgsPreviewNodeCapture.EndSlot();
                        }

                        if (drawTextures && st.Renderer.Texture != null)
                            Widgets.DrawTextureFitted(drawView, st.Renderer.Texture, 1f);
                    }
                    catch (System.Exception e)
                    {
                        Despicable.Core.DebugLogger.WarnExceptionOnce(
                            "AgsPreviewSession.ViewportRenderFallback",
                            "AGS preview viewport render failed; drawing will continue.",
                            e);
                    }
                }
            }
        }
        finally
        {
            if (captureNodes)
            {
                AgsPreviewNodeCapture.CopyFrameSamplesTo(lastNodeSamplesBySlot);
                AgsPreviewNodeCapture.CopyFrameStatsTo(lastNodeStatsBySlot);
                AgsPreviewNodeCapture.EndFrame();
            }
        }
    }
}
