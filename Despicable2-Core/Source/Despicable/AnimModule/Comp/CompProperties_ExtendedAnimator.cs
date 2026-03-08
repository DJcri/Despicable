using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Despicable;
public class CompProperties_ExtendedAnimator : CompProperties
{
    public CompProperties_ExtendedAnimator()
    {
        base.compClass = typeof(CompExtendedAnimator);
    }
}
