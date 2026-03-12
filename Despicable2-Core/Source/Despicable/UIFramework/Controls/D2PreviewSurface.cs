using System;
using UnityEngine;
using Verse;

namespace Despicable.UIFramework.Controls;
/// <summary>
/// Framework-native framed preview surface for image-fit content.
///
/// The framework owns panel padding and preview bounds. Callers provide texture content,
/// but do not perform layout or frame budgeting themselves.
/// </summary>
public static class D2PreviewSurface
{
    public struct Spec
    {
        public string Id;
        public bool Soft;
        public bool Pad;
        public bool DrawBackground;
        public float? PadOverride;
        public string EmptyLabel;
        public ScaleMode ScaleMode;

        public Spec(string id, bool soft = false, bool pad = true, bool drawBackground = true, float? padOverride = null, string emptyLabel = null, ScaleMode scaleMode = ScaleMode.ScaleToFit)
        {
            Id = id;
            Soft = soft;
            Pad = pad;
            DrawBackground = drawBackground;
            PadOverride = padOverride;
            EmptyLabel = emptyLabel;
            ScaleMode = scaleMode;
        }
    }

    public static void Draw(UIContext ctx, Rect rect, Texture texture, Spec spec)
    {
        using (var panel = ctx.GroupPanel(
            spec.Id,
            rect,
            soft: spec.Soft,
            pad: spec.Pad,
            padOverride: spec.PadOverride,
            drawBackground: spec.DrawBackground,
            label: spec.Id))
        {
            DrawContent(ctx, panel.Inner, texture, spec);
        }
    }

    public static void Draw(UIContext ctx, Rect rect, Func<Rect, Texture> textureProvider, Spec spec)
    {
        using (var panel = ctx.GroupPanel(
            spec.Id,
            rect,
            soft: spec.Soft,
            pad: spec.Pad,
            padOverride: spec.PadOverride,
            drawBackground: spec.DrawBackground,
            label: spec.Id))
        {
            Texture texture = textureProvider != null ? textureProvider(panel.Inner) : null;
            DrawContent(ctx, panel.Inner, texture, spec);
        }
    }

    private static void DrawContent(UIContext ctx, Rect inner, Texture texture, Spec spec)
    {
        ctx?.RecordRect(inner, UIRectTag.PanelSoft, (spec.Id ?? "PreviewSurface") + "/Content", null); // loc-allow-internal: fallback preview surface id

        if (texture != null)
        {
            if (ctx == null || ctx.Pass == UIPass.Draw)
                GUI.DrawTexture(inner, texture, spec.ScaleMode);
            return;
        }

        if (!string.IsNullOrEmpty(spec.EmptyLabel))
            D2Widgets.LabelClipped(ctx, inner, spec.EmptyLabel, (spec.Id ?? "PreviewSurface") + "/Empty"); // loc-allow-internal: fallback preview surface id
    }
}
