# DMRS AI Model Training

This folder holds the training scripts for the project's ONNX risk models. They are written to run
top-to-bottom in **Google Colab** (the same workflow used for the original high-utilization model)
and export a `.onnx` file using `skl2onnx`, with the input tensor named **`float_input`** so the
existing C# inference code (`HighUtilizationRiskService` / `OnnxOutputParser`) consumes them unchanged.

## Models

| Script | Dataset | Reduced feature set (order matters) | Output file |
|---|---|---|---|
| `train_diabetes.py` | Pima Indians Diabetes (`uciml/pima-indians-diabetes-database`) | `[Glucose, BloodPressure(diastolic), BMI, Age]` | `diabetes_predictor.onnx` |
| `train_cardiovascular.py` | UCI Heart Disease (`johnsmith88/heart-disease-dataset`) | `[age, sex, trestbps, chol, thalach, fbs]` | `cardiovascular_predictor.onnx` |

We deliberately train on a **reduced** feature set — only the columns we can recover from each
patient's FHIR Observations at inference time — so the deployed models score real patient data. See
the feature → LOINC mapping in `DMRS.Api/Application/ClinicalDecisionSupport/Services/`
(`DiabetesRiskService`, `CardiovascularRiskService`).

## How to run (Colab)

1. Open a new Colab notebook.
2. Paste the contents of the script into a cell (or upload the `.py` and `%run` it).
3. Run the first cell to `pip install` dependencies.
4. Provide the dataset:
   - **Option A (Kaggle API):** upload your `kaggle.json` token when prompted; the script downloads the CSV.
   - **Option B (manual):** download the CSV from Kaggle and upload it via the Colab file panel, then
     set `CSV_PATH` at the top of the script.
5. Run the rest. The script prints **accuracy / ROC-AUC** and the **per-feature medians**.
6. **Verify the export (important):** the last cell must print
   - `ONNX input name: float_input` (NOT `X`), and
   - an `ONNX P(pos)` row that **matches** the `sklearn P(pos)` row and is **between 0 and 1**.

   If the input name is `X` or the probabilities look symmetric around zero (e.g. `{0: -0.17, 1: 0.17}`),
   you ran the old export — re-run with the current Cell 5 (it sets `initial_types` + `zipmap=False`).
7. **Copy the printed medians** into the matching C# service's imputation constants.
8. Download the generated `.onnx` and drop it into `DMRS.Api/Ai/`.

## After training

- Place `diabetes_predictor.onnx` and `cardiovascular_predictor.onnx` in `DMRS.Api/Ai/`.
- The `.csproj` copies them to the output directory (see the `<Content>` entries).
- Update the median constants in `DiabetesRiskService.cs` / `CardiovascularRiskService.cs` if they
  differ from the defaults committed there.
