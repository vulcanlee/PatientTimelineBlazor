# PatientTimelineBlazor

Blazor Server (Interactive Server - global) front-end prototype for querying Firely FHIR data and rendering a patient timeline.

## Features
- Input Patient ID and optional date range.
- Query patient-related FHIR resources from `https://server.fire.ly`.
- Render unified timeline events (Encounter, Condition, Observation, Procedure, MedicationRequest, etc.).
- Click event card to inspect details.

## Run
```bash
dotnet restore
dotnet run
```

Then open `/patient-timeline`.
