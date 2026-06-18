using LifeLine.Application.Common.Response;
using LifeLine.Application.DTO.Payout;

namespace LifeLine.Application.Interfaces.IServices;

public interface IPayoutService
{
    Task<BaseResponse<PayoutResponseDto>> RequestPayoutAsync(
        string requesterId, RequestPayoutDto dto, CancellationToken ct = default);

    Task<BaseResponse<List<PayoutResponseDto>>> GetMyPayoutsAsync(
        string requesterId, CancellationToken ct = default);

    Task<BaseResponse<List<PayoutResponseDto>>> GetAllPayoutsAsync(
        int page, int pageSize, string? status, CancellationToken ct = default);

    Task<BaseResponse<string>> ApprovePayoutAsync(
        Guid payoutId, string adminId, CancellationToken ct = default);

    Task<BaseResponse<string>> RejectPayoutAsync(
        Guid payoutId, string adminId,
        RejectPayoutDto dto, CancellationToken ct = default);
}