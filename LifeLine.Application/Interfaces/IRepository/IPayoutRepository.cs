using LifeLine.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LifeLine.Application.Interfaces.IRepository
{
    public interface IPayoutRepository
    {
        Task AddAsync(Payout payout, CancellationToken ct = default);
        Task<Payout?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<List<Payout>> GetByCampaignIdAsync(Guid campaignId, CancellationToken ct = default);
        Task<List<Payout>> GetAllAsync(int page, int pageSize, string? status, CancellationToken ct = default);
        Task<bool> HasPendingPayoutAsync(Guid campaignId, CancellationToken ct = default);
        Task UpdateAsync(Payout payout, CancellationToken ct = default);
        Task SaveChangesAsync(CancellationToken ct = default);
    }
}
