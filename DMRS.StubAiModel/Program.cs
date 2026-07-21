using System.Text.Json;

// ---------------------------------------------------------------------------------------------------
// DMRS.StubAiModel — a stand-in "remote AI model" for demonstrating the External AI Models feature.
//
// It plays the role of an external model registered in Nabd (Intelligence -> External AI Models):
// Nabd POSTs a patient's FHIR Bundle to /predict, this server inspects the bundle and returns a
// pretend risk decision as JSON. It is NOT a real model — the score is a trivial function of how many
// conditions/medications the patient has, purely so the end-to-end flow can be shown.
//
// Runs on https://localhost:5005 using the trusted ASP.NET Core localhost dev certificate, which is
// why Nabd's HTTPS-only rule is satisfied with no extra setup. Register it in Nabd as:
//     Endpoint URL : https://localhost:5005/predict
//     Auth type    : None
//     Decision path: (blank for full response, or "decision" for just the label)
// ---------------------------------------------------------------------------------------------------

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Receives a patient FHIR Bundle and returns a toy risk decision derived from its contents.
app.MapPost("/predict", async (HttpRequest request) =>
{
    using var reader = new StreamReader(request.Body);
    var body = await reader.ReadToEndAsync();

    var counts = new Dictionary<string, int>();
    string? gender = null;
    string? birthDate = null;

    try
    {
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        if (root.TryGetProperty("entry", out var entries) && entries.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in entries.EnumerateArray())
            {
                if (!entry.TryGetProperty("resource", out var resource)) continue;
                if (!resource.TryGetProperty("resourceType", out var rt)) continue;

                var type = rt.GetString() ?? "Unknown";
                counts[type] = counts.GetValueOrDefault(type) + 1;

                if (type == "Patient")
                {
                    gender = resource.TryGetProperty("gender", out var g) ? g.GetString() : null;
                    birthDate = resource.TryGetProperty("birthDate", out var b) ? b.GetString() : null;
                }
            }
        }
    }
    catch (JsonException)
    {
        return Results.BadRequest(new { error = "Body was not valid JSON." });
    }

    var conditionCount = counts.GetValueOrDefault("Condition");
    var medicationCount = counts.GetValueOrDefault("MedicationRequest");

    // Pretend scoring: more conditions + meds => higher risk. Purely illustrative.
    var score = Math.Min(1.0, (conditionCount * 0.15) + (medicationCount * 0.1));
    var label = score >= 0.6 ? "High" : score >= 0.3 ? "Medium" : "Low";

    var response = new
    {
        decision = label,
        score = Math.Round(score, 2),
        rationale = $"{conditionCount} condition(s) and {medicationCount} active medication(s) on record.",
        patient = new { gender, birthDate },
        counts,
        model = "stub-risk-v1",
        evaluatedAt = DateTimeOffset.UtcNow
    };

    return Results.Json(response);
});

// Liveness probe — handy to confirm the stub is up before registering it.
app.MapGet("/", () => "Nabd stub AI model is running. POST a FHIR bundle to /predict.");

// Fixed port so the URL registered in Nabd (https://localhost:5005/predict) is stable across runs.
app.Run("https://localhost:5005");
