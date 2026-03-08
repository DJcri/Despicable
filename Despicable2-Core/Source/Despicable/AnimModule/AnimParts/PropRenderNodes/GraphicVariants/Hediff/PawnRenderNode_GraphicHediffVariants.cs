using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Despicable;
public class PawnRenderNode_GraphicHediffVariants : PawnRenderNode_GraphicVariants
{

    protected new PawnRenderNodeProperties_GraphicHediffVariants props;
    private HediffDef curHediff;

    public PawnRenderNode_GraphicHediffVariants(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree) : base(pawn, props, tree)
    {

        this.props = (PawnRenderNodeProperties_GraphicHediffVariants)props;

    }

    protected Dictionary<int, Graphic> GraphicHediffVariantsFor(Pawn pawn)
    {

        if (props.hediffVariants == null)
        {
            Log.Error("[Anims] Error: Tried to use GraphicHediffVariants node, but hediffVariants weren't given");
            return null;
        }

        //for each different hediff-based texpathvariants,
        foreach (TexPathVariants_Hediff texPathVariant_Hediff in props.hediffVariants)
        {
            //for all the hediffs corresponding to that texpathvariant,
            foreach (HediffDef hediffDef in texPathVariant_Hediff.hediffs)
            {
                //if the pawn has that hediff,
                if (pawn?.health?.hediffSet?.hediffs is List<Hediff> pawnHediffs && pawnHediffs.Any((Hediff hediff) => hediff.def == hediffDef))
                {
                    //return that specific variant
                    curHediff = hediffDef;
                    return GenerateVariants(pawn, texPathVariant_Hediff.texPathVariantsDef);

                }
            }
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
            && hediffs.Any((Hediff hediff) => hediff.def == curHediff)))
        {
            //do graphicvariantsfor
            variants = GraphicHediffVariantsFor(this.tree.pawn);
        }
        //call this in case variants wasn't set, and there is no graphic hediff variants appropriate; it'll set variants based on default
        base.EnsureMaterialVariantsInitialized(g);
    }
}
