using System.Collections.Generic;
using System.Text;

namespace Despicable;

internal sealed class AutoEyePatchDiagnostics
{
    private int _headsScanned;
    private int _headsEligible;
    private int _headsGenerated;
    private int _headsPartial;
    private int _headsSkipped;
    private int _headsFailed;
    private readonly Dictionary<AutoEyePatchSkipReason, int> _skipReasonCounts = new();

    public void RecordScanned() => _headsScanned++;
    public void RecordEligible() => _headsEligible++;

    public void RecordResult(AutoEyePatchHeadResult result)
    {
        if (result == null)
            return;

        switch (result.Status)
        {
            case AutoEyePatchHeadStatus.Generated:
                _headsGenerated++;
                break;
            case AutoEyePatchHeadStatus.Partial:
                _headsPartial++;
                break;
            case AutoEyePatchHeadStatus.Failed:
                _headsFailed++;
                break;
            default:
                _headsSkipped++;
                break;
        }

        RecordReasons(result.SummaryReasons);
    }

    public AutoEyePatchStartupSummary BuildSummary()
    {
        return new AutoEyePatchStartupSummary
        {
            HeadsScanned = _headsScanned,
            HeadsEligible = _headsEligible,
            HeadsGenerated = _headsGenerated,
            HeadsPartial = _headsPartial,
            HeadsSkipped = _headsSkipped,
            HeadsFailed = _headsFailed,
        };
    }

    public string BuildLogSummary()
    {
        StringBuilder sb = new();
        AutoEyePatchStartupSummary summary = BuildSummary();
        sb.Append("[Despicable] AEP-DIAG-PROBE-V9 HIT ")
            .Append("genVer=").Append(AutoEyePatchRuntime.GenerationVersion)
            .Append(" scanned=").Append(summary.HeadsScanned)
            .Append(", eligible=").Append(summary.HeadsEligible)
            .Append(", generated=").Append(summary.HeadsGenerated)
            .Append(", partial=").Append(summary.HeadsPartial)
            .Append(", skipped=").Append(summary.HeadsSkipped)
            .Append(", failed=").Append(summary.HeadsFailed);
        return sb.ToString();
    }

    private void RecordReasons(AutoEyePatchSkipReason reasons)
    {
        if (reasons == AutoEyePatchSkipReason.None)
            return;

        foreach (AutoEyePatchSkipReason flag in System.Enum.GetValues(typeof(AutoEyePatchSkipReason)))
        {
            if (flag == AutoEyePatchSkipReason.None || !reasons.HasFlag(flag))
                continue;
            _skipReasonCounts.TryGetValue(flag, out int count);
            _skipReasonCounts[flag] = count + 1;
        }
    }
}
