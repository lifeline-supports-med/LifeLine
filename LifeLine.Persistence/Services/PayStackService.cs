using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using LifeLine.Application.Common.RequestModel.PaystackDTO;
using LifeLine.Application.Common.Response;
using LifeLine.Application.Interfaces.IServices;
using LifeLine.Domain.Settings.Paystack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LifeLine.Persistence.Services;

public class PaystackService : IPaystackService
{
    private readonly HttpClient _httpClient;
    private readonly PaystackSettings _settings;
    private readonly ILogger<PaystackService> _logger;

    public PaystackService(
        IHttpClientFactory httpClientFactory,
        IOptions<PaystackSettings> settings,
        ILogger<PaystackService> logger)
    {
        _settings = settings.Value;
        _logger = logger;

        _httpClient = httpClientFactory.CreateClient("Paystack");
        //var baseUrl = _settings.BaseUrl.TrimEnd('/') + "/";
        //_httpClient.BaseAddress = new Uri(baseUrl);
        //_httpClient.DefaultRequestHeaders.Authorization =
        //    new AuthenticationHeaderValue("Bearer", _settings.SecretKey);
    }

    public async Task<BaseResponse<PaystackInitializeResultDto>> InitializeTransactionAsync(
        PaystackInitializeRequestDto request,
        string idempotencyKey,
        CancellationToken ct = default)
    {
        try
        {
            var payload = new
            {
                email = request.Email,
                amount = request.AmountKobo,
                reference = request.Reference,
                callback_url = request.CallbackUrl,
                split = new
                {
                    type = "flat",
                    bearer_type = "account",
                    subaccounts = request.Splits.Select(s => new
                    {
                        subaccount = s.SubaccountCode,
                        share = s.ShareKobo
                    })
                },
                metadata = request.Metadata
            };

            var json = JsonConvert.SerializeObject(payload);
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, "transaction/initialize")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            httpRequest.Headers.Add("Idempotency-Key", idempotencyKey);

            var httpResponse = await _httpClient.SendAsync(httpRequest, ct);
            var body = await httpResponse.Content.ReadAsStringAsync(ct);
            var parsed = JObject.Parse(body);

            var status = parsed.Value<bool?>("status") ?? false;
            if (!status)
            {
                var msg = parsed.Value<string>("message") ?? "Unknown error from Paystack";
                _logger.LogError(
                    "Paystack initialize failed. HttpStatus: {Status}. Ref: {Ref}. Msg: {Msg}. Body: {Body}",
                    httpResponse.StatusCode, request.Reference, msg, body);
                return BaseResponse<PaystackInitializeResultDto>.Failure(
                    $"Payment initiation failed: {msg}", statusCode: 400);
            }

            var data = parsed["data"];
            var result = new PaystackInitializeResultDto
            {
                AuthorizationUrl = data?.Value<string>("authorization_url") ?? string.Empty,
                AccessCode = data?.Value<string>("access_code") ?? string.Empty,
                Reference = data?.Value<string>("reference") ?? request.Reference
            };

            return BaseResponse<PaystackInitializeResultDto>.Success(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("InitializeTransactionAsync cancelled for {Ref}.", request.Reference);
            return BaseResponse<PaystackInitializeResultDto>.Failure(
                "Request was cancelled.", statusCode: 499);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError("Network error contacting Paystack for {Ref}: {Error}", request.Reference, ex.Message);
            return BaseResponse<PaystackInitializeResultDto>.Failure(
                "Could not reach payment provider. Please try again.", statusCode: 502);
        }
        catch (JsonException ex)
        {
            _logger.LogError("Malformed response from Paystack for {Ref}: {Error}", request.Reference, ex.Message);
            return BaseResponse<PaystackInitializeResultDto>.Failure(
                "Unexpected response from payment provider.", statusCode: 502);
        }
        catch (Exception ex)
        {
            _logger.LogError("Unexpected error initializing transaction {Ref}: {Error}", request.Reference, ex.Message);
            return BaseResponse<PaystackInitializeResultDto>.Failure(
                "An error occurred while initiating payment.", statusCode: 500);
        }
    }

    public async Task<BaseResponse<PaystackVerifyResultDto>> VerifyTransactionAsync(
        string reference,
        CancellationToken ct = default)
    {
        try
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"transaction/verify/{reference}");
            httpRequest.Headers.Add("Idempotency-Key", $"VERIFY-{reference}");

            var httpResponse = await _httpClient.SendAsync(httpRequest, ct);
            var body = await httpResponse.Content.ReadAsStringAsync(ct);
            var parsed = JObject.Parse(body);

            var status = parsed.Value<bool?>("status") ?? false;
            if (!status)
            {
                var msg = parsed.Value<string>("message") ?? "Unknown error from Paystack";
                _logger.LogWarning("Paystack verify failed for {Ref}. Msg: {Msg}. Body: {Body}", reference, msg, body);
                return BaseResponse<PaystackVerifyResultDto>.Failure(
                    $"Payment verification failed: {msg}", statusCode: 400);
            }

            var data = parsed["data"];
            var result = new PaystackVerifyResultDto
            {
                Status = data?.Value<string>("status") ?? string.Empty,
                AmountKobo = data?.Value<long?>("amount") ?? 0,
                GatewayResponse = data?.Value<string>("gateway_response") ?? string.Empty
            };

            return BaseResponse<PaystackVerifyResultDto>.Success(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("VerifyTransactionAsync cancelled for {Ref}.", reference);
            return BaseResponse<PaystackVerifyResultDto>.Failure(
                "Request was cancelled.", statusCode: 499);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError("Network error verifying {Ref}: {Error}", reference, ex.Message);
            return BaseResponse<PaystackVerifyResultDto>.Failure(
                "Could not reach payment provider. Please try again.", statusCode: 502);
        }
        catch (JsonException ex)
        {
            _logger.LogError("Malformed verify response for {Ref}: {Error}", reference, ex.Message);
            return BaseResponse<PaystackVerifyResultDto>.Failure(
                "Unexpected response from payment provider.", statusCode: 502);
        }
        catch (Exception ex)
        {
            _logger.LogError("Unexpected error verifying {Ref}: {Error}", reference, ex.Message);
            return BaseResponse<PaystackVerifyResultDto>.Failure(
                "An error occurred during verification.", statusCode: 500);
        }
    }

    public async Task<string?> EnsureSubaccountAsync(
        string businessName,
        string accountNumber,
        string bankCode,
        string idempotencyKey,
        CancellationToken ct = default)
    {
        try
        {
            var listRequest = new HttpRequestMessage(HttpMethod.Get, "subaccount?perPage=100");
            var listResponse = await _httpClient.SendAsync(listRequest, ct);
            var listBody = await listResponse.Content.ReadAsStringAsync(ct);
            var listParsed = JObject.Parse(listBody);

            if ((listParsed.Value<bool?>("status") ?? false) && listParsed["data"] is JArray items)
            {
                foreach (var item in items)
                {
                    var itemAccount = item.Value<string>("account_number");
                    if (itemAccount == accountNumber)
                    {
                        var existingCode = item.Value<string>("subaccount_code");
                        _logger.LogInformation("Subaccount already exists for {Account}: {Code}", accountNumber, existingCode);
                        return existingCode;
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
            var createRequest = new HttpRequestMessage(HttpMethod.Post, "subaccount")
            {
                Content = new StringContent(createJson, Encoding.UTF8, "application/json")
            };
            createRequest.Headers.Add("Idempotency-Key", idempotencyKey);

            var createResponse = await _httpClient.SendAsync(createRequest, ct);
            var createBody = await createResponse.Content.ReadAsStringAsync(ct);
            var createParsed = JObject.Parse(createBody);

            if (createParsed.Value<bool?>("status") ?? false)
            {
                var code = createParsed["data"]?.Value<string>("subaccount_code");
                _logger.LogInformation("Created Paystack subaccount for {Name} ({Account}): {Code}", businessName, accountNumber, code);
                return code;
            }

            var errMsg = createParsed.Value<string>("message") ?? "Unknown error";
            _logger.LogError("Failed to create subaccount for {Account}. Msg: {Msg}. Body: {Body}", accountNumber, errMsg, createBody);
            return null;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("EnsureSubaccountAsync cancelled for {Account}.", accountNumber);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError("Error ensuring subaccount for {Account}: {Error}", accountNumber, ex.Message);
            return null;
        }
    }

    public bool VerifyWebhookSignature(string rawBody, string? signatureHeader)
    {
        if (string.IsNullOrEmpty(signatureHeader) || string.IsNullOrEmpty(rawBody))
            return false;

        try
        {
            var secretBytes = Encoding.UTF8.GetBytes(_settings.SecretKey);
            var bodyBytes = Encoding.UTF8.GetBytes(rawBody);

            using var hmac = new HMACSHA512(secretBytes);
            var hash = hmac.ComputeHash(bodyBytes);
            var computedSignature = Convert.ToHexString(hash).ToLowerInvariant();

            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computedSignature),
                Encoding.UTF8.GetBytes(signatureHeader.ToLowerInvariant()));
        }
        catch (Exception ex)
        {
            _logger.LogError("Error verifying webhook signature: {Error}", ex.Message);
            return false;
        }
    }
}