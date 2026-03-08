using UnityEngine;
using Verse;

namespace Despicable.UIFramework;

/// <summary>
/// Single source of truth for vanilla UI textures used by opt-in framework helpers.
/// Loaded once so feature code does not scatter magic paths across the codebase.
/// </summary>
[StaticConstructorOnStartup]
public static class D2VanillaTex
{
    private static Texture2D Load(string path)
    {
        return ContentFinder<Texture2D>.Get(path, reportFailure: false);
    }

    // Close / clear
    public static readonly Texture2D CloseXSmall = Load("UI/Widgets/CloseXSmall");
    public static readonly Texture2D CloseX = Load("UI/Widgets/CloseX");

    // Plus / minus
    public static readonly Texture2D Plus = Load("UI/Buttons/Plus");
    public static readonly Texture2D Minus = Load("UI/Buttons/Minus");

    // Dropdown / reorder
    public static readonly Texture2D Drop = Load("UI/Buttons/Drop");
    public static readonly Texture2D ReorderDown = Load("UI/Buttons/ReorderDown");
    public static readonly Texture2D ReorderUp = Load("UI/Buttons/ReorderUp");

    // Row actions
    public static readonly Texture2D Rename = Load("UI/Buttons/Rename");
    public static readonly Texture2D Copy = Load("UI/Buttons/Copy");
    public static readonly Texture2D Paste = Load("UI/Buttons/Paste");
    public static readonly Texture2D Delete = Load("UI/Buttons/Delete");
    public static readonly Texture2D Dismiss = Load("UI/Buttons/Dismiss");

    // Radios
    public static readonly Texture2D RadioButOn = Load("UI/Widgets/RadioButOn");
    public static readonly Texture2D RadioButOff = Load("UI/Widgets/RadioButOff");

    // Sorting
    public static readonly Texture2D Sorting = Load("UI/Icons/Sorting");
    public static readonly Texture2D SortingDescending = Load("UI/Icons/SortingDescending");

    // Tabs
    public static readonly Texture2D TabAtlas = Load("UI/Widgets/TabAtlas");

    // Search / inspector
    public static readonly Texture2D SearchInspector = Load("UI/Buttons/DevRoot/OpenInspector");
}
