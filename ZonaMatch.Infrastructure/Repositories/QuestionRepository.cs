using Microsoft.EntityFrameworkCore;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;
using ZonaMatch.Infrastructure.Data;

namespace ZonaMatch.Infrastructure.Repositories
{
    public class QuestionRepository : IQuestionRepository
    {
        private readonly ZonaMatchDbContext _dbContext;

        public QuestionRepository(ZonaMatchDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IReadOnlyList<Question>> ListByZoneAsync(string zoneSlug, CancellationToken cancellationToken = default) =>
            await _dbContext.Questions
                .AsNoTracking()
                .AsSplitQuery()
                .Include(p => p.User)
                .Include(question => question.Answers).ThenInclude(answer => answer.User)
                .Where(question => question.ZoneSlug == zoneSlug)
                .OrderByDescending(question => question.CreatedAt)
                .ToListAsync(cancellationToken);

        public Task<bool> ExistsAsync(string zoneSlug, Guid id, CancellationToken cancellationToken = default) =>
            _dbContext.Questions.AnyAsync(question => question.ZoneSlug == zoneSlug && question.Id == id, cancellationToken);

        public Task<Question?> GetByIdAsync(string zoneSlug, Guid id, CancellationToken cancellationToken = default) =>
            _dbContext.Questions
                .Include(question => question.User)
                .FirstOrDefaultAsync(question => question.ZoneSlug == zoneSlug && question.Id == id, cancellationToken);

        public void Add(Question question) => _dbContext.Questions.Add(question);

        public void Add(Answer answer) => _dbContext.Answers.Add(answer);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            _dbContext.SaveChangesAsync(cancellationToken);
    }
}
