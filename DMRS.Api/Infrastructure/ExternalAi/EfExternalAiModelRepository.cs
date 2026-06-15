using DMRS.Api.Application.ExternalAi.Interfaces;
using DMRS.Api.Domain.ExternalAi;
using DMRS.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DMRS.Api.Infrastructure.ExternalAi
{
    public sealed class EfExternalAiModelRepository : IExternalAiModelRepository
    {
        private readonly AppDbContext _dbContext;

        public EfExternalAiModelRepository(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IReadOnlyList<ExternalAiModel>> ListAsync(CancellationToken cancellationToken)
        {
            return await _dbContext.ExternalAiModels
                .AsNoTracking()
                .OrderBy(m => m.Name)
                .ToListAsync(cancellationToken);
        }

        public Task<ExternalAiModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            return _dbContext.ExternalAiModels.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        }

        public async Task AddAsync(ExternalAiModel model, CancellationToken cancellationToken)
        {
            _dbContext.ExternalAiModels.Add(model);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task UpdateAsync(ExternalAiModel model, CancellationToken cancellationToken)
        {
            _dbContext.ExternalAiModels.Update(model);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task DeleteAsync(ExternalAiModel model, CancellationToken cancellationToken)
        {
            _dbContext.ExternalAiModels.Remove(model);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
