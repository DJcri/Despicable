using RimWorld;
using Verse;

namespace Despicable;
public class AnatomySlotDef : Def
{
    public string anchorKey;
    public PawnRenderNodeTagDef parentTagDef;
    public string anchorTag = "Body";
    public bool renderable = true;
}
