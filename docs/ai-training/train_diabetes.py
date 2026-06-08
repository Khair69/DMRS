"""
DMRS — Diabetes Risk Model training (Google Colab friendly).

Trains a scikit-learn classifier on the Pima Indians Diabetes dataset using ONLY the features that
DMRS can recover from a patient's FHIR Observations at inference time, then exports to ONNX via
skl2onnx with the input tensor named "float_input" (matching the existing high-utilization model so
the C# OnnxOutputParser consumes it unchanged).

Reduced feature set (ORDER MUST MATCH the C# DiabetesRiskService feature vector):
    [Glucose, BloodPressure (diastolic), BMI, Age]

Run order in Colab:
    1. Run the pip install cell.
    2. Provide the dataset (Kaggle API or manual CSV upload — see README).
    3. Run the rest; copy the printed medians into DiabetesRiskService.cs.
    4. Download diabetes_predictor.onnx into DMRS.Api/Ai/.
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
#   !kaggle datasets download -d uciml/pima-indians-diabetes-database --unzip
#   CSV_PATH = "diabetes.csv"
#
# Option B — upload diabetes.csv manually via the Colab file panel, then:
CSV_PATH = "diabetes.csv"

df = pd.read_csv(CSV_PATH)

# Pima columns: Pregnancies, Glucose, BloodPressure, SkinThickness, Insulin,
#               BMI, DiabetesPedigreeFunction, Age, Outcome
FEATURES = ["Glucose", "BloodPressure", "BMI", "Age"]
TARGET = "Outcome"

# In Pima, a value of 0 for these columns is a recording artifact (impossible
# physiologically) and really means "missing" — replace with NaN, then impute.
for col in ["Glucose", "BloodPressure", "BMI"]:
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
    n_estimators=200, max_depth=6, min_samples_leaf=5, random_state=42
)
model.fit(X_train, y_train)

pred = model.predict(X_test)
proba = model.predict_proba(X_test)[:, 1]
print("Accuracy :", round(accuracy_score(y_test, pred), 4))
print("ROC-AUC  :", round(roc_auc_score(y_test, proba), 4))
print(classification_report(y_test, pred, digits=3))

# --- Cell 4: PRINT MEDIANS (copy into DiabetesRiskService.cs) ----------------
print("\n=== Imputation medians (feature order: Glucose, BloodPressure, BMI, Age) ===")
for f in FEATURES:
    print(f"  {f:14s} = {float(medians[f]):.3f}")

# --- Cell 5: export to ONNX (force name 'float_input' + real probabilities) --
from skl2onnx import to_onnx
from skl2onnx.common.data_types import FloatTensorType

# Two things matter here so the C# app can use the model:
#   1. initial_types names the input "float_input" (what DMRS sends).
#   2. zipmap=False makes the 2nd output a plain [N, 2] float tensor of REAL
#      probabilities (column 1 = P(diabetes)), instead of a list of dicts.
onnx_model = to_onnx(
    model,
    initial_types=[("float_input", FloatTensorType([None, X_train.shape[1]]))],
    options={id(model): {"zipmap": False}},
    target_opset=12,
)
with open("diabetes_predictor.onnx", "wb") as f:
    f.write(onnx_model.SerializeToString())
print("\nWrote diabetes_predictor.onnx")

# --- Cell 6: sanity check — ONNX must match sklearn and sit in [0, 1] --------
import onnxruntime as ort

sess = ort.InferenceSession("diabetes_predictor.onnx")
input_name = sess.get_inputs()[0].name
print("ONNX input name:", input_name, "(must be: float_input)")

sample = X_test[:3].astype(np.float32)
onnx_label, onnx_proba = sess.run(None, {input_name: sample})
print("ONNX label    :", onnx_label)
print("ONNX  P(pos)  :", onnx_proba[:, 1])               # probability of diabetes
print("sklearn P(pos):", model.predict_proba(sample)[:, 1])
# The two P(pos) rows should match and be between 0 and 1. If they do, you're done.

# In Colab, download the file:
#   from google.colab import files; files.download("diabetes_predictor.onnx")

"""
THE RESULTS

THE RESULTS
 Accuracy : 0.7338
ROC-AUC  : 0.8157
              precision    recall  f1-score   support

           0      0.776     0.830     0.802       100
           1      0.638     0.556     0.594        54

    accuracy                          0.734       154
   macro avg      0.707     0.693     0.698       154
weighted avg      0.728     0.734     0.729       154


=== Imputation medians (feature order: Glucose, BloodPressure, BMI, Age) ===
  Glucose        = 117.000
  BloodPressure  = 72.000
  BMI            = 32.300
  Age            = 29.000

Wrote diabetes_predictor.onnx
ONNX input name: float_input (must be: float_input)
ONNX label    : [1 1 1]
ONNX  P(pos)  : [0.6957152  0.19275025 0.16971387]
sklearn P(pos): [0.69571565 0.19275027 0.16971386]
'\nTHE RESULTS\nAccuracy : 0.7338\nROC-AUC  : 0.8157\n              precision    recall  f1-score   support\n\n           0      0.776     0.830     0.802       100\n           1      0.638     0.556     0.594        54\n\n    accuracy                          0.734       154\n   macro avg      0.707     0.693     0.698       154\nweighted avg      0.728     0.734     0.729       154\n\n\n=== Imputation medians (feature order: Glucose, BloodPressure, BMI, Age) ===\n  Glucose        = 117.000\n  BloodPressure  = 72.000\n  BMI            = 32.300\n  Age            = 29.000\n\nWrote diabetes_predictor.onnx\nONNX input name: X (expected: float_input)\nONNX label output : [1 1 1]\nONNX proba output : [{0: -0.6957151889801025, 1: 0.6957151889801025}, {0: -0.1927502453327179, 1: 0.1927502453327179}, {0: -0.16971386969089508, 1: 0.16971386969089508}]\n'
"""