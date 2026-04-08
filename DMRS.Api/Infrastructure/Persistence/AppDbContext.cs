using Microsoft.EntityFrameworkCore;
using DMRS.Api.Domain;
namespace DMRS.Api.Infrastructure.Persistence
{
    public class AppDbContext:DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
        public DbSet<FhirResource> FhirResources { get; set; }
        public DbSet<FhirResourceVersion> FhirResourceVersions { get; set; }
        public DbSet<ResourceIndex> ResourceIndices { get; set; }
        public DbSet<DrugMapping> DrugMappings { get; set; }
        public DbSet<DrugMaxDose> DrugMaxDoses { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FhirResource>(entity =>
            {
                entity.HasKey(e => new { e.ResourceType, e.Id }); 
                entity.Property(e => e.LastUpdated).IsRequired();
                entity.Property(e => e.RawContent).IsRequired(); 
                entity.Property(e => e.VersionId).IsConcurrencyToken();
            });

            modelBuilder.Entity<FhirResourceVersion>(entity =>
            {
                entity.HasKey(e => new { e.ResourceType, e.Id, e.VersionId });
                entity.Property(e => e.LastUpdated).IsRequired();
                entity.Property(e => e.RawContent).IsRequired();
            });

            modelBuilder.Entity<ResourceIndex>(entity =>
            {
                entity.HasKey(i => i.Id);

                entity.HasIndex(i => new { i.ResourceType, i.SearchParamCode, i.Value });
            });

            modelBuilder.Entity<DrugMapping>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.SourceTerm).IsRequired();
                entity.Property(x => x.SourceSystem).IsRequired();
                entity.Property(x => x.IngredientRxCui).IsRequired();
                entity.Property(x => x.LastUpdatedUtc).IsRequired();
                entity.HasIndex(x => new { x.SourceTerm, x.SourceSystem, x.IngredientRxCui }).IsUnique();
            });

            modelBuilder.Entity<DrugMaxDose>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.IngredientRxCui).IsRequired();
                entity.Property(x => x.MaxDailyDoseMg).IsRequired();
                entity.Property(x => x.LastUpdatedUtc).IsRequired();
                entity.HasIndex(x => x.IngredientRxCui).IsUnique();
            });
        }
    }
}
