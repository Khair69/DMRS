"""
DMRS — Diabetes Risk Model training.

IMPORTANT (2026-06-15 re-export): the previous version trained a RandomForest and
exported it via skl2onnx, which produced a DEGENERATE `probabilities` output —
the two columns came out as negatives of each other (e.g. [-0.011, 0.011]) and
summed to 0 instead of 1, rather than a valid 2-class distribution. It only
"worked" because the C# parser reads column 1, whose value happened to track
P(diabetes) in [0, 1] for the inputs seen so far — fragile, since a future input
could push column 1 outside [0, 1].

This version trains a GradientBoostingClassifier (same family as the readmission
and cardiovascular models) and exports with `options={id(model):{"zipmap":False}}`,
which produces a clean [N, 2] float tensor of REAL probabilities (column 1 =
P(diabetes)) that sum to ~1. The model's clinical behaviour is unchanged in
spirit (high glucose/BMI -> higher risk); this re-export is purely about making
the ONNX output format robust.

Reduced feature set (ORDER MUST MATCH the C# DiabetesRiskService feature vector):
    [Glucose, BloodPressure (diastolic), BMI, Age]

Run locally (no Kaggle account needed) or in Colab:
    python train_diabetes.py
It fetches the canonical Pima Indians Diabetes dataset directly, writes
diabetes_predictor.onnx into ../../DMRS.Api/Ai/ when run from docs/ai-training/,
and prints the imputation medians (reference only) plus an export sanity check.
"""

import io
import os
import urllib.request

import numpy as np
import pandas as pd
from sklearn.ensemble import GradientBoostingClassifier
from sklearn.model_selection import train_test_split
from sklearn.metrics import accuracy_score, roc_auc_score, classification_report

# --- Cell 1: dependencies (Colab) -------------------------------------------
# !pip install -q scikit-learn skl2onnx onnxruntime pandas

# --- Cell 2: load the canonical Pima Indians Diabetes dataset ---------------
# 768 rows, no header. Columns (in file order):
#   Pregnancies, Glucose, BloodPressure, SkinThickness, Insulin,
#   BMI, DiabetesPedigreeFunction, Age, Outcome
PIMA_URL = "https://raw.githubusercontent.com/jbrownlee/Datasets/master/pima-indians-diabetes.data.csv"
COLUMNS = ["Pregnancies", "Glucose", "BloodPressure", "SkinThickness", "Insulin",
           "BMI", "DiabetesPedigreeFunction", "Age", "Outcome"]

raw = urllib.request.urlopen(PIMA_URL, timeout=30).read().decode()
df = pd.read_csv(io.StringIO(raw), header=None, names=COLUMNS)

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
print(f"Rows: {len(df)}   positive rate: {y.mean():.3f}")

# --- Cell 3: train ----------------------------------------------------------
X_train, X_test, y_train, y_test = train_test_split(
    X, y, test_size=0.2, random_state=42, stratify=y
)

# GradientBoosting (like the readmission/cardiovascular models) exports a clean
# [N, 2] probability tensor via skl2onnx; it avoids the symmetric/degenerate
# proba output the old RandomForest export produced.
model = GradientBoostingClassifier(
    n_estimators=200, max_depth=3, learning_rate=0.05, random_state=42
)
model.fit(X_train, y_train)

pred = model.predict(X_test)
proba = model.predict_proba(X_test)[:, 1]
print("Accuracy :", round(accuracy_score(y_test, pred), 4))
print("ROC-AUC  :", round(roc_auc_score(y_test, proba), 4))
print(classification_report(y_test, pred, digits=3))

# --- Cell 4: PRINT MEDIANS (reference only) ---------------------------------
# NOTE: DiabetesRiskService no longer imputes these training medians for missing
# features — it imputes HEALTHY-NORMAL values instead ("absence of a recorded
# abnormality => assume normal"), so a record-less patient reads LOW rather than
# median-of-an-at-risk-cohort. Printed for reference / sanity only.
print("\n=== Training medians (reference; NOT used for imputation) ===")
for f in FEATURES:
    print(f"  {f:14s} = {float(medians[f]):.3f}")

# --- Cell 5: export to ONNX (real probabilities, input 'float_input') -------
from skl2onnx import to_onnx
from skl2onnx.common.data_types import FloatTensorType

# Two things matter so the C# app can use the model:
#   1. initial_types names the input "float_input" (what DMRS sends).
#   2. zipmap=False makes the 2nd output a plain [N, 2] float tensor of REAL
#      probabilities (column 1 = P(diabetes)) that sum to ~1, instead of the old
#      symmetric/degenerate [-x, +x] tensor or a list of dicts.
onnx_model = to_onnx(
    model,
    initial_types=[("float_input", FloatTensorType([None, X_train.shape[1]]))],
    options={id(model): {"zipmap": False}},
    target_opset=12,
)
OUT = os.path.join(os.path.dirname(__file__), "..", "..", "DMRS.Api", "Ai",
                   "diabetes_predictor.onnx")
OUT = os.path.normpath(OUT)
with open(OUT, "wb") as f:
    f.write(onnx_model.SerializeToString())
print("\nWrote", OUT)

# --- Cell 6: sanity check — REAL probabilities (sum to ~1), correct direction -
import onnxruntime as ort

sess = ort.InferenceSession(OUT)
input_name = sess.get_inputs()[0].name
print("ONNX input name:", input_name, "(must be: float_input)")


def risk(vec):
    out = sess.run(None, {input_name: np.array([vec], dtype=np.float32)})
    return float(out[1][0][1])


# The two proba columns must sum to ~1 (not the old symmetric [-x, +x]), and the
# ONNX P(pos) must match sklearn. Feature order: [Glucose, BloodPressure, BMI, Age].
sample = X_test[:3].astype(np.float32)
onnx_label, onnx_proba = sess.run(None, {input_name: sample})
print("proba rows (each must sum to ~1):")
for row in onnx_proba:
    print("  ", row, " sum=", round(float(row.sum()), 6))
print("ONNX  P(pos):", onnx_proba[:, 1])
print("sklearn P(pos):", model.predict_proba(sample)[:, 1])

# Directionality: healthy young patient LOW, glucose/BMI-heavy patient HIGH.
print("HEALTHY young [90,75,23,25]  P(diabetes):", round(risk([90, 75, 23, 25]) * 100, 1), "%")
print("AT-RISK      [185,90,40,55]  P(diabetes):", round(risk([185, 90, 40, 55]) * 100, 1), "%")
print("higher glucose must RAISE risk:",
      round(risk([100, 75, 25, 40]) * 100, 1), "->",
      round(risk([180, 75, 25, 40]) * 100, 1), "%")
print("higher BMI must RAISE risk:",
      round(risk([110, 75, 22, 40]) * 100, 1), "->",
      round(risk([110, 75, 42, 40]) * 100, 1), "%")
