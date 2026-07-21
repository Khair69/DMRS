# Development Guide

How to run Nabd locally, what talks to what, and the errors you'll most likely hit first. For the
production-deployment intent, see [`deployment.md`](deployment.md).

## Prerequisites

- **.NET 10 SDK**
- **PostgreSQL 12+** (two databases: `DMRS` and `DMRSMedicine`)
- **Keycloak 20+** (realm `DMRS`, client `dmrs-api`)
- A FHIR-aware HTTP client is handy for poking the API (the bundled [`DMRS.Api.http`](../DMRS.Api.http),
  or Swagger UI).

## Services and ports

Nabd is three .NET services plus Keycloak and PostgreSQL. Default local ports:

| Component | URL | Notes |
|---|---|---|
| `DMRS.Api` | `https://localhost:7029` (http `5210`) | Main API — FHIR, CDS, AI, documents. |
| `DMRS.Client` | `https://localhost:7099` (http `5155`) | Blazor WebAssembly front-end. |
| `DMRS.MedicineInfo.Api` | `http://localhost:5041` (https `7234`) | Medicine knowledge source for CDS. |
| Keycloak | `http://localhost:8080` | Identity provider, realm `DMRS`. |
| PostgreSQL | `localhost:5432` | Databases `DMRS` and `DMRSMedicine`. |

The client is configured to call the API at `7029`; the API's CORS policy allows the client's `7099`
and `5155` origins. If you change a port, update **both** sides (`wwwroot/appsettings.json` on the
client, the CORS policy in [`Program.cs`](../Program.cs) on the API).

## First-time setup

1. **PostgreSQL** — start it and ensure the `postgres` user/password matches the connection strings in
   `DMRS.Api/appsettings.json` (`DMRS`) and `DMRS.MedicineInfo.Api/appsettings.json` (`DMRSMedicine`).
2. **Apply migrations** — from `DMRS.Api/`:
   ```
   dotnet ef database update
   ```
   This creates the FHIR tables (`FhirResources`, `FhirResourceVersions`, `ResourceIndices`) and the
   CDS / external-AI tables.
3. **Keycloak** — start it, with realm `DMRS` and a public client `dmrs-api` (Authorization Code + PKCE;
   redirect URIs for `https://localhost:7099/authentication/login-callback` and the logout callback).
   Direct Access Grants may be enabled in dev for API testing.

## Running (start order)

Start back-to-front so each service's dependency is already up:

```
1. DMRS.MedicineInfo.Api   (:5041)   ← CDS knowledge source
2. DMRS.Api                (:7029)   ← depends on Postgres, Keycloak, MedicineInfo
3. DMRS.Client             (:7099)   ← depends on the API
```

In development the API exposes **Swagger UI** at `https://localhost:7029/swagger`.

The trained AI models (`*.onnx`) ship in `DMRS.Api/Ai/` and are copied to the build output
automatically — no extra step.

## Seeding sample data

Sample Synthea FHIR bundles live in `DMRS.Api/sampledata/fhir/`. They are loaded through the
**development-only** seeding endpoints on the API (`SeedDataController`, route prefix `dev/`). Every
endpoint returns **404 outside Development** and requires a valid token with system-admin access
(`FhirScope` policy). Authenticate in Swagger (or use [`DMRS.Api.http`](../DMRS.Api.http)) and call:

| Endpoint | Purpose |
|---|---|
| `GET  /dev/seed/status` | Whether the DB already has data + patient count. |
| `GET  /dev/seed/files?limit=N` | List the bundle files available to import (cap with `limit`). |
| `POST /dev/seed/file` (`{ "fileName": "…" }`) | Import one bundle file. |
| `POST /dev/seed` | Import the full sample set. |
| `DELETE /dev/seed` | Clear all FHIR data. |
| `GET  /dev/seed/reindex/status` · `POST /dev/seed/reindex/{type}` | Rebuild missing search indices. |

> Earlier builds drove these endpoints from an in-app "Test Bench" page; that debug page was removed
> during pre-handoff cleanup, so seeding is now done directly against the API.

## Common errors

| Symptom | Likely cause | Fix |
|---|---|---|
| **401 Unauthorized** on every API call | Token missing, expired, or wrong audience | Confirm you logged in; the token's `aud` must be `dmrs-api` (the API validates audience). |
| **401 with "issuer" / signature error** | API `Keycloak:Authority` ≠ the realm that issued the token | Make both point at `http://localhost:8080/realms/DMRS`. |
| **CORS error** in the browser console | Client origin not in the API's CORS policy | Add the client URL to `WithOrigins(...)` in `Program.cs`. |
| **403 Forbidden** on a resource you "should" see | SMART compartment check — wrong org/patient, or missing role | See [`authorization.md`](authorization.md); confirm the caller's role and org links. |
| **404 on `/dev/seed/...`** | API not running in Development | Run with `ASPNETCORE_ENVIRONMENT=Development`. |
| **CDS cards empty / medicine lookups fail** | `DMRS.MedicineInfo.Api` not running | Start it on `:5041` (the CDS knowledge provider points there). |
| **DB / migration errors at startup** | Postgres down or connection string mismatch | Verify Postgres is up and the `DMRS` database exists and is migrated. |

## Project layout

| Project | Role |
|---|---|
| `DMRS.Api` | ASP.NET Core 10 Web API — FHIR endpoints, CDS engine, AI risk services, documents. |
| `DMRS.Client` | Blazor WebAssembly front-end. |
| `DMRS.MedicineInfo.Api` | Standalone medicine-knowledge API for the CDS engine. |
| `DMRS.Shared` | Shared DTOs/constants. |
| `DMRS.StubAiModel` | Stub external-AI model service for exercising the external-AI integration. |

See also: [`authentication.md`](authentication.md) · [`authorization.md`](authorization.md) · [`fhir.md`](fhir.md) · [`cds-system.md`](cds-system.md)
