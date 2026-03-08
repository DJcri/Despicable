using RimWorld;
using Verse;

namespace Despicable.Core;
public sealed class InteractionResolution
{
    /// <summary>
    /// Echo of the chosen/retained stage variant. Core treats this as opaque.
    /// </summary>
    public string ChosenStageId;

    public InteractionDef ChosenInteractionDef;
    public JobDef ChosenJobDef;
    public string ChosenCommand;
    public bool Allowed;
    public string Reason;

    public string ChosenInteractionId;

    public OutcomeDelta Outcome;       // placeholder for later
    public AnimationPlan Animation;    // placeholder for later
}
