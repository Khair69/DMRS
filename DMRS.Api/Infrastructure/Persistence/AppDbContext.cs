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
        }
    }
}
