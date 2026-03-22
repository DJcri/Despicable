using System.Collections.Generic;
using System.Text;
using RimWorld;
using Verse;

namespace Despicable;
internal static class AnatomyDebugReportBuilder
{
    internal static string Build(Pawn pawn, CompAnatomyBootstrap tracker)
    {
        StringBuilder sb = new StringBuilder(2048);
        sb.AppendLine("Despicable NSFW Anatomy Debug");
        sb.AppendLine();

        if (pawn == null)
        {
            sb.AppendLine("No pawn selected.");
            return sb.ToString();
        }

        AppendPawnSummary(sb, pawn, tracker);
        sb.AppendLine();
        AppendCoverageSummary(sb, pawn);
        sb.AppendLine();
        AppendPartSummary(sb, pawn, tracker);
        return sb.ToString().TrimEnd();
    }

    private static void AppendPawnSummary(StringBuilder sb, Pawn pawn, CompAnatomyBootstrap tracker)
    {
        sb.AppendLine($"Pawn: {pawn.LabelCap}");
        sb.AppendLine($"Def: {pawn.def?.defName ?? "<none>"}");
        sb.AppendLine($"Kind: {pawn.kindDef?.defName ?? "<none>"}");
        sb.AppendLine($"Gender: {pawn.gender}");
        sb.AppendLine($"Humanlike: {pawn.RaceProps?.Humanlike == true}");
        sb.AppendLine($"Adult: {pawn.ageTracker?.Adult == true}");
        sb.AppendLine($"BodyType: {pawn.story?.bodyType?.defName ?? "<none>"}");
        sb.AppendLine($"LifeStage: {pawn.ageTracker?.CurLifeStage?.defName ?? "<none>"}");
        sb.AppendLine($"Tracker resolved: {tracker?.HasResolvedAnatomy == true}");
        sb.AppendLine($"Logical anatomy known: {AnatomyQuery.TryGetLogicalAnatomy(pawn, out bool hasPenis, out bool hasVagina)}");
        sb.AppendLine($"Has penis: {hasPenis}");
        sb.AppendLine($"Has vagina: {hasVagina}");

        if (pawn.genes?.GenesListForReading != null && pawn.genes.GenesListForReading.Count > 0)
        {
            List<string> geneNames = new List<string>(pawn.genes.GenesListForReading.Count);
            for (int i = 0; i < pawn.genes.GenesListForReading.Count; i++)
            {
                Gene gene = pawn.genes.GenesListForReading[i];
                if (gene?.def == null)
                    continue;

                geneNames.Add(gene.def.defName);
            }

            geneNames.Sort();
            sb.AppendLine($"Genes: {(geneNames.Count > 0 ? string.Join(", ", geneNames) : "<none>")}");
        }
        else
        {
            sb.AppendLine("Genes: <none>");
        }
    }

    private static void AppendCoverageSummary(StringBuilder sb, Pawn pawn)
    {
        GetBasicApparelCoverage(pawn, out bool hasPants, out bool hasShirt);
        sb.AppendLine("Coverage");
        sb.AppendLine($"  Pants coverage: {hasPants}");
        sb.AppendLine($"  Shirt coverage: {hasShirt}");
    }

    private static void AppendPartSummary(StringBuilder sb, Pawn pawn, CompAnatomyBootstrap tracker)
    {
        if (tracker == null)
        {
            sb.AppendLine("Parts");
            sb.AppendLine("  Anatomy tracker comp is missing.");
            return;
        }

        bool gotInstances = tracker.TryGetResolvedPartInstances(out List<AnatomyPartInstance> instances);
        sb.AppendLine("Parts");
        sb.AppendLine($"  Query success: {gotInstances}");
        if (!gotInstances || instances == null || instances.Count == 0)
        {
            sb.AppendLine("  <none>");
            return;
        }

        GetBasicApparelCoverage(pawn, out bool hasPants, out bool hasShirt);
        for (int i = 0; i < instances.Count; i++)
        {
            AnatomyPartInstance instance = instances[i];
            AnatomyPartDef part = instance?.partDef;
            if (part == null)
                continue;

            sb.AppendLine($"  [{i + 1}] {part.defName}");
            sb.AppendLine($"    Slot: {part.slot?.defName ?? "<none>"}");
            sb.AppendLine($"    Installed variant: {instance.installedVariant?.defName ?? "<natural/base>"}");
            sb.AppendLine($"    Anchor key: {part.slot?.anchorKey ?? "<none>"}");
            sb.AppendLine($"    Anchor tag: {part.slot?.anchorTag ?? "<none>"}");
            sb.AppendLine($"    Parent tag: {part.slot?.parentTagDef?.defName ?? "<none>"}");
            sb.AppendLine($"    Renderable slot: {part.slot?.renderable != false}");
            sb.AppendLine($"    Visible by default: {part.visibleByDefault}");
            sb.AppendLine($"    Show while animating: {part.showWhileAnimating}");
            sb.AppendLine($"    Show outside animation: {part.showOutsideAnimation}");
            sb.AppendLine($"    Covered by apparel now: {IsCoveredByApparel(part, hasPants, hasShirt)}");
            sb.AppendLine($"    Resolved size: {instance.size:0.###}");
            sb.AppendLine($"    Tags: {FormatList(part.tags)}");
            sb.AppendLine($"    Capabilities: {FormatList(part.capabilities)}");

            bool foundAnchor = AnatomyQuery.TryGetAnchor(pawn, part.slot, out BodyPartRecord anchor);
            sb.AppendLine($"    Anchor resolved: {foundAnchor} {(foundAnchor ? $"({anchor.def?.defName ?? "<unknown>"})" : string.Empty)}");

            string neutralTex = AnatomyAppearanceResolver.ResolveTexturePath(pawn, part, isAroused: false);
            string arousedTex = AnatomyAppearanceResolver.ResolveTexturePath(pawn, part, isAroused: true);
            sb.AppendLine($"    Neutral texture: {neutralTex ?? "<none>"}");
            sb.AppendLine($"    Aroused texture: {arousedTex ?? "<none>"}");

            if (part == LovinModule_GenitalDefOf.Genital_Penis)
            {
                AnatomyTextureHeuristicResult heuristic = AnatomyTextureHeuristicResolver.Evaluate(pawn, part);
                sb.AppendLine($"    Heuristic family: {heuristic.Family}");
                sb.AppendLine($"    Heuristic scores: sheathed={heuristic.SheathedScore}, reptile={heuristic.ReptileScore}, insect={heuristic.InsectScore}, xeno={heuristic.XenoScore}");
                sb.AppendLine($"    Heuristic neutral texture: {heuristic.NeutralTexturePath ?? "<none>"}");
                sb.AppendLine($"    Heuristic aroused texture: {heuristic.ArousedTexturePath ?? "<none>"}");
                sb.AppendLine($"    Heuristic reason: {heuristic.Reason ?? "<none>"}");
                sb.AppendLine($"    Heuristic hits: {(heuristic.Hits.Count > 0 ? string.Join(" | ", heuristic.Hits) : "<none>")}");
            }

            if (instance.fluids == null || instance.fluids.Count == 0)
            {
                sb.AppendLine("    Fluids: <none>");
            }
            else
            {
                sb.AppendLine("    Fluids:");
                for (int j = 0; j < instance.fluids.Count; j++)
                {
                    AnatomyFluidInstance fluid = instance.fluids[j];
                    if (fluid?.fluidDef == null)
                        continue;

                    float refillPerDay = AnatomyQuery.GetFluidRefillPerDay(pawn, part, fluid.fluidDef, 0f);
                    sb.AppendLine($"      - {fluid.fluidDef.defName}: amount {fluid.amount:0.###} / capacity {fluid.capacity:0.###} / refillPerDay {refillPerDay:0.###}");
                }
            }
        }
    }

    private static string FormatList(List<string> values)
    {
        if (values == null || values.Count == 0)
            return "<none>";

        List<string> filtered = new List<string>(values.Count);
        for (int i = 0; i < values.Count; i++)
        {
            string value = values[i];
            if (!value.NullOrEmpty())
                filtered.Add(value);
        }

        return filtered.Count > 0 ? string.Join(", ", filtered) : "<none>";
    }

    private static void GetBasicApparelCoverage(Pawn pawn, out bool hasPants, out bool hasShirt)
    {
        hasPants = false;
        hasShirt = false;
        pawn?.apparel?.HasBasicApparel(out hasPants, out hasShirt);
    }

    private static bool IsCoveredByApparel(AnatomyPartDef part, bool hasPants, bool hasShirt)
    {
        string slotDefName = part?.slot?.defName;
        if (slotDefName == "ExternalGenitals")
            return hasPants;

        if (slotDefName == "Chest")
            return hasShirt;

        return false;
    }
}
