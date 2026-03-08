using RimWorld;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;
using Verse;
using Despicable.AnimGroupStudio.Preview;
using Despicable.AnimGroupStudio;
using Verse.Sound;
using Despicable.UIFramework;
using Despicable.UIFramework.Controls;
using Despicable.UIFramework.Layout;

namespace Despicable.AnimModule.AnimGroupStudio.UI;
public partial class Dialog_AnimGroupStudio
{
            private static bool ButtonTextSelected(Rect rect, string label, bool selected)
            {
                Color prev = GUI.color;
                if (selected) GUI.color = SelectedGreen;
                bool clicked = Widgets.ButtonText(rect, label);
                GUI.color = prev;
                return clicked;
            }

            private static bool SelectableRowButton(Rect rect, string label, bool selected)
            {
                if (selected) Widgets.DrawBoxSolid(rect, SelectedGreenBg);
                Widgets.DrawHighlightIfMouseover(rect);

                bool clicked = Widgets.ButtonInvisible(rect);
                if (clicked) SoundDefOf.Click.PlayOneShotOnCamera();

                var prevAnchor = Text.Anchor;
                Text.Anchor = TextAnchor.MiddleLeft;

                Color prev = GUI.color;
                if (selected) GUI.color = SelectedGreen;
                Widgets.Label(rect.ContractedBy(6f, 0f), label);
                GUI.color = prev;

                Text.Anchor = prevAnchor;
                return clicked;
            }

            private void DrawStudioHeader(Rect rect)
            {
                var ctx = frameworkCtx;
                if (ctx == null)
                    return;

                var v = ctx.VStack(rect, label: "Header/Stack");
                Rect titleRect = v.Next(28f, UIRectTag.Label, "Header/Title");
                Rect subtitleRect = v.Next(24f, UIRectTag.Label, "Header/Subtitle");

                D2Text.DrawWrappedLabel(ctx, titleRect, "Animation Studio", GameFont.Medium, UIRectTag.Label, "Header/TitleText");
                D2Text.DrawWrappedLabel(ctx, subtitleRect, GetStudioSubtitle(), GameFont.Small, UIRectTag.Label, "Header/SubtitleText");
            }

            private string GetStudioSubtitle()
            {
                if (sourceMode == SourceMode.ExistingDef)
                    return "Browse Existing";

                if (!string.IsNullOrWhiteSpace(project?.label))
                    return project.label;

                return "Author";
            }

            private void DrawGroupedHeader(UIContext ctx, ref VStack v, string id, string text, bool topPadding = false)
            {
                if (topPadding)
                    v.NextSpace(Mathf.Max(2f, ctx.Style.Gap * 0.35f));

                Rect band = v.Next(SubsectionHeaderHeight, UIRectTag.Label, id + "/Band");
                D2Section.DrawCaptionStrip(ctx, band, text, id + "/Caption", GameFont.Small);
                v.NextSpace(Mathf.Max(2f, ctx.Style.Gap * 0.25f));
            }

            private bool DrawIconButton(UIContext ctx, Rect rect, Texture2D icon, string tooltip, string id, bool enabled = true, string disabledReason = null)
            {
                ctx?.RecordRect(rect, UIRectTag.Button, id, null);

                if (ctx != null && ctx.Pass == UIPass.Measure)
                    return false;

                if (!disabledReason.NullOrEmpty())
                    TooltipHandler.TipRegion(rect, disabledReason);
                else if (!tooltip.NullOrEmpty())
                    TooltipHandler.TipRegion(rect, tooltip);

                if (!enabled)
                {
                    if (icon != null)
                    {
                        Rect iconRect = rect.ContractedBy(ctx?.Style?.IconInset ?? 2f);
                        using (new GUIColorScope(new Color(1f, 1f, 1f, 0.35f)))
                            GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit, true);
                    }
                    return false;
                }

                return D2Widgets.ButtonIcon(ctx, rect, icon, tooltip, label: id + "/Icon");
            }


}
