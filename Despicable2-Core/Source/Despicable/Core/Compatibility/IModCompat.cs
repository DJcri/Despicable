using System;

namespace Despicable.Core.Compatibility;
/// <summary>
/// Small, reusable lifecycle contract for optional third-party integrations.
/// Implementations should be idempotent and conservative: detect first, activate second,
/// and report status for diagnostics either way.
/// </summary>
public interface IModCompat
{
    string Id { get; }
    bool CanActivate();
    void Activate();
    string ReportStatus();
}
