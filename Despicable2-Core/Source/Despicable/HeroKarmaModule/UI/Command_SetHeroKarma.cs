using RimWorld;
using UnityEngine;
using Verse;

namespace Despicable.HeroKarma.UI;

public class Command_SetHeroKarma : Command
{
    private readonly Pawn pawn;

    public override string Label => "D2HK_UI_AssignHero".Translate();
    public override string Desc => "D2HK_UI_AssignHeroDesc".Translate();

    public Command_SetHeroKarma(Pawn pawn, Texture2D iconTex = null)
    {
        this.pawn = pawn;
        icon = iconTex ?? HKUIConstants.HeroGizmoIcon;
        defaultLabel = Label;
        defaultDesc = Desc;
    }

    public override void ProcessInput(Event ev)
    {
        base.ProcessInput(ev);
        if (pawn == null)
            return;

        Dialog_AssignHeroKarma.ShowFor(pawn);
    }
}
