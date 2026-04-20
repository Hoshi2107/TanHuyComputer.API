using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TanHuyComputer.API.DTOs;
using TanHuyComputer.API.Helpers;
using TanHuyComputer.API.Services;

namespace TanHuyComputer.API.Controllers;

[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _service;
    public OrdersController(IOrderService service) => _service = service;

    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest req)
    {
        int? userId = User.Identity?.IsAuthenticated == true ? JwtHelper.GetUserId(User) : null;
        var orderCode = await _service.CreateOrderAsync(userId, req);
        return Ok(ApiResponse<object>.SuccessResponse(new { orderCode }, "Đặt hàng thành công!"));
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetMyOrders([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var userId = JwtHelper.GetUserId(User);
        var (items, total) = await _service.GetUserOrdersAsync(userId, page, pageSize);
        return Ok(ApiResponse<List<OrderDto>>.SuccessResponse(items, "Thành công",
            new PaginationInfo { Page = page, PageSize = pageSize, Total = total }));
    }

    [HttpGet("{orderCode}")]
    public async Task<IActionResult> GetOrder(string orderCode)
    {
        int? userId = User.Identity?.IsAuthenticated == true ? JwtHelper.GetUserId(User) : null;
        var order = await _service.GetByCodeAsync(orderCode, userId)
            ?? throw new KeyNotFoundException("Không tìm thấy đơn hàng.");
        return Ok(ApiResponse<OrderDetailDto>.SuccessResponse(order));
    }

    [HttpPut("{id}/cancel")]
    [Authorize]
    public async Task<IActionResult> CancelOrder(int id)
    {
        var userId = JwtHelper.GetUserId(User);
        await _service.CancelOrderAsync(id, userId);
        return Ok(ApiResponse<object>.SuccessResponse(null, "Đã hủy đơn hàng."));
    }
}

[ApiController]
[Route("api/admin/orders")]
[Authorize(Roles = "admin,staff")]
public class AdminOrdersController : ControllerBase
{
    private readonly IOrderService _service;
    public AdminOrdersController(IOrderService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAllOrders([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? status = null)
    {
        var (items, total) = await _service.GetAllOrdersAsync(page, pageSize, status);
        return Ok(ApiResponse<List<OrderDto>>.SuccessResponse(items, "Thành công",
            new PaginationInfo { Page = page, PageSize = pageSize, Total = total }));
    }

    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateOrderStatusRequest req)
    {
        var adminId = JwtHelper.GetUserId(User);
        await _service.UpdateStatusAsync(id, req.NewStatus, adminId, req.Note);
        return Ok(ApiResponse<object>.SuccessResponse(null, "Cập nhật trạng thái thành công."));
    }
}
