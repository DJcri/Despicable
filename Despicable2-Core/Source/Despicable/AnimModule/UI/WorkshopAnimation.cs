using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Despicable
{
    /// <summary>
    /// In-game authoring format that mirrors the runtime ExtendedKeyframe system.
    ///
    /// This format is editor-facing and stable for disk persistence.
    /// Later steps compile/export this into real AnimationDefs.
    /// </summary>
    public class WorkshopAnimation : IExposable
    {
        public const int CurrentFormatVersion = 1;

        public int formatVersion = CurrentFormatVersion;
        public string name = "New Animation";
        public int durationTicks = 120;
        public List<WorkshopTrack> tracks = new();

        
        // --- Export metadata (used by the Workshop exporter UI) ---
        public string exportTargetGroupDefName;
        public string exportDefNameOverride;
        public string exportFolderOverride;
public void ExposeData()
        {
            Scribe_Values.Look(ref formatVersion, "formatVersion", CurrentFormatVersion);
            Scribe_Values.Look(ref name, "name", "New Animation");
            Scribe_Values.Look(ref durationTicks, "durationTicks", 120);
            Scribe_Collections.Look(ref tracks, "tracks", LookMode.Deep);
        
            Scribe_Values.Look(ref exportTargetGroupDefName, "exportTargetGroupDefName", null);
            Scribe_Values.Look(ref exportDefNameOverride, "exportDefNameOverride", null);
            Scribe_Values.Look(ref exportFolderOverride, "exportFolderOverride", null);
}

        public void EnsureDefaults()
        {
            if (durationTicks < 1) durationTicks = 1;
            if (tracks == null) tracks = new List<WorkshopTrack>();

            for (int i = 0; i < tracks.Count; i++)
                tracks[i]?.EnsureDefaults(durationTicks);
        }

        public void SortAndClamp()
        {
            EnsureDefaults();
            for (int i = 0; i < tracks.Count; i++)
                tracks[i]?.SortAndClamp(durationTicks);
        }

        public WorkshopAnimation CloneDeep()
        {
            var a = new WorkshopAnimation
            {
                formatVersion = formatVersion,
                name = name,
                durationTicks = durationTicks,
                exportTargetGroupDefName = exportTargetGroupDefName,
                exportDefNameOverride = exportDefNameOverride,
                exportFolderOverride = exportFolderOverride,
                tracks = new List<WorkshopTrack>()
            };

            if (!tracks.NullOrEmpty())
            {
                for (int i = 0; i < tracks.Count; i++)
                    a.tracks.Add(tracks[i]?.CloneDeep());
            }
            return a;
        }
    }

    /// <summary>
    /// A single animated channel, keyed by a PawnRenderNodeTagDef defName.
    /// We store the defName string for stability across loads and easier authoring.
    /// </summary>
    public class WorkshopTrack : IExposable
    {
        public const int CurrentFormatVersion = 1;

        public int formatVersion = CurrentFormatVersion;

        /// <summary>
        /// PawnRenderNodeTagDef defName (string). Resolved later via DefDatabase.
        /// </summary>
        public string tagDefName;

        public List<WorkshopExtKeyframe> keyframes = new();

        public void ExposeData()
        {
            Scribe_Values.Look(ref formatVersion, "formatVersion", CurrentFormatVersion);
            Scribe_Values.Look(ref tagDefName, "tagDefName", null);
            Scribe_Collections.Look(ref keyframes, "keyframes", LookMode.Deep);
        }

        public void EnsureDefaults(int durationTicks)
        {
            if (keyframes == null) keyframes = new List<WorkshopExtKeyframe>();

            // If a track exists but has no keyframes, initialize minimal endpoints.
            if (keyframes.Count == 0)
            {
                keyframes.Add(new WorkshopExtKeyframe { tick = 0, rotation = Rot4.South, visible = true });
                keyframes.Add(new WorkshopExtKeyframe { tick = durationTicks, rotation = Rot4.South, visible = true });
            }

            SortAndClamp(durationTicks);
        }

        public void SortAndClamp(int durationTicks)
        {
            if (keyframes == null) keyframes = new List<WorkshopExtKeyframe>();

            keyframes.Sort((a, b) => a.tick.CompareTo(b.tick));
            for (int i = 0; i < keyframes.Count; i++)
            {
                if (keyframes[i] == null) continue;
                keyframes[i].tick = Mathf.Clamp(keyframes[i].tick, 0, durationTicks);
            }
        }

        public WorkshopTrack CloneDeep()
        {
            var t = new WorkshopTrack
            {
                formatVersion = formatVersion,
                tagDefName = tagDefName,
                keyframes = new List<WorkshopExtKeyframe>()
            };
            if (!keyframes.NullOrEmpty())
            {
                for (int i = 0; i < keyframes.Count; i++)
                    t.keyframes.Add(keyframes[i]?.CloneDeep());
            }
            return t;
        }
    }

    /// <summary>
    /// Editor-side keyframe that maps closely to ExtendedKeyframe fields.
    /// Facial animation is intentionally included but not required/edited for now.
    /// </summary>
    public class WorkshopExtKeyframe : IExposable
    {
        public int tick;

        public float angle = 0f;
        public Vector3 offset = Vector3.zero;
        public Rot4 rotation = Rot4.South;

        // Prop nodes may keyframe non-1 scale (e.g. fluids). Defaults to Vector3.one when absent.
        public Vector3 scale = Vector3.one;

        public bool visible = true;

        /// <summary>
        /// Preferred variant selection method in the runtime.
        /// </summary>
        public string graphicState;

        /// <summary>
        /// Legacy numeric variant selection. -1 means unset.
        /// </summary>
        public int variant = -1;

        /// <summary>
        /// Optional: play a sound at this keyframe (stored as defName).
        /// </summary>
        public string soundDefName;

        /// <summary>
        /// Optional: facial anim (stored as defName). Editor can ignore this for now.
        /// </summary>
        public string facialAnimDefName;

        /// <summary>
        /// Optional: draw-order bias. Negative = push back, positive = pull forward.
        /// </summary>
        public int layerBias = 0;

        public void ExposeData()
        {
            Scribe_Values.Look(ref tick, "tick", 0);
            Scribe_Values.Look(ref angle, "angle", 0f);
            Scribe_Values.Look(ref offset, "offset", Vector3.zero);
            Scribe_Values.Look(ref rotation, "rotation", Rot4.South);
            Scribe_Values.Look(ref scale, "scale", Vector3.one);
            Scribe_Values.Look(ref visible, "visible", true);

            Scribe_Values.Look(ref graphicState, "graphicState", null);
            Scribe_Values.Look(ref variant, "variant", -1);
            Scribe_Values.Look(ref soundDefName, "soundDefName", null);
            Scribe_Values.Look(ref facialAnimDefName, "facialAnimDefName", null);
            Scribe_Values.Look(ref layerBias, "layerBias", 0);
        }


        public WorkshopExtKeyframe CloneDeep()
        {
            return new WorkshopExtKeyframe
            {
                tick = tick,
                angle = angle,
                offset = offset,
                rotation = rotation,
                scale = scale,
                visible = visible,
                graphicState = graphicState,
                variant = variant,
                soundDefName = soundDefName,
                facialAnimDefName = facialAnimDefName,
                layerBias = layerBias
            };
        }

        public bool HasVariant => variant >= 0;
    }
}
