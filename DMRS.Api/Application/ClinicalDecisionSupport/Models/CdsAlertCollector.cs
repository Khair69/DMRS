namespace DMRS.Api.Application.ClinicalDecisionSupport.Models
{
    public sealed class CdsAlertCollector
    {
        private readonly List<CdsAlert> _alerts = [];

        public IReadOnlyList<CdsAlert> Alerts => _alerts;

        public void Add(CdsAlert alert)
        {
            _alerts.Add(alert);
        }
    }
}
