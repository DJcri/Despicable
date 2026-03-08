using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Despicable;
public class PawnRenderNode_Genitals : PawnRenderNode
{
    public PawnRenderNode_Genitals(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree) : base(pawn, props, tree)
    {
    }

    public override GraphicMeshSet MeshSetFor(Pawn pawn)
    {
        return HumanlikeMeshPoolUtility.GetHumanlikeBodySetForPawn(pawn);
    }

    public override Verse.Graphic GraphicFor(Pawn pawn)
    {
        if (pawn.Drawer.renderer.CurRotDrawMode == RotDrawMode.Dessicated)
        {
            return null;
        }

        return GraphicDatabase.Get<Graphic_Multi>(this.TexPathFor(pawn), ShaderDatabase.CutoutSkin, Vector2.one, ColorFor(pawn));
    }
}
