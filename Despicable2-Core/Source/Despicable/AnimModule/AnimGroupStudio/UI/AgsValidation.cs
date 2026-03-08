using RimWorld;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;
using Verse;
using Despicable.AnimModule.AnimGroupStudio.Export;

namespace Despicable.AnimModule.AnimGroupStudio.UI;
public partial class Dialog_AnimGroupStudio
{
    private bool TryValidateProject(bool showSuccess)
            {
                try
                {
                    var exporter = new AgsExport();
                    var vr = exporter.Validate(project);
    
                    if (!vr.Ok)
                    {
                        string msg = "Validation failed:\n\n" + string.Join("\n", vr.errors);
                        Find.WindowStack.Add(new Dialog_MessageBox(msg));
                        return false;
                    }
    
                    if (showSuccess)
                    {
                        string msg = "Validation passed.";
                        if (!vr.warnings.NullOrEmpty())
                            msg += "\n\nWarnings:\n" + string.Join("\n", vr.warnings);
                        Find.WindowStack.Add(new Dialog_MessageBox(msg));
                    }
    
                    return true;
                }
                catch (Exception e)
                {
                    Log.Warning("[Despicable] [AGS] Validate failed: " + e);
                    Find.WindowStack.Add(new Dialog_MessageBox("Validation failed (see log)."));
                    return false;
                }
            }

}
