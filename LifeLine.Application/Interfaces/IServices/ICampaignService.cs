using LifeLine.Application.Common.Response;
using LifeLine.Application.Common.Response.Campaign;
using LifeLine.Application.DTO.Campaign;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LifeLine.Application.Interfaces
{
    public interface ICampaignService
    {
        //Task<BaseResponse<CampaignResponseDto>> CreateCampaignAsync(
        //    string creatorId, CreateCampaignDto dto, CancellationToken ct = default);

        Task<BaseResponse<CampaignResponseDto>> GetCampaignBySlugAsync(
            string slug, CancellationToken ct = default);

        Task<BaseResponse<CampaignResponseDto>> CreateCampaignAsync(
            string creatorId, string creatorName, string creatorEmail,
            CreateCampaignDto dto, CancellationToken ct = default);

        Task<BaseResponse<CampaignResponseDto>> GetCampaignByIdAsync(
            Guid id, CancellationToken ct = default);

        Task<BaseResponse<List<CampaignResponseDto>>> GetAllCampaignsAsync(
            int page, int pageSize, CancellationToken ct = default);

        Task<BaseResponse<List<CampaignResponseDto>>> GetMyCampaignsAsync(
            string creatorId, CancellationToken ct = default);

        Task<BaseResponse<CampaignResponseDto>> UpdateCampaignAsync(
            Guid id, string creatorId, UpdateCampaignDto dto, CancellationToken ct = default);

        Task<BaseResponse<string>> UploadCoverImageAsync(
            Guid campaignId, string creatorId, IFormFile file, CancellationToken ct = default);

        Task<BaseResponse<string>> UploadMedicalDocumentAsync(
            Guid campaignId, string creatorId, IFormFile file,
            string fileType, CancellationToken ct = default);

        Task<BaseResponse<string>> PostUpdateAsync(
            Guid campaignId, string creatorId,
            PostCampaignUpdateDto dto, CancellationToken ct = default);

        Task<BaseResponse<List<CampaignUpdateResponseDto>>> GetCampaignUpdatesAsync(
            Guid campaignId, CancellationToken ct = default);

        Task<BaseResponse<string>> DeleteCampaignAsync(
            Guid id, string requesterId, string requesterRole, CancellationToken ct = default);
    }
}
