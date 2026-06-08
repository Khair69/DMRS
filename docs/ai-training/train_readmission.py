"""
DMRS — Readmission Risk Model training (Google Colab friendly).

Replaces the old 2-feature (age+gender) "high utilization" predictor with a real, model-driven
predictor of 30-day hospital readmission, trained on the UCI "Diabetes 130-US hospitals" dataset
using ONLY features DMRS can recover from a patient's FHIR record at inference time.

Reduced feature set (ORDER MUST MATCH the C# HighUtilizationRiskService feature vector):
    [age, gender, conditionCount, medicationCount, recentEncounterCount, procedureCount]

    age                 : years (dataset stores 10-year buckets -> mapped to midpoints)
    gender              : Female = 1, Male = 0   (matches the C# ToGenderFeature encoding)
    conditionCount      : number_diagnoses            -> FHIR Condition count
    medicationCount     : num_medications             -> FHIR active MedicationRequest count
    recentEncounterCount: number_inpatient + number_emergency + number_outpatient (prior year)
                                                       -> FHIR Encounter count
    procedureCount      : num_procedures               -> FHIR Procedure count

Target: readmitted within 30 days (the dataset's "<30" class) = 1, else 0.

Run order in Colab:
    1. Run the pip install cell.
    2. Provide the dataset (Kaggle or manual upload of diabetic_data.csv — see README).
    3. Run the rest; copy the printed medians into HighUtilizationRiskService.cs.
    4. Download readmission_predictor.onnx into DMRS.Api/Ai/.
"""

# --- Cell 1: dependencies (run once in Colab) -------------------------------
# !pip install -q scikit-learn skl2onnx onnxruntime pandas kaggle

import numpy as np
import pandas as pd
from sklearn.ensemble import RandomForestClassifier
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

# The dataset uses "?" for missing values.
df = df.replace("?", np.nan)

# age is stored as 10-year buckets like "[70-80)" — map each to its midpoint.
age_midpoints = {
    "[0-10)": 5, "[10-20)": 15, "[20-30)": 25, "[30-40)": 35, "[40-50)": 45,
    "[50-60)": 55, "[60-70)": 65, "[70-80)": 75, "[80-90)": 85, "[90-100)": 95,
}
df["age_years"] = df["age"].map(age_midpoints)

# gender: Female = 1, Male = 0 (drop the few "Unknown/Invalid" rows).
df = df[df["gender"].isin(["Female", "Male"])].copy()
df["gender_feat"] = (df["gender"] == "Female").astype(int)

# prior-year visits = inpatient + emergency + outpatient.
df["prior_visits"] = (
    df["number_inpatient"] + df["number_emergency"] + df["number_outpatient"]
)

# Assemble the 6 features in the exact order the C# service feeds them.
df["conditionCount"] = df["number_diagnoses"]
df["medicationCount"] = df["num_medications"]
df["procedureCount"] = df["num_procedures"]

FEATURES = ["age_years", "gender_feat", "conditionCount", "medicationCount",
            "prior_visits", "procedureCount"]
# Friendly names for the printout (same order).
FEATURE_LABELS = ["age", "gender", "conditionCount", "medicationCount",
                  "recentEncounterCount", "procedureCount"]

# Target: readmitted within 30 days.
df["target"] = (df["readmitted"] == "<30").astype(int)

medians = df[FEATURES].median()
df[FEATURES] = df[FEATURES].fillna(medians)

X = df[FEATURES].astype(np.float32).values
y = df["target"].astype(np.int64).values

# --- Cell 3: train ----------------------------------------------------------
X_train, X_test, y_train, y_test = train_test_split(
    X, y, test_size=0.2, random_state=42, stratify=y
)

# class_weight balances the classes — only ~11% of admissions are readmitted <30 days.
model = RandomForestClassifier(
    n_estimators=300, max_depth=8, min_samples_leaf=10,
    class_weight="balanced", random_state=42
)
model.fit(X_train, y_train)

pred = model.predict(X_test)
proba = model.predict_proba(X_test)[:, 1]
print("Accuracy :", round(accuracy_score(y_test, pred), 4))
print("ROC-AUC  :", round(roc_auc_score(y_test, proba), 4))
print(classification_report(y_test, pred, digits=3))

# --- Cell 4: PRINT MEDIANS (copy into HighUtilizationRiskService.cs) ---------
print("\n=== Imputation medians (feature order below) ===")
for label, f in zip(FEATURE_LABELS, FEATURES):
    print(f"  {label:22s} = {float(medians[f]):.3f}")

# --- Cell 5: export to ONNX (force name 'float_input' + real probabilities) --
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

# --- Cell 6: sanity check — ONNX must match sklearn and sit in [0, 1] --------
import onnxruntime as ort

sess = ort.InferenceSession("readmission_predictor.onnx")
input_name = sess.get_inputs()[0].name
print("ONNX input name:", input_name, "(must be: float_input)")

sample = X_test[:3].astype(np.float32)
onnx_label, onnx_proba = sess.run(None, {input_name: sample})
print("ONNX label    :", onnx_label)
print("ONNX  P(pos)  :", onnx_proba[:, 1])               # probability of 30-day readmission
print("sklearn P(pos):", model.predict_proba(sample)[:, 1])
# The two P(pos) rows should match and be between 0 and 1. If they do, you're done.

# In Colab, download the file:
#   from google.colab import files; files.download("readmission_predictor.onnx")

"""
THE OUTPUT

Accuracy : 0.6507
ROC-AUC  : 0.6298
              precision    recall  f1-score   support

           0      0.915     0.669     0.773     18082
           1      0.161     0.508     0.245      2271

    accuracy                          0.651     20353
   macro avg      0.538     0.588     0.509     20353
weighted avg      0.831     0.651     0.714     20353


=== Imputation medians (feature order below) ===
  age                    = 65.000
  gender                 = 1.000
  conditionCount         = 8.000
  medicationCount        = 15.000
  recentEncounterCount   = 0.000
  procedureCount         = 1.000

Wrote readmission_predictor.onnx
ONNX input name: float_input (must be: float_input)
ONNX label    : [1 1 1]
ONNX  P(pos)  : [0.4586517  0.2754838  0.48527262]
sklearn P(pos): [0.4586517  0.27548381 0.4852725 ]

"""
