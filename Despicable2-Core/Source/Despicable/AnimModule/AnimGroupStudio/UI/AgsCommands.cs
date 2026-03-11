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
    private void TrySaveProjects()
            {
                QueueAuthorSave();
            }

    private void FlushQueuedSaveIfNeeded(bool force = false)
            {
                if (!force && Event.current != null && Event.current.type != EventType.Repaint)
                    return;
                if (!authorRuntime.SavePending)
                    return;
                try
                {
                    repo.SaveAll(projects);
                    authorRuntime.SavePending = false;
                }
                catch (System.Exception e) { Log.Warning("[Despicable] [AGS] Save failed: " + e.Message); }
            }

    private void TryExportProject()
            {
                if (!TryValidateProject(showSuccess: false)) return;
    
                var exporter = new AgsExport();
                AgsExport.ExportPlan plan = null;
                try
                {
                    plan = exporter.BuildPlan(project);
                }
                catch (Exception e)
                {
                    Log.Warning("[Despicable] [AGS] BuildPlan failed: " + e);
                    Find.WindowStack.Add(new Dialog_MessageBox("Export failed while building plan (see log)."));
                    return;
                }
    
                Action doExport = () =>
                {
                    try
                    {
                        var res = exporter.ExportAll(project, allowOverwrite: true);
    
                        lastExportFolder = plan.packageDir;
                        lastExportWritten = res.filesWritten.Count;
                        lastExportOverwritten = res.filesOverwritten.Count;
    
                        string msg = "Export complete.\n\n" +
                                     $"Package: {plan.packageDir}\n" +
                                     $"Group file: {plan.groupFilePath}\n" +
                                     $"Offsets file: {plan.offsetFilePath}\n" +
                                     $"Stages: {plan.stagesDir}\n\n" +
                                     $"Wrote {lastExportWritten} file(s) ({lastExportOverwritten} overwritten).\n\n" +
                                     "Restart RimWorld to load the new defs.";
                        Find.WindowStack.Add(new Dialog_MessageBox(msg));
                    }
                    catch (Exception e)
                    {
                        Log.Warning("[Despicable] [AGS] Export failed: " + e);
                        Find.WindowStack.Add(new Dialog_MessageBox("Export failed (see log)."));
                    }
                };
    
                if (plan != null && !plan.existingTargets.NullOrEmpty())
                {
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                        $"Export will overwrite {plan.existingTargets.Count} existing file(s). Continue?\n\n{plan.packageDir}",
                        doExport));
                }
                else
                {
                    doExport();
                }
            }

    private static void TryOpenFolder(string dir)
            {
                try
                {
                    if (dir.NullOrEmpty() || !Directory.Exists(dir))
                    {
                        Messages.Message("Export folder does not exist.", MessageTypeDefOf.RejectInput, false);
                        return;
                    }
    
                    // Unity: works on most platforms.
                    try
                    {
                        Application.OpenURL("file://" + dir.Replace("\\", "/"));
                        return;
                    }
                    catch (Exception ex) { Despicable.Core.DebugLogger.WarnExceptionOnce("AgsCommands:1", "AgsCommands ignored a non-fatal editor exception.", ex); }
    
                    // Fallback.
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = dir,
                        UseShellExecute = true,
                        Verb = "open"
                    });
                }
                catch (Exception e)
                {
                    Log.Warning("[Despicable] [AGS] Failed to open folder: " + e);
                    Messages.Message("Failed to open folder (see log).", MessageTypeDefOf.RejectInput, false);
                }
            }

}
