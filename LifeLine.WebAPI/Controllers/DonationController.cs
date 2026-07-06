using LifeLine.Application.Common.Response;
using LifeLine.Application.DTO.Donation;
using LifeLine.Application.Interfaces.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace LifeLine.WebAPI.Controllers;

[ApiController]
[Route("api/donations")]
public class DonationController : ControllerBase
{
    private readonly IDonationService _donationService;
    private readonly IPaystackService _paystackService;
    private readonly ILogger<DonationController> _logger;

    public DonationController(
        IDonationService donationService,
        IPaystackService paystackService,
        ILogger<DonationController> logger)
    {
        _donationService = donationService;
        _paystackService = paystackService;
        _logger = logger;
    }

    [HttpPost("initiate")]
    [ProducesResponseType(typeof(BaseResponse<InitiateDonationResponseDto>), 200)]
    [ProducesResponseType(typeof(BaseResponse<InitiateDonationResponseDto>), 400)]
    public async Task<IActionResult> Initiate(
        [FromBody] InitiateDonationDto dto,
        CancellationToken ct = default)
    {
        var donorId = User.FindFirstValue("userId");
        var donorName = User.FindFirstValue("fullName");
        var donorEmail = User.FindFirstValue(ClaimTypes.Email)
                         ?? User.FindFirstValue(JwtRegisteredClaimNames.Email);

        var response = await _donationService
            .InitiateDonationAsync(dto, donorId, donorName, donorEmail, ct);
        return StatusCode(response.StatusCode ?? 200, response);
    }

    [HttpGet("verify")]
    [ProducesResponseType(typeof(BaseResponse<string>), 200)]
    [ProducesResponseType(typeof(BaseResponse<string>), 400)]
    public async Task<IActionResult> Verify(
        [FromQuery] string reference,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reference))
            return BadRequest(BaseResponse<string>.Failure("Reference is required."));

        var response = await _donationService.VerifyDonationAsync(reference, ct);
        return StatusCode(response.StatusCode ?? 200, response);
    }

    [HttpPost("webhook")]
    [AllowAnonymous]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> Webhook(
        [FromServices] IServiceScopeFactory scopeFactory)
    {
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync();
        Request.Body.Position = 0;

        var signature = Request.Headers["x-paystack-signature"].FirstOrDefault();
        if (!_paystackService.VerifyWebhookSignature(rawBody, signature))
        {
            _logger.LogWarning("Rejected webhook with invalid or missing signature.");
            return Unauthorized();
        }

        // Paystack expects a fast 200 response; verification work continues
        // in the background on its own scope, since this request's scope
        // will likely be disposed before the Task.Run body finishes.
        _ = Task.Run(async () =>
        {
            try
            {
                var payload = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(rawBody);
                string? eventType = payload?.@event?.ToString();
                if (eventType != "charge.success") return;

                string? reference = payload?.data?.reference?.ToString();
                if (string.IsNullOrEmpty(reference)) return;

                using var scope = scopeFactory.CreateScope();
                var donationService = scope.ServiceProvider.GetRequiredService<IDonationService>();
                await donationService.VerifyDonationAsync(reference, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError("Webhook background processing failed: {Error}", ex.Message);
            }
        });

        return Ok();
    }

    /// <summary>Get all verified donations for a campaign.</summary>
    [HttpGet("campaign/{campaignId:guid}")]
    [ProducesResponseType(typeof(BaseResponse<List<DonationResponseDto>>), 200)]
    public async Task<IActionResult> GetCampaignDonations(
        Guid campaignId, CancellationToken ct = default)
    {
        var response = await _donationService
            .GetCampaignDonationsAsync(campaignId, ct);
        return StatusCode(response.StatusCode ?? 200, response);
    }
}