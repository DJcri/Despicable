using UnityEngine;
using Verse;

namespace Despicable.Core.Staging;
public class StageOffset : IExposable
{
    public float x;
    public float y;
    public float z;

    public Vector3 ToVector3() => new Vector3(x, y, z);

    public void ExposeData()
    {
        Scribe_Values.Look(ref x, "x", 0f);
        Scribe_Values.Look(ref y, "y", 0f);
        Scribe_Values.Look(ref z, "z", 0f);
    }
}
