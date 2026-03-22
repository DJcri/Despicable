using Verse;

namespace Despicable;
internal static class AnatomyAnchorResolver
{
    internal static bool TryGetAnchor(Pawn pawn, AnatomySlotDef slot, out BodyPartRecord part)
    {
        part = null;
        if (slot == null)
            return false;

        switch (slot.anchorKey)
        {
            case "ExternalGenitals":
                return AnatomyQuery.TryGetStableAnatomyAnchor(pawn, out part);
            case "Pelvis":
                return AnatomyQuery.TryGetPelvis(pawn, out part);
            case "LegacyExternalGenitals":
                return AnatomyQuery.TryGetLegacyExternalGenitals(pawn, out part);
            case "GenderWorksReproduction":
                return AnatomyQuery.TryGetGenderWorksReproductionPart(pawn, out part);
            case "Torso":
            case "Chest":
                return AnatomyQuery.TryGetTorso(pawn, out part);
            default:
                return false;
        }
    }
}
