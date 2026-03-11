using UnityEngine;

namespace Despicable.UIFramework.Layout;

/// <summary>
/// Single entry point for the most common layout patterns.
///
/// This is the recommended starting point when vibe-coding a new window or panel.
/// It wraps D2PaneLayout, D2EditorShell, RectTake, and RectSplit so you don't need
/// to know which underlying system to reach for.
///
/// Typical usage:
///
///   // Dialog with header + scrollable body
///   var shell = D2Layout.Window(ctx, inRect, headerH: 36f);
///   DrawHeader(shell.Header);
///   DrawBody(shell.Body);
///
///   // Left sidebar + right content
///   var cols = D2Layout.TwoColumn(ctx, inRect, leftW: 260f);
///   DrawSidebar(cols.Left);
///   DrawContent(cols.Right);
///
///   // Three-panel editor (browser | details | preview)
///   var cols = D2Layout.ThreeColumn(ctx, inRect, leftW: 220f, rightW: 280f);
///   DrawBrowser(cols.Left);
///   DrawDetails(cols.Center);
///   DrawPreview(cols.Right);
///
/// For more control (min/preferred/flex/collapse), use D2PaneLayout directly.
/// For full editor shells (header + rails + footer), use D2EditorShell directly.
/// </summary>
public static class D2Layout
{
    // -------------------------------------------------------------------------
    // Return types — named structs so call sites read cleanly without indexing
    // -------------------------------------------------------------------------

    public readonly struct WindowShell
    {
        /// <summary>Full outer rect passed in.</summary>
        public readonly Rect Outer;
        /// <summary>Header strip. Zero if headerH == 0.</summary>
        public readonly Rect Header;
        /// <summary>Remaining body space below the header (and above the footer).</summary>
        public readonly Rect Body;
        /// <summary>Footer strip. Zero if footerH == 0.</summary>
        public readonly Rect Footer;

        public WindowShell(Rect outer, Rect header, Rect body, Rect footer)
        {
            Outer  = outer;
            Header = header;
            Body   = body;
            Footer = footer;
        }
    }

    public readonly struct TwoColumnResult
    {
        public readonly Rect Left;
        public readonly Rect Right;

        public TwoColumnResult(Rect left, Rect right)
        {
            Left  = left;
            Right = right;
        }
    }

    public readonly struct ThreeColumnResult
    {
        public readonly Rect Left;
        public readonly Rect Center;
        public readonly Rect Right;

        public ThreeColumnResult(Rect left, Rect center, Rect right)
        {
            Left   = left;
            Center = center;
            Right  = right;
        }
    }

    // -------------------------------------------------------------------------
    // Window shell
    // -------------------------------------------------------------------------

    /// <summary>
    /// Carves a rect into header / body / footer strips.
    ///
    /// Any section with height == 0 is omitted (its rect is Rect.zero).
    /// Gap is applied between non-zero sections.
    ///
    ///   var shell = D2Layout.Window(ctx, inRect, headerH: 36f, footerH: 40f);
    /// </summary>
    public static WindowShell Window(
        UIContext ctx,
        Rect outer,
        float headerH = 0f,
        float footerH = 0f,
        float? gap = null)
    {
        float g = gap ?? ctx?.Style?.Gap ?? 6f;

        Rect remaining = outer;
        Rect header    = Rect.zero;
        Rect footer    = Rect.zero;

        if (headerH > 0f)
        {
            header    = RectTake.TakeTop(ref remaining, headerH);
            RectTake.TakeTop(ref remaining, g);   // consume the gap
        }

        if (footerH > 0f)
        {
            footer    = RectTake.TakeBottom(ref remaining, footerH);
            RectTake.TakeBottom(ref remaining, g);
        }

        Rect body = remaining;

        if (ctx != null)
        {
            if (header != Rect.zero) ctx.RecordRect(header, UIRectTag.Header, "D2Layout/Header");
            ctx.RecordRect(body,  UIRectTag.Body,   "D2Layout/Body");
            if (footer != Rect.zero) ctx.RecordRect(footer, UIRectTag.Footer, "D2Layout/Footer");
        }

        return new WindowShell(outer, header, body, footer);
    }

    // -------------------------------------------------------------------------
    // Column splits
    // -------------------------------------------------------------------------

    /// <summary>
    /// Splits a rect into a fixed-width left column and a right fill.
    ///
    ///   var cols = D2Layout.TwoColumn(ctx, rect, leftW: 260f);
    ///   // cols.Left  = fixed 260px sidebar
    ///   // cols.Right = everything else
    /// </summary>
    public static TwoColumnResult TwoColumn(
        UIContext ctx,
        Rect outer,
        float leftW,
        float? gap = null)
    {
        float g = gap ?? ctx?.Style?.Gap ?? 6f;
        RectSplit.SplitVertical(outer, leftW, g, out Rect left, out Rect right);

        if (ctx != null)
        {
            ctx.RecordRect(left,  UIRectTag.Panel, "D2Layout/Left");
            ctx.RecordRect(right, UIRectTag.Panel, "D2Layout/Right");
        }

        return new TwoColumnResult(left, right);
    }

    /// <summary>
    /// Splits a rect into left / center / right columns.
    /// Left and right widths are fixed; center gets the remainder.
    ///
    ///   var cols = D2Layout.ThreeColumn(ctx, rect, leftW: 220f, rightW: 280f);
    /// </summary>
    public static ThreeColumnResult ThreeColumn(
        UIContext ctx,
        Rect outer,
        float leftW,
        float rightW,
        float? gap = null)
    {
        float g = gap ?? ctx?.Style?.Gap ?? 6f;

        RectSplit.SplitVertical(outer, leftW, g, out Rect left, out Rect centerAndRight);
        RectSplit.SplitVertical(centerAndRight, Mathf.Max(0f, centerAndRight.width - rightW - g), g, out Rect center, out Rect right);

        if (ctx != null)
        {
            ctx.RecordRect(left,   UIRectTag.Panel, "D2Layout/Left");
            ctx.RecordRect(center, UIRectTag.Panel, "D2Layout/Center");
            ctx.RecordRect(right,  UIRectTag.Panel, "D2Layout/Right");
        }

        return new ThreeColumnResult(left, center, right);
    }

    /// <summary>
    /// Splits a rect into N columns by proportional weights.
    /// Gap is applied between columns.
    ///
    ///   var cols = D2Layout.WeightedColumns(ctx, rect, new[]{ 1f, 2f, 1f });
    ///   // => 25% | 50% | 25%
    ///
    /// Returns a stack-allocated-friendly array. If you're calling this every frame
    /// and allocation matters, use RectSplit.Columns directly and cache the result.
    /// </summary>
    public static Rect[] WeightedColumns(
        UIContext ctx,
        Rect outer,
        float[] weights,
        float? gap = null)
    {
        float g = gap ?? ctx?.Style?.Gap ?? 6f;
        Rect[] cols = RectSplit.Columns(outer, g, weights);

        if (ctx != null)
        {
            for (int i = 0; i < cols.Length; i++)
                ctx.RecordRect(cols[i], UIRectTag.Panel, "D2Layout/Col[" + i + "]");
        }

        return cols;
    }
}
