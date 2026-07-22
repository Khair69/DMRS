using DMRS.Api.Application.ClinicalDecisionSupport.Services;
using DMRS.Api.Domain.Interfaces;
using Hl7.Fhir.Model;
using Microsoft.AspNetCore.Hosting;
using NSubstitute;

namespace DMRS.UnitTests.Ai;

/// <summary>
/// Shared setup for the risk-model tests. Each derived class exercises one predictor against its
/// REAL deployed .onnx file, substituting only the FHIR repository — so the tests cover the whole
/// inference pipeline: feature extraction, imputation, the ONNX Runtime call and output parsing.
/// </summary>
public abstract class RiskModelTestBase
{
    protected const string PatientId = "patient-1";

    // LOINC codes shared across the predictors.
    protected const string GlucoseCode = "2339-0";
    protected const string SystolicBpCode = "8480-6";
    protected const string DiastolicBpCode = "8462-4";
    protected const string BmiCode = "39156-5";
    protected const string CholesterolCode = "2093-3";
    protected const string HeartRateCode = "8867-4";

    protected readonly IFhirRepository Repository = Substitute.For<IFhirRepository>();

    /// <summary>
    /// The shared ONNX session cache the risk services take. In the API this is a singleton, so the
    /// model file is read once rather than per request; the tests use a real pool for the same reason.
    /// </summary>
    protected readonly OnnxModelPool ModelPool = new();

    /// <summary>
    /// The API copies the .onnx models next to the test assembly, so the test output directory
    /// stands in for the API's content root.
    /// </summary>
    protected static IWebHostEnvironment Environment()
    {
        var environment = Substitute.For<IWebHostEnvironment>();
        environment.ContentRootPath.Returns(AppContext.BaseDirectory);
        return environment;
    }

    protected void GivenPatient(Patient patient, params Observation[] observations)
    {
        Repository.GetAsync<Patient>(PatientId).Returns(patient);
        Repository.SearchAsync<Observation>(Arg.Any<Dictionary<string, string>>()).Returns([.. observations]);
    }

    protected void GivenPatient(string birthDate, params Observation[] observations)
        => GivenPatient(new Patient { Id = PatientId, BirthDate = birthDate }, observations);

    protected static Observation Observed(string loincCode, double value) => new()
    {
        Code = new CodeableConcept { Coding = [new Coding("http://loinc.org", loincCode)] },
        Value = new Quantity((decimal)value, "1"),
        Effective = new FhirDateTime("2026-01-15")
    };

    /// <summary>A birth date that yields the given age today, so the tests do not rot over time.</summary>
    protected static string BirthDateForAge(int age) =>
        DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-age).AddDays(-1).ToString("yyyy-MM-dd");
}
