# DMRS API Reference

Complete reference for the DMRS HTTP APIs.

## Base URLs

| Service | Dev URL |
|---|---|
| Main API (`DMRS.Api`) | `https://localhost:7029` |
| Medicine Info API (`DMRS.MedicineInfo.Api`) | `http://localhost:5041` |

Interactive docs (Swagger UI) are available in Development at `https://localhost:7029/swagger`.

## Authentication

All main-API endpoints require a **Bearer JWT** issued by Keycloak (realm `DMRS`):

```
Authorization: Bearer <access_token>
```

- Most endpoints require the **`FhirScope`** policy (a valid token with FHIR scopes). FHIR resource
  access is further restricted per user/patient/organization by the SMART authorization service.
- Onboarding endpoints (`/api/patients`, `/api/staff`) require only an authenticated user.
- `/dev/*` endpoints exist **only in the Development environment** (they return `404` otherwise).
- The Medicine Info API is an internal service and is unauthenticated.

## Conventions

- **FHIR endpoints** consume/produce `application/fhir+json`; errors return a FHIR `OperationOutcome`.
- **Non-FHIR endpoints** use plain `application/json`.
- IDs in `{...}` are path parameters. `?param=` denotes query parameters.

---

## 1. FHIR resource endpoints

Every FHIR resource type is served by the same generic controller at **`/fhir/{ResourceType}`** with
the standard REST interactions:

| Interaction | Method & path | Notes |
|---|---|---|
| Search | `GET /fhir/{Type}?param=value` | Returns a FHIR `Bundle` (searchset). Params are FHIR search params, e.g. `?patient=Patient/123`. |
| Read | `GET /fhir/{Type}/{id}` | Current version of the resource. |
| Version read | `GET /fhir/{Type}/{id}/_history/{vid}` | A specific historical version. |
| History | `GET /fhir/{Type}/{id}/_history` | All versions of the resource. |
| Create | `POST /fhir/{Type}` | Body = FHIR resource JSON. Returns `201` with `Location`. |
| Update | `PUT /fhir/{Type}/{id}` | Full replace; body id must match path id. |
| Patch | `PATCH /fhir/{Type}/{id}` | Partial update. |
| Delete | `DELETE /fhir/{Type}/{id}` | Soft delete (history preserved). |

Created/updated resources are validated against the FHIR R5 spec; invalid bodies return `400` with an
`OperationOutcome`.

**Available resource types** (`{Type}`):

`Patient`, `Practitioner`, `PractitionerRole`, `Organization`, `Location`, `Encounter`,
`AllergyIntolerance`, `Condition`, `Observation`, `Procedure`, `MedicationRequest`, `Appointment`,
`ServiceRequest`, `Bundle`, `Provenance`.

**Type-specific notes**
- `MedicationRequest` — create/update/patch additionally warm up medicine-knowledge lookups used by CDS.
- `Practitioner` — has a custom delete that also cleans up related links.

---

## 2. Clinical Decision Support (CDS)

### CDS Hooks — `/cds-services`
| Method & path | Description |
|---|---|
| `GET /cds-services` | Discovery: list available CDS services (`{ "services": [...] }`). |
| `POST /cds-services/{id}` | Execute a CDS service. Body = CDS Hooks request (`context`, optional `prefetch`). Returns CDS `cards`. |

### Rule management — `/cds/rules`
| Method & path | Description |
|---|---|
| `GET /cds/rules` | List all rule definitions. |
| `GET /cds/rules/{id}` | Get one rule (GUID id). |
| `GET /cds/rules/{id}/versions` | List published versions of a rule. |
| `POST /cds/rules` | Create a rule. Body = `CdsRuleDefinition`. |
| `PUT /cds/rules/{id}` | Update a rule. Body = `CdsRuleDefinition`. |
| `PATCH /cds/rules/{id}/activate` | Activate the rule. |
| `PATCH /cds/rules/{id}/deactivate` | Deactivate the rule. |
| `POST /cds/rules/{id}/publish` | Publish current draft as a new immutable version. |
| `PATCH /cds/rules/{id}/archive` | Archive the rule. |
| `POST /cds/rules/{id}/clone` | Clone an existing rule. |
| `POST /cds/rules/validate` | Validate a rule definition without saving. Body = `CdsRuleDefinition`. |
| `POST /cds/rules/preview` | Dry-run a rule against sample context. Body = `RulePreviewRequest`. |
| `GET /cds/rules/variables` | List the CDS variables usable in rule expressions. |
| `GET /cds/rules/templates` | List rule templates. |
| `POST /cds/rules/templates/compile` | Compile a template into a rule (no save). Body = `RuleTemplateRequest`. |
| `POST /cds/rules/templates` | Create a rule from a template. Body = `RuleTemplateRequest`. |

### Medicine knowledge — `/cds/medications`
| Method & path | Description |
|---|---|
| `GET /cds/medications?q=&ingredient=&indication=&limit=25` | Search cached medicine knowledge. |
| `GET /cds/medications/{value}` | Get knowledge for one drug by RxCUI or name. |
| `POST /cds/medications/{value}/refresh` | Force-refresh that drug's cached knowledge from the provider. |

### AI risk insights — `/cds/risk`
| Method & path | Description |
|---|---|
| `GET /cds/risk/diabetes/{patientId}` | Diabetes risk assessment for a patient. |
| `GET /cds/risk/cardiovascular/{patientId}` | Cardiovascular risk assessment. |
| `GET /cds/risk/high-utilization/{patientId}` | 30-day readmission risk assessment. |

Each returns the probability, risk tier, feature values used, and a `featuresComplete` flag. Returns
`200` with a null body when the model is unavailable.

### Alert feed — `/cds/alerts`
| Method & path | Description |
|---|---|
| `GET /cds/alerts?count=20` | Most recent CDS card-fire events, newest first (max 50). |

---

## 3. Analytics — `/analytics`
| Method & path | Description |
|---|---|
| `GET /analytics/condition-prevalence` | Top 10 most common conditions across all patients. |

---

## 4. Patient documents — `/api/patients/{patientId}/documents`
| Method & path | Description |
|---|---|
| `GET /api/patients/{patientId}/documents` | List a patient's document records. |
| `POST /api/patients/{patientId}/documents` | Upload a file (`multipart/form-data`, field `file`; max 20 MB). |
| `GET /api/patients/{patientId}/documents/{documentId}/content` | Download the file. |
| `DELETE /api/patients/{patientId}/documents/{documentId}` | Delete a document. |

---

## 5. Onboarding — invites & claims

Staff create an invite (returns a one-time code); the invited user then claims it to link their
Keycloak account to the FHIR Patient/Practitioner record and receive the right role.

| Method & path | Description |
|---|---|
| `POST /api/patients/create-invite` | Create a new Patient record + invite code. Body: `organizationId`, `givenName`, `familyName`, `birthDate`, `appBaseUri`. |
| `POST /api/patients/{id}/create-invite` | Create an invite for an existing Patient. |
| `POST /api/patients/claim-invite` | Patient claims an invite. Body: `inviteCode`, `keycloakUserId`. Grants `ROLE_PATIENT`. |
| `POST /api/staff/create-invite` | Create a Practitioner record + invite. Body: `organizationId`, `givenName`, `familyName`, … |
| `POST /api/staff/claim-invite` | Staff claims an invite. Body: `inviteCode`, `keycloakUserId`. Grants org-admin / doctor role. |

---

## 6. Development & seeding — `/dev` (Development only)
| Method & path | Description |
|---|---|
| `GET /dev/seed/status` | Patient count, to show whether data is loaded. |
| `GET /dev/seed/files?limit=N` | List available sample-data file names. |
| `POST /dev/seed/file` | Seed one file. Body: `{ "fileName": "..." }`. |
| `POST /dev/seed` | Seed every sample file in one request. |
| `DELETE /dev/seed` | Delete all FHIR resources, versions, and indices. |
| `GET /dev/seed/reindex/status` | Count of un-indexed resources per type. |
| `POST /dev/seed/reindex/{resourceType}` | Rebuild the search index for one resource type. |

---

## 7. Medicine Info API (separate service, `:5041`)
| Method & path | Description |
|---|---|
| `GET /api/medications/{value}` | Look up a medicine. If `{value}` is all digits it matches by RxCUI; otherwise by name (exact preferred, then substring, case-insensitive). Returns the medicine with dosing, safety, and ingredients, or `404`. |
| `GET /` | Welcome/help string. |

This standalone service backs the main API's `/cds/medications` knowledge provider.

---

## Appendix: internal/debug

`TestController` (`/api/test/*`) contains auth-diagnostic endpoints used during development. It is
hidden from Swagger and is not part of the public API surface.
