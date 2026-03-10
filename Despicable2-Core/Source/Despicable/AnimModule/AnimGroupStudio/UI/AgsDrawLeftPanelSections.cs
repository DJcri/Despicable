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
        string projLabel = project?.label.NullOrEmpty() != false ? "(select)" : project.label;
        if (D2Widgets.ButtonText(scrollCtx, projH.NextFixed(Mathf.Max(80f, projRow.width - (scrollCtx.Style.RowHeight + scrollCtx.Style.Gap)), UIRectTag.Button, "Project/Picker"), projLabel, "Project/Picker"))
        {
            var opts = new List<FloatMenuOption>();
            if (!projects.NullOrEmpty())
    {
                for (int i = 0; i < projects.Count; i++)
                {
                    var p = projects[i];
                    if (p == null) continue;
                    opts.Add(new FloatMenuOption(p.label ?? p.projectId, () =>
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
                    $"Delete project '{pDel?.label ?? pDel?.projectId}'?",
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

        Rect labelRow = v.NextRow(UIRectTag.Input, "Project/NameRow");
        var labelH = new HRow(scrollCtx, labelRow);
        D2Widgets.Label(scrollCtx, labelH.NextFixed(90f, UIRectTag.Label, "Project/NameLabel"), "Label", "Project/NameLabel");
        project.label = D2Widgets.TextField(scrollCtx, labelH.Remaining(UIRectTag.TextField, "Project/NameField"), project.label ?? "", 256, "Project/NameField");

        if (project.export == null) project.export = new AgsModel.ExportSpec();
        Rect defRow = v.NextRow(UIRectTag.Input, "Project/BaseDefRow");
        var defH = new HRow(scrollCtx, defRow);
        D2Widgets.Label(scrollCtx, defH.NextFixed(90f, UIRectTag.Label, "Project/BaseDefLabel"), "Base def", "Project/BaseDefLabel");
        project.export.baseDefName = D2Widgets.TextField(scrollCtx, defH.Remaining(UIRectTag.TextField, "Project/BaseDefField"), project.export.baseDefName ?? "", 256, "Project/BaseDefField");

        string baseDefHint = GetDefNameValidationHint(project.export.baseDefName);
        if (!baseDefHint.NullOrEmpty())
            v.NextTextBlock(scrollCtx, baseDefHint, GameFont.Small, padding: 2f, label: "Project/BaseDefHint");

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
            v.NextTextBlock(scrollCtx, $"Last export: {lastExportWritten} files ({lastExportOverwritten} overwritten)\n{lastExportFolder}", GameFont.Small, padding: 2f, label: "Project/LastExport");

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
