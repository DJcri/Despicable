using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using VerseKeyframe = Verse.Keyframe;

namespace Despicable;
/// <summary>
/// Best-effort conversion from a runtime RimWorld AnimationDef into the editor-facing WorkshopAnimation format.
///
/// This is intentionally conservative:
/// - Only imports keyframe parts that exist in AnimationDef.keyframeParts.
/// - Reads ExtendedKeyframe fields when available; falls back to Verse.Keyframe basics.
///
/// This enables "import existing animation group" to actually play something in the workshop.
/// </summary>
public static class WorkshopAnimationImporter
{

    public static WorkshopAnimation FromAnimationDef(AnimationDef def, string nameOverride = null)
    {
        if (def == null) return null;

        var w = new WorkshopAnimation
        {
            name = nameOverride ?? def.label ?? def.defName ?? "Imported Animation",
            durationTicks = Mathf.Max(1, def.durationTicks),
            tracks = new List<WorkshopTrack>()
        };

        try
        {
            var parts = def.keyframeParts;
            if (parts != null)
            {
                foreach (var kv in parts)
                {
                    var tag = kv.Key;
                    var part = kv.Value;
                    if (tag == null || part == null) continue;

                    var t = new WorkshopTrack
                    {
                        tagDefName = tag.defName,
                        keyframes = new List<WorkshopExtKeyframe>()
                    };

                    if (part.keyframes != null)
                    {
                        foreach (var kf in part.keyframes)
                        {
                            if (kf == null) continue;

                            // ExtendedKeyframe carries the fields the Workshop cares about.
                            if (kf is ExtendedKeyframe ek)
                            {
                                int legacyVariant = ek.variant ?? -1;
                                t.keyframes.Add(new WorkshopExtKeyframe
                                {
                                    tick = ek.tick,
                                    angle = ek.angle,
                                    offset = ek.offset,
                                    rotation = ek.rotation,
                                    scale = VerseKeyframeCompat.GetScaleOrDefault(ek),
                                    visible = ek.visible,
                                    graphicState = !ek.graphicState.NullOrEmpty() ? ek.graphicState : (legacyVariant >= 0 ? "variant_" + legacyVariant : null),
                                    variant = -1,
                                    soundDefName = ek.sound?.defName,
                                    facialAnimDefName = ek.facialAnim?.defName,
                                    layerBias = ek.layerBias
                                });
                            }
                            else
                            {
                                // Fallback: Verse.Keyframe guarantees tick/angle/offset only in some RW versions; default the rest.
                                t.keyframes.Add(new WorkshopExtKeyframe
                                {
                                    tick = kf.tick,
                                    angle = kf.angle,
                                    offset = kf.offset,
                                    rotation = Rot4.South,
                                    scale = VerseKeyframeCompat.GetScaleOrDefault(kf),
                                    visible = true
                                });
                            }
                        }
                    }

                    t.EnsureDefaults(w.durationTicks);
                    w.tracks.Add(t);
                }
            }
        }
        catch (Exception e)
        {
            Log.Warning($"[Despicable] WorkshopAnimationImporter failed to import AnimationDef '{def.defName}': {e}");
        }

        w.EnsureDefaults();
        w.SortAndClamp();
        return w;
    }
}
