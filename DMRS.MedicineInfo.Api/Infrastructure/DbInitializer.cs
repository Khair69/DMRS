using DMRS.MedicineInfo.Api.Domain;
using System.Text.Json;

namespace DMRS.MedicineInfo.Api.Infrastructure
{
    public class DbInitializer
    {
        public static async Task SeedAsync(AppDbContext context)
        {
            await context.Database.EnsureCreatedAsync();

            if (context.Medicines.Any())
                return; // DB has been seeded

            var filePath = Path.Combine(AppContext.BaseDirectory, "seed-data.json");
            if (!File.Exists(filePath)) return;

            var json = await File.ReadAllTextAsync(filePath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var medicines = JsonSerializer.Deserialize<List<Medicine>>(json, options);

            if (medicines != null)
            {
                // Note: EF Core is smart enough to handle many-to-many 
                // duplicates if you use the same Ingredient objects, 
                // but for a mock API, simple adding is fine.
                await context.Medicines.AddRangeAsync(medicines);
                await context.SaveChangesAsync();
            }
        }
    }
}
