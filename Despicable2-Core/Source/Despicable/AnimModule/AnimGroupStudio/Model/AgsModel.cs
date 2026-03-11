using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Despicable.AnimModule.AnimGroupStudio.Model;
/// <summary>
/// Editor-owned models for Anim Group Studio.
///
/// Core intent:
/// - Author AnimationGroupDefs (AnimGroupDef) using dummy pawns.
/// - Stages are chronological groupings ("stage" is time, not a location).
/// - "Variation" is authored and previewed as a WHOLE AnimGroupDef.
///   (This matches your existing data: e.g. AGDName_A, AGDName_CA, etc.)
/// - Stage selection is index-based into AnimRoleDef.anims[] (chronological ordering).
/// - Props are group-scoped membership: if a prop is referenced anywhere in the group,
///   it is considered part of the group and simply toggles visibility via keyframes.
/// - Body type offsets are keyed by BodyTypeDef (hulk/fat/thin/etc.).
///   Offsets are stored per role (AnimRoleDef.offsetDef) but span the whole group.
/// </summary>
public static partial class AgsModel
{
    public enum RoleId
    {
        Male = 0,
        Female = 1
    }

    /// <summary>
    /// Authoring-time gender requirement used to generate preview dummies and (optionally)
    /// validate pawn assignments.
    /// </summary>
    public enum RoleGenderReq
    {
        Male = 0,
        Female = 1,
        Unisex = 2
    }

    public sealed class EditorState
    {
        public bool authorMode = true;

        public List<Project> projects = new();
        public Project project;

        // Author selection
        public int selectedStageIndex = 0;
        public string selectedVariantId = "Base";
        public string selectedRoleKey = "male_1";

        // Preview-existing selection
        public string selectedFamilyKey;
        public ExistingFamily existingFamily;
    }

    public sealed partial class Project : IExposable
    {
        public string projectId;
        public string label;

        // Author-defined roles (v2+). Stable role identity is roleKey.
        public List<RoleSpec> roles = new();

        // Chronological stages.
        public List<StageSpec> stages = new();

        // Group-scoped prop membership (union of any prop referenced anywhere).
        public HashSet<string> propLibrary = new();

        // Group-scoped tags written to the exported AnimGroupDef.stageTags.
        // Legacy projects may still carry per-stage stageTags; those are migrated into this list on load.
        public List<string> groupTags = new();

        // Offsets are per role (AnimRoleDef.offsetDef), keyed by body type.
        public Dictionary<string, Dictionary<string, BodyTypeOffset>> offsetsByRoleKey = new();

        public ExportSpec export = new();

        public void ExposeData()
        {
            Scribe_Values.Look(ref projectId, "projectId");
            Scribe_Values.Look(ref label, "label");

            Scribe_Collections.Look(ref stages, "stages", LookMode.Deep);

            // Roles
            Scribe_Collections.Look(ref roles, "roles", LookMode.Deep);

            // Props
            var propList = propLibrary != null ? new List<string>(propLibrary) : new List<string>();
            Scribe_Collections.Look(ref propList, "propLibrary", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
                propLibrary = propList != null ? new HashSet<string>(propList) : new HashSet<string>();

            Scribe_Collections.Look(ref groupTags, "groupTags", LookMode.Value);

            // Offsets per role (v2+)
            var bundles = new List<RoleOffsetsBundle>();
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                if (offsetsByRoleKey != null)
                {
                    foreach (var kv in offsetsByRoleKey)
                    {
                        bundles.Add(new RoleOffsetsBundle
                        {
                            roleKey = kv.Key,
                            offsets = ToPairs(kv.Value)
                        });
                    }
                }
            }
            Scribe_Collections.Look(ref bundles, "offsetBundles", LookMode.Deep);

            // Back-compat offsets (v1)
            var malePairs = new List<BodyTypeOffsetPair>();
            var femalePairs = new List<BodyTypeOffsetPair>();
            Scribe_Collections.Look(ref malePairs, "offsetsMale", LookMode.Deep);
            Scribe_Collections.Look(ref femalePairs, "offsetsFemale", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                offsetsByRoleKey = new Dictionary<string, Dictionary<string, BodyTypeOffset>>();

                // Prefer v2 bundles if present.
                if (!bundles.NullOrEmpty())
                {
                    for (int i = 0; i < bundles.Count; i++)
                    {
                        var b = bundles[i];
                        if (b == null || b.roleKey.NullOrEmpty()) continue;
                        offsetsByRoleKey[b.roleKey] = FromPairs(b.offsets);
                    }
                }
                else
                {
                    // Fallback: map legacy male/female offsets to the first matching roles.
                    var maleKey = FindFirstRoleKey(RoleGenderReq.Male);
                    var femaleKey = FindFirstRoleKey(RoleGenderReq.Female);
                    if (!maleKey.NullOrEmpty()) offsetsByRoleKey[maleKey] = FromPairs(malePairs);
                    if (!femaleKey.NullOrEmpty()) offsetsByRoleKey[femaleKey] = FromPairs(femalePairs);
                }

                NormalizeRoles();
                NormalizeMetadata();
            }

            Scribe_Deep.Look(ref export, "export");

            if (Scribe.mode == LoadSaveMode.Saving)
            {
                // Ensure stable data when saving.
                NormalizeRoles();
                NormalizeMetadata();
            }
        }

    }

}
