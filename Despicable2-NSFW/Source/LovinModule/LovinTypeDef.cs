using RimWorld;
using UnityEngine;
using Verse;

namespace Despicable;
public class LovinTypeDef : Def
{
    public InteractionDef interaction;
    public bool isSolo = false;
    public bool requiresMale = false;
    public bool requiresFemale = false;
    public string iconPath;

    private Texture2D cachedMenuIcon;

    public string ResolvePlayerFacingLabel()
    {
        if (!label.NullOrEmpty())
            return label.CapitalizeFirst();

        if (interaction != null && !interaction.label.NullOrEmpty())
            return interaction.label.CapitalizeFirst();

        return defName;
    }

    public Texture2D ResolveMenuIcon()
    {
        if (cachedMenuIcon != null)
            return cachedMenuIcon;

        if (!iconPath.NullOrEmpty())
            cachedMenuIcon = ContentFinder<Texture2D>.Get(iconPath, reportFailure: false);

        if (cachedMenuIcon == null && interaction != null)
            cachedMenuIcon = interaction.GetSymbol();

        return cachedMenuIcon;
    }
}
