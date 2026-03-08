using System;
using UnityEngine;
using Verse;

namespace Despicable.UIFramework;
/// <summary>
/// IMGUI is global-state driven. These small scopes ensure we always restore GUI/Text state
/// so debug overlays or widgets can't accidentally poison the rest of the UI draw.
/// </summary>
public readonly struct GUIColorScope : IDisposable
{
    private readonly Color _prev;
    public GUIColorScope(Color c)
    {
        _prev = GUI.color;
        GUI.color = c;
    }
    public void Dispose() => GUI.color = _prev;
}

public readonly struct GUIEnabledScope : IDisposable
{
    private readonly bool _prev;
    public GUIEnabledScope(bool enabled)
    {
        _prev = GUI.enabled;
        GUI.enabled = enabled;
    }
    public void Dispose() => GUI.enabled = _prev;
}

public readonly struct TextStateScope : IDisposable
{
    private readonly GameFont _font;
    private readonly TextAnchor _anchor;
    private readonly bool _wordWrap;

    public TextStateScope(GameFont font, TextAnchor anchor, bool wordWrap)
    {
        _font = Text.Font;
        _anchor = Text.Anchor;
        _wordWrap = Text.WordWrap;

        Text.Font = font;
        Text.Anchor = anchor;
        Text.WordWrap = wordWrap;
    }

    public void Dispose()
    {
        Text.Font = _font;
        Text.Anchor = _anchor;
        Text.WordWrap = _wordWrap;
    }
}
