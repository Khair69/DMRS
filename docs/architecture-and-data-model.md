# DMRS — Architecture & Data Model

Diagrams for the report and defense. They render automatically on GitHub (Mermaid).

---

## 1. System architecture

```mermaid
flowchart LR
    subgraph Client["DMRS.Client — Blazor WebAssembly (:7099)"]
        UI["Clinical workspace · CDS Admin · AI Insights · Patient chart"]
    end

    subgraph Auth["Keycloak (:8080)"]
        KC["OAuth2 / OpenID Connect<br/>realm: DMRS"]
    end

    subgraph Api["DMRS.Api — ASP.NET Core 10 (:7029)"]
        FHIR["FHIR R5 controllers<br/>(CRUD · search · history · validation)"]
        CDS["CDS engine<br/>(Hooks · JSON-Logic rules · cards)"]
        AISVC["AI risk services<br/>(Diabetes · Cardiovascular · Readmission)"]
        DOCS["Patient documents"]
    end

    ONNX["ONNX models<br/>DMRS.Api/Ai/*.onnx"]
    MED["DMRS.MedicineInfo.Api (:5041)"]
    RX["RxNorm REST API<br/>(optional live provider)"]

    PG[("PostgreSQL — DMRS")]
    PGMED[("PostgreSQL — DMRSMedicine")]

    UI -->|"login (OIDC)"| KC
    UI -->|"HTTPS + JWT"| FHIR
    UI --> CDS
    UI --> AISVC
    UI --> DOCS

    FHIR -->|"JWT validation"| KC
    FHIR --> PG
    CDS --> PG
    AISVC -->|"InferenceSession"| ONNX
    AISVC --> PG
    CDS -->|"drug knowledge"| MED
    CDS -.->|"optional"| RX
    MED --> PGMED
    DOCS --> PG
```

The API stores every FHIR resource as raw JSON plus a denormalized search index, secures access with
Keycloak-issued JWTs, and enriches CDS evaluation with both medicine knowledge and the AI risk scores.

---

## 2. AI model pipeline (training → inference)

```mermaid
flowchart LR
    subgraph Train["Offline training (Google Colab)"]
        DS["Public datasets<br/>Pima · UCI Heart · UCI 130-hospitals"]
        SK["scikit-learn<br/>RandomForestClassifier"]
        EXP["skl2onnx export<br/>(float_input, real probabilities)"]
        DS --> SK --> EXP
    end

    EXP -->|".onnx file"| AIDIR["DMRS.Api/Ai/"]

    subgraph Infer["Online inference (DMRS.Api)"]
        FEAT["Feature extraction from FHIR<br/>(Observations, Conditions, Meds, Encounters, Procedures)"]
        SVC["Risk service<br/>(median-impute missing → run model)"]
        CARD["Risk card / CDS variable"]
        FEAT --> SVC --> CARD
    end

    AIDIR -->|"loaded by InferenceSession"| SVC
```

Models are trained on a **reduced feature set** — only the columns DMRS can recover from a patient's
FHIR record — so inference uses real patient data. Missing features are median-imputed and flagged.

---

## 3. CDS Hook evaluation flow

```mermaid
sequenceDiagram
    participant C as Client (EHR action)
    participant H as CdsHooks controller
    participant B as CdsContextBuilder
    participant F as FHIR repository
    participant A as AI risk service
    participant K as Medicine knowledge
    participant E as Rule engine (JSON-Logic)

    C->>H: POST /cds-services/{hook} (patient context)
    H->>B: build evaluation context
    B->>F: patient clinical data
    B->>A: readmission risk (ai.* variables)
    B->>K: drug dosing / safety
    B-->>H: variables
    H->>E: evaluate active published rules
    E-->>H: matched rules → cards
    H-->>C: CDS cards (warnings / info)
```

---

## 4. ERD — `DMRS` database (main API)

```mermaid
erDiagram
    FhirResource ||--o{ FhirResourceVersion : "version history (ResourceType+Id)"
    FhirResource ||--o{ ResourceIndex : "search index (ResourceType+ResourceId)"
    CdsRuleDefinition ||--o{ CdsRuleVersion : "RuleDefinitionId (cascade)"

    FhirResource {
        string ResourceType PK
        string Id PK
        int VersionId "concurrency token"
        bool IsDeleted
        datetime LastUpdated
        string RawContent "FHIR JSON"
    }

    FhirResourceVersion {
        string ResourceType PK
        string Id PK
        int VersionId PK
        datetime LastUpdated
        string RawContent "FHIR JSON"
    }

    ResourceIndex {
        int Id PK
        string ResourceType
        string ResourceId
        string SearchParamCode
        string Value
    }

    CdsRuleDefinition {
        guid Id PK
        string HookId
        string Name
        string Status "Draft/Published/Archived"
        bool IsActive
        bool HasUnpublishedChanges
        guid PublishedVersionId FK "-> CdsRuleVersion"
        int Priority
        string ExpressionJson "jsonb"
        string CardTemplateJson "jsonb"
        datetime CreatedAt
        datetime UpdatedAt
    }

    CdsRuleVersion {
        guid Id PK
        guid RuleDefinitionId FK
        int VersionNumber
        string HookId
        string Name
        int Priority
        string ExpressionJson "jsonb"
        string CardTemplateJson "jsonb"
        bool IsActive
        datetime PublishedAt
    }

    MedicineKnowledgeRecord {
        string RxCui PK
        string Name
        decimal MaxDailyMg
        decimal MaxSingleMg
        decimal WarningThresholdMg
        string PregnancyCategory
        bool IsControlled
        string IngredientCodesJson "jsonb"
        string IndicationCodesJson "jsonb"
        string Source
        datetime FetchedAt
        datetime ExpiresAt
    }
```

**Notes**
- `FhirResource`/`FhirResourceVersion`/`ResourceIndex` relationships are **logical** (matched on
  `ResourceType` + id) — no enforced foreign keys, which keeps the generic FHIR store flexible.
- `CdsRuleDefinition.PublishedVersionId` also points at the published `CdsRuleVersion` (FK, set-null on delete).
- `MedicineKnowledgeRecord` is a time-bounded cache (`FetchedAt`/`ExpiresAt`) of drug knowledge.

---

## 5. ERD — `DMRSMedicine` database (DMRS.MedicineInfo.Api)

```mermaid
erDiagram
    Medicine }o--o{ Ingredient : "contains (many-to-many)"

    Medicine {
        string RxCui PK
        string Name
        decimal Dosing_MaxDailyMg "owned"
        decimal Dosing_MaxSingleMg "owned"
        decimal Dosing_WarningThreshold "owned"
        string Safety_PregnancyCategory "owned"
        bool Safety_IsControlled "owned"
        list Indications "value collection"
    }

    Ingredient {
        int Id PK
        string Code "UNII"
        string Name
    }
```

**Notes**
- `Dosing` and `Safety` are EF Core **owned types** — flattened into columns on the `Medicine` table.
- `Medicine` ↔ `Ingredient` is many-to-many via an EF-generated join table.

---

### Source of truth
- Main schema: `DMRS.Api/Infrastructure/Persistence/AppDbContext.cs` + `DMRS.Api/Migrations/`
- Domain entities: `DMRS.Api/Domain/` and `DMRS.MedicineInfo.Api/Domain/`
