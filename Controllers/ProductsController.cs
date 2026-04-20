using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TanHuyComputer.API.DTOs;
using TanHuyComputer.API.Helpers;
using TanHuyComputer.API.Services;

namespace TanHuyComputer.API.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _service;
    public ProductsController(IProductService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetProducts([FromQuery] ProductQueryParams query)
    {
        var (items, total) = await _service.GetProductsAsync(query);
        return Ok(ApiResponse<List<ProductListDto>>.SuccessResponse(items, "Thành công",
            new PaginationInfo { Page = query.Page, PageSize = query.PageSize, Total = total }));
    }

    [HttpGet("featured")]
    public async Task<IActionResult> GetFeatured([FromQuery] int count = 8)
    {
        var items = await _service.GetFeaturedAsync(count);
        return Ok(ApiResponse<List<ProductListDto>>.SuccessResponse(items));
    }

    [HttpGet("top-selling")]
    public async Task<IActionResult> GetTopSelling([FromQuery] int count = 8)
    {
        var items = await _service.GetTopSellingAsync(count);
        return Ok(ApiResponse<List<ProductListDto>>.SuccessResponse(items));
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> GetBySlug(string slug)
    {
        var product = await _service.GetBySlugAsync(slug)
            ?? throw new KeyNotFoundException("Sản phẩm không tồn tại.");
        return Ok(ApiResponse<ProductDetailDto>.SuccessResponse(product));
    }

    [HttpPost]
    [Authorize(Roles = "admin,staff")]
    public async Task<IActionResult> Create([FromBody] CreateProductRequest req)
    {
        var id = await _service.CreateAsync(req);
        return Ok(ApiResponse<object>.SuccessResponse(new { productId = id }, "Thêm sản phẩm thành công."));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "admin,staff")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateProductRequest req)
    {
        await _service.UpdateAsync(id, req);
        return Ok(ApiResponse<object>.SuccessResponse(null, "Cập nhật sản phẩm thành công."));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return Ok(ApiResponse<object>.SuccessResponse(null, "Xóa sản phẩm thành công."));
    }
}
