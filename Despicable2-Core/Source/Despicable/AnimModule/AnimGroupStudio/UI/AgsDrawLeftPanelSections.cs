using RimWorld;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Verse;
using Despicable.UIFramework;
using Despicable.UIFramework.Controls;
using Despicable.UIFramework.Layout;
using Despicable.AnimModule.AnimGroupStudio.Export;
using Despicable.AnimModule.AnimGroupStudio.Model;

namespace Despicable.AnimModule.AnimGroupStudio.UI;
public partial class Dialog_AnimGroupStudio
{
    private bool DrawAuthorProjectSection(UIContext scrollCtx, ref VStack v)
    {
        DrawGroupedHeader(scrollCtx, ref v, "Left/Project", "Project", topPadding: true);
        Rect projRow = v.NextRow(UIRectTag.Input, "Project/PickerRow");
        var projH = new HRow(scrollCtx, projRow);
        string projLabel = GetProjectDisplayName(project);
        if (D2Widgets.ButtonText(scrollCtx, projH.NextFixed(Mathf.Max(80f, projRow.width - (scrollCtx.Style.RowHeight + scrollCtx.Style.Gap)), UIRectTag.Button, "Project/Picker"), projLabel, "Project/Picker"))
        {
            var opts = new List<FloatMenuOption>();
            if (!projects.NullOrEmpty())
            {
                for (int i = 0; i < projects.Count; i++)
                {
                    var p = projects[i];
                    if (p == null) continue;
                    opts.Add(new FloatMenuOption(GetProjectDisplayName(p), () =>
                    {
                        project = p;
                        authorStageIndex = 0;
                        authorTrackIndex = -1;
                        authorKeyIndex = -1;
                    }));
                }
            }
            opts.Add(new FloatMenuOption("+ New project", () =>
            {
                var p = repo.CreateNewProject();
                projects.Add(p);
                project = p;
                authorStageIndex = 0;
                authorTrackIndex = -1;
                authorKeyIndex = -1;
                TrySaveProjects();
            }));
            Find.WindowStack.Add(new FloatMenu(opts));
        }

        bool canDeleteProj = project != null && projects != null && projects.Count > 1;
        Rect deleteRect = projH.NextFixed(scrollCtx.Style.RowHeight, UIRectTag.Button, "Project/Delete");
        if (canDeleteProj)
        {
            if (DrawIconButton(scrollCtx, deleteRect, D2VanillaTex.Delete, "Delete project", "Project/Delete"))
            {
                var pDel = project;
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    $"Delete project '{GetProjectDisplayName(pDel)}'?",
                    () =>
                    {
                        StopAuthorPreview(resetTick: true);
                        projects.Remove(pDel);
                        if (projects.NullOrEmpty())
                        {
                            var np = repo.CreateNewProject();
                            projects = new List<AgsModel.Project> { np };
                        }
                        project = projects[0];
                        authorStageIndex = 0;
                        authorTrackIndex = -1;
                        authorKeyIndex = -1;
                        RebuildFamilies();
                        TrySaveProjects();
                    }));
            }
        }
        else
        {
            DrawIconButton(scrollCtx, deleteRect, D2VanillaTex.Delete, "Delete project", "Project/DeleteDisabled", enabled: false, disabledReason: "You need at least two projects to delete one.");
        }

        if (project == null)
        {
            v.NextTextBlock(scrollCtx, "No project loaded.", GameFont.Small, padding: 2f, label: "Project/None");
            return false;
        }

        if (project.export == null) project.export = new AgsModel.ExportSpec();
        if (project.groupTags == null) project.groupTags = new List<string>();

        string oldLabel = project.label ?? string.Empty;
        string oldBaseDef = project.export.baseDefName ?? string.Empty;

        Rect labelRow = v.NextRow(UIRectTag.Input, "Project/NameRow");
        var labelH = new HRow(scrollCtx, labelRow);
        D2Widgets.Label(scrollCtx, labelH.NextFixed(90f, UIRectTag.Label, "Project/NameLabel"), "Variation", "Project/NameLabel");
        project.label = D2Widgets.TextField(scrollCtx, labelH.Remaining(UIRectTag.TextField, "Project/NameField"), project.label ?? "", 256, "Project/NameField");

        Rect defRow = v.NextRow(UIRectTag.Input, "Project/BaseDefRow");
        var defH = new HRow(scrollCtx, defRow);
        D2Widgets.Label(scrollCtx, defH.NextFixed(90f, UIRectTag.Label, "Project/BaseDefLabel"), "Def prefix", "Project/BaseDefLabel");
        project.export.baseDefName = D2Widgets.TextField(scrollCtx, defH.Remaining(UIRectTag.TextField, "Project/BaseDefField"), project.export.baseDefName ?? "", 256, "Project/BaseDefField");

        string resolvedDefName = GetResolvedProjectVariationDefName(project);
        v.NextTextBlock(scrollCtx, "Full defName: " + resolvedDefName, GameFont.Small, padding: 2f, label: "Project/ResolvedDefName");

        string variationHint = GetVariationLabelValidationHint(project.label);
        if (!variationHint.NullOrEmpty())
            v.NextTextBlock(scrollCtx, variationHint, GameFont.Small, padding: 2f, label: "Project/VariationHint");

        string baseDefHint = GetDefNameValidationHint(project.export.baseDefName);
        if (!baseDefHint.NullOrEmpty())
            v.NextTextBlock(scrollCtx, baseDefHint, GameFont.Small, padding: 2f, label: "Project/BaseDefHint");

        v.NextSpace(scrollCtx.Style.Gap);
        Rect tagsHeaderRow = v.NextRow(UIRectTag.Label, "Project/TagsHeader");
        D2Widgets.Label(scrollCtx, tagsHeaderRow, "Group Tags", "Project/TagsLabel");

        float chipLineH = scrollCtx.Style.Line;
        float chipGap = Mathf.Max(3f, scrollCtx.Style.Gap * 0.5f);
        int tagCount = project.groupTags.Count;
        float chipAreaH;
        if (tagCount == 0)
        {
            chipAreaH = chipLineH;
        }
        else
        {
            float curX = 0f;
            int lineCount = 1;
            float closeW = chipLineH;
            float chipPadX = 6f;
            GameFont prev = Text.Font;
            Text.Font = GameFont.Small;
            for (int ti = 0; ti < tagCount; ti++)
            {
                string t = project.groupTags[ti] ?? "";
                float chipW = Mathf.Min(Text.CalcSize(t).x + chipPadX * 2f + closeW + chipGap, v.Bounds.width);
                if (curX > 0f && curX + chipW > v.Bounds.width)
                {
                    lineCount++;
                    curX = 0f;
                }
                curX += chipW + chipGap;
            }
            Text.Font = prev;
            chipAreaH = lineCount * chipLineH + Mathf.Max(0, lineCount - 1) * chipGap;
        }

        Rect chipAreaRect = v.Next(chipAreaH, UIRectTag.None, "Project/TagChips");
        bool tagsDirty = false;
        if (scrollCtx.Pass == UIPass.Draw)
        {
            if (tagCount == 0)
            {
                var prevColor = GUI.color;
                GUI.color = Color.gray;
                Widgets.Label(chipAreaRect, "(none)");
                GUI.color = prevColor;
            }
            else
            {
                float closeW = chipLineH;
                float chipPadX = 6f;
                var flow = new HFlow(scrollCtx, chipAreaRect, chipLineH, chipGap);
                GameFont prevFont = Text.Font;
                Text.Font = GameFont.Small;
                int removeAt = -1;
                for (int ti = 0; ti < tagCount; ti++)
                {
                    string t = project.groupTags[ti] ?? "";
                    float chipW = Mathf.Min(Text.CalcSize(t).x + chipPadX * 2f + closeW + chipGap, chipAreaRect.width);
                    Rect chipRect = flow.Next(chipW);
                    Widgets.DrawBoxSolid(chipRect, new Color(0.25f, 0.25f, 0.25f, 0.85f));

                    Rect labelChipRect = new Rect(chipRect.xMin + chipPadX, chipRect.yMin, chipRect.width - chipPadX - closeW, chipRect.height);
                    Widgets.Label(labelChipRect, t);

                    Rect closeRect = new Rect(chipRect.xMax - closeW, chipRect.yMin, closeW, chipRect.height);
                    TooltipHandler.TipRegion(closeRect, "Remove tag");
                    if (Widgets.ButtonImage(closeRect, D2VanillaTex.CloseX))
                        removeAt = ti;
                }
                Text.Font = prevFont;
                if (removeAt >= 0)
                {
                    project.groupTags.RemoveAt(removeAt);
                    tagsDirty = true;
                }
            }
        }

        Rect addTagRow = v.NextRow(UIRectTag.Button, "Project/AddTagRow");
        var addTagH = new HRow(scrollCtx, addTagRow);
        Rect addTagBtnRect = addTagH.NextFixed(scrollCtx.Style.RowHeight, UIRectTag.Button, "Project/AddTagBtn");
        if (DrawIconButton(scrollCtx, addTagBtnRect, D2VanillaTex.Plus, "Add group tag", "Project/AddTagBtn") && scrollCtx.Pass == UIPass.Draw)
        {
            var currentProject = project;
            Find.WindowStack.Add(new Dialog_AgsAddTag(newTag =>
            {
                if (newTag.NullOrEmpty()) return;
                if (currentProject.groupTags == null) currentProject.groupTags = new List<string>();
                string trimmed = newTag.Trim();
                if (!trimmed.NullOrEmpty() && !currentProject.groupTags.Contains(trimmed))
                {
                    currentProject.groupTags.Add(trimmed);
                    TrySaveProjects();
                }
            }));
        }

        if (project.label != oldLabel || project.export.baseDefName != oldBaseDef || tagsDirty)
            TrySaveProjects();

        var projectActions = new List<D2ActionBar.Item>
        {
            new D2ActionBar.Item("Save", "Save project") { MinWidthOverride = 108f },
            new D2ActionBar.Item("Validate", "Validate") { MinWidthOverride = 92f },
            new D2ActionBar.Item("Export", "Export") { MinWidthOverride = 88f },
            new D2ActionBar.Item("OpenExport", "Open export folder")
            {
                MinWidthOverride = 148f,
                Disabled = lastExportFolder.NullOrEmpty() || !Directory.Exists(lastExportFolder),
                DisabledReason = "Export the project first to open the last written folder."
            },
        };
        float projActionsH = D2ActionBar.MeasureHeight(scrollCtx, new Rect(0f, 0f, v.Bounds.width, 9999f), projectActions);
        var projActionsRect = v.Next(projActionsH, UIRectTag.Input, "Project/Actions");
        var projActionsRes = D2ActionBar.Draw(scrollCtx, projActionsRect, projectActions, "Project/Actions");
        if (projActionsRes.Clicked)
        {
            switch (projActionsRes.ActivatedId)
            {
                case "Save": TrySaveProjects(); break;
                case "Validate": TryValidateProject(showSuccess: true); break;
                case "Export": TryExportProject(); break;
                case "OpenExport": TryOpenFolder(lastExportFolder); break;
            }
        }

        if (!lastExportFolder.NullOrEmpty())
            v.NextTextBlock(scrollCtx, $"Last export: {lastExportWritten} file(s) written, {lastExportOverwritten} overwritten\n{lastExportFolder}", GameFont.Small, padding: 2f, label: "Project/LastExport");

        v.NextSpace(4f);
        return true;
    }


    private void DrawAuthorRolesSection(UIContext scrollCtx, ref VStack v)
    {
        DrawGroupedHeader(scrollCtx, ref v, "Left/Roles", "Roles", topPadding: true);
        EnsureRoles(project);
        EnsureAuthorRoleKeyValid(project);
        var curRole = GetRole(project, authorRoleKey);
        Rect roleRow = v.NextRow(UIRectTag.Input, "Roles/Row");
        var roleH = new HRow(scrollCtx, roleRow);
        string roleLabel = curRole?.displayName ?? authorRoleKey;
        if (D2Widgets.ButtonText(scrollCtx, roleH.NextFixed(Mathf.Max(80f, roleRow.width - 130f), UIRectTag.Button, "Roles/Picker"), roleLabel, "Roles/Picker"))
        {
            var opts = new List<FloatMenuOption>();
            for (int i = 0; i < project.roles.Count; i++)
    {
                var r = project.roles[i];
                if (r == null) continue;
                string suffix = r.genderReq == AgsModel.RoleGenderReq.Female ? "Female" : (r.genderReq == AgsModel.RoleGenderReq.Unisex ? "Unisex" : "Male");
                string menuLabel = $"{r.displayName ?? r.roleKey} ({suffix})";
                string captured = r.roleKey;
                opts.Add(new FloatMenuOption(menuLabel, () =>
                {
                    authorRoleKey = captured;
                    authorTrackIndex = -1;
                    authorKeyIndex = -1;
                }));
            }
            if (opts.Count == 0) opts.Add(new FloatMenuOption("(none)", null));
            Find.WindowStack.Add(new FloatMenu(opts));
        }
        if (DrawIconButton(scrollCtx, roleH.NextFixed(28f, UIRectTag.Button, "Roles/Add"), D2VanillaTex.Plus, "Add role", "Roles/Add"))
        {
            Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>
    {
                new FloatMenuOption("Male", () => AddRole(project, AgsModel.RoleGenderReq.Male)),
                new FloatMenuOption("Female", () => AddRole(project, AgsModel.RoleGenderReq.Female)),
                new FloatMenuOption("Unisex", () => AddRole(project, AgsModel.RoleGenderReq.Unisex)),
            }));
        }
        bool canDeleteRole = project.roles != null && project.roles.Count > 1 && curRole != null;
        Rect delRoleRect = roleH.NextFixed(28f, UIRectTag.Button, "Roles/Delete");
        if (canDeleteRole)
        {
            if (DrawIconButton(scrollCtx, delRoleRect, D2VanillaTex.Delete, "Delete role", "Roles/Delete"))
    {
                var ro = curRole;
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation($"Delete role '{ro.displayName ?? ro.roleKey}'?", () =>
                {
                    DeleteRole(project, ro.roleKey);
                    EnsureAuthorRoleKeyValid(project);
                    TrySaveProjects();
                }, destructive: true));
            }
        }
        else
        {
            DrawIconButton(scrollCtx, delRoleRect, D2VanillaTex.Delete, "Delete role", "Roles/DeleteDisabled", enabled: false, disabledReason: "A project needs at least one role.");
        }
        Rect renRoleRect = roleH.Remaining(UIRectTag.Button, "Roles/Rename");
        if (curRole != null)
        {
            if (D2Widgets.ButtonText(scrollCtx, renRoleRect, "Edit", "Roles/Rename"))
    {
                var ro = curRole;
                Find.WindowStack.Add(new Dialog_TextEntrySimple("Role name", ro.displayName ?? ro.roleKey, (s) =>
                {
                    ro.displayName = s;
                    TrySaveProjects();
                }, validator: (s) => s.NullOrEmpty() ? "Name cannot be empty." : null));
            }
        }
        else
        {
            scrollCtx.Record(renRoleRect, UIRectTag.Button, "Roles/RenameDisabled");
            if (scrollCtx.Pass == UIPass.Draw)
    {
                using (new GUIEnabledScope(false)) Widgets.ButtonText(renRoleRect, "Edit");
            }
        }

        v.NextSpace(Mathf.Max(2f, scrollCtx.Style.Gap * 0.2f));
    }
}
