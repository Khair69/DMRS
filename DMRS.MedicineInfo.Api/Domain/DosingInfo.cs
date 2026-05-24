namespace DMRS.MedicineInfo.Api.Domain
{
    public class DosingInfo
    {
        public decimal MaxDailyMg { get; set; }
        public decimal MaxSingleMg { get; set; }
        public decimal WarningThreshold { get; set; }
    }
}
