using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TanHuyComputer.API.DTOs;
using TanHuyComputer.API.Helpers;
using TanHuyComputer.API.Services;

namespace TanHuyComputer.API.Controllers;

[ApiController]
[Route("api/categories")]
public class CategoriesController : ControllerBase
{
    private readonly ICategoryService _catService;
    private readonly IProductService _productService;
    public CategoriesController(ICategoryService catService, IProductService productService)
    {
        _catService = catService;
        _productService = productService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var cats = await _catService.GetAllAsync();
        return Ok(ApiResponse<List<CategoryDto>>.SuccessResponse(cats));
    }

    [HttpGet("{slug}/products")]
    public async Task<IActionResult> GetProductsByCategory(string slug, [FromQuery] ProductQueryParams query)
    {
        query.CategorySlug = slug;
        var (items, total) = await _productService.GetProductsAsync(query);
        return Ok(ApiResponse<List<ProductListDto>>.SuccessResponse(items, "Thành công",
            new PaginationInfo { Page = query.Page, PageSize = query.PageSize, Total = total }));
    }

    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Create([FromBody] CreateCategoryRequest req)
    {
        var id = await _catService.CreateAsync(req);
        return Ok(ApiResponse<object>.SuccessResponse(new { categoryId = id }, "Thêm danh mục thành công."));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateCategoryRequest req)
    {
        await _catService.UpdateAsync(id, req);
        return Ok(ApiResponse<object>.SuccessResponse(null, "Cập nhật danh mục thành công."));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Delete(int id)
    {
        await _catService.DeleteAsync(id);
        return Ok(ApiResponse<object>.SuccessResponse(null, "Xóa danh mục thành công."));
    }
}

[ApiController]
[Route("api/brands")]
public class BrandsController : ControllerBase
{
    private readonly IBrandService _service;
    public BrandsController(IBrandService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var brands = await _service.GetAllAsync();
        return Ok(ApiResponse<List<BrandDto>>.SuccessResponse(brands));
    }

    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Create([FromBody] CreateBrandRequest req)
    {
        var id = await _service.CreateAsync(req);
        return Ok(ApiResponse<object>.SuccessResponse(new { brandId = id }, "Thêm thương hiệu thành công."));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateBrandRequest req)
    {
        await _service.UpdateAsync(id, req);
        return Ok(ApiResponse<object>.SuccessResponse(null, "Cập nhật thương hiệu thành công."));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return Ok(ApiResponse<object>.SuccessResponse(null, "Xóa thương hiệu thành công."));
    }
}
