using Dapper;
using Microsoft.Data.SqlClient;
using TanHuyComputer.API.Models;

namespace TanHuyComputer.API.Repositories;

public interface IAuthRepository
{
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByIdAsync(int userId);
    Task<int> CreateUserAsync(User user, string roleName = "customer");
    Task UpdateUserAsync(User user);
    Task<string?> GetRoleNameByIdAsync(int roleId);
    Task<int?> GetRoleIdByNameAsync(string roleName);
    Task SaveResetTokenAsync(int userId, string token, DateTime expiry);
    Task<User?> GetByResetTokenAsync(string token);
    Task UpdatePasswordAsync(int userId, string passwordHash);
    Task ClearResetTokenAsync(int userId);
}

public class AuthRepository : IAuthRepository
{
    private readonly string _connectionString;
    public AuthRepository(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("DefaultConnection")!;
    }
    private SqlConnection CreateConnection() => new SqlConnection(_connectionString);

    public async Task<User?> GetByEmailAsync(string email)
    {
        using var conn = CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<User>(
            @"SELECT u.*, r.role_name AS RoleName 
              FROM Users u LEFT JOIN Roles r ON u.role_id = r.role_id 
              WHERE u.email = @Email",
            new { Email = email });
    }

    public async Task<User?> GetByIdAsync(int userId)
    {
        using var conn = CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<User>(
            @"SELECT u.*, r.role_name AS RoleName 
              FROM Users u LEFT JOIN Roles r ON u.role_id = r.role_id 
              WHERE u.user_id = @UserId",
            new { UserId = userId });
    }

    public async Task<int> CreateUserAsync(User user, string roleName = "customer")
    {
        using var conn = CreateConnection();
        var roleId = await GetRoleIdByNameAsync(roleName) ?? 3;
        return await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO Users (full_name, email, password_hash, phone, role_id, is_active, email_verified, created_at, updated_at)
              VALUES (@FullName, @Email, @PasswordHash, @Phone, @RoleId, 1, 0, GETDATE(), GETDATE());
              SELECT SCOPE_IDENTITY();",
            new { user.FullName, user.Email, user.PasswordHash, user.Phone, RoleId = roleId });
    }

    public async Task UpdateUserAsync(User user)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync(
            @"UPDATE Users SET full_name=@FullName, phone=@Phone, avatar_url=@AvatarUrl, updated_at=GETDATE()
              WHERE user_id=@UserId",
            new { user.FullName, user.Phone, user.AvatarUrl, user.UserId });
    }

    public async Task<string?> GetRoleNameByIdAsync(int roleId)
    {
        using var conn = CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<string>(
            "SELECT role_name FROM Roles WHERE role_id = @RoleId", new { RoleId = roleId });
    }

    public async Task<int?> GetRoleIdByNameAsync(string roleName)
    {
        using var conn = CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<int?>(
            "SELECT role_id FROM Roles WHERE role_name = @RoleName", new { RoleName = roleName });
    }

    public async Task SaveResetTokenAsync(int userId, string token, DateTime expiry)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE Users SET reset_token=@Token, reset_token_exp=@Expiry WHERE user_id=@UserId",
            new { Token = token, Expiry = expiry, UserId = userId });
    }

    public async Task<User?> GetByResetTokenAsync(string token)
    {
        using var conn = CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<User>(
            "SELECT * FROM Users WHERE reset_token=@Token AND reset_token_exp > GETDATE()",
            new { Token = token });
    }

    public async Task UpdatePasswordAsync(int userId, string passwordHash)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE Users SET password_hash=@Hash, updated_at=GETDATE() WHERE user_id=@UserId",
            new { Hash = passwordHash, UserId = userId });
    }

    public async Task ClearResetTokenAsync(int userId)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE Users SET reset_token=NULL, reset_token_exp=NULL WHERE user_id=@UserId",
            new { UserId = userId });
    }
}
