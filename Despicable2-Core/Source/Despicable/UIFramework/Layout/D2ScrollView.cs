using System;
using UnityEngine;
using Verse;

namespace Despicable.UIFramework.Layout;
/// <summary>
/// Generic scroll view helper for "section content".
/// - Caller provides a drawer that allocates/draws using a D2VStack.
/// - Helper measures content height in Measure pass using a large virtual bounds.
/// - In Draw pass, begins a real scroll view and draws with a PushOffset.
/// </summary>
public static class D2ScrollView
{
    public delegate void ContentDrawer(UIContext ctx, ref D2VStack v);

    public static void Draw(
        UIContext ctx,
        Rect outRect,
        ref Vector2 scroll,
        ref float cachedContentHeight,
        ContentDrawer drawer,
        string label = "ScrollView")
    {
        if (ctx == null || drawer == null) return;

        using (ctx.PushScope(label))
        {
            // Record viewport.
            ctx.Record(outRect, UIRectTag.ScrollView, label + "/Viewport");

            float contentH;
            const float scrollbarW = 16f;

            float MeasureUsedHeight(D2VStack stack)
            {
                // Keep the full consumed height, plus a tiny bottom breathing buffer.
                // Without this, content that measures *exactly* to the viewport can still feel
                // shaved at the bottom by the clip edge and fail to trip the scrollbar threshold.
                float bottomPad = Mathf.Max(2f, ctx != null && ctx.Style != null ? ctx.Style.Gap * 0.5f : 3f);
                return stack.UsedHeight + bottomPad;
            }

            Func<float, float> measureForWidth = (float width) =>
            {
                UIContext measureCtx = ctx != null && ctx.Pass == UIPass.Measure
                    ? ctx
                    : new UIContext(ctx.Style, null, label, UIPass.Measure);

                using (measureCtx.PushScope("Measure"))
                {
                    Rect measureRect = new(0f, 0f, Mathf.Max(0f, width), 100000f);
                    var mv = measureCtx.D2VStack(measureRect);
                    drawer(measureCtx, ref mv);
                    return Mathf.Max(MeasureUsedHeight(mv), outRect.height);
                }
            };

            if (ctx.Pass == UIPass.Measure)
            {
                contentH = measureForWidth(outRect.width);

                // If a scrollbar will be needed, re-measure using the width the content will
                // actually receive during draw. This avoids rows fitting during measure and
                // then feeling clipped once the scrollbar steals width.
                if (contentH > outRect.height + 0.5f && outRect.width > scrollbarW)
                    contentH = measureForWidth(outRect.width - scrollbarW);

                cachedContentHeight = contentH;
                return;
            }

            // Some windows currently run a draw-only pass. Re-measure here before drawing,
            // but do it by temporarily flipping the *same* context into Measure mode.
            // That keeps caller code that reaches for its owning Ctx field from accidentally
            // drawing real widgets during this internal sizing pass.
            using (ctx.PushPass(UIPass.Measure))
            {
                contentH = measureForWidth(outRect.width);
                if (contentH > outRect.height + 0.5f && outRect.width > scrollbarW)
                    contentH = measureForWidth(outRect.width - scrollbarW);
            }
            cachedContentHeight = contentH;
            bool needsScrollbar = contentH > outRect.height + 0.5f;
            Rect viewRect = new(0f, 0f, outRect.width - (needsScrollbar ? scrollbarW : 0f), contentH);

            // D2ScrollView is also intended as a vertical scroller. Keep the horizontal
            // axis pinned at zero so callers don't inherit phantom sideways scrolling.
            scroll.x = 0f;
            Widgets.BeginScrollView(outRect, ref scroll, viewRect);
            scroll.x = 0f;
            try
            {
                using (ctx.PushOffset(outRect.position - scroll))
                {
                    var v = ctx.D2VStack(viewRect);
                    drawer(ctx, ref v);
                }
            }
            finally
            {
                Widgets.EndScrollView();
                scroll.x = 0f;
            }
        }
    }
}
