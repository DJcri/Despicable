using System.Collections.Generic;
using Verse;

namespace Despicable.Core.Staging;
/// <summary>
/// A staged interaction clip that can be planned (slot assignment) and played (via a backend).
///
/// v1 fields (stageTags/anchorMode/slots/backendKey) are preserved for backward compatibility.
/// v2 adds an explicit stage timeline (stages + per-slot tracks) and optional variants.
/// </summary>
public class StageClipDef : Def
{
    // Opaque keys used for filtering (ex: "lovin_vaginal", "dance_slow").
    public List<string> stageTags;

    // Planner-time anchor filter only (bed vs standing).
    public StageAnchorMode anchorMode = StageAnchorMode.Any;

    // Slot definitions used for assignment.
    public List<StageSlotDef> slots;

    /// <summary>
    /// Backend payload key.
    ///
    /// Legacy v1: interpreted directly by the backend (e.g. RAF AnimGroupDef defName).
    /// v2: typically left blank; the default is this defName.
    /// </summary>
    public string backendKey;

    /// <summary>
    /// Optional playback backend selector.
    /// If set, StagePlanner will copy it onto StagePlan.PlaybackBackendKey.
    /// If unset, backends may infer playback from the payload.
    /// </summary>
    public string playbackBackendKey;

    /// <summary>
    /// v2 timeline stages. If present and non-empty, StageClipTimelineBackend can play this clip.
    /// </summary>
    public List<StageTimelineStageDef> stages;

    /// <summary>
    /// Optional variants that override per-stage track assignments.
    /// </summary>
    public List<StageVariantDef> variants;
}
