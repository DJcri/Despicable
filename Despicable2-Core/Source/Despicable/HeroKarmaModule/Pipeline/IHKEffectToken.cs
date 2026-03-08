using Verse;

namespace Despicable.HeroKarma;
/// <summary>
/// Step 4 (pipeline alignment): effect tokens are produced by the Karma pipeline
/// and then applied in one place.
///
/// This interface is intentionally small so your real pipeline can adopt it
/// without needing to reference UI types.
/// </summary>
public interface IHKEffectToken
{
    void Apply(Pawn hero);
}
