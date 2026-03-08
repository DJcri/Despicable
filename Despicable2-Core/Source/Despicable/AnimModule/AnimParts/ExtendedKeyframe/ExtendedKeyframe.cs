using Verse;

namespace Despicable;
public class ExtendedKeyframe : Keyframe
{
    public int? variant;
    public new string graphicState;
    public Rot4 rotation = Rot4.South;
    public SoundDef sound = null;
    public bool visible = false;
    public FacialAnimDef facialAnim = null;
    public int layerBias = 0;
}
