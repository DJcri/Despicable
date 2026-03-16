using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Despicable;
public class PawnRenderNode_Mouth : PawnRenderNode
{
    CompFaceParts compFaceParts;

    public PawnRenderNode_Mouth(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree) : base(pawn, props, tree)
    {
        compFaceParts = pawn.TryGetComp<CompFaceParts>();
    }

    public override GraphicMeshSet MeshSetFor(Pawn pawn)
    {
        return HumanlikeMeshPoolUtility.GetHumanlikeHeadSetForPawn(pawn);
    }

    public override Verse.Graphic GraphicFor(Pawn pawn)
    {
        if (compFaceParts?.IsRenderActiveNow() != true)
        {
            return null;
        }

        if (pawn?.health?.hediffSet == null)
        {
            return null;
        }
        if (!pawn.health.hediffSet.HasHead)
        {
            return null;
        }
        if (pawn?.Drawer?.renderer == null)
        {
            return null;
        }
        if (pawn.Drawer.renderer.CurRotDrawMode == RotDrawMode.Dessicated)
        {
            return null;
        }

        string texPath = this.props.texPath;

        if (compFaceParts != null)
        {
            ExpressionDef baseExpression = compFaceParts?.baseExpression;
            ExpressionDef animExpression = compFaceParts?.animExpression;

            if (!(animExpression?.texPathMouth).NullOrEmpty())
                texPath = animExpression.texPathMouth;
            else if (!(baseExpression?.texPathMouth).NullOrEmpty())
                texPath = baseExpression.texPathMouth;
            else if (compFaceParts.mouthStyleDef != null && !compFaceParts.mouthStyleDef.texPath.NullOrEmpty())
                texPath = compFaceParts.mouthStyleDef.texPath;
        }

        if (texPath.NullOrEmpty())
            texPath = "FaceParts/Details/detail_empty";

        return GraphicDatabase.Get<Graphic_Multi>(texPath, ShaderDatabase.Cutout, Vector2.one, ColorFor(pawn));
    }
}
