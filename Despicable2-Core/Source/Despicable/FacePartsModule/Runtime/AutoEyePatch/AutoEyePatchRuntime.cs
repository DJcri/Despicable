using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using UnityEngine;

// Guardrail-Reason: Auto eye patch runtime state stays centralized because generation flags, caches, and refresh queues reset together.
namespace Despicable;

internal static class AutoEyePatchRuntime
{
    private static readonly Dictionary<string, AutoEyePatchTextureAnalysis> _textureAnalysisByKey = new();
    private static readonly Dictionary<string, AutoEyePatchHeadResult> _headResultsByKey = new();
    private static readonly Dictionary<Texture2D, Texture2D> _flippedRuntimeTextures = new();
    private static readonly HashSet<int> _pendingFaceRefreshPawnIds = new();
    private static readonly Queue<string> _pendingHeadGenerationKeys = new();
    private static readonly HashSet<string> _queuedHeadGenerationKeys = new();
    private static readonly Dictionary<string, HeadTypeDef> _queuedHeadTypesByKey = new();
    private static readonly Dictionary<string, HashSet<int>> _waitingPawnIdsByHeadKey = new();
    // Guardrail-Allow-Static: Runtime queue gate owned by AutoEyePatchRuntime; reset via ResetRuntimeState() with the rest of the face-refresh queue.
    private static bool _hasPendingFaceRefresh;
    private static AutoEyePatchStartupSummary _lastSummary = new();
    // Guardrail-Allow-Static: Startup generation flag owned by AutoEyePatchRuntime; reset via ResetRuntimeState() on load/new game transitions.
    private static bool _generatedThisLoad;
    // Guardrail-Allow-Static: Startup attempt flag owned by AutoEyePatchRuntime; reset via ResetRuntimeState() on load/new game transitions.
    private static bool _generationAttemptedThisLoad;
    // Guardrail-Allow-Static: Warn-once gate owned by AutoEyePatchRuntime; reset via ResetRuntimeState() so each load can emit fresh diagnostics.
    private static bool _loggedMissingGenerationThisLoad;
    // Guardrail-Allow-Static: Warn-once probe gate owned by AutoEyePatchRuntime; reset via ResetRuntimeState() so each load can emit fresh diagnostics.
    private static bool _loggedEyeBaseProbeThisLoad;

    internal static int GenerationVersion => 18;
    private static bool ShouldEmitVerboseDiagnostics => Prefs.DevMode;


    private static Texture2D GetOrCreateFlipped(Texture2D source)
    {
        if (source == null)
            return null;

        if (_flippedRuntimeTextures.TryGetValue(source, out Texture2D cached) && cached != null)
            return cached;

        int width = source.width;
        int height = source.height;
        Texture2D flipped = new Texture2D(width, height, TextureFormat.RGBA32, false);
        flipped.name = source.name + "_flipX";
        flipped.filterMode = FilterMode.Bilinear;
        flipped.wrapMode = source.wrapMode;

        int levelWidth = width;
        int levelHeight = height;
        Color32[] src = source.GetPixels32(0);
        Color32[] dst = new Color32[src.Length];

        for (int y = 0; y < levelHeight; y++)
        {
            int row = y * levelWidth;
            for (int x = 0; x < levelWidth; x++)
            {
                dst[row + x] = src[row + (levelWidth - 1 - x)];
            }
        }

        flipped.SetPixels32(dst, 0);
        flipped.Apply(false, false);

        _flippedRuntimeTextures[source] = flipped;
        return flipped;
    }



    public static void EnsureGenerated()
    {
        if (!IsGenerationEnabled())
            return;

        _generationAttemptedThisLoad = true;
    }

    public static void ResetRuntimeState()
    {
        DestroyGeneratedRuntimeTextures();
        AutoEyePatchAnalyzer.ResetRuntimeState();
        _generatedThisLoad = false;
        _generationAttemptedThisLoad = false;
        _loggedMissingGenerationThisLoad = false;
        _loggedEyeBaseProbeThisLoad = false;
        _textureAnalysisByKey.Clear();
        _headResultsByKey.Clear();
        _flippedRuntimeTextures.Clear();
        _pendingFaceRefreshPawnIds.Clear();
        _pendingHeadGenerationKeys.Clear();
        _queuedHeadGenerationKeys.Clear();
        _queuedHeadTypesByKey.Clear();
        _waitingPawnIdsByHeadKey.Clear();
        _hasPendingFaceRefresh = false;
        _lastSummary = new AutoEyePatchStartupSummary();
    }

    public static bool HasPendingFaceRefresh => _hasPendingFaceRefresh;

    public static void QueuePendingFaceRefresh(Pawn pawn)
    {
        if (pawn == null)
            return;

        CompFaceParts comp = pawn.TryGetComp<CompFaceParts>();
        if (comp != null)
        {
            comp.NotifyPendingAutoEyePatchFaceRefreshQueued();
            return;
        }

        if (_pendingFaceRefreshPawnIds.Add(pawn.thingIDNumber))
            _hasPendingFaceRefresh = true;
    }

    public static bool TryConsumePendingFaceRefresh(Pawn pawn)
    {
        if (pawn == null || !_hasPendingFaceRefresh)
            return false;

        bool removed = _pendingFaceRefreshPawnIds.Remove(pawn.thingIDNumber);
        if (_pendingFaceRefreshPawnIds.Count == 0)
            _hasPendingFaceRefresh = false;

        return removed;
    }

    public static Texture2D GetMirroredRuntimeTexture(Texture2D source)
    {
        return GetOrCreateFlipped(source);
    }

    public static bool TryGetOrEnsureHeadResult(HeadTypeDef headType, out AutoEyePatchHeadResult result)
    {
        result = null;
        if (headType == null || !IsGenerationEnabled())
            return false;

        EnsureGeneratedForHeadImmediate(headType);
        return TryGetHeadResult(headType, out result);
    }

    public static bool TryGetOrRequestHeadResult(HeadTypeDef headType, Pawn pawn, out AutoEyePatchHeadResult result)
    {
        result = null;
        if (headType == null || !IsGenerationEnabled())
            return false;

        if (TryGetHeadResult(headType, out result))
            return true;

        if (ShouldGenerateImmediatelyNow())
        {
            EnsureGeneratedForHeadImmediate(headType);
            return TryGetHeadResult(headType, out result);
        }

        RequestHeadGeneration(headType, pawn);
        return false;
    }

    public static void PrewarmForPawn(Pawn pawn)
    {
        if (pawn == null || !IsGenerationEnabled())
            return;

        EnsureGeneratedForHeadImmediate(pawn.story?.headType);
    }

    public static bool TryGetHeadResult(HeadTypeDef headType, out AutoEyePatchHeadResult result)
    {
        result = null;
        if (headType == null)
            return false;

        return _headResultsByKey.TryGetValue(GetHeadKey(headType), out result);
    }

    public static bool TryResolveEyeBaseReplacement(Pawn pawn, PawnRenderNode node, Rot4 facing, out AutoEyePatchRenderSelection selection)
    {
        selection = default;

        if (ShouldEmitVerboseDiagnostics && !_loggedEyeBaseProbeThisLoad)
        {
            Settings probeSettings = ModMain.Instance?.settings;
            string headDef = pawn?.story?.headType?.defName ?? "<null>";
            string nodeLabel = node?.Props?.debugLabel ?? "<null>";
            Log.Message($"[Despicable] EYEBASE-PROBE enabled={(probeSettings != null && probeSettings.facialPartsExtensionEnabled && probeSettings.experimentalAutoEyePatchEnabled && !ModMain.IsNlFacialInstalled)} facialExt={(probeSettings?.facialPartsExtensionEnabled ?? false)} autoEye={(probeSettings?.experimentalAutoEyePatchEnabled ?? false)} nlFacial={ModMain.IsNlFacialInstalled} generated={_generatedThisLoad} attempted={_generationAttemptedThisLoad} head={headDef} facing={facing} node={nodeLabel} pawnsAlive={PawnsFinder.AllMapsAndWorld_Alive.Count}");
            _loggedEyeBaseProbeThisLoad = true;
        }
        if (pawn == null || node == null || !IsGenerationEnabled())
            return false;

        HeadTypeDef headType = pawn.story?.headType;
        if (headType == null)
            return false;

        if (!TryGetOrRequestHeadResult(headType, pawn, out AutoEyePatchHeadResult result) || !result.ReplacesLegacyEyeBase)
            return false;

        string debugLabel = node.Props?.debugLabel ?? string.Empty;

        if (facing == Rot4.South)
        {
            bool isRight = debugLabel.EndsWith("_R", System.StringComparison.OrdinalIgnoreCase);
            if (isRight)
            {
                Texture2D rightTex = result.South?.Secondary?.RuntimeTexture ?? result.South?.Primary?.RuntimeTexture;
                if (rightTex != null)
                {
                    selection = new AutoEyePatchRenderSelection(true, GetOrCreateFlipped(rightTex), result.South?.Secondary ?? result.South?.Primary, mirrorDescriptorX: true);
                    return true;
                }
                return false;
            }

            if (result.South?.Primary?.RuntimeTexture != null)
            {
                selection = new AutoEyePatchRenderSelection(true, result.South.Primary.RuntimeTexture, result.South.Primary);
                return true;
            }

            return false;
        }

        if (facing == Rot4.East && result.East?.Primary?.RuntimeTexture != null)
        {
            selection = new AutoEyePatchRenderSelection(true, result.East.Primary.RuntimeTexture, result.East.Primary);
            return true;
        }

        if (facing == Rot4.West && (result.West?.Primary?.RuntimeTexture != null || result.East?.Primary?.RuntimeTexture != null))
        {
            Texture2D runtimeTexture = result.West?.Primary?.RuntimeTexture ?? result.East.Primary.RuntimeTexture;
            selection = new AutoEyePatchRenderSelection(true, runtimeTexture, result.West?.Primary ?? result.East?.Primary, mirrorDescriptorX: true);
            return true;
        }

        return false;
    }

    public static AutoEyePatchStartupSummary GetLastSummary() => _lastSummary;

    private static void GenerateAll()
    {
        AutoEyePatchDiagnostics diagnostics = new();
        foreach (HeadTypeDef headType in DefDatabase<HeadTypeDef>.AllDefsListForReading)
        {
            diagnostics.RecordScanned();
            GenerateForHead(headType, diagnostics);
        }

        _lastSummary = diagnostics.BuildSummary();
        Log.Message($"[Despicable] AEP-RUNTIME-PROBE INLINE genVer={GenerationVersion}");
        Log.Message(diagnostics.BuildLogSummary());
    }

    private static void GenerateForHead(HeadTypeDef headType, AutoEyePatchDiagnostics diagnostics)
    {
        AutoEyePatchEligibility.Result eligibility = AutoEyePatchEligibility.Evaluate(headType);
        if (eligibility.Eligible)
            diagnostics?.RecordEligible();

        string headKey = eligibility.HeadKey ?? GetHeadKey(headType);

        AutoEyePatchHeadResult headResult;
        if (!eligibility.Eligible)
        {
            headResult = new AutoEyePatchHeadResult
            {
                HeadKey = headKey,
                GraphicPath = eligibility.GraphicPath,
                Status = AutoEyePatchHeadStatus.Skipped,
                SummaryReasons = eligibility.Reasons,
                North = new AutoEyePatchVariantResult { Slot = AutoEyePatchFacingSlot.North, Status = AutoEyePatchVariantStatus.None },
                South = new AutoEyePatchVariantResult { Slot = AutoEyePatchFacingSlot.South, Status = AutoEyePatchVariantStatus.Skipped, Reasons = eligibility.Reasons },
                East = new AutoEyePatchVariantResult { Slot = AutoEyePatchFacingSlot.East, Status = AutoEyePatchVariantStatus.Skipped, Reasons = eligibility.Reasons },
                West = new AutoEyePatchVariantResult { Slot = AutoEyePatchFacingSlot.West, Status = AutoEyePatchVariantStatus.Skipped, Reasons = eligibility.Reasons },
            };
            _headResultsByKey[headKey] = headResult;
            _generationAttemptedThisLoad = true;
            diagnostics?.RecordResult(headResult);
            return;
        }

        string southKey = GetTextureKey(eligibility.GraphicPath, "south", eligibility.SouthTexture);
        string eastKey = GetTextureKey(eligibility.GraphicPath, "east", eligibility.EastTexture);

        TryGetOrAnalyzeTexture(southKey, eligibility.GraphicPath, eligibility.SouthTexture, false, out AutoEyePatchTextureAnalysis southAnalysis);
        TryGetOrAnalyzeTexture(eastKey, eligibility.GraphicPath, eligibility.EastTexture, true, out AutoEyePatchTextureAnalysis eastAnalysis);

        try
        {
            headResult = AutoEyePatchGenerator.Generate(headType, eligibility, southAnalysis, eastAnalysis);
        }
        catch (System.Exception ex)
        {
            Log.Error($"[Despicable] AutoEyePatch generation failed for head '{headKey}': {ex}");
            headResult = new AutoEyePatchHeadResult
            {
                HeadKey = headKey,
                GraphicPath = eligibility.GraphicPath,
                Status = AutoEyePatchHeadStatus.Failed,
                SummaryReasons = AutoEyePatchSkipReason.GenerationError,
                North = new AutoEyePatchVariantResult { Slot = AutoEyePatchFacingSlot.North, Status = AutoEyePatchVariantStatus.None },
                South = new AutoEyePatchVariantResult { Slot = AutoEyePatchFacingSlot.South, Status = AutoEyePatchVariantStatus.Skipped, Reasons = AutoEyePatchSkipReason.GenerationError },
                East = new AutoEyePatchVariantResult { Slot = AutoEyePatchFacingSlot.East, Status = AutoEyePatchVariantStatus.Skipped, Reasons = AutoEyePatchSkipReason.GenerationError },
                West = new AutoEyePatchVariantResult { Slot = AutoEyePatchFacingSlot.West, Status = AutoEyePatchVariantStatus.Skipped, Reasons = AutoEyePatchSkipReason.GenerationError },
            };
        }

        _headResultsByKey[headKey] = headResult;
        _generatedThisLoad = true;
        _generationAttemptedThisLoad = true;
        PrewarmMirroredRuntimeTextures(headResult);
        diagnostics?.RecordResult(headResult);
    }



    public static void RequestHeadGeneration(HeadTypeDef headType, Pawn pawn = null)
    {
        if (headType == null || !IsGenerationEnabled())
            return;

        string headKey = GetHeadKey(headType);
        if (TryGetHeadResult(headType, out _))
            return;

        if (pawn != null)
        {
            if (!_waitingPawnIdsByHeadKey.TryGetValue(headKey, out HashSet<int> waitingPawnIds))
            {
                waitingPawnIds = new HashSet<int>();
                _waitingPawnIdsByHeadKey[headKey] = waitingPawnIds;
            }

            waitingPawnIds.Add(pawn.thingIDNumber);
        }

        if (_queuedHeadGenerationKeys.Add(headKey))
        {
            _queuedHeadTypesByKey[headKey] = headType;
            _pendingHeadGenerationKeys.Enqueue(headKey);
        }
    }

    public static void ProcessQueuedGenerationBudget(int maxHeadsPerTick = 1)
    {
        if (maxHeadsPerTick <= 0 || !IsGenerationEnabled())
            return;

        int processed = 0;
        while (processed < maxHeadsPerTick && _pendingHeadGenerationKeys.Count > 0)
        {
            string headKey = _pendingHeadGenerationKeys.Dequeue();
            _queuedHeadGenerationKeys.Remove(headKey);

            if (!_queuedHeadTypesByKey.TryGetValue(headKey, out HeadTypeDef headType) || headType == null)
            {
                _queuedHeadTypesByKey.Remove(headKey);
                _waitingPawnIdsByHeadKey.Remove(headKey);
                continue;
            }

            _queuedHeadTypesByKey.Remove(headKey);
            if (!TryGetHeadResult(headType, out _))
            {
                GenerateForHead(headType, diagnostics: null);
            }

            NotifyWaitingPawnsForHead(headKey);
            processed++;
        }
    }

    private static void EnsureGeneratedForHeadImmediate(HeadTypeDef headType)
    {
        if (headType == null || !IsGenerationEnabled())
            return;

        if (TryGetHeadResult(headType, out _))
            return;

        string headKey = GetHeadKey(headType);
        _queuedHeadGenerationKeys.Remove(headKey);
        _queuedHeadTypesByKey.Remove(headKey);
        GenerateForHead(headType, diagnostics: null);
        NotifyWaitingPawnsForHead(headKey);
    }

    private static bool ShouldGenerateImmediatelyNow()
    {
        return WorkshopRenderContext.Active || FacePartsPortraitRenderContext.NonWorkshopPortraitActive;
    }

    private static void NotifyWaitingPawnsForHead(string headKey)
    {
        if (headKey.NullOrEmpty())
            return;

        if (!_waitingPawnIdsByHeadKey.TryGetValue(headKey, out HashSet<int> waitingPawnIds) || waitingPawnIds == null || waitingPawnIds.Count == 0)
        {
            _waitingPawnIdsByHeadKey.Remove(headKey);
            return;
        }

        foreach (Pawn pawn in PawnsFinder.AllMapsAndWorld_Alive)
        {
            if (pawn == null || !waitingPawnIds.Contains(pawn.thingIDNumber))
                continue;

            QueuePendingFaceRefresh(pawn);
        }

        _waitingPawnIdsByHeadKey.Remove(headKey);
    }

    private static void PrewarmMirroredRuntimeTextures(AutoEyePatchHeadResult result)
    {
        if (result?.South == null || result.South.Status != AutoEyePatchVariantStatus.Generated)
            return;

        Texture2D source = result.South.Secondary?.RuntimeTexture ?? result.South.Primary?.RuntimeTexture;
        if (source != null)
            _ = GetOrCreateFlipped(source);
    }

    private static void DestroyGeneratedRuntimeTextures()
    {
        HashSet<Texture2D> destroyed = new();

        foreach (AutoEyePatchHeadResult result in _headResultsByKey.Values)
        {
            DestroyVariantRuntimeTextures(result?.South, destroyed);
            DestroyVariantRuntimeTextures(result?.East, destroyed);
        }

        foreach (Texture2D flipped in _flippedRuntimeTextures.Values)
            DestroyRuntimeTexture(flipped, destroyed);
    }

    private static void DestroyVariantRuntimeTextures(AutoEyePatchVariantResult variant, HashSet<Texture2D> destroyed)
    {
        DestroyDescriptorRuntimeTexture(variant?.Primary, destroyed);
        DestroyDescriptorRuntimeTexture(variant?.Secondary, destroyed);
    }

    private static void DestroyDescriptorRuntimeTexture(AutoEyePatchDescriptor descriptor, HashSet<Texture2D> destroyed)
    {
        DestroyRuntimeTexture(descriptor?.RuntimeTexture, destroyed);
    }

    private static void DestroyRuntimeTexture(Texture2D texture, HashSet<Texture2D> destroyed)
    {
        if (texture == null || !destroyed.Add(texture))
            return;

        AutoEyePatchAnalyzer.InvalidateCachedPixels(texture);
        UnityEngine.Object.Destroy(texture);
    }

    private static string GetHeadKey(HeadTypeDef headType) => headType?.defName ?? string.Empty;

    private static string GetTextureKey(string texturePath, string slot, Texture2D texture)
    {
        if (!texturePath.NullOrEmpty())
            return texturePath + "|" + slot;
        if (texture != null)
            return texture.name + "|" + slot;
        return slot;
    }

    private static bool TryGetOrAnalyzeTexture(string textureKey, string texturePath, Texture2D texture, bool sideMode, out AutoEyePatchTextureAnalysis analysis)
    {
        if (_textureAnalysisByKey.TryGetValue(textureKey, out analysis))
            return true;

        analysis = AutoEyePatchAnalyzer.Analyze(textureKey, texturePath, texture, sideMode);
        _textureAnalysisByKey[textureKey] = analysis;
        return analysis != null;
    }

    private static void InvalidateExistingFaceGraphics()
    {
        if (_lastSummary == null)
            return;

        if (_lastSummary.HeadsGenerated <= 0 && _lastSummary.HeadsPartial <= 0)
            return;

        foreach (Pawn pawn in PawnsFinder.AllMapsAndWorld_Alive)
        {
            if (pawn?.RaceProps?.Humanlike != true)
                continue;

            QueuePendingFaceRefresh(pawn);
        }
    }

    private static bool IsGenerationEnabled()
    {
        Settings settings = ModMain.Instance?.settings;
        return settings != null && settings.facialPartsExtensionEnabled && settings.experimentalAutoEyePatchEnabled && !ModMain.IsNlFacialInstalled;
    }
}
