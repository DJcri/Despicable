using UnityEngine;

namespace Despicable.UIFramework.Layout;
/// <summary>
/// Canonical editor window shell allocator.
///
/// Provides stable named regions for editor-style windows:
/// - header
/// - optional left rail
/// - workspace
/// - optional right preview rail
/// - footer command bar
///
/// This allocates and optionally draws the shell surface. Callers still own the actual content.
/// </summary>
public static class D2EditorShell
{
    public struct Spec
    {
        public string Id;
        public float HeaderHeight;
        public float FooterHeight;
        public float LeftRailWidth;
        public float RightRailWidth;
        public bool DrawBackground;
        public bool Soft;
        public bool Pad;
        public float? PadOverride;

        public Spec(string id, float headerHeight = 0f, float footerHeight = 0f, float leftRailWidth = 0f, float rightRailWidth = 0f, bool drawBackground = true, bool soft = true, bool pad = true, float? padOverride = null)
        {
            Id = id;
            HeaderHeight = Mathf.Max(0f, headerHeight);
            FooterHeight = Mathf.Max(0f, footerHeight);
            LeftRailWidth = Mathf.Max(0f, leftRailWidth);
            RightRailWidth = Mathf.Max(0f, rightRailWidth);
            DrawBackground = drawBackground;
            Soft = soft;
            Pad = pad;
            PadOverride = padOverride;
        }
    }

    public readonly struct Parts
    {
        public readonly Rect Outer;
        public readonly Rect Inner;
        public readonly Rect Header;
        public readonly Rect LeftRail;
        public readonly Rect Workspace;
        public readonly Rect RightRail;
        public readonly Rect Footer;

        public Parts(Rect outer, Rect inner, Rect header, Rect leftRail, Rect workspace, Rect rightRail, Rect footer)
        {
            Outer = outer;
            Inner = inner;
            Header = header;
            LeftRail = leftRail;
            Workspace = workspace;
            RightRail = rightRail;
            Footer = footer;
        }
    }

    public static Parts Layout(UIContext ctx, Rect outer, Spec spec)
    {
        using (var g = ctx.GroupPanel(
            spec.Id,
            outer,
            soft: spec.Soft,
            pad: spec.Pad,
            padOverride: spec.PadOverride,
            drawBackground: spec.DrawBackground,
            label: spec.Id))
        {
            Rect inner = g.Inner;
            Rect header = Rect.zero;
            Rect footer = Rect.zero;
            Rect middle = inner;

            float gap = ctx != null && ctx.Style != null ? ctx.Style.Gap : 6f;

            if (spec.HeaderHeight > 0f)
            {
                header = new Rect(inner.x, inner.y, inner.width, spec.HeaderHeight);
                middle.yMin = Mathf.Min(inner.yMax, header.yMax + gap);
            }

            if (spec.FooterHeight > 0f)
            {
                footer = new Rect(inner.x, inner.yMax - spec.FooterHeight, inner.width, spec.FooterHeight);
                middle.yMax = Mathf.Max(middle.yMin, footer.yMin - gap);
            }

            Rect left = Rect.zero;
            Rect right = Rect.zero;
            Rect workspace = middle;

            if (spec.LeftRailWidth > 0f)
            {
                float w = Mathf.Min(spec.LeftRailWidth, workspace.width);
                left = new Rect(workspace.x, workspace.y, w, workspace.height);
                workspace.xMin = Mathf.Min(workspace.xMax, left.xMax + gap);
            }

            if (spec.RightRailWidth > 0f && workspace.width > 0f)
            {
                float w = Mathf.Min(spec.RightRailWidth, workspace.width);
                right = new Rect(workspace.xMax - w, workspace.y, w, workspace.height);
                workspace.xMax = Mathf.Max(workspace.xMin, right.xMin - gap);
            }

            if (ctx != null)
            {
                string root = string.IsNullOrEmpty(spec.Id) ? "EditorShell" : spec.Id;
                if (header.height > 0f) ctx.RecordRect(header, UIRectTag.Header, root + "/Header", null);
                if (left.width > 0f) ctx.RecordRect(left, UIRectTag.PanelSoft, root + "/LeftRail", null);
                ctx.RecordRect(workspace, UIRectTag.Body, root + "/Workspace", null);
                if (right.width > 0f) ctx.RecordRect(right, UIRectTag.PanelSoft, root + "/RightRail", null);
                if (footer.height > 0f) ctx.RecordRect(footer, UIRectTag.Footer, root + "/Footer", null);
            }

            return new Parts(outer, inner, header, left, workspace, right, footer);
        }
    }
}
