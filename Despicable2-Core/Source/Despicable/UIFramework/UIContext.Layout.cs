using System;
using UnityEngine;
using Despicable.UIFramework.Layout;
using Verse;

namespace Despicable.UIFramework;
public sealed partial class UIContext
{
    /// <summary>
    /// Creates a named group scope and (optionally) records the group container rect.
    /// Returns a disposable that will pop scope on dispose, and exposes Inner/Outer rects.
    ///
    /// Usage:
    /// using (var g = ctx.Group("LeftPanel", rect, UIRectTag.PanelSoft))
    /// {
    ///     var v = ctx.VStack(g.Inner);
    /// }
    /// </summary>
    public UIGroupScope Group(
        string name,
        Rect outer,
        UIRectTag tag = UIRectTag.Group,
        bool pad = true,
        float? padOverride = null,
        bool recordContainer = true,
        string label = null)
    {
        // Record the container at the current scope (so it shows up as .../LeftPanel instead of .../LeftPanel/Group).
        if (recordContainer)
            Record(outer, tag, label ?? name ?? "Group");

        if (!string.IsNullOrEmpty(name))
        {
            _scopes.Add(name);
            _scopePathDirty = true;
        }

        float p = pad ? (padOverride ?? Style.Pad) : 0f;
        Rect inner = p > 0f ? outer.ContractedBy(p) : outer;

        return new UIGroupScope(this, outer, inner, name);
    }

    /// <summary>
    /// Convenience: creates a named scope group inside a panel surface.
    /// Records/draws the panel container (Panel/PanelSoft) and returns an inner rect for content.
    ///
    /// Typical use:
    /// using (var g = ctx.GroupPanel("LeftPanel", leftRect))
    /// {
    ///     var v = ctx.VStack(g.Inner);
    /// }
    /// </summary>
    public UIGroupScope GroupPanel(
        string name,
        Rect outer,
        bool soft = true,
        bool pad = true,
        float? padOverride = null,
        bool drawBackground = true,
        string label = null)
    {
        // Record/draw the surface container at the current scope.
        if (soft)
        {
            if (drawBackground)
                D2Widgets.DrawPanelSoft(this, outer, label ?? name ?? "PanelSoft");
            else
                Record(outer, UIRectTag.PanelSoft, label ?? name ?? "PanelSoft");
        }
        else
        {
            if (drawBackground)
                D2Widgets.DrawPanel(this, outer, label ?? name ?? "Panel");
            else
                Record(outer, UIRectTag.Panel, label ?? name ?? "Panel");
        }

        // Create the scope + inner rect without recording a second container.
        // NOTE: We compute Inner manually here so we can reserve a right-side debug gutter when overlays are enabled.
        if (!string.IsNullOrEmpty(name))
        {
            _scopes.Add(name);
            _scopePathDirty = true;
        }

        float p = pad ? (padOverride ?? Style.Pad) : 0f;
        Rect inner = p > 0f ? outer.ContractedBy(p) : outer;

        // Reserve a debug gutter so overlay glyphs/labels never collide with controls.
        if (ShouldReserveDebugGutter() && Style.DebugGutterWidth > 0f)
        {
            float w = Mathf.Min(
                Style.DebugGutterWidth,
                Mathf.Max(0f, inner.width - Style.MinClickSize));
            if (w > 0f)
            {
                Rect gutter = new(inner.xMax - w, inner.y, w, inner.height);
                inner.width -= w;

                // Record gutter at the current scope so overlay tools can target it.
                Record(gutter, UIRectTag.DebugOverlay, "DebugGutter");
            }
        }

        return new UIGroupScope(this, outer, inner, name);
    }

    private bool ShouldReserveDebugGutter()
    {
        // Disabled: the gutter visually shrinks panels and creates "mystery whitespace".
        // The overlay system should not change layout metrics.
        return false;
    }

    /// <summary>
    /// GroupPanel + VStack in one call.
    /// Usage:
    /// using (var g = ctx.GroupPanel("Left", rect, out var v))
    /// {
    ///     D2Widgets.Label(ctx, v.NextLine(), "Hello");
    /// }
    /// </summary>
    public UIGroupScope GroupPanel(
        string name,
        Rect outer,
        out VStack stack,
        bool soft = true,
        bool pad = true,
        float? padOverride = null,
        bool drawBackground = true,
        string label = null)
    {
        var g = GroupPanel(name, outer, soft, pad, padOverride, drawBackground, label);
        stack = VStack(g.Inner);
        return g;
    }

    /// <summary>
    /// Convenience: begin a group and return only the inner rect + an IDisposable.
    /// </summary>
    public Rect BeginGroup(
        string name,
        Rect outer,
        out IDisposable scope,
        UIRectTag tag = UIRectTag.Group,
        bool pad = true,
        float? padOverride = null,
        bool recordContainer = true,
        string label = null)
    {
        var g = Group(name, outer, tag, pad, padOverride, recordContainer, label);
        scope = g;
        return g.Inner;
    }

    /// <summary>
    /// Create a vertical stack allocator over the given rect.
    /// IMPORTANT: This does NOT apply padding by default.
    /// Padding should be applied by containers (Group/GroupPanel) so spacing rules stay consistent.
    /// If you truly need a one-off padded stack, pass a padOverride explicitly.
    /// </summary>
    public VStack VStack(Rect rect, float? padOverride = null, UIRectTag tag = UIRectTag.Panel, string label = null)
    {
        // Allocator containers should not inflate auto-measure heights; only the children they allocate should.
        if (Pass == UIPass.Draw)
            Record(rect, tag, label);

        float pad = padOverride ?? 0f;
        return pad > 0f ? new VStack(this, rect.ContractedBy(pad)) : new VStack(this, rect);
    }

    /// <summary>
    /// Create a horizontal row allocator over the given rect.
    /// IMPORTANT: This does NOT apply padding by default.
    /// Padding should be applied by containers (Group/GroupPanel).
    /// If you truly need a one-off padded row, pass a padOverride explicitly.
    /// </summary>
    public HRow HRow(Rect rect, float? padOverride = null, UIRectTag tag = UIRectTag.Panel, string label = null)
    {
        // Allocator containers should not inflate auto-measure heights; only the children they allocate should.
        if (Pass == UIPass.Draw)
            Record(rect, tag, label);

        float pad = padOverride ?? 0f;
        return pad > 0f ? new HRow(this, rect.ContractedBy(pad)) : new HRow(this, rect);
    }

    /// <summary>
    /// Escape hatch: record a rect in the registry without requiring a specific widget wrapper.
    /// This is intended for custom-drawn elements (textures, bespoke highlights, etc.)
    /// so validation/debug overlay can still target them.
    ///
    /// Notes:
    /// - In Measure pass, this will only update ContentMaxY (no registry writes).
    /// - In Draw pass, this records to the registry like Record().
    /// </summary>
    public void RecordRect(string id, Rect rect, UIRectTag tag, string label = null)
    {
        // 'id' is currently unused, but reserved for future per-element stable keys.
        // Keeping it in the signature makes it easier to evolve the registry later.
        Record(rect, tag, label ?? id);
    }

    /// <summary>
    /// Escape hatch overload when you don't have an id.
    /// </summary>
    public void RecordRect(Rect rect, UIRectTag tag, string label = null)
    {
        Record(rect, tag, label);
    }

    /// <summary>
    /// Escape hatch overload for custom drawing code that wants to attach additional context.
    /// Right now we only store a single label string in the registry, but keeping a "meta" hook
    /// here means we can evolve the overlay/validator later without rewriting call sites.
    ///
    /// PSEUDOCODE FUTURE:
    /// - registry entry becomes (Rect, Tag, Label, Meta)
    /// - Meta might include: stableId, control state (selected/disabled), clip info, measured vs drawn height, etc.
    /// - overlay can render Meta in a tooltip or inspector.
    /// </summary>
    public void RecordRect(Rect rect, UIRectTag tag, string label, string meta)
    {
        // Current registry doesn't store meta, so we append it to the label in a conservative way.
        // (This keeps it visible in debug output without changing registry structure yet.)
        if (string.IsNullOrEmpty(meta))
        {
            Record(rect, tag, label);
            return;
        }

        string combined = string.IsNullOrEmpty(label) ? meta : (label + " | " + meta);
        Record(rect, tag, combined);
    }

    /// <summary>
    /// IDisposable returned by Group(). Pops scope on Dispose. Exposes Inner/Outer rects.
    /// </summary>
    public sealed class UIGroupScope : IDisposable
    {
        private UIContext _ctx;

        public readonly Rect Outer;
        public readonly Rect Inner;
        public readonly string Name;

        internal UIGroupScope(UIContext ctx, Rect outer, Rect inner, string name)
        {
            _ctx = ctx;
            Outer = outer;
            Inner = inner;
            Name = name ?? string.Empty;
        }

        public void Dispose()
        {
            if (_ctx == null)
                return;

            if (!string.IsNullOrEmpty(Name))
                _ctx.PopScope();

            _ctx = null;
        }
    }
}
