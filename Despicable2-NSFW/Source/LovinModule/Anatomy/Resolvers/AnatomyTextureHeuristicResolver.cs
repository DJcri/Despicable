using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Despicable;
internal static class AnatomyTextureHeuristicResolver
{
    private static readonly Dictionary<string, PenisTextureFamily> ExactFamilyMatches = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Android"] = PenisTextureFamily.Xeno,
        ["Androids"] = PenisTextureFamily.Xeno,
        ["Saurid"] = PenisTextureFamily.Reptile,
        ["Dragonian"] = PenisTextureFamily.Reptile,
        ["Ratkin"] = PenisTextureFamily.Sheathed,
        ["Kurin"] = PenisTextureFamily.Sheathed,
        ["Slime"] = PenisTextureFamily.Xeno,
        ["Slimes"] = PenisTextureFamily.Xeno,
        ["Insect"] = PenisTextureFamily.Insect,
        ["Insects"] = PenisTextureFamily.Insect,
        ["Insectoid"] = PenisTextureFamily.Insect,
        ["Insectoids"] = PenisTextureFamily.Insect,
        ["Yttakin"] = PenisTextureFamily.Sheathed,
        ["Furskin"] = PenisTextureFamily.Sheathed,
        ["Pigskin"] = PenisTextureFamily.Sheathed,
        ["Phytokin"] = PenisTextureFamily.Xeno,
    };

    private static readonly string[] SheathedTokens =
    {
        "canin", "canid", "wolf", "vulp", "fox", "lupin", "kurin",
        "ratkin", "rodent", "rabbit", "hare", "beast", "anthro",
        "kemono", "feline", "neko", "sheath", "ytta", "furskin",
        "pigskin", "pig", "boar", "swine", "hog"
    };

    private static readonly string[] ReptileTokens =
    {
        "saur", "lizard", "rept", "dragon", "draco", "drake",
        "serpent", "snake", "naga", "gecko", "scaled", "scale",
        "croc", "alligator"
    };

    private static readonly string[] InsectTokens =
    {
        "insect", "bug", "chitin", "arthro", "arthropod", "beetle",
        "roach", "moth", "wasp", "hornet", "bee", "ant", "termite",
        "mantis", "locust", "scarab", "hive"
    };

    private static readonly string[] XenoTokens =
    {
        "android", "droid", "robot", "synthe", "mech", "mechan",
        "cyber", "bionic", "archotech", "xeno", "alien",
        "extrater", "void", "eldrit", "aberr", "slime", "ooze", "gel", "gelatin", "amorph",
        "phytokin", "phyto", "plantskin", "woodskin", "barkskin", "leafskin"
    };

    private static readonly Dictionary<string, bool> TextureExistsCache = new(StringComparer.Ordinal);

    internal static string ResolveTexturePath(Pawn pawn, AnatomyPartDef part, bool isAroused)
    {
        AnatomyTextureHeuristicResult result = Evaluate(pawn, part);
        if (result.Family == PenisTextureFamily.Human)
            return null;

        ResolveFamilyTexturePair(result.Family, out string neutral, out string aroused);
        return ChooseTexture(isAroused, neutral, aroused);
    }

    internal static AnatomyTextureHeuristicResult Evaluate(Pawn pawn, AnatomyPartDef part)
    {
        AnatomyTextureHeuristicResult result = new AnatomyTextureHeuristicResult();
        if (pawn == null || part != LovinModule_GenitalDefOf.Genital_Penis)
            return result;

        if (pawn.RaceProps?.Humanlike != true)
        {
            result.Reason = "Pawn is not humanlike.";
            return result;
        }

        if (pawn.RaceProps.IsFlesh == false)
        {
            result.Family = PenisTextureFamily.Xeno;
            result.XenoScore = 1000;
            result.Hits.Add("RaceProps.IsFlesh=false => Xeno");
            ResolveFamilyTexturePair(result.Family, out result.NeutralTexturePath, out result.ArousedTexturePath);
            return result;
        }

        ScoreThingDef(pawn.def, 25, "race", result);
        ScorePawnKind(pawn.kindDef, 12, result);
        ScoreXenotype(pawn.genes?.Xenotype, 35, result);

        List<Gene> genes = pawn.genes?.GenesListForReading;
        if (genes != null)
        {
            for (int i = 0; i < genes.Count; i++)
            {
                Gene gene = genes[i];
                if (gene?.def == null || !gene.Active)
                    continue;

                ScoreText(gene.def.defName, 50, "gene.defName", result);
                ScoreText(gene.def.label, 20, "gene.label", result);
            }
        }

        int bestScore = Math.Max(result.SheathedScore, Math.Max(result.ReptileScore, Math.Max(result.XenoScore, result.InsectScore)));
        if (bestScore < 25)
        {
            result.Reason = "No heuristic family met the confidence threshold.";
            return result;
        }

        if (result.InsectScore >= result.XenoScore && result.InsectScore >= result.ReptileScore && result.InsectScore >= result.SheathedScore)
            result.Family = PenisTextureFamily.Insect;
        else if (result.XenoScore >= result.ReptileScore && result.XenoScore >= result.SheathedScore)
            result.Family = PenisTextureFamily.Xeno;
        else if (result.ReptileScore >= result.SheathedScore)
            result.Family = PenisTextureFamily.Reptile;
        else
            result.Family = PenisTextureFamily.Sheathed;

        ResolveFamilyTexturePair(result.Family, out result.NeutralTexturePath, out result.ArousedTexturePath);
        return result;
    }

    private static void ScoreThingDef(ThingDef def, int weight, string source, AnatomyTextureHeuristicResult result)
    {
        if (def == null)
            return;

        ScoreText(def.defName, weight, source + ".defName", result);
        ScoreText(def.label, Math.Max(1, weight / 2), source + ".label", result);
    }

    private static void ScorePawnKind(PawnKindDef def, int weight, AnatomyTextureHeuristicResult result)
    {
        if (def == null)
            return;

        ScoreText(def.defName, weight, "pawnKind.defName", result);
        ScoreText(def.label, Math.Max(1, weight / 2), "pawnKind.label", result);
    }

    private static void ScoreXenotype(XenotypeDef def, int weight, AnatomyTextureHeuristicResult result)
    {
        if (def == null)
            return;

        ScoreText(def.defName, weight, "xenotype.defName", result);
        ScoreText(def.label, Math.Max(1, weight / 2), "xenotype.label", result);
    }

    private static void ScoreText(string raw, int weight, string source, AnatomyTextureHeuristicResult result)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return;

        string normalized = Normalize(raw);
        if (ExactFamilyMatches.TryGetValue(raw.Trim(), out PenisTextureFamily exactFamily))
        {
            AddScore(exactFamily, Math.Max(100, weight * 3), source, raw.Trim(), result, exact: true);
            return;
        }

        if (ContainsAny(normalized, InsectTokens))
            AddScore(PenisTextureFamily.Insect, weight, source, raw, result);

        if (ContainsAny(normalized, XenoTokens))
            AddScore(PenisTextureFamily.Xeno, weight, source, raw, result);

        if (ContainsAny(normalized, ReptileTokens))
            AddScore(PenisTextureFamily.Reptile, weight, source, raw, result);

        if (ContainsAny(normalized, SheathedTokens))
            AddScore(PenisTextureFamily.Sheathed, weight, source, raw, result);
    }

    private static void AddScore(PenisTextureFamily family, int weight, string source, string raw, AnatomyTextureHeuristicResult result, bool exact = false)
    {
        switch (family)
        {
            case PenisTextureFamily.Sheathed:
                result.SheathedScore += weight;
                break;
            case PenisTextureFamily.Reptile:
                result.ReptileScore += weight;
                break;
            case PenisTextureFamily.Insect:
                result.InsectScore += weight;
                break;
            case PenisTextureFamily.Xeno:
                result.XenoScore += weight;
                break;
        }

        string mode = exact ? "exact" : "token";
        result.Hits.Add($"{source}:{raw} => {family} (+{weight}, {mode})");
    }

    private static void ResolveFamilyTexturePair(PenisTextureFamily family, out string neutral, out string aroused)
    {
        neutral = null;
        aroused = null;

        switch (family)
        {
            case PenisTextureFamily.Sheathed:
                ResolveTexturePair("Anatomy/Penis/sheathed/flaccid", "Anatomy/Penis/sheathed/erect", out neutral, out aroused);
                break;
            case PenisTextureFamily.Reptile:
                ResolveTexturePair("Anatomy/Penis/reptile/flaccid", "Anatomy/Penis/reptile/erect", out neutral, out aroused);
                break;
            case PenisTextureFamily.Insect:
                ResolveTexturePair("Anatomy/Penis/insect/flaccid", "Anatomy/Penis/insect/erect", out neutral, out aroused);
                break;
            case PenisTextureFamily.Xeno:
                ResolveTexturePair("Anatomy/Penis/xeno/flaccid", "Anatomy/Penis/xeno/erect", out neutral, out aroused);
                break;
        }
    }

    private static void ResolveTexturePair(string neutralCandidate, string arousedCandidate, out string neutral, out string aroused)
    {
        bool hasNeutral = HasTexturePath(neutralCandidate);
        bool hasAroused = HasTexturePath(arousedCandidate);

        neutral = hasNeutral ? neutralCandidate : (hasAroused ? arousedCandidate : null);
        aroused = hasAroused ? arousedCandidate : (hasNeutral ? neutralCandidate : null);
    }

    internal static bool HasTexturePath(string path)
    {
        if (path.NullOrEmpty())
            return false;

        if (TextureExistsCache.TryGetValue(path, out bool cached))
            return cached;

        bool exists = ContentFinder<Texture2D>.Get(path, false) != null
            || ContentFinder<Texture2D>.Get(path + "_south", false) != null
            || ContentFinder<Texture2D>.Get(path + "_east", false) != null
            || ContentFinder<Texture2D>.Get(path + "_north", false) != null
            || ContentFinder<Texture2D>.Get(path + "_west", false) != null;
        TextureExistsCache[path] = exists;
        return exists;
    }

    private static string Normalize(string value)
    {
        return value.Trim().ToLowerInvariant().Replace("_", " ").Replace("-", " ");
    }

    private static bool ContainsAny(string text, string[] tokens)
    {
        for (int i = 0; i < tokens.Length; i++)
        {
            if (text.Contains(tokens[i]))
                return true;
        }

        return false;
    }

    private static string ChooseTexture(bool isAroused, string neutral, string aroused)
    {
        if (isAroused)
            return !aroused.NullOrEmpty() ? aroused : neutral;

        return !neutral.NullOrEmpty() ? neutral : aroused;
    }
}

internal sealed class AnatomyTextureHeuristicResult
{
    internal PenisTextureFamily Family;
    internal int SheathedScore;
    internal int ReptileScore;
    internal int InsectScore;
    internal int XenoScore;
    internal string NeutralTexturePath;
    internal string ArousedTexturePath;
    internal string Reason;
    internal List<string> Hits = new();
}

internal enum PenisTextureFamily
{
    Human = 0,
    Sheathed = 1,
    Reptile = 2,
    Insect = 3,
    Xeno = 4,
}
