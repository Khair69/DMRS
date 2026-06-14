# AI Risk Models

DMRS layers three machine-learning models on top of the FHIR data to give clinicians predictive,
patient-specific risk scores. All three follow the same design and serving pattern.

## Models at a glance

| Model | Predicts | Training dataset | Test accuracy | ROC-AUC |
|---|---|---|---|---|
| Diabetes risk | Type-2 diabetes risk | Pima Indians Diabetes | 0.73 | 0.82 |
| Cardiovascular risk | Coronary heart-disease risk | UCI Heart Disease | 0.81 | 0.93 |
| Readmission risk | 30-day hospital readmission | UCI "130-US hospitals" | 0.65 | 0.63 |

(Readmission is intentionally a harder target — see [Limitations](#limitations-and-ethics).)

## Design philosophy: features the system can actually observe

A model is only useful in the app if its inputs can be recovered from a real patient record. So each
model is trained on a **reduced feature set** — only the columns DMRS can extract from a patient's
FHIR data — rather than the full research dataset. This keeps training honest: the model learns from
the same kind of data it will see at inference.

| Model | Features (training order) | FHIR source |
|---|---|---|
| Diabetes | Glucose, diastolic BP, BMI, age | Observations (LOINC 2339-0/2345-7, 8462-4, 39156-5) + Patient.birthDate |
| Cardiovascular | age, sex, resting systolic BP, total cholesterol, max heart rate, fasting blood sugar | Patient + Observations (8480-6, 2093-3, 8867-4, glucose) |
| Readmission | age, gender, #conditions, #active meds, #recent visits, #procedures | Patient + counts of Condition / MedicationRequest / Encounter / Procedure |

> Note on encoding: the cardiovascular model uses the UCI dataset's `sex` (male=1/female=0); the
> diabetes and readmission models use female=1/male=0. Each is encoded to match its own training data.

## Training pipeline (offline, Google Colab)

Scripts live in [`docs/ai-training`](ai-training/README.md), one per model. Each:

1. Loads the public dataset and selects the reduced feature columns.
2. Trains a scikit-learn `RandomForestClassifier`.
3. Prints accuracy, ROC-AUC, and the **per-feature medians** (used for imputation in C#).
4. Exports to ONNX with `skl2onnx`, forcing the input name to `float_input` and disabling ZipMap so
   the model emits a real `[N, 2]` probability tensor.
5. Verifies the ONNX output matches scikit-learn and lies in `[0, 1]`.

The resulting `.onnx` files are placed in `DMRS.Api/Ai/`.

## Inference (online, in DMRS.Api)

The risk services live in `DMRS.Api/Application/ClinicalDecisionSupport/Services/`
(`DiabetesRiskService`, `CardiovascularRiskService`, `HighUtilizationRiskService` = readmission).
Each one:

1. Loads the patient and the relevant FHIR resources.
2. Builds the feature vector. Observation values are pulled by LOINC code via
   `ObservationFeatureExtractor` (which also reads component values, e.g. systolic/diastolic inside a
   blood-pressure panel).
3. **Imputes** any missing feature with the training-set median and flags `FeaturesComplete = false`.
4. Runs the ONNX model (output parsing shared in `OnnxOutputParser`; the input name is read from the
   model's own metadata, so `X` vs `float_input` never breaks it).
5. Maps the probability to a tier (Low < 0.35 ≤ Medium < 0.65 ≤ High) and returns the assessment.

**Graceful degradation:** if a model file is missing the service returns `null` instead of throwing,
and the API/UI simply show "not available" — the app never crashes because a model hasn't been trained.

## How users see it

- **Patient chart** (`Pages/Patients/Details.razor`) — a card per model with the probability,
  tier, the feature values used, and an "estimated/imputed" note when inputs were missing.
- **AI Insights page** — a model catalog plus, for each of the three models, the cohort risk
  distribution and a top-risk watchlist (scored via the per-model `/batch` endpoints), a shared
  "How Scoring Works" explainer, and population-health condition prevalence.
- **Endpoints** — per patient: `GET /cds/risk/diabetes/{id}`, `/cds/risk/cardiovascular/{id}`,
  `/cds/risk/high-utilization/{id}` (readmission). Whole-cohort batch (one request, used by the AI
  Insights page): `GET /cds/risk/diabetes/batch`, `/cds/risk/cardiovascular/batch`,
  `/cds/risk/high-utilization/batch`.

## CDS integration

The readmission score is also exposed to the **CDS rule engine** as variables (`ai.highUtilizationRisk`,
`ai.highUtilizationProbability`, `ai.compositeScore`, `ai.riskLevel`, …) via `CdsContextBuilder`, so
authored rules can fire cards based on AI risk (e.g. a "Readmission Risk" rule template).

## Limitations and ethics

- The models are trained on **public research datasets**, not on this system's own patient population,
  so scores are **illustrative**, not clinically validated.
- **Readmission** is a genuinely hard prediction; ~0.63 AUC is in the expected range for these
  features. Class weighting is used because only ~11% of admissions are 30-day readmissions.
- Imputing missing features with medians lets a score always render, but a heavily-imputed score is
  less reliable — hence the explicit `FeaturesComplete` flag surfaced in the UI.
- These scores are **decision support**, intended to assist — not replace — clinical judgment.
