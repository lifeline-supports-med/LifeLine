using LifeLine.Application.Common.Response;
using LifeLine.Application.DTO.Donation;

namespace LifeLine.Application.Interfaces.IServices;

public interface IDonationService
{
    Task<BaseResponse<InitiateDonationResponseDto>> InitiateDonationAsync(
        InitiateDonationDto dto, string? donorId,
        string? donorName, string? donorEmail,
        CancellationToken ct = default);

    Task<BaseResponse<string>> VerifyDonationAsync(
        string reference, CancellationToken ct = default);

    Task<BaseResponse<List<DonationResponseDto>>> GetCampaignDonationsAsync(
        Guid campaignId, CancellationToken ct = default);
}