using LifeLine.Application.Common.RequestModel.PaystackDTO;
using LifeLine.Application.Common.Response;
using LifeLine.Application.DTO.Donation;
using LifeLine.Application.Interfaces.IRepository;
using LifeLine.Application.Interfaces.IServices;
using LifeLine.Domain.Entities;
using LifeLine.Domain.Settings.Paystack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LifeLine.Persistence.Services;

public class DonationService : IDonationService
{
    private readonly IDonationRepository _donationRepo;
    private readonly ICampaignRepository _campaignRepo;
    private readonly IEmailService _emailService;
    private readonly IPaystackService _paystackService;
    private readonly PaystackSettings _paystack;
    private readonly ILogger<DonationService> _logger;

    public DonationService(
        IDonationRepository donationRepo,
        ICampaignRepository campaignRepo,
        IEmailService emailService,
        IPaystackService paystackService,
        IOptions<PaystackSettings> paystack,
        ILogger<DonationService> logger)
    {
        _donationRepo = donationRepo;
        _campaignRepo = campaignRepo;
        _emailService = emailService;
        _paystackService = paystackService;
        _paystack = paystack.Value;
        _logger = logger;
    }

    public async Task<BaseResponse<InitiateDonationResponseDto>> InitiateDonationAsync(
        InitiateDonationDto dto, string? donorId,
        string? donorName, string? donorEmail,
        CancellationToken ct = default)
    {
        try
        {
            var campaign = await _campaignRepo.GetByIdAsync(dto.CampaignId, ct);
            if (campaign is null)
                return BaseResponse<InitiateDonationResponseDto>.Failure("Campaign not found.", statusCode: 404);

            if (!campaign.IsVerified)
                return BaseResponse<InitiateDonationResponseDto>.Failure(
                    "This campaign is not yet verified and cannot accept donations.", statusCode: 400);

            var resolvedEmail = dto.IsAnonymous
                ? (dto.DonorEmail ?? donorEmail ?? "donor@lifeline.ng")
                : (donorEmail ?? dto.DonorEmail ?? "donor@lifeline.ng");
            var resolvedName = dto.IsAnonymous ? "Anonymous" : (donorName ?? dto.DonorName ?? "Anonymous");

            var totalAmount = dto.Amount;
            var platformFee = _paystack.PlatformFeeAmount;
            var campaignAmount = totalAmount - platformFee;

            if (campaignAmount <= 0)
                return BaseResponse<InitiateDonationResponseDto>.Failure(
                    $"Donation amount must be greater than ₦{platformFee} (platform fee).", statusCode: 400);

            var totalAmountKobo = (long)(totalAmount * 100);
            var platformFeeKobo = (long)(platformFee * 100);
            var campaignAmountKobo = (long)(campaignAmount * 100);

            var reference = $"LL-{Guid.NewGuid().ToString()[..8].ToUpper()}-{DateTime.UtcNow:yyyyMMddHHmmss}";

            var platformSubaccount = await _paystackService.EnsureSubaccountAsync(
                _paystack.PlatformFeeAccountName,
                _paystack.PlatformFeeAccountNumber,
                _paystack.PlatformFeeBankCode,
                $"SUB-PLATFORM-{_paystack.PlatformFeeAccountNumber}",
                ct);

            if (platformSubaccount is null)
            {
                _logger.LogError("Failed to create/get platform subaccount for campaign {CampaignId}.", dto.CampaignId);
                return BaseResponse<InitiateDonationResponseDto>.Failure(
                    "Payment setup failed. Please try again.", statusCode: 500);
            }

            // Campaign subaccount is created once, at verification time,
            // by CampaignService.ActivatePaymentsAsync. Just read it here —
            // no Paystack call needed on every donation.
            if (string.IsNullOrEmpty(campaign.SubAccountCode))
            {
                _logger.LogError(
                    "Campaign {CampaignId} is verified but has no SubAccountCode. Donations will go entirely to platform.",
                    dto.CampaignId);
            }
            string? campaignSubaccount = campaign.SubAccountCode;

            var splits = new List<PaystackSplitEntryDto>
            {
                new() { SubaccountCode = platformSubaccount, ShareKobo = platformFeeKobo }
            };
            if (campaignSubaccount is not null)
                splits.Add(new() { SubaccountCode = campaignSubaccount, ShareKobo = campaignAmountKobo });

            var initResult = await _paystackService.InitializeTransactionAsync(
                new PaystackInitializeRequestDto
                {
                    Email = resolvedEmail,
                    AmountKobo = totalAmountKobo,
                    Reference = reference,
                    CallbackUrl = _paystack.CallbackUrl,
                    Splits = splits,
                    Metadata = new Dictionary<string, object>
                    {
                        ["campaign_id"] = dto.CampaignId.ToString(),
                        ["campaign_title"] = campaign.Title,
                        ["donor_name"] = resolvedName,
                        ["is_anonymous"] = dto.IsAnonymous,
                        ["message"] = dto.Message ?? "",
                        ["platform_fee"] = platformFee
                    }
                },
                idempotencyKey: $"INIT-{reference}",
                ct: ct);

            if (!initResult.IsSuccess || initResult.Data is null)
                return BaseResponse<InitiateDonationResponseDto>.Failure(
                    initResult.Message ?? "Payment initiation failed.", statusCode: initResult.StatusCode ?? 400);

            var donation = new Donation
            {
                CampaignId = dto.CampaignId,
                Amount = totalAmount,
                PaymentReference = reference,
                IsAnonymous = dto.IsAnonymous,
                IsVerified = false,
                DonorId = donorId,
                DonorName = dto.IsAnonymous ? "Anonymous" : resolvedName,
                DonorEmail = dto.IsAnonymous ? null : resolvedEmail,
                Message = dto.Message,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _donationRepo.AddAsync(donation, ct);
            await _donationRepo.SaveChangesAsync(ct);

            return BaseResponse<InitiateDonationResponseDto>.Success(
                new InitiateDonationResponseDto
                {
                    PaymentReference = reference,
                    PaymentUrl = initResult.Data.AuthorizationUrl
                },
                "Payment initiated. Redirect the user to the payment URL.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("InitiateDonationAsync was cancelled.");
            return BaseResponse<InitiateDonationResponseDto>.Failure("Request was cancelled.", statusCode: 499);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error initiating donation: {Msg}", ex.Message);
            return BaseResponse<InitiateDonationResponseDto>.Failure(
                "An error occurred while initiating payment.", statusCode: 500);
        }
    }

    public async Task<BaseResponse<string>> VerifyDonationAsync(string reference, CancellationToken ct = default)
    {
        try
        {
            var donation = await _donationRepo.GetByReferenceAsync(reference, ct);
            if (donation is null)
                return BaseResponse<string>.Failure("Donation record not found.", statusCode: 404);

            if (donation.IsVerified)
                return BaseResponse<string>.Success(null!, "Donation already verified.");

            var verifyResult = await _paystackService.VerifyTransactionAsync(reference, ct);
            if (!verifyResult.IsSuccess || verifyResult.Data is null)
                return BaseResponse<string>.Failure(
                    verifyResult.Message ?? "Payment verification failed.", statusCode: verifyResult.StatusCode ?? 400);

            if (verifyResult.Data.Status != "success")
                return BaseResponse<string>.Failure(
                    $"Payment was not successful. Status: {verifyResult.Data.Status}", statusCode: 400);

            var paidAmount = verifyResult.Data.AmountKobo / 100m;
            if (paidAmount < donation.Amount)
            {
                _logger.LogWarning("Amount mismatch for {Ref}. Expected ₦{Expected}, Paid ₦{Paid}",
                    reference, donation.Amount, paidAmount);
                return BaseResponse<string>.Failure("Payment amount mismatch detected.", statusCode: 400);
            }

            donation.IsVerified = true;
            donation.UpdatedAt = DateTime.UtcNow;
            await _donationRepo.UpdateAsync(donation, ct);
            await _donationRepo.SaveChangesAsync(ct);

            var campaign = await _campaignRepo.GetByIdAsync(donation.CampaignId, ct);
            if (campaign is not null)
            {
                campaign.AmountRaised += donation.Amount;
                campaign.UpdatedAt = DateTime.UtcNow;
                await _campaignRepo.UpdateAsync(campaign, ct);
                await _campaignRepo.SaveChangesAsync(ct);
            }

            if (!donation.IsAnonymous && !string.IsNullOrEmpty(donation.DonorEmail))
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _emailService.SendDonationConfirmationEmailAsync(
                            donation.DonorEmail!, donation.DonorName ?? "Donor",
                            campaign?.Title ?? "the campaign", donation.Amount);
                    }
                    catch (Exception emailEx)
                    {
                        _logger.LogError("Failed to send donation email for {Ref}: {Error}", reference, emailEx.Message);
                    }
                });
            }

            return BaseResponse<string>.Success(null!, "Donation verified successfully. Thank you!");
        }
        catch (OperationCanceledException)
        {
            return BaseResponse<string>.Failure("Request was cancelled.", statusCode: 499);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error verifying donation {Ref}: {Error}", reference, ex.Message);
            return BaseResponse<string>.Failure("An error occurred during verification.", statusCode: 500);
        }
    }

    public async Task<BaseResponse<List<DonationResponseDto>>> GetCampaignDonationsAsync(
    Guid campaignId, CancellationToken ct = default)
    {
        try
        {
            var donations = await _donationRepo.GetByCampaignIdAsync(campaignId, ct);

            var result = donations.Select(d => new DonationResponseDto
            {
                Id = d.Id,
                Amount = d.Amount,
                DonorName = d.IsAnonymous ? "Anonymous" : (d.DonorName ?? "Anonymous"),
                Message = d.Message,
                IsAnonymous = d.IsAnonymous,
                IsVerified = d.IsVerified,
                DonatedAt = d.CreatedAt
            }).ToList();

            return BaseResponse<List<DonationResponseDto>>.Success(
                result, $"Retrieved {result.Count} donations.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("GetCampaignDonationsAsync was cancelled.");
            return BaseResponse<List<DonationResponseDto>>.Failure(
                "Request was cancelled.", statusCode: 499);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                "Error fetching donations for {CampaignId}: {Error}",
                campaignId, ex.Message);
            return BaseResponse<List<DonationResponseDto>>.Failure(
                "An error occurred.", statusCode: 500);
        }
    }
}