using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Despicable;
public class AnimRoleDef : Def
{
    public List<AnimationDef> anims;
    public AnimationOffsetDef offsetDef;
    public int gender;
}
