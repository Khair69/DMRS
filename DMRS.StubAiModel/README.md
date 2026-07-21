# DMRS.StubAiModel

A throwaway **stand-in for a remote AI model**, used to demonstrate the Nabd *External AI Models*
feature without needing a real external service. It is intentionally not clinically meaningful.

Nabd POSTs a patient's FHIR `Bundle` to `/predict`; this server counts the resources and returns a
pretend risk decision as JSON.

## Run it

```powershell
dotnet run --project DMRS.StubAiModel
```

It listens on **https://localhost:5005** using the trusted ASP.NET Core localhost dev certificate
(run `dotnet dev-certs https --trust` once if you've never trusted it). That trusted HTTPS is what lets
Nabd — which only allows HTTPS endpoints — call it directly.

Confirm it's up: open https://localhost:5005/ → "Nabd stub AI model is running."

## Register it in Nabd

In the client, go to **Intelligence → External AI Models** (`/external-ai/admin`) as a system/org admin:

| Field | Value |
| ----- | ----- |
| Name | `Stub Risk Model` |
| Endpoint URL | `https://localhost:5005/predict` |
| Auth type | `None` |
| Timeout | `30` |
| Decision JSON path | blank (full response) or `decision` (just `High`/`Medium`/`Low`) |
| Active | ✓ |

Then run it from a patient chart (`/patients/{id}`) or the **AI Insights** page.

## Example response

```json
{
  "decision": "Medium",
  "score": 0.4,
  "rationale": "2 condition(s) and 1 active medication(s) on record.",
  "patient": { "gender": "female", "birthDate": "1965-04-12" },
  "counts": { "Patient": 1, "Condition": 2, "MedicationRequest": 1 },
  "model": "stub-risk-v1"
}
```

## Customising

Edit the scoring in `Program.cs` (the `score`/`label` block) to demo different behaviour, or change the
port in the `app.Run(...)` call (and update the registered Endpoint URL to match).
