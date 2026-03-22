using System;
using System.Collections.Generic;
using Verse;

namespace Despicable;

public enum FacePartSideMode
{
    Both = 0,
    LeftOnly = 1,
    RightOnly = 2,
}

public class FacePartStyleDef : Def
{
    public byte? requiredGender = null;
    public List<string> requiredGenes = null;
    public PawnRenderNodeTagDef renderNodeTag;
    public string texPath;
    public int weight = 1;
    public FacePartSideMode sideMode = FacePartSideMode.Both;
    public bool allowSideSelection = false;

    public FacePartSideMode ResolveEffectiveSideMode(FacePartSideMode selectedSideMode = FacePartSideMode.Both)
    {
        if (!allowSideSelection)
            return sideMode;

        return selectedSideMode == FacePartSideMode.RightOnly
            ? FacePartSideMode.RightOnly
            : FacePartSideMode.LeftOnly;
    }

    public bool AllowsSide(bool isRightSide, FacePartSideMode selectedSideMode = FacePartSideMode.Both)
    {
        return ResolveEffectiveSideMode(selectedSideMode) switch
        {
            FacePartSideMode.LeftOnly => !isRightSide,
            FacePartSideMode.RightOnly => isRightSide,
            _ => true,
        };
    }
}
