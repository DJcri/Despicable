using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Despicable;
/// <summary>
/// Handles LOGIC and contains FUNCTIONS for
/// FINDING ANIMATIONS and ASSIGNING ROLES
/// </summary>
public static class ContextUtil
{
    /// <summary>
    /// Returns list of playable animation groups in context to participants.
    /// If stageTag is provided, only groups whose stageTags contains it are returned.
    /// </summary>
    public static List<AnimGroupDef> GetPlayableAnimationsFor(List<Pawn> participants, string stageTag = null)
    {
        // NOTE: participant-based filtering is not yet implemented here; this mirrors current behavior.
        if (!stageTag.NullOrEmpty())
        {
            return DefDatabase<AnimGroupDef>.AllDefsListForReading
                .Where(def => def.stageTags != null && def.stageTags.Contains(stageTag))
                .ToList();
        }

        return DefDatabase<AnimGroupDef>.AllDefsListForReading.ToList();
    }

    // Validates whether or not pawn fits role within animation group
    public static bool CheckPawnFitsRole(AnimRoleDef animRole, Pawn pawn)
    {
        if (PawnStateUtil.ComparePawnGenderToByte(pawn, (byte)animRole.gender) || (animRole.gender < 1))
        {
            return true;
        }

        return false;
    }

    // Checks whether or not animation group fits the context
    public static Dictionary<string, Pawn> AssignRoles(AnimGroupDef animGroup, List<Pawn> participants)
    {
        Dictionary<string, Pawn> roleAssignments = new();

        // Simple check
        if (participants.Count != animGroup.numActors)
        {
            return null;
        }

        // Fill specific roles first
        foreach (AnimRoleDef role in animGroup.animRoles.ToList())
        {
            if (role.gender >= 1)
            {
                foreach (Pawn pawn in participants)
                {
                    if (CheckPawnFitsRole(role, pawn) && !roleAssignments.ContainsValue(pawn))
                    {
                        roleAssignments.Add(role.defName, pawn);
                    }
                }
            }
        }

        // Fill flexible roles second
        foreach (AnimRoleDef role in animGroup.animRoles.ToList())
        {
            if (role.gender < 1)
            {
                foreach (Pawn pawn in participants)
                {
                    if (!roleAssignments.ContainsValue(pawn))
                    {
                        roleAssignments.Add(role.defName, pawn);
                    }
                }
            }
        }

        if (roleAssignments.Count < participants.Count)
        {
            return null;
        }

        return roleAssignments;
    }
}
