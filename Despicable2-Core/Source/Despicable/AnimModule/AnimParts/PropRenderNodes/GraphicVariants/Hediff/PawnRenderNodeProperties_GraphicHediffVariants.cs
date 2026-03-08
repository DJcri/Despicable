using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Despicable;
public class PawnRenderNodeProperties_GraphicHediffVariants : PawnRenderNodeProperties_GraphicVariants
{

    public List<TexPathVariants_Hediff> hediffVariants;

}

public class TexPathVariants_Hediff
{

    public List<HediffDef> hediffs;
    public TexPathVariantsDef texPathVariantsDef;

}


