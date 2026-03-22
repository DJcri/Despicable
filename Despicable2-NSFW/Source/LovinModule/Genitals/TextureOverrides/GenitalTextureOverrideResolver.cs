using Verse;

namespace Despicable;
internal static class GenitalTextureOverrideResolver
{
    internal static string ResolveTexturePath(Pawn pawn, GenitalDef genitalDef, bool isAroused)
    {
        return AnatomyAppearanceResolver.ResolveTexturePath(pawn, genitalDef, isAroused);
    }
}
