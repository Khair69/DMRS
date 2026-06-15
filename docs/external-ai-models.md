# External AI Models (remote inference)

Lets an admin register remote ("away") AI model endpoints. A clinical user can then send a single
patient's FHIR record to a chosen model and read back its JSON decision. This complements the built-in
local ONNX risk models (diabetes / cardiovascular / readmission) by opening the platform to *any*
HTTP-based model — a teammate's FastAPI/Flask service, a hosted ML endpoint, etc.

## How it works

1. An admin registers a model in **Intelligence → External AI Models** (`/external-ai/admin`):
   name, **HTTPS** endpoint URL, auth (none / API key header / bearer token), timeout, and an optional
   `DecisionJsonPath` to pull a field out of the response.
2. On the patient chart (`/patients/{id}`) or the **AI Insights** page, a user picks a model and runs it.
3. The API gathers the patient's `Patient`, `Observation`, `Condition`, `MedicationRequest`, and
   `AllergyIntolerance` resources into a FHIR **Bundle** (`type = collection`) and `POST`s it
   (`application/fhir+json`) to the endpoint with the configured auth header.
4. The response body is parsed; `DecisionJsonPath` (dot-path, e.g. `result.label`) narrows it, otherwise
   the whole body is returned. The decision + raw response + timing are shown in the UI.

## API

| Method | Route | Who | Purpose |
| ------ | ----- | --- | ------- |
| GET/POST/PUT/DELETE | `external-ai/models[/{id}]` | `ROLE_SYSTEM_ADMIN`, `ROLE_ORG_ADMIN` | CRUD over the registry |
| POST | `external-ai/infer/{modelId}/{patientId}` | any clinical caller (`FhirScope`) | run a model against one patient |

## Security

- **HTTPS-only** endpoints are enforced on save — patient data is never sent over plain HTTP.
- **Secrets are encrypted at rest** with the ASP.NET Data Protection API; they are never returned to the
  client (the DTO only exposes a `HasSecret` flag). On update, a blank secret keeps the stored one.
- **Inference is authorization-checked**: the caller must be allowed to view the patient
  (`ISmartAuthorizationService.ResolveViewPatientIdsAsync`) before any data leaves the system.
- **Every send is audit-logged** (user → model → patient) before the outbound call.
- The run UI shows an explicit notice that patient data leaves the system.

### Operational note

Data Protection keys persist to the host's default key ring. If that key ring is reset (e.g. a fresh
container with no persisted keys), previously stored secrets can no longer be decrypted and must be
re-entered. For a multi-instance deployment, configure a shared key ring
(`PersistKeysToFileSystem`/`ProtectKeysWith…`).

## Code map

- Domain: `DMRS.Api/Domain/ExternalAi/ExternalAiModel.cs`
- Persistence: `AppDbContext` + `Infrastructure/ExternalAi/EfExternalAiModelRepository.cs` (migration `AddExternalAiModels`)
- Services: `Application/ExternalAi/Services/{ExternalAiModelManagementService,ExternalAiInferenceService}.cs`
- Controllers: `Controllers/ExternalAi/{ExternalAiModelsController,ExternalAiInferenceController}.cs`
- Client: `Features/ExternalAi/**`, `Pages/ExternalAi/Admin.razor`, run panel on patient Details + AI Insights
