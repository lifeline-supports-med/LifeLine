using LifeLine.Application.Common.Response;
using LifeLine.Application.DTO.Payout;
using LifeLine.Application.Interfaces.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace LifeLine.WebAPI.Controllers;

[ApiController]
[Route("api/payouts")]
public class PayoutController : ControllerBase
{
    private readonly IPayoutService _payoutService;

    public PayoutController(IPayoutService payoutService)
    {
        _payoutService = payoutService;
    }

    [HttpPost("request")]
    [Authorize(Roles = "CampaignCreator,Organization")]
    [ProducesResponseType(typeof(BaseResponse<PayoutResponseDto>), 201)]
    [ProducesResponseType(typeof(BaseResponse<PayoutResponseDto>), 400)]
    public async Task<IActionResult> RequestPayout(
        [FromBody] RequestPayoutDto dto,
        CancellationToken ct = default)
    {
        var requesterId = User.FindFirstValue("userId");
        if (string.IsNullOrEmpty(requesterId))
            return Unauthorized(BaseResponse<string>.Unauthorized());

        var response = await _payoutService.RequestPayoutAsync(requesterId, dto, ct);
        return StatusCode(response.StatusCode ?? 200, response);
    }

    /// <summary>Get my payout requests.</summary>
    [HttpGet("my")]
    [Authorize(Roles = "CampaignCreator,Organization")]
    [ProducesResponseType(typeof(BaseResponse<List<PayoutResponseDto>>), 200)]
    public async Task<IActionResult> GetMyPayouts(CancellationToken ct = default)
    {
        var requesterId = User.FindFirstValue("userId");
        if (string.IsNullOrEmpty(requesterId))
            return Unauthorized(BaseResponse<string>.Unauthorized());

        var response = await _payoutService.GetMyPayoutsAsync(requesterId, ct);
        return StatusCode(response.StatusCode ?? 200, response);
    }

    /// <summary>Get all payout requests (admin only).</summary>
    [HttpGet]
    [Authorize(Roles = "SuperAdmin,VerificationAdmin")]
    [ProducesResponseType(typeof(BaseResponse<List<PayoutResponseDto>>), 200)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        var response = await _payoutService
            .GetAllPayoutsAsync(page, pageSize, status, ct);
        return StatusCode(response.StatusCode ?? 200, response);
    }

    /// <summary>Approve a payout request.</summary>
    [HttpPut("{id:guid}/approve")]
    [Authorize(Roles = "SuperAdmin,VerificationAdmin")]
    [ProducesResponseType(typeof(BaseResponse<string>), 200)]
    public async Task<IActionResult> Approve(
        Guid id, CancellationToken ct = default)
    {
        var adminId = User.FindFirstValue("userId");
        if (string.IsNullOrEmpty(adminId))
            return Unauthorized(BaseResponse<string>.Unauthorized());

        var response = await _payoutService.ApprovePayoutAsync(id, adminId, ct);
        return StatusCode(response.StatusCode ?? 200, response);
    }

    /// <summary>Reject a payout request with a reason.</summary>
    [HttpPut("{id:guid}/reject")]
    [Authorize(Roles = "SuperAdmin,VerificationAdmin")]
    [ProducesResponseType(typeof(BaseResponse<string>), 200)]
    public async Task<IActionResult> Reject(
        Guid id,
        [FromBody] RejectPayoutDto dto,
        CancellationToken ct = default)
    {
        var adminId = User.FindFirstValue("userId");
        if (string.IsNullOrEmpty(adminId))
            return Unauthorized(BaseResponse<string>.Unauthorized());

        var response = await _payoutService.RejectPayoutAsync(id, adminId, dto, ct);
        return StatusCode(response.StatusCode ?? 200, response);
    }
}