# Deployment

> **Status:** Nabd currently runs as a **local / development** stack. This document records how to run
> it today and the **intent** for a future production deployment. Production hardening is out of scope
> for the graduation milestone but is documented here as planned work.

## Running locally (current)

The full setup — databases, Keycloak, the three services, and seeding — is in the
[root README](../../README.md#getting-started). In short:

1. PostgreSQL up; `DMRS` database created and migrated (`dotnet ef database update`).
2. Keycloak up (`docker/keycloak`), realm `DMRS`, client `dmrs-api`.
3. Run `DMRS.MedicineInfo.Api` (:5041) → `DMRS.Api` (:7029) → `DMRS.Client` (:7099).
4. Seed sample FHIR data via the development-only `dev/seed` API endpoints (see
   [`development.md`](development.md#seeding-sample-data)).

Trained AI models (`*.onnx`) ship in `DMRS.Api/Ai/` and are copied to the output on build.

## What changes in production (intent)

The following are **not yet implemented** but are the planned steps to take Nabd from a local demo to
a real deployment:

- **Configuration & secrets.** Move connection strings and the Keycloak admin credentials out of
  `appsettings.json` into a secrets manager / environment variables (e.g. Azure Key Vault, user
  secrets, or container env). Provide per-environment `appsettings.Production.json`.
- **HTTPS / TLS.** Terminate TLS with real certificates (not the ASP.NET dev cert); set
  `RequireHttpsMetadata = true` for JWT validation and serve the client over HTTPS only.
- **Database persistence & backups.** Run PostgreSQL on managed/persistent storage with a scheduled
  backup + restore-test policy; apply migrations as a deployment step.
- **Containerization.** Add Dockerfiles for `DMRS.Api`, `DMRS.Client`, and `DMRS.MedicineInfo.Api`,
  plus a compose/orchestration manifest that also brings up PostgreSQL and Keycloak.
- **Health & monitoring.** Add `/health` endpoints, ship Serilog output to a central log sink, and add
  basic metrics/alerting.
- **CORS & origins.** Replace the hardcoded `https://localhost:7099` origin with the real client URL.
- **Keycloak realm export.** Commit a realm/client export so the identity configuration is
  reproducible rather than configured by hand.

## Security & compliance note

Nabd handles simulated patient data only. Any real-world use must address the relevant healthcare
regulations (HIPAA, GDPR, etc.) — audit logging of PHI access, encryption at rest and in transit,
access reviews, and a data-retention policy — before go-live.
