# Authorization in DMRS

This document describes how DMRS decides **what an authenticated caller is allowed to do**. It covers
the role model, the SMART-on-FHIR–style scope levels, the two authorization policies, and the
per-request enforcement pipeline as it exists in the code today.

Authentication (proving *who* the caller is) is a separate concern handled by Keycloak and JWT bearer
validation — see [`authentication.md`](authentication.md). Authorization picks up *after* a valid token
has been established.

## Two layers of authorization

DMRS gates access at two levels, and a request must pass both:

1. **Role-based access (RBAC)** — coarse-grained. Realm roles from the token (`ROLE_SYSTEM_ADMIN`,
   `ROLE_ORG_ADMIN`, `ROLE_PRACTITIONER`, `ROLE_PATIENT`) decide which *areas* of the system a caller
   can reach. Enforced with `[Authorize(Roles = …)]` on controllers/actions and `<AuthorizeView>` in
   the Blazor UI.
2. **SMART scope + compartment access** — fine-grained. For FHIR resource operations, a custom
   authorization handler decides whether *this* caller may perform *this* action on *this specific
   resource*, based on SMART scopes plus patient/organization ownership ("compartment") checks.

> **Why both?** Roles say "a practitioner may use the clinical workspace." Compartment checks say
> "*this* practitioner may edit demographics and administrative data only for *their* organization"
> (clinical data is contributable cross-org — see below). Least privilege requires the second layer;
> roles alone are too coarse for patient data.

## Roles

| Role | Who | Typical capability |
|---|---|---|
| `ROLE_SYSTEM_ADMIN` | Platform operator | Full cross-organization access; manage organizations, CDS rules, AI model registry. |
| `ROLE_ORG_ADMIN` | Organization administrator | Manage their own organization: staff, CDS admin, external-AI admin. |
| `ROLE_PRACTITIONER` | Clinician (doctor/nurse) | Clinical workspace — read any patient record and create/update clinical data for any patient (cross-org); administrative edits stay within their org. |
| `ROLE_PATIENT` | Patient | Read-only self-service portal scoped to their own record (`/my-health`, `/my-profile`). |

Roles are issued by Keycloak as realm roles and surface on the token under the `roles` claim
(configured via `RoleClaimType = "roles"` on the API and `RoleClaim = "roles"` on the client).

### Why doctors ≠ patients ≠ admins (least privilege)

Each role is granted only what its job requires:

- A **patient** can read their own record but cannot see other patients or write clinical data.
- A **practitioner** can read across organizations *and* contribute clinical data (conditions,
  observations, medications, …) to any patient, because a patient may be treated anywhere. Only
  **administrative** writes (the Patient record itself, organizations, locations, staff) and **deletes**
  stay within their own organization's compartment.
- An **org admin** manages people and configuration for one organization, not clinical content at large.
- A **system admin** is the only role with unrestricted, cross-organization reach.

This narrowing is enforced in code, not by convention — see the access levels below.

## SMART access levels

For FHIR resource operations, `SmartAuthorizationService.GetAccessLevel(user, resourceType, action)`
([`Infrastructure/Security/SmartAuthorizationService.cs`](../Infrastructure/Security/SmartAuthorizationService.cs))
resolves the caller to one of four levels by combining their **scopes** with their **roles**:

| Level | Granted when | Effective reach |
|---|---|---|
| `System` | Has a `system/<type>.<action>` scope **and** the `ROLE_SYSTEM_ADMIN` role | All resources, all organizations. |
| `User` | Has a `user/<type>.<action>` scope **and** a staff role (`IsStaff`) | Cross-org reads and cross-org clinical writes for patient-facing types; administrative writes and all deletes stay org-scoped. |
| `Patient` | Has a `patient/<type>.<action>` scope | Only resources in the caller's own patient compartment. |
| `None` | No matching scope | Denied. |

Scopes follow the SMART pattern `<context>/<resourceType>.<action>` with wildcards
(`user/*.*`, `system/Patient.read`, …). Action mapping: `GET → read`, `POST/PUT/PATCH → write`,
`DELETE → delete`; a `write` is also satisfied by `create`/`update`/`delete` scopes. See `HasScope`.

### The default-scope gotcha (why roles gate the level)

The Keycloak realm grants `user/*.*` and `system/*.*` as **default client scopes to every token —
including a patient's**. Scope alone would therefore promote a patient to `User` (org) level and then,
finding no organization, show them nothing. To prevent this, `System` and `User` levels each require a
**role gate** (`ROLE_SYSTEM_ADMIN` / `IsStaff`). A pure patient fails both gates and correctly falls
through to `Patient` level, scoped to their own record. This is the same class of bug that once
misclassified patients as org users.

## The two policies

Registered in [`Infrastructure/Security/AuthorizationServiceExtensions.cs`](../Infrastructure/Security/AuthorizationServiceExtensions.cs):

| Policy | Applied to | Rule |
|---|---|---|
| `FhirScope` | Every FHIR resource controller (via `FhirBaseController<T>`) and `analytics` | Runs the SMART access-level + compartment pipeline (below). |
| `CdsAdmin` | CDS administration endpoints (rule authoring, medicine knowledge) | Simple role check: `ROLE_SYSTEM_ADMIN` or `ROLE_ORG_ADMIN`. |

> **Why CDS admin is *not* `FhirScope`.** The `FhirScope` policy keys off the controller name as if it
> were a FHIR resource type and runs an org-ownership gate. CDS rules are not FHIR resources and have no
> org-ownership semantics, so routing them through `FhirScope` would 403 legitimate admins. CDS admin is
> therefore gated on roles directly via the `CdsAdmin` policy.

## Per-request enforcement (the `FhirScope` pipeline)

`FhirScopeHandler` ([`Infrastructure/Security/FhirScopeHandler.cs`](../Infrastructure/Security/FhirScopeHandler.cs))
runs on every `FhirScope`-protected request:

1. Derive `action` from the HTTP method and `resourceType` from the route's controller name.
2. Compute the SMART access level. `None` → **deny**.
3. For **instance** requests (a specific `{id}`, non-`POST`), apply a compartment check:
   - **Patient level:** the resource must be owned by the caller's patient compartment
     (`IsResourceOwnedByPatientAsync`), or it's denied.
   - **User level:** the resource must be owned by one of the caller's organizations
     (`IsResourceOwnedByOrganizationsAsync`) — **except** cross-organization reads and cross-organization
     clinical writes (below), which skip this gate and defer to the controller's finer-grained check.
4. Otherwise (collection/search/create, or a passing instance check) → **succeed**.

Version/history reads (`/_history`, `vid`) skip the instance ownership gate so audit trails remain
viewable; the access-level gate still applies.

### Cross-organization read & write relaxation

A patient may be treated at multiple organizations, so a practitioner needs to read a patient's full
record — and contribute to it — regardless of which org owns each row. Two whitelists express this:

- **`IsCrossOrganizationReadableType`** — the patient-facing types `Patient`, `Encounter`, `Condition`,
  `Observation`, `Procedure`, `MedicationRequest`, `ServiceRequest`, `AllergyIntolerance`, `Appointment`.
  **User-level reads** of these bypass the org-ownership gate.
- **`IsCrossOrganizationWritableType`** — the patient-owned *clinical* types (the same set **minus
  `Patient`**): `Encounter`, `Condition`, `Observation`, `Procedure`, `MedicationRequest`,
  `ServiceRequest`, `AllergyIntolerance`, `Appointment`. **User-level creates and updates** of these
  bypass the org-ownership gate, so a treating clinician at any facility can add to (and correct) a
  patient's longitudinal record. This is enforced in both `FhirScopeHandler` (the policy gate, for
  instance PUT/PATCH) and `CanCreateResource`/`CanUpdateResource` in `FhirBaseController<T>`.

Deliberately **still org-scoped**: editing the **`Patient`** record itself (demographics), **deletes**
of any type, and all **administrative** types (`Organization`, `Location`, `Practitioner`,
`PractitionerRole` — which stay org-scoped even for reads).

> **Why writes, not just reads?** In a national record model, cross-org read without cross-org write is
> a dead end: a doctor could open a referred patient's chart but not prescribe. Allowing clinical writes
> everywhere — while keeping demographics, deletes, and administrative data org-scoped — matches how a
> shared longitudinal record is actually used. The trade-off is that any authenticated clinician can add
> clinical data to any patient; attributing *who* wrote what is a Provenance/audit concern, not an
> access-gate one.

## Aggregate views (dashboards & analytics)

Dashboards, analytics, and batch risk scoring don't operate on a single resource, so they ask the
authorization service for the **set of patient ids the caller may see**:

- `ResolveAccessiblePatientIdsAsync` returns `null` for a system caller ("all patients"), the org's
  patients for a staff caller, or just the caller's own id for a patient.
- `ResolveViewPatientIdsAsync(panelOnly: true)` further narrows a practitioner to their own panel
  (patients whose `generalPractitioner` is the caller), intersected with the accessible set so a view
  filter can never widen access.

## How a caller is mapped to a compartment

The service resolves identity from token claims, with database fallbacks:

- **Patient id** — from `patient` / `fhirUser` claims, falling back to the Patient whose FHIR
  `identifier` is linked to the caller's Keycloak `sub`.
- **Practitioner id** — from `practitioner` / `fhirUser` claims, with the same `sub`-linked fallback.
- **Organization ids** — from `organization` claims **plus** every organization referenced by the
  caller's `PractitionerRole` records.

These fallbacks exist because the invite/claim flow links accounts via FHIR `identifier`s rather than
stamping `patient_id`/`practitioner_id` attributes onto the Keycloak user.

## Summary

- RBAC gates *areas*; SMART scopes + compartment checks gate *individual resources*.
- Four access levels (`System` / `User` / `Patient` / `None`), with role gates that stop default
  wildcard scopes from over-granting.
- Two policies: `FhirScope` (resource pipeline) and `CdsAdmin` (role check).
- Cross-org reads are relaxed for patient-facing types, and cross-org **clinical writes** for the
  patient-owned clinical types; Patient demographics, deletes, and administrative writes stay org-scoped.

See also: [`authentication.md`](authentication.md) · [`security.md`](security.md) · [`fhir.md`](fhir.md)
