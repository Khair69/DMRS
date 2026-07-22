using DMRS.Api.Application.ClinicalDecisionSupport.Services;
using DMRS.Api.Domain.ClinicalDecisionSupport;
using Shouldly;

namespace DMRS.UnitTests.Cds;

/// <summary>
/// Covers <see cref="RuleDefinitionValidator"/> — the gate that stops a malformed rule from being
/// saved in the rule workshop. It matters because the evaluator is deliberately forgiving at
/// runtime: an unsupported operator evaluates to false rather than throwing, so without validation
/// a clinician could save a rule containing a typo and see no alerts and no error, ever.
/// </summary>
public class RuleDefinitionValidatorTests
{
    private readonly RuleDefinitionValidator _validator = new();

    private static CdsRuleDefinition Rule(
        string hookId = "patient-view",
        string name = "High glucose alert",
        string expression = """{">": [{"var": "glucose"}, 140]}""",
        string cardTemplate = """{"summary": "Glucose is high", "indicator": "warning"}""")
        => new()
        {
            HookId = hookId,
            Name = name,
            ExpressionJson = expression,
            CardTemplateJson = cardTemplate
        };

    [Fact]
    public void A_well_formed_rule_is_accepted()
    {
        var result = _validator.Validate(Rule());

        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    // ---------------------------------------------------------------- required fields

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_rule_without_a_hook_is_rejected(string hookId)
    {
        var result = _validator.Validate(Rule(hookId: hookId));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain("HookId is required.");
    }

    [Fact]
    public void A_rule_without_a_name_is_rejected()
    {
        var result = _validator.Validate(Rule(name: ""));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain("Name is required.");
    }

    [Fact]
    public void A_rule_without_an_expression_is_rejected()
    {
        var result = _validator.Validate(Rule(expression: ""));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain("ExpressionJson is required.");
    }

    [Fact]
    public void A_rule_without_a_card_template_is_rejected()
    {
        var result = _validator.Validate(Rule(cardTemplate: ""));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain("CardTemplateJson is required.");
    }

    [Fact]
    public void Every_problem_is_reported_at_once_rather_than_one_at_a_time()
    {
        var result = _validator.Validate(Rule(hookId: "", name: "", expression: "", cardTemplate: ""));

        result.IsValid.ShouldBeFalse();
        result.Errors.Count.ShouldBe(4);
    }

    // ---------------------------------------------------------------- malformed JSON

    [Theory]
    [InlineData("""{">": [{"var": "glucose"}, 140]""")]  // missing closing brace
    [InlineData("not json at all")]
    [InlineData("""{"and": [,]}""")]
    public void A_rule_whose_expression_is_not_valid_json_is_rejected(string expression)
    {
        var result = _validator.Validate(Rule(expression: expression));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.StartsWith("ExpressionJson is not valid JSON"));
    }

    [Fact]
    public void A_rule_whose_card_template_is_not_valid_json_is_rejected()
    {
        var result = _validator.Validate(Rule(cardTemplate: "{oops"));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.StartsWith("CardTemplateJson is not valid JSON"));
    }

    // ---------------------------------------------------------------- operator whitelist

    [Theory]
    [InlineData("var")]
    [InlineData("==")]
    [InlineData("!=")]
    [InlineData(">")]
    [InlineData("<")]
    [InlineData(">=")]
    [InlineData("<=")]
    [InlineData("and")]
    [InlineData("or")]
    [InlineData("in")]
    public void Every_operator_the_evaluator_implements_is_accepted_by_the_validator(string op)
    {
        var expression = op == "var"
            ? """{"var": "glucose"}"""
            : $$"""{"{{op}}": [{"var": "glucose"}, 140]}""";

        _validator.Validate(Rule(expression: expression)).IsValid.ShouldBeTrue(
            $"the validator and the evaluator must agree on the operator '{op}'");
    }

    /// <summary>
    /// A typo such as "gt" instead of ">" parses as valid JSON and evaluates to false at runtime, so
    /// the rule would simply never fire. The validator is the only place this is caught.
    /// </summary>
    [Theory]
    [InlineData("gt")]
    [InlineData("greaterThan")]
    [InlineData("!")]
    [InlineData("+")]
    [InlineData("if")]
    public void An_operator_the_evaluator_does_not_implement_is_rejected(string op)
    {
        var expression = $$"""{"{{op}}": [{"var": "glucose"}, 140]}""";

        var result = _validator.Validate(Rule(expression: expression));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain($"Unsupported operator or property '{op}'.");
    }

    [Fact]
    public void An_unsupported_operator_nested_deep_inside_a_rule_is_still_caught()
    {
        const string expression = """
            {"and": [
                {">": [{"var": "glucose"}, 140]},
                {"or": [
                    {"==": [{"var": "gender"}, "female"]},
                    {"xor": [{"var": "a"}, {"var": "b"}]}
                ]}
            ]}
            """;

        var result = _validator.Validate(Rule(expression: expression));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain("Unsupported operator or property 'xor'.");
    }

    [Fact]
    public void Card_template_properties_are_not_treated_as_operators()
    {
        const string cardTemplate = """
            {"summary": "Review glucose control", "indicator": "warning",
             "source": {"label": "DMRS CDS", "url": "https://example.org"}}
            """;

        _validator.Validate(Rule(cardTemplate: cardTemplate)).IsValid.ShouldBeTrue();
    }

    /// <summary>
    /// The operator check only applies to single-property objects, since that is the JsonLogic shape.
    /// A multi-property object is data, not an operator, and must pass through untouched.
    /// </summary>
    [Fact]
    public void A_multi_property_object_is_treated_as_data_not_as_an_operator()
    {
        const string expression = """{"and": [{"anything": 1, "goes": 2}]}""";

        _validator.Validate(Rule(expression: expression)).IsValid.ShouldBeTrue();
    }
}
