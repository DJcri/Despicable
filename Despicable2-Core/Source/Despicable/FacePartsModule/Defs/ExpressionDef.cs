using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Despicable;
public class ExpressionDef : Def, IExposable
{
    public string texPathEyes;
    public string texPathBrows;
    public string browVariant;
    public string texPathMouth;
    public string texPathDetail;
    public string texPathEyeDetailState;
    public string texPathFaceDetailState;
    public Vector3? eyesOffset;
    public Vector3? browsOffset;
    public Vector3? mouthOffset;
    public Vector3? detailOffset;
    public Vector3? eyeDetailOffset;
    public Vector3? faceDetailOffset;

    public void ExposeData()
    {
        Scribe_Values.Look(ref texPathEyes, "texPathEyes");
        Scribe_Values.Look(ref texPathBrows, "texPathBrows");
        Scribe_Values.Look(ref browVariant, "browVariant");
        Scribe_Values.Look(ref texPathMouth, "texPathMouth");
        Scribe_Values.Look(ref texPathDetail, "texPathDetail");
        Scribe_Values.Look(ref texPathEyeDetailState, "texPathEyeDetailState");
        Scribe_Values.Look(ref texPathFaceDetailState, "texPathFaceDetailState");
        Scribe_Values.Look(ref eyesOffset, "eyesOffset");
        Scribe_Values.Look(ref browsOffset, "browsOffset");
        Scribe_Values.Look(ref mouthOffset, "mouthOffset");
        Scribe_Values.Look(ref detailOffset, "detailOffset");
        Scribe_Values.Look(ref eyeDetailOffset, "eyeDetailOffset");
        Scribe_Values.Look(ref faceDetailOffset, "faceDetailOffset");
    }

    public Vector3? getOffset(string facePartLabel)
    {
        Vector3? offset = null;

        switch (facePartLabel)
        {
            case "FacePart_Eye_L":
            case "FacePart_Eye_R":
                offset = eyesOffset;
                break;
            case "FacePart_Brow_L":
            case "FacePart_Brow_R":
                offset = browsOffset ?? eyesOffset;
                break;
            case "FacePart_Mouth":
            case "FacePart_Mouth_L":
            case "FacePart_Mouth_R":
                offset = mouthOffset;
                break;
            case "FacePart_FaceDetail":
                offset = faceDetailOffset ?? mouthOffset;
                break;
            case "FacePart_EyeDetail_L":
            case "FacePart_EyeDetail_R":
            case "FacePart_SecondaryDetail_L":
            case "FacePart_SecondaryDetail_R":
                offset = eyeDetailOffset ?? detailOffset;
                break;
        }

        return offset;
    }
}
