using UnityEngine;
using Verse;
using Despicable.UIFramework.Layout;

namespace Despicable.UIFramework.Blueprints;
/// <summary>
/// A lightweight blueprint for RimWorld inspector tabs (ITab).
///
/// Inspector tabs are small, dense, and often scroll-heavy. This blueprint provides a
/// consistent Header / Body / Footer split and a helper to draw the Body as a scroll view
/// using the framework's Measure/Draw pass rules.
///
/// Usage:
/// - Construct once per ITab fill (you pass in inRect).
/// - Put paragraphs in NextTextBlock (measured).
/// - Put big panels/lists in NextFill (containers).
/// </summary>
public sealed class D2ITabBlueprint
{
    public Rect Full { get; private set; }
    public Rect Header { get; private set; }
    public Rect Body { get; private set; }
    public Rect Footer { get; private set; }

    public readonly float HeaderHeight;
    public readonly float FooterHeight;
    public readonly float Gap;

    public D2ITabBlueprint(UIContext ctx, Rect inRect, float headerHeight, float footerHeight, float gap)
    {
        Full = inRect;
        HeaderHeight = Mathf.Max(0f, headerHeight);
        FooterHeight = Mathf.Max(0f, footerHeight);
        Gap = Mathf.Max(0f, gap);

        float y = inRect.yMin;

        Header = HeaderHeight > 0f
            ? new Rect(inRect.xMin, y, inRect.width, HeaderHeight)
            : new Rect(inRect.xMin, y, inRect.width, 0f);

        if (HeaderHeight > 0f)
            y = Header.yMax + Gap;

        float footerH = FooterHeight > 0f ? FooterHeight : 0f;
        float bodyH = Mathf.Max(0f, inRect.yMax - y - (footerH > 0f ? (Gap + footerH) : 0f));
        Body = new Rect(inRect.xMin, y, inRect.width, bodyH);

        if (footerH > 0f)
        {
            float fy = Body.yMax + Gap;
            Footer = new Rect(inRect.xMin, fy, inRect.width, footerH);
        }
        else
        {
            Footer = new Rect(inRect.xMin, inRect.yMax, inRect.width, 0f);
        }

        // Record regions for overlay/validation (draw pass only).
        if (ctx != null && ctx.Pass == UIPass.Draw)
        {
            ctx.Record(Header, UIRectTag.Header, "ITab/Header");
            ctx.Record(Body, UIRectTag.Body, "ITab/Body");
            if (FooterHeight > 0f)
                ctx.Record(Footer, UIRectTag.Footer, "ITab/Footer");
        }
    }

    /// <summary>
    /// Draw the Body region as a standard scroll view using D2ScrollView.
    /// </summary>
    public void DrawBodyScroll(UIContext ctx, ref Vector2 scroll, ref float cachedContentHeight, D2ScrollView.ContentDrawer drawer, string label = "ITab/BodyScroll")
    {
        if (ctx == null || drawer == null) return;
        D2ScrollView.Draw(ctx, Body, ref scroll, ref cachedContentHeight, drawer, label);
    }
}
