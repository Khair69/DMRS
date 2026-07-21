# Nabd — ERD & Use-Case Diagrams

Source diagrams (Graphviz **DOT**) plus rendered **PNG** and **SVG**. Edit the `.dot` file and
re-render; the visuals are intentionally simple — the **content is verified against the code**.

## Render

```bash
# one file
dot -Tpng erd.dot -o erd.png
dot -Tsvg erd.dot -o erd.svg

# all of them
for f in *.dot; do dot -Tpng "$f" -o "${f%.dot}.png"; dot -Tsvg "$f" -o "${f%.dot}.svg"; done
```

(SVG is best for the report — it scales without blur and you can recolor/move nodes in any vector editor.)

## Print sizing (why the sequence diagrams look the way they do)

كتاب المشروع is A4 portrait with 1701-twip margins, so a figure is placed at **~6.27in wide**.
Word scales the whole image to that width, which means:

> **on-page text size = fontsize × (6.27 ÷ natural width)**

So the diagram's **natural width is the only thing that controls readability**. The width is set by
the **participant header boxes** — *not* by the message text. (Measured: removing every message label
changed the width by 0in; the 7 long class-name headers were the entire 15in.) Hence the sequence
diagrams deliberately:

- keep **5 columns** with **short header labels** (`CdsHooks\nController`, not `CdsHooksController`);
- use a **small header font (10)** — it sets the column pitch — but a **large message font (14)**,
  since the messages are the content;
- put message text in `xlabel`, never `label` (an inline label injects a virtual node at the edge
  midpoint that both widens the drawing and bows the arrow off horizontal);
- grow **downward**: height is free on a portrait page.

Result: ~8.3 × 10.9in natural → placed at 6.27in it is ~8.3in tall (fits one page) with **~10pt**
message text. Insert the **SVG** at 6.27in and do not shrink it further. If you edit a `.dot`, keep
the headers short and the column count at 5 — every extra inch of width shrinks the text on the page.

## Files

| File | Diagram | Book figure |
|---|---|---|
| `erd.dot` | ERD for both databases (`DMRS` + `DMRSMedicine`) | الشكل 3-6 |
| `usecase-practitioner.dot` | Use cases — الطبيب / Practitioner | الشكل 3-1 |
| `usecase-orgadmin.dot` | Use cases — مدير المؤسسة / Org Admin | الشكل 3-2 |
| `usecase-sysadmin.dot` | Use cases — مدير النظام / System Admin | الشكل 3-3 |
| `usecase-patient.dot` | Use cases — المريض / Patient | الشكل 3-4 |
| `usecase-visitor.dot` | Use cases — الزائر / Visitor | الشكل 3-5 |
| `usecase-overview.dot` | System-wide overview (all actors + capability groups) | (bonus) |
| `seq-cds-hook.dot` | Sequence — CDS Hook decision-support evaluation / تقييم دعم القرار | مخطّطات التسلسل |
| `seq-risk-inference.dot` | Sequence — AI risk-model (ONNX) inference / استدلال نماذج المخاطر | مخطّطات التسلسل |
| `seq-invite-claim.dot` | Sequence — patient invite & claim onboarding / الدعوة والربط | مخطّطات التسلسل |

## Verification basis (so the info is exact)

**ERD** — taken field-by-field from:
- `DMRS.Api/Domain/**` entities + `DMRS.Api/Infrastructure/Persistence/AppDbContext.cs`
  (keys, concurrency token, `jsonb` columns, the two CDS FKs: `RuleDefinitionId` cascade and
  `PublishedVersionId` set-null).
- `DMRS.MedicineInfo.Api/Domain/**` + `Infrastructure/AppDbContext.cs` +
  migration `20260410135142_ALL` (owned `Dosing_*`/`Safety_*` columns, `Indications text[]`,
  `MedicineIngredients` join with composite PK/FK).
- `FhirResource → FhirResourceVersion` and `FhirResource → ResourceIndex` are **logical** links
  (matched on `ResourceType`+id, **no enforced FK**) — drawn dashed.
- `MedicineKnowledgeRecord` (in the `DMRS` DB) is a cache populated **over HTTP** from the
  MedicineInfo service — drawn dotted, it is **not** a database relationship.

**Use cases** — actor↔use-case mapping taken from the `@attribute [Authorize(Roles = …)]` on every
Blazor page in `DMRS.Client/Pages/**`. Key points that the diagrams reflect exactly:
- Clinical resource pages allow `ROLE_PRACTITIONER, ROLE_ORG_ADMIN, ROLE_SYSTEM_ADMIN` (not patients).
- Patient portal (`MyHealth`, `MyProfile`) is `ROLE_PATIENT` only; `Welcome` is `[AllowAnonymous]`.
- **CDS Admin (`Cds/Admin`) is `ROLE_SYSTEM_ADMIN, ROLE_ORG_ADMIN`** (API: `CdsRulesController` →
  `Authorize(Policy = "CdsAdmin")`) → CDS authoring appears under **Org Admin** (System Admin inherits it).
- External-AI Admin is `ROLE_SYSTEM_ADMIN, ROLE_ORG_ADMIN`.
- Organizations Index/Create/Edit/History are System-Admin only; `Organizations/Details` adds Org Admin.
- Actor generalization: **System Admin ▷ Org Admin ▷ Practitioner** (clinical pages permit the admins).

**Sequence diagrams** — each step is taken from the runtime call path in the code. To stay readable at
6.27in (see *Print sizing*) each diagram is drawn with 5 lifelines, so a few collaborators share a
column. These are the only abstractions; every step/order below is real:
- `seq-cds-hook` — `IRuleDefinitionRepository` and `IRuleEngine` share the **"Rules Repo + RuleEngine"**
  column (steps 3/4 hit the repository, steps 9-11 the engine). The `CdsContextBuilder` steps 6/7 are
  drawn as self-calls; in code they are outbound calls to `IFhirRepository`,
  `IClinicalKnowledgeService` (MedicineInfo) and `IHighUtilizationRiskService`.
- `seq-invite-claim` — `PatientInviteController` and `PatientClaimController` share the **"Nabd API"**
  column (steps 1-5 are the invite controller, steps 9-14 the claim controller).
- `seq-risk-inference` — `OnnxModelPool` and `InferenceSession` share the **"نموذج ONNX"** column;
  step 2 is `SmartAuthorizationService.ResolveViewPatientIdsAsync`, drawn as a controller self-call.

Full call paths:
- `seq-cds-hook` — `DMRS.Client/Features/MedicationRequests/Services/MedicationDecisionSupportService.cs`
  → `Controllers/ClinicalDecisionSupport/CdsHooksController.cs` → `CdsHookService` (rule repo +
  `CdsContextBuilder` over FHIR/MedicineInfo/AI risk) → `IRuleEngine` (JSON-Logic) → cards +
  `CdsAlertFeed.Enqueue`.
- `seq-risk-inference` — `Controllers/ClinicalDecisionSupport/CdsRiskInsightsController.cs`
  → `SmartAuthorizationService.ResolveViewPatientIdsAsync` (batch scope) → a `*RiskService`
  (`HighUtilization`/`Diabetes`/`Cardiovascular`) → `OnnxModelPool.GetOrLoad` (session cached once)
  → FHIR feature fetch → `InferenceSession.Run` → threshold → `RiskAssessment`.
- `seq-invite-claim` — `Controllers/Security/PatientInviteController.cs` (writes the invite-code
  `Identifier` + builds the Keycloak registration URL) then, after Keycloak registration + redirect to
  `Pages/Patients/Claim.razor`, `Controllers/Security/PatientClaimController.cs` links the account and
  assigns `ROLE_PATIENT`.
