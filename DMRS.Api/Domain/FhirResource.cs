namespace DMRS.Api.Domain
{
    public class FhirResource
    {
        public string Id { get; set; }
        public string ResourceType { get; set; }
        public int VersionId { get; set; }
        public bool IsDeleted { get; set; }
        public DateTimeOffset LastUpdated { get; set; }
        public string RawContent { get; set; }
    }
}
