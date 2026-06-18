using System.Net.Http;
using System.Net.Http.Headers;
using LifeLine.Application.Common.Response;
using LifeLine.Application.DTO.Donation;
using LifeLine.Application.Interfaces.IRepository;
using LifeLine.Application.Interfaces.IServices;
using LifeLine.Domain.Entities;
using LifeLine.Domain.Settings.Paystack;
using System.Text;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LifeLine.Persistence.Services;

public class DonationService : IDonationService
{
    private readonly IDonationRepository _donationRepo;
    private readonly ICampaignRepository _campaignRepo;
    private readonly IEmailService _emailService;
    private readonly PaystackSettings _paystack;
    private readonly HttpClient _httpClient;
    private readonly ILogger<DonationService> _logger;

    public DonationService(
        IDonationRepository donationRepo,
        ICampaignRepository campaignRepo,
        IEmailService emailService,
        IOptions<PaystackSettings> paystack,
        IHttpClientFactory httpClientFactory,
        ILogger<DonationService> logger)
    {
        _donationRepo = donationRepo;
        _campaignRepo = campaignRepo;
        _emailService = emailService;
        _paystack = paystack.Value;
        _logger = logger;

        _httpClient = httpClientFactory.CreateClient("Paystack");
        _httpClient.BaseAddress = new Uri(_paystack.BaseUrl);
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _paystack.SecretKey);
    }

    // ──────────────────────────────────────────────────
    // INITIATE DONATION
    // ──────────────────────────────────────────────────
    public async Task<BaseResponse<InitiateDonationResponseDto>> InitiateDonationAsync(
        InitiateDonationDto dto, string? donorId,
        string? donorName, string? donorEmail,
        CancellationToken ct = default)
    {
        try
        {
            var campaign = await _campaignRepo.GetByIdAsync(dto.CampaignId, ct);
            if (campaign is null)
                return BaseResponse<InitiateDonationResponseDto>.Failure(
                    "Campaign not found.", statusCode: 404);

            if (!campaign.IsVerified)
                return BaseResponse<InitiateDonationResponseDto>.Failure(
                    "This campaign is not yet verified and cannot accept donations.",
                    statusCode: 400);

            var resolvedEmail = donorEmail
                ?? dto.DonorEmail
                ?? "anonymous@lifeline.ng";

            var resolvedName = donorName
                ?? dto.DonorName
                ?? "Anonymous";

            // Use real email for Paystack processing — Paystack requires a
            // genuine-looking email. We still hide the donor's identity
            // from the public donation list when IsAnonymous is true.
            if (dto.IsAnonymous)
            {
                resolvedEmail = !string.IsNullOrEmpty(dto.DonorEmail)
                    ? dto.DonorEmail
                    : donorEmail ?? resolvedEmail;
                resolvedName = "Anonymous";
            }

            var reference = $"LL-{Guid.NewGuid().ToString()[..8].ToUpper()}-{DateTime.UtcNow:yyyyMMddHHmmss}";
            var amountInKobo = (int)(dto.Amount * 100);

            var paystackPayload = new
            {
                email = resolvedEmail,
                amount = amountInKobo,
                reference = reference,
                callback_url = _paystack.CallbackUrl,
                metadata = new
                {
                    campaign_id = dto.CampaignId.ToString(),
                    donor_name = resolvedName,
                    is_anonymous = dto.IsAnonymous,
                    message = dto.Message ?? ""
                }
            };

            _logger.LogInformation(
                "Sending to Paystack — Email: {Email}, Amount(kobo): {Amount}, Reference: {Reference}, CallbackUrl: {CallbackUrl}",
                resolvedEmail, amountInKobo, reference, _paystack.CallbackUrl);

            var json = JsonConvert.SerializeObject(paystackPayload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var httpResponse = await _httpClient.PostAsync(
                "/transaction/initialize", content, ct);

            var responseBody = await httpResponse.Content.ReadAsStringAsync(ct);
            dynamic? result = JsonConvert.DeserializeObject(responseBody);

            if (result is null || result.status == false)
            {
                string paystackMessage = "Unknown error from Paystack";
                try
                {
                    paystackMessage = result?.message?.ToString() ?? paystackMessage;
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        "Error initiating donation. Exception type: {Type}. Message: {Error}. StackTrace: {Stack}",
                        ex.GetType().Name, ex.Message, ex.StackTrace);
                    return BaseResponse<InitiateDonationResponseDto>.Failure(
                        "An error occurred while initiating payment.", statusCode: 500);
                }

                _logger.LogError(
                    "Paystack initiation failed. HttpStatus: {HttpStatus}. Reference: {Reference}. Paystack message: {Message}. Full response: {Response}",
                    httpResponse.StatusCode, reference, paystackMessage, responseBody);

                return BaseResponse<InitiateDonationResponseDto>.Failure(
                    $"Payment initiation failed: {paystackMessage}", statusCode: 400);
            }

            var donation = new Donation
            {
                CampaignId = dto.CampaignId,
                Amount = dto.Amount,
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

            _logger.LogInformation(
                "Donation initiated: {Reference} for campaign {CampaignId} — ₦{Amount}",
                reference, dto.CampaignId, dto.Amount);

            return BaseResponse<InitiateDonationResponseDto>.Success(
                new InitiateDonationResponseDto
                {
                    PaymentReference = reference,
                    PaymentUrl = (string)result.data.authorization_url
                },
                "Payment initiated. Redirect the user to the payment URL.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("InitiateDonationAsync was cancelled.");
            return BaseResponse<InitiateDonationResponseDto>.Failure(
                "Request was cancelled.", statusCode: 499);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                "Error initiating donation: {Error}", ex.Message);
            return BaseResponse<InitiateDonationResponseDto>.Failure(
                "An error occurred while initiating payment.", statusCode: 500);
        }
    }

    // ──────────────────────────────────────────────────
    // VERIFY DONATION
    // ──────────────────────────────────────────────────
    public async Task<BaseResponse<string>> VerifyDonationAsync(
        string reference, CancellationToken ct = default)
    {
        try
        {
            var donation = await _donationRepo.GetByReferenceAsync(reference, ct);
            if (donation is null)
                return BaseResponse<string>.Failure(
                    "Donation record not found.", statusCode: 404);

            if (donation.IsVerified)
            {
                _logger.LogInformation(
                    "Duplicate verification attempt for {Reference} — already processed.",
                    reference);
                return BaseResponse<string>.Success(
                    null!, "Donation already verified.");
            }

            var httpResponse = await _httpClient.GetAsync(
                $"/transaction/verify/{reference}", ct);

            var responseBody = await httpResponse.Content.ReadAsStringAsync(ct);
            dynamic? result = JsonConvert.DeserializeObject(responseBody);

            if (result is null || result.status == false)
            {
                string paystackMessage = result?.message?.ToString() ?? "Unknown error from Paystack";

                _logger.LogWarning(
                    "Paystack verification failed for {Reference}. Message: {Message}. Response: {Response}",
                    reference, paystackMessage, responseBody);

                return BaseResponse<string>.Failure(
                    $"Payment verification failed: {paystackMessage}", statusCode: 400);
            }

            var paystackStatus = (string)result.data.status;
            if (paystackStatus != "success")
            {
                _logger.LogWarning(
                    "Payment not successful for {Reference}. Status: {Status}",
                    reference, paystackStatus);
                return BaseResponse<string>.Failure(
                    $"Payment was not successful. Status: {paystackStatus}",
                    statusCode: 400);
            }

            var paidAmount = (decimal)result.data.amount / 100;
            var expectedAmount = donation.Amount;

            if (paidAmount < expectedAmount)
            {
                _logger.LogWarning(
                    "Amount mismatch for {Reference}. Expected: ₦{Expected}, Paid: ₦{Paid}",
                    reference, expectedAmount, paidAmount);
                return BaseResponse<string>.Failure(
                    "Payment amount mismatch detected.", statusCode: 400);
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
                            donation.DonorEmail!,
                            donation.DonorName ?? "Donor",
                            campaign?.Title ?? "the campaign",
                            donation.Amount);
                    }
                    catch (Exception emailEx)
                    {
                        _logger.LogError(
                            "Failed to send donation email for {Reference}: {Error}",
                            reference, emailEx.Message);
                    }
                });
            }

            _logger.LogInformation(
                "Donation verified: {Reference} — ₦{Amount} for campaign {CampaignId}",
                reference, donation.Amount, donation.CampaignId);

            return BaseResponse<string>.Success(
                null!, "Donation verified successfully. Thank you!");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("VerifyDonationAsync was cancelled.");
            return BaseResponse<string>.Failure(
                "Request was cancelled.", statusCode: 499);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                "Error verifying donation {Reference}: {Error}",
                reference, ex.Message);
            return BaseResponse<string>.Failure(
                "An error occurred during verification.", statusCode: 500);
        }
    }

    // ──────────────────────────────────────────────────
    // GET CAMPAIGN DONATIONS
    // ──────────────────────────────────────────────────
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