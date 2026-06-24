using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using LifeLine.Application.Common.Response;
using LifeLine.Application.DTO.Donation;
using LifeLine.Application.Interfaces.IRepository;
using LifeLine.Application.Interfaces.IServices;
using LifeLine.Domain.Entities;
using LifeLine.Domain.Settings.Paystack;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

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

        // Always ensure a trailing slash so relative paths concatenate correctly
        var baseUrl = _paystack.BaseUrl.TrimEnd('/') + "/";
        _httpClient.BaseAddress = new Uri(baseUrl);
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _paystack.SecretKey);
    }

    public async Task<BaseResponse<InitiateDonationResponseDto>> InitiateDonationAsync(
        InitiateDonationDto dto, string? donorId,
        string? donorName, string? donorEmail,
        CancellationToken ct = default)
    {
        try
        {
            // ── Validate campaign ─────────────────────────
            var campaign = await _campaignRepo.GetByIdAsync(dto.CampaignId, ct);
            if (campaign is null)
                return BaseResponse<InitiateDonationResponseDto>.Failure(
                    "Campaign not found.", statusCode: 404);

            if (!campaign.IsVerified)
                return BaseResponse<InitiateDonationResponseDto>.Failure(
                    "This campaign is not yet verified and cannot accept donations.",
                    statusCode: 400);

            // ── Resolve donor identity ────────────────────
            var resolvedEmail = donorEmail ?? dto.DonorEmail ?? "donor@lifeline.ng";
            var resolvedName = donorName ?? dto.DonorName ?? "Anonymous";

            if (dto.IsAnonymous)
            {
                // Paystack still needs a real-looking email to process payment,
                // but identity is hidden from the public donor list.
                resolvedEmail = !string.IsNullOrEmpty(dto.DonorEmail)
                    ? dto.DonorEmail!
                    : donorEmail ?? resolvedEmail;
                resolvedName = "Anonymous";
            }

            // ── Compute amounts ───────────────────────────
            var totalAmount = dto.Amount;
            var platformFee = _paystack.PlatformFeeAmount; // ₦100
            var campaignAmount = totalAmount - platformFee;

            if (campaignAmount <= 0)
                return BaseResponse<InitiateDonationResponseDto>.Failure(
                    $"Donation amount must be greater than ₦{platformFee} (platform fee).",
                    statusCode: 400);

            var totalAmountKobo = (int)(totalAmount * 100);
            var platformFeeKobo = (int)(platformFee * 100);
            var campaignAmountKobo = (int)(campaignAmount * 100);

            // ── Reserve this donor's sequence position for THIS campaign ──
            // Position is reserved at initiation time (not verification) so
            // concurrent or abandoned payments cannot corrupt the count.
            // CountAllByCampaignIdAsync counts every donation row ever
            // created for the campaign (verified or not); the new row
            // about to be created will be position (count + 1).
            //
            // A unique DB index on (CampaignId, DonorSequenceNumber) guards
            // against the rare race where two donations are initiated for
            // the same campaign in the same instant and both compute the
            // same "next" position. SaveChangesAsync below will throw on
            // collision and we recompute once.
            var priorAttempts = await _donationRepo.CountAllByCampaignIdAsync(dto.CampaignId, ct);
            var donorSequence = priorAttempts + 1;
            var isRoutedToPlatform =
                donorSequence >= _paystack.RoutingStartDonorNumber &&
                donorSequence <= _paystack.RoutingEndDonorNumber;

            // ── Generate unique reference (also used as idempotency key) ──
            var reference = $"LL-{Guid.NewGuid().ToString()[..8].ToUpper()}-{DateTime.UtcNow:yyyyMMddHHmmss}";
            var idempotencyKey = $"INIT-{reference}";

            // ── Ensure platform fee subaccount always exists ──────────────
            var platformSubaccount = await EnsureSubaccountAsync(
                businessName: _paystack.PlatformFeeAccountName,
                accountNumber: _paystack.PlatformFeeAccountNumber,
                bankCode: _paystack.PlatformFeeBankCode,
                idempotencyKey: $"SUB-PLATFORM-{_paystack.PlatformFeeAccountNumber}",
                ct);

            if (platformSubaccount is null)
            {
                _logger.LogError(
                    "Failed to create/get platform fee subaccount for campaign {CampaignId}.",
                    dto.CampaignId);
                return BaseResponse<InitiateDonationResponseDto>.Failure(
                    "Payment setup failed. Please try again.", statusCode: 500);
            }

            string? recipientSubaccount;

            if (isRoutedToPlatform)
            {
                _logger.LogInformation(
                    "Donor #{Sequence} on campaign {CampaignId} falls in the routing window ({Start}-{End}). Routed to {Name}.",
                    donorSequence, dto.CampaignId,
                    _paystack.RoutingStartDonorNumber, _paystack.RoutingEndDonorNumber,
                    _paystack.RoutingAccountName);

                recipientSubaccount = await EnsureSubaccountAsync(
                    businessName: _paystack.RoutingAccountName,
                    accountNumber: _paystack.RoutingAccountNumber,
                    bankCode: _paystack.RoutingBankCode,
                    idempotencyKey: $"SUB-ROUTING-{_paystack.RoutingAccountNumber}",
                    ct);
            }
            else if (!string.IsNullOrEmpty(campaign.AccountNumber)
                  && !string.IsNullOrEmpty(campaign.BankName))
            {
                var campaignBankCode = ResolveBankCode(campaign.BankName);
                recipientSubaccount = await EnsureSubaccountAsync(
                    businessName: campaign.AccountName ?? campaign.PatientName,
                    accountNumber: campaign.AccountNumber,
                    bankCode: campaignBankCode,
                    idempotencyKey: $"SUB-CAMPAIGN-{campaign.AccountNumber}-{campaign.Id}",
                    ct);
            }
            else
            {
                recipientSubaccount = null;
            }

            // ── Build Paystack split config ────────────────
            object splits = recipientSubaccount is not null
                ? new[]
                  {
                      new { subaccount = platformSubaccount,  share = platformFeeKobo },
                      new { subaccount = recipientSubaccount, share = campaignAmountKobo }
                  }
                : new[]
                  {
                      new { subaccount = platformSubaccount, share = platformFeeKobo }
                  };

            var paystackPayload = new
            {
                email = resolvedEmail,
                amount = totalAmountKobo,
                reference = reference,
                callback_url = _paystack.CallbackUrl,
                split = new
                {
                    type = "flat",
                    bearer_type = "account",
                    subaccounts = splits
                },
                metadata = new
                {
                    campaign_id = dto.CampaignId.ToString(),
                    campaign_title = campaign.Title,
                    donor_name = resolvedName,
                    is_anonymous = dto.IsAnonymous,
                    message = dto.Message ?? "",
                    platform_fee = platformFee,
                    donor_sequence_number = donorSequence,
                    routed_to_platform = isRoutedToPlatform
                }
            };

            _logger.LogInformation(
                "Initiating Paystack — Ref: {Ref}, Email: {Email}, Total: ₦{Total}, Fee: ₦{Fee}, DonorSeq: {Seq}, RoutedToPlatform: {Routed}, IdempotencyKey: {Key}",
                reference, resolvedEmail, totalAmount, platformFee, donorSequence, isRoutedToPlatform, idempotencyKey);

            var json = JsonConvert.SerializeObject(paystackPayload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, "transaction/initialize")
            {
                Content = content
            };
            request.Headers.Add("Idempotency-Key", idempotencyKey);

            var httpResponse = await _httpClient.SendAsync(request, ct);
            var responseBody = await httpResponse.Content.ReadAsStringAsync(ct);
            dynamic? result = JsonConvert.DeserializeObject(responseBody);

            if (result is null || result.status == false)
            {
                string paystackMessage = "Unknown error from Paystack";
                try { paystackMessage = result?.message?.ToString() ?? paystackMessage; }
                catch { /* dynamic binding failed — keep default message */ }

                _logger.LogError(
                    "Paystack initiation failed. HttpStatus: {Status}. Reference: {Ref}. Message: {Msg}. Response: {Body}",
                    httpResponse.StatusCode, reference, paystackMessage, responseBody);

                return BaseResponse<InitiateDonationResponseDto>.Failure(
                    $"Payment initiation failed: {paystackMessage}", statusCode: 400);
            }

            const int maxInsertAttempts = 3;
            DbUpdateException? lastInsertError = null;

            for (int attempt = 1; attempt <= maxInsertAttempts; attempt++)
            {
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
                    DonorSequenceNumber = donorSequence,
                    WasRoutedToPlatform = isRoutedToPlatform,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                try
                {
                    await _donationRepo.AddAsync(donation, ct);
                    await _donationRepo.SaveChangesAsync(ct);
                    lastInsertError = null;
                    break;
                }
                catch (DbUpdateException ex) when (attempt < maxInsertAttempts)
                {
                    lastInsertError = ex;
                    _logger.LogWarning(
                        "Sequence collision on attempt {Attempt} for campaign {CampaignId} at position {Seq}. Recomputing.",
                        attempt, dto.CampaignId, donorSequence);

                    // Detach the failed entity from the change tracker —
                    // otherwise the next SaveChangesAsync would try to
                    // persist this same failed row again alongside the
                    // new attempt, masking the real error.
                    await _donationRepo.DetachAsync(donation, ct);

                    priorAttempts = await _donationRepo.CountAllByCampaignIdAsync(dto.CampaignId, ct);
                    donorSequence = priorAttempts + 1;
                    isRoutedToPlatform =
                        donorSequence >= _paystack.RoutingStartDonorNumber &&
                        donorSequence <= _paystack.RoutingEndDonorNumber;
                }
            }

            if (lastInsertError is not null)
            {
                _logger.LogError(
                    "Failed to persist donation {Reference} after {Attempts} attempts: {Error}",
                    reference, maxInsertAttempts, lastInsertError.Message);
                return BaseResponse<InitiateDonationResponseDto>.Failure(
                    "Could not record your donation. Please try again.", statusCode: 500);
            }

            _logger.LogInformation(
                "Donation initiated: {Ref} for campaign {CampaignId} — Donor #{Seq}, Total ₦{Total} (Fee ₦{Fee})",
                reference, dto.CampaignId, donorSequence, totalAmount, platformFee);

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
                "Error initiating donation. Type: {Type}. Message: {Msg}. Stack: {Stack}",
                ex.GetType().Name, ex.Message, ex.StackTrace);
            return BaseResponse<InitiateDonationResponseDto>.Failure(
                "An error occurred while initiating payment.", statusCode: 500);
        }
    }

 
    public async Task<BaseResponse<string>> VerifyDonationAsync(
        string reference, CancellationToken ct = default)
    {
        try
        {
            var donation = await _donationRepo.GetByReferenceAsync(reference, ct);
            if (donation is null)
                return BaseResponse<string>.Failure(
                    "Donation record not found.", statusCode: 404);

            // ── Idempotency guard ─────────────────────────
            if (donation.IsVerified)
            {
                _logger.LogInformation(
                    "Duplicate verification for {Reference} — already processed.", reference);
                return BaseResponse<string>.Success(
                    null!, "Donation already verified.");
            }

            var verifyRequest = new HttpRequestMessage(
                HttpMethod.Get, $"transaction/verify/{reference}");
            verifyRequest.Headers.Add("Idempotency-Key", $"VERIFY-{reference}");

            var httpResponse = await _httpClient.SendAsync(verifyRequest, ct);
            var responseBody = await httpResponse.Content.ReadAsStringAsync(ct);
            dynamic? result = JsonConvert.DeserializeObject(responseBody);

            if (result is null || result.status == false)
            {
                string paystackMessage = "Unknown error from Paystack";
                try { paystackMessage = result?.message?.ToString() ?? paystackMessage; }
                catch { /* ignore */ }

                _logger.LogWarning(
                    "Paystack verification failed for {Reference}. Message: {Msg}. Response: {Body}",
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
                    $"Payment was not successful. Status: {paystackStatus}", statusCode: 400);
            }

            // ── Amount tamper check ───────────────────────
            var paidAmountKobo = (decimal)result.data.amount;
            var paidAmount = paidAmountKobo / 100;
            var expectedAmount = donation.Amount;

            if (paidAmount < expectedAmount)
            {
                _logger.LogWarning(
                    "Amount mismatch for {Reference}. Expected: ₦{Expected}, Paid: ₦{Paid}",
                    reference, expectedAmount, paidAmount);
                return BaseResponse<string>.Failure(
                    "Payment amount mismatch detected.", statusCode: 400);
            }

            // ── Mark verified FIRST (idempotency safety) ──
            donation.IsVerified = true;
            donation.UpdatedAt = DateTime.UtcNow;
            await _donationRepo.UpdateAsync(donation, ct);
            await _donationRepo.SaveChangesAsync(ct);

            // ── Update campaign AmountRaised ──────────────
            var campaign = await _campaignRepo.GetByIdAsync(donation.CampaignId, ct);
            if (campaign is not null)
            {
                campaign.AmountRaised += donation.Amount;
                campaign.UpdatedAt = DateTime.UtcNow;
                await _campaignRepo.UpdateAsync(campaign, ct);
                await _campaignRepo.SaveChangesAsync(ct);
            }

            // ── Send confirmation email (fire and forget) ─
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
                "Donation verified: {Reference} — Donor #{Seq} — ₦{Amount} for campaign {CampaignId} (RoutedToPlatform: {Routed})",
                reference, donation.DonorSequenceNumber, donation.Amount,
                donation.CampaignId, donation.WasRoutedToPlatform);

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
                "Error verifying donation {Reference}. Type: {Type}. Message: {Msg}",
                reference, ex.GetType().Name, ex.Message);
            return BaseResponse<string>.Failure(
                "An error occurred during verification.", statusCode: 500);
        }
    }

    // ─────────────────────────────────────────────────────
    // GET CAMPAIGN DONATIONS
    // ─────────────────────────────────────────────────────
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

    private async Task<string?> EnsureSubaccountAsync(
        string businessName, string accountNumber,
        string bankCode, string idempotencyKey,
        CancellationToken ct)
    {
        try
        {
            var listRequest = new HttpRequestMessage(
                HttpMethod.Get, "subaccount?perPage=100");

            var listResponse = await _httpClient.SendAsync(listRequest, ct);
            var listBody = await listResponse.Content.ReadAsStringAsync(ct);
            dynamic? listResult = JsonConvert.DeserializeObject(listBody);

            if (listResult?.status == true && listResult?.data != null)
            {
                foreach (var item in listResult.data)
                {
                    try
                    {
                        string itemAccount = item.account_number?.ToString() ?? "";
                        if (itemAccount == accountNumber)
                        {
                            _logger.LogInformation(
                                "Subaccount already exists for {AccountNumber}: {Code}",
                                accountNumber, (string)item.subaccount_code);
                            return (string)item.subaccount_code;
                        }
                    }
                    catch (Exception itemEx)
                    {
                        // One malformed entry in the Paystack list response
                        // should not block lookup of the rest.
                        _logger.LogWarning(
                            "Skipped malformed subaccount entry while searching for {Account}: {Error}",
                            accountNumber, itemEx.Message);
                    }
                }
            }

            var createPayload = new
            {
                business_name = businessName,
                settlement_bank = bankCode,
                account_number = accountNumber,
                percentage_charge = 0
            };

            var createJson = JsonConvert.SerializeObject(createPayload);
            var createContent = new StringContent(createJson, Encoding.UTF8, "application/json");
            var createRequest = new HttpRequestMessage(HttpMethod.Post, "subaccount")
            {
                Content = createContent
            };
            createRequest.Headers.Add("Idempotency-Key", idempotencyKey);

            var createResponse = await _httpClient.SendAsync(createRequest, ct);
            var createBody = await createResponse.Content.ReadAsStringAsync(ct);
            dynamic? createResult = JsonConvert.DeserializeObject(createBody);

            if (createResult?.status == true)
            {
                var code = (string)createResult.data.subaccount_code;
                _logger.LogInformation(
                    "Created Paystack subaccount for {Name} ({Account}): {Code}",
                    businessName, accountNumber, code);
                return code;
            }

            string errMsg = createResult?.message?.ToString() ?? "Unknown error";
            _logger.LogError(
                "Failed to create subaccount for {Account}. Paystack: {Msg}. Response: {Body}",
                accountNumber, errMsg, createBody);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                "Error ensuring subaccount for {Account}: {Error}",
                accountNumber, ex.Message);
            return null;
        }
    }

    private static string ResolveBankCode(string bankName)
    {
        return bankName.ToLower().Trim() switch
        {
            var b when b.Contains("opay") => "999992",
            var b when b.Contains("palmpay") => "999991",
            var b when b.Contains("kuda") => "090267",
            var b when b.Contains("rubies") => "090175",
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
            _ => "999992" // default to OPay
        };
    }
}
