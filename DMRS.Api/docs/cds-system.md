# CDS System Guide

This document explains the CDS system in `DMRS.Api` as it exists now, including the rule engine, the mock medicine integration, the normalized medicine knowledge layer, the enriched context flow, the admin/testing endpoints, and how to verify the whole stack without getting lost.

Use this together with:

- [cds-testing.md](/D:/Code/ASP/DMRS/DMRS.Api/docs/cds-testing.md)
- [DMRS.Api.http](/D:/Code/ASP/DMRS/DMRS.Api/DMRS.Api.http)

## 1. What the CDS system is trying to do

The CDS implementation is currently an admin-first, rule-driven clinical decision support system for medication prescribing.

Its job is to:

- expose a CDS Hooks-style endpoint for medication decisions
- store rules in the DMRS database instead of hardcoding them
- pull medicine facts from a current source, which is now the local mock medicine API
- normalize those medicine facts inside `DMRS.Api`
- enrich the rule context with medication, dose, and allergy-derived values
- evaluate rules and return CDS cards

The current system is not AI-driven yet. It is a deterministic rule engine with a medicine knowledge pipeline that prepares the system for future AI-assisted authoring or recommendation features.

## 2. Projects involved

There are two important projects in this flow.

### `DMRS.Api`

This is the main system. It contains:

- FHIR resources
- CDS endpoints
- CDS rule storage
- local normalized medicine knowledge persistence
- context enrichment
- authorization

### `DMRS.MedicineInfo.Api`

This is the current medicine source. It provides a mock medication dataset over HTTP and is used as the upstream medicine knowledge provider.

It runs separately and is queried by `DMRS.Api`.

## 3. High-level runtime flow

The main CDS runtime path for a medication decision is:

1. A client calls `POST /cds-services/medication-prescribe`
2. `CdsHooksController` resolves the service definition
3. `CdsHookService` loads active rules for that hook
4. `CdsContextBuilder` builds a CDS context from the request
5. During context building, medication facts may be loaded from normalized local medicine knowledge
6. Allergy data may be pulled from stored `AllergyIntolerance` FHIR resources
7. Dose values may be derived from the incoming `MedicationRequest`
8. Rules are created from stored `CdsRuleDefinition` records
9. Rules are evaluated using the JSON logic evaluator
10. Matching rules render cards through the template renderer
11. The API returns a `cards` response

Important code paths:

- [CdsHooksController.cs](/D:/Code/ASP/DMRS/DMRS.Api/Controllers/ClinicalDecisionSupport/CdsHooksController.cs)
- [CdsHookService.cs](/D:/Code/ASP/DMRS/DMRS.Api/Application/ClinicalDecisionSupport/Services/CdsHookService.cs)
- [CdsContextBuilder.cs](/D:/Code/ASP/DMRS/DMRS.Api/Application/ClinicalDecisionSupport/Services/CdsContextBuilder.cs)
- [RuleEngine.cs](/D:/Code/ASP/DMRS/DMRS.Api/Application/ClinicalDecisionSupport/Services/RuleEngine.cs)
- [CardTemplateRenderer.cs](/D:/Code/ASP/DMRS/DMRS.Api/Application/ClinicalDecisionSupport/Services/CardTemplateRenderer.cs)

## 4. Original CDS foundation that already existed

Before the recent changes, the system already had:

- a CDS service registry
- a `medication-prescribe` hook
- a rule database table
- a JSON-logic-style evaluator
- a card renderer
- a provider abstraction for medicine facts

That earlier foundation is still in place. The recent work extends it rather than replacing it.

## 5. New pieces added in the recent CDS work

The recent implementation added four important areas.

### A. Local normalized medicine knowledge

`DMRS.Api` now stores normalized medicine records in its own database table and uses that table as the CDS runtime source of truth.

Main pieces:

- [MedicineKnowledgeRecord.cs](/D:/Code/ASP/DMRS/DMRS.Api/Domain/ClinicalDecisionSupport/MedicineKnowledgeRecord.cs)
- [MedicineKnowledgeService.cs](/D:/Code/ASP/DMRS/DMRS.Api/Application/ClinicalDecisionSupport/Services/MedicineKnowledgeService.cs)
- migration: [20260428095620_AddMedicineKnowledgeRecords.cs](/D:/Code/ASP/DMRS/DMRS.Api/Migrations/20260428095620_AddMedicineKnowledgeRecords.cs)

This gives DMRS a local store for:

- `RxCui`
- `Name`
- `MaxDailyMg`
- `MaxSingleMg`
- `WarningThresholdMg`
- `PregnancyCategory`
- `IsControlled`
- ingredient codes and names
- indication codes
- source metadata
- fetch and expiry timestamps

### Why there is one table now

Earlier CDS work used a generic cache table called `DrugKnowledgeEntries`. It stored key-value payloads such as ingredients or max dose as JSON blobs.

That design was useful for the first prototype, but it became a bad fit once CDS needed:

- searchable medicine records
- normalized fields for admin rule authoring
- stable context enrichment
- direct runtime access to ingredients, dose limits, and safety flags

The newer `MedicineKnowledgeRecords` table replaces that cache design for CDS. It is now the canonical medicine store used by the CDS runtime and admin APIs. The old `DrugKnowledgeEntries` table is being removed from the runtime and dropped from the schema.

### B. Richer medication context

The CDS context is no longer just the raw incoming hook JSON.

The context builder now derives stable fields for rules:

- `medication.rxCui`
- `medication.name`
- `medication.ingredients`
- `medication.indications`
- `dose.requestedSingleMg`
- `dose.requestedDailyMg`
- `dose.maxDailyMg`
- `dose.maxSingleMg`
- `dose.warningThresholdMg`
- `safety.pregnancyCategory`
- `safety.isControlled`
- `safety.allergyConflict`
- `allergies.codes`
- `allergies.matches`

This is the main feature that makes rules easier to author. Rules no longer need to inspect raw FHIR JSON directly for common medication use cases.

### C. Rule tooling for admins and testing

The system now supports:

- rule validation
- rule preview
- variable discovery
- rule templates

Main pieces:

- [CdsRulesController.cs](/D:/Code/ASP/DMRS/DMRS.Api/Controllers/ClinicalDecisionSupport/CdsRulesController.cs)
- [RuleDefinitionValidator.cs](/D:/Code/ASP/DMRS/DMRS.Api/Application/ClinicalDecisionSupport/Services/RuleDefinitionValidator.cs)
- [CdsVariableCatalog.cs](/D:/Code/ASP/DMRS/DMRS.Api/Application/ClinicalDecisionSupport/Services/CdsVariableCatalog.cs)
- [RuleTemplateService.cs](/D:/Code/ASP/DMRS/DMRS.Api/Application/ClinicalDecisionSupport/Services/RuleTemplateService.cs)

This makes the rule layer safer and easier to test before activation.

The template layer now provides starter rule types that compile into standard `CdsRuleDefinition` records:

- `max-dose-exceeded`
- `allergy-conflict`
- `pregnancy-category-warning`
- `controlled-medication-warning`
- `indication-mismatch`

### D. Dynamic card templates

Card templates now support placeholders such as:

- `{{medication.name}}`
- `{{dose.requestedDailyMg}}`
- `{{dose.maxDailyMg}}`
- `{{allergies.matches}}`

This means a card can display patient-specific and medication-specific values instead of only static text.

## 6. The medicine knowledge flow

The medicine knowledge path now works like this:

1. `DMRS.Api` needs medicine data
2. it asks `IMedicineKnowledgeService`
3. that service first checks `MedicineKnowledgeRecords`
4. if no current record exists, it asks `IKnowledgeProvider`
5. the current provider is usually `MockMedicineKnowledgeProvider`
6. that provider calls `DMRS.MedicineInfo.Api`
7. the result is normalized and stored in `DMRS.Api`
8. later lookups use the local normalized record until expiry

This is important because it means the mock medicine API is an upstream source, while DMRS keeps its own CDS-friendly medicine table locally.

## 7. The warmup behavior on MedicationRequest writes

When a `MedicationRequest` is created or updated through the FHIR API, the controller triggers medicine knowledge warmup.

Main piece:

- [MedicationRequestKnowledgeWarmup.cs](/D:/Code/ASP/DMRS/DMRS.Api/Application/ClinicalDecisionSupport/Services/MedicationRequestKnowledgeWarmup.cs)

That warmup now:

- refreshes normalized medicine knowledge
- warms ingredient knowledge
- warms max dose knowledge

This means the CDS system becomes ready earlier for later evaluations involving that medication.

## 8. Endpoints you now have

### CDS service endpoints

- `GET /cds-services`
- `POST /cds-services/{id}`

Current service:

- `medication-prescribe`

### Rule management endpoints

- `GET /cds/rules`
- `GET /cds/rules/{id}`
- `POST /cds/rules`
- `PUT /cds/rules/{id}`
- `PATCH /cds/rules/{id}/activate`
- `PATCH /cds/rules/{id}/deactivate`
- `POST /cds/rules/validate`
- `POST /cds/rules/preview`
- `GET /cds/rules/variables`
- `GET /cds/rules/templates`
- `POST /cds/rules/templates/compile`
- `POST /cds/rules/templates`

### Medicine knowledge endpoints

- `GET /cds/medications`
- `GET /cds/medications/{value}`
- `POST /cds/medications/{value}/refresh`

## 9. Rule model and how it works

Rules are stored as `CdsRuleDefinition`.

Key fields:

- `HookId`
- `Name`
- `Description`
- `Priority`
- `IsActive`
- `ExpressionJson`
- `CardTemplateJson`

### `ExpressionJson`

This is the condition. It is evaluated by the JSON logic evaluator.

Supported operators currently include:

- `var`
- `==`
- `!=`
- `>`
- `<`
- `>=`
- `<=`
- `and`
- `or`
- `in`

If a rule uses an unsupported operator, validation should reject it.

### `CardTemplateJson`

This is the response card definition.

Current supported fields:

- `summary`
- `detail`
- `indicator`
- `source.label`
- `source.url`

These fields may include placeholder expressions wrapped in `{{...}}`.

## 10. How allergy matching works now

Allergy matching is now derived during context building.

The flow is:

1. the hook context provides a patient id
2. `CdsContextBuilder` searches stored `AllergyIntolerance` resources for that patient
3. allergy codes are collected from `AllergyIntolerance.code`
4. the current medication’s ingredient codes are loaded from local medicine knowledge
5. ingredient codes are compared with allergy codes
6. the result is exposed as:
   - `safety.allergyConflict`
   - `allergies.codes`
   - `allergies.matches`

This is a simple but useful deterministic mechanism for ingredient-based contraindication rules.

## 11. How dose derivation works now

Dose derivation happens from the incoming `MedicationRequest` when possible.

The current logic looks for:

- `dosageInstruction`
- `doseAndRate`
- `doseQuantity.value`
- `doseQuantity.unit/code`
- timing repeat values such as frequency and period

The implementation currently expects dose quantities in `mg` when deriving the numerical comparison fields.

That means:

- `requestedSingleMg` is based on the dose quantity
- `requestedDailyMg` is based on single dose times inferred daily frequency

This is enough for the current max-dose testing flow, but it is still a first version.

## 12. Authentication and authorization gotcha

This is the most important operational detail for testing.

All CDS endpoints use the `FhirScope` policy, and that policy checks scopes using the controller name.

That means the new CDS controllers may require scopes like:

- `user/cdsmedicineknowledge.read`
- `user/cdsmedicineknowledge.write`
- `user/cdsrules.read`
- `user/cdsrules.write`
- `user/cdshooks.write`

This is why a token that works for the FHIR resource endpoints may still fail with `403` on the CDS endpoints.

Relevant code:

- [AuthorizationServiceExtensions.cs](/D:/Code/ASP/DMRS/DMRS.Api/Infrastructure/Security/AuthorizationServiceExtensions.cs)
- [FhirScopeHandler.cs](/D:/Code/ASP/DMRS/DMRS.Api/Infrastructure/Security/FhirScopeHandler.cs)
- [SmartAuthorizationService.cs](/D:/Code/ASP/DMRS/DMRS.Api/Infrastructure/Security/SmartAuthorizationService.cs)

## 13. Testing map

The complete manual test sequence is already written in:

- [cds-testing.md](/D:/Code/ASP/DMRS/DMRS.Api/docs/cds-testing.md)
- [DMRS.Api.http](/D:/Code/ASP/DMRS/DMRS.Api/DMRS.Api.http)

Recommended order:

1. apply migration
2. start `DMRS.MedicineInfo.Api`
3. start `DMRS.Api`
4. verify mock medicine source
5. verify `/cds/medications`
6. verify `/cds/rules/variables`
7. verify rule validation
8. verify rule preview
9. persist a rule
10. execute live `medication-prescribe`
11. test allergy enrichment
12. test `MedicationRequest` warmup

## 14. Template authoring path

Rule templates are a new admin-facing layer on top of the raw rule engine.

They do not create a second runtime. Instead:

1. the client sends a `RuleTemplateRequest`
2. the template service compiles that request into a normal `CdsRuleDefinition`
3. the compiled rule can be previewed, validated, stored, and executed exactly like any hand-authored raw rule

This keeps runtime behavior simple while making authoring easier.

The current starter templates are:

- max dose exceeded
- allergy conflict
- pregnancy category warning
- controlled medication warning
- indication mismatch

## 15. What is still limited

The system is much stronger than before, but it is still a first CDS platform version.

Current limitations:

- only one CDS service is registered: `medication-prescribe`
- rule authoring is still template-plus-JSON-based, not a full admin UI model
- dose derivation is currently simple and mg-focused
- there is no advanced terminology normalization beyond the current inputs
- there is no AI runtime decision layer yet
- there is no dedicated audit/history model for rule previews and rule changes beyond normal persistence

## 16. Recommended next implementation areas

If you continue building CDS from here, the next logical steps are:

- add an admin UI for rule authoring on top of the variable catalog and validation endpoints
- add more medication-aware safety checks such as pregnancy warnings and indication mismatch
- expand the template catalog and allow richer per-template parameter validation
- strengthen dose/unit normalization
- add explicit audit logging for CDS rule lifecycle and preview usage
- add AI only as assistive drafting and explanation after the deterministic rule layer is stable

## 17. Practical mental model

If you want one simple way to think about the system, use this:

- `DMRS.MedicineInfo.Api` is the current medicine source
- `DMRS.Api` stores a normalized local medicine view for CDS
- FHIR resources provide patient and allergy context
- `CdsContextBuilder` turns raw input into rule-friendly fields
- `CdsRuleDefinition` stores logic and card output
- the rule engine evaluates those rules and returns CDS cards

That is the current CDS architecture as a whole.
