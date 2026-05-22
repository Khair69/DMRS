# CDS Testing Guide

This guide tests the CDS work in seven layers:

1. `database + startup`
2. `medicine knowledge sync`
3. `template-driven rule authoring`
4. `draft/publish rule lifecycle`
5. `rule validation + preview`
6. `live CDS execution`
7. `ai high-utilization risk signal`

Use the ready-made requests in [DMRS.Api.http](/D:/Code/ASP/DMRS/DMRS.Api/DMRS.Api.http).
For the full architecture and change summary, see [cds-system.md](/D:/Code/ASP/DMRS/DMRS.Api/docs/cds-system.md).

## Prerequisites

- PostgreSQL running for `DMRS.Api`
- Keycloak token already available
- Token scopes broad enough for the new CDS controllers
- `DMRS.MedicineInfo.Api` running on `http://localhost:5041`
- `DMRS.Api` running on `http://localhost:5210`

Important auth note:

- The CDS authorization policy checks scopes against the controller name, not only the route.
- If CDS calls return `403`, add scopes like:
  - `user/cdsmedicineknowledge.read`
  - `user/cdsmedicineknowledge.write`
  - `user/cdsrules.read`
  - `user/cdsrules.write`
  - `user/cdshooks.write`
- For FHIR setup requests, also make sure your token covers:
  - `user/patient.write`
  - `user/allergyintolerance.write`
  - `user/medicationrequest.write`

## Startup

Run the migration first:

```powershell
dotnet ef database update --project D:\Code\ASP\DMRS\DMRS.Api\DMRS.Api.csproj --startup-project D:\Code\ASP\DMRS\DMRS.Api\DMRS.Api.csproj
```

Start the mock medicine API:

```powershell
dotnet run --project D:\Code\ASP\DMRS\DMRS.MedicineInfo.Api\DMRS.MedicineInfo.Api.csproj
```

Start the main API:

```powershell
dotnet run --project D:\Code\ASP\DMRS\DMRS.Api\DMRS.Api.csproj
```

## Step-by-Step Checks

### 1. Mock medicine source

Run requests `1` and `2`.

Expected:

- `200 OK`
- medicine payload includes `rxCui`, `name`, `dosing`, `safety`, `ingredients`, `indications`

If this fails, stop. The CDS medicine sync depends on it.

### 2. Local medicine knowledge sync

Run requests `3` to `6`.

Expected:

- `refresh` stores a normalized medicine record in DMRS
- `GET /cds/medications/161` returns Acetaminophen
- search by `q=acetaminophen` returns Acetaminophen
- search by ingredient `UNII-L960UP28W1` returns Acetaminophen and Percocet

This proves:

- migration applied
- mock provider works
- sync service works
- normalized search works

### 3. Template-driven authoring

Run requests `7` to `10`.

Expected:

- variables list still exposes the stable runtime fields
- templates endpoint lists supported starter templates
- compile endpoint returns a normal `CdsRuleDefinition`
- create-from-template persists a rule without needing manual `ExpressionJson`

This proves:

- admins can start from supported medication templates
- templates compile to the same runtime model used by the rule engine

### 4. Rule variables and validation

Before this step, save a rule as a draft and publish it once.

Run requests `11` and `12`.

Expected:

- variables list contains:
  - `patient.ageYears`
  - `medication.rxCui`
  - `medication.ingredients`
  - `dose.requestedDailyMg`
  - `dose.maxDailyMg`
  - `safety.allergyConflict`
  - `therapy.duplicateIngredientConflict`
  - `ai.highUtilizationRisk`
- valid rule returns `isValid = true`
- invalid rule returns `isValid = false`

This proves:

- enriched context contract is published
- rule validator is active

### 5. Rule preview with dose calculation

Run request `13`.

Expected:

- one card returned
- summary includes `Acetaminophen (Tylenol)`
- detail shows `4500` requested mg/day and `4000` max mg/day

This proves:

- rule preview works
- medication lookup works during preview
- dose derivation works
- placeholder interpolation works

### 6. Persist rule and run live CDS

Run requests `14` to `17`.

Expected:

- draft rule creation returns `201`
- publishing creates a live version snapshot
- rule listing shows draft/published state and version metadata
- services list includes `medication-prescribe`
- live CDS execution returns cards

This proves:

- persistent draft storage works
- publish lifecycle works
- immutable version history works
- runtime hook evaluation works

### 9. AI high-utilization risk signal

Test the direct AI endpoint and then use the `high-utilization-risk-warning` template in preview.

Expected:

- `GET /cds/risk/high-utilization/{patientId}` returns the patient features, model name, and risk result
- the variable catalog includes `ai.highUtilizationRisk` and `ai.highUtilizationProbability`
- the AI-based template compiles into a normal deterministic rule
- preview can use the AI-enriched context to decide whether to emit a CDS card

This proves:

- the ONNX model is loading
- patient feature extraction is working
- AI output is available to rules without bypassing rule governance

### 7. Allergy enrichment

Run requests `18` to `20`.

Expected:

- patient creation succeeds
- allergy creation succeeds
- allergy rule preview returns one card
- detail contains `UNII-L960UP28W1`

This proves:

- allergy resources are being read from local FHIR storage
- context builder derives `safety.allergyConflict`

### 8. MedicationRequest warmup

Run requests `21` and `22`.

Expected:

- MedicationRequest create succeeds
- `GET /cds/medications/5640` returns Ibuprofen even if you did not manually refresh it first

This proves:

- MedicationRequest write path triggers medicine knowledge warmup

## Failure Guide

- `401 Unauthorized`: bad or missing token
- `403 Forbidden`: token missing CDS controller scopes
- `500` on `/cds/medications/...`: migration missing or mock medicine API down
- preview returns `cards: []`: expression did not match derived context
- allergy test returns no match: allergy code does not exactly match the medicine ingredient code

## Pass Criteria

The CDS work is behaving correctly if all of these pass:

- mock medicine source resolves medicines
- DMRS stores and searches normalized medicine knowledge
- supported rule templates compile into valid persisted CDS rules
- rule validation accepts valid JSON logic and rejects unsupported operators
- preview renders dynamic card text
- live `medication-prescribe` execution returns cards
- allergy matching uses local FHIR allergy data
- MedicationRequest writes warm the medicine knowledge store
