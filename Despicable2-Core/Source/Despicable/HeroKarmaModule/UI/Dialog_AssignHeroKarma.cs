using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Despicable.UIFramework;
using Despicable.UIFramework.Blueprints;
using Despicable.UIFramework.Controls;
using Despicable.UIFramework.Layout;

namespace Despicable.HeroKarma.UI;

public sealed class Dialog_AssignHeroKarma : D2WindowBlueprint
{
    private readonly Pawn _targetPawn;

    private static readonly D2UIStyle AssignStyle = D2UIStyle.Default.With(s =>
    {
        s.HeaderHeight = 44f;
        s.FooterHeight = 40f;
        s.BodyTopPadY = 6f;
        s.BodyBottomPadY = 6f;
    });

    public Dialog_AssignHeroKarma(Pawn targetPawn)
    {
        _targetPawn = targetPawn;
        forcePause = false;
        absorbInputAroundWindow = true;
        closeOnClickedOutside = false;
        doCloseX = true;
        draggable = true;
    }

    public override Vector2 InitialSize => HKUIConstants.AssignWindowSize;

    protected override bool UseBodyScroll => false;
    protected override bool EnableAutoMeasure => true;
    protected override bool EnableAutoMeasureHeader => false;
    protected override D2UIStyle Style => AssignStyle;

    public static void ShowFor(Pawn pawn)
    {
        if (!HKSettingsUtil.ModuleEnabled || pawn == null)
            return;

        Find.WindowStack?.Add(new Dialog_AssignHeroKarma(pawn));
    }

    protected override void DrawHeader(Rect rect)
    {
        using (new TextStateScope(GameFont.Medium, TextAnchor.UpperLeft, false))
            D2Widgets.LabelClipped(Ctx, rect, "D2HK_UI_AssignHero".Translate(), "Title", "D2HK_UI_AssignHero".Translate());
    }

    protected override void DrawBody(Rect rect)
    {
        Pawn currentHero = HKRuntime.GetHeroPawnSafe();
        int karma = HKRuntime.GetGlobalKarma(currentHero);
        int standing = HKRuntime.GetGlobalStanding(currentHero);
        bool showStanding = HKIdeologyCompat.IsStandingEnabled;

        var v = Ctx.VStack(rect);
        v.NextTextBlock(Ctx, "D2HK_UI_AssignHeroBody".Translate(), GameFont.Small, 0f, "Intro");

        DrawSummaryRow(ref v, "D2HK_UI_CurrentHero".Translate(), currentHero != null ? currentHero.LabelShortCap : "D2HK_UI_NoneAssigned".Translate(), "CurrentHero");
        DrawSummaryRow(ref v, "D2HK_UI_NewHero".Translate(), _targetPawn.LabelShortCap, "NewHero");
        DrawSummaryRow(ref v, "D2HK_UI_Karma".Translate(), HKUIData.KarmaSummary(karma), "Karma");

        if (showStanding)
            DrawSummaryRow(ref v, "D2HK_UI_Standing".Translate(), HKUIData.StandingSummary(standing), "Standing");

        string activeEffects = HKUIData.GetActiveEffectNames(karma);
        DrawSummaryRow(ref v, "D2HK_UI_ActiveEffects".Translate(), activeEffects, "Effects");

        if (currentHero == _targetPawn)
            v.NextTextBlock(Ctx, "D2HK_UI_AssignHeroAlready".Translate(), GameFont.Small, 0f, "AlreadyAssigned");
    }

    protected override void DrawFooter(Rect rect)
    {
        string cancelLabel = "D2HK_UI_Cancel".Translate().ToString();
        string assignLabel = "D2HK_UI_Assign".Translate().ToString();
        float buttonWidth = MeasureFooterButtonWidth(cancelLabel, assignLabel, rect.width);

        float buttonGroupWidth = (buttonWidth * 2f) + Ctx.Style.Gap;
        Rect buttonArea = new Rect(
            Mathf.Max(rect.x, rect.xMax - buttonGroupWidth),
            rect.y,
            Mathf.Min(buttonGroupWidth, rect.width),
            rect.height);
        var h = new HRow(Ctx, buttonArea);
        Rect cancelRect = h.NextFixed(buttonWidth, UIRectTag.Button, "Cancel");
        Rect assignRect = h.NextFixed(buttonWidth, UIRectTag.Button, "Assign");

        if (D2Widgets.ButtonText(Ctx, cancelRect, cancelLabel, "Cancel/Button"))
            Close();

        bool disabled = _targetPawn == null || !HKUIData.IsEligibleHero(_targetPawn) || _targetPawn == HKRuntime.GetHeroPawnSafe();
        bool clicked;
        using (new GUIEnabledScope(!disabled))
            clicked = D2Widgets.ButtonText(Ctx, assignRect, assignLabel, "Assign/Button");

        if (clicked)
        {
            HeroKarmaBridge.SetHero(_targetPawn);
            Messages.Message("D2HK_UI_HeroAssigned".Translate(_targetPawn.LabelShortCap), _targetPawn, MessageTypeDefOf.PositiveEvent);
            Close();
        }
    }

    private void DrawSummaryRow(ref VStack v, string left, string right, string id)
    {
        Rect rowRect = v.NextRow(UIRectTag.Input, id + "/Row");
        D2TextPairRow.Draw(Ctx, rowRect, left, right, id: id + "/TextPair", leftWidthFraction: 0.34f);
    }

    private float MeasureFooterButtonWidth(string cancelLabel, string assignLabel, float availableWidth)
    {
        float textWidth = Mathf.Max(MeasureButtonTextWidth(cancelLabel), MeasureButtonTextWidth(assignLabel));
        float padded = textWidth + (Ctx.Style.Pad * 2.4f);
        float maxPerButton = Mathf.Max(Ctx.Style.MinClickSize, (availableWidth - Ctx.Style.GapXS) * 0.5f);
        return Mathf.Clamp(padded, Ctx.Style.MinClickSize, maxPerButton);
    }

    private static float MeasureButtonTextWidth(string text)
    {
        using (new TextStateScope(GameFont.Small, TextAnchor.MiddleCenter, false))
            return Text.CalcSize(text ?? string.Empty).x;
    }
}
