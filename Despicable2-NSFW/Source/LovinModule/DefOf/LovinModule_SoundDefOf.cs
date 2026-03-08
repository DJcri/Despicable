using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Despicable;
[DefOf]
public class LovinModule_SoundDefOf
{
    public static SoundDef Cum;
    public static SoundDef Sex;
    public static SoundDef Fuck;
    public static SoundDef Slap;
    public static SoundDef Suck;

    static LovinModule_SoundDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(LovinModule_SoundDefOf));
    }
}
