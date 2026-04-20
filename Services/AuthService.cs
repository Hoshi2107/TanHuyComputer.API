using TanHuyComputer.API.DTOs;
using TanHuyComputer.API.Helpers;
using TanHuyComputer.API.Models;
using TanHuyComputer.API.Repositories;

namespace TanHuyComputer.API.Services;

public interface IAuthService
{
    Task<LoginResponse> RegisterAsync(RegisterRequest req);
    Task<LoginResponse> LoginAsync(LoginRequest req);
    Task<ProfileDto> GetProfileAsync(int userId);
    Task UpdateProfileAsync(int userId, UpdateProfileRequest req);
    Task<string> ForgotPasswordAsync(string email);
    Task ResetPasswordAsync(ResetPasswordRequest req);
}

public class AuthService : IAuthService
{
    private readonly IAuthRepository _repo;
    private readonly JwtHelper _jwtHelper;

    public AuthService(IAuthRepository repo, JwtHelper jwtHelper)
    {
        _repo = repo;
        _jwtHelper = jwtHelper;
    }

    public async Task<LoginResponse> RegisterAsync(RegisterRequest req)
    {
        var existing = await _repo.GetByEmailAsync(req.Email);
        if (existing != null)
            throw new InvalidOperationException("Email này đã được đăng ký.");

        var user = new User
        {
            FullName = req.FullName,
            Email = req.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            Phone = req.Phone
        };

        var userId = await _repo.CreateUserAsync(user, "customer");
        var roleName = "customer";
        var token = _jwtHelper.GenerateToken(userId, req.Email, roleName);

        return new LoginResponse
        {
            Token = token,
            UserId = userId,
            FullName = req.FullName,
            Email = req.Email,
            Role = roleName
        };
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest req)
    {
        var user = await _repo.GetByEmailAsync(req.Email);
        if (user == null)
            throw new InvalidOperationException("Email hoặc mật khẩu không đúng.");

        if (!BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            throw new InvalidOperationException("Email hoặc mật khẩu không đúng.");

        if (!user.IsActive)
            throw new InvalidOperationException("Tài khoản của bạn đã bị khóa.");

        var roleName = user.RoleName ?? "customer";
        var token = _jwtHelper.GenerateToken(user.UserId, user.Email, roleName);

        return new LoginResponse
        {
            Token = token,
            UserId = user.UserId,
            FullName = user.FullName,
            Email = user.Email,
            Role = roleName,
            AvatarUrl = user.AvatarUrl
        };
    }

    public async Task<ProfileDto> GetProfileAsync(int userId)
    {
        var user = await _repo.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException("Không tìm thấy người dùng.");

        return new ProfileDto
        {
            UserId = user.UserId,
            FullName = user.FullName,
            Email = user.Email,
            Phone = user.Phone,
            AvatarUrl = user.AvatarUrl,
            Role = user.RoleName ?? "customer",
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt
        };
    }

    public async Task UpdateProfileAsync(int userId, UpdateProfileRequest req)
    {
        var user = await _repo.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException("Không tìm thấy người dùng.");

        if (!string.IsNullOrEmpty(req.NewPassword))
        {
            if (string.IsNullOrEmpty(req.CurrentPassword) || !BCrypt.Net.BCrypt.Verify(req.CurrentPassword, user.PasswordHash))
                throw new InvalidOperationException("Mật khẩu hiện tại không đúng.");
            await _repo.UpdatePasswordAsync(userId, BCrypt.Net.BCrypt.HashPassword(req.NewPassword));
        }

        user.FullName = req.FullName;
        user.Phone = req.Phone;
        user.AvatarUrl = req.AvatarUrl;
        await _repo.UpdateUserAsync(user);
    }

    public async Task<string> ForgotPasswordAsync(string email)
    {
        var user = await _repo.GetByEmailAsync(email);
        if (user == null)
            throw new KeyNotFoundException("Email không tồn tại trong hệ thống.");

        var token = Guid.NewGuid().ToString("N");
        await _repo.SaveResetTokenAsync(user.UserId, token, DateTime.UtcNow.AddHours(1));
        // In production: gửi email với link reset password
        return token; // Trả token về cho dev testing
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest req)
    {
        var user = await _repo.GetByResetTokenAsync(req.Token)
            ?? throw new InvalidOperationException("Token không hợp lệ hoặc đã hết hạn.");

        await _repo.UpdatePasswordAsync(user.UserId, BCrypt.Net.BCrypt.HashPassword(req.NewPassword));
        await _repo.ClearResetTokenAsync(user.UserId);
    }
}
