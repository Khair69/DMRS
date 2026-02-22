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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // 1. FhirResource Configuration
            modelBuilder.Entity<FhirResource>(entity =>
            {
                entity.HasKey(e => new { e.ResourceType, e.Id }); // Composite Key
                entity.Property(e => e.LastUpdated).IsRequired();
                entity.Property(e => e.RawContent).IsRequired(); // In Postgres, map this to JSONB
                entity.Property(e => e.VersionId).IsConcurrencyToken(); // Optimistic Concurrency
            });

            modelBuilder.Entity<FhirResourceVersion>(entity =>
            {
                entity.HasKey(e => new { e.ResourceType, e.Id, e.VersionId });
                entity.Property(e => e.LastUpdated).IsRequired();
                entity.Property(e => e.RawContent).IsRequired();
            });

            // 2. ResourceIndex Configuration
            modelBuilder.Entity<ResourceIndex>(entity =>
            {
                entity.HasKey(i => i.Id);

                // CRITICAL: Indexes for performance
                entity.HasIndex(i => new { i.ResourceType, i.SearchParamCode, i.Value });
            });
        }
    }
}
