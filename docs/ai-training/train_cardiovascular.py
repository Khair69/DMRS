"""
DMRS — Cardiovascular (Heart Disease) Risk Model training.

IMPORTANT (2026-06-15 rewrite): the previous version trained on the Kaggle
"heart.csv" (johnsmith88) dataset, which is a DUPLICATED/RESAMPLED copy of the
original UCI data (1025 rows cloned from 303). That caused two serious defects:
  1. Train/test leakage (identical rows on both sides) inflated the reported
     ROC-AUC to ~0.92 while the model generalised poorly.
  2. The label semantics in that file are effectively INVERTED, so the model
     learned every relationship backwards — a healthy 26-year-old scored ~88%
     "risk" and a sick 68-year-old scored ~12%. Higher cholesterol LOWERED the
     score; higher max-heart-rate RAISED it. Clinically nonsensical.

This version trains on the AUTHENTIC UCI Cleveland Heart Disease dataset
(303 unique rows, the canonical source) fetched directly from the UCI archive,
with the correct label (num > 0 = disease). Honest hold-out ROC-AUC ~0.83 — no
leakage, and the feature directions are clinically correct.

Reduced feature set (ORDER MUST MATCH the C# CardiovascularRiskService vector):
    [age, sex, trestbps, chol, thalach, fbs]

    sex      : 1 = male, 0 = female   (UCI encoding)
    trestbps : resting systolic blood pressure (mm Hg)        -> FHIR LOINC 8480-6
    chol     : serum total cholesterol (mg/dL)                -> FHIR LOINC 2093-3
    thalach  : max heart rate (bpm)                           -> FHIR LOINC 8867-4
    fbs      : fasting blood sugar > 120 mg/dL (1/0)          -> derived from glucose Observation

Run locally (no Kaggle account needed) or in Colab:
    python train_cardiovascular.py
It writes cardiovascular_predictor.onnx into ../../DMRS.Api/Ai/ when run from
docs/ai-training/, and prints the imputation values to confirm.
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

# --- Cell 2: load the AUTHENTIC UCI Cleveland dataset -----------------------
# The processed.cleveland.data file is the canonical 303-row source. Columns:
#   age sex cp trestbps chol fbs restecg thalach exang oldpeak slope ca thal num
# 'num' is 0 (no disease) or 1-4 (disease severity); target = (num > 0).
UCI_URL = "https://archive.ics.uci.edu/ml/machine-learning-databases/heart-disease/processed.cleveland.data"
COLUMNS = ["age", "sex", "cp", "trestbps", "chol", "fbs", "restecg",
           "thalach", "exang", "oldpeak", "slope", "ca", "thal", "num"]

raw = urllib.request.urlopen(UCI_URL, timeout=30).read().decode()
df = pd.read_csv(io.StringIO(raw), header=None, names=COLUMNS, na_values="?")

FEATURES = ["age", "sex", "trestbps", "chol", "thalach", "fbs"]

# Correct label: any narrowing (num > 0) is disease.
df["target"] = (df["num"].fillna(0) > 0).astype(int)

# Drop exact duplicate rows defensively (the authentic file has none, but this
# guarantees no leakage if the source is ever swapped for a resampled copy).
df = df.drop_duplicates(subset=FEATURES + ["target"]).reset_index(drop=True)

# A few rows use 0 as a missing marker for chol/trestbps — treat as NaN, impute.
for col in ["trestbps", "chol", "thalach"]:
    df[col] = df[col].replace(0, np.nan)

medians = df[FEATURES].median()
df[FEATURES] = df[FEATURES].fillna(medians)

X = df[FEATURES].astype(np.float32).values
y = df["target"].astype(np.int64).values
print(f"Rows after dedup: {len(df)}   positive rate: {y.mean():.3f}")

# --- Cell 3: train ----------------------------------------------------------
X_train, X_test, y_train, y_test = train_test_split(
    X, y, test_size=0.2, random_state=42, stratify=y
)

# GradientBoosting (like the readmission model) exports a clean [N,2] probability
# tensor via skl2onnx; it also avoids the symmetric/degenerate proba output the
# old RandomForest export produced.
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
# NOTE: CardiovascularRiskService no longer imputes these training medians for
# missing features — it imputes age/sex-appropriate HEALTHY-NORMAL values
# instead ("absence of a recorded abnormality => assume normal"), so a patient
# with no labs reads LOW rather than median-of-a-sick-cohort. These are printed
# for reference / sanity only.
print("\n=== Training medians (reference; NOT used for imputation) ===")
for f in FEATURES:
    print(f"  {f:9s} = {float(medians[f]):.3f}")

# --- Cell 5: export to ONNX (real probabilities, input 'float_input') -------
from skl2onnx import to_onnx
from skl2onnx.common.data_types import FloatTensorType

onnx_model = to_onnx(
    model,
    initial_types=[("float_input", FloatTensorType([None, X_train.shape[1]]))],
    options={id(model): {"zipmap": False}},
    target_opset=12,
)
OUT = os.path.join(os.path.dirname(__file__), "..", "..", "DMRS.Api", "Ai",
                   "cardiovascular_predictor.onnx")
OUT = os.path.normpath(OUT)
with open(OUT, "wb") as f:
    f.write(onnx_model.SerializeToString())
print("\nWrote", OUT)

# --- Cell 6: sanity check — must be REAL probabilities, correct direction ----
import onnxruntime as ort

sess = ort.InferenceSession(OUT)
input_name = sess.get_inputs()[0].name
print("ONNX input name:", input_name, "(must be: float_input)")


def risk(vec):
    out = sess.run(None, {input_name: np.array([vec], dtype=np.float32)})
    return float(out[1][0][1])


# Column 1 must be P(disease): healthy-young LOW, sick-elderly HIGH, and the two
# proba columns must sum to ~1 (not the old symmetric [-x, +x]).
raw = sess.run(None, {input_name: np.array([[55, 1, 130, 240, 150, 0]], dtype=np.float32)})
print("proba row (must sum to ~1):", raw[1][0])
print("HEALTHY young [26,1,120,180,194,0] P(disease):", round(risk([26, 1, 120, 180, 194, 0]) * 100, 1), "%")
print("SICK  elderly [68,1,165,300,108,1] P(disease):", round(risk([68, 1, 165, 300, 108, 1]) * 100, 1), "%")
print("higher chol must RAISE risk:",
      round(risk([55, 1, 130, 180, 150, 0]) * 100, 1), "->",
      round(risk([55, 1, 130, 320, 150, 0]) * 100, 1), "%")
