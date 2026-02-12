namespace DMRS.Api.Domain
{
    public class ResourceIndex
    {
        public int Id { get; set; }
        public string ResourceId { get; set; }
        public string ResourceType { get; set; }
        public string SearchParamCode { get; set; } 
        public string Value { get; set; }
    }
}
