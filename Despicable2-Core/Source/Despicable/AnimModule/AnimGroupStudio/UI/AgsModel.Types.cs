using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Despicable.AnimModule.AnimGroupStudio.Model;
// Guardrail-Reason: AGS model graph types stay adjacent for discoverability and serialization consistency.
public static partial class AgsModel
{
    public sealed class RoleSpec : IExposable
    {
        // v2+ stable identity
        public string roleKey;
        public string displayName;
        public RoleGenderReq genderReq = RoleGenderReq.Male;

        // Legacy (v1) fields retained for back-compat.
        public int roleId;
        public Gender gender;

        // For visual distinction by default.
        public string defaultDummyBodyTypeDefName;

        // UI state: currently selected dummy body type.
        public string previewDummyBodyTypeDefName;

        public void ExposeData()
        {
            Scribe_Values.Look(ref roleKey, "roleKey");
            Scribe_Values.Look(ref displayName, "displayName");
            Scribe_Values.Look(ref genderReq, "genderReq", RoleGenderReq.Male);

            Scribe_Values.Look(ref roleId, "roleId");
            Scribe_Values.Look(ref gender, "gender");
            Scribe_Values.Look(ref defaultDummyBodyTypeDefName, "defaultDummyBodyTypeDefName");
            Scribe_Values.Look(ref previewDummyBodyTypeDefName, "previewDummyBodyTypeDefName");
        }

        public static RoleSpec MakeDefault(string roleKey, string displayName, RoleGenderReq req)
        {
            return new RoleSpec
            {
                roleKey = roleKey,
                displayName = displayName,
                genderReq = req,
                roleId = req == RoleGenderReq.Female ? (int)RoleId.Female : (int)RoleId.Male,
                gender = req == RoleGenderReq.Female ? Gender.Female : Gender.Male,
                defaultDummyBodyTypeDefName = req == RoleGenderReq.Female ? "Female" : "Male",
                previewDummyBodyTypeDefName = req == RoleGenderReq.Female ? "Female" : "Male"
            };
        }
    }

    public sealed class ExportSpec : IExposable
    {
        public string baseDefName;

        public void ExposeData()
        {
            Scribe_Values.Look(ref baseDefName, "baseDefName");
        }
    }

    public sealed class StageSpec : IExposable
    {
        public int stageIndex;
        public string label;
        public int durationTicks = 60;
        /// <summary>
        /// How many times this stage repeats when used as a queue entry (drives AnimGroupDef.loopIndex).
        /// 1 = play once.
        /// Large values are treated as effectively infinite for preview/playback.
        /// </summary>
        public int repeatCount = 1;

        // Back-compat: older projects used a bool "loop". If present, treat it as "infinite".
        // (We keep this field so Scribe can still load it.)
        public bool loop;

        /// <summary>
        /// Optional content tags written into the exported AnimGroupDef.stageTags alongside the
        /// project key. Useful for filtering (e.g. "lovin_oral", "lovin_anal").
        /// </summary>
        public List<string> stageTags;

        public List<StageVariant> variants = new();

        public void ExposeData()
        {
            Scribe_Values.Look(ref stageIndex, "stageIndex");
            Scribe_Values.Look(ref label, "label");
            Scribe_Values.Look(ref durationTicks, "durationTicks", 60);
            Scribe_Values.Look(ref repeatCount, "repeatCount", 1);
            Scribe_Values.Look(ref loop, "loop", false);
            Scribe_Collections.Look(ref stageTags, "stageTags", LookMode.Value);
            Scribe_Collections.Look(ref variants, "variants", LookMode.Deep);

            if (repeatCount < 1) repeatCount = 1;
            // If loading an older save that used `loop=true` and repeatCount is still default,
            // interpret it as an effectively-infinite repeat.
            if (loop && repeatCount == 1)
                repeatCount = 999999;
        }
    }

    public sealed class StageVariant : IExposable
    {
        public string variantId = "Base";
        public List<RoleClip> clips = new();

        // Legacy v1
        public ClipSpec male;
        public ClipSpec female;

        public void ExposeData()
        {
            Scribe_Values.Look(ref variantId, "variantId");
            Scribe_Collections.Look(ref clips, "clips", LookMode.Deep);

            // Legacy
            Scribe_Deep.Look(ref male, "male");
            Scribe_Deep.Look(ref female, "female");

            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                if (clips == null) clips = new List<RoleClip>();
                if (clips.Count == 0 && (male != null || female != null))
                {
                    if (male != null) clips.Add(new RoleClip { roleKey = "male_1", clip = male });
                    if (female != null) clips.Add(new RoleClip { roleKey = "female_1", clip = female });
                }
            }
        }

        public ClipSpec GetClip(string roleKey)
        {
            if (clips.NullOrEmpty() || roleKey.NullOrEmpty()) return null;
            for (int i = 0; i < clips.Count; i++)
            {
                var c = clips[i];
                if (c != null && c.roleKey == roleKey) return c.clip;
            }
            return null;
        }

        public ClipSpec EnsureClip(string roleKey)
        {
            if (roleKey.NullOrEmpty()) return null;
            if (clips == null) clips = new List<RoleClip>();
            for (int i = 0; i < clips.Count; i++)
            {
                var c = clips[i];
                if (c != null && c.roleKey == roleKey)
                {
                    if (c.clip == null) c.clip = new ClipSpec();
                    return c.clip;
                }
            }
            var rc = new RoleClip { roleKey = roleKey, clip = new ClipSpec() };
            clips.Add(rc);
            return rc.clip;
        }
    }

    public sealed class RoleClip : IExposable
    {
        public string roleKey;
        public ClipSpec clip;

        public void ExposeData()
        {
            Scribe_Values.Look(ref roleKey, "roleKey");
            Scribe_Deep.Look(ref clip, "clip");
        }
    }

    public sealed class RoleOffsetsBundle : IExposable
    {
        public string roleKey;
        public List<BodyTypeOffsetPair> offsets;

        public void ExposeData()
        {
            Scribe_Values.Look(ref roleKey, "roleKey");
            Scribe_Collections.Look(ref offsets, "offsets", LookMode.Deep);
        }
    }

    public sealed class ClipSpec : IExposable
    {
        public int lengthTicks = 60;
        public List<Track> tracks = new();

        public void ExposeData()
        {
            Scribe_Values.Look(ref lengthTicks, "lengthTicks", 60);
            Scribe_Collections.Look(ref tracks, "tracks", LookMode.Deep);
        }
    }

    public sealed class Track : IExposable
    {
        public string nodeTag;
        public List<Keyframe> keys = new();

        public void ExposeData()
        {
            Scribe_Values.Look(ref nodeTag, "nodeTag");
            Scribe_Collections.Look(ref keys, "keys", LookMode.Deep);
        }
    }

    public sealed class Keyframe : IExposable
    {
        public int tick;
        // Mirrors WorkshopExtKeyframe / ExtendedKeyframe shape.
        public float angle = 0f;
        public Vector3 offset = Vector3.zero;
        // Optional: prop-node scaling. Defaults to 1,1,1.
        // (We keep this in the model even if the UI hides it most of the time.)
        public Vector3 scale = Vector3.one;
        public Rot4 rotation = Rot4.South;
        public bool visible = true;

        // Optional extended fields (not required by the simple Author UI).
        public string graphicState;
        public int variant = -1;
        public string soundDefName;
        public string facialAnimDefName;
        public int layerBias = 0;

        // Optional prop reference (kept for future, hidden from simple UI).
        public PropRef prop;

        public void ExposeData()
        {
            Scribe_Values.Look(ref tick, "tick");
            Scribe_Values.Look(ref angle, "angle", 0f);
            Scribe_Values.Look(ref offset, "offset", Vector3.zero);
            Scribe_Values.Look(ref scale, "scale", Vector3.one);
            Scribe_Values.Look(ref rotation, "rotation", Rot4.South);
            Scribe_Values.Look(ref visible, "visible", true);

            Scribe_Values.Look(ref graphicState, "graphicState", null);
            Scribe_Values.Look(ref variant, "variant", -1);
            Scribe_Values.Look(ref soundDefName, "soundDefName", null);
            Scribe_Values.Look(ref facialAnimDefName, "facialAnimDefName", null);
            Scribe_Values.Look(ref layerBias, "layerBias", 0);
            Scribe_Deep.Look(ref prop, "prop");
        }
    }

    public sealed class PropRef : IExposable
    {
        public string propDefName;
        public bool propVisible;
        public Vector2 propOffset;
        public float propAngle;
        public float propScale = 1f;

        public void ExposeData()
        {
            Scribe_Values.Look(ref propDefName, "propDefName");
            Scribe_Values.Look(ref propVisible, "propVisible", false);
            Scribe_Values.Look(ref propOffset, "propOffset");
            Scribe_Values.Look(ref propAngle, "propAngle");
            Scribe_Values.Look(ref propScale, "propScale", 1f);
        }
    }

    public sealed class BodyTypeOffset : IExposable
    {
        public Vector2 rootOffset;
        public int rotation;
        public Vector3 scale = Vector3.one;

        public void ExposeData()
        {
            Scribe_Values.Look(ref rootOffset, "rootOffset");
            Scribe_Values.Look(ref rotation, "rotation");
            Scribe_Values.Look(ref scale, "scale", Vector3.one);
        }
    }

    /// <summary>Helper wrapper to serialize Dictionary&lt;string, BodyTypeOffset&gt; via Scribe.</summary>
    public sealed class BodyTypeOffsetPair : IExposable
    {
        public string bodyTypeDefName;
        public BodyTypeOffset value;

        public void ExposeData()
        {
            Scribe_Values.Look(ref bodyTypeDefName, "bodyTypeDefName");
            Scribe_Deep.Look(ref value, "value");
        }
    }

    public sealed class ExistingFamily
    {
        public string familyKey;
        /// <summary>
        /// Variations are whole AnimGroupDefs that share a base name.
        /// Example: Base "Foo" has variations "Foo_A", "Foo_B", "Foo_CA".
        /// </summary>
        public List<AnimGroupDef> variationsSorted = new();

        /// <summary>
        /// Selected variation in UI (index into variationsSorted).
        /// </summary>
        public int selectedVariationIndex;
    }

    /// <summary>
    /// Naming helpers: groups AnimGroupDefs into "families" based on your conventions.
    ///
    /// Convention:
    /// - "AGDName_A" => familyKey "AGDName", code "A"
    /// - "AGDName_AC" => familyKey "AGDName", code "AC"
    /// - If no trailing code, code = "" and familyKey = defName.
    ///
    /// This is purely editor UX: runtime doesn't care.
    /// </summary>
    public static class Name
    {
        public static void SplitFamilyAndCode(string defName, out string familyKey, out string code)
        {
            familyKey = defName ?? "";
            code = "";

            if (string.IsNullOrEmpty(defName))
                return;

            // Case 1: last underscore segment is ALL CAPS letters.
            int us = defName.LastIndexOf('_');
            if (us >= 0 && us < defName.Length - 1)
            {
                string tail = defName.Substring(us + 1);
                if (IsAllCapsLetters(tail))
                {
                    familyKey = defName.Substring(0, us);
                    code = tail;
                    return;
                }
            }

            // Case 2: trailing ALL CAPS letters (1..4) with a non-cap immediately before.
            int cut = TrailingCapsCut(defName);
            if (cut > 0 && cut < defName.Length)
            {
                familyKey = defName.Substring(0, cut);
                code = defName.Substring(cut);
            }
        }

        public static bool IsAllCapsLetters(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c < 'A' || c > 'Z') return false;
            }
            return true;
        }

        private static int TrailingCapsCut(string s)
        {
            if (string.IsNullOrEmpty(s)) return -1;

            // Count trailing caps letters.
            int i = s.Length - 1;
            int caps = 0;
            while (i >= 0 && s[i] >= 'A' && s[i] <= 'Z')
            {
                caps++;
                i--;
                if (caps > 4) break; // keep conservative
            }
            if (caps == 0 || caps > 4) return -1;
            if (i < 0) return -1;

            // Require the character before the caps run to NOT be a cap.
            if (s[i] >= 'A' && s[i] <= 'Z')
                return -1;

            return i + 1;
        }
    }
}
