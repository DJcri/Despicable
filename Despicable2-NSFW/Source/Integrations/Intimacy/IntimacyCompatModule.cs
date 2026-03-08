using Despicable.Core.Compatibility;
using Despicable.NSFW.Integrations;

namespace Despicable.NSFW.Integrations.Intimacy;
/// <summary>
/// Primes reflection caches for the Intimacy integration so runtime hooks fail fast and log clearly.
/// </summary>
internal sealed class IntimacyCompatModule : IModCompat
{
    public string Id
    {
        get { return "Intimacy"; }
    }

    public bool CanActivate()
    {
        return IntegrationGuards.IsIntimacyLoaded();
    }

    public void Activate()
    {
        IntimacyReflectionUtil.PrimeCache();
        IntimacyApplyUtil.PrimeCache();
    }

    public string ReportStatus()
    {
        return "reflection caches primed";
    }
}
