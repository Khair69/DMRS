using System.Text.Json.Serialization;

namespace DMRS.MedicineInfo.Api.Domain
{
    public class Ingredient
    {
        [JsonIgnore]
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty; // UNII Code
        public string Name { get; set; } = string.Empty;
        [JsonIgnore]
        public List<Medicine> Medicines { get; set; } = new();
    }
}
