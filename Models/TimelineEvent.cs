namespace PatientTimelineBlazor.Models;

public sealed class TimelineEvent
{
    public required string ResourceType { get; init; }
    public required string Id { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }
    public required string Title { get; init; }
    public string? EncounterId { get; init; }
    public string? OrganizationDisplay { get; init; }
    public string? PractitionerDisplay { get; init; }
    public Dictionary<string, string> Details { get; init; } = new();
}
