using PatientTimelineBlazor.Models;

namespace PatientTimelineBlazor.Services;

public interface IFhirDataService
{
    Task<PatientTimelineResult> GetTimelineAsync(string patientId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default);
}
