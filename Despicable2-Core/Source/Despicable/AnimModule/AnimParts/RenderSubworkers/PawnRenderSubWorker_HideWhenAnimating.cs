using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Despicable;
public class PawnRenderSubWorker_HideWhenAnimating : PawnRenderSubWorker
{
    public override void EditMaterial(PawnRenderNode node, PawnDrawParms parms, ref Material material)
    {
        if (node.tree.pawn.def != ThingDefOf.Human && node.tree.pawn.def != ThingDefOf.CreepJoiner) return;

        if (node.tree.rootNode.AnimationWorker is AnimationWorker_ExtendedKeyframes
            || node.tree.rootNode.children.Any(x => x.AnimationWorker is AnimationWorker_ExtendedKeyframes))
        {
            material.color = Color.clear;
            material.shader = ShaderTypeDefOf.Transparent.Shader;
        }
    }

    public override void TransformLayer(PawnRenderNode node, PawnDrawParms parms, ref float layer)
    {
        base.TransformLayer(node, parms, ref layer);

        if (node.tree.pawn.def != ThingDefOf.Human && node.tree.pawn.def != ThingDefOf.CreepJoiner) return;
        layer -= 1000;
    }
}
