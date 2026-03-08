using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Despicable;
public class PawnRenderNodeProperties_GraphicHediffSeverityVariants : PawnRenderNodeProperties_GraphicVariants
{

    public BodyPartDef bodyPart = null;
    public List<HediffWithSeverity> hediffSeverityVariants;

}

public class HediffWithSeverity
{
    public HediffDef hediff;
    public List<TexPathVariants_Severity> severityVariants;
}

public class TexPathVariants_Severity
{
    public int severity;
    public TexPathVariantsDef texPathVariantsDef;
    

}


