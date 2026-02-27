using System.Text.Json;
using PatientTimelineBlazor.Models;

namespace PatientTimelineBlazor.Services;

public sealed class FhirDataService(HttpClient httpClient) : IFhirDataService
{
    private static readonly string[] TimelineResourceTypes =
    [
        "Encounter",
        "Condition",
        "AllergyIntolerance",
        "Immunization",
        "Device",
        "Observation",
        "Procedure",
        "DiagnosticReport",
        "DocumentReference",
        "MedicationRequest"
    ];

    public async Task<PatientTimelineResult> GetTimelineAsync(string patientId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default)
    {
        var patient = await GetResourceAsync($"Patient/{patientId}", cancellationToken);
        var patientSummary = ToPatientSummary(patient, patientId);

        var events = new List<TimelineEvent>();
        foreach (var resourceType in TimelineResourceTypes)
        {
            var query = BuildPatientQuery(resourceType, patientId, from, to);
            var bundle = await GetResourceAsync(query, cancellationToken);
            events.AddRange(ParseBundle(resourceType, bundle));
        }

        var orderedEvents = events.OrderByDescending(e => e.OccurredAt).ToList();
        var encounterCount = orderedEvents.Count(e => e.ResourceType == "Encounter");
        var latestEncounterDate = orderedEvents.FirstOrDefault(e => e.ResourceType == "Encounter")?.OccurredAt;

        var summary = new PatientSummary
        {
            PatientId = patientSummary.PatientId,
            Name = patientSummary.Name,
            Gender = patientSummary.Gender,
            BirthDate = patientSummary.BirthDate,
            EncounterCount = encounterCount,
            LatestEncounterDate = latestEncounterDate
        };

        return new PatientTimelineResult
        {
            Summary = summary,
            Events = orderedEvents
        };
    }

    private async Task<JsonDocument> GetResourceAsync(string relativeUrl, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(relativeUrl, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"FHIR API error: {(int)response.StatusCode} {response.ReasonPhrase}. {body}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    private static string BuildPatientQuery(string resourceType, string patientId, DateOnly? from, DateOnly? to)
    {
        var dateQuery = resourceType switch
        {
            "Encounter" => "date",
            "Observation" => "date",
            "Procedure" => "date",
            "Immunization" => "date",
            "DiagnosticReport" => "date",
            "Condition" => "recorded-date",
            _ => string.Empty
        };

        var queryParts = new List<string> { $"patient={Uri.EscapeDataString(patientId)}", "_count=200" };

        if (!string.IsNullOrWhiteSpace(dateQuery) && from is not null)
        {
            queryParts.Add($"{dateQuery}=ge{from.Value:yyyy-MM-dd}");
        }

        if (!string.IsNullOrWhiteSpace(dateQuery) && to is not null)
        {
            queryParts.Add($"{dateQuery}=le{to.Value:yyyy-MM-dd}");
        }

        return $"{resourceType}?{string.Join("&", queryParts)}";
    }

    private static List<TimelineEvent> ParseBundle(string resourceType, JsonDocument bundleDocument)
    {
        var result = new List<TimelineEvent>();
        if (!bundleDocument.RootElement.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var entry in entries.EnumerateArray())
        {
            if (!entry.TryGetProperty("resource", out var resource))
            {
                continue;
            }

            var id = GetString(resource, "id") ?? Guid.NewGuid().ToString("N");
            var occurred = GetDateTime(resource)
                ?? DateTimeOffset.MinValue;
            var title = GetTitle(resourceType, resource);
            var encounterId = ReadReferenceId(resource, "encounter");

            result.Add(new TimelineEvent
            {
                ResourceType = resourceType,
                Id = id,
                OccurredAt = occurred,
                Title = title,
                EncounterId = encounterId,
                OrganizationDisplay = ReadActorDisplay(resource),
                PractitionerDisplay = ReadPerformerDisplay(resource),
                Details = GetCommonDetails(resource)
            });
        }

        return result;
    }

    private static PatientSummary ToPatientSummary(JsonDocument patient, string patientId)
    {
        var root = patient.RootElement;
        string? name = null;
        if (root.TryGetProperty("name", out var names) && names.ValueKind == JsonValueKind.Array)
        {
            var first = names.EnumerateArray().FirstOrDefault();
            var given = first.TryGetProperty("given", out var givenArray) && givenArray.ValueKind == JsonValueKind.Array
                ? string.Join(" ", givenArray.EnumerateArray().Select(g => g.GetString()).Where(v => !string.IsNullOrWhiteSpace(v)))
                : null;
            var family = first.TryGetProperty("family", out var familyProp) ? familyProp.GetString() : null;
            name = string.Join(" ", new[] { given, family }.Where(v => !string.IsNullOrWhiteSpace(v)));
        }

        var gender = GetString(root, "gender");
        DateOnly? birthDate = null;
        if (GetString(root, "birthDate") is { } birthText && DateOnly.TryParse(birthText, out var parsed))
        {
            birthDate = parsed;
        }

        return new PatientSummary
        {
            PatientId = patientId,
            Name = string.IsNullOrWhiteSpace(name) ? null : name,
            Gender = gender,
            BirthDate = birthDate
        };
    }

    private static string GetTitle(string resourceType, JsonElement resource)
    {
        return resourceType switch
        {
            "Encounter" => GetString(resource, "status") is { } status ? $"Encounter ({status})" : "Encounter",
            "Condition" => GetCodeText(resource, "code") ?? "Condition",
            "Observation" => GetCodeText(resource, "code") ?? "Observation",
            "Procedure" => GetCodeText(resource, "code") ?? "Procedure",
            "MedicationRequest" => GetCodeText(resource, "medicationCodeableConcept") ?? "MedicationRequest",
            "DiagnosticReport" => GetCodeText(resource, "code") ?? "DiagnosticReport",
            "AllergyIntolerance" => GetCodeText(resource, "code") ?? "AllergyIntolerance",
            "Immunization" => GetCodeText(resource, "vaccineCode") ?? "Immunization",
            "DocumentReference" => GetCodeText(resource, "type") ?? "DocumentReference",
            "Device" => GetCodeText(resource, "type") ?? "Device",
            _ => resourceType
        };
    }

    private static DateTimeOffset? GetDateTime(JsonElement resource)
    {
        string[] possibleFields = ["effectiveDateTime", "issued", "recordedDate", "occurrenceDateTime", "performedDateTime", "authoredOn", "created", "date", "period"];

        foreach (var field in possibleFields)
        {
            if (!resource.TryGetProperty(field, out var value))
            {
                continue;
            }

            if (field == "period" && value.ValueKind == JsonValueKind.Object)
            {
                if (value.TryGetProperty("start", out var start) && DateTimeOffset.TryParse(start.GetString(), out var periodStart))
                {
                    return periodStart;
                }
            }
            else if (value.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(value.GetString(), out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static string? GetString(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? GetCodeText(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (GetString(property, "text") is { } text)
        {
            return text;
        }

        if (property.TryGetProperty("coding", out var coding) && coding.ValueKind == JsonValueKind.Array)
        {
            foreach (var code in coding.EnumerateArray())
            {
                if (GetString(code, "display") is { } display)
                {
                    return display;
                }
            }
        }

        return null;
    }

    private static string? ReadReferenceId(JsonElement resource, string propertyName)
    {
        if (!resource.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var reference = GetString(property, "reference");
        if (string.IsNullOrWhiteSpace(reference))
        {
            return null;
        }

        var segments = reference.Split('/');
        return segments.LastOrDefault();
    }

    private static string? ReadActorDisplay(JsonElement resource)
    {
        if (resource.TryGetProperty("serviceProvider", out var serviceProvider) && serviceProvider.ValueKind == JsonValueKind.Object)
        {
            return GetString(serviceProvider, "display");
        }

        return null;
    }

    private static string? ReadPerformerDisplay(JsonElement resource)
    {
        if (resource.TryGetProperty("participant", out var participant) && participant.ValueKind == JsonValueKind.Array)
        {
            var firstParticipant = participant.EnumerateArray().FirstOrDefault();
            if (firstParticipant.ValueKind == JsonValueKind.Object
                && firstParticipant.TryGetProperty("individual", out var individual)
                && individual.ValueKind == JsonValueKind.Object)
            {
                return GetString(individual, "display");
            }
        }

        if (resource.TryGetProperty("performer", out var performer) && performer.ValueKind == JsonValueKind.Array)
        {
            var firstPerformer = performer.EnumerateArray().FirstOrDefault();
            if (firstPerformer.ValueKind == JsonValueKind.Object)
            {
                if (firstPerformer.TryGetProperty("actor", out var actor) && actor.ValueKind == JsonValueKind.Object)
                {
                    return GetString(actor, "display");
                }

                if (firstPerformer.TryGetProperty("reference", out var reference) && reference.ValueKind == JsonValueKind.Object)
                {
                    return GetString(reference, "display");
                }
            }
        }

        return null;
    }

    private static Dictionary<string, string> GetCommonDetails(JsonElement resource)
    {
        var details = new Dictionary<string, string>();
        var status = GetString(resource, "status");
        if (!string.IsNullOrWhiteSpace(status))
        {
            details["status"] = status;
        }

        var encounter = ReadReferenceId(resource, "encounter");
        if (!string.IsNullOrWhiteSpace(encounter))
        {
            details["encounterId"] = encounter;
        }

        return details;
    }
}
