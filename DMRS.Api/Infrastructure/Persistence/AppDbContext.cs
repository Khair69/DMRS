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
        public DbSet<CdsRuleVersion> CdsRuleVersions { get; set; }
        public DbSet<MedicineKnowledgeRecord> MedicineKnowledgeRecords { get; set; }

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
                entity.Property(e => e.Status).HasConversion<string>();
                entity.Property(e => e.ExpressionJson).HasColumnType("jsonb");
                entity.Property(e => e.CardTemplateJson).HasColumnType("jsonb");
                entity.HasIndex(e => new { e.HookId, e.Status, e.IsActive });

                entity.HasOne<CdsRuleVersion>()
                    .WithMany()
                    .HasForeignKey(e => e.PublishedVersionId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<CdsRuleVersion>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.HookId).IsRequired();
                entity.Property(e => e.Name).IsRequired();
                entity.Property(e => e.ExpressionJson).HasColumnType("jsonb");
                entity.Property(e => e.CardTemplateJson).HasColumnType("jsonb");
                entity.HasIndex(e => new { e.RuleDefinitionId, e.VersionNumber }).IsUnique();
                entity.HasIndex(e => e.HookId);

                entity.HasOne<CdsRuleDefinition>()
                    .WithMany()
                    .HasForeignKey(e => e.RuleDefinitionId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<MedicineKnowledgeRecord>(entity =>
            {
                entity.HasKey(e => e.RxCui);
                entity.Property(e => e.RxCui).ValueGeneratedNever();
                entity.Property(e => e.Name).IsRequired();
                entity.Property(e => e.IngredientCodesJson).HasColumnType("jsonb");
                entity.Property(e => e.IngredientNamesJson).HasColumnType("jsonb");
                entity.Property(e => e.IndicationCodesJson).HasColumnType("jsonb");
                entity.Property(e => e.Source).IsRequired();
                entity.HasIndex(e => e.Name);
            });
        }
    }
}
