using Verse;

namespace Despicable.NSFW;
/// <summary>
/// Assembly-load entrypoint for RimWorld 1.6 (About.xml no longer supports <modClass>).
/// Ensures NSFW Harmony patches + hook registrations are initialized.
/// </summary>
[StaticConstructorOnStartup]
public static class DespicableNSFW_HookBootstrap
{
    static DespicableNSFW_HookBootstrap()
    {
        // This is idempotent.
        ModMain.EnsureInitialized();
    }
}
