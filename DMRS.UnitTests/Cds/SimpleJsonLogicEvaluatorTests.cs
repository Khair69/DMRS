using DMRS.Api.Application.ClinicalDecisionSupport.Models;
using DMRS.Api.Application.ClinicalDecisionSupport.Services;
using Shouldly;
using System.Text.Json;

namespace DMRS.UnitTests.Cds;

/// <summary>
/// Covers <see cref="SimpleJsonLogicEvaluator"/> — the JsonLogic evaluator behind the clinical
/// decision support rules that clinicians author in the rule workshop. A rule that silently
/// evaluates to false shows no alert at all, so a mistake here is invisible at runtime: these tests
/// pin each operator, and in particular what happens with missing or non-numeric data.
/// </summary>
public class SimpleJsonLogicEvaluatorTests
{
    private readonly SimpleJsonLogicEvaluator _evaluator = new();

    private static CdsContext ContextWith(params (string Key, object? Value)[] data)
        => CdsContext.Create(
            hookId: "patient-view",
            hookInstance: Guid.NewGuid().ToString(),
            patientId: "patient-1",
            data: data.ToDictionary(d => d.Key, d => d.Value),
            rawContext: default,
            prefetch: null);

    private bool Evaluate(string expression, params (string Key, object? Value)[] data)
        => _evaluator.Evaluate(expression, ContextWith(data));

    // ---------------------------------------------------------------- malformed / empty input

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void An_empty_expression_never_fires(string? expression)
    {
        _evaluator.Evaluate(expression!, ContextWith()).ShouldBeFalse();
    }

    [Fact]
    public void An_unknown_operator_does_not_fire()
    {
        Evaluate("""{"regex": ["a", "b"]}""").ShouldBeFalse();
    }

    // ---------------------------------------------------------------- variable resolution

    [Fact]
    public void A_variable_resolves_from_the_context()
    {
        Evaluate("""{"var": "age"}""", ("age", 42)).ShouldBeTrue();
    }

    [Fact]
    public void A_missing_variable_resolves_to_its_fallback()
    {
        Evaluate("""{">": [{"var": ["glucose", 200]}, 150]}""").ShouldBeTrue();
    }

    [Fact]
    public void A_missing_variable_without_a_fallback_does_not_fire()
    {
        Evaluate("""{">": [{"var": "glucose"}, 150]}""").ShouldBeFalse();
    }

    [Fact]
    public void A_nested_variable_path_is_traversed()
    {
        var nested = new Dictionary<string, object?> { ["value"] = 180m };

        Evaluate("""{">": [{"var": "observation.value"}, 150]}""", ("observation", nested)).ShouldBeTrue();
    }

    [Fact]
    public void A_nested_path_into_missing_data_does_not_fire()
    {
        var nested = new Dictionary<string, object?> { ["value"] = 180m };

        Evaluate("""{">": [{"var": "observation.missing.deeper"}, 150]}""", ("observation", nested)).ShouldBeFalse();
    }

    [Fact]
    public void A_variable_path_resolves_through_raw_json_data()
    {
        // Prefetched FHIR data arrives as JsonElement rather than as a dictionary.
        using var doc = JsonDocument.Parse("""{"valueQuantity": {"value": 9.1}}""");

        Evaluate("""{">": [{"var": "observation.valueQuantity.value"}, 7]}""",
            ("observation", doc.RootElement.Clone())).ShouldBeTrue();
    }

    // ---------------------------------------------------------------- comparison operators

    [Theory]
    [InlineData("""{">":  [{"var": "v"}, 100]}""", 120, true)]
    [InlineData("""{">":  [{"var": "v"}, 100]}""", 100, false)]
    [InlineData("""{">=": [{"var": "v"}, 100]}""", 100, true)]
    [InlineData("""{"<":  [{"var": "v"}, 100]}""", 80, true)]
    [InlineData("""{"<=": [{"var": "v"}, 100]}""", 100, true)]
    [InlineData("""{"<":  [{"var": "v"}, 100]}""", 100, false)]
    public void Numeric_comparisons_respect_their_boundaries(string expression, int value, bool expected)
    {
        Evaluate(expression, ("v", value)).ShouldBe(expected);
    }

    [Fact]
    public void A_numeric_string_is_compared_as_a_number()
    {
        Evaluate("""{">": [{"var": "v"}, 100]}""", ("v", "120")).ShouldBeTrue();
    }

    /// <summary>
    /// A non-numeric value on either side makes the comparison false rather than throwing. This is
    /// deliberate: one malformed observation must not fail the whole decision support request and
    /// take the patient chart down with it.
    /// </summary>
    [Fact]
    public void Comparing_a_non_numeric_value_does_not_fire_and_does_not_throw()
    {
        Should.NotThrow(() => Evaluate("""{">": [{"var": "v"}, 100]}""", ("v", "not-a-number")))
            .ShouldBeFalse();
    }

    [Fact]
    public void Equality_compares_values()
    {
        Evaluate("""{"==": [{"var": "gender"}, "female"]}""", ("gender", "female")).ShouldBeTrue();
        Evaluate("""{"==": [{"var": "gender"}, "female"]}""", ("gender", "male")).ShouldBeFalse();
        Evaluate("""{"!=": [{"var": "gender"}, "female"]}""", ("gender", "male")).ShouldBeTrue();
    }

    // ---------------------------------------------------------------- logical operators

    [Fact]
    public void And_requires_every_branch()
    {
        const string bothConditions = """
            {"and": [
                {">": [{"var": "glucose"}, 140]},
                {">": [{"var": "bmi"}, 30]}
            ]}
            """;

        Evaluate(bothConditions, ("glucose", 180), ("bmi", 34)).ShouldBeTrue();
        Evaluate(bothConditions, ("glucose", 180), ("bmi", 22)).ShouldBeFalse();
        Evaluate(bothConditions, ("glucose", 100), ("bmi", 34)).ShouldBeFalse();
    }

    [Fact]
    public void Or_requires_a_single_branch()
    {
        const string eitherCondition = """
            {"or": [
                {">": [{"var": "glucose"}, 200]},
                {">": [{"var": "bmi"}, 30]}
            ]}
            """;

        Evaluate(eitherCondition, ("glucose", 100), ("bmi", 34)).ShouldBeTrue();
        Evaluate(eitherCondition, ("glucose", 100), ("bmi", 22)).ShouldBeFalse();
    }

    [Fact]
    public void Logical_operators_nest()
    {
        // "elderly OR (diabetic AND hypertensive)"
        const string rule = """
            {"or": [
                {">=": [{"var": "age"}, 75]},
                {"and": [
                    {"==": [{"var": "diabetic"}, true]},
                    {">":  [{"var": "systolic"}, 140]}
                ]}
            ]}
            """;

        Evaluate(rule, ("age", 80), ("diabetic", false), ("systolic", 120)).ShouldBeTrue();
        Evaluate(rule, ("age", 50), ("diabetic", true), ("systolic", 150)).ShouldBeTrue();
        Evaluate(rule, ("age", 50), ("diabetic", true), ("systolic", 120)).ShouldBeFalse();
        Evaluate(rule, ("age", 50), ("diabetic", false), ("systolic", 150)).ShouldBeFalse();
    }

    [Fact]
    public void An_and_over_no_branches_is_vacuously_true_but_an_or_is_false()
    {
        Evaluate("""{"and": []}""").ShouldBeTrue();
        Evaluate("""{"or": []}""").ShouldBeFalse();
    }

    // ---------------------------------------------------------------- membership

    [Fact]
    public void In_matches_an_item_of_a_list()
    {
        Evaluate("""{"in": [{"var": "code"}, ["44054006", "38341003"]]}""", ("code", "44054006")).ShouldBeTrue();
        Evaluate("""{"in": [{"var": "code"}, ["44054006", "38341003"]]}""", ("code", "99999")).ShouldBeFalse();
    }

    [Fact]
    public void In_matches_a_substring_case_insensitively()
    {
        Evaluate("""{"in": ["diabetes", {"var": "text"}]}""", ("text", "Type 2 DIABETES mellitus")).ShouldBeTrue();
    }

    [Fact]
    public void In_over_a_missing_variable_does_not_fire()
    {
        Evaluate("""{"in": [{"var": "code"}, {"var": "codes"}]}""").ShouldBeFalse();
    }

    // ---------------------------------------------------------------- truthiness

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("something", true)]
    [InlineData(null, false)]
    public void A_bare_variable_is_evaluated_for_truthiness(object? value, bool expected)
    {
        Evaluate("""{"var": "flag"}""", ("flag", value)).ShouldBe(expected);
    }

    /// <summary>A realistic rule from the workshop: flag an uncontrolled diabetic patient.</summary>
    [Fact]
    public void A_realistic_authored_rule_fires_only_for_the_intended_patient()
    {
        const string rule = """
            {"and": [
                {"in": ["diabetes", {"var": "conditionText"}]},
                {">":  [{"var": "hba1c"}, 8.0]},
                {">=": [{"var": "age"}, 18]}
            ]}
            """;

        Evaluate(rule, ("conditionText", "Type 2 diabetes mellitus"), ("hba1c", 9.4), ("age", 61)).ShouldBeTrue();
        Evaluate(rule, ("conditionText", "Type 2 diabetes mellitus"), ("hba1c", 6.2), ("age", 61)).ShouldBeFalse();
        Evaluate(rule, ("conditionText", "Seasonal allergy"), ("hba1c", 9.4), ("age", 61)).ShouldBeFalse();
        // Missing HbA1c must not fire the alert on incomplete data.
        Evaluate(rule, ("conditionText", "Type 2 diabetes mellitus"), ("age", 61)).ShouldBeFalse();
    }
}
