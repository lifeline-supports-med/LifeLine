using LifeLine.Application.Common.Response;
using LifeLine.Application.DTO.Payout;
using LifeLine.Application.Interfaces.IRepository;
using LifeLine.Application.Interfaces.IServices;
using LifeLine.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace LifeLine.Persistence.Services;

public class PayoutService : IPayoutService
{
    private readonly IPayoutRepository _payoutRepo;
    private readonly ICampaignRepository _campaignRepo;
    private readonly IEmailService _emailService;
    private readonly ILogger<PayoutService> _logger;

    public PayoutService(
        IPayoutRepository payoutRepo,
        ICampaignRepository campaignRepo,
        IEmailService emailService,
        ILogger<PayoutService> logger)
    {
        _payoutRepo = payoutRepo;
        _campaignRepo = campaignRepo;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<BaseResponse<PayoutResponseDto>> RequestPayoutAsync(
        string requesterId, RequestPayoutDto dto, CancellationToken ct = default)
    {
        try
        {
            var campaign = await _campaignRepo.GetByIdAsync(dto.CampaignId, ct);
            if (campaign is null)
                return BaseResponse<PayoutResponseDto>.Failure(
                    "Campaign not found.", statusCode: 404);

            if (campaign.CreatorId != requesterId)
                return BaseResponse<PayoutResponseDto>.Failure(
                    "You are not authorized to request a payout for this campaign.",
                    statusCode: 403);

            if (!campaign.IsVerified)
                return BaseResponse<PayoutResponseDto>.Failure(
                    "Only verified campaigns can request payouts.", statusCode: 400);

            var hasPending = await _payoutRepo.HasPendingPayoutAsync(dto.CampaignId, ct);
            if (hasPending)
                return BaseResponse<PayoutResponseDto>.Failure(
                    "There is already a pending payout request for this campaign.",
                    statusCode: 400);

            if (dto.Amount > campaign.AmountRaised)
                return BaseResponse<PayoutResponseDto>.Failure(
                    $"Requested amount ₦{dto.Amount:N0} exceeds amount raised ₦{campaign.AmountRaised:N0}.",
                    statusCode: 400);

            if (dto.Amount < 1000)
                return BaseResponse<PayoutResponseDto>.Failure(
                    "Minimum payout amount is ₦1,000.", statusCode: 400);

            var payout = new Payout
            {
                CampaignId = dto.CampaignId,
                Amount = dto.Amount,
                BankName = campaign.BankName ?? string.Empty,
                AccountNumber = campaign.AccountNumber ?? string.Empty,
                AccountName = campaign.AccountName ?? string.Empty,
                RequestedById = requesterId,
                Status = "Pending",
                IsApproved = false,
                Notes = dto.Notes?.Trim(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _payoutRepo.AddAsync(payout, ct);
            await _payoutRepo.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Payout requested: {PayoutId} for campaign {CampaignId} — ₦{Amount}",
                payout.Id, dto.CampaignId, dto.Amount);

            return BaseResponse<PayoutResponseDto>.Success(
                MapToDto(payout, campaign.Title),
                "Payout request submitted. Our team will process it shortly.",
                201);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("RequestPayoutAsync was cancelled.");
            return BaseResponse<PayoutResponseDto>.Failure(
                "Request was cancelled.", statusCode: 499);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                "Error requesting payout for {CampaignId}: {Error}",
                dto.CampaignId, ex.Message);
            return BaseResponse<PayoutResponseDto>.Failure(
                "An error occurred while submitting payout request.", statusCode: 500);
        }
    }

    public async Task<BaseResponse<List<PayoutResponseDto>>> GetMyPayoutsAsync(
        string requesterId, CancellationToken ct = default)
    {
        try
        {
            // Get all campaigns owned by this user, then their payouts
            var campaigns = await _campaignRepo.GetByCreatorIdAsync(requesterId, ct);
            var campaignIds = campaigns.Select(c => c.Id).ToList();

            var allPayouts = new List<PayoutResponseDto>();

            foreach (var campaignId in campaignIds)
            {
                var payouts = await _payoutRepo.GetByCampaignIdAsync(campaignId, ct);
                var campaign = campaigns.First(c => c.Id == campaignId);
                allPayouts.AddRange(payouts.Select(p => MapToDto(p, campaign.Title)));
            }

            var ordered = allPayouts
                .OrderByDescending(p => p.CreatedAt)
                .ToList();

            return BaseResponse<List<PayoutResponseDto>>.Success(
                ordered, $"Retrieved {ordered.Count} payout requests.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("GetMyPayoutsAsync was cancelled.");
            return BaseResponse<List<PayoutResponseDto>>.Failure(
                "Request was cancelled.", statusCode: 499);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error fetching payouts: {Error}", ex.Message);
            return BaseResponse<List<PayoutResponseDto>>.Failure(
                "An error occurred.", statusCode: 500);
        }
    }

    public async Task<BaseResponse<List<PayoutResponseDto>>> GetAllPayoutsAsync(
        int page, int pageSize, string? status, CancellationToken ct = default)
    {
        try
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var payouts = await _payoutRepo.GetAllAsync(page, pageSize, status, ct);
            var result = payouts.Select(p =>
                MapToDto(p, p.Campaign?.Title ?? string.Empty)).ToList();

            return BaseResponse<List<PayoutResponseDto>>.Success(
                result, $"Retrieved {result.Count} payout requests.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("GetAllPayoutsAsync was cancelled.");
            return BaseResponse<List<PayoutResponseDto>>.Failure(
                "Request was cancelled.", statusCode: 499);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error fetching all payouts: {Error}", ex.Message);
            return BaseResponse<List<PayoutResponseDto>>.Failure(
                "An error occurred.", statusCode: 500);
        }
    }

    public async Task<BaseResponse<string>> ApprovePayoutAsync(
        Guid payoutId, string adminId, CancellationToken ct = default)
    {
        try
        {
            var payout = await _payoutRepo.GetByIdAsync(payoutId, ct);
            if (payout is null)
                return BaseResponse<string>.Failure(
                    "Payout request not found.", statusCode: 404);

            if (payout.Status != "Pending")
                return BaseResponse<string>.Failure(
                    $"This payout has already been {payout.Status.ToLower()}.",
                    statusCode: 400);

            payout.IsApproved = true;
            payout.Status = "Approved";
            payout.ApprovedAt = DateTime.UtcNow;
            payout.ApprovedByAdminId = adminId;
            payout.UpdatedAt = DateTime.UtcNow;

            await _payoutRepo.UpdateAsync(payout, ct);
            await _payoutRepo.SaveChangesAsync(ct);

            var campaign = await _campaignRepo.GetByIdAsync(payout.CampaignId, ct);
            if (campaign?.Creator is not null)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _emailService.SendPayoutApprovedEmailAsync(
                            campaign.Creator.Email!,
                            $"{campaign.Creator.FirstName} {campaign.Creator.LastName}".Trim(),
                            campaign.Title,
                            payout.Amount,
                            payout.BankName,
                            payout.AccountNumber);
                    }
                    catch (Exception emailEx)
                    {
                        _logger.LogError(
                            "Failed to send payout approval email: {Error}",
                            emailEx.Message);
                    }
                });
            }

            _logger.LogInformation(
                "Payout {PayoutId} approved by admin {AdminId}", payoutId, adminId);

            return BaseResponse<string>.Success(
                null!, "Payout approved. The creator has been notified.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("ApprovePayoutAsync was cancelled.");
            return BaseResponse<string>.Failure(
                "Request was cancelled.", statusCode: 499);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                "Error approving payout {PayoutId}: {Error}", payoutId, ex.Message);
            return BaseResponse<string>.Failure(
                "An error occurred.", statusCode: 500);
        }
    }

    public async Task<BaseResponse<string>> RejectPayoutAsync(
        Guid payoutId, string adminId,
        RejectPayoutDto dto, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dto.Reason))
                return BaseResponse<string>.Failure(
                    "A rejection reason is required.", statusCode: 400);

            var payout = await _payoutRepo.GetByIdAsync(payoutId, ct);
            if (payout is null)
                return BaseResponse<string>.Failure(
                    "Payout request not found.", statusCode: 404);

            if (payout.Status != "Pending")
                return BaseResponse<string>.Failure(
                    $"This payout has already been {payout.Status.ToLower()}.",
                    statusCode: 400);

            payout.Status = "Rejected";
            payout.IsApproved = false;
            payout.RejectionReason = dto.Reason.Trim();
            payout.UpdatedAt = DateTime.UtcNow;

            await _payoutRepo.UpdateAsync(payout, ct);
            await _payoutRepo.SaveChangesAsync(ct);

            // Notify creator
            var campaign = await _campaignRepo.GetByIdAsync(payout.CampaignId, ct);
            if (campaign?.Creator is not null)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _emailService.SendPayoutRejectedEmailAsync(
                            campaign.Creator.Email!,
                            $"{campaign.Creator.FirstName} {campaign.Creator.LastName}".Trim(),
                            campaign.Title,
                            payout.Amount,
                            dto.Reason);
                    }
                    catch (Exception emailEx)
                    {
                        _logger.LogError(
                            "Failed to send payout rejection email: {Error}",
                            emailEx.Message);
                    }
                });
            }

            _logger.LogInformation(
                "Payout {PayoutId} rejected by admin {AdminId}. Reason: {Reason}",
                payoutId, adminId, dto.Reason);

            return BaseResponse<string>.Success(
                null!, "Payout rejected. The creator has been notified.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("RejectPayoutAsync was cancelled.");
            return BaseResponse<string>.Failure(
                "Request was cancelled.", statusCode: 499);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                "Error rejecting payout {PayoutId}: {Error}", payoutId, ex.Message);
            return BaseResponse<string>.Failure(
                "An error occurred.", statusCode: 500);
        }
    }

    private static PayoutResponseDto MapToDto(Payout payout, string campaignTitle) =>
        new()
        {
            Id = payout.Id,
            CampaignId = payout.CampaignId,
            CampaignTitle = campaignTitle,
            Amount = payout.Amount,
            BankName = payout.BankName,
            AccountNumber = payout.AccountNumber,
            AccountName = payout.AccountName,
            Status = payout.Status,
            IsApproved = payout.IsApproved,
            ApprovedAt = payout.ApprovedAt,
            RejectionReason = payout.RejectionReason,
            Notes = payout.Notes,
            RequestedById = payout.RequestedById,
            CreatedAt = payout.CreatedAt
        };
}