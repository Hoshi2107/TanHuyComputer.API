using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TanHuyComputer.API.DTOs;
using TanHuyComputer.API.Helpers;
using TanHuyComputer.API.Models;
using TanHuyComputer.API.Repositories;
using TanHuyComputer.API.Services;

namespace TanHuyComputer.API.Controllers;

// ===== ADMIN DASHBOARD =====
[ApiController]
[Route("api/admin")]
[Authorize(Roles = "admin")]
public class AdminController : ControllerBase
{
    private readonly IAdminService _service;
    public AdminController(IAdminService service) => _service = service;

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        var data = await _service.GetDashboardAsync();
        return Ok(ApiResponse<DashboardDto>.SuccessResponse(data));
    }

    [HttpGet("revenue/daily")]
    public async Task<IActionResult> GetDailyRevenue([FromQuery] int days = 7)
    {
        var data = await _service.GetDailyRevenueAsync(days);
        return Ok(ApiResponse<List<DailyRevenueDto>>.SuccessResponse(data));
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null)
    {
        var (items, total) = await _service.GetUsersAsync(page, pageSize, search);
        return Ok(ApiResponse<List<AdminUserDto>>.SuccessResponse(items, "Thành công",
            new PaginationInfo { Page = page, PageSize = pageSize, Total = total }));
    }

    [HttpPut("users/{id}/status")]
    public async Task<IActionResult> SetUserStatus(int id, [FromBody] SetUserStatusRequest req)
    {
        await _service.SetUserStatusAsync(id, req.IsActive);
        return Ok(ApiResponse<object>.SuccessResponse(null,
            req.IsActive ? "Đã mở khóa tài khoản." : "Đã khóa tài khoản."));
    }
}

public class SetUserStatusRequest
{
    public bool IsActive { get; set; }
}

// ===== BANNERS, SETTINGS, ABOUT, CONTACT =====
[ApiController]
public class BannersController : ControllerBase
{
    private readonly IBannerService _service;
    private readonly IBannerRepository _bannerRepo;

    public BannersController(IBannerService service, IBannerRepository bannerRepo)
    {
        _service = service;
        _bannerRepo = bannerRepo;
    }

    [HttpGet("api/banners")]
    public async Task<IActionResult> GetBanners()
    {
        var banners = await _service.GetActiveBannersAsync();
        return Ok(ApiResponse<List<Banner>>.SuccessResponse(banners));
    }

    [HttpGet("api/settings")]
    public async Task<IActionResult> GetSettings()
    {
        var settings = await _service.GetSettingsAsync();
        return Ok(ApiResponse<Dictionary<string, string?>>.SuccessResponse(settings));
    }

    [HttpGet("api/about")]
    public async Task<IActionResult> GetAbout()
    {
        var about = await _service.GetAboutAsync();
        return Ok(ApiResponse<AboutUs?>.SuccessResponse(about));
    }

    [HttpPost("api/contact")]
    public async Task<IActionResult> CreateContact([FromBody] ContactRequest req)
    {
        var id = await _service.CreateContactRequestAsync(req);
        return Ok(ApiResponse<object>.SuccessResponse(new { requestId = id }, "Gửi liên hệ thành công. Chúng tôi sẽ phản hồi sớm nhất!"));
    }

    // Admin banner management
    [HttpGet("api/admin/banners")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetAllBanners()
    {
        var banners = await _bannerRepo.GetAllBannersAsync();
        return Ok(ApiResponse<List<Banner>>.SuccessResponse(banners));
    }

    [HttpPost("api/admin/banners")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> CreateBanner([FromBody] Banner banner)
    {
        var id = await _bannerRepo.CreateBannerAsync(banner);
        return Ok(ApiResponse<object>.SuccessResponse(new { bannerId = id }, "Thêm banner thành công."));
    }

    [HttpPut("api/admin/banners/{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> UpdateBanner(int id, [FromBody] Banner banner)
    {
        banner.BannerId = id;
        await _bannerRepo.UpdateBannerAsync(banner);
        return Ok(ApiResponse<object>.SuccessResponse(null, "Cập nhật banner thành công."));
    }

    [HttpDelete("api/admin/banners/{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> DeleteBanner(int id)
    {
        await _bannerRepo.DeleteBannerAsync(id);
        return Ok(ApiResponse<object>.SuccessResponse(null, "Xóa banner thành công."));
    }
}
