using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Despicable;
public class PawnRenderNode_GraphicHediffSeverityVariants : PawnRenderNode_GraphicVariants
{

    protected HediffDef hediffWithSeverity;
    protected float curSeverity;

    protected new PawnRenderNodeProperties_GraphicHediffSeverityVariants props;
    private HediffDef curHediff;

    public PawnRenderNode_GraphicHediffSeverityVariants(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree) : base(pawn, props, tree)
    {

        this.props = (PawnRenderNodeProperties_GraphicHediffSeverityVariants)props;

    }

    protected Dictionary<int, Graphic> GraphicHediffSeverityVariantsFor(Pawn pawn)
    {


        if (props.hediffSeverityVariants == null)
        {
            Log.Error("[Anims] Error: Tried to use GraphicBodyPartHediffSeverityVariants node, but hediffSeverityVariants weren't given");
        }


        Hediff idealHediff = null;
        HediffWithSeverity idealHediffSeverity = null;

        if (props.bodyPart == null)
        {
            //search the entire body for the hediff because no bodypart was set
            for (int i = 0; i < props.hediffSeverityVariants.Count; i++)
            {
                idealHediff = pawn.health.hediffSet.hediffs.Find((Hediff hediffWithSeverity) =>
                    hediffWithSeverity.def == props.hediffSeverityVariants[i].hediff);

                if (idealHediff != null)
                {
                    //get the ideal hediff severity variants, to iterate through and find the right one for the severity
                    idealHediffSeverity = props.hediffSeverityVariants[i];
                    break;

                }

            }
        }

        else
        {
            //search for a hediff with a specific body part 

            for (int i = 0; i < props.hediffSeverityVariants.Count; i++)
            {
                //right hediff with the right hediff and right body part

                idealHediff = pawn.health.hediffSet.hediffs.Find((Hediff hediffWithSeverity) =>
                    hediffWithSeverity.def == props.hediffSeverityVariants[i].hediff
                    && hediffWithSeverity.Part.def == props.bodyPart);

                if (idealHediff != null) {

                    //get the ideal hediff severity variants, to iterate through and find the right one for the severity
                    idealHediffSeverity = props.hediffSeverityVariants[i];
                    break;
                }
            }
        }

        if (idealHediff != null)
        {
            //set severity so that recache when this is different
            curSeverity = idealHediff.Severity;

            //look for the right severity-based texpathvariants
            TexPathVariants_Severity texPathVariants_Severity = idealHediffSeverity.severityVariants.Find((TexPathVariants_Severity texPathVariants) =>
                texPathVariants.severity < idealHediff.Severity);

            //if null, assume value is really too small
            if (texPathVariants_Severity == null)
            {
                //return largest value
                return GenerateVariants(pawn, idealHediffSeverity.severityVariants.First().texPathVariantsDef);
            }

            //return right severity variants
            return GenerateVariants(pawn, texPathVariants_Severity.texPathVariantsDef);

        }

        //there is no graphic hediff variants appropriate
        curHediff = null;
        return null;

    }

    protected override void EnsureMaterialVariantsInitialized(Graphic g)
    {
        //if pawn no longer has the hediff,
        if (variants == null ||
            !(this.tree.pawn.health?.hediffSet?.hediffs is List<Hediff> hediffs 
            && hediffs.Any((Hediff hediff) => hediff.def == curHediff && hediff.Severity == curSeverity)))
        {
            //do graphicvariantsfor
            variants = GraphicHediffSeverityVariantsFor(this.tree.pawn);
        }

        //call this in case variants wasn't set, and there is no graphic hediff variants appropriate; it'll set variants based on default
        base.EnsureMaterialVariantsInitialized(g);
    }
}
