using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Despicable.NSFW.Integrations.GenderWorks;
internal static class GenderWorksUtil
{
    private const string MaleReproTag = "SEX_MaleReproductiveOrgan";
    private const string FemaleReproTag = "SEX_FemaleReproductiveOrgan";
    private const string AnyReproTag = "SEX_ReproductiveOrgan";
    private const string GenderUtilitiesTypeName = "LoveyDoveySexWithRosaline.GenderUtilities";

    private static bool reflectionResolved;
    private static MethodInfo hasMaleReproductiveOrganMethod;
    private static MethodInfo hasFemaleReproductiveOrganMethod;
    private static MethodInfo hasAnyReproductiveOrganMethod;

    internal static bool HasMaleReproductiveOrgan(Pawn pawn)
    {
        bool value;
        if (TryInvokeUtilityBool(ref hasMaleReproductiveOrganMethod, "HasMaleReproductiveOrgan", pawn, out value))
            return value;

        return HasMaleReproductiveOrganTag(pawn);
    }

    internal static bool HasFemaleReproductiveOrgan(Pawn pawn)
    {
        bool value;
        if (TryInvokeUtilityBool(ref hasFemaleReproductiveOrganMethod, "HasFemaleReproductiveOrgan", pawn, out value))
            return value;

        return HasFemaleReproductiveOrganTag(pawn);
    }

    internal static bool HasAnyReproductiveSignal(Pawn pawn)
    {
        bool value;
        if (TryInvokeUtilityBool(ref hasAnyReproductiveOrganMethod, "HasReproductiveOrgan", pawn, out value)
            || TryInvokeUtilityBool(ref hasAnyReproductiveOrganMethod, "HasAnyReproductiveOrgan", pawn, out value))
        {
            return value;
        }

        return HasHediffTag(pawn, AnyReproTag)
            || HasMaleReproductiveOrganTag(pawn)
            || HasFemaleReproductiveOrganTag(pawn);
    }

    internal static bool TryResolveForDespicable(Pawn pawn, out bool wantsPenis, out bool wantsVagina)
    {
        wantsPenis = false;
        wantsVagina = false;

        if (pawn == null)
            return false;

        bool maleKnown = TryInvokeUtilityBool(ref hasMaleReproductiveOrganMethod, "HasMaleReproductiveOrgan", pawn, out wantsPenis);
        bool femaleKnown = TryInvokeUtilityBool(ref hasFemaleReproductiveOrganMethod, "HasFemaleReproductiveOrgan", pawn, out wantsVagina);

        bool hasAnyOrgan = false;
        bool anyResolved = TryInvokeUtilityBool(ref hasAnyReproductiveOrganMethod, "HasReproductiveOrgan", pawn, out hasAnyOrgan)
            || TryInvokeUtilityBool(ref hasAnyReproductiveOrganMethod, "HasAnyReproductiveOrgan", pawn, out hasAnyOrgan);

        if (!maleKnown)
            wantsPenis = HasMaleReproductiveOrganTag(pawn);

        if (!femaleKnown)
            wantsVagina = HasFemaleReproductiveOrganTag(pawn);

        if (wantsPenis || wantsVagina)
            return true;

        if (HasHediffTag(pawn, AnyReproTag))
            return true;

        if (anyResolved)
            return true;

        return false;
    }

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
        }
        return false;
    }

    private static void EnsureReflectionResolved()
    {
        if (reflectionResolved)
            return;

        reflectionResolved = true;

        try
        {
            var utilityType = AccessTools.TypeByName(GenderUtilitiesTypeName);
            if (utilityType == null)
                return;

            hasMaleReproductiveOrganMethod = ResolveUtilityMethod(utilityType, "HasMaleReproductiveOrgan");
            hasFemaleReproductiveOrganMethod = ResolveUtilityMethod(utilityType, "HasFemaleReproductiveOrgan");
            hasAnyReproductiveOrganMethod = ResolveUtilityMethod(utilityType, "HasReproductiveOrgan")
                ?? ResolveUtilityMethod(utilityType, "HasAnyReproductiveOrgan");
        }
        catch
        {
        }
    }

    private static MethodInfo ResolveUtilityMethod(System.Type utilityType, string methodName)
    {
        var method = AccessTools.Method(utilityType, methodName, new[] { typeof(Pawn) });
        if (method == null || !method.IsStatic || method.ReturnType != typeof(bool))
            return null;

        return method;
    }

    private static bool TryInvokeUtilityBool(ref MethodInfo cachedMethod, string methodName, Pawn pawn, out bool value)
    {
        value = false;

        if (pawn == null)
            return false;

        EnsureReflectionResolved();
        if (cachedMethod == null)
            return false;

        try
        {
            object result = cachedMethod.Invoke(null, new object[] { pawn });
            if (result is bool boolResult)
            {
                value = boolResult;
                return true;
            }
        }
        catch
        {
        }

        return false;
    }
}
