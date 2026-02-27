namespace PatientTimelineBlazor.Models;

public sealed class PatientTimelineResult
{
    public required PatientSummary Summary { get; init; }
    public required IReadOnlyList<TimelineEvent> Events { get; init; }
}
