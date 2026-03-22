using RimWorld;
using Verse;

namespace Despicable;
internal static class LegacyAnatomyDefLookup
{
    private const string PenisHediffDefName = "D2_Genital_Penis";
    private const string VaginaHediffDefName = "D2_Genital_Vagina";

    private static bool lookedUpPenis;
    private static bool lookedUpVagina;

    private static HediffDef cachedPenis;
    private static HediffDef cachedVagina;

    internal static BodyPartDef ExternalGenitals => null;

    internal static BodyPartGroupDef GenitalsGroup => null;

    internal static HediffDef PenisHediff
    {
        get
        {
            if (!lookedUpPenis)
            {
                lookedUpPenis = true;
                cachedPenis = DefDatabase<HediffDef>.GetNamedSilentFail(PenisHediffDefName);
            }

            return cachedPenis;
        }
    }

    internal static HediffDef VaginaHediff
    {
        get
        {
            if (!lookedUpVagina)
            {
                lookedUpVagina = true;
                cachedVagina = DefDatabase<HediffDef>.GetNamedSilentFail(VaginaHediffDefName);
            }

            return cachedVagina;
        }
    }

    internal static bool HasBridgeDefs => PenisHediff != null || VaginaHediff != null;

    internal static void ResetCache()
    {
        lookedUpPenis = false;
        lookedUpVagina = false;
        cachedPenis = null;
        cachedVagina = null;
    }
}
