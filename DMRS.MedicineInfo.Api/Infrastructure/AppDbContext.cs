using DMRS.MedicineInfo.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace DMRS.MedicineInfo.Api.Infrastructure
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Medicine> Medicines { get; set; }
        public DbSet<Ingredient> Ingredients { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // Configure owned types
            modelBuilder.Entity<Medicine>().OwnsOne(m => m.Dosing);
            modelBuilder.Entity<Medicine>().OwnsOne(m => m.Safety);
            // Configure many-to-many relationship
            modelBuilder.Entity<Medicine>()
                .HasMany(m => m.Ingredients)
                .WithMany(i => i.Medicines)
                .UsingEntity(j => j.ToTable("MedicineIngredients"));
        }
    }
}
