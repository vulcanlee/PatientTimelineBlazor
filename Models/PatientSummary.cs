namespace PatientTimelineBlazor.Models;

public sealed class PatientSummary
{
    public string PatientId { get; init; } = string.Empty;
    public string? Name { get; init; }
    public string? Gender { get; init; }
    public DateOnly? BirthDate { get; init; }
    public int? EncounterCount { get; init; }
    public DateTimeOffset? LatestEncounterDate { get; init; }
}
