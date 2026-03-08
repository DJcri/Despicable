using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Despicable;
public class FacePartStyleDef : Def
{
    public byte? requiredGender = null;
    public PawnRenderNodeTagDef renderNodeTag;
    public string texPath;
    public int weight = 1;
}
