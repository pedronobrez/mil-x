namespace OpenDIAL.Pipeline.Model;

/// <summary>
/// Progress notification. <see cref="Percent"/> is local to the current stage, <see cref="OverallPercent"/>
/// spans the whole run. <see cref="ShowInLog"/> is false for high-frequency percentage ticks that a UI
/// should use only to move a progress bar.
/// </summary>
public sealed record PipelineProgress(
    string Stage,
    string? FileName,
    double Percent,
    double OverallPercent,
    string Message,
    bool ShowInLog = true)
{
    public override string ToString() => FileName is null ? $"[{Stage}] {Message}" : $"[{Stage}] {FileName}: {Message}";
}
