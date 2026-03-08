using Despicable.Core.Compatibility;
using Verse;

namespace Despicable.FacePartsModule.Compatibility.PawnEditorCompat;
[StaticConstructorOnStartup]
internal static class PawnEditorCompatBootstrap
{
    private static readonly PawnEditorCompatModule Module = new();

    static PawnEditorCompatBootstrap()
    {
        LongEventHandler.ExecuteWhenFinished(ApplyDeferredRegistration);
    }

    private static void ApplyDeferredRegistration()
    {
        ModCompatRegistry.EnsureRegistered(Module, "[Despicable2.Core]");
    }
}
