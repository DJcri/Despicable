using UnityEngine;

namespace Despicable.UIFramework.Layout;
/// <summary>
/// Opinionated side-rail shell for persistent editor controls.
///
/// A control rail is usually narrower than a full content pane and often hosts:
/// - summary/header content
/// - stacked controls or a list body
/// - optional pinned footer actions
/// </summary>
public static class D2ControlRail
{
    public struct Spec
    {
        public string Id;
        public float HeaderHeight;
        public float FooterHeight;
        public bool DrawBackground;
        public bool Soft;
        public bool Pad;
        public float? PadOverride;

        public Spec(string id, float headerHeight = 0f, float footerHeight = 0f, bool drawBackground = true, bool soft = true, bool pad = true, float? padOverride = null)
        {
            Id = id;
            HeaderHeight = Mathf.Max(0f, headerHeight);
            FooterHeight = Mathf.Max(0f, footerHeight);
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
        public readonly Rect Body;
        public readonly Rect Footer;

        public Parts(Rect outer, Rect inner, Rect header, Rect body, Rect footer)
        {
            Outer = outer;
            Inner = inner;
            Header = header;
            Body = body;
            Footer = footer;
        }
    }

    public static Parts Layout(UIContext ctx, Rect outer, Spec spec)
    {
        var section = D2Section.Layout(
            ctx,
            outer,
            new D2Section.Spec(
                spec.Id,
                headerHeight: spec.HeaderHeight,
                toolbarHeight: 0f,
                footerHeight: spec.FooterHeight,
                soft: spec.Soft,
                pad: spec.Pad,
                drawBackground: spec.DrawBackground,
                padOverride: spec.PadOverride));

        return new Parts(section.Outer, section.Inner, section.Header, section.Body, section.Footer);
    }
}
