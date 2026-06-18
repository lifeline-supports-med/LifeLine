using LifeLine.Application.Interfaces.IRepository;
using LifeLine.Domain.Entities;
using LifeLine.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LifeLine.Persistence.Repositories;

public class PayoutRepository : IPayoutRepository
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PayoutRepository> _logger;

    public PayoutRepository(
        ApplicationDbContext context,
        ILogger<PayoutRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task AddAsync(
        Payout payout, CancellationToken ct = default)
    {
        await _context.Payouts.AddAsync(payout, ct);
        _logger.LogInformation(
            "Payout request added for campaign {CampaignId}", payout.CampaignId);
    }

    public async Task<Payout?> GetByIdAsync(
        Guid id, CancellationToken ct = default)
    {
        return await _context.Payouts
            .Include(p => p.Campaign)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<List<Payout>> GetByCampaignIdAsync(
        Guid campaignId, CancellationToken ct = default)
    {
        return await _context.Payouts
            .Include(p => p.Campaign)
            .Where(p => p.CampaignId == campaignId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<List<Payout>> GetAllAsync(
        int page, int pageSize, string? status, CancellationToken ct = default)
    {
        var query = _context.Payouts
            .Include(p => p.Campaign)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(p => p.Status.ToLower() == status.ToLower());

        return await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task<bool> HasPendingPayoutAsync(
        Guid campaignId, CancellationToken ct = default)
    {
        return await _context.Payouts
            .AnyAsync(p => p.CampaignId == campaignId
                        && p.Status == "Pending", ct);
    }

    public async Task UpdateAsync(
        Payout payout, CancellationToken ct = default)
    {
        _context.Payouts.Update(payout);
        await Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}