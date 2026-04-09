using Microsoft.EntityFrameworkCore;
using DMRS.Api.Domain;
using DMRS.Api.Domain.ClinicalDecisionSupport;
namespace DMRS.Api.Infrastructure.Persistence
{
    public class AppDbContext:DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
        public DbSet<FhirResource> FhirResources { get; set; }
        public DbSet<FhirResourceVersion> FhirResourceVersions { get; set; }
        public DbSet<ResourceIndex> ResourceIndices { get; set; }
        public DbSet<CdsRuleDefinition> CdsRuleDefinitions { get; set; }
        public DbSet<DrugKnowledgeEntry> DrugKnowledgeEntries { get; set; }

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

            modelBuilder.Entity<CdsRuleDefinition>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.HookId).IsRequired();
                entity.Property(e => e.Name).IsRequired();
                entity.Property(e => e.ExpressionJson).HasColumnType("jsonb");
                entity.Property(e => e.CardTemplateJson).HasColumnType("jsonb");
                entity.HasIndex(e => new { e.HookId, e.IsActive });
            });

            modelBuilder.Entity<DrugKnowledgeEntry>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.QueryKey).IsRequired();
                entity.Property(e => e.KnowledgeType).IsRequired();
                entity.Property(e => e.Source).IsRequired();
                entity.Property(e => e.PayloadJson).HasColumnType("jsonb");
                entity.HasIndex(e => new { e.QueryKey, e.KnowledgeType, e.Source });
            });
        }
    }
}
