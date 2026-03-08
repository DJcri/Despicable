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
    private static readonly System.Reflection.FieldInfo _kfScaleField =
        typeof(VerseKeyframe).GetField("scale", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

    private static readonly System.Reflection.PropertyInfo _kfScaleProp =
        typeof(VerseKeyframe).GetProperty("scale", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

    private static Vector3 ReadKeyframeScale(VerseKeyframe kf)
    {
        if (kf == null) return Vector3.one;
        try
        {
            if (_kfScaleField != null)
            {
                object v = _kfScaleField.GetValue(kf);
                // If the underlying member type is Nullable<Vector3>, boxing a non-null value yields
                // a boxed Vector3 (not a boxed Nullable<Vector3>). So checking `is Vector3` is enough.
                if (v is Vector3) return (Vector3)v;
            }

            if (_kfScaleProp != null)
            {
                object v = _kfScaleProp.GetValue(kf, null);
                if (v is Vector3) return (Vector3)v;
            }
        }
        catch (System.Exception e) { Despicable.Core.DebugLogger.WarnExceptionOnce("WorkshopAnimationImporter.EmptyCatch:1", "Workshop animation import fallback path failed.", e); }
        return Vector3.one;
    }

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
                                t.keyframes.Add(new WorkshopExtKeyframe
                                {
                                    tick = ek.tick,
                                    angle = ek.angle,
                                    offset = ek.offset,
                                    rotation = ek.rotation,
                                    scale = ReadKeyframeScale(ek),
                                    visible = ek.visible,
                                    graphicState = ek.graphicState,
                                    variant = ek.variant ?? -1,
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
                                    scale = ReadKeyframeScale(kf),
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
