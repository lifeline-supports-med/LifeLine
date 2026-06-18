using LifeLine.Application.Common.Response;
using LifeLine.Application.DTO.Admin;
using LifeLine.Application.Interfaces;
using LifeLine.Application.Interfaces.IRepository;
using LifeLine.Application.Interfaces.IServices;
using LifeLine.Domain.Entities;
using LifeLine.Domain.Enum;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LifeLine.Persistence.Services;

public class AdminService : IAdminService
{
    private readonly ICampaignRepository _campaignRepo;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailService _emailService;
    private readonly ILogger<AdminService> _logger;

    public AdminService(
        ICampaignRepository campaignRepo,
        UserManager<ApplicationUser> userManager,
        IEmailService emailService,
        ILogger<AdminService> logger)
    {
        _campaignRepo = campaignRepo;
        _userManager = userManager;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<BaseResponse<List<AdminCampaignResponseDto>>> GetAllCampaignsAsync(
        int page, int pageSize, string? status, CancellationToken ct = default)
    {
        try
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            List<Campaign> campaigns;

            if (!string.IsNullOrWhiteSpace(status))
                campaigns = await _campaignRepo.GetAllByStatusAsync(status, page, pageSize, ct);
            else
                campaigns = await _campaignRepo.GetAllForAdminAsync(page, pageSize, ct);

            var result = campaigns.Select(MapToAdminDto).ToList();

            return BaseResponse<List<AdminCampaignResponseDto>>.Success(
                result, $"Retrieved {result.Count} campaigns.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("GetAllCampaignsAsync (admin) was cancelled.");
            return BaseResponse<List<AdminCampaignResponseDto>>.Failure(
                "Request was cancelled.", statusCode: 499);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error fetching admin campaigns: {Error}", ex.Message);
            return BaseResponse<List<AdminCampaignResponseDto>>.Failure(
                "An error occurred.", statusCode: 500);
        }
    }

    public async Task<BaseResponse<AdminCampaignResponseDto>> GetCampaignDetailsAsync(
        Guid campaignId, CancellationToken ct = default)
    {
        try
        {
            var campaign = await _campaignRepo.GetByIdAsync(campaignId, ct);
            if (campaign is null)
                return BaseResponse<AdminCampaignResponseDto>.Failure(
                    "Campaign not found.", statusCode: 404);

            return BaseResponse<AdminCampaignResponseDto>.Success(
                MapToAdminDto(campaign));
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("GetCampaignDetailsAsync (admin) was cancelled.");
            return BaseResponse<AdminCampaignResponseDto>.Failure(
                "Request was cancelled.", statusCode: 499);
        }
    }

    public async Task<BaseResponse<string>> VerifyCampaignAsync(
        Guid campaignId, string adminId, CancellationToken ct = default)
    {
        try
        {
            var campaign = await _campaignRepo.GetByIdAsync(campaignId, ct);
            if (campaign is null)
                return BaseResponse<string>.Failure(
                    "Campaign not found.", statusCode: 404);

            if (campaign.IsVerified)
                return BaseResponse<string>.Failure(
                    "This campaign is already verified.", statusCode: 400);

            if (campaign.Status == CampaignStatus.Rejected)
                return BaseResponse<string>.Failure(
                    "A rejected campaign cannot be verified directly. Ask the creator to resubmit.",
                    statusCode: 400);

            campaign.IsVerified = true;
            campaign.Status = CampaignStatus.Verified;
            campaign.VerifiedAt = DateTime.UtcNow;
            campaign.UpdatedAt = DateTime.UtcNow;
            campaign.RejectionReason = null;

            await _campaignRepo.UpdateAsync(campaign, ct);
            await _campaignRepo.SaveChangesAsync(ct);

            if (campaign.Creator is not null)
            {
                await _emailService.SendCampaignApprovedEmailAsync(
                    campaign.Creator.Email!,
                    $"{campaign.Creator.FirstName} {campaign.Creator.LastName}".Trim(),
                    campaign.Title,
                    campaign.Slug);
            }

            _logger.LogInformation(
                "Campaign {CampaignId} verified by admin {AdminId}",
                campaignId, adminId);

            return BaseResponse<string>.Success(
                null!, "Campaign verified and is now live.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("VerifyCampaignAsync was cancelled.");
            return BaseResponse<string>.Failure(
                "Request was cancelled.", statusCode: 499);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                "Error verifying campaign {CampaignId}: {Error}",
                campaignId, ex.Message);
            return BaseResponse<string>.Failure(
                "An error occurred.", statusCode: 500);
        }
    }

    public async Task<BaseResponse<string>> RejectCampaignAsync(
        Guid campaignId, string adminId,
        RejectCampaignDto dto, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dto.Reason))
                return BaseResponse<string>.Failure(
                    "A rejection reason is required.", statusCode: 400);

            var campaign = await _campaignRepo.GetByIdAsync(campaignId, ct);
            if (campaign is null)
                return BaseResponse<string>.Failure(
                    "Campaign not found.", statusCode: 404);

            if (campaign.Status == CampaignStatus.Completed)
                return BaseResponse<string>.Failure(
                    "A completed campaign cannot be rejected.", statusCode: 400);

            campaign.Status = CampaignStatus.Rejected;
            campaign.IsVerified = false;
            campaign.RejectionReason = dto.Reason.Trim();
            campaign.UpdatedAt = DateTime.UtcNow;

            await _campaignRepo.UpdateAsync(campaign, ct);
            await _campaignRepo.SaveChangesAsync(ct);

            if (campaign.Creator is not null)
            {
                await _emailService.SendCampaignRejectedEmailAsync(
                    campaign.Creator.Email!,
                    $"{campaign.Creator.FirstName} {campaign.Creator.LastName}".Trim(),
                    campaign.Title,
                    dto.Reason);
            }

            _logger.LogInformation(
                "Campaign {CampaignId} rejected by admin {AdminId}. Reason: {Reason}",
                campaignId, adminId, dto.Reason);

            return BaseResponse<string>.Success(
                null!, "Campaign rejected. The creator has been notified.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("RejectCampaignAsync was cancelled.");
            return BaseResponse<string>.Failure(
                "Request was cancelled.", statusCode: 499);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                "Error rejecting campaign {CampaignId}: {Error}",
                campaignId, ex.Message);
            return BaseResponse<string>.Failure(
                "An error occurred.", statusCode: 500);
        }
    }

    public async Task<BaseResponse<List<AdminUserResponseDto>>> GetAllUsersAsync(
        int page, int pageSize, CancellationToken ct = default)
    {
        try
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var users = await _userManager.Users
                .Include(u => u.Campaigns)
                .OrderByDescending(u => u.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            var result = users.Select(u => new AdminUserResponseDto
            {
                Id = u.Id,
                FullName = $"{u.FirstName} {u.LastName}".Trim(),
                Email = u.Email!,
                PhoneNumber = u.PhoneNumber ?? string.Empty,
                Role = u.Role,
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt,
                CampaignCount = u.Campaigns?.Count ?? 0
            }).ToList();

            return BaseResponse<List<AdminUserResponseDto>>.Success(
                result, $"Retrieved {result.Count} users.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("GetAllUsersAsync (admin) was cancelled.");
            return BaseResponse<List<AdminUserResponseDto>>.Failure(
                "Request was cancelled.", statusCode: 499);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error fetching users: {Error}", ex.Message);
            return BaseResponse<List<AdminUserResponseDto>>.Failure(
                "An error occurred.", statusCode: 500);
        }
    }

    public async Task<BaseResponse<string>> SuspendUserAsync(
        string userId, string adminId, CancellationToken ct = default)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user is null)
                return BaseResponse<string>.Failure(
                    "User not found.", statusCode: 404);

            if (!user.IsActive)
                return BaseResponse<string>.Failure(
                    "User is already suspended.", statusCode: 400);

            if (user.Role == "SuperAdmin")
                return BaseResponse<string>.Failure(
                    "A SuperAdmin account cannot be suspended.", statusCode: 403);

            user.IsActive = false;
            await _userManager.UpdateAsync(user);

            _logger.LogInformation(
                "User {UserId} suspended by admin {AdminId}", userId, adminId);

            return BaseResponse<string>.Success(
                null!, "User account suspended successfully.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("SuspendUserAsync was cancelled.");
            return BaseResponse<string>.Failure(
                "Request was cancelled.", statusCode: 499);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                "Error suspending user {UserId}: {Error}", userId, ex.Message);
            return BaseResponse<string>.Failure(
                "An error occurred.", statusCode: 500);
        }
    }

    public async Task<BaseResponse<string>> ReactivateUserAsync(
        string userId, string adminId, CancellationToken ct = default)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user is null)
                return BaseResponse<string>.Failure(
                    "User not found.", statusCode: 404);

            if (user.IsActive)
                return BaseResponse<string>.Failure(
                    "User is already active.", statusCode: 400);

            user.IsActive = true;
            await _userManager.UpdateAsync(user);

            _logger.LogInformation(
                "User {UserId} reactivated by admin {AdminId}", userId, adminId);

            return BaseResponse<string>.Success(
                null!, "User account reactivated successfully.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("ReactivateUserAsync was cancelled.");
            return BaseResponse<string>.Failure(
                "Request was cancelled.", statusCode: 499);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                "Error reactivating user {UserId}: {Error}", userId, ex.Message);
            return BaseResponse<string>.Failure(
                "An error occurred.", statusCode: 500);
        }
    }

    private static AdminCampaignResponseDto MapToAdminDto(Campaign campaign) =>
        new()
        {
            Id = campaign.Id,
            Title = campaign.Title,
            PatientName = campaign.PatientName,
            MedicalCondition = campaign.MedicalCondition,
            Story = campaign.Story,
            GoalAmount = campaign.GoalAmount,
            AmountRaised = campaign.AmountRaised,
            CoverImageUrl = campaign.CoverImageUrl,
            Slug = campaign.Slug,
            Status = campaign.Status.ToString(),
            IsVerified = campaign.IsVerified,
            VerifiedAt = campaign.VerifiedAt,
            RejectionReason = campaign.RejectionReason,
            SurgeryDate = campaign.SurgeryDate,
            BankName = campaign.BankName ?? string.Empty,
            AccountNumber = campaign.AccountNumber ?? string.Empty,
            AccountName = campaign.AccountName ?? string.Empty,
            DocumentCount = campaign.Documents?.Count ?? 0,
            DonorCount = campaign.Donations?.Count ?? 0,
            CreatedAt = campaign.CreatedAt,
            CreatorName = campaign.Creator is not null
                ? $"{campaign.Creator.FirstName} {campaign.Creator.LastName}".Trim()
                : string.Empty,
            CreatorEmail = campaign.Creator?.Email ?? string.Empty,
            Documents = campaign.Documents?.Select(d => new AdminDocumentDto
            {
                Id = d.Id,
                FileUrl = d.FileUrl,
                FileName = d.FileName,
                FileType = d.FileType,
                UploadedAt = d.CreatedAt
            }).ToList() ?? []
        };
}