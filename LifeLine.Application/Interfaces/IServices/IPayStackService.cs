using LifeLine.Application.Common.RequestModel.PaystackDTO;
using LifeLine.Application.Common.Response;

namespace LifeLine.Application.Interfaces.IServices
{
    public interface IPaystackService
    {
        Task<BaseResponse<PaystackInitializeResultDto>> InitializeTransactionAsync(
            PaystackInitializeRequestDto request,
            string idempotencyKey,
            CancellationToken ct = default);

        Task<BaseResponse<PaystackVerifyResultDto>> VerifyTransactionAsync(
            string reference,
            CancellationToken ct = default);

        Task<string?> EnsureSubaccountAsync(
            string businessName,
            string accountNumber,
            string bankCode,
            string idempotencyKey,
            CancellationToken ct = default);

        /// <summary>
        /// Verifies the x-paystack-signature header against the raw request
        /// body using HMAC-SHA512 with the Paystack secret key. Must be called
        /// with the untouched raw body — not a re-serialized/deserialized copy.
        /// </summary>
        bool VerifyWebhookSignature(string rawBody, string? signatureHeader);
    }
}
