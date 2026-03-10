using RimWorld;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;
using Verse;
using Despicable.AnimGroupStudio;
using Despicable.AnimModule.AnimGroupStudio.Model;
using Despicable.AnimModule.AnimGroupStudio.Export;

namespace Despicable.AnimModule.AnimGroupStudio.UI;
public partial class Dialog_AnimGroupStudio
{
            private void SelectFamily(string familyKey)
            {
                selectedFamilyKey = familyKey;
                selectedStageIndex = 0;
    
                if (families != null && families.TryGetValue(familyKey, out var fam) && !fam.variationsSorted.NullOrEmpty())
                {
                    // Default to first variation.
                    fam.selectedVariationIndex = Mathf.Clamp(fam.selectedVariationIndex, 0, fam.variationsSorted.Count - 1);
                    SelectGroup(fam.variationsSorted[fam.selectedVariationIndex]);
                }
                else
                {
                    SelectGroup(null);
                }
            }
    private void SelectGroup(AnimGroupDef group)
            {
                selectedGroup = group;
                selectedStageIndex = 0;
                preview.ConfigureFor(group);
                preview.SelectedStageIndex = 0;
                if (group != null && preview.StageCount > 0)
                    preview.ShowSelectedStageAtTick(0);
            }
    private void RebuildFamilies()
            {
                families = new Dictionary<string, AgsModel.ExistingFamily>();
    
                foreach (var d in DefDatabase<AnimGroupDef>.AllDefsListForReading)
                {
                    if (d == null || d.defName.NullOrEmpty()) continue;
                    AgsModel.Name.SplitFamilyAndCode(d.defName, out var familyKey, out _);
                    if (familyKey.NullOrEmpty()) familyKey = d.defName;
    
                    if (!families.TryGetValue(familyKey, out var fam))
                    {
                        fam = new AgsModel.ExistingFamily { familyKey = familyKey };
                        families[familyKey] = fam;
                    }
                    fam.variationsSorted.Add(d);
                }
    
                // Natural-sort families and their variations.
                var cmp = new NaturalComparer();
                familyKeysSorted = families.Keys.OrderBy(k => k, cmp).ToList();
                foreach (var kv in families)
                {
                    kv.Value.variationsSorted = kv.Value.variationsSorted
                        .Where(v => v != null)
                        .OrderBy(v => v.defName, cmp)
                        .ToList();
                }
    
                // Default selection: first family, first variation.
                if (selectedFamilyKey.NullOrEmpty() && familyKeysSorted.Count > 0)
                {
                    SelectFamily(familyKeysSorted[0]);
                }
            }
}
