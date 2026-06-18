using FluentValidation;
using LifeLine.Application.Common.Response;
using LifeLine.Application.DTO.Auth;
using LifeLine.Application.Interfaces.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace LifeLine.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IValidator<RegisterDto> _registerValidator;
        private readonly IValidator<LoginDto> _loginValidator;
        private readonly IValidator<ResetPasswordDto> _resetValidator;
        private readonly IValidator<ChangePasswordDto> _changePasswordValidator;
        private readonly IValidator<ForgotPasswordDto> _forgotPasswordValidator;

        public AuthController(
        IAuthService authService,
        IValidator<RegisterDto> registerValidator,
        IValidator<LoginDto> loginValidator,
        IValidator<ResetPasswordDto> resetValidator,
        IValidator<ChangePasswordDto> changePasswordValidator,
        IValidator<ForgotPasswordDto> forgotPasswordValidator)
        {
            _authService = authService;
            _registerValidator = registerValidator;
            _loginValidator = loginValidator;
            _resetValidator = resetValidator;
            _changePasswordValidator = changePasswordValidator;
            _forgotPasswordValidator = forgotPasswordValidator;
        }

        [HttpPost("register")]
        [ProducesResponseType(typeof(BaseResponse<AuthResponseDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(BaseResponse<AuthResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(BaseResponse<AuthResponseDto>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            var validation = await _registerValidator.ValidateAsync(dto);
            if (!validation.IsValid)
            {
                var errors = validation.Errors.Select(e => e.ErrorMessage).ToList();
                return UnprocessableEntity(BaseResponse<AuthResponseDto>.ValidationFailure(errors));
            }

            var response = await _authService.RegisterAsync(dto);
            return StatusCode(response.StatusCode ?? 200, response);
        }

        [HttpPost("login")]
        [EnableRateLimiting("per-user")]
        [ProducesResponseType(typeof(BaseResponse<AuthResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BaseResponse<AuthResponseDto>), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var validation = await _loginValidator.ValidateAsync(dto);
            if (!validation.IsValid)
            {
                var errors = validation.Errors.Select(e => e.ErrorMessage).ToList();
                return UnprocessableEntity(BaseResponse<AuthResponseDto>.ValidationFailure(errors));
            }

            var response = await _authService.LoginAsync(dto);
            return StatusCode(response.StatusCode ?? 200, response);
        }

        [HttpPost("refresh-token")]
        [ProducesResponseType(typeof(BaseResponse<AuthResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BaseResponse<AuthResponseDto>), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenDto dto)
        {
            var response = await _authService.RefreshTokenAsync(dto);
            return StatusCode(response.StatusCode ?? 200, response);
        }

        [HttpPost("forgot-password")]
        [EnableRateLimiting("per-user")]
        [ProducesResponseType(typeof(BaseResponse<string>), StatusCodes.Status200OK)]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
        {
            var validation = await _forgotPasswordValidator.ValidateAsync(dto);
            if (!validation.IsValid)
            {
                var errors = validation.Errors.Select(e => e.ErrorMessage).ToList();
                return UnprocessableEntity(BaseResponse<string>.ValidationFailure(errors));
            }

            var response = await _authService.ForgotPasswordAsync(dto);
            return StatusCode(response.StatusCode ?? 200, response);
        }

        [HttpPost("reset-password")]
        [ProducesResponseType(typeof(BaseResponse<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BaseResponse<string>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
        {
            var validation = await _resetValidator.ValidateAsync(dto);
            if (!validation.IsValid)
            {
                var errors = validation.Errors.Select(e => e.ErrorMessage).ToList();
                return UnprocessableEntity(BaseResponse<string>.ValidationFailure(errors));
            }

            var response = await _authService.ResetPasswordAsync(dto);
            return StatusCode(response.StatusCode ?? 200, response);
        }

        [HttpPost("change-password")]
        [Authorize]
        [ProducesResponseType(typeof(BaseResponse<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BaseResponse<string>), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
        {
            var validation = await _changePasswordValidator.ValidateAsync(dto);
            if (!validation.IsValid)
            {
                var errors = validation.Errors.Select(e => e.ErrorMessage).ToList();
                return UnprocessableEntity(BaseResponse<string>.ValidationFailure(errors));
            }

            var userId = User.FindFirstValue("userId");
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(BaseResponse<string>.Unauthorized());

            var response = await _authService.ChangePasswordAsync(userId, dto);
            return StatusCode(response.StatusCode ?? 200, response);
        }

        [HttpPost("google")]
        [ProducesResponseType(typeof(BaseResponse<AuthResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BaseResponse<AuthResponseDto>), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GoogleSignIn([FromBody] GoogleSignInDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.IdToken))
                return BadRequest(BaseResponse<string>.Failure("Google token is required."));

            var response = await _authService.GoogleSignInAsync(dto);
            return StatusCode(response.StatusCode ?? 200, response);
        }

        [HttpPost("logout")]
        [Authorize]
        [ProducesResponseType(typeof(BaseResponse<string>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Logout()
        {
            var userId = User.FindFirstValue("userId");
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(BaseResponse<string>.Unauthorized());

            var response = await _authService.LogoutAsync(userId);
            return StatusCode(response.StatusCode ?? 200, response);
        }
    }
}
