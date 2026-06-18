using LifeLine.Application.Interfaces.IRepository;
using LifeLine.Domain.Entities;
using LifeLine.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LifeLine.Persistence.Repositories;

public class DonationRepository : IDonationRepository
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DonationRepository> _logger;

    public DonationRepository(
        ApplicationDbContext context,
        ILogger<DonationRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task AddAsync(
        Donation donation, CancellationToken ct = default)
    {
        await _context.Donations.AddAsync(donation, ct);
        _logger.LogInformation(
            "Donation added: {Reference}", donation.PaymentReference);
    }

    public async Task<Donation?> GetByReferenceAsync(
        string reference, CancellationToken ct = default)
    {
        return await _context.Donations
            .Include(d => d.Campaign)
            .FirstOrDefaultAsync(d => d.PaymentReference == reference, ct);
    }

    public async Task<Donation?> GetByIdAsync(
        Guid id, CancellationToken ct = default)
    {
        return await _context.Donations
            .Include(d => d.Campaign)
            .Include(d => d.Donor)
            .FirstOrDefaultAsync(d => d.Id == id, ct);
    }

    public async Task<List<Donation>> GetByCampaignIdAsync(
        Guid campaignId, CancellationToken ct = default)
    {
        return await _context.Donations
            .Include(d => d.Donor)
            .Where(d => d.CampaignId == campaignId && d.IsVerified)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task UpdateAsync(
        Donation donation, CancellationToken ct = default)
    {
        _context.Donations.Update(donation);
        await Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}