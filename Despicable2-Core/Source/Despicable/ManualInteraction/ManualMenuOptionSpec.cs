using System;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Despicable;

/// <summary>
/// Framework-level menu option spec that maps as closely as practical to vanilla FloatMenuOption features.
/// The builder resolves the best available vanilla constructor at runtime and falls back gracefully when a feature is unavailable.
/// </summary>
public sealed class ManualMenuOptionSpec
{
    public string Label { get; set; } = string.Empty;
    public Action Action { get; set; }
    public string Tooltip { get; set; }
    public MenuOptionPriority Priority { get; set; } = MenuOptionPriority.Default;

    public ThingDef ShownItemForIcon { get; set; }
    public Thing IconThing { get; set; }
    public Texture2D IconTex { get; set; }
    public Color? IconColor { get; set; }
    public HorizontalJustification IconJustification { get; set; } = HorizontalJustification.Left;

    public Thing RevalidateClickTarget { get; set; }
    public WorldObject RevalidateWorldClickTarget { get; set; }

    public float ExtraPartWidth { get; set; }
    public Action<Rect> ExtraPartOnGUI { get; set; }

    public Action MouseoverGuiAction { get; set; }

    public bool? CheckboxOn { get; set; }
    public Action ToggleAction { get; set; }

    public bool Disabled { get; set; }
    public string DisabledReason { get; set; }

    public bool IsDisabled => Disabled || (Action == null && ToggleAction == null);

    public static ManualMenuOptionSpec DisabledOption(string label, string disabledReason = null, string tooltip = null)
    {
        return new ManualMenuOptionSpec
        {
            Label = label ?? string.Empty,
            Action = null,
            Disabled = true,
            DisabledReason = disabledReason,
            Tooltip = tooltip
        };
    }
}
