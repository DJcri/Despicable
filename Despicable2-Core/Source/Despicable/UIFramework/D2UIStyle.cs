using System;
using UnityEngine;
using Verse;

namespace Despicable.UIFramework;
/// <summary>
/// Single source of truth for common UI metrics.
/// Intentionally conservative defaults that match RimWorld-ish sizing.
/// </summary>
public sealed class D2UIStyle
{
    public static readonly D2UIStyle Default = new();

    // Spacing
    public float Pad = 10f;

    /// <summary>
    /// Canonical named gap tiers for new additive layouts.
    /// Existing code can keep using <see cref="Gap"/> as the default row-to-row spacing.
    /// </summary>
    public float GapXS = 4f;
    public float GapS = 6f;
    public float GapM = 10f;
    public float GapL = 16f;

    // Section padding (header/footer): smaller vertical pad so text/buttons don't get crushed.
    public float HeaderPadY = 4f;
    public float FooterPadY = 4f;

    /// <summary>
    /// Optional body padding overrides for blueprint windows.
    /// Leave as NaN to inherit <see cref="Pad"/> so existing windows keep their current spacing.
    /// </summary>
    public float BodyPadX = float.NaN;
    public float BodyPadY = float.NaN;

    /// <summary>
    /// Optional per-edge vertical body padding overrides for blueprint windows.
    /// Leave as NaN to inherit <see cref="BodyPadY"/>, then <see cref="Pad"/>.
    /// Use these when a window needs intentional breathing room on one edge without page-local rect nudges.
    /// </summary>
    public float BodyTopPadY = float.NaN;
    public float BodyBottomPadY = float.NaN;

    public float Gap = 6f;
    /// <summary>
    /// Canonical single-line text height used for measuring and compact labels.
    /// Widgets may be centered within a taller RowHeight.
    /// </summary>
    public float Line = 24f;

    /// <summary>
    /// Canonical row height for single-line controls.
    /// Use this for buttons, sliders, checkboxes, dropdown rows, etc.
    /// </summary>
    public float RowHeight = 28f;

    /// <summary>
    /// Default inner height to center most vanilla Widgets controls within a RowHeight.
    /// </summary>
    public float ControlHeight = 24f;

    /// <summary>
    /// Default insets for text inside fields/areas when we need manual padding.
    /// </summary>
    public float TextInsetX = 6f;
    public float TextInsetY = 2f;

    // Sections
    public float HeaderHeight = 36f;
    public float FooterHeight = 40f;

    // Controls
    public float ButtonHeight = 30f;
    public float IconSize = 20f;

    // Banded ruler controls
    public float RulerHeight = 12f;
    public float RulerMarkerHeight = 12f;
    public float RulerMarkerWidth = 3f;
    public float RulerMarkerDiamondSize = 8f;
    public float RulerSegmentGap = 1f;
    public float RulerMilestoneIconSize = 14f;
    public float RulerMilestoneStripHeight = 16f;

    /// <summary>
    /// Canonical visual icon size for vanilla-ish micro-actions inside a 24px hitbox.
    /// </summary>
    public float IconVisualSize = 16f;

    /// <summary>
    /// Inset used when drawing icon-only buttons so the hitbox stays large and the icon stays clean.
    /// </summary>
    public float IconInset = 2f;

    public float MinClickSize = 24f;

    // Semantic text colors
    /// <summary>
    /// Shared semantic text color for beneficial focused/pinned modifiers.
    /// Centralize here so UI surfaces can stay consistent and future tuning only touches one place.
    /// </summary>
    public Color PositiveTextColor = new Color(0.56f, 0.84f, 0.56f);

    /// <summary>
    /// Shared semantic text color for harmful focused/pinned modifiers.
    /// </summary>
    public Color NegativeTextColor = ColorLibrary.RedReadable;

    /// <summary>
    /// Canonical aliases used in docs/recipes for vanilla-style layouts.
    /// </summary>
    public float RowH => RowHeight;
    public float SectionPadX => Pad;
    public float SectionPadY => Pad;
    public float EffectiveBodyPadX => float.IsNaN(BodyPadX) ? Pad : Mathf.Max(0f, BodyPadX);
    public float EffectiveBodyPadY => float.IsNaN(BodyPadY) ? Pad : Mathf.Max(0f, BodyPadY);
    public float EffectiveBodyTopPadY => float.IsNaN(BodyTopPadY) ? EffectiveBodyPadY : Mathf.Max(0f, BodyTopPadY);
    public float EffectiveBodyBottomPadY => float.IsNaN(BodyBottomPadY) ? EffectiveBodyPadY : Mathf.Max(0f, BodyBottomPadY);

    // Debug
    public float DebugLabelHeight = 18f;

    /// <summary>
    /// Reserved space on the right edge of panels/groups when UI debug overlay is enabled.
    /// Prevents overlay glyphs/labels from colliding with actual UI controls.
    /// </summary>
    public float DebugGutterWidth = 18f;

    public D2UIStyle Clone() => (D2UIStyle)MemberwiseClone();

    /// <summary>
    /// Fluent style overrides without mutating the original.
    /// </summary>
    public D2UIStyle With(Action<D2UIStyle> mutate)
    {
        var s = Clone();
        mutate?.Invoke(s);
        return s;
    }

    // Convenience helpers (common overrides)
    public D2UIStyle WithPad(float pad) => With(s => s.Pad = pad);
    public D2UIStyle WithGap(float gap) => With(s => s.Gap = gap);
    public D2UIStyle WithLine(float line) => With(s => s.Line = line);
    public D2UIStyle WithRow(float row) => With(s => s.RowHeight = row);
    public D2UIStyle WithControl(float h) => With(s => s.ControlHeight = h);
    public D2UIStyle WithHeader(float h) => With(s => s.HeaderHeight = h);
    public D2UIStyle WithFooter(float h) => With(s => s.FooterHeight = h);
    public D2UIStyle WithBodyPad(float? x = null, float? y = null, float? top = null, float? bottom = null) => With(s =>
    {
        if (x.HasValue) s.BodyPadX = x.Value;
        if (y.HasValue) s.BodyPadY = y.Value;
        if (top.HasValue) s.BodyTopPadY = top.Value;
        if (bottom.HasValue) s.BodyBottomPadY = bottom.Value;
    });
    public D2UIStyle WithButton(float h) => With(s => s.ButtonHeight = h);
    public D2UIStyle WithIcon(float s2) => With(s => s.IconSize = s2);
    public D2UIStyle WithIconVisual(float s2) => With(s => s.IconVisualSize = s2);
    public D2UIStyle WithIconInset(float inset) => With(s => s.IconInset = inset);
    public D2UIStyle WithMinClick(float min) => With(s => s.MinClickSize = min);
}
