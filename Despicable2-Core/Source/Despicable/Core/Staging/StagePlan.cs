using System.Collections.Generic;
using Verse;

namespace Despicable.Core.Staging;
/// <summary>
/// Output of StagePlanner. Opaque to gameplay systems; interpreted by playback backends.
/// </summary>
public sealed class StagePlan
{
    /// <summary>
    /// Optional: the stage tag that requested this plan (ex: "LovinVaginal", "dance_slow").
    /// </summary>
    public string StageTag;

    /// <summary>
    /// Backend payload key.
    /// - For RAF backend: AnimGroupDef.defName
    /// - For StageClip timeline: StageClipDef.defName
    /// </summary>
    public string BackendKey;

    /// <summary>
    /// Optional playback backend selector (ex: "stageClipTimeline", "rafAnimGroup").
    /// If set, each backend will only accept plans whose key matches its expected id.
    /// </summary>
    public string PlaybackBackendKey;

    /// <summary>
    /// Anchor to stage around (bed, pawn, cell, etc).
    /// </summary>
    public LocalTargetInfo Anchor = LocalTargetInfo.Invalid;

    /// <summary>
    /// SlotId -> Pawn assignment.
    /// Slot ids are defined by StageSlotDef.slotId (or legacy role defName).
    /// </summary>
    public readonly Dictionary<string, Pawn> SlotAssignments = new();

    public override string ToString()
        => $"StagePlan(tag={StageTag ?? "null"}, backendKey={BackendKey ?? "null"}, backend={PlaybackBackendKey ?? "infer"}, slots={SlotAssignments.Count})";
}
