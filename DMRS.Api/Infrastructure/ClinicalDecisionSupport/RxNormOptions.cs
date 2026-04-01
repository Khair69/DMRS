namespace DMRS.Api.Infrastructure.ClinicalDecisionSupport
{
    public sealed class RxNormOptions
    {
        public const string SectionName = "RxNorm";

        public string BaseUrl { get; set; } = "https://rxnav.nlm.nih.gov/REST/";
        public int TimeoutSeconds { get; set; } = 10;
    }
}
