using System.Collections.Generic;
using System.IO;
using Verse;
using Despicable.AnimModule.AnimGroupStudio.Model;

namespace Despicable.AnimGroupStudio;
/// <summary>
/// Clean-slate persistence for Anim Group Studio projects.
///
/// Writes to a new path so legacy workshop data remains untouched.
/// </summary>
public sealed class AgsRepository
{
    // Store in the user's RimWorld config directory (not the game install folder).
    // Using a relative path like "Config/..." causes Scribe to resolve against the
    // current working directory (typically the RimWorld install dir), which fails on
    // Steam installs due to write permissions and missing folders.
    private static string ProjectsFilePath => Path.Combine(
        GenFilePaths.ConfigFolderPath,
        "Despicable",
        "AnimGroupStudio",
        "Projects.xml");

    public List<AgsModel.Project> LoadAll()
    {
        var abs = ProjectsFilePath;
        if (!File.Exists(abs))
            return new List<AgsModel.Project>();

        var projects = new List<AgsModel.Project>();
        Scribe.loader.InitLoading(abs);
        try
        {
            Scribe_Collections.Look(ref projects, "projects", LookMode.Deep);
        }
        finally
        {
            Scribe.loader.FinalizeLoading();
        }
        return projects ?? new List<AgsModel.Project>();
    }

    public void SaveAll(List<AgsModel.Project> projects)
    {
        var abs = ProjectsFilePath;
        try
        {
            var dir = Path.GetDirectoryName(abs);
            if (!dir.NullOrEmpty())
                Directory.CreateDirectory(dir);
        }
        catch
        {
            // If we can't create the directory for some reason, let Scribe throw a useful error.
        }

        Scribe.saver.InitSaving(abs, "AgsProjects");
        try
        {
            Scribe_Collections.Look(ref projects, "projects", LookMode.Deep);
        }
        finally
        {
            Scribe.saver.FinalizeSaving();
        }
    }

    public AgsModel.Project CreateNewProject()
    {
        var p = new AgsModel.Project
        {
            projectId = System.Guid.NewGuid().ToString("N"),
            label = "",
            groupTags = new List<string>(),
            export = new AgsModel.ExportSpec { baseDefName = "AGD_NewGroup" }
        };

        // Default roles: male + female dummies for visual distinction.
        p.roles = new List<AgsModel.RoleSpec>
        {
            AgsModel.RoleSpec.MakeDefault("male_1", "Male 1", AgsModel.RoleGenderReq.Male),
            AgsModel.RoleSpec.MakeDefault("female_1", "Female 1", AgsModel.RoleGenderReq.Female)
        };

        // Default stage 0 with Base variant.
        var s0 = new AgsModel.StageSpec { stageIndex = 0, label = "Stage 0", durationTicks = 60 };
        s0.variants.Add(new AgsModel.StageVariant
        {
            variantId = "Base",
            clips = new List<AgsModel.RoleClip>
            {
                new AgsModel.RoleClip { roleKey = "male_1", clip = new AgsModel.ClipSpec() },
                new AgsModel.RoleClip { roleKey = "female_1", clip = new AgsModel.ClipSpec() }
            }
        });
        p.stages.Add(s0);

        // Offset dictionaries (per-role) start empty.
        p.offsetsByRoleKey = new Dictionary<string, Dictionary<string, AgsModel.BodyTypeOffset>>
        {
            ["male_1"] = new Dictionary<string, AgsModel.BodyTypeOffset>(),
            ["female_1"] = new Dictionary<string, AgsModel.BodyTypeOffset>()
        };

        return p;
    }
}
