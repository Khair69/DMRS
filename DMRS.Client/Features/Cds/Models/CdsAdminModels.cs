namespace DMRS.Client.Features.Cds.Models;

public sealed class CdsRuleSummary
{
    public Guid Id { get; set; }
    public string HookId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; }
    public string Status { get; set; } = "Draft";
    public bool HasUnpublishedChanges { get; set; }
    public Guid? PublishedVersionId { get; set; }
    public int? PublishedVersionNumber { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public string? PublishedBy { get; set; }
    public string ExpressionJson { get; set; } = string.Empty;
    public string CardTemplateJson { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class CdsRuleVersionModel
{
    public Guid Id { get; set; }
    public Guid RuleDefinitionId { get; set; }
    public int VersionNumber { get; set; }
    public string HookId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Priority { get; set; }
    public string ExpressionJson { get; set; } = string.Empty;
    public string CardTemplateJson { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
    public string? PublishedBy { get; set; }
}

public sealed class CdsVariableDefinitionModel
{
    public string Path { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public sealed class CdsRuleTemplateDefinitionModel
{
    public string TemplateId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<CdsRuleTemplateParameterDefinitionModel> Parameters { get; set; } = [];
}

public sealed class CdsRuleTemplateParameterDefinitionModel
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool Required { get; set; }
    public string Description { get; set; } = string.Empty;
}

public sealed class CdsRuleTemplateRequestModel
{
    public string TemplateId { get; set; } = "max-dose-exceeded";
    public string HookId { get; set; } = "medication-prescribe";
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Priority { get; set; } = 1;
    public bool IsActive { get; set; }
    public string Indicator { get; set; } = "warning";
    public string SourceLabel { get; set; } = "DMRS CDS";
    public string? SourceUrl { get; set; }
    public string? MedicationRxCui { get; set; }
    public string? PregnancyCategory { get; set; }
    public string? IndicationCode { get; set; }
}

public sealed class CdsRuleValidationResultModel
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = [];
}

public sealed class CdsCardModel
{
    public string Summary { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public string Indicator { get; set; } = string.Empty;
    public CdsCardSourceModel Source { get; set; } = new();
}

public sealed class CdsCardSourceModel
{
    public string Label { get; set; } = string.Empty;
    public string? Url { get; set; }
}

public sealed class CdsRulePreviewRequestModel
{
    public string Hook { get; set; } = "medication-prescribe";
    public CdsRuleSummary Rule { get; set; } = new();
    public object Context { get; set; } = new();
    public object? Prefetch { get; set; }
}

public sealed class CdsRulePreviewResponseModel
{
    public CdsRuleValidationResultModel Validation { get; set; } = new();
    public List<CdsCardModel> Cards { get; set; } = [];
}

public sealed class CdsHookResponseModel
{
    public List<CdsCardModel> Cards { get; set; } = [];
}

public sealed class CdsMedicineKnowledgeModel
{
    public string RxCui { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal? MaxDailyMg { get; set; }
    public decimal? MaxSingleMg { get; set; }
    public decimal? WarningThresholdMg { get; set; }
    public string? PregnancyCategory { get; set; }
    public bool? IsControlled { get; set; }
    public List<CdsMedicineIngredientModel> Ingredients { get; set; } = [];
    public List<string> Indications { get; set; } = [];
    public string Source { get; set; } = string.Empty;
    public DateTimeOffset FetchedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}

public sealed class CdsMedicineIngredientModel
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
