using RimWorld;
using System;
using UnityEngine;
using Verse;

namespace Despicable;
/// <summary>
/// Tiny reusable text-entry dialog (rename, label edits, etc.).
/// Kept local to the mod to avoid relying on RimWorld internal rename dialogs.
/// </summary>
public class Dialog_TextEntrySimple : Window
{
    private readonly string title;
    private readonly Action<string> onAccept;
    private readonly Func<string, string> validator;

    private string text;
    private string error;

    public override Vector2 InitialSize => new Vector2(520f, 180f);

    public Dialog_TextEntrySimple(string title, string initialText, Action<string> onAccept, Func<string, string> validator = null)
    {
        this.title = title ?? "";
        this.text = initialText ?? "";
        this.onAccept = onAccept;
        this.validator = validator;

        doCloseX = true;
        closeOnClickedOutside = true;
        absorbInputAroundWindow = true;
    }

    public override void DoWindowContents(Rect inRect)
    {
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 34f), title);
        Text.Font = GameFont.Small;

        float y = inRect.y + 42f;
        var fieldRect = new Rect(inRect.x, y, inRect.width, 30f);
        text = Widgets.TextField(fieldRect, text);
        y = fieldRect.yMax + 8f;

        // Validate live (lightweight)
        error = validator?.Invoke(text);
        if (!error.NullOrEmpty())
        {
            GUI.color = ColorLibrary.Red;
            Widgets.Label(new Rect(inRect.x, y, inRect.width, 22f), error);
            GUI.color = Color.white;
        }

        // Buttons
        float butW = 120f;
        float butH = 34f;
        var cancelRect = new Rect(inRect.xMax - (butW * 2f) - 10f, inRect.yMax - butH, butW, butH);
        var okRect = new Rect(inRect.xMax - butW, inRect.yMax - butH, butW, butH);

        if (Widgets.ButtonText(cancelRect, "Cancel"))
            Close();

        GUI.enabled = error.NullOrEmpty();
        if (Widgets.ButtonText(okRect, "OK"))
        {
            onAccept?.Invoke(text?.Trim() ?? "");
            Close();
        }
        GUI.enabled = true;

        if (Event.current != null && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
        {
            if (error.NullOrEmpty())
            {
                onAccept?.Invoke(text?.Trim() ?? "");
                Close();
            }
            Event.current.Use();
        }
    }
}
