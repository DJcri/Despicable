using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Despicable;
/// <summary>
/// Internal configuration for face part handling
/// and shared helper functions for face parts.
/// </summary>
[StaticConstructorOnStartup]
public static partial class FacePartsUtil
{
    private static readonly string[] HardBlacklistedHeadDefNames =
    {
        "Stump",
        "Head_Stump"
    };

    private static readonly string[] HarPackageIds =
    {
        "erdelf.humanoidalienraces"
    };

    private static readonly HashSet<string> AllowedDefaultDisabledHeadDefNames = new();

    public const string TexturePathBase = "FaceParts/";
    public static string TexPathBase => TexturePathBase;

    private const int DefaultExpressionUpdateInterval = 60;
    private const int DefaultUpdateTickResetOn = 10000;
    // Blink interval should always be lower than update interval reset on.
    private const int DefaultBlinkInterval = 1000;
    private const int DefaultBlinkTickVariance = 240;
    private const int DefaultVisualStatePollInterval = 60;

    public static int ExpressionUpdateInterval => DefaultExpressionUpdateInterval;
    public static int UpdateTickResetOn => DefaultUpdateTickResetOn;
    public static int BlinkInterval => DefaultBlinkInterval;
    public static int BlinkTickVariance => DefaultBlinkTickVariance;
    public static int VisualStatePollInterval => DefaultVisualStatePollInterval;

    public static string GetEyePath(Pawn pawn, string shortPath)
    {
        if (shortPath.NullOrEmpty())
            return string.Empty;

        if (shortPath.StartsWith("Gendered/"))
        {
            CompFaceParts compFaceParts = pawn.TryGetComp<CompFaceParts>();
            return $"FaceParts/Eyes/{shortPath.ReplaceFirst("Gendered/", compFaceParts?.genderPath ?? CompFaceParts.DEFAULT_GENDER_PATH)}";
        }

        return shortPath;
    }
}
