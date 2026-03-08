namespace Despicable;
public readonly struct PawnHealthSnapshot
{
    public PawnHealthSnapshot(bool isDead, bool isDowned, float bleedRate, bool isInfant, bool isAdult)
    {
        IsDead = isDead;
        IsDowned = isDowned;
        BleedRate = bleedRate;
        IsInfant = isInfant;
        IsAdult = isAdult;
    }

    public bool IsDead { get; }
    public bool IsDowned { get; }
    public float BleedRate { get; }
    public bool IsInfant { get; }
    public bool IsAdult { get; }

    public bool IsEmergencyTendTarget(float bleedRateThreshold = PawnHealthQuery.EmergencyBleedRateThreshold)
    {
        return !IsDead && (IsDowned || BleedRate >= bleedRateThreshold);
    }
}
