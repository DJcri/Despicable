using Verse;

namespace Despicable.NSFW;
/// <summary>
/// RimWorld 1.6 no longer supports <modClass> in About.xml. This bootstrap ensures
/// NSFW Harmony patches and hook registrations are initialized when the assembly loads.
/// </summary>
[StaticConstructorOnStartup]
public static class DespicableNSFWBootstrap
{
    static DespicableNSFWBootstrap()
    {
        ModMain.EnsureInitialized();
    }
}
