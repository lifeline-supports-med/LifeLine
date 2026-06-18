using LifeLine.Application.Common.Response;
using LifeLine.Application.DTO.Auth;


namespace LifeLine.Application.Interfaces.IServices
{
    public interface IAuthService
    {
        Task<BaseResponse<AuthResponseDto>> RegisterAsync(RegisterDto dto);
        Task<BaseResponse<AuthResponseDto>> LoginAsync(LoginDto dto);
        Task<BaseResponse<AuthResponseDto>> RefreshTokenAsync(RefreshTokenDto dto);
        Task<BaseResponse<AuthResponseDto>> GoogleSignInAsync(GoogleSignInDto dto);
        Task<BaseResponse<string>> ForgotPasswordAsync(ForgotPasswordDto dto);
        Task<BaseResponse<string>> ResetPasswordAsync(ResetPasswordDto dto);
        Task<BaseResponse<string>> ChangePasswordAsync(string userId, ChangePasswordDto dto);
        Task<BaseResponse<string>> LogoutAsync(string userId);
    }
}
