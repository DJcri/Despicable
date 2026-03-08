using UnityEngine;
using Verse;

namespace Despicable.UIFramework.Layout;
/// <summary>
/// Lightweight section splitter for panels that need predictable header/body/footer
/// regions inside a panel surface.
/// Drawing remains optional and caller-owned, but layout is centralized.
/// </summary>
public static class D2Section
{
    public struct Spec
    {
        public string Id;
        public float HeaderHeight;
        public float ToolbarHeight;
        public float FooterHeight;
        public bool Soft;
        public bool Pad;
        public bool DrawBackground;
        public float? PadOverride;

        public Spec(string id, float headerHeight = 0f, float toolbarHeight = 0f, float footerHeight = 0f, bool soft = true, bool pad = true, bool drawBackground = true, float? padOverride = null)
        {
            Id = id;
            HeaderHeight = Mathf.Max(0f, headerHeight);
            ToolbarHeight = Mathf.Max(0f, toolbarHeight);
            FooterHeight = Mathf.Max(0f, footerHeight);
            Soft = soft;
            Pad = pad;
            DrawBackground = drawBackground;
            PadOverride = padOverride;
        }
    }

    public readonly struct Parts
    {
        public readonly Rect Outer;
        public readonly Rect Inner;
        public readonly Rect Header;
        public readonly Rect Toolbar;
        public readonly Rect Body;
        public readonly Rect Footer;

        public Parts(Rect outer, Rect inner, Rect header, Rect toolbar, Rect body, Rect footer)
        {
            Outer = outer;
            Inner = inner;
            Header = header;
            Toolbar = toolbar;
            Body = body;
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
            Rect toolbar = Rect.zero;
            Rect body = inner;
            Rect footer = Rect.zero;

            float gap = ctx != null && ctx.Style != null ? ctx.Style.Gap : 6f;
            float usedTop = 0f;
            float usedBottom = 0f;

            if (spec.HeaderHeight > 0f)
            {
                header = new Rect(inner.x, inner.y, inner.width, spec.HeaderHeight);
                usedTop += spec.HeaderHeight;
            }

            if (spec.ToolbarHeight > 0f)
            {
                if (usedTop > 0f) usedTop += gap;
                toolbar = new Rect(inner.x, inner.y + usedTop, inner.width, spec.ToolbarHeight);
                usedTop += spec.ToolbarHeight;
            }

            if (spec.FooterHeight > 0f)
            {
                usedBottom = spec.FooterHeight;
                footer = new Rect(inner.x, inner.yMax - spec.FooterHeight, inner.width, spec.FooterHeight);
            }

            float bodyY = inner.y + usedTop;
            if (usedTop > 0f)
                bodyY += gap;

            float bodyBottomInset = usedBottom;
            if (usedBottom > 0f)
                bodyBottomInset += gap;

            float bodyH = Mathf.Max(0f, inner.yMax - bodyY - bodyBottomInset);
            body = new Rect(inner.x, bodyY, inner.width, bodyH);

            if (ctx != null)
            {
                string root = string.IsNullOrEmpty(spec.Id) ? "Section" : spec.Id;
                if (header.height > 0f) ctx.RecordRect(header, UIRectTag.Label, root + "/Header", null);
                if (toolbar.height > 0f) ctx.RecordRect(toolbar, UIRectTag.Input, root + "/Toolbar", null);
                ctx.RecordRect(body, UIRectTag.Body, root + "/Body", null);
                if (footer.height > 0f) ctx.RecordRect(footer, UIRectTag.Footer, root + "/Footer", null);
            }

            return new Parts(outer, inner, header, toolbar, body, footer);
        }
    }

    public static void DrawCaptionStrip(UIContext ctx, Rect rect, string text, string id, GameFont font = GameFont.Medium, TextAnchor anchor = TextAnchor.MiddleLeft)
    {
        if (ctx == null)
            return;

        string root = string.IsNullOrEmpty(id) ? "SectionCaption" : id;
        ctx.RecordRect(rect, UIRectTag.Label, root, text);

        Color fill = new Color(1f, 1f, 1f, font == GameFont.Medium ? 0.06f : 0.04f);
        D2Widgets.DrawBoxSolid(ctx, rect, fill, root + "/Fill");
        D2Widgets.DrawDivider(ctx, new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), root + "/Divider");

        Rect textRect = rect.ContractedBy(ctx.Style.TextInsetX, 0f);
        using (new TextStateScope(font, anchor, false))
            D2Widgets.LabelClippedAligned(ctx, textRect, text ?? string.Empty, anchor, root + "/Text");
    }

    public static void DrawCaptionStripKey(UIContext ctx, Rect rect, string key, string id, GameFont font = GameFont.Medium, TextAnchor anchor = TextAnchor.MiddleLeft, params object[] args)
    {
        string text = (args != null && args.Length > 0) ? key.Translate(args).ToString() : key.Translate().ToString();
        DrawCaptionStrip(ctx, rect, text, id, font, anchor);
    }
}

