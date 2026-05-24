using System.Text.Json;
using DMRS.Api.Application.ClinicalDecisionSupport.Interfaces;
using DMRS.Api.Application.ClinicalDecisionSupport.Models;
using DMRS.Api.Domain.ClinicalDecisionSupport;
using DMRS.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Services
{
    public sealed class MedicineKnowledgeService : IMedicineKnowledgeService
    {
        private readonly AppDbContext _dbContext;
        private readonly IKnowledgeProvider _knowledgeProvider;

        public MedicineKnowledgeService(AppDbContext dbContext, IKnowledgeProvider knowledgeProvider)
        {
            _dbContext = dbContext;
            _knowledgeProvider = knowledgeProvider;
        }

        public async Task<MedicineKnowledge?> GetAsync(string medicationCode, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(medicationCode))
            {
                return null;
            }

            var record = await FindRecordAsync(medicationCode, cancellationToken);
            if (record != null && record.ExpiresAt > DateTimeOffset.UtcNow)
            {
                return Map(record);
            }

            return await RefreshAsync(medicationCode, cancellationToken);
        }

        public async Task<MedicineKnowledge?> RefreshAsync(string medicationCode, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(medicationCode))
            {
                return null;
            }

            var knowledge = await _knowledgeProvider.GetMedicationKnowledgeAsync(medicationCode, cancellationToken);
            if (knowledge == null || string.IsNullOrWhiteSpace(knowledge.RxCui))
            {
                return null;
            }

            var record = await _dbContext.MedicineKnowledgeRecords
                .FirstOrDefaultAsync(entry => entry.RxCui == knowledge.RxCui, cancellationToken);

            if (record == null)
            {
                record = new MedicineKnowledgeRecord
                {
                    RxCui = knowledge.RxCui
                };

                _dbContext.MedicineKnowledgeRecords.Add(record);
            }

            UpdateRecord(record, knowledge);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Map(record);
        }

        public async Task<IReadOnlyList<MedicineKnowledge>> SearchAsync(
            string? query,
            string? ingredient,
            string? indication,
            int limit,
            CancellationToken cancellationToken)
        {
            var normalizedQuery = query?.Trim().ToLowerInvariant();
            var normalizedIngredient = ingredient?.Trim().ToLowerInvariant();
            var normalizedIndication = indication?.Trim().ToLowerInvariant();
            var take = limit <= 0 ? 25 : Math.Min(limit, 100);

            var recordsQuery = _dbContext.MedicineKnowledgeRecords.AsQueryable();

            if (!string.IsNullOrWhiteSpace(normalizedQuery))
            {
                recordsQuery = recordsQuery.Where(entry =>
                    entry.RxCui.ToLower().Contains(normalizedQuery)
                    || entry.Name.ToLower().Contains(normalizedQuery));
            }

            if (!string.IsNullOrWhiteSpace(normalizedIngredient))
            {
                recordsQuery = recordsQuery.Where(entry => entry.IngredientSearchText.Contains(normalizedIngredient));
            }

            if (!string.IsNullOrWhiteSpace(normalizedIndication))
            {
                recordsQuery = recordsQuery.Where(entry => entry.IndicationSearchText.Contains(normalizedIndication));
            }

            var records = await recordsQuery
                .OrderBy(entry => entry.Name)
                .Take(take)
                .ToListAsync(cancellationToken);

            return records.Select(Map).ToArray();
        }

        private async Task<MedicineKnowledgeRecord?> FindRecordAsync(string medicationCode, CancellationToken cancellationToken)
        {
            var trimmed = medicationCode.Trim();
            if (trimmed.All(char.IsDigit))
            {
                return await _dbContext.MedicineKnowledgeRecords
                    .FirstOrDefaultAsync(entry => entry.RxCui == trimmed, cancellationToken);
            }

            var normalized = trimmed.ToLowerInvariant();
            return await _dbContext.MedicineKnowledgeRecords
                .Where(entry => entry.Name.ToLower() == normalized || entry.Name.ToLower().Contains(normalized))
                .OrderBy(entry => entry.Name.ToLower() == normalized ? 0 : 1)
                .ThenBy(entry => entry.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }

        private static void UpdateRecord(MedicineKnowledgeRecord record, MedicineKnowledge knowledge)
        {
            record.Name = knowledge.Name;
            record.MaxDailyMg = knowledge.MaxDailyMg;
            record.MaxSingleMg = knowledge.MaxSingleMg;
            record.WarningThresholdMg = knowledge.WarningThresholdMg;
            record.PregnancyCategory = knowledge.PregnancyCategory;
            record.IsControlled = knowledge.IsControlled;
            record.IngredientCodesJson = JsonSerializer.Serialize(knowledge.Ingredients.Select(ingredient => ingredient.Code).Distinct(StringComparer.OrdinalIgnoreCase));
            record.IngredientNamesJson = JsonSerializer.Serialize(knowledge.Ingredients.Select(ingredient => ingredient.Name).Distinct(StringComparer.OrdinalIgnoreCase));
            record.IndicationCodesJson = JsonSerializer.Serialize(knowledge.Indications.Distinct(StringComparer.OrdinalIgnoreCase));
            record.IngredientSearchText = string.Join(" ", knowledge.Ingredients.SelectMany(ingredient => new[] { ingredient.Code, ingredient.Name }).Where(value => !string.IsNullOrWhiteSpace(value))).ToLowerInvariant();
            record.IndicationSearchText = string.Join(" ", knowledge.Indications.Where(value => !string.IsNullOrWhiteSpace(value))).ToLowerInvariant();
            record.Source = knowledge.Source;
            record.FetchedAt = knowledge.FetchedAt;
            record.ExpiresAt = knowledge.ExpiresAt;
        }

        private static MedicineKnowledge Map(MedicineKnowledgeRecord record)
        {
            var ingredientCodes = DeserializeStringList(record.IngredientCodesJson);
            var ingredientNames = DeserializeStringList(record.IngredientNamesJson);
            var ingredients = ingredientCodes
                .Select((code, index) => new MedicineIngredient(
                    code,
                    index < ingredientNames.Count ? ingredientNames[index] : code))
                .ToArray();

            return new MedicineKnowledge(
                record.RxCui,
                record.Name,
                record.MaxDailyMg,
                record.MaxSingleMg,
                record.WarningThresholdMg,
                record.PregnancyCategory,
                record.IsControlled,
                ingredients,
                DeserializeStringList(record.IndicationCodesJson),
                record.Source,
                record.FetchedAt,
                record.ExpiresAt);
        }

        private static List<string> DeserializeStringList(string json)
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
    }
}
