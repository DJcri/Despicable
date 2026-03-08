using Despicable.Core.Compatibility;
using Despicable.NSFW.Integrations;

namespace Despicable.NSFW.Integrations.GenderWorks;
/// <summary>
/// Lightweight status module for Gender Works integration.
/// There is no eager patch work today, but standardizing the shape keeps startup predictable.
/// </summary>
internal sealed class GenderWorksCompatModule : IModCompat
{
    public string Id
    {
        get { return "GenderWorks"; }
    }

    public bool CanActivate()
    {
        return IntegrationGuards.IsGenderWorksLoaded();
    }

    public void Activate()
    {
        // No eager warmup required at the moment.
    }

    public string ReportStatus()
    {
        return "available";
    }
}
