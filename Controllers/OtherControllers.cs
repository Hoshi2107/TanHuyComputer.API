using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TanHuyComputer.API.DTOs;
using TanHuyComputer.API.Helpers;
using TanHuyComputer.API.Models;
using TanHuyComputer.API.Services;

namespace TanHuyComputer.API.Controllers;

// ===== REVIEWS =====
[ApiController]
public class ReviewsController : ControllerBase
{
    private readonly IReviewService _service;
    public ReviewsController(IReviewService service) => _service = service;

    [HttpGet("api/products/{productId}/reviews")]
    public async Task<IActionResult> GetProductReviews(int productId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var (items, total) = await _service.GetByProductAsync(productId, page, pageSize);
        return Ok(ApiResponse<List<ReviewDto>>.SuccessResponse(items, "Thành công",
            new PaginationInfo { Page = page, PageSize = pageSize, Total = total }));
    }

    [HttpPost("api/reviews")]
    [Authorize]
    public async Task<IActionResult> CreateReview([FromBody] CreateReviewRequest req)
    {
        var userId = JwtHelper.GetUserId(User);
        var id = await _service.CreateAsync(req, userId);
        return Ok(ApiResponse<object>.SuccessResponse(new { reviewId = id }, "Đánh giá thành công."));
    }

    [HttpGet("api/admin/reviews")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetAllReviews(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 15,
        [FromQuery] string? status = null,
        [FromQuery] int? productId = null)
    {
        var (items, total) = await _service.GetAllAsync(page, pageSize, status, productId);
        return Ok(ApiResponse<List<ReviewDto>>.SuccessResponse(items, "Thành công",
            new PaginationInfo { Page = page, PageSize = pageSize, Total = total }));
    }

    [HttpPut("api/admin/reviews/{id}/approve")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> ApproveReview(int id)
    {
        await _service.ApproveAsync(id);
        return Ok(ApiResponse<object>.SuccessResponse(null, "Duyệt đánh giá thành công."));
    }

    [HttpDelete("api/admin/reviews/{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> DeleteReview(int id)
    {
        await _service.DeleteAsync(id);
        return Ok(ApiResponse<object>.SuccessResponse(null, "Xóa đánh giá thành công."));
    }
}

// ===== COUPONS =====
[ApiController]
public class CouponsController : ControllerBase
{
    private readonly ICouponService _service;
    public CouponsController(ICouponService service) => _service = service;

    [HttpPost("api/coupons/validate")]
    public async Task<IActionResult> Validate([FromBody] ValidateCouponRequest req)
    {
        var result = await _service.ValidateAsync(req);
        return Ok(ApiResponse<CouponValidateResult>.SuccessResponse(result));
    }

    [HttpGet("api/coupons")]
    public async Task<IActionResult> GetActiveCoupons()
    {
        var (items, _) = await _service.GetAllAsync(1, 100);
        var activeItems = items.Where(c => c.IsActive && c.EndDate >= DateTime.Now).ToList();
        return Ok(ApiResponse<List<CouponDto>>.SuccessResponse(activeItems, "Thành công"));
    }

    [HttpGet("api/admin/coupons")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var (items, total) = await _service.GetAllAsync(page, pageSize);
        return Ok(ApiResponse<List<CouponDto>>.SuccessResponse(items, "Thành công",
            new PaginationInfo { Page = page, PageSize = pageSize, Total = total }));
    }

    [HttpPost("api/admin/coupons")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Create([FromBody] CreateCouponRequest req)
    {
        var adminId = JwtHelper.GetUserId(User);
        var id = await _service.CreateAsync(req, adminId);
        return Ok(ApiResponse<object>.SuccessResponse(new { couponId = id }, "Tạo mã giảm giá thành công."));
    }

    [HttpPut("api/admin/coupons/{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateCouponRequest req)
    {
        await _service.UpdateAsync(id, req);
        return Ok(ApiResponse<object>.SuccessResponse(null, "Cập nhật mã giảm giá thành công."));
    }
}

// ===== WISHLIST =====
[ApiController]
[Route("api/wishlist")]
[Authorize]
public class WishlistController : ControllerBase
{
    private readonly IWishlistService _service;
    public WishlistController(IWishlistService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetWishlist()
    {
        var userId = JwtHelper.GetUserId(User);
        var items = await _service.GetByUserAsync(userId);
        return Ok(ApiResponse<List<Wishlist>>.SuccessResponse(items));
    }

    [HttpPost("{productId}")]
    public async Task<IActionResult> AddToWishlist(int productId)
    {
        var userId = JwtHelper.GetUserId(User);
        await _service.AddAsync(userId, productId);
        return Ok(ApiResponse<object>.SuccessResponse(null, "Đã thêm vào yêu thích."));
    }

    [HttpDelete("{productId}")]
    public async Task<IActionResult> RemoveFromWishlist(int productId)
    {
        var userId = JwtHelper.GetUserId(User);
        await _service.RemoveAsync(userId, productId);
        return Ok(ApiResponse<object>.SuccessResponse(null, "Đã xóa khỏi yêu thích."));
    }
}

// ===== ADDRESSES =====
[ApiController]
[Route("api/addresses")]
[Authorize]
public class AddressesController : ControllerBase
{
    private readonly IAddressService _service;
    public AddressesController(IAddressService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAddresses()
    {
        var userId = JwtHelper.GetUserId(User);
        var addresses = await _service.GetByUserAsync(userId);
        return Ok(ApiResponse<List<AddressDto>>.SuccessResponse(addresses));
    }

    [HttpPost]
    public async Task<IActionResult> CreateAddress([FromBody] CreateAddressRequest req)
    {
        var userId = JwtHelper.GetUserId(User);
        var id = await _service.CreateAsync(userId, req);
        return Ok(ApiResponse<object>.SuccessResponse(new { addressId = id }, "Thêm địa chỉ thành công."));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateAddress(int id, [FromBody] CreateAddressRequest req)
    {
        var userId = JwtHelper.GetUserId(User);
        await _service.UpdateAsync(id, userId, req);
        return Ok(ApiResponse<object>.SuccessResponse(null, "Cập nhật địa chỉ thành công."));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAddress(int id)
    {
        var userId = JwtHelper.GetUserId(User);
        await _service.DeleteAsync(id, userId);
        return Ok(ApiResponse<object>.SuccessResponse(null, "Xóa địa chỉ thành công."));
    }

    [HttpPut("{id}/default")]
    public async Task<IActionResult> SetDefault(int id)
    {
        var userId = JwtHelper.GetUserId(User);
        await _service.SetDefaultAsync(id, userId);
        return Ok(ApiResponse<object>.SuccessResponse(null, "Đã đặt làm địa chỉ mặc định."));
    }
}
