namespace DMRS.Api.Application.ClinicalDecisionSupport.Models
{
    public sealed class DoseFact
    {
        public DoseFact(double dailyDoseMg)
        {
            DailyDoseMg = dailyDoseMg;
        }

        public double DailyDoseMg { get; }
    }
}
