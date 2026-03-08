using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Despicable;
public class PawnRenderNode_BodyTypeVariants : PawnRenderNode_GraphicVariants
{

    private BodyTypeDef bodyType;
    protected new PawnRenderNodeProperties_BodyTypeVariants props;

    public PawnRenderNode_BodyTypeVariants(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree) : base(pawn, props, tree)
    {

        this.props = (PawnRenderNodeProperties_BodyTypeVariants)props;

    }

    protected Dictionary<int, Graphic> GraphicBodyTypeVariantsFor(Pawn pawn)
    {

        if (props.bodyTypeVariantsDef == null)
        {
            Log.Error("[Anims] Error: Tried to use BodyTypeVariants node, but bodyTypeVariants weren't given");
            return null;
        }

        //for each different hediff-based texpathvariants,
        foreach (TexPathVariants_BodyType texPathVariant_BodyType in props.bodyTypeVariantsDef)
        {
            if (pawn.story?.bodyType == texPathVariant_BodyType.bodyType)
            {
                //return that specific variant
                bodyType = texPathVariant_BodyType.bodyType;
                return GenerateVariants(pawn, texPathVariant_BodyType.texPathVariantsDef);

            }

        }

        return null;

    }

    protected override void EnsureMaterialVariantsInitialized(Graphic g)
    {
        if (variants == null 
            || this.tree.pawn.story?.bodyType != bodyType)
        variants = GraphicBodyTypeVariantsFor(this.tree.pawn);

        //call this in case variants wasn't set, and there is no graphic bodytype variants appropriate; it'll set variants based on default
        base.EnsureMaterialVariantsInitialized(g);
    }


}

