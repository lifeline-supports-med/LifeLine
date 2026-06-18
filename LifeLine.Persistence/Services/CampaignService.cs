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
    private readonly IConfiguration _config;
    private readonly ILogger<CampaignService> _logger;

    public CampaignService(
        ICampaignRepository repo,
        ICloudinaryService cloudinary,
        IEmailService emailService,
        IConfiguration config,
        ILogger<CampaignService> logger)
    {
        _repo = repo;
        _cloudinary = cloudinary;
        _emailService = emailService;
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
                AccountNumber = dto.AccountNumber.Trim(),
                AccountName = dto.AccountName.Trim(),
                Slug = slug,
                Status = CampaignStatus.Pending,
                IsVerified = false,
                CreatorId = creatorId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _repo.AddAsync(campaign, ct);
            await _repo.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Campaign created: {CampaignId} by {CreatorId}",
                campaign.Id, creatorId);

            // ── Notify creator ──────────────────────────
            await _emailService.SendCampaignSubmittedToCreatorAsync(
                creatorEmail, creatorName, campaign.Title);

            // ── Notify admin ────────────────────────────
            var adminEmail = _config["AdminSettings:NotificationEmail"]!;
            await _emailService.SendNewCampaignAlertToAdminAsync(
                adminEmail, creatorName, campaign.Title, campaign.Id);

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

    // ──────────────────────────────────────────────────
    // UPLOAD COVER IMAGE
    // ──────────────────────────────────────────────────
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

            // Delete old image from Cloudinary if exists
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

    // ──────────────────────────────────────────────────
    // UPLOAD MEDICAL DOCUMENT
    // ──────────────────────────────────────────────────
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

    // ──────────────────────────────────────────────────
    // POST CAMPAIGN UPDATE
    // ──────────────────────────────────────────────────
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

    // ──────────────────────────────────────────────────
    // GET CAMPAIGN UPDATES
    // ──────────────────────────────────────────────────
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

    // ──────────────────────────────────────────────────
    // DELETE CAMPAIGN
    // ──────────────────────────────────────────────────
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

            // Delete cover image from Cloudinary
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
}