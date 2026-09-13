using LifeLine.Application.Common.Response;
using LifeLine.Application.Common.Response.Campaign;
using LifeLine.Application.DTO.Campaign;
using LifeLine.Application.Helpers;
using LifeLine.Application.Interfaces;
using LifeLine.Application.Interfaces.IRepository;
using LifeLine.Application.Interfaces.IServices;
using LifeLine.Domain.Entities;
using LifeLine.Domain.Enum;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LifeLine.Persistence.Services;

public class CampaignService : ICampaignService
{
    private readonly ICampaignRepository _repo;
    private readonly ICloudinaryService _cloudinary;
    private readonly IEmailService _emailService;
    private readonly IPaystackService _paystackService;
    private readonly IConfiguration _config;
    private readonly ILogger<CampaignService> _logger;

    public CampaignService(
        ICampaignRepository repo,
        ICloudinaryService cloudinary,
        IEmailService emailService,
        IPaystackService paystackService,
        IConfiguration config,
        ILogger<CampaignService> logger)
    {
        _repo = repo;
        _cloudinary = cloudinary;
        _emailService = emailService;
        _paystackService = paystackService;
        _config = config;
        _logger = logger;
    }

    public async Task<BaseResponse<CampaignResponseDto>> CreateCampaignAsync(
    string creatorId, string creatorName, string creatorEmail,
    CreateCampaignDto dto, CancellationToken ct = default)
    {
        try
        {
            var slug = SlugHelper.Generate(dto.PatientName, dto.MedicalCondition);
            while (await _repo.SlugExistsAsync(slug, ct))
                slug = SlugHelper.Generate(dto.PatientName, dto.MedicalCondition);

            var campaign = new Campaign
            {
                Title = dto.Title.Trim(),
                PatientName = dto.PatientName.Trim(),
                MedicalCondition = dto.MedicalCondition.Trim(),
                Story = dto.Story.Trim(),
                GoalAmount = dto.GoalAmount,
                SurgeryDate = dto.SurgeryDate,
                BankName = dto.BankName.Trim(),
                BankCode = dto.BankCode.Trim(),
                AccountNumber = dto.AccountNumber.Trim(),
                AccountName = dto.AccountName.Trim(),
                Slug = slug,
                Status = CampaignStatus.Pending,
                IsVerified = false,
                IsPaymentReady = false,
                CreatorId = creatorId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _repo.AddAsync(campaign, ct);
            await _repo.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Campaign created: {CampaignId} by {CreatorId}",
                campaign.Id, creatorId);

            // Email failures must NEVER block campaign creation
            try
            {
                await _emailService.SendCampaignSubmittedToCreatorAsync(
                    creatorEmail, creatorName, campaign.Title);
            }
            catch (Exception emailEx)
            {
                _logger.LogError(
                    "Failed to send creator email for {CampaignId}: {Error}",
                    campaign.Id, emailEx.Message);
            }

            try
            {
                var adminEmail = _config["AdminSettings:NotificationEmail"]!;
                await _emailService.SendNewCampaignAlertToAdminAsync(
                    adminEmail, creatorName, campaign.Title, campaign.Id);
            }
            catch (Exception emailEx)
            {
                _logger.LogError(
                    "Failed to send admin alert for {CampaignId}: {Error}",
                    campaign.Id, emailEx.Message);
            }

            return BaseResponse<CampaignResponseDto>.Success(
                MapToDto(campaign),
                "Campaign created successfully. It is now pending verification.",
                201);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("CreateCampaignAsync was cancelled.");
            return BaseResponse<CampaignResponseDto>.Failure(
                "Request was cancelled.", statusCode: 499);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                "Error creating campaign for {CreatorId}: {Error}",
                creatorId, ex.Message);
            return BaseResponse<CampaignResponseDto>.Failure(
                "An error occurred while creating the campaign.", statusCode: 500);
        }
    }


    public async Task<BaseResponse<CampaignResponseDto>> GetCampaignBySlugAsync(
        string slug, CancellationToken ct = default)
    {
        try
        {
            var campaign = await _repo.GetBySlugAsync(slug, ct);
            if (campaign is null)
                return BaseResponse<CampaignResponseDto>.Failure(
                    "Campaign not found.", statusCode: 404);

            return BaseResponse<CampaignResponseDto>.Success(MapToDto(campaign));
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("GetCampaignBySlugAsync was cancelled.");
            return BaseResponse<CampaignResponseDto>.Failure(
                "Request was cancelled.", statusCode: 499);
        }
    }

    public async Task<BaseResponse<CampaignResponseDto>> GetCampaignByIdAsync(
        Guid id, CancellationToken ct = default)
    {
        try
        {
            var campaign = await _repo.GetByIdAsync(id, ct);
            if (campaign is null)
                return BaseResponse<CampaignResponseDto>.Failure(
                    "Campaign not found.", statusCode: 404);

            return BaseResponse<CampaignResponseDto>.Success(MapToDto(campaign));
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("GetCampaignByIdAsync was cancelled.");
            return BaseResponse<CampaignResponseDto>.Failure(
                "Request was cancelled.", statusCode: 499);
        }
    }

    public async Task<BaseResponse<List<CampaignResponseDto>>> GetAllCampaignsAsync(
        int page, int pageSize, CancellationToken ct = default)
    {
        try
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 50);

            var campaigns = await _repo.GetAllVerifiedAsync(page, pageSize, ct);
            var result = campaigns.Select(MapToDto).ToList();

            return BaseResponse<List<CampaignResponseDto>>.Success(
                result, $"Retrieved {result.Count} campaigns.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("GetAllCampaignsAsync was cancelled.");
            return BaseResponse<List<CampaignResponseDto>>.Failure(
                "Request was cancelled.", statusCode: 499);
        }
    }


    public async Task<BaseResponse<string>> ActivatePaymentsAsync(Guid campaignId, CancellationToken ct = default)
    {
        try
        {
            var campaign = await _repo.GetByIdAsync(campaignId, ct);
            if (campaign is null)
                return BaseResponse<string>.Failure("Campaign not found.", statusCode: 404);

            if (string.IsNullOrWhiteSpace(campaign.AccountNumber))
                return BaseResponse<string>.Failure("Campaign is missing bank account number.", statusCode: 400);

            // 1. Sanitize Account Number (remove spaces/dashes, strip leading 0 if 11-digit phone number)
            var accountNumber = campaign.AccountNumber.Trim().Replace(" ", "").Replace("-", "");
            if (accountNumber.Length == 11 && accountNumber.StartsWith("0"))
                accountNumber = accountNumber[1..];

            // 2. Resolve Bank Code if empty or invalid
            var bankCode = campaign.BankCode?.Trim();
            if (string.IsNullOrWhiteSpace(bankCode) || !bankCode.All(char.IsDigit))
            {
                bankCode = ResolveBankCode(campaign.BankName ?? "");
            }

            if (string.IsNullOrWhiteSpace(bankCode))
                return BaseResponse<string>.Failure("Could not determine a valid bank code for this campaign.", statusCode: 400);

            campaign.AccountNumber = accountNumber;
            campaign.BankCode = bankCode;

            // 3. Resolve Account Name via NIBSS (Paystack)
            _logger.LogInformation("Resolving bank details for campaign {Id}: Account={Account}, BankCode={BankCode}", 
                campaignId, accountNumber, bankCode);

            var resolveRes = await _paystackService.ResolveAccountNumberAsync(accountNumber, bankCode, ct);
            if (!resolveRes.IsSuccess || resolveRes.Data is null)
            {
                campaign.Status = CampaignStatus.PaymentSetupFailed;
                campaign.PaymentSetupErrorMessage = resolveRes.Message ?? "Invalid account number or bank code.";
                await _repo.UpdateAsync(campaign, ct);
                await _repo.SaveChangesAsync(ct);
                return BaseResponse<string>.Failure(campaign.PaymentSetupErrorMessage, statusCode: 400);
            }

            // Save the official NIBSS-verified account name
            campaign.AccountName = resolveRes.Data.AccountName;
            campaign.IsAccountNameResolved = true;

            // 4. Ensure / Create Paystack Subaccount
            var subAccountCode = await _paystackService.EnsureSubaccountAsync(
                $"Lifeline - {campaign.PatientName}",
                accountNumber,
                bankCode,
                idempotencyKey: $"SUB-{campaign.CampaignId}",
                ct: ct);

            if (string.IsNullOrWhiteSpace(subAccountCode))
            {
                campaign.Status = CampaignStatus.PaymentSetupFailed;
                campaign.PaymentSetupErrorMessage = "Failed to create payment subaccount with Paystack.";
                await _repo.UpdateAsync(campaign, ct);
                await _repo.SaveChangesAsync(ct);
                return BaseResponse<string>.Failure(campaign.PaymentSetupErrorMessage, statusCode: 502);
            }

            campaign.SubAccountCode = subAccountCode;

            // 5. Inspect Paystack Subaccount Verification & Settlement Status
            var subaccountDetails = await _paystackService.GetSubaccountAsync(subAccountCode, ct);
            var isVerified = subaccountDetails.IsSuccess && (subaccountDetails.Data?.IsVerified ?? false);
            var isActive = subaccountDetails.IsSuccess && (subaccountDetails.Data?.Active ?? false);

            campaign.PaystackSubaccountIsVerified = isVerified;
            campaign.PaystackSubaccountIsActive = isActive;

            // 6. Evaluate Settlement Readiness (An active subaccount with resolved NIBSS bank account is ready to receive split payments)
            if (!string.IsNullOrWhiteSpace(subAccountCode) && (isActive || !subaccountDetails.IsSuccess))
            {
                campaign.IsPaymentReady = true;
                campaign.Status = CampaignStatus.Verified;
                campaign.PaymentSetupErrorMessage = null;
                campaign.PaymentActivatedAt = DateTime.UtcNow;
            }
            else
            {
                campaign.IsPaymentReady = false;
                campaign.Status = CampaignStatus.PaymentSetupPending;
                campaign.PaymentSetupErrorMessage = "Subaccount created but is currently inactive on Paystack.";
            }

            campaign.UpdatedAt = DateTime.UtcNow;
            await _repo.UpdateAsync(campaign, ct);
            await _repo.SaveChangesAsync(ct);

            _logger.LogInformation("Payments activated for campaign {CampaignId}: Subaccount={SubAccount}, Ready={IsReady}",
                campaignId, subAccountCode, campaign.IsPaymentReady);

            return BaseResponse<string>.Success(campaign.SubAccountCode, "Payment activation processed.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("ActivatePaymentsAsync was cancelled for campaign {CampaignId}.", campaignId);
            return BaseResponse<string>.Failure("Request was cancelled.", statusCode: 499);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error activating payments for campaign {CampaignId}: {Error}", campaignId, ex.Message);
            return BaseResponse<string>.Failure("An error occurred while activating payments.", statusCode: 500);
        }
    }

    public async Task<BaseResponse<List<CampaignResponseDto>>> GetMyCampaignsAsync(
        string creatorId, CancellationToken ct = default)
    {
        try
        {
            var campaigns = await _repo.GetByCreatorIdAsync(creatorId, ct);
            var result = campaigns.Select(MapToDto).ToList();

            return BaseResponse<List<CampaignResponseDto>>.Success(
                result, $"Retrieved {result.Count} campaigns.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("GetMyCampaignsAsync was cancelled.");
            return BaseResponse<List<CampaignResponseDto>>.Failure(
                "Request was cancelled.", statusCode: 499);
        }
    }

    public async Task<BaseResponse<CampaignResponseDto>> UpdateCampaignAsync(
        Guid id, string creatorId, UpdateCampaignDto dto, CancellationToken ct = default)
    {
        try
        {
            var campaign = await _repo.GetByIdAsync(id, ct);
            if (campaign is null)
                return BaseResponse<CampaignResponseDto>.Failure(
                    "Campaign not found.", statusCode: 404);

            if (campaign.CreatorId != creatorId)
                return BaseResponse<CampaignResponseDto>.Failure(
                    "You are not authorized to update this campaign.", statusCode: 403);

            if (campaign.Status == CampaignStatus.Completed)
                return BaseResponse<CampaignResponseDto>.Failure(
                    "A completed campaign cannot be edited.", statusCode: 400);

            if (dto.Title is not null) campaign.Title = dto.Title.Trim();
            if (dto.Story is not null) campaign.Story = dto.Story.Trim();
            if (dto.GoalAmount.HasValue) campaign.GoalAmount = dto.GoalAmount.Value;
            if (dto.SurgeryDate.HasValue) campaign.SurgeryDate = dto.SurgeryDate.Value;
            if (dto.BankName is not null) campaign.BankName = dto.BankName.Trim();
            if (dto.AccountNumber is not null) campaign.AccountNumber = dto.AccountNumber.Trim();
            if (dto.AccountName is not null) campaign.AccountName = dto.AccountName.Trim();

            campaign.UpdatedAt = DateTime.UtcNow;

            await _repo.UpdateAsync(campaign, ct);
            await _repo.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Campaign updated: {CampaignId} by {CreatorId}", id, creatorId);

            return BaseResponse<CampaignResponseDto>.Success(
                MapToDto(campaign), "Campaign updated successfully.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("UpdateCampaignAsync was cancelled.");
            return BaseResponse<CampaignResponseDto>.Failure(
                "Request was cancelled.", statusCode: 499);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                "Error updating campaign {CampaignId}: {Error}", id, ex.Message);
            return BaseResponse<CampaignResponseDto>.Failure(
                "An error occurred while updating the campaign.", statusCode: 500);
        }
    }

    public async Task<BaseResponse<string>> UploadCoverImageAsync(
        Guid campaignId, string creatorId,
        IFormFile file, CancellationToken ct = default)
    {
        try
        {
            var campaign = await _repo.GetByIdAsync(campaignId, ct);
            if (campaign is null)
                return BaseResponse<string>.Failure(
                    "Campaign not found.", statusCode: 404);

            if (campaign.CreatorId != creatorId)
                return BaseResponse<string>.Failure(
                    "You are not authorized to update this campaign.", statusCode: 403);

            var upload = await _cloudinary.UploadImageAsync(file, "campaign-covers");
            if (!upload.IsSuccess)
                return BaseResponse<string>.Failure(
                    upload.Error ?? "Image upload failed.", statusCode: 400);

            if (!string.IsNullOrEmpty(campaign.CoverImagePublicId))
                await _cloudinary.DeleteFileAsync(campaign.CoverImagePublicId);

            campaign.CoverImageUrl = upload.Url;
            campaign.CoverImagePublicId = upload.PublicId;
            campaign.UpdatedAt = DateTime.UtcNow;

            await _repo.UpdateAsync(campaign, ct);
            await _repo.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Cover image uploaded for campaign {CampaignId}: {Url}",
                campaignId, upload.Url);

            return BaseResponse<string>.Success(
                upload.Url!, "Cover image uploaded successfully.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("UploadCoverImageAsync was cancelled.");
            return BaseResponse<string>.Failure(
                "Request was cancelled.", statusCode: 499);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                "Error uploading cover image for {CampaignId}: {Error}",
                campaignId, ex.Message);
            return BaseResponse<string>.Failure(
                "An error occurred during upload.", statusCode: 500);
        }
    }

    public async Task<BaseResponse<string>> UploadMedicalDocumentAsync(
        Guid campaignId, string creatorId,
        IFormFile file, string fileType, CancellationToken ct = default)
    {
        try
        {
            var campaign = await _repo.GetByIdAsync(campaignId, ct);
            if (campaign is null)
                return BaseResponse<string>.Failure(
                    "Campaign not found.", statusCode: 404);

            if (campaign.CreatorId != creatorId)
                return BaseResponse<string>.Failure(
                    "You are not authorized to upload documents for this campaign.",
                    statusCode: 403);

            var upload = await _cloudinary.UploadDocumentAsync(file, "medical-documents");
            if (!upload.IsSuccess)
                return BaseResponse<string>.Failure(
                    upload.Error ?? "Document upload failed.", statusCode: 400);

            var document = new MedicalDocument
            {
                CampaignId = campaignId,
                FileUrl = upload.Url!,
                FileName = file.FileName,
                FileType = fileType,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _repo.AddDocumentAsync(document, ct);
            await _repo.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Medical document uploaded for campaign {CampaignId}: {FileType}",
                campaignId, fileType);

            return BaseResponse<string>.Success(
                upload.Url!, "Medical document uploaded successfully.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("UploadMedicalDocumentAsync was cancelled.");
            return BaseResponse<string>.Failure(
                "Request was cancelled.", statusCode: 499);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                "Error uploading document for {CampaignId}: {Error}",
                campaignId, ex.Message);
            return BaseResponse<string>.Failure(
                "An error occurred during upload.", statusCode: 500);
        }
    }

    public async Task<BaseResponse<string>> PostUpdateAsync(
        Guid campaignId, string creatorId,
        PostCampaignUpdateDto dto, CancellationToken ct = default)
    {
        try
        {
            var campaign = await _repo.GetByIdAsync(campaignId, ct);
            if (campaign is null)
                return BaseResponse<string>.Failure(
                    "Campaign not found.", statusCode: 404);

            if (campaign.CreatorId != creatorId)
                return BaseResponse<string>.Failure(
                    "You are not authorized to post updates for this campaign.",
                    statusCode: 403);

            var update = new MedicalUpdate
            {
                CampaignId = campaignId,
                Title = dto.Title.Trim(),
                Content = dto.Content.Trim(),
                ImageUrl = dto.ImageUrl,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _repo.AddUpdateAsync(update, ct);
            await _repo.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Update posted for campaign {CampaignId} by {CreatorId}",
                campaignId, creatorId);

            return BaseResponse<string>.Success(
                null!, "Update posted successfully.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("PostUpdateAsync was cancelled.");
            return BaseResponse<string>.Failure(
                "Request was cancelled.", statusCode: 499);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                "Error posting update for {CampaignId}: {Error}",
                campaignId, ex.Message);
            return BaseResponse<string>.Failure(
                "An error occurred while posting the update.", statusCode: 500);
        }
    }

    public async Task<BaseResponse<List<CampaignUpdateResponseDto>>> GetCampaignUpdatesAsync(
        Guid campaignId, CancellationToken ct = default)
    {
        try
        {
            var updates = await _repo.GetUpdatesAsync(campaignId, ct);
            var result = updates.Select(u => new CampaignUpdateResponseDto
            {
                Id = u.Id,
                Title = u.Title,
                Content = u.Content,
                ImageUrl = u.ImageUrl,
                PostedAt = u.CreatedAt
            }).ToList();

            return BaseResponse<List<CampaignUpdateResponseDto>>.Success(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("GetCampaignUpdatesAsync was cancelled.");
            return BaseResponse<List<CampaignUpdateResponseDto>>.Failure(
                "Request was cancelled.", statusCode: 499);
        }
    }

    public async Task<BaseResponse<string>> DeleteCampaignAsync(
        Guid id, string requesterId,
        string requesterRole, CancellationToken ct = default)
    {
        try
        {
            var campaign = await _repo.GetByIdAsync(id, ct);
            if (campaign is null)
                return BaseResponse<string>.Failure(
                    "Campaign not found.", statusCode: 404);

            var isAdmin = requesterRole is "SuperAdmin" or "VerificationAdmin";
            var isCreator = campaign.CreatorId == requesterId;

            if (!isAdmin && !isCreator)
                return BaseResponse<string>.Failure(
                    "You are not authorized to delete this campaign.", statusCode: 403);

            if (!string.IsNullOrEmpty(campaign.CoverImagePublicId))
                await _cloudinary.DeleteFileAsync(campaign.CoverImagePublicId);

            await _repo.DeleteAsync(campaign, ct);
            await _repo.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Campaign {CampaignId} deleted by {RequesterId} [{Role}]",
                id, requesterId, requesterRole);

            return BaseResponse<string>.Success(
                null!, "Campaign deleted successfully.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("DeleteCampaignAsync was cancelled.");
            return BaseResponse<string>.Failure(
                "Request was cancelled.", statusCode: 499);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                "Error deleting campaign {CampaignId}: {Error}", id, ex.Message);
            return BaseResponse<string>.Failure(
                "An error occurred while deleting the campaign.", statusCode: 500);
        }
    }

    private static CampaignResponseDto MapToDto(Campaign campaign) =>
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
            Status = campaign.Status,
            IsVerified = campaign.IsVerified,
            VerifiedAt = campaign.VerifiedAt,
            SurgeryDate = campaign.SurgeryDate,
            DonorCount = campaign.Donations?.Count ?? 0,
            CreatorName = campaign.Creator is not null
                ? $"{campaign.Creator.FirstName} {campaign.Creator.LastName}".Trim()
                : string.Empty,
            CreatorId = campaign.CreatorId,
            CreatedAt = campaign.CreatedAt
        };

   
    private static string ResolveBankCode(string bankName) => bankName.ToLower().Trim() switch
    {
        var b when b.Contains("opay") => "999992",
        var b when b.Contains("palmpay") => "999991",
        var b when b.Contains("kuda") => "50211",
        var b when b.Contains("rubies") => "125",
        var b when b.Contains("moniepoint") => "50515",
        var b when b.Contains("gtbank") || b.Contains("guaranty") => "058",
        var b when b.Contains("access") => "044",
        var b when b.Contains("zenith") => "057",
        var b when b.Contains("uba") => "033",
        var b when b.Contains("first bank") || b.Contains("firstbank") => "011",
        var b when b.Contains("union") => "032",
        var b when b.Contains("sterling") => "232",
        var b when b.Contains("wema") => "035",
        var b when b.Contains("fcmb") => "214",
        var b when b.Contains("stanbic") => "221",
        _ => "999992"
    };
}