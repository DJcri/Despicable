using HarmonyLib;
using Despicable.Core.Compatibility;

namespace Despicable.FacePartsModule.Compatibility.PawnEditorCompat;
/// <summary>
/// Optional Pawn Editor integration. Defers patch application until the external mod is confirmed active.
/// </summary>
internal sealed class PawnEditorCompatModule : IModCompat
{
    public string Id
    {
        get { return "PawnEditor"; }
    }

    public bool CanActivate()
    {
        return Verse.ModsConfig.IsActive("segaswolf.pawneditor.fork");
    }

    public void Activate()
    {
        ModMain.harmony ??= new Harmony(DespicableBootstrap.HarmonyId);
        HarmonyPatch_PawnEditor_AppearanceEditor.Apply(ModMain.harmony);
    }

    public string ReportStatus()
    {
        return "deferred appearance editor patch applied";
    }
}
