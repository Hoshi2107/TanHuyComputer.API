using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TanHuyComputer.API.DTOs;
using TanHuyComputer.API.Helpers;
using TanHuyComputer.API.Services;

namespace TanHuyComputer.API.Controllers;

[ApiController]
[Route("api/cart")]
public class CartController : ControllerBase
{
    private readonly ICartService _service;
    public CartController(ICartService service) => _service = service;

    private (int? userId, string? sessionId) GetIdentity()
    {
        if (User.Identity?.IsAuthenticated == true)
            return (JwtHelper.GetUserId(User), null);
        var sessionId = Request.Headers["X-Session-Id"].FirstOrDefault() ?? Request.Query["sessionId"].FirstOrDefault();
        return (null, sessionId);
    }

    [HttpGet]
    public async Task<IActionResult> GetCart()
    {
        var (userId, sessionId) = GetIdentity();
        var cart = await _service.GetCartAsync(userId, sessionId);
        return Ok(ApiResponse<CartDto>.SuccessResponse(cart!));
    }

    [HttpPost("add")]
    public async Task<IActionResult> AddToCart([FromBody] AddToCartRequest req)
    {
        var (userId, _) = GetIdentity();
        await _service.AddToCartAsync(userId, req);
        return Ok(ApiResponse<object>.SuccessResponse(null, "Đã thêm vào giỏ hàng."));
    }

    [HttpPut("update")]
    public async Task<IActionResult> UpdateCart([FromBody] UpdateCartRequest req)
    {
        var (userId, _) = GetIdentity();
        await _service.UpdateCartAsync(userId, req);
        return Ok(ApiResponse<object>.SuccessResponse(null, "Đã cập nhật giỏ hàng."));
    }

    [HttpDelete("remove/{productId}")]
    public async Task<IActionResult> RemoveFromCart(int productId)
    {
        var (userId, sessionId) = GetIdentity();
        await _service.RemoveFromCartAsync(userId, sessionId, productId);
        return Ok(ApiResponse<object>.SuccessResponse(null, "Đã xóa sản phẩm khỏi giỏ hàng."));
    }

    [HttpDelete("clear")]
    public async Task<IActionResult> ClearCart()
    {
        var (userId, sessionId) = GetIdentity();
        await _service.ClearCartAsync(userId, sessionId);
        return Ok(ApiResponse<object>.SuccessResponse(null, "Đã xóa toàn bộ giỏ hàng."));
    }

    [HttpPost("merge")]
    [Authorize]
    public async Task<IActionResult> MergeCart([FromBody] MergeCartRequest req)
    {
        var userId = JwtHelper.GetUserId(User);
        if (string.IsNullOrEmpty(req.SessionId))
            return Ok(ApiResponse<object>.SuccessResponse(null, "Không có giỏ hàng guest để gộp."));
        await _service.MergeCartAsync(req.SessionId, userId);
        return Ok(ApiResponse<object>.SuccessResponse(null, "Đã gộp giỏ hàng thành công."));
    }
}
