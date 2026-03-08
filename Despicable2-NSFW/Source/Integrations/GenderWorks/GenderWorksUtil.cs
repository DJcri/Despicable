using RimWorld;
using Verse;

namespace Despicable.NSFW.Integrations.GenderWorks;
internal static class GenderWorksUtil
{
    // GenderWorks tags observed in defs:
    //  - SEX_MaleReproductiveOrgan
    //  - SEX_FemaleReproductiveOrgan
    private const string MaleReproTag = "SEX_MaleReproductiveOrgan";
    private const string FemaleReproTag = "SEX_FemaleReproductiveOrgan";

    internal static bool HasMaleReproductiveOrganTag(Pawn pawn) => HasHediffTag(pawn, MaleReproTag);

    internal static bool HasFemaleReproductiveOrganTag(Pawn pawn) => HasHediffTag(pawn, FemaleReproTag);

    internal static bool HasHediffTag(Pawn pawn, string tag)
    {
        try
        {
            if (tag.NullOrEmpty()) return false;
            if (pawn?.health?.hediffSet?.hediffs == null) return false;
            var hs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hs.Count; i++)
            {
                var def = hs[i]?.def;
                var tags = def?.tags;
                if (tags == null) continue;
                for (int t = 0; t < tags.Count; t++)
                {
                    if (tags[t] == tag)
                        return true;
                }
            }
        }
        catch
        {
            // swallow - soft integration
        }
        return false;
    }
}
