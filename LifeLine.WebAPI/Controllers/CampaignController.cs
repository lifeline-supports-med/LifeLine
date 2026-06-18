using LifeLine.Application.Common.Response;
using LifeLine.Application.Common.Response.Campaign;
using LifeLine.Application.DTO.Campaign;
using LifeLine.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace LifeLine.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CampaignController : ControllerBase
    {
        private readonly ICampaignService _campaignService;

        public CampaignController(ICampaignService campaignService)
        {
            _campaignService = campaignService;
        }

        [HttpPost]
        [Authorize(Roles = "SuperAdmin, CampaignCreator,Organization")]
        public async Task<IActionResult> Create(
            [FromBody] CreateCampaignDto dto,
            CancellationToken ct = default)
        {
            var creatorId = User.FindFirstValue("userId");
            var creatorName = User.FindFirstValue("fullName") ?? "Campaign Creator";
            var creatorEmail = User.FindFirstValue(ClaimTypes.Email) ?? "";

            if (string.IsNullOrEmpty(creatorId))
                return Unauthorized(BaseResponse<string>.Unauthorized());

            var response = await _campaignService
                .CreateCampaignAsync(creatorId, creatorName, creatorEmail, dto, ct);
            return StatusCode(response.StatusCode ?? 200, response);
        }

        [HttpGet]
        [ProducesResponseType(typeof(BaseResponse<List<CampaignResponseDto>>), 200)]
        public async Task<IActionResult> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            CancellationToken ct = default)
        {
            var response = await _campaignService.GetAllCampaignsAsync(page, pageSize, ct);
            return StatusCode(response.StatusCode ?? 200, response);
        }

        [HttpGet("{slug}")]
        [ProducesResponseType(typeof(BaseResponse<CampaignResponseDto>), 200)]
        [ProducesResponseType(typeof(BaseResponse<CampaignResponseDto>), 404)]
        public async Task<IActionResult> GetBySlug(
            string slug, CancellationToken ct = default)
        {
            var response = await _campaignService.GetCampaignBySlugAsync(slug, ct);
            return StatusCode(response.StatusCode ?? 200, response);
        }

        [HttpGet("my")]
        [Authorize]
        [ProducesResponseType(typeof(BaseResponse<List<CampaignResponseDto>>), 200)]
        public async Task<IActionResult> GetMyCampaigns(CancellationToken ct = default)
        {
            var creatorId = User.FindFirstValue("userId");
            if (string.IsNullOrEmpty(creatorId))
                return Unauthorized(BaseResponse<string>.Unauthorized());

            var response = await _campaignService.GetMyCampaignsAsync(creatorId, ct);
            return StatusCode(response.StatusCode ?? 200, response);
        }

        //[HttpPost]
        //[Authorize(Roles = "CampaignCreator,Organization")]
        //[ProducesResponseType(typeof(BaseResponse<CampaignResponseDto>), 201)]
        //[ProducesResponseType(typeof(BaseResponse<CampaignResponseDto>), 400)]
        //public async Task<IActionResult> Create(
        //    [FromBody] CreateCampaignDto dto,
        //    CancellationToken ct = default)
        //{
        //    var creatorId = User.FindFirstValue("userId");
        //    if (string.IsNullOrEmpty(creatorId))
        //        return Unauthorized(BaseResponse<string>.Unauthorized());

        //    var response = await _campaignService.CreateCampaignAsync(creatorId, dto, ct);
        //    return StatusCode(response.StatusCode ?? 200, response);
        //}

        [HttpPut("{id:guid}")]
        [Authorize(Roles = "SuperAdmin, CampaignCreator,Organization")]
        [ProducesResponseType(typeof(BaseResponse<CampaignResponseDto>), 200)]
        public async Task<IActionResult> Update(
            Guid id,
            [FromBody] UpdateCampaignDto dto,
            CancellationToken ct = default)
        {
            var creatorId = User.FindFirstValue("userId");
            if (string.IsNullOrEmpty(creatorId))
                return Unauthorized(BaseResponse<string>.Unauthorized());

            var response = await _campaignService.UpdateCampaignAsync(id, creatorId, dto, ct);
            return StatusCode(response.StatusCode ?? 200, response);
        }

        //[HttpPost("{id:guid}/cover-image")]
        //[Authorize(Roles = "CampaignCreator,Organization")]
        //[ProducesResponseType(typeof(BaseResponse<string>), 200)]
        //public async Task<IActionResult> UploadCoverImage(
        //    Guid id,
        //    IFormFile file,
        //    CancellationToken ct = default)
        //{
        //    var creatorId = User.FindFirstValue("userId");
        //    if (string.IsNullOrEmpty(creatorId))
        //        return Unauthorized(BaseResponse<string>.Unauthorized());

        //    var response = await _campaignService.UploadCoverImageAsync(id, creatorId, file, ct);
        //    return StatusCode(response.StatusCode ?? 200, response);
        //}

        [HttpPost("{id:guid}/cover-image")]
        [Authorize(Roles = "SuperAdmin, CampaignCreator,Organization")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(BaseResponse<string>), 200)]
        public async Task<IActionResult> UploadCoverImage(
            Guid id,
            [FromForm] UploadCoverImageDto request,
            CancellationToken ct = default)
        {
            var creatorId = User.FindFirstValue("userId");
            if (string.IsNullOrEmpty(creatorId))
                return Unauthorized(BaseResponse<string>.Unauthorized());

            var response = await _campaignService
                .UploadCoverImageAsync(id, creatorId, request.File, ct);
            return StatusCode(response.StatusCode ?? 200, response);
        }

        //[HttpPost("{id:guid}/documents")]
        //[Authorize(Roles = "CampaignCreator,Organization")]
        //[ProducesResponseType(typeof(BaseResponse<string>), 200)]
        //public async Task<IActionResult> UploadDocument(
        //    Guid id,
        //    IFormFile file,
        //    [FromQuery] string fileType = "medical_document",
        //    CancellationToken ct = default)
        //{
        //    var creatorId = User.FindFirstValue("userId");
        //    if (string.IsNullOrEmpty(creatorId))
        //        return Unauthorized(BaseResponse<string>.Unauthorized());

        //    var response = await _campaignService
        //        .UploadMedicalDocumentAsync(id, creatorId, file, fileType, ct);
        //    return StatusCode(response.StatusCode ?? 200, response);
        //}

        [HttpPost("{id:guid}/documents")]
        [Authorize(Roles = "SuperAdmin, CampaignCreator,Organization")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(BaseResponse<string>), 200)]
        public async Task<IActionResult> UploadDocument(
            Guid id,
            [FromForm] UploadDocumentDto request,
            CancellationToken ct = default)
        {
            var creatorId = User.FindFirstValue("userId");
            if (string.IsNullOrEmpty(creatorId))
                return Unauthorized(BaseResponse<string>.Unauthorized());

            var response = await _campaignService
                .UploadMedicalDocumentAsync(id, creatorId, request.File, request.FileType, ct);
            return StatusCode(response.StatusCode ?? 200, response);
        }

        [HttpPost("{id:guid}/updates")]
        [Authorize(Roles = "SuperAdmin, CampaignCreator,Organization")]
        [ProducesResponseType(typeof(BaseResponse<string>), 200)]
        public async Task<IActionResult> PostUpdate(
            Guid id,
            [FromBody] PostCampaignUpdateDto dto,
            CancellationToken ct = default)
        {
            var creatorId = User.FindFirstValue("userId");
            if (string.IsNullOrEmpty(creatorId))
                return Unauthorized(BaseResponse<string>.Unauthorized());

            var response = await _campaignService.PostUpdateAsync(id, creatorId, dto, ct);
            return StatusCode(response.StatusCode ?? 200, response);
        }

        [HttpGet("{id:guid}/updates")]
        [ProducesResponseType(typeof(BaseResponse<List<CampaignUpdateResponseDto>>), 200)]
        public async Task<IActionResult> GetUpdates(
            Guid id, CancellationToken ct = default)
        {
            var response = await _campaignService.GetCampaignUpdatesAsync(id, ct);
            return StatusCode(response.StatusCode ?? 200, response);
        }

        [HttpDelete("{id:guid}")]
        [Authorize]
        [ProducesResponseType(typeof(BaseResponse<string>), 200)]
        public async Task<IActionResult> Delete(
            Guid id, CancellationToken ct = default)
        {
            var requesterId = User.FindFirstValue("userId");
            var requesterRole = User.FindFirstValue(ClaimTypes.Role);

            if (string.IsNullOrEmpty(requesterId))
                return Unauthorized(BaseResponse<string>.Unauthorized());

            var response = await _campaignService
                .DeleteCampaignAsync(id, requesterId, requesterRole ?? "", ct);
            return StatusCode(response.StatusCode ?? 200, response);
        }
    }
}
