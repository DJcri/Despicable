using RimWorld;
using UnityEngine;
using Verse;

namespace Despicable.HeroKarma.UI;

public class Command_OpenHeroKarma : Command
{
    public override string Label => "D2HK_UI_HeroKarma".Translate();
    public override string Desc => "D2HK_UI_OpenHeroKarmaDesc".Translate();

    public Command_OpenHeroKarma(Texture2D iconTex = null)
    {
        icon = iconTex ?? HKUIConstants.HeroGizmoIcon;
        defaultLabel = Label;
        defaultDesc = Desc;
    }

    public override void ProcessInput(Event ev)
    {
        base.ProcessInput(ev);
        Dialog_HeroKarma.ShowWindow();
    }
}
