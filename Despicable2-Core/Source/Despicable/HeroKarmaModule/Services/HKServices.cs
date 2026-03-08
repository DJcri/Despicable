namespace Despicable.HeroKarma;
/// <summary>
/// Simple indirection so UI can compile and run before the full backend is wired in this branch/zip.
/// Replace these with your real singletons/services when integrating.
///
/// The mutable service instances now live behind a small registry object so we
/// do not expose raw writable static fields to the rest of the codebase.
/// </summary>
public static class HKServices
{
    private static readonly ServiceRegistry Registry = new();

    public static ILocalReputationService LocalRep => Registry.LocalRep;
    public static IEventDisplayCatalog EventCatalog => Registry.EventCatalog;

    public static void RegisterLocalRep(ILocalReputationService service)
    {
        Registry.RegisterLocalRep(service);
    }

    public static void RegisterEventCatalog(IEventDisplayCatalog catalog)
    {
        Registry.RegisterEventCatalog(catalog);
    }

    private sealed class ServiceRegistry
    {
        private ILocalReputationService _localRep = new LocalReputationService();
        private IEventDisplayCatalog _eventCatalog = new DefaultEventDisplayCatalog();

        internal ILocalReputationService LocalRep => _localRep;
        internal IEventDisplayCatalog EventCatalog => _eventCatalog;

        internal void RegisterLocalRep(ILocalReputationService service)
        {
            if (service != null)
                _localRep = service;
        }

        internal void RegisterEventCatalog(IEventDisplayCatalog catalog)
        {
            if (catalog != null)
                _eventCatalog = catalog;
        }
    }
}
