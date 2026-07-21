# FHIR in Nabd

## What FHIR is

**FHIR** (Fast Healthcare Interoperability Resources, by HL7) is the modern standard for exchanging
healthcare data. Clinical concepts are modeled as **resources** (Patient, Condition, Observation, …),
each with a defined structure and a RESTful API for create/read/update/delete, search, and history.
Using FHIR means Nabd can, in principle, exchange data with any other FHIR-compliant system.

Nabd targets **FHIR R5** and uses the **Firely .NET SDK** (`Hl7.Fhir.R5`) for the resource model,
JSON (de)serialization, and specification validation.

## Storage model

Rather than mapping every FHIR field to relational columns, Nabd stores each resource as its **raw
FHIR JSON** and maintains lightweight indexes for querying. Three tables (see
[`architecture-and-data-model.md`](../../docs/architecture-and-data-model.md)) do the work:

| Table | Purpose |
|---|---|
| `FhirResources` | Current version of each resource (raw JSON + metadata; key = `ResourceType` + `Id`). |
| `FhirResourceVersions` | Immutable history — every past version is retained for audit/`_history`. |
| `ResourceIndices` | Denormalized search index: `(ResourceType, SearchParamCode, Value)` rows extracted per resource. |

**Why this design:** it keeps the store generic (any resource type, full fidelity, easy versioning)
while still allowing efficient parameter search through the index — a good fit for a system that must
handle many resource types uniformly.

When a resource is written, a resource-type-specific **indexer** (in
`Infrastructure/Search/…`) extracts the searchable values into `ResourceIndices`.

## Supported resources

| Domain | Resources |
|---|---|
| Administrative | Patient, Practitioner, PractitionerRole, Organization, Location, Encounter |
| Clinical | Condition, Observation, Procedure, AllergyIntolerance |
| Medication | MedicationRequest |
| Scheduling | Appointment, ServiceRequest |

Each is exposed by a thin controller inheriting the generic `FhirBaseController<T>`.

## API surface

All resource endpoints follow the FHIR REST pattern under `fhir/{ResourceType}` and require a valid
token (`Authorize(Policy = "FhirScope")`). Responses use `application/fhir+json`; errors return a
FHIR `OperationOutcome`.

| Operation | Method & route |
|---|---|
| Read (current) | `GET /fhir/{Type}/{id}` |
| Version read | `GET /fhir/{Type}/{id}/_history/{vid}` |
| History | `GET /fhir/{Type}/{id}/_history` |
| Search | `GET /fhir/{Type}?param=value` |
| Create | `POST /fhir/{Type}` |
| Update | `PUT /fhir/{Type}/{id}` |
| Patch | `PATCH /fhir/{Type}/{id}` |
| Delete | `DELETE /fhir/{Type}/{id}` |

Example: `GET /fhir/Patient/123`, `GET /fhir/Condition?patient=Patient/123`.

## Versioning & history

Every write increments the resource's `VersionId` (an optimistic-concurrency token) and appends the
prior state to `FhirResourceVersions`. Deletes are **soft** (`IsDeleted`), preserving history. This
gives a complete audit trail and supports the FHIR `_history` interactions.

## Validation

Incoming resources are validated against the FHIR R5 specification via `FhirValidatorService`
(Firely validation). Invalid resources are rejected with an `OperationOutcome` describing the problem.

## Authorization

Access is gated by `ISmartAuthorizationService`, applying SMART-on-FHIR–style scoping
(System / User / Patient) and organization/patient compartment checks on top of Keycloak
authentication. See [`authorization.md`](authorization.md).
