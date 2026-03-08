using System;
using UnityEngine;

namespace Despicable.UIFramework.Layout;
/// <summary>
/// Reusable multi-pane allocator for workspace UIs.
///
/// Goals:
/// - caller provides pane intent (min / preferred / flex)
/// - framework decides geometry
/// - can degrade to alternate structures instead of forcing manual rect math
///
/// Notes:
/// - This primitive ALLOCATES only. It does not draw.
/// - When space is too tight, panes may be collapsed to empty rects.
/// - "Stack" means switch to the opposite axis so the same panes still render cleanly.
/// </summary>
public static partial class D2PaneLayout
{
    public enum Axis
    {
        Horizontal = 0,
        Vertical = 1
    }

    public enum FallbackMode
    {
        None = 0,
        Stack = 1,
        HideLowPriority = 2,
        Tabs = 3
    }

    public struct PaneSpec
    {
        public string Id;
        public float Min;
        public float Preferred;
        public float Flex;
        public bool CanCollapse;
        public int Priority;

        public PaneSpec(
            string id,
            float min,
            float preferred,
            float flex = 0f,
            bool canCollapse = false,
            int priority = 0)
        {
            Id = id;
            Min = Mathf.Max(0f, min);
            Preferred = Mathf.Max(Min, preferred);
            Flex = Mathf.Max(0f, flex);
            CanCollapse = canCollapse;
            Priority = priority;
        }
    }

    public readonly struct LayoutResult
    {
        public readonly Rect[] Rects;
        public readonly bool UsedFallback;
        public readonly FallbackMode Mode;
        public readonly int[] VisibleIndices;

        public LayoutResult(Rect[] rects, bool usedFallback, FallbackMode mode, int[] visibleIndices)
        {
            Rects = rects ?? new Rect[0];
            UsedFallback = usedFallback;
            Mode = mode;
            VisibleIndices = visibleIndices ?? new int[0];
        }

        public bool IsVisible(int index)
        {
            if (VisibleIndices == null) return false;
            for (int i = 0; i < VisibleIndices.Length; i++)
                if (VisibleIndices[i] == index) return true;
            return false;
        }
    }

    public static LayoutResult Columns(
        UIContext ctx,
        Rect outer,
        PaneSpec[] panes,
        float? gap = null,
        FallbackMode fallback = FallbackMode.HideLowPriority,
        string label = null)
    {
        return Allocate(ctx, outer, Axis.Horizontal, panes, gap, fallback, label);
    }

    public static LayoutResult Rows(
        UIContext ctx,
        Rect outer,
        PaneSpec[] panes,
        float? gap = null,
        FallbackMode fallback = FallbackMode.HideLowPriority,
        string label = null)
    {
        return Allocate(ctx, outer, Axis.Vertical, panes, gap, fallback, label);
    }

    private static LayoutResult Allocate(
        UIContext ctx,
        Rect outer,
        Axis axis,
        PaneSpec[] panes,
        float? gapOverride,
        FallbackMode fallback,
        string label)
    {
        if (panes == null || panes.Length == 0)
            return new LayoutResult(new Rect[0], false, FallbackMode.None, new int[0]);

        float gap = gapOverride ?? (ctx != null && ctx.Style != null ? ctx.Style.Gap : 6f);
        if (gap < 0f) gap = 0f;

        Rect[] rects = NewEmptyRects(panes.Length, outer);
        bool[] visible = new bool[panes.Length];
        for (int i = 0; i < visible.Length; i++)
            visible[i] = true;

        float primary = axis == Axis.Horizontal ? outer.width : outer.height;

        if (TryCollapseToFit(primary, gap, panes, visible))
        {
            AllocateVisibleAlongAxis(outer, axis, gap, panes, visible, rects);
            Record(ctx, rects, panes, visible, label, axis, fallback == FallbackMode.None ? FallbackMode.None : FallbackMode.HideLowPriority, false);
            return new LayoutResult(rects, false, FallbackMode.None, CollectVisible(visible));
        }

        if (fallback == FallbackMode.HideLowPriority)
        {
            // We already tried collapsing everything collapsible. Still doesn't fit.
            // Fall through to stack so layout remains drawable rather than overflowing.
            fallback = FallbackMode.Stack;
        }

        if (fallback == FallbackMode.Tabs)
        {
            // Tabs are a caller-owned behavioral choice, but we can still provide a safe
            // full-body page allocation for the highest-priority visible pane.
            int winner = SelectTabWinner(panes, visible);
            for (int i = 0; i < visible.Length; i++)
                visible[i] = (i == winner);

            rects[winner] = outer;
            Record(ctx, rects, panes, visible, label, axis, FallbackMode.Tabs, true);
            return new LayoutResult(rects, true, FallbackMode.Tabs, new[] { winner });
        }

        if (fallback == FallbackMode.None)
        {
            // "None" means stay on the requested axis, even when the declared minimums do not fit.
            // Preserve all panes and proportionally compress them below their mins rather than
            // silently flipping orientation.
            AllocateVisibleForcedAlongAxis(outer, axis, gap, panes, rects);
            Record(ctx, rects, panes, visible, label, axis, FallbackMode.None, false);
            return new LayoutResult(rects, false, FallbackMode.None, CollectVisible(visible));
        }

        // Default fallback: stack on the opposite axis.
        bool[] stackedVisible = new bool[panes.Length];
        for (int i = 0; i < panes.Length; i++)
            stackedVisible[i] = true;

        Axis stackedAxis = axis == Axis.Horizontal ? Axis.Vertical : Axis.Horizontal;
        AllocateVisibleStacked(outer, stackedAxis, gap, panes, rects);
        Record(ctx, rects, panes, stackedVisible, label, axis, FallbackMode.Stack, true);
        return new LayoutResult(rects, true, FallbackMode.Stack, CollectVisible(stackedVisible));
    }

}
