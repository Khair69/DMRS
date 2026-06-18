# DMRS — ERD & Use-Case Diagrams

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
