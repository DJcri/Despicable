using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Despicable;
public class PawnRenderNodeProperties_GraphicVariants : PawnRenderNodeProperties
{
    public AnimationOffsetDef propOffsetDef = null;
    public TexPathVariantsDef texPathVariantsDef = null;
    public bool absoluteTransform = false;
}
