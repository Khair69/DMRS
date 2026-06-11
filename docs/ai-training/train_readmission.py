"""
DMRS — Readmission Risk Model training (v2, spread-aware; Google Colab friendly).

Predicts 30-day hospital readmission from ONLY features DMRS can recover from a
patient's FHIR record at inference time. Feature ORDER MUST MATCH the C#
HighUtilizationRiskService feature vector:

    [age, gender, conditionCount, medicationCount, recentEncounterCount, procedureCount]

    age                 : years (dataset stores 10-year buckets -> midpoints)
    gender              : Female = 1, Male = 0   (matches C# ToGenderFeature)
    conditionCount      : number_diagnoses            -> FHIR Condition count
    medicationCount     : num_medications             -> FHIR active MedicationRequest count
    recentEncounterCount: number_inpatient + number_emergency + number_outpatient
                                                       -> FHIR Encounter count
    procedureCount      : num_procedures               -> FHIR Procedure count

Target: readmitted within 30 days (the dataset's "<30" class) = 1, else 0.

────────────────────────────────────────────────────────────────────────────
WHY v2 (read this — it explains the "everything is Medium" bug)
────────────────────────────────────────────────────────────────────────────
v1 used RandomForestClassifier(class_weight="balanced", max_depth=8,
min_samples_leaf=10, n_estimators=300). On this weak-signal, ~11%-positive
problem that combination pulled EVERY predicted probability to ~0.5 (averaging
300 shallow trees + balancing => regression to the balanced prior). In the app,
with fixed cut-points High>=0.65 / Medium>=0.35, that meant every patient came
out "Medium". The model's whole output range on real patients was ~0.37-0.68.

v2 fixes the score collapse:
  * Uses GradientBoosting (sequential, not averaged) and DROPS class balancing,
    so probabilities keep a usable spread instead of piling up at 0.5.
  * Does NOT calibrate the probabilities to the base rate — that would just
    re-compress them toward ~0.11 and re-create a one-bucket problem. Instead we
    DERIVE the High/Medium cut-points from the model's OWN score distribution
    (Cell 5) and print them, so the three risk tiers are actually populated.

Honest expectation: 30-day readmission is intrinsically hard with these 6
features. ROC-AUC will land around ~0.63-0.66 no matter the model — the data is
only weakly predictive. v2 does not make the model "smart"; it makes the risk
SCORE usable (it spreads + ranks patients) so the watchlist is meaningful.

Run order in Colab:
    1. Run the pip install cell.
    2. Provide the dataset (Kaggle or manual upload of diabetic_data.csv).
    3. Run the rest.
    4. Copy the printed medians into HighUtilizationRiskService.cs (if changed).
    5. Copy the printed THRESHOLDS into appsettings.json
       (Cds:Ai:HighUtilizationRisk:HighRiskThreshold / MediumRiskThreshold).
    6. Download readmission_predictor.onnx into DMRS.Api/Ai/.
"""

# --- Cell 1: dependencies (run once in Colab) -------------------------------
# !pip install -q scikit-learn skl2onnx onnxruntime pandas kaggle

import numpy as np
import pandas as pd
from sklearn.ensemble import GradientBoostingClassifier
from sklearn.model_selection import train_test_split
from sklearn.metrics import accuracy_score, roc_auc_score, classification_report

# --- Cell 2: load the dataset -----------------------------------------------
# Option A — Kaggle API (upload kaggle.json first):
#   from google.colab import files; files.upload()   # pick kaggle.json
#   !mkdir -p ~/.kaggle && cp kaggle.json ~/.kaggle/ && chmod 600 ~/.kaggle/kaggle.json
#   !kaggle datasets download -d brandao/diabetes --unzip
#   CSV_PATH = "diabetic_data.csv"
#
# Option B — upload diabetic_data.csv manually via the Colab file panel, then:
CSV_PATH = "diabetic_data.csv"

df = pd.read_csv(CSV_PATH)
df = df.replace("?", np.nan)

age_midpoints = {
    "[0-10)": 5, "[10-20)": 15, "[20-30)": 25, "[30-40)": 35, "[40-50)": 45,
    "[50-60)": 55, "[60-70)": 65, "[70-80)": 75, "[80-90)": 85, "[90-100)": 95,
}
df["age_years"] = df["age"].map(age_midpoints)

df = df[df["gender"].isin(["Female", "Male"])].copy()
df["gender_feat"] = (df["gender"] == "Female").astype(int)

df["prior_visits"] = (
    df["number_inpatient"] + df["number_emergency"] + df["number_outpatient"]
)

df["conditionCount"] = df["number_diagnoses"]
df["medicationCount"] = df["num_medications"]
df["procedureCount"] = df["num_procedures"]

FEATURES = ["age_years", "gender_feat", "conditionCount", "medicationCount",
            "prior_visits", "procedureCount"]
FEATURE_LABELS = ["age", "gender", "conditionCount", "medicationCount",
                  "recentEncounterCount", "procedureCount"]

df["target"] = (df["readmitted"] == "<30").astype(int)

medians = df[FEATURES].median()
df[FEATURES] = df[FEATURES].fillna(medians)

X = df[FEATURES].astype(np.float32).values
y = df["target"].astype(np.int64).values

# --- Cell 3: train ----------------------------------------------------------
X_train, X_test, y_train, y_test = train_test_split(
    X, y, test_size=0.2, random_state=42, stratify=y
)

# GradientBoosting keeps a real probability spread. NO class_weight balancing
# (that was what flattened v1 to ~0.5). Imbalance is handled downstream by the
# distribution-derived thresholds in Cell 5, not by squashing the scores.
model = GradientBoostingClassifier(
    n_estimators=400,
    max_depth=3,
    learning_rate=0.05,
    subsample=0.8,
    random_state=42,
)
model.fit(X_train, y_train)

proba = model.predict_proba(X_test)[:, 1]
pred = (proba >= 0.5).astype(int)
print("Accuracy (@0.5):", round(accuracy_score(y_test, pred), 4))
print("ROC-AUC        :", round(roc_auc_score(y_test, proba), 4))
print("Score spread   : min={:.3f}  p25={:.3f}  median={:.3f}  p75={:.3f}  max={:.3f}".format(
    proba.min(), np.percentile(proba, 25), np.median(proba),
    np.percentile(proba, 75), proba.max()))

# --- Cell 4: PRINT MEDIANS (copy into HighUtilizationRiskService.cs) ---------
print("\n=== Imputation medians (feature order below) ===")
for label, f in zip(FEATURE_LABELS, FEATURES):
    print(f"  {label:22s} = {float(medians[f]):.3f}")

# --- Cell 5: DERIVE risk thresholds from the score distribution -------------
# The app buckets each patient by the model's probability:
#     score >= HighRiskThreshold   -> "High"
#     score >= MediumRiskThreshold -> "Medium"   else "Low"
# We pick the cut-points as percentiles of the model's OWN scores so the three
# tiers are populated regardless of the absolute scale the model settles on.
# Targets: ~top 15% High, next ~25% Medium, bottom ~60% Low.
all_scores = model.predict_proba(X)[:, 1]
high_threshold = float(np.percentile(all_scores, 85))
medium_threshold = float(np.percentile(all_scores, 60))

print("\n=== Risk thresholds (paste into appsettings.json) ===")
print('  "Cds:Ai:HighUtilizationRisk:HighRiskThreshold"   =', round(high_threshold, 3))
print('  "Cds:Ai:HighUtilizationRisk:MediumRiskThreshold" =', round(medium_threshold, 3))

high = int((all_scores >= high_threshold).sum())
med = int(((all_scores >= medium_threshold) & (all_scores < high_threshold)).sum())
low = int((all_scores < medium_threshold).sum())
n = len(all_scores)
print(f"  resulting split on the training population: "
      f"High={high} ({high/n:.0%})  Medium={med} ({med/n:.0%})  Low={low} ({low/n:.0%})")
print("  NOTE: your live DMRS cohort will differ. After dropping in the .onnx,")
print("  open /ai-insights and nudge the two thresholds if the split looks off.")

# Report at the High cut-point so precision/recall reflect how the app flags risk.
pred_high = (proba >= high_threshold).astype(int)
print("\n=== Classification report at HighRiskThreshold ===")
print(classification_report(y_test, pred_high, digits=3))

# --- Cell 6: export to ONNX (force name 'float_input' + real probabilities) --
from skl2onnx import to_onnx
from skl2onnx.common.data_types import FloatTensorType

onnx_model = to_onnx(
    model,
    initial_types=[("float_input", FloatTensorType([None, X_train.shape[1]]))],
    options={id(model): {"zipmap": False}},
    target_opset=12,
)
with open("readmission_predictor.onnx", "wb") as f:
    f.write(onnx_model.SerializeToString())
print("\nWrote readmission_predictor.onnx")

# --- Cell 7: sanity check — ONNX must match sklearn and spread across [0,1] --
import onnxruntime as ort

sess = ort.InferenceSession("readmission_predictor.onnx")
input_name = sess.get_inputs()[0].name
print("ONNX input name:", input_name, "(must be: float_input)")

sample = X_test[:5].astype(np.float32)
onnx_label, onnx_proba = sess.run(None, {input_name: sample})
print("ONNX  P(pos)  :", np.round(onnx_proba[:, 1], 4))
print("sklearn P(pos):", np.round(model.predict_proba(sample)[:, 1], 4))
# The two P(pos) rows should match. They should NOT all sit near 0.5 — if they
# do, the dataset really has no signal and only relative ranking is meaningful.
"""
Re-run produces fresh numbers; the shape to expect:
  - ROC-AUC around ~0.63-0.66 (the data ceiling for this task).
  - Score spread noticeably wider than v1 (which was ~0.37-0.68); lots of low
    scores with a high tail, instead of everything piled at 0.5.
  - The printed thresholds give a real High/Medium/Low split.
"""

"""
 Accuracy (@0.5): 0.8882
ROC-AUC        : 0.6308
Score spread   : min=0.014  p25=0.081  median=0.101  p75=0.135  max=0.875

=== Imputation medians (feature order below) ===
  age                    = 65.000
  gender                 = 1.000
  conditionCount         = 8.000
  medicationCount        = 15.000
  recentEncounterCount   = 0.000
  procedureCount         = 1.000

=== Risk thresholds (paste into appsettings.json) ===
  "Cds:Ai:HighUtilizationRisk:HighRiskThreshold"   = 0.154
  "Cds:Ai:HighUtilizationRisk:MediumRiskThreshold" = 0.11
  resulting split on the training population: High=15272 (15%)  Medium=25434 (25%)  Low=61057 (60%)
  NOTE: your live DMRS cohort will differ. After dropping in the .onnx,
  open /ai-insights and nudge the two thresholds if the split looks off.

=== Classification report at HighRiskThreshold ===
              precision    recall  f1-score   support

           0      0.902     0.865     0.883     18082
           1      0.191     0.254     0.218      2271

    accuracy                          0.797     20353
   macro avg      0.546     0.559     0.550     20353
weighted avg      0.823     0.797     0.809     20353


Wrote readmission_predictor.onnx
ONNX input name: float_input (must be: float_input)
ONNX  P(pos)  : [0.0955 0.0528 0.1032 0.1549 0.0809]
sklearn P(pos): [0.0955 0.0528 0.1032 0.1549 0.0809]
'\nRe-run produces fresh numbers; the shape to expect:\n  - ROC-AUC around ~0.63-0.66 (the data ceiling for this task).\n  - Score spread noticeably wider than v1 (which was ~0.37-0.68); lots of low\n    scores with a high tail, instead of everything piled at 0.5.\n  - The printed thresholds give a real High/Medium/Low split.\n'
"""
