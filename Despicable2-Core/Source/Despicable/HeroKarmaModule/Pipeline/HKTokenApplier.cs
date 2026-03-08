using System.Collections.Generic;
using Verse;

namespace Despicable.HeroKarma;
/// <summary>
/// Applies effect tokens in one place.
/// Your rebuilt pipeline can call this during its Apply stage.
/// </summary>
public static class HKTokenApplier
{
    public static void ApplyAll(Pawn hero, IEnumerable<IHKEffectToken> tokens)
    {
        if (hero == null || tokens == null) return;
        foreach (var t in tokens)
        {
            if (t == null) continue;
            try { t.Apply(hero); }
            catch { /* never let a single token break the pipeline */ }
        }
    }
}
