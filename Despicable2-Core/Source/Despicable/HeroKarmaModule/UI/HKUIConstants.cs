using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Despicable.HeroKarma.UI;

[StaticConstructorOnStartup]
public static class HKUIConstants
{
    public static readonly Vector2 WindowSize = new(920f, 740f);
    public static readonly Vector2 AssignWindowSize = new(520f, 360f);

    public const float Pad = 10f;
    public const float Gap = 8f;
    public const float HeroPortraitSize = 72f;

    public static readonly Texture2D KarmaIcon;
    public static readonly Texture2D StandingIcon;
    public static readonly Texture2D HeroGizmoIcon;

    private static readonly Dictionary<string, Texture2D> PerkIconCache = new();

    static HKUIConstants()
    {
        KarmaIcon = ContentFinder<Texture2D>.Get("UI/Karma/KarmaIcon", false);
        StandingIcon = ContentFinder<Texture2D>.Get("UI/Karma/StandingIcon", false);
        HeroGizmoIcon = ContentFinder<Texture2D>.Get("UI/Karma/HeroGizmo", false);
    }

    public static Texture2D GetPerkIcon(string iconKey)
    {
        if (iconKey.NullOrEmpty())
            return null;

        if (PerkIconCache.TryGetValue(iconKey, out Texture2D cached))
            return cached;

        Texture2D tex = ContentFinder<Texture2D>.Get("UI/Karma/Perks/" + iconKey, false);
        PerkIconCache[iconKey] = tex;
        return tex;
    }
}
