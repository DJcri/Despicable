using RimWorld;
using Verse;
using Despicable.NSFW.Integrations;
using Despicable.NSFW.Integrations.GenderWorks;

namespace Despicable;
internal static class AnatomyBootstrapper
{
    internal static bool TrySeed(Pawn pawn)
    {
        if (pawn == null || pawn.RaceProps?.Humanlike != true)
            return false;

        if (pawn.health?.hediffSet == null)
            return false;

        BodyPartRecord part;
        if (!AnatomyQuery.TryGetExternalGenitals(pawn, out part))
            return false;

        if (pawn.health.hediffSet.PartIsMissing(part))
            return true;

        if (AnatomyQuery.HasKnownExternalGenitalAnatomy(pawn))
            return true;

        if (IntegrationGuards.IsGenderWorksLoaded())
        {
            bool wantsPenis = GenderWorksUtil.HasMaleReproductiveOrganTag(pawn);
            bool wantsVagina = GenderWorksUtil.HasFemaleReproductiveOrganTag(pawn);
            ApplyExactSet(pawn, part, wantsPenis, wantsVagina);
            return true;
        }

        if (pawn.def == ThingDefOf.Human)
        {
            bool wantsPenis = pawn.gender == Gender.Male;
            bool wantsVagina = pawn.gender == Gender.Female;
            ApplyExactSet(pawn, part, wantsPenis, wantsVagina);
            return true;
        }

        return true;
    }

    private static void ApplyExactSet(Pawn pawn, BodyPartRecord part, bool wantsPenis, bool wantsVagina)
    {
        RemoveIfPresent(pawn, part, LovinModule_AnatomyDefOf.D2_Genital_Penis, !wantsPenis);
        RemoveIfPresent(pawn, part, LovinModule_AnatomyDefOf.D2_Genital_Vagina, !wantsVagina);

        AddIfMissing(pawn, part, LovinModule_AnatomyDefOf.D2_Genital_Penis, wantsPenis);
        AddIfMissing(pawn, part, LovinModule_AnatomyDefOf.D2_Genital_Vagina, wantsVagina);
    }

    private static void AddIfMissing(Pawn pawn, BodyPartRecord part, HediffDef def, bool shouldHave)
    {
        if (!shouldHave || def == null)
            return;

        if (GetPartHediff(pawn, part, def) != null)
            return;

        pawn.health.AddHediff(def, part);
    }

    private static void RemoveIfPresent(Pawn pawn, BodyPartRecord part, HediffDef def, bool shouldRemove)
    {
        if (!shouldRemove || def == null)
            return;

        Hediff hediff = GetPartHediff(pawn, part, def);
        if (hediff != null)
            pawn.health.RemoveHediff(hediff);
    }

    private static Hediff GetPartHediff(Pawn pawn, BodyPartRecord part, HediffDef def)
    {
        var hediffs = pawn?.health?.hediffSet?.hediffs;
        if (hediffs == null)
            return null;

        for (int i = 0; i < hediffs.Count; i++)
        {
            Hediff hediff = hediffs[i];
            if (hediff?.def == def && hediff.Part == part)
                return hediff;
        }

        return null;
    }
}
