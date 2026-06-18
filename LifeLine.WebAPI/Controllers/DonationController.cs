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

    public DonationController(IDonationService donationService)
    {
        _donationService = donationService;
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
    public IActionResult Webhook(
    [FromBody] dynamic payload,
    [FromServices] IServiceScopeFactory scopeFactory)
    {

        _ = Task.Run(async () =>
        {
            try
            {
                string? eventType = payload?.@event?.ToString();

                if (eventType != "charge.success")
                    return;

                string? reference = payload?.data?.reference?.ToString();
                if (string.IsNullOrEmpty(reference))
                    return;

                using var scope = scopeFactory.CreateScope();
                var donationService = scope.ServiceProvider
                    .GetRequiredService<IDonationService>();

                await donationService.VerifyDonationAsync(
                    reference, CancellationToken.None);
            }
            catch (Exception ex)
            {
                var logger = scopeFactory.CreateScope().ServiceProvider
                    .GetRequiredService<ILogger<DonationController>>();
                logger.LogError(
                    "Webhook background processing failed: {Error}", ex.Message);
            }
        });

        return Ok();
    }

    //[HttpPost("webhook")]
    //[AllowAnonymous]
    //[ProducesResponseType(200)]
    //public async Task<IActionResult> Webhook(
    //    [FromBody] dynamic payload,
    //    [FromServices] IServiceScopeFactory scopeFactory,
    //    CancellationToken ct = default)
    //{
    //    try
    //    {
    //        string? eventType = payload?.@event?.ToString();

    //        if (eventType == "charge.success")
    //        {
    //            string? reference = payload?.data?.reference?.ToString();
    //            if (!string.IsNullOrEmpty(reference))
    //            {
    //                await _donationService.VerifyDonationAsync(reference, ct);
    //                return Ok();
    //            }
    //        }

    //        return Ok();
    //    }
    //    catch
    //    {
    //        return Ok();
    //    }
    //}

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