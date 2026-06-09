# Digital Medical Records System (DMRS) — نظام السجلات الطبية الرقمية

A comprehensive, FHIR-compliant digital medical records platform built with ASP.NET Core and Blazor.
It combines standards-based clinical data management (HL7 FHIR R5) with a Clinical Decision Support
(CDS) rule engine and AI risk-prediction models.

نظام شامل لإدارة السجلات الطبية الرقمية تم بناؤه باستخدام ASP.NET Core وBlazor، ويطبّق معايير
HL7 FHIR R5 لضمان قابلية التشغيل البيني، مع محرّك دعم القرار السريري (CDS) ونماذج ذكاء اصطناعي
للتنبؤ بالمخاطر.

## Overview

DMRS is a modern, standards-based healthcare information system designed to manage patient medical
records securely and efficiently. It leverages the HL7 FHIR R5 specification for interoperability,
secures access through OAuth2/OpenID Connect (Keycloak) with SMART-on-FHIR–style scopes, and layers
on a configurable CDS rule engine plus three trained ONNX models that score clinical risk from each
patient's FHIR data.

## Features

- **FHIR R5 resources** — Patient, Practitioner, PractitionerRole, Organization, Location, Encounter,
  Appointment, Condition, Observation, Procedure, MedicationRequest, AllergyIntolerance, ServiceRequest.
- **Versioning & history** — every resource is version-tracked with a full history trail and soft deletes.
- **FHIR search & validation** — parameter-based search via resource indexers; validation against the
  R5 specification (Firely SDK).
- **Authentication & authorization** — Keycloak (OAuth2/OIDC), JWT bearer tokens, role-based access,
  and SMART-style System/User/Patient scoping.
- **Clinical Decision Support (CDS)** — CDS Hooks endpoints, a JSON-Logic rule engine, a rule-authoring
  workbench (CDS Admin), card templates, and a real-time alert feed.
- **Medicine knowledge** — drug dosing/safety lookups via a pluggable provider (a bundled
  `DMRS.MedicineInfo.Api`, or live RxNorm).
- **AI risk models** — three ONNX classifiers that score risk from a patient's FHIR record:
  **Diabetes risk**, **Cardiovascular (heart-disease) risk**, and **30-day Readmission risk**
  (see [AI Models](#ai-models)).
- **Patient documents** — upload and manage clinical files per patient.
- **Dashboards & analytics** — workspace dashboard, AI Insights watchlist, and population-health views.

## Projects

| Project | Description |
|---|---|
| `DMRS.Api` | ASP.NET Core 10 Web API — FHIR endpoints, CDS engine, AI risk services, documents. |
| `DMRS.Client` | Blazor WebAssembly front-end (clinical workspace, CDS Admin, AI Insights). |
| `DMRS.MedicineInfo.Api` | Standalone API serving medicine knowledge (RxCUI lookup) to the CDS engine. |
| `DMRS.Shared` | Shared DTOs/constants. |

## Tech Stack

- **Backend:** ASP.NET Core 10 (Web API), Entity Framework Core 10, PostgreSQL
- **FHIR:** HL7.Fhir.R5 (Firely SDK) with R5 specification validation
- **Frontend:** Blazor WebAssembly (.NET 10), Bootstrap
- **Auth:** Keycloak (OAuth2 / OpenID Connect)
- **AI/ML:** Microsoft.ML.OnnxRuntime (scikit-learn models exported to ONNX)
- **Logging / API docs:** Serilog, Swagger / OpenAPI

## Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download)
- [PostgreSQL](https://www.postgresql.org/download/) 12 or higher
- [Keycloak](https://www.keycloak.org/) 20 or higher (a Docker Compose file is provided)
- *(Optional, only to re-train the AI models)* Python with scikit-learn / skl2onnx — see
  [`docs/ai-training`](docs/ai-training/README.md). The trained `.onnx` files already live in `DMRS.Api/Ai/`.

## Getting Started

### 1. Clone

```bash
git clone https://github.com/Khair69/DMRS.git
cd DMRS
```

### 2. Databases

The solution uses two PostgreSQL databases:

| Database | Used by | Connection string location |
|---|---|---|
| `DMRS` | `DMRS.Api` | `DMRS.Api/appsettings.json` → `ConnectionStrings:DefaultConnection` |
| `DMRSMedicine` | `DMRS.MedicineInfo.Api` | `DMRS.MedicineInfo.Api/appsettings.json` → `ConnectionStrings:DefaultConnection` |

Update both connection strings (host/port/user/password) to match your local PostgreSQL.

For the main API, create the `DMRS` database and apply the EF Core schema:

```bash
createdb DMRS
cd DMRS.Api
dotnet ef database update
```

`DMRS.MedicineInfo.Api` creates its `DMRSMedicine` database and seeds sample medicine data
automatically on first run (`EnsureCreated`) — no manual migration needed.

### 3. Keycloak

Start Keycloak (a compose file is provided):

```bash
cd docker/keycloak
docker compose up -d
```

Then ensure a realm named **`DMRS`** exists with a client **`dmrs-api`** configured for the
Authorization Code flow (PKCE), with:
- **Valid redirect URIs:** `https://localhost:7099/*`
- **Web origins:** `https://localhost:7099`

Keycloak runs at `http://localhost:8080`. The relevant settings live in
`DMRS.Api/appsettings.json` and `DMRS.Client/wwwroot/appsettings.json`.

### 4. Run the services

Start them in this order (each in its own terminal):

```bash
# 1. Medicine knowledge API  → http://localhost:5041
cd DMRS.MedicineInfo.Api && dotnet run

# 2. Main API                → https://localhost:7029  (Swagger at /swagger)
cd DMRS.Api && dotnet run

# 3. Blazor front-end        → https://localhost:7099
cd DMRS.Client && dotnet run
```

### 5. Seed sample data (development)

With the stack running, load sample FHIR data (Synthea bundles) through the app's dev seeding flow
(`SeedDataController` / the in-app seeding tools). This populates patients, conditions, observations,
medications, encounters, and procedures so the dashboards, CDS, and AI risk cards have data to show.

## AI Models

Three scikit-learn classifiers, exported to ONNX and served by the API, score risk from each patient's
FHIR record. Missing inputs are median-imputed and flagged, so a prediction is always available.

| Model | Predicts | Features (from FHIR) | Training dataset |
|---|---|---|---|
| Diabetes risk | Type-2 diabetes risk | Glucose, diastolic BP, BMI, age | Pima Indians Diabetes |
| Cardiovascular risk | Heart-disease risk | age, sex, resting BP, cholesterol, max HR, fasting blood sugar | UCI Heart Disease |
| Readmission risk | 30-day hospital readmission | age, gender, #conditions, #active meds, #recent visits, #procedures | UCI "130-US hospitals" |

Training scripts, datasets, and the export/verification workflow are documented in
[`docs/ai-training`](docs/ai-training/README.md). Models are consumed by the risk services in
`DMRS.Api/Application/ClinicalDecisionSupport/Services/` and surfaced on the patient chart and the
AI Insights page.

## Documentation

- **[API reference](docs/api-reference.md)** — full HTTP endpoint documentation (FHIR resources, CDS, AI risk, documents, onboarding, admin) for anyone integrating with the API. Interactive Swagger UI is also available at `https://localhost:7029/swagger` in development.
- [Architecture & data model](docs/architecture-and-data-model.md) — system, AI-pipeline, and CDS diagrams plus ERDs for both databases.
- [AI models](docs/ai-models.md) — how the three risk models work, their features, datasets, and limitations.
- [AI training scripts](docs/ai-training/README.md) — how to (re)train and export the models in Google Colab.
- API internals: [FHIR](DMRS.Api/docs/fhir.md) · [authentication](DMRS.Api/docs/authentication.md) · [authorization](DMRS.Api/docs/authorization.md) · [CDS system](DMRS.Api/docs/cds-system.md) · [deployment](DMRS.Api/docs/deployment.md)

## License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.

## Authors

- **Khair** — _Initial work_ — [Khair69](https://github.com/Khair69)

## Acknowledgments

- [HL7 FHIR](https://www.hl7.org/fhir/) — healthcare interoperability standard
- [Firely SDK](https://fire.ly/products/firely-net-sdk/) — .NET FHIR implementation
- [Keycloak](https://www.keycloak.org/) — identity and access management
- [ASP.NET Core in Action](https://www.manning.com/books/asp-net-core-in-action-third-edition) by Andrew Lock

---

**Note:** This is an academic / graduation project. Ensure compliance with relevant healthcare
regulations (HIPAA, GDPR, etc.) before any real-world deployment.
