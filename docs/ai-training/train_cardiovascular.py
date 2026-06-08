"""
DMRS — Cardiovascular (Heart Disease) Risk Model training (Google Colab friendly).

Trains a scikit-learn classifier on the UCI Heart Disease dataset using ONLY the features that DMRS
can recover from a patient's FHIR data at inference time, then exports to ONNX via skl2onnx with the
input tensor named "float_input" (matching the existing high-utilization model so the C#
OnnxOutputParser consumes it unchanged).

Reduced feature set (ORDER MUST MATCH the C# CardiovascularRiskService feature vector):
    [age, sex, trestbps, chol, thalach, fbs]

    sex      : 1 = male, 0 = female   (UCI encoding — note this is the OPPOSITE of the
               high-utilization model, which used female=1; encode per THIS dataset)
    trestbps : resting systolic blood pressure (mm Hg)        -> FHIR LOINC 8480-6
    chol     : serum total cholesterol (mg/dL)                -> FHIR LOINC 2093-3
    thalach  : max heart rate (recorded heart rate used as a documented proxy in DMRS) -> LOINC 8867-4
    fbs      : fasting blood sugar > 120 mg/dL (1/0)          -> derived from glucose Observation

Run order in Colab:
    1. Run the pip install cell.
    2. Provide the dataset (Kaggle API or manual CSV upload — see README).
    3. Run the rest; copy the printed medians into CardiovascularRiskService.cs.
    4. Download cardiovascular_predictor.onnx into DMRS.Api/Ai/.
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
#   !kaggle datasets download -d johnsmith88/heart-disease-dataset --unzip
#   CSV_PATH = "heart.csv"
#
# Option B — upload heart.csv manually via the Colab file panel, then:
CSV_PATH = "heart.csv"

df = pd.read_csv(CSV_PATH)

# heart.csv columns: age, sex, cp, trestbps, chol, fbs, restecg, thalach,
#                    exang, oldpeak, slope, ca, thal, target  (target 1 = disease)
FEATURES = ["age", "sex", "trestbps", "chol", "thalach", "fbs"]
TARGET = "target"

# A few public copies use 0 as a missing marker for chol/trestbps — treat as NaN then impute.
for col in ["trestbps", "chol", "thalach"]:
    df[col] = df[col].replace(0, np.nan)

medians = df[FEATURES].median()
df[FEATURES] = df[FEATURES].fillna(medians)

X = df[FEATURES].astype(np.float32).values
y = df[TARGET].astype(np.int64).values

# --- Cell 3: train ----------------------------------------------------------
X_train, X_test, y_train, y_test = train_test_split(
    X, y, test_size=0.2, random_state=42, stratify=y
)

model = RandomForestClassifier(
    n_estimators=200, max_depth=6, min_samples_leaf=4, random_state=42
)
model.fit(X_train, y_train)

pred = model.predict(X_test)
proba = model.predict_proba(X_test)[:, 1]
print("Accuracy :", round(accuracy_score(y_test, pred), 4))
print("ROC-AUC  :", round(roc_auc_score(y_test, proba), 4))
print(classification_report(y_test, pred, digits=3))

# --- Cell 4: PRINT MEDIANS (copy into CardiovascularRiskService.cs) ----------
print("\n=== Imputation medians (feature order: age, sex, trestbps, chol, thalach, fbs) ===")
for f in FEATURES:
    print(f"  {f:9s} = {float(medians[f]):.3f}")

# --- Cell 5: export to ONNX (force name 'float_input' + real probabilities) --
from skl2onnx import to_onnx
from skl2onnx.common.data_types import FloatTensorType

# Two things matter here so the C# app can use the model:
#   1. initial_types names the input "float_input" (what DMRS sends).
#   2. zipmap=False makes the 2nd output a plain [N, 2] float tensor of REAL
#      probabilities (column 1 = P(heart disease)), instead of a list of dicts.
onnx_model = to_onnx(
    model,
    initial_types=[("float_input", FloatTensorType([None, X_train.shape[1]]))],
    options={id(model): {"zipmap": False}},
    target_opset=12,
)
with open("cardiovascular_predictor.onnx", "wb") as f:
    f.write(onnx_model.SerializeToString())
print("\nWrote cardiovascular_predictor.onnx")

# --- Cell 6: sanity check — ONNX must match sklearn and sit in [0, 1] --------
import onnxruntime as ort

sess = ort.InferenceSession("cardiovascular_predictor.onnx")
input_name = sess.get_inputs()[0].name
print("ONNX input name:", input_name, "(must be: float_input)")

sample = X_test[:3].astype(np.float32)
onnx_label, onnx_proba = sess.run(None, {input_name: sample})
print("ONNX label    :", onnx_label)
print("ONNX  P(pos)  :", onnx_proba[:, 1])               # probability of heart disease
print("sklearn P(pos):", model.predict_proba(sample)[:, 1])
# The two P(pos) rows should match and be between 0 and 1. If they do, you're done.

# In Colab, download the file:
#   from google.colab import files; files.download("cardiovascular_predictor.onnx")

"""
 Accuracy : 0.8098
ROC-AUC  : 0.9255
              precision    recall  f1-score   support

           0      0.808     0.800     0.804       100
           1      0.811     0.819     0.815       105

    accuracy                          0.810       205
   macro avg      0.810     0.810     0.810       205
weighted avg      0.810     0.810     0.810       205


=== Imputation medians (feature order: age, sex, trestbps, chol, thalach, fbs) ===
  age       = 56.000
  sex       = 1.000
  trestbps  = 130.000
  chol      = 240.000
  thalach   = 152.000
  fbs       = 0.000

Wrote cardiovascular_predictor.onnx
ONNX input name: float_input (must be: float_input)
ONNX label    : [1 1 1]
ONNX  P(pos)  : [0.17051356 0.2603602  0.16992748]
sklearn P(pos): [0.17051361 0.26036027 0.16992749]
'\nTHE RESULTS\nAccuracy : 0.8098\nROC-AUC  : 0.9255\n              precision    recall  f1-score   support\n\n           0      0.808     0.800     0.804       100\n           1      0.811     0.819     0.815       105\n\n    accuracy                          0.810       205\n   macro avg      0.810     0.810     0.810       205\nweighted avg      0.810     0.810     0.810       205\n\n\n=== Imputation medians (feature order: age, sex, trestbps, chol, thalach, fbs) ===\n  age       = 56.000\n  sex       = 1.000\n  trestbps  = 130.000\n  chol      = 240.000\n  thalach   = 152.000\n  fbs       = 0.000\n\nWrote cardiovascular_predictor.onnx\nONNX input name: X (expected: float_input)\nONNX label output : [1 1 1]\nONNX proba output : [{0: -0.1705135554075241, 1: 0.1705135554075241}, {0: -0.2603602111339569, 1: 0.2603602111339569}, {0: -0.1699274778366089, 1: 0.1699274778366089}]\n'
"""
