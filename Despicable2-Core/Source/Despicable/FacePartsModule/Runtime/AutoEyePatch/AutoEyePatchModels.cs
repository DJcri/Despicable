using System;
using UnityEngine;

namespace Despicable;

[Flags]
internal enum AutoEyePatchSkipReason
{
    None = 0,
    TextureMissing = 1 << 0,
    TextureUnreadable = 1 << 1,
    HeadIneligible = 1 << 2,
    NoDarkCandidate = 1 << 3,
    PairInvalid = 1 << 4,
    TooCloseToOutline = 1 << 5,
    ColorUnstable = 1 << 6,
    TooNoisy = 1 << 7,
    TooAsymmetric = 1 << 8,
    TooManyCandidates = 1 << 9,
    GenerationError = 1 << 10,
}

internal enum AutoEyePatchHeadStatus
{
    Generated,
    Partial,
    Skipped,
    Failed,
}

internal enum AutoEyePatchVariantStatus
{
    Generated,
    Skipped,
    None,
}

internal enum AutoEyePatchFacingSlot
{
    North,
    South,
    East,
    West,
}

internal enum AutoEyePatchEnvelopeType
{
    Ellipse,
    RoundedBox,
    Blob,
}

internal sealed class AutoEyePatchDescriptor
{
    public AutoEyePatchEnvelopeType EnvelopeType;
    public Vector2 CenterUV;
    public Vector2 RadiusUV;
    public float FeatherUV;
    public Color FillColor;
    public float Confidence;
    public float OutlineSafetyScore;
    public float LocalColorStabilityScore;
    public float FootprintCompactnessScore;
    public Texture2D RuntimeTexture;
}

internal sealed class AutoEyePatchVariantResult
{
    public AutoEyePatchFacingSlot Slot;
    public AutoEyePatchVariantStatus Status;
    public AutoEyePatchDescriptor Primary;
    public AutoEyePatchDescriptor Secondary;
    public AutoEyePatchSkipReason Reasons;

    public bool HasPrimary => Primary != null && Primary.RuntimeTexture != null;
    public bool HasSecondary => Secondary != null && Secondary.RuntimeTexture != null;
}

internal sealed class AutoEyePatchHeadResult
{
    public string HeadKey;
    public string GraphicPath;
    public AutoEyePatchHeadStatus Status;
    public bool ReplacesLegacyEyeBase;
    public float Confidence;
    public AutoEyePatchVariantResult North;
    public AutoEyePatchVariantResult South;
    public AutoEyePatchVariantResult East;
    public AutoEyePatchVariantResult West;
    public AutoEyePatchSkipReason SummaryReasons;
    public int Version;
}

internal sealed class AutoEyePatchTextureAnalysis
{
    public string TextureKey;
    public string TexturePath;
    public Texture2D RuntimeTexture;
    public int Width;
    public int Height;
    public RectInt OpaqueBounds;
    public RectInt SafeInteriorBounds;
    public System.Collections.Generic.List<AutoEyePatchDarkCandidate> Candidates = new();
    public AutoEyePatchSkipReason AnalysisReasons;
    public int Version;
}

internal sealed class AutoEyePatchDarkCandidate
{
    public RectInt BoundsPx;
    public Vector2 CenterUV;
    public Vector2 RadiusUV;
    public float DarknessScore;
    public float CompactnessScore;
    public float OutlineSafetyScore;
    public System.Collections.Generic.List<Vector2Int> FootprintPixels = new();
    public float[] CroppedAlpha = System.Array.Empty<float>();
    public int CroppedAlphaWidth;
    public int CroppedAlphaHeight;
}

internal readonly struct AutoEyePatchRenderSelection
{
    public readonly bool SuppressLegacy;
    public readonly Texture2D RuntimeTexture;
    public readonly AutoEyePatchDescriptor Descriptor;
    public readonly bool MirrorDescriptorX;

    public AutoEyePatchRenderSelection(bool suppressLegacy, Texture2D runtimeTexture, AutoEyePatchDescriptor descriptor, bool mirrorDescriptorX = false)
    {
        SuppressLegacy = suppressLegacy;
        RuntimeTexture = runtimeTexture;
        Descriptor = descriptor;
        MirrorDescriptorX = mirrorDescriptorX;
    }
}

internal sealed class AutoEyePatchStartupSummary
{
    public int HeadsScanned;
    public int HeadsEligible;
    public int HeadsGenerated;
    public int HeadsPartial;
    public int HeadsSkipped;
    public int HeadsFailed;
}
