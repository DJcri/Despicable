using System;
using System.Collections.Generic;
using Verse;

namespace Despicable.AnimModule.AnimGroupStudio.Model;
public static partial class AgsModel
{
    public sealed partial class Project
    {
        private void NormalizeRoles()
        {
            if (roles == null) roles = new List<RoleSpec>();

            // Create defaults if empty.
            if (roles.Count == 0)
            {
                roles.Add(RoleSpec.MakeDefault("male_1", "Male 1", RoleGenderReq.Male));
                roles.Add(RoleSpec.MakeDefault("female_1", "Female 1", RoleGenderReq.Female));
            }

            int maleN = 0, femaleN = 0, uniN = 0;
            var usedKeys = new HashSet<string>();
            for (int i = 0; i < roles.Count; i++)
            {
                var r = roles[i];
                if (r == null) continue;

                // Back-compat: infer genderReq if missing.
                if (!Enum.IsDefined(typeof(RoleGenderReq), r.genderReq))
                {
                    if (r.gender == Gender.Female || r.roleId == (int)RoleId.Female) r.genderReq = RoleGenderReq.Female;
                    else r.genderReq = RoleGenderReq.Male;
                }

                // Assign indices.
                if (r.genderReq == RoleGenderReq.Female) femaleN++;
                else if (r.genderReq == RoleGenderReq.Unisex) uniN++;
                else maleN++;

                string prefix = r.genderReq == RoleGenderReq.Female ? "female" : (r.genderReq == RoleGenderReq.Unisex ? "unisex" : "male");
                int idx = r.genderReq == RoleGenderReq.Female ? femaleN : (r.genderReq == RoleGenderReq.Unisex ? uniN : maleN);

                if (r.roleKey.NullOrEmpty())
                    r.roleKey = $"{prefix}_{idx}";
                if (r.displayName.NullOrEmpty())
                {
                    string dn = r.genderReq == RoleGenderReq.Female ? "Female" : (r.genderReq == RoleGenderReq.Unisex ? "Unisex" : "Male");
                    r.displayName = $"{dn} {idx}";
                }

                // Ensure unique roleKey.
                string baseKey = r.roleKey;
                if (baseKey.NullOrEmpty()) baseKey = $"{prefix}_{idx}";
                string key = baseKey;
                int bump = 2;
                while (usedKeys.Contains(key))
                {
                    key = baseKey + "_" + bump;
                    bump++;
                }
                r.roleKey = key;
                usedKeys.Add(key);

                // Default dummy body types.
                string bt = r.genderReq == RoleGenderReq.Female ? "Female" : "Male";
                if (r.defaultDummyBodyTypeDefName.NullOrEmpty()) r.defaultDummyBodyTypeDefName = bt;
                if (r.previewDummyBodyTypeDefName.NullOrEmpty()) r.previewDummyBodyTypeDefName = r.defaultDummyBodyTypeDefName;

                // Legacy fields kept stable.
                if (r.genderReq == RoleGenderReq.Female) { r.roleId = (int)RoleId.Female; r.gender = Gender.Female; }
                else { r.roleId = (int)RoleId.Male; r.gender = Gender.Male; }
            }

            // Ensure offsets dict has entries for all roles.
            if (offsetsByRoleKey == null) offsetsByRoleKey = new Dictionary<string, Dictionary<string, BodyTypeOffset>>();
            for (int i = 0; i < roles.Count; i++)
            {
                var r = roles[i];
                if (r?.roleKey.NullOrEmpty() != false) continue;
                if (!offsetsByRoleKey.ContainsKey(r.roleKey))
                    offsetsByRoleKey[r.roleKey] = new Dictionary<string, BodyTypeOffset>();
            }
        }

        private string FindFirstRoleKey(RoleGenderReq req)
        {
            if (roles == null) return null;
            for (int i = 0; i < roles.Count; i++)
            {
                var r = roles[i];
                if (r == null) continue;
                if (r.genderReq == req) return r.roleKey;
                // Legacy fallback
                if (req == RoleGenderReq.Female && (r.gender == Gender.Female || r.roleId == (int)RoleId.Female)) return r.roleKey;
                if (req == RoleGenderReq.Male && (r.gender == Gender.Male || r.roleId == (int)RoleId.Male)) return r.roleKey;
            }
            return null;
        }

        private static List<BodyTypeOffsetPair> ToPairs(Dictionary<string, BodyTypeOffset> dict)
        {
            var list = new List<BodyTypeOffsetPair>();
            if (dict == null) return list;
            foreach (var kv in dict)
                list.Add(new BodyTypeOffsetPair { bodyTypeDefName = kv.Key, value = kv.Value });
            return list;
        }

        private static Dictionary<string, BodyTypeOffset> FromPairs(List<BodyTypeOffsetPair> pairs)
        {
            var dict = new Dictionary<string, BodyTypeOffset>();
            if (pairs == null) return dict;
            for (int i = 0; i < pairs.Count; i++)
            {
                var p = pairs[i];
                if (p == null || p.bodyTypeDefName.NullOrEmpty() || p.value == null) continue;
                dict[p.bodyTypeDefName] = p.value;
            }
            return dict;
        }
    }
}
