using System;
using UnityEngine;
using Verse;

namespace Despicable.AnimModule.AnimGroupStudio.UI;

/// <summary>
/// Tiny modal for entering a new freeform stage tag.
/// Confirmed via Enter key or the "Add" button; dismissed via Escape or clicking outside.
/// </summary>
public sealed class Dialog_AgsAddTag : Window
{
    private readonly Action<string> _onConfirm;
    private string _buffer = "";
    private const string ControlName = "AgsAddTagField";

    public Dialog_AgsAddTag(Action<string> onConfirm)
    {
        _onConfirm = onConfirm;
        forcePause = false;
        absorbInputAroundWindow = false;
        closeOnClickedOutside = true;
        doCloseX = false;
        doCloseButton = false;
        draggable = false;
        resizeable = false;
    }

    public override Vector2 InitialSize => new(280f, 96f);

    public override void DoWindowContents(Rect inRect)
    {
        // Focus the text field on first frame.
        if (Event.current.type == EventType.Layout)
            Verse.UI.FocusControl(ControlName, this);

        float pad = 8f;
        float rowH = 28f;
        float btnW = 56f;
        float gap = 6f;

        float y = inRect.yMin + pad;
        float fieldW = inRect.width - pad * 2f - btnW - gap;

        Rect fieldRect = new(inRect.xMin + pad, y, fieldW, rowH);
        Rect addRect = new(fieldRect.xMax + gap, y, btnW, rowH);

        GUI.SetNextControlName(ControlName);
        _buffer = Widgets.TextField(fieldRect, _buffer);

        // Confirm on Enter
        if (Event.current.type == EventType.KeyDown &&
            (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter))
        {
            Confirm();
            Event.current.Use();
        }

        if (Widgets.ButtonText(addRect, "Add"))
            Confirm();

        // Hint text
        Rect hintRect = new(inRect.xMin + pad, fieldRect.yMax + gap, inRect.width - pad * 2f, rowH);
        GameFont prev = Text.Font;
        Text.Font = GameFont.Tiny;
        GUI.color = Color.gray;
        Widgets.Label(hintRect, "e.g.  lovin_oral   lovin_anal");
        GUI.color = Color.white;
        Text.Font = prev;
    }

    private void Confirm()
    {
        string trimmed = (_buffer ?? "").Trim();
        if (!trimmed.NullOrEmpty())
            _onConfirm?.Invoke(trimmed);
        Close();
    }
}
