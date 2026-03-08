using Despicable.AnimModule.Runtime.Graphics;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Despicable;
public class PawnRenderNode_GraphicVariants : PawnRenderNode
{
    protected new PawnRenderNodeProperties_GraphicVariants props;

    /// <summary>
    /// True when this node came from an AnimationPropDef (runtime injected prop).
    /// Used to apply prop-specific visibility defaults (e.g. hide until a state/variant is driven).
    /// </summary>
    public bool isAnimPropNode;

    /// <summary>
    /// When true and <see cref="isAnimPropNode"/> is true, the prop will not draw until the
    /// current animation explicitly drives a graphic state (or a legacy numeric variant).
    /// This prevents props from appearing at tick 0 due to a default texPath.
    /// </summary>
    public bool hideUntilDrivenState = true;

    protected Graphic missingTextureGraphic;
    protected Dictionary<string, Graphic> stateGraphics;
    // Legacy numeric variants (1-based). Kept for compatibility with older variant-based nodes and defs.
    protected Dictionary<int, Graphic> variants;

    public PawnRenderNode_GraphicVariants(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree) : base(pawn, props, tree)
    {
        this.props = (PawnRenderNodeProperties_GraphicVariants)props;
    }

    public Graphic GetGraphicState(string stateId)
    {
        if (stateGraphics == null || stateGraphics.Count == 0)
            return missingTextureGraphic;

        string resolved = GraphicStateResolver.ResolveStateId(
            nodeDebugId: props?.debugLabel ?? GetType().Name,
            requested: stateId,
            available: stateGraphics.Keys);

        if (resolved != null && stateGraphics.TryGetValue(resolved, out Graphic g))
            return g;

        return missingTextureGraphic;
    }

    // Back-compat (1-based)
    public Graphic getGraphicVariant(int variant) => GetGraphicState("variant_" + variant);

    protected override void EnsureMaterialVariantsInitialized(Graphic g)
    {
        if (stateGraphics == null)
            stateGraphics = GraphicStatesFor(tree.pawn);

        // Keep legacy numeric variants in sync for older nodes that still reference `variants`.
        if (variants == null)
            variants = (props != null && props.texPathVariantsDef != null) ? GenerateVariants(tree.pawn, props.texPathVariantsDef) : null;

        if (missingTextureGraphic == null)
            missingTextureGraphic = GenerateMissingTextureGraphic();

        base.EnsureMaterialVariantsInitialized(g);
    }

    protected virtual Dictionary<string, Graphic> GraphicStatesFor(Pawn pawn)
    {
        if (props.texPathVariantsDef == null || props.texPathVariantsDef.variants.NullOrEmpty())
            return null;

        return GenerateStates(pawn, props.texPathVariantsDef);
    }

    protected Dictionary<string, Graphic> GenerateStates(Pawn pawn, TexPathVariantsDef texPathVariants)
    {
        var dict = new Dictionary<string, Graphic>();

        for (int i = 0; i < texPathVariants.variants.Count; i++)
        {
            string stateId = null;

            if (texPathVariants.stateIds != null && i < texPathVariants.stateIds.Count)
                stateId = texPathVariants.stateIds[i];

            if (stateId.NullOrEmpty())
                stateId = "variant_" + (i + 1);

            Graphic variant = GraphicDatabase.Get<Graphic_Multi>(
                texPathVariants.variants[i],
                ShaderDatabase.CutoutSkinOverlay,
                Vector2.one,
                ColorFor(pawn));

            dict[stateId] = variant;
        }

        return dict;
    }

        
    /// <summary>
    /// Legacy helper: build a 1-based numeric variant map from a TexPathVariantsDef.
    /// Prefer named states via <see cref="TexPathVariantsDef.stateIds"/> when authoring new content.
    /// </summary>
    protected Dictionary<int, Graphic> GenerateVariants(Pawn pawn, TexPathVariantsDef texPathVariants)
    {
        if (texPathVariants == null || texPathVariants.variants.NullOrEmpty())
            return null;

        var dict = new Dictionary<int, Graphic>();

        for (int i = 0; i < texPathVariants.variants.Count; i++)
        {
            string path = texPathVariants.variants[i];
            if (path.NullOrEmpty())
                continue;

            Graphic g = GraphicDatabase.Get<Graphic_Multi>(
                path,
                ShaderFor(pawn),
                Vector2.one,
                ColorFor(pawn));

            dict[i + 1] = g;
        }

        return dict;
    }


    protected Graphic GenerateMissingTextureGraphic()
    {
        return GraphicDatabase.Get<Graphic_Multi>("AnimationProps/MissingTexture/MissingTexture");
    }
}
