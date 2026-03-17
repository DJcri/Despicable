using Despicable.Core.Compatibility;
using Verse;

namespace Despicable.Core.Compatibility.PerspectiveShiftCompat;
[StaticConstructorOnStartup]
internal static class PerspectiveShiftCompatBootstrap
{
    private static readonly PerspectiveShiftCompatModule Module = new();

    static PerspectiveShiftCompatBootstrap()
    {
        ApplyRegistration();
        LongEventHandler.ExecuteWhenFinished(ApplyRegistration);
    }

    private static void ApplyRegistration()
    {
        ModCompatRegistry.EnsureRegistered(Module, "[Despicable2.Core]");
    }
}
