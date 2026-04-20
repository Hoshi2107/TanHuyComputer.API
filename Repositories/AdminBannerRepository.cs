using Dapper;
using Microsoft.Data.SqlClient;
using TanHuyComputer.API.DTOs;
using TanHuyComputer.API.Models;

namespace TanHuyComputer.API.Repositories;

// ===== ADMIN REPOSITORY =====
public interface IAdminRepository
{
    Task<DashboardDto> GetDashboardAsync();
    Task<List<DailyRevenueDto>> GetDailyRevenueAsync(int days);
    Task<(List<AdminUserDto> Items, int Total)> GetUsersAsync(int page, int pageSize, string? search);
    Task SetUserStatusAsync(int userId, bool isActive);
}

public class AdminRepository : IAdminRepository
{
    private readonly string _conn;
    public AdminRepository(IConfiguration config) => _conn = config.GetConnectionString("DefaultConnection")!;
    private SqlConnection Conn() => new SqlConnection(_conn);

    public async Task<DashboardDto> GetDashboardAsync()
    {
        using var conn = Conn();
        var result = await conn.QueryFirstOrDefaultAsync<DashboardDto>(@"
            SELECT
                ISNULL((SELECT SUM(total_amount) FROM Orders WHERE order_status='Hoàn thành'), 0) AS TotalRevenue,
                (SELECT COUNT(*) FROM Orders) AS TotalOrders,
                (SELECT COUNT(*) FROM Products WHERE is_active=1) AS TotalProducts,
                (SELECT COUNT(*) FROM Users) AS TotalUsers,
                (SELECT COUNT(*) FROM Orders WHERE order_status IN ('Đặt hàng', 'Xử lý')) AS PendingOrders,
                (SELECT COUNT(*) FROM Products WHERE stock_quantity <= ISNULL(low_stock_alert, 5) AND is_active=1) AS LowStockProducts");
        return result ?? new DashboardDto();
    }

    public async Task<List<DailyRevenueDto>> GetDailyRevenueAsync(int days)
    {
        using var conn = Conn();
        try
        {
            return (await conn.QueryAsync<DailyRevenueDto>(
                @"SELECT TOP (@Days) CAST(date AS DATE) AS Date, revenue AS Revenue, order_count AS OrderCount
                  FROM vw_DailyRevenue ORDER BY date DESC",
                new { Days = days })).ToList();
        }
        catch
        {
            // Fallback nếu view chưa tồn tại
            return (await conn.QueryAsync<DailyRevenueDto>(
                @"SELECT TOP (@Days)
                    CAST(created_at AS DATE) AS Date,
                    SUM(total_amount) AS Revenue,
                    COUNT(*) AS OrderCount
                  FROM Orders
                  WHERE order_status='Hoàn thành'
                  GROUP BY CAST(created_at AS DATE)
                  ORDER BY CAST(created_at AS DATE) DESC",
                new { Days = days })).ToList();
        }
    }

    public async Task<(List<AdminUserDto> Items, int Total)> GetUsersAsync(int page, int pageSize, string? search)
    {
        using var conn = Conn();
        var whereClause = !string.IsNullOrEmpty(search)
            ? "WHERE u.full_name LIKE @Search OR u.email LIKE @Search"
            : "";
        var sql = $@"
            SELECT u.user_id AS UserId, u.full_name AS FullName, u.email, u.phone,
                   r.role_name AS Role, u.is_active AS IsActive, u.created_at AS CreatedAt
            FROM Users u LEFT JOIN Roles r ON u.role_id=r.role_id
            {whereClause}
            ORDER BY u.created_at DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            SELECT COUNT(*) FROM Users u {whereClause};";

        using var multi = await conn.QueryMultipleAsync(sql,
            new { Search = $"%{search}%", Offset = (page - 1) * pageSize, PageSize = pageSize });
        var items = (await multi.ReadAsync<AdminUserDto>()).ToList();
        var total = await multi.ReadFirstAsync<int>();
        return (items, total);
    }

    public async Task SetUserStatusAsync(int userId, bool isActive)
    {
        using var conn = Conn();
        await conn.ExecuteAsync("UPDATE Users SET is_active=@IsActive WHERE user_id=@UserId",
            new { IsActive = isActive, UserId = userId });
    }
}

// ===== BANNER REPOSITORY =====
public interface IBannerRepository
{
    Task<List<Banner>> GetActiveBannersAsync();
    Task<Dictionary<string, string?>> GetSettingsAsync();
    Task<AboutUs?> GetAboutAsync();
    Task<int> CreateContactRequestAsync(ContactRequest req);
    Task<List<Banner>> GetAllBannersAsync();
    Task<int> CreateBannerAsync(Banner banner);
    Task UpdateBannerAsync(Banner banner);
    Task DeleteBannerAsync(int bannerId);
}

public class BannerRepository : IBannerRepository
{
    private readonly string _conn;
    public BannerRepository(IConfiguration config) => _conn = config.GetConnectionString("DefaultConnection")!;
    private SqlConnection Conn() => new SqlConnection(_conn);

    public async Task<List<Banner>> GetActiveBannersAsync()
    {
        using var conn = Conn();
        return (await conn.QueryAsync<Banner>(
            @"SELECT banner_id AS BannerId, title, image_url AS ImageUrl, link_url AS LinkUrl,
                     sort_order AS SortOrder, is_active AS IsActive
              FROM Banners WHERE is_active=1 ORDER BY sort_order")).ToList();
    }

    public async Task<Dictionary<string, string?>> GetSettingsAsync()
    {
        using var conn = Conn();
        var settings = await conn.QueryAsync<SiteSetting>(
            "SELECT setting_key AS SettingKey, setting_value AS SettingValue FROM SiteSettings");
        return settings.ToDictionary(s => s.SettingKey, s => s.SettingValue);
    }

    public async Task<AboutUs?> GetAboutAsync()
    {
        using var conn = Conn();
        return await conn.QueryFirstOrDefaultAsync<AboutUs>(
            "SELECT about_id AS AboutId, content, image_urls AS ImageUrls, updated_at AS UpdatedAt FROM AboutUs");
    }

    public async Task<int> CreateContactRequestAsync(ContactRequest req)
    {
        using var conn = Conn();
        return await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO ContactRequests (full_name, email, phone, message, is_resolved, created_at)
              VALUES (@FullName, @Email, @Phone, @Message, 0, GETDATE()); SELECT SCOPE_IDENTITY();",
            new { req.FullName, req.Email, req.Phone, req.Message });
    }

    public async Task<List<Banner>> GetAllBannersAsync()
    {
        using var conn = Conn();
        return (await conn.QueryAsync<Banner>(
            "SELECT banner_id AS BannerId, title, image_url AS ImageUrl, link_url AS LinkUrl, sort_order AS SortOrder, is_active AS IsActive FROM Banners ORDER BY sort_order")).ToList();
    }

    public async Task<int> CreateBannerAsync(Banner banner)
    {
        using var conn = Conn();
        return await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO Banners (title, image_url, link_url, sort_order, is_active)
              VALUES (@Title, @ImageUrl, @LinkUrl, @SortOrder, @IsActive); SELECT SCOPE_IDENTITY();",
            new { banner.Title, banner.ImageUrl, banner.LinkUrl, banner.SortOrder, banner.IsActive });
    }

    public async Task UpdateBannerAsync(Banner banner)
    {
        using var conn = Conn();
        await conn.ExecuteAsync(
            "UPDATE Banners SET title=@Title, image_url=@ImageUrl, link_url=@LinkUrl, sort_order=@SortOrder, is_active=@IsActive WHERE banner_id=@BannerId",
            new { banner.Title, banner.ImageUrl, banner.LinkUrl, banner.SortOrder, banner.IsActive, banner.BannerId });
    }

    public async Task DeleteBannerAsync(int bannerId)
    {
        using var conn = Conn();
        await conn.ExecuteAsync("DELETE FROM Banners WHERE banner_id=@Id", new { Id = bannerId });
    }
}
