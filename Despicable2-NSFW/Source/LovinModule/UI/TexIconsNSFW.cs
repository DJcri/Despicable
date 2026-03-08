using UnityEngine;
using Verse;

namespace Despicable;

/// <summary>
/// NSFW-owned texture references for lovin interaction icons.
/// Keeps Core free of NSFW texture paths.
/// </summary>
[StaticConstructorOnStartup]
public static class TexIconsNSFW
{
    public static readonly Texture2D Vaginal = ContentFinder<Texture2D>.Get("UI/Interaction/vaginal");
    public static readonly Texture2D Oral = ContentFinder<Texture2D>.Get("UI/Interaction/oral");
    public static readonly Texture2D Anal = ContentFinder<Texture2D>.Get("UI/Interaction/anal");
}
