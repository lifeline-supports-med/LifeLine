using LifeLine.Domain.Entities;

namespace LifeLine.Application.Interfaces.IRepository
{
    public interface IDonationRepository
    {
        Task AddAsync(Donation donation, CancellationToken ct = default);
        Task<Donation?> GetByReferenceAsync(string reference, CancellationToken ct = default);
        Task<Donation?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<List<Donation>> GetByCampaignIdAsync(Guid campaignId, CancellationToken ct = default);
        Task UpdateAsync(Donation donation, CancellationToken ct = default);
        Task SaveChangesAsync(CancellationToken ct = default);
    }
}
