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
    private void DrawExistingPreviewTransport(Rect rect)
    {
        var ctx = frameworkCtx;
        var items = new List<D2ActionBar.Item>
        {
            new D2ActionBar.Item("ModeStage", "Stage", D2ActionBar.ItemKind.Selector) { Selected = authorPlayMode == AuthorPlayMode.Stage, MinWidthOverride = 84f },
            new D2ActionBar.Item("ModeGroup", "Group", D2ActionBar.ItemKind.Selector) { Selected = authorPlayMode == AuthorPlayMode.Group, MinWidthOverride = 84f },
            new D2ActionBar.Item("Play", preview.IsPlaying ? "Pause" : (preview.CanResume ? "Resume" : "Play")) { MinWidthOverride = 82f },
            new D2ActionBar.Item("Stop", "Stop") { MinWidthOverride = 74f },
            new D2ActionBar.Item("Speed", authorPreviewSpeed.ToString("0.#") + "x") { MinWidthOverride = 64f },
        };

        var res = D2ActionBar.Draw(ctx, rect, items, "ExistingTransport");
        if (!res.Clicked) return;

        switch (res.ActivatedId)
        {
            case "ModeStage":
                authorPlayMode = AuthorPlayMode.Stage;
                break;
            case "ModeGroup":
                authorPlayMode = AuthorPlayMode.Group;
                break;
            case "Play":
                if (preview.IsPlaying)
                {
                    preview.Pause();
                }
                else if (preview.CanResume)
                {
                    preview.SetSpeed(authorPreviewSpeed);
                    preview.Resume();
                }
                else
                {
                    preview.SetSpeed(authorPreviewSpeed);
                    if (authorPlayMode == AuthorPlayMode.Group)
                    {
                        preview.SelectedStageIndex = 0;
                        preview.Play(fromStageToEnd: true, loopSelectedStage: false);
                    }
                    else
                    {
                        preview.SelectedStageIndex = selectedStageIndex;
                        preview.Play(fromStageToEnd: false, loopSelectedStage: loopCurrentStage);
                    }
                }
                break;
            case "Stop":
                preview.Stop();
                preview.SelectedStageIndex = Mathf.Clamp(selectedStageIndex, 0, Mathf.Max(0, preview.StageCount - 1));
                if (selectedGroup != null && preview.StageCount > 0)
                    preview.ShowSelectedStageAtTick(0);
                break;
            case "Speed":
                Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>
                {
                    new FloatMenuOption("0.5x", () => { authorPreviewSpeed = 0.5f; preview.SetSpeed(authorPreviewSpeed); }),
                    new FloatMenuOption("1x", () => { authorPreviewSpeed = 1f; preview.SetSpeed(authorPreviewSpeed); }),
                    new FloatMenuOption("2x", () => { authorPreviewSpeed = 2f; preview.SetSpeed(authorPreviewSpeed); })
                }));
                break;
        }
    }

    private void DrawAuthorPreviewTransport(Rect rect)
    {
        var ctx = frameworkCtx;
        authorPreviewPlaying = preview.IsPlaying;

        var items = new List<D2ActionBar.Item>
        {
            new D2ActionBar.Item("ModeStage", "Stage", D2ActionBar.ItemKind.Selector) { Selected = authorPlayMode == AuthorPlayMode.Stage, MinWidthOverride = 84f },
            new D2ActionBar.Item("ModeGroup", "Group", D2ActionBar.ItemKind.Selector) { Selected = authorPlayMode == AuthorPlayMode.Group, MinWidthOverride = 84f },
            new D2ActionBar.Item("Play", authorPreviewPlaying ? "Pause" : (preview.CanResume ? "Resume" : "Play")) { MinWidthOverride = 82f },
            new D2ActionBar.Item("Stop", "Stop") { MinWidthOverride = 74f },
            new D2ActionBar.Item("Speed", authorPreviewSpeed.ToString("0.#") + "x") { MinWidthOverride = 64f },
        };
        if (authorPlayMode == AuthorPlayMode.Group)
            items.Add(new D2ActionBar.Item("LoopGroup", "Loop group", D2ActionBar.ItemKind.Checkbox) { Checked = authorLoopGroup, MinWidthOverride = 110f });

        var res = D2ActionBar.Draw(ctx, rect, items, "AuthorTransport");
        if (res.CheckboxValues.TryGetValue("LoopGroup", out bool loopVal))
            authorLoopGroup = loopVal;
        if (!res.Clicked) return;

        switch (res.ActivatedId)
        {
            case "ModeStage":
                authorPlayMode = AuthorPlayMode.Stage;
                break;
            case "ModeGroup":
                authorPlayMode = AuthorPlayMode.Group;
                break;
            case "Play":
                if (preview.IsPlaying)
                {
                    preview.Pause();
                }
                else if (preview.CanResume)
                {
                    authorKeyIndex = -1;
                    preview.SetSpeed(authorPreviewSpeed);
                    preview.Resume();
                }
                else
                {
                    authorKeyIndex = -1;
                    preview.SetSpeed(authorPreviewSpeed);
                    preview.SelectedStageIndex = authorStageIndex;
                    if (authorPlayMode == AuthorPlayMode.Group)
                        preview.Play(fromStageToEnd: true, loopSelectedStage: false, loopSequence: authorLoopGroup);
                    else
                        preview.Play(fromStageToEnd: false, loopSelectedStage: false);
                }
                break;
            case "Stop":
                StopAuthorPreview(resetTick: true);
                break;
            case "Speed":
                Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>
                {
                    new FloatMenuOption("0.5x", () => { authorPreviewSpeed = 0.5f; preview.SetSpeed(authorPreviewSpeed); }),
                    new FloatMenuOption("1x", () => { authorPreviewSpeed = 1f; preview.SetSpeed(authorPreviewSpeed); }),
                    new FloatMenuOption("2x", () => { authorPreviewSpeed = 2f; preview.SetSpeed(authorPreviewSpeed); }),
                }));
                break;
        }

        authorPreviewPlaying = preview.IsPlaying;
        authorPreviewTick = preview.CurrentTick;
    }

    private void DrawPlaybackButtons(Rect rect)
    {
        float y = rect.y;
        float w = rect.width;

        var r1 = new Rect(rect.x, y, w, 28f);
        if (Widgets.ButtonText(r1, "Play selected stage"))
        {
            preview.SetSpeed(authorPreviewSpeed);
            preview.SelectedStageIndex = selectedStageIndex;
            preview.Play(fromStageToEnd: false, loopSelectedStage: loopCurrentStage);
        }
        y += 32f;

        var r2 = new Rect(rect.x, y, w, 28f);
        if (Widgets.ButtonText(r2, "Play group (full)"))
        {
            preview.SetSpeed(authorPreviewSpeed);
            preview.SelectedStageIndex = 0;
            preview.Play(fromStageToEnd: true, loopSelectedStage: false);
        }
        y += 32f;

        var r3 = new Rect(rect.x, y, w, 24f);
        if (Widgets.ButtonText(r3, preview.IsPlaying ? "Stop" : "Stop (idle)"))
        {
            preview.Stop();
        }
    }
}
