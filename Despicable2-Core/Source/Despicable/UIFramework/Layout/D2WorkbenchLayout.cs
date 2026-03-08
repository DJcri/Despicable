using UnityEngine;

namespace Despicable.UIFramework.Layout;
/// <summary>
/// Named 3-pane workbench allocator: browser + details + preview.
///
/// Thin wrapper over D2PaneLayout so common editor workspaces do not re-specify the same panes.
/// </summary>
public static class D2WorkbenchLayout
{
    public struct Spec
    {
        public D2PaneLayout.PaneSpec Browser;
        public D2PaneLayout.PaneSpec Details;
        public D2PaneLayout.PaneSpec Preview;
        public D2PaneLayout.FallbackMode Fallback;

        public Spec(
            D2PaneLayout.PaneSpec browser,
            D2PaneLayout.PaneSpec details,
            D2PaneLayout.PaneSpec preview,
            D2PaneLayout.FallbackMode fallback = D2PaneLayout.FallbackMode.HideLowPriority)
        {
            Browser = browser;
            Details = details;
            Preview = preview;
            Fallback = fallback;
        }
    }

    public readonly struct Parts
    {
        public readonly Rect Browser;
        public readonly Rect Details;
        public readonly Rect Preview;
        public readonly D2PaneLayout.LayoutResult Layout;

        public Parts(Rect browser, Rect details, Rect preview, D2PaneLayout.LayoutResult layout)
        {
            Browser = browser;
            Details = details;
            Preview = preview;
            Layout = layout;
        }
    }

    public static Parts Columns(UIContext ctx, Rect outer, Spec spec, float? gap = null, string label = null)
    {
        var layout = D2PaneLayout.Columns(
            ctx,
            outer,
            new[] { spec.Browser, spec.Details, spec.Preview },
            gap: gap,
            fallback: spec.Fallback,
            label: label ?? "Workbench");

        Rect browser = layout.Rects != null && layout.Rects.Length > 0 ? layout.Rects[0] : Rect.zero;
        Rect details = layout.Rects != null && layout.Rects.Length > 1 ? layout.Rects[1] : Rect.zero;
        Rect preview = layout.Rects != null && layout.Rects.Length > 2 ? layout.Rects[2] : Rect.zero;
        return new Parts(browser, details, preview, layout);
    }
}
