using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TanHuyComputer.API.DTOs;
using TanHuyComputer.API.Helpers;
using TanHuyComputer.API.Services;

namespace TanHuyComputer.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    public AuthController(IAuthService authService) => _authService = authService;

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        var result = await _authService.RegisterAsync(req);
        return Ok(ApiResponse<LoginResponse>.SuccessResponse(result, "Đăng ký thành công."));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var result = await _authService.LoginAsync(req);
        return Ok(ApiResponse<LoginResponse>.SuccessResponse(result, "Đăng nhập thành công."));
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest req)
    {
        var token = await _authService.ForgotPasswordAsync(req.Email);
        // Dev only: trả token về. Production: gửi email
        return Ok(ApiResponse<object>.SuccessResponse(new { resetToken = token }, "Đã gửi token reset mật khẩu."));
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest req)
    {
        await _authService.ResetPasswordAsync(req);
        return Ok(ApiResponse<object>.SuccessResponse(null, "Đặt lại mật khẩu thành công."));
    }

    [HttpGet("profile")]
    [Authorize]
    public async Task<IActionResult> GetProfile()
    {
        var userId = JwtHelper.GetUserId(User);
        var profile = await _authService.GetProfileAsync(userId);
        return Ok(ApiResponse<ProfileDto>.SuccessResponse(profile));
    }

    [HttpPut("profile")]
    [Authorize]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest req)
    {
        var userId = JwtHelper.GetUserId(User);
        await _authService.UpdateProfileAsync(userId, req);
        return Ok(ApiResponse<object>.SuccessResponse(null, "Cập nhật thông tin thành công."));
    }
}
