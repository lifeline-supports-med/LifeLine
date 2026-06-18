using LifeLine.Application.Common.Response;
using LifeLine.Application.DTO.Admin;
using LifeLine.Application.Interfaces.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace LifeLine.WebAPI.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "SuperAdmin,VerificationAdmin")]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;

    public AdminController(IAdminService adminService)
    {
        _adminService = adminService;
    }

    [HttpGet("campaigns")]
    [ProducesResponseType(typeof(BaseResponse<List<AdminCampaignResponseDto>>), 200)]
    public async Task<IActionResult> GetCampaigns(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        var response = await _adminService
            .GetAllCampaignsAsync(page, pageSize, status, ct);
        return StatusCode(response.StatusCode ?? 200, response);
    }

    [HttpGet("campaigns/{id:guid}")]
    [ProducesResponseType(typeof(BaseResponse<AdminCampaignResponseDto>), 200)]
    [ProducesResponseType(typeof(BaseResponse<AdminCampaignResponseDto>), 404)]
    public async Task<IActionResult> GetCampaignDetails(
        Guid id, CancellationToken ct = default)
    {
        var response = await _adminService.GetCampaignDetailsAsync(id, ct);
        return StatusCode(response.StatusCode ?? 200, response);
    }

    [HttpPut("campaigns/{id:guid}/verify")]
    [ProducesResponseType(typeof(BaseResponse<string>), 200)]
    [ProducesResponseType(typeof(BaseResponse<string>), 400)]
    public async Task<IActionResult> VerifyCampaign(
        Guid id, CancellationToken ct = default)
    {
        var adminId = User.FindFirstValue("userId");
        if (string.IsNullOrEmpty(adminId))
            return Unauthorized(BaseResponse<string>.Unauthorized());

        var response = await _adminService.VerifyCampaignAsync(id, adminId, ct);
        return StatusCode(response.StatusCode ?? 200, response);
    }

    [HttpPut("campaigns/{id:guid}/reject")]
    [ProducesResponseType(typeof(BaseResponse<string>), 200)]
    [ProducesResponseType(typeof(BaseResponse<string>), 400)]
    public async Task<IActionResult> RejectCampaign(
        Guid id,
        [FromBody] RejectCampaignDto dto,
        CancellationToken ct = default)
    {
        var adminId = User.FindFirstValue("userId");
        if (string.IsNullOrEmpty(adminId))
            return Unauthorized(BaseResponse<string>.Unauthorized());

        var response = await _adminService.RejectCampaignAsync(id, adminId, dto, ct);
        return StatusCode(response.StatusCode ?? 200, response);
    }

    [HttpGet("users")]
    [Authorize(Roles = "SuperAdmin")]
    [ProducesResponseType(typeof(BaseResponse<List<AdminUserResponseDto>>), 200)]
    public async Task<IActionResult> GetUsers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var response = await _adminService.GetAllUsersAsync(page, pageSize, ct);
        return StatusCode(response.StatusCode ?? 200, response);
    }

    [HttpPut("users/{userId}/suspend")]
    [Authorize(Roles = "SuperAdmin")]
    [ProducesResponseType(typeof(BaseResponse<string>), 200)]
    public async Task<IActionResult> SuspendUser(
        string userId, CancellationToken ct = default)
    {
        var adminId = User.FindFirstValue("userId");
        if (string.IsNullOrEmpty(adminId))
            return Unauthorized(BaseResponse<string>.Unauthorized());

        var response = await _adminService.SuspendUserAsync(userId, adminId, ct);
        return StatusCode(response.StatusCode ?? 200, response);
    }

    [HttpPut("users/{userId}/reactivate")]
    [Authorize(Roles = "SuperAdmin")]
    [ProducesResponseType(typeof(BaseResponse<string>), 200)]
    public async Task<IActionResult> ReactivateUser(
        string userId, CancellationToken ct = default)
    {
        var adminId = User.FindFirstValue("userId");
        if (string.IsNullOrEmpty(adminId))
            return Unauthorized(BaseResponse<string>.Unauthorized());

        var response = await _adminService.ReactivateUserAsync(userId, adminId, ct);
        return StatusCode(response.StatusCode ?? 200, response);
    }
}