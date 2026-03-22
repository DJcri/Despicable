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
// Guardrail-Reason: Import command flow is kept together until import mapping diverges from command orchestration.
public partial class Dialog_AnimGroupStudio
{
    private void ImportSelectedExistingAsProject()
            {
                if (selectedGroup == null)
                {
                    Messages.Message("No AnimGroupDef selected.", MessageTypeDefOf.RejectInput, false);
                    return;
                }
    
                try
                {
                    var group = selectedGroup;
    
                    var p = new AgsModel.Project
                    {
                        projectId = System.Guid.NewGuid().ToString("N"),
                        label = "",
                        groupTags = new List<string>(),
                        roles = new List<AgsModel.RoleSpec>(),
                        stages = new List<AgsModel.StageSpec>(),
                        propLibrary = new HashSet<string>(),
                        offsetsByRoleKey = new Dictionary<string, Dictionary<string, AgsModel.BodyTypeOffset>>(),
                        export = new AgsModel.ExportSpec()
                    };
    
                    AgsModel.Name.SplitFamilyAndCode(group.defName, out string importedBaseDef, out string importedVariationLabel);
                    if (importedBaseDef.NullOrEmpty()) importedBaseDef = group.defName;
                    if (importedBaseDef.NullOrEmpty()) importedBaseDef = "AGD_Player";
                    p.export.baseDefName = importedBaseDef.Replace(' ', '_');
                    p.label = importedVariationLabel ?? "";
                    if (!group.stageTags.NullOrEmpty())
                        p.groupTags = new List<string>(group.stageTags.Where(x => !x.NullOrEmpty()));
    
                    // Roles
                    int maleN = 0, femaleN = 0, uniN = 0;
                    var roleKeyByIndex = new List<string>();
    
                    if (!group.animRoles.NullOrEmpty())
                    {
                        for (int i = 0; i < group.animRoles.Count; i++)
                        {
                            var ar = group.animRoles[i];
                            if (ar == null) continue;
    
                            var req = MapGenderReq(ar.gender);
                            string key;
                            string label;
    
                            if (req == AgsModel.RoleGenderReq.Female) { femaleN++; key = $"female_{femaleN}"; label = $"Female {femaleN}"; }
                            else if (req == AgsModel.RoleGenderReq.Unisex) { uniN++; key = $"unisex_{uniN}"; label = $"Unisex {uniN}"; }
                            else { maleN++; key = $"male_{maleN}"; label = $"Male {maleN}"; }
    
                            // If the source role has a meaningful defName, keep it as a hint (but roleKey stays stable).
                            if (!ar.defName.NullOrEmpty())
                                label = ar.defName;
    
                            p.roles.Add(AgsModel.RoleSpec.MakeDefault(key, label, req));
                            roleKeyByIndex.Add(key);
    
                            // Import bodytype offsets if possible.
                            p.offsetsByRoleKey[key] = ExtractOffsets(ar.offsetDef, req);
                        }
                    }
    
                    if (p.roles.Count == 0)
                    {
                        // Fallback: at least one role so the project isn't empty.
                        p.roles.Add(AgsModel.RoleSpec.MakeDefault("male_1", "Male 1", AgsModel.RoleGenderReq.Male));
                        roleKeyByIndex.Add("male_1");
                        p.offsetsByRoleKey["male_1"] = new Dictionary<string, AgsModel.BodyTypeOffset>();
                    }
    
                    // Stage count (max across role anim lists; roles with fewer anims just import blank clips for missing stages)
                    int stageCount = 0;
                    if (!group.animRoles.NullOrEmpty())
                    {
                        for (int i = 0; i < group.animRoles.Count; i++)
                        {
                            var ar = group.animRoles[i];
                            if (ar?.anims == null) continue;
                            stageCount = Mathf.Max(stageCount, ar.anims.Count);
                        }
                    }
    
    // Build stages using durations + loopIndex repeat counts.
                    for (int s = 0; s < stageCount; s++)
                    {
                        int dur = 0;
                        for (int r = 0; r < group.animRoles.Count; r++)
                        {
                            var ar = group.animRoles[r];
                            var anim = (ar?.anims != null && s >= 0 && s < ar.anims.Count) ? ar.anims[s] : null;
                            if (anim == null) continue;
                            try { dur = Mathf.Max(dur, anim.durationTicks); } catch (Exception ex) { Despicable.Core.DebugLogger.WarnExceptionOnce("AgsCommands:2", "AgsCommands ignored a non-fatal editor exception.", ex); }
                        }
                        if (dur <= 0) dur = 60;
    
                        int rep = 1;
                        try
                        {
                            if (group.loopIndex != null && s >= 0 && s < group.loopIndex.Count)
                                rep = Mathf.Max(1, group.loopIndex[s]);
                        }
                        catch (Exception ex)
                        {
                            Despicable.Core.DebugLogger.WarnExceptionOnce(
                                "AgsCommands:6",
                                "AgsCommands failed to read the imported stage repeat count; falling back to 1.",
                                ex);
                            rep = 1;
                        }
    
                        var st = new AgsModel.StageSpec
                        {
                            stageIndex = s,
                            label = "Stage " + s,
                            durationTicks = dur,
                            repeatCount = rep
                        };
    
                        var variant = new AgsModel.StageVariant
                        {
                            variantId = "Base",
                            clips = new List<AgsModel.RoleClip>()
                        };
    
                        for (int r = 0; r < roleKeyByIndex.Count; r++)
                        {
                            string rk = roleKeyByIndex[r];
    
                            // Import keyframes/tracks when available.
                            AnimationDef srcAnim = null;
                            try
                            {
                                var ar = (group.animRoles != null && r >= 0 && r < group.animRoles.Count) ? group.animRoles[r] : null;
                                if (ar?.anims != null && s >= 0 && s < ar.anims.Count)
                                    srcAnim = ar.anims[s];
                            }
                            catch (Exception ex) { Despicable.Core.DebugLogger.WarnExceptionOnce("AgsCommands:3", "AgsCommands ignored a non-fatal editor exception.", ex); }
    
                            var clip = new AgsModel.ClipSpec { lengthTicks = dur, tracks = new List<AgsModel.Track>() };
    
                            if (srcAnim != null)
                            {
                                var w = Despicable.WorkshopAnimationImporter.FromAnimationDef(srcAnim);
                                if (w?.tracks != null)
                                {
                                    for (int ti = 0; ti < w.tracks.Count; ti++)
                                    {
                                        var wt = w.tracks[ti];
                                        if (wt == null) continue;
    
                                        var t = new AgsModel.Track { nodeTag = wt.tagDefName, keys = new List<AgsModel.Keyframe>() };
    
                                        if (wt.keyframes != null)
                                        {
                                            for (int ki = 0; ki < wt.keyframes.Count; ki++)
                                            {
                                                var wk = wt.keyframes[ki];
                                                if (wk == null) continue;
    
                                                string graphicState = !wk.graphicState.NullOrEmpty() ? wk.graphicState : (wk.variant >= 0 ? "variant_" + wk.variant : null);
                                                t.keys.Add(new AgsModel.Keyframe
                                                {
                                                    tick = wk.tick,
                                                    angle = wk.angle,
                                                    offset = wk.offset,
                                                    rotation = wk.rotation,
                                                    scale = wk.scale,
                                                    visible = wk.visible,
                                                    graphicState = graphicState,
                                                    variant = -1,
                                                    soundDefName = wk.soundDefName,
                                                    facialAnimDefName = wk.facialAnimDefName,
                                                    layerBias = Mathf.Clamp(wk.layerBias, -3, 3)
                                                });
                                            }
                                        }
    
                                        clip.tracks.Add(t);
                                    }
                                }
                            }
    
                            variant.clips.Add(new AgsModel.RoleClip { roleKey = rk, clip = clip });
                        }
    st.variants.Add(variant);
                        p.stages.Add(st);
                    }
    
                    if (p.stages.Count == 0)
                    {
                        // Keep a minimal authorable skeleton.
                        var st = new AgsModel.StageSpec { stageIndex = 0, label = "Stage 0", durationTicks = 60, repeatCount = 1 };
                        var variant = new AgsModel.StageVariant { variantId = "Base", clips = new List<AgsModel.RoleClip>() };
                        for (int r = 0; r < roleKeyByIndex.Count; r++)
                            variant.clips.Add(new AgsModel.RoleClip { roleKey = roleKeyByIndex[r], clip = new AgsModel.ClipSpec { lengthTicks = 60 } });
                        st.variants.Add(variant);
                        p.stages.Add(st);
                    }
    
                    // Persist + select
                    if (projects == null) projects = repo.LoadAll();
                    projects.Add(p);
                    repo.SaveAll(projects);
    
                    project = p;
                    authorStageIndex = 0;
                    authorTrackIndex = -1;
                    authorKeyIndex = -1;
                    StopAuthorPreview(resetTick: true);
    
                    // Switch to authoring
                    ActivateAuthorSourceMode();
    
                    EnsureRoles(project);
                    EnsureAuthorRoleKeyValid(project);
    
                    Messages.Message("Imported as a new project variation.", MessageTypeDefOf.PositiveEvent, false);
                }
                catch (Exception e)
                {
                    Log.Warning("[Despicable] ImportSelectedExistingAsProject failed: " + e);
                    Messages.Message("Import failed (see log).", MessageTypeDefOf.RejectInput, false);
                }
            }
    private static AgsModel.RoleGenderReq MapGenderReq(int gender)
            {
                // Def convention: 0=Any/Unisex, 1=Male, 2=Female.
                if (gender == 2) return AgsModel.RoleGenderReq.Female;
                if (gender == 1) return AgsModel.RoleGenderReq.Male;
                return AgsModel.RoleGenderReq.Unisex;
            }
    private static Dictionary<string, AgsModel.BodyTypeOffset> ExtractOffsets(AnimationOffsetDef offsetDef, AgsModel.RoleGenderReq req)
            {
                var dict = new Dictionary<string, AgsModel.BodyTypeOffset>();
                if (offsetDef?.offsets == null) return dict;
    
                try
                {
                    for (int i = 0; i < offsetDef.offsets.Count; i++)
                    {
                        var o = offsetDef.offsets[i];
                        if (o == null) continue;
    
                        if (o is AnimationOffset_BodyType bt && bt.offsets != null)
                        {
                            for (int j = 0; j < bt.offsets.Count; j++)
                            {
                                var b = bt.offsets[j];
                                if (b?.bodyType == null) continue;
                                dict[b.bodyType.defName] = new AgsModel.BodyTypeOffset
                                {
                                    rootOffset = new Vector2(b.offset.x, b.offset.z),
                                    rotation = b.rotation,
                                    scale = b.scale
                                };
                            }
                        }
                        else if (o is AnimationOffset_BodyTypeGendered bg)
                        {
                            IEnumerable<Despicable.BodyTypeOffset> src = null;
                            if (req == AgsModel.RoleGenderReq.Female) src = bg.offsetsFemale;
                            else if (req == AgsModel.RoleGenderReq.Male) src = bg.offsetsMale;
                            else
                            {
                                src = (bg.offsetsMale ?? new List<Despicable.BodyTypeOffset>()).Concat(bg.offsetsFemale ?? new List<Despicable.BodyTypeOffset>());
                            }
    
                            if (src != null)
                            {
                                foreach (var b in src)
                                {
                                    if (b?.bodyType == null) continue;
                                    dict[b.bodyType.defName] = new AgsModel.BodyTypeOffset
                                    {
                                        rootOffset = new Vector2(b.offset.x, b.offset.z),
                                        rotation = b.rotation,
                                        scale = b.scale
                                    };
                                }
                            }
                        }
                    }
                }
                catch (Exception ex) { Despicable.Core.DebugLogger.WarnExceptionOnce("AgsCommands:4", "AgsCommands ignored a non-fatal editor exception.", ex); }
    
                return dict;
            }
    private string MakeUniqueBaseDefName(string baseName)
            {
                baseName ??= "AGD_Player";
                baseName = baseName.Replace(' ', '_');
    
                var used = new HashSet<string>();
                try
                {
                    foreach (var d in DefDatabase<AnimGroupDef>.AllDefsListForReading)
                        if (d != null && !d.defName.NullOrEmpty())
                            used.Add(d.defName);
                }
                catch (Exception ex) { Despicable.Core.DebugLogger.WarnExceptionOnce("AgsCommands:5", "AgsCommands ignored a non-fatal editor exception.", ex); }
    
                if (projects != null)
                {
                    for (int i = 0; i < projects.Count; i++)
                    {
                        var p = projects[i];
                        if (p?.export?.baseDefName != null) used.Add(p.export.baseDefName);
                    }
                }
    
                string candidate = baseName;
                int n = 1;
                while (used.Contains(candidate))
                {
                    n++;
                    candidate = baseName + "_" + n;
                    if (n > 9999) break;
                }
                return candidate;
            }
}
