using LifeLine.Application.Interfaces.IRepository;
using LifeLine.Domain.Entities;
using LifeLine.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LifeLine.Persistence.Repositories
{
    public class CampaignRepository : ICampaignRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CampaignRepository> _logger;

        public CampaignRepository(
            ApplicationDbContext context,
            ILogger<CampaignRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Campaign?> GetByIdAsync(
            Guid id, CancellationToken ct = default)
        {
            return await _context.Campaigns
                .Include(c => c.Creator)
                .Include(c => c.Documents)
                .Include(c => c.Updates)
                .Include(c => c.Donations)
                .FirstOrDefaultAsync(c => c.Id == id, ct);
        }

        public async Task<Campaign?> GetBySlugAsync(
            string slug, CancellationToken ct = default)
        {
            return await _context.Campaigns
                .Include(c => c.Creator)
                .Include(c => c.Documents)
                .Include(c => c.Updates)
                .Include(c => c.Donations)
                .FirstOrDefaultAsync(c => c.Slug == slug.ToLower(), ct);
        }

        public async Task<List<Campaign>> GetAllVerifiedAsync(
            int page, int pageSize, CancellationToken ct = default)
        {
            return await _context.Campaigns
                .Include(c => c.Creator)
                .Include(c => c.Donations)
                .Where(c => c.IsVerified)
                .OrderByDescending(c => c.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);
        }

        public async Task<List<Campaign>> GetByCreatorIdAsync(
            string creatorId, CancellationToken ct = default)
        {
            return await _context.Campaigns
                .Include(c => c.Donations)
                .Include(c => c.Documents)
                .Where(c => c.CreatorId == creatorId)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync(ct);
        }

        public async Task<bool> SlugExistsAsync(
            string slug, CancellationToken ct = default)
        {
            return await _context.Campaigns
                .AnyAsync(c => c.Slug == slug.ToLower(), ct);
        }

        public async Task AddAsync(
            Campaign campaign, CancellationToken ct = default)
        {
            await _context.Campaigns.AddAsync(campaign, ct);
            _logger.LogInformation(
                "Campaign added to context: {CampaignId}", campaign.Id);
        }

        public async Task<List<Campaign>> GetAllByStatusAsync(
    string status, int page, int pageSize, CancellationToken ct = default)
        {
            var query = _context.Campaigns
                .Include(c => c.Creator)
                .Include(c => c.Documents)
                .Include(c => c.Donations)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(c => c.Status.ToString().ToLower() == status.ToLower());

            return await query
                .OrderByDescending(c => c.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);
        }

        public async Task<List<Campaign>> GetAllForAdminAsync(
            int page, int pageSize, CancellationToken ct = default)
        {
            return await _context.Campaigns
                .Include(c => c.Creator)
                .Include(c => c.Documents)
                .Include(c => c.Donations)
                .OrderByDescending(c => c.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);
        }

        public async Task UpdateAsync(
            Campaign campaign, CancellationToken ct = default)
        {
            _context.Campaigns.Update(campaign);
            _logger.LogInformation(
                "Campaign updated in context: {CampaignId}", campaign.Id);
            await Task.CompletedTask;
        }

        public async Task DeleteAsync(
            Campaign campaign, CancellationToken ct = default)
        {
            _context.Campaigns.Remove(campaign);
            _logger.LogInformation(
                "Campaign removed from context: {CampaignId}", campaign.Id);
            await Task.CompletedTask;
        }

        public async Task AddDocumentAsync(
            MedicalDocument document, CancellationToken ct = default)
        {
            await _context.MedicalDocument.AddAsync(document, ct);
        }

        public async Task AddUpdateAsync(
            MedicalUpdate update, CancellationToken ct = default)
        {
            await _context.MedicalUpdate.AddAsync(update, ct);
        }

        public async Task<List<MedicalUpdate>> GetUpdatesAsync(
            Guid campaignId, CancellationToken ct = default)
        {
            return await _context.MedicalUpdate
                .Where(u => u.CampaignId == campaignId)
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync(ct);
        }

        public async Task SaveChangesAsync(CancellationToken ct = default)
        {
            await _context.SaveChangesAsync(ct);
        }
    }
}
