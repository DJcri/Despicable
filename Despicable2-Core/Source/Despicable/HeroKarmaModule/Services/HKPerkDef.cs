namespace Despicable.HeroKarma;
public sealed class HKPerkDef
{
    public readonly string id;
    public readonly string label;
    public readonly string tooltip;
    public readonly string iconKey;

    public HKPerkDef(string id, string label, string tooltip, string iconKey = null)
    {
        this.id = id;
        this.label = label;
        this.tooltip = tooltip;
        this.iconKey = string.IsNullOrEmpty(iconKey) ? id : iconKey;
    }
}
