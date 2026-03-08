using System.Collections.Generic;
using Verse;

namespace Despicable;
public class AnimGroupDef : Def
{
    public List<AnimRoleDef> animRoles;
    public int numActors = 1;
    public List<int> loopIndex;

    /// <summary>
    /// Optional content tags used to filter animation groups.
    ///
    /// IMPORTANT: This is intentionally string-based so Core does not need to reference
    /// module-specific types provided by external modules.
    /// Typical value: some module's DefName, or any agreed-upon stage id.
    /// </summary>
    public List<string> stageTags;

    // Optional: relative folder (within a mod project root) that Workshop exports should write into.
    // Example: "Defs/AnimModule/AnimationDefs/Bed".
    public string workshopExportFolder;
}
