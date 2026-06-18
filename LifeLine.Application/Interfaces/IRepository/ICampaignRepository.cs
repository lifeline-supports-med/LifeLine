using LifeLine.Domain.Entities;

namespace LifeLine.Application.Interfaces.IRepository
{
    public interface ICampaignRepository
    {
        Task<Campaign?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<Campaign?> GetBySlugAsync(string slug, CancellationToken ct = default);
        Task<List<Campaign>> GetAllVerifiedAsync(int page, int pageSize, CancellationToken ct = default);
        Task<List<Campaign>> GetByCreatorIdAsync(string creatorId, CancellationToken ct = default);
        Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default);
        Task<List<Campaign>> GetAllByStatusAsync(string status, int page, int pageSize, CancellationToken ct = default);

        Task<List<Campaign>> GetAllForAdminAsync(int page, int pageSize, CancellationToken ct = default);
        Task AddAsync(Campaign campaign, CancellationToken ct = default);
        Task UpdateAsync(Campaign campaign, CancellationToken ct = default);
        Task DeleteAsync(Campaign campaign, CancellationToken ct = default);
        Task AddDocumentAsync(MedicalDocument document, CancellationToken ct = default);
        Task AddUpdateAsync(MedicalUpdate update, CancellationToken ct = default);
        Task<List<MedicalUpdate>> GetUpdatesAsync(Guid campaignId, CancellationToken ct = default);
        Task SaveChangesAsync(CancellationToken ct = default);
    }
}
