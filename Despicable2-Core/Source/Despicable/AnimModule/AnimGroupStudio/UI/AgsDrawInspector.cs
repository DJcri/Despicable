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
using Despicable.AnimModule.AnimGroupStudio.Model;

namespace Despicable.AnimModule.AnimGroupStudio.UI;
public partial class Dialog_AnimGroupStudio
{
            private void DrawAuthorRight(Rect rect)
            {
                var ctx = frameworkCtx;
                var parts = D2Section.Layout(ctx, rect, new D2Section.Spec("AuthorRight", headerHeight: SectionHeaderHeight, soft: true, pad: true, drawBackground: true, padOverride: ctx.Style.Pad));
                D2Section.DrawCaptionStrip(ctx, parts.Header, "Animation Data", "AuthorRight/Header", GameFont.Medium);
                var v = ctx.VStack(parts.Body);

                if (sourceMode == SourceMode.ExistingDef)
                {
                    v.NextTextBlock(ctx, "Read-only preview of an existing AnimGroupDef. Import it on the left to create a mutable player project.", GameFont.Small, padding: 2f, label: "Right/ExistingInfo");
                    return;
                }

                if (project == null)
                {
                    v.NextTextBlock(ctx, "No project loaded.", GameFont.Small, padding: 2f, label: "Right/NoProject");
                    return;
                }

                var stage = GetStage(project, authorStageIndex);
                if (stage == null)
                {
                    v.NextTextBlock(ctx, "No stage selected.", GameFont.Small, padding: 2f, label: "Right/NoStage");
                    return;
                }

                Rect durRow = v.NextRow(UIRectTag.Input, "Right/DurationRow");
                var durH = new HRow(ctx, durRow);
                D2Widgets.Label(ctx, durH.NextFixed(110f, UIRectTag.Label, "Right/DurationLabel"), "Duration (ticks)", "Right/DurationLabel");
                int durVal = Mathf.Max(1, stage.durationTicks);
                string durStr = durVal.ToString();
                var durRect = durH.Remaining(UIRectTag.TextField, "Right/DurationField");
                ctx.Record(durRect, UIRectTag.TextField, "Right/DurationField");
                if (ctx.Pass == UIPass.Draw)
                    Widgets.TextFieldNumeric(durRect, ref durVal, ref durStr, 1, 60000);
                stage.durationTicks = durVal;

                Rect repRow = v.NextRow(UIRectTag.Input, "Right/RepeatRow");
                var repH = new HRow(ctx, repRow);
                D2Widgets.Label(ctx, repH.NextFixed(110f, UIRectTag.Label, "Right/RepeatLabel"), "Repeat count", "Right/RepeatLabel");
                int rep = Mathf.Max(1, stage.repeatCount);
                string repStr = rep.ToString();
                var repRect = repH.NextFixed(Mathf.Min(90f, Mathf.Max(70f, repRow.width * 0.25f)), UIRectTag.TextField, "Right/RepeatField");
                ctx.Record(repRect, UIRectTag.TextField, "Right/RepeatField");
                if (ctx.Pass == UIPass.Draw)
                    Widgets.TextFieldNumeric(repRect, ref rep, ref repStr, 1, 999999);
                stage.repeatCount = rep;
                bool inf = stage.repeatCount >= 999999;
                Rect infRect = repH.NextFixed(56f, UIRectTag.Checkbox, "Right/InfiniteToggle");
                D2Widgets.CheckboxLabeled(ctx, infRect, "∞", ref inf, "Right/InfiniteToggle");
                if (inf) stage.repeatCount = 999999;
                else if (stage.repeatCount >= 999999) stage.repeatCount = 1;
                if (repH.RemainingWidth > 60f)
                    D2Widgets.Label(ctx, repH.Remaining(UIRectTag.Label, "Right/RepeatHint"), "(1 = normal)", "Right/RepeatHint");

                v.NextSpace(2f);
                v.NextDivider(1f, "Right/Div0");
                v.NextSpace(2f);

                var stackRect = v.NextFill(UIRectTag.Body, "Right/Stacks");
                float paneGap = ctx.Style.Gap * 1.15f;

                // Keep all three editor panes visible together. The list panes still target about
                // three visible rows on a normal-sized window, but when the window gets tighter we
                // stay on the same vertical axis and compress proportionally instead of paging or
                // flipping into a sideways layout.
                const float tracksMinH = 172f;
                const float keysMinH = 166f;
                const float inspectorMinH = 140f;

                var panes = D2PaneLayout.Rows(
                    ctx,
                    stackRect,
                    new[]
                    {
                        new D2PaneLayout.PaneSpec("Tracks", tracksMinH, 208f, 1.0f, canCollapse: false, priority: 0),
                        new D2PaneLayout.PaneSpec("Keys", keysMinH, 202f, 1.0f, canCollapse: false, priority: 0),
                        new D2PaneLayout.PaneSpec("Inspector", inspectorMinH, 320f, 2.4f, canCollapse: false, priority: 0),
                    },
                    gap: paneGap,
                    fallback: D2PaneLayout.FallbackMode.None,
                    label: "Right/EditorPanes");

                DrawTrackList(panes.Rects[0], stage);
                DrawKeyframeList(panes.Rects[1], stage);
                DrawKeyframeInspector(panes.Rects[2], stage);
            }

}
