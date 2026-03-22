using UnityEngine;
using Verse;

namespace Despicable;
internal sealed class Dialog_AnatomyDebugReport : Window
{
    private readonly string report;
    private Vector2 scrollPosition;
    private float viewHeight;

    internal Dialog_AnatomyDebugReport(string report)
    {
        this.report = report ?? "<no report>";
        doCloseX = true;
        closeOnClickedOutside = false;
        absorbInputAroundWindow = true;
        draggable = true;
        resizeable = true;
        optionalTitle = "Anatomy Debug";
    }

    public override Vector2 InitialSize => new Vector2(920f, 720f);

    public override void DoWindowContents(Rect inRect)
    {
        Text.Font = GameFont.Small;

        float footerHeight = 32f;
        Rect bodyRect = new Rect(inRect.x, inRect.y, inRect.width, inRect.height - footerHeight - 8f);
        Rect closeRect = new Rect(inRect.x + inRect.width - 120f, inRect.y + inRect.height - footerHeight, 120f, 30f);

        float bodyWidth = bodyRect.width - 16f;
        float requiredHeight = Text.CalcHeight(report, bodyWidth);
        viewHeight = Mathf.Max(viewHeight, requiredHeight + 12f);
        Rect viewRect = new Rect(0f, 0f, bodyWidth, viewHeight);

        Widgets.BeginScrollView(bodyRect, ref scrollPosition, viewRect);
        Widgets.Label(new Rect(0f, 0f, viewRect.width, requiredHeight + 8f), report);
        Widgets.EndScrollView();

        if (Widgets.ButtonText(closeRect, "Close"))
            Close();
    }
}
