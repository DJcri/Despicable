using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Despicable;
public class PawnRenderNodeProperties_BodyTypeVariants : PawnRenderNodeProperties_GraphicVariants
{

    public List<TexPathVariants_BodyType> bodyTypeVariantsDef;

}

public class TexPathVariants_BodyType
{

    public BodyTypeDef bodyType;
    public TexPathVariantsDef texPathVariantsDef;

}


