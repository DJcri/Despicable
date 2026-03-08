using Verse;

namespace Despicable.Core;
public static class ContentAvailability
{
    public const string NSFWPackageId = "DCSzar.Despicable2.NSFW";

    public static bool NSFWActive =>
        ModLister.GetActiveModWithIdentifier(NSFWPackageId, ignorePostfix: true) != null;
}
