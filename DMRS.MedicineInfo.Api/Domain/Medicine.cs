using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DMRS.MedicineInfo.Api.Domain
{
    public class Medicine
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public string RxCui { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        // Owned Types (Stored as columns in the same table)
        public DosingInfo Dosing { get; set; } = new();
        public SafetyInfo Safety { get; set; } = new();

        // Complex types
        public List<string> Indications { get; set; } = new();

        // Many-to-Many Relationship
        public List<Ingredient> Ingredients { get; set; } = new();
    }
}
