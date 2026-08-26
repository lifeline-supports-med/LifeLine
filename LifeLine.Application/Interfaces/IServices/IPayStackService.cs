using LifeLine.Application.Common.RequestModel.PaystackDTO;
using LifeLine.Application.Common.Response;
using LifeLine.Application.DTO.Paystack;

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

        Task<BaseResponse<PaystackResolveAccountResultDto>> ResolveAccountNumberAsync(
        string accountNumber, string bankCode, CancellationToken ct = default);

        Task<BaseResponse<PaystackSubaccountDetailDto>> GetSubaccountAsync(
    string subaccountCode,
    CancellationToken ct = default);

        Task<BaseResponse<List<PaystackBankDto>>> GetBanksAsync(CancellationToken ct = default);

        Task<string?> EnsureSubaccountAsync(
            string businessName,
            string accountNumber,
            string bankCode,
            string idempotencyKey,
            CancellationToken ct = default);

        bool VerifyWebhookSignature(string rawBody, string? signatureHeader);
    }
}
