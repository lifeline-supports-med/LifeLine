using LifeLine.Application.Common.Response;
using LifeLine.Application.DTO.Admin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LifeLine.Application.Interfaces.IServices
{
    public interface IAdminService
    {
        Task<BaseResponse<List<AdminCampaignResponseDto>>> GetAllCampaignsAsync(
            int page, int pageSize, string? status, CancellationToken ct = default);

        Task<BaseResponse<AdminCampaignResponseDto>> GetCampaignDetailsAsync(
            Guid campaignId, CancellationToken ct = default);

        Task<BaseResponse<string>> VerifyCampaignAsync(
            Guid campaignId, string adminId, CancellationToken ct = default);

        Task<BaseResponse<string>> RejectCampaignAsync(
            Guid campaignId, string adminId,
            RejectCampaignDto dto, CancellationToken ct = default);

        Task<BaseResponse<List<AdminUserResponseDto>>> GetAllUsersAsync(
            int page, int pageSize, CancellationToken ct = default);

        Task<BaseResponse<string>> SuspendUserAsync(
            string userId, string adminId, CancellationToken ct = default);

        Task<BaseResponse<string>> ReactivateUserAsync(
            string userId, string adminId, CancellationToken ct = default);
    }
}
