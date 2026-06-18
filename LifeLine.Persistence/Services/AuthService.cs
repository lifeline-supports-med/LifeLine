using Google.Apis.Auth;
using LifeLine.Application.Common.Response;
using LifeLine.Application.DTO.Auth;
using LifeLine.Application.Helpers;
using LifeLine.Application.Interfaces.IServices;
using LifeLine.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LifeLine.Persistence.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly JwtTokenHelper _jwtHelper;
    private readonly IConfiguration _config;
    private readonly IEmailService _emailService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        JwtTokenHelper jwtHelper,
        IConfiguration config,
        IEmailService emailService,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _jwtHelper = jwtHelper;
        _config = config;
        _emailService = emailService;
        _logger = logger;
    }

    //public async Task<BaseResponse<AuthResponseDto>> RegisterAsync(RegisterDto dto)
    //{
    //    if (string.IsNullOrWhiteSpace(dto.Email))
    //        return BaseResponse<AuthResponseDto>.Failure("Email is required.", statusCode: 400);

    //    var existingUser = await _userManager.FindByEmailAsync(dto.Email);
    //    if (existingUser is not null)
    //        return BaseResponse<AuthResponseDto>.Failure(
    //            "An account with this email already exists.", statusCode: 409);

    //    var email = dto.Email.ToLower().Trim();

    //    var user = new ApplicationUser
    //    {
    //        FirstName = dto.FirstName.Trim(),
    //        LastName = dto.LastName.Trim(),
    //        Email = email,
    //        NormalizedEmail = email.ToUpper(),
    //        UserName = email,
    //        NormalizedUserName = email.ToUpper(),
    //        PhoneNumber = dto.PhoneNumber?.Trim(),
    //        Role = dto.Role,
    //        IsActive = true,
    //        EmailConfirmed = false
    //    };

    //    _logger.LogInformation(
    //        "Attempting to register: Email={Email}, UserName={UserName}",
    //        user.Email, user.UserName);

    //    var result = await _userManager.CreateAsync(user, dto.Password);
    //    if (!result.Succeeded)
    //    {
    //        var errors = result.Errors.Select(e => e.Description).ToList();
    //        return BaseResponse<AuthResponseDto>.ValidationFailure(errors);
    //    }

    //    await _userManager.AddToRoleAsync(user, dto.Role);

    //    await _emailService.SendWelcomeEmailAsync(
    //        user.Email!, $"{user.FirstName} {user.LastName}".Trim());

    //    _logger.LogInformation(
    //        "New user registered: {Email} as {Role}", user.Email, user.Role);

    //    var response = await BuildAuthResponseAsync(user);
    //    return BaseResponse<AuthResponseDto>.Success(
    //        response, "Welcome to Lifeline! Your account has been created.", 201);
    //}

    public async Task<BaseResponse<AuthResponseDto>> RegisterAsync(RegisterDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Email))
            return BaseResponse<AuthResponseDto>.Failure(
                "Email is required.", statusCode: 400);

        var existingUser = await _userManager.FindByEmailAsync(dto.Email);
        if (existingUser is not null)
            return BaseResponse<AuthResponseDto>.Failure(
                "An account with this email already exists.", statusCode: 409);

        var email = dto.Email.ToLower().Trim();

        var user = new ApplicationUser
        {
            FirstName = dto.FirstName.Trim(),
            LastName = dto.LastName.Trim(),
            Email = email,
            NormalizedEmail = email.ToUpper(),
            UserName = email,
            NormalizedUserName = email.ToUpper(),
            PhoneNumber = dto.PhoneNumber?.Trim(),
            Role = "CampaignCreator",
            IsActive = true,
            EmailConfirmed = false
        };

        _logger.LogInformation(
            "Attempting to register: Email={Email}, UserName={UserName}",
            user.Email, user.UserName);

        var result = await _userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => e.Description).ToList();
            return BaseResponse<AuthResponseDto>.ValidationFailure(errors);
        }

        await _userManager.AddToRoleAsync(user, "CampaignCreator");

        await _emailService.SendWelcomeEmailAsync(
            user.Email!, $"{user.FirstName} {user.LastName}".Trim());

        _logger.LogInformation(
            "New user registered: {Email} as CampaignCreator", user.Email);

        var response = await BuildAuthResponseAsync(user);
        return BaseResponse<AuthResponseDto>.Success(
            response, "Welcome to Lifeline! Your account has been created.", 201);
    }

    public async Task<BaseResponse<AuthResponseDto>> LoginAsync(LoginDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email.ToLower().Trim());

        if (user is null || !await _userManager.CheckPasswordAsync(user, dto.Password))
            return BaseResponse<AuthResponseDto>.Unauthorized(
                "Invalid email or password. Please try again.");

        if (!user.IsActive)
            return BaseResponse<AuthResponseDto>.Failure(
                "Your account has been suspended. Please contact support.", statusCode: 403);

        _logger.LogInformation("User logged in: {Email}", user.Email);

        var response = await BuildAuthResponseAsync(user);
        return BaseResponse<AuthResponseDto>.Success(response, "Welcome back!");
    }

    public async Task<BaseResponse<AuthResponseDto>> RefreshTokenAsync(RefreshTokenDto dto)
    {
        var principal = _jwtHelper.GetPrincipalFromExpiredToken(dto.AccessToken);
        if (principal is null)
            return BaseResponse<AuthResponseDto>.Unauthorized("Invalid access token.");

        var userId = principal.FindFirst("userId")?.Value;
        var user = await _userManager.FindByIdAsync(userId!);

        if (user is null
            || user.RefreshToken != dto.RefreshToken
            || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
        {
            return BaseResponse<AuthResponseDto>.Unauthorized(
                "Your session has expired. Please log in again.");
        }

        var response = await BuildAuthResponseAsync(user);
        return BaseResponse<AuthResponseDto>.Success(response, "Token refreshed successfully.");
    }

    public async Task<BaseResponse<string>> ForgotPasswordAsync(ForgotPasswordDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email.ToLower().Trim());

        // Always return same message — never expose if email exists
        if (user is null)
            return BaseResponse<string>.Success(
                null!, "If this email is registered, a password reset link has been sent.");

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var encoded = Uri.EscapeDataString(token);

        await _emailService.SendPasswordResetEmailAsync(
            user.Email!, $"{user.FirstName} {user.LastName}".Trim(), encoded);

        _logger.LogInformation(
            "Password reset token sent to {Email}", user.Email);

        return BaseResponse<string>.Success(
            null!, "If this email is registered, a password reset link has been sent.");
    }

    public async Task<BaseResponse<string>> ResetPasswordAsync(ResetPasswordDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email.ToLower().Trim());
        if (user is null)
            return BaseResponse<string>.Failure(
                "Invalid password reset request.", statusCode: 400);

        var decodedToken = Uri.UnescapeDataString(dto.Token);
        var result = await _userManager.ResetPasswordAsync(
            user, decodedToken, dto.NewPassword);

        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => e.Description).ToList();
            return BaseResponse<string>.ValidationFailure(errors);
        }

        await InvalidateRefreshTokenAsync(user);

        await _emailService.SendPasswordChangedEmailAsync(
            user.Email!, $"{user.FirstName} {user.LastName}".Trim());

        _logger.LogInformation(
            "Password reset successful for {Email}", user.Email);

        return BaseResponse<string>.Success(
            null!, "Your password has been reset successfully. Please log in.");
    }

    public async Task<BaseResponse<string>> ChangePasswordAsync(string userId, ChangePasswordDto dto)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return BaseResponse<string>.Unauthorized();

        var result = await _userManager.ChangePasswordAsync(
            user, dto.CurrentPassword, dto.NewPassword);

        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => e.Description).ToList();
            return BaseResponse<string>.ValidationFailure(errors);
        }

        await InvalidateRefreshTokenAsync(user);

        await _emailService.SendPasswordChangedEmailAsync(
            user.Email!, $"{user.FirstName} {user.LastName}".Trim());

        _logger.LogInformation(
            "Password changed for user {UserId}", userId);

        return BaseResponse<string>.Success(
            null!, "Password changed successfully. Please log in again.");
    }

    public async Task<BaseResponse<AuthResponseDto>> GoogleSignInAsync(GoogleSignInDto dto)
    {
        GoogleJsonWebSignature.Payload payload;

        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = [_config["GoogleKeys:ClientId"]!]
            };
            payload = await GoogleJsonWebSignature.ValidateAsync(dto.IdToken, settings);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "Google token validation failed: {Message}", ex.Message);
            return BaseResponse<AuthResponseDto>.Unauthorized(
                "Google sign-in failed. Please try again.");
        }

        var user = await _userManager.FindByEmailAsync(payload.Email);

        if (user is null)
        {
            var email = payload.Email.ToLower().Trim();

            user = new ApplicationUser
            {
                FirstName = payload.GivenName ?? "User",
                LastName = payload.FamilyName ?? "",
                Email = email,
                NormalizedEmail = email.ToUpper(),
                UserName = email,
                NormalizedUserName = email.ToUpper(),
                Role = "Donor",
                IsActive = true,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user);
            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(e => e.Description).ToList();
                return BaseResponse<AuthResponseDto>.ValidationFailure(errors);
            }

            await _userManager.AddToRoleAsync(user, "Donor");

            await _emailService.SendWelcomeEmailAsync(
                user.Email!, $"{user.FirstName} {user.LastName}".Trim());

            _logger.LogInformation(
                "New user registered via Google: {Email}", user.Email);
        }

        if (!user.IsActive)
            return BaseResponse<AuthResponseDto>.Failure(
                "Your account has been suspended. Please contact support.", statusCode: 403);

        var response = await BuildAuthResponseAsync(user);
        return BaseResponse<AuthResponseDto>.Success(response, "Google sign-in successful.");
    }

    public async Task<BaseResponse<string>> LogoutAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return BaseResponse<string>.Unauthorized();

        await InvalidateRefreshTokenAsync(user);

        _logger.LogInformation("User logged out: {UserId}", userId);

        return BaseResponse<string>.Success(
            null!, "You have been logged out successfully.");
    }

    private async Task<AuthResponseDto> BuildAuthResponseAsync(ApplicationUser user)
    {
        var accessToken = _jwtHelper.GenerateAccessToken(user);
        var refreshToken = _jwtHelper.GenerateRefreshToken();
        var expiryDays = int.Parse(_config["JwtSettings:RefreshTokenExpiryDays"]!);

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(expiryDays);
        await _userManager.UpdateAsync(user);

        return new AuthResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(
                           double.Parse(_config["JwtSettings:ExpiryMinutes"]!)),
            UserId = user.Id.ToString(),
            FullName = $"{user.FirstName} {user.LastName}".Trim(),
            Email = user.Email!,
            Role = user.Role
        };
    }

    private async Task InvalidateRefreshTokenAsync(ApplicationUser user)
    {
        user.RefreshToken = null;
        user.RefreshTokenExpiryTime = null;
        await _userManager.UpdateAsync(user);
    }
}