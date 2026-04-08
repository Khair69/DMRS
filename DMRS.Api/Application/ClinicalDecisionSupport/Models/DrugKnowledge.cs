namespace DMRS.Api.Application.ClinicalDecisionSupport.Models
{
    public sealed class DrugKnowledge
    {
        public DrugKnowledge(string code, double maxDailyDoseMg, string? display = null)
        {
            Code = code;
            MaxDailyDoseMg = maxDailyDoseMg;
            Display = display;
        }

        public string Code { get; }
        public double MaxDailyDoseMg { get; }
        public string? Display { get; }
    }
}
