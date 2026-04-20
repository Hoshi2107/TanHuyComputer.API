using TanHuyComputer.API.DTOs;
using TanHuyComputer.API.Repositories;

namespace TanHuyComputer.API.Services;

public interface IProductService
{
    Task<(List<ProductListDto> Items, int Total)> GetProductsAsync(ProductQueryParams query);
    Task<ProductDetailDto?> GetBySlugAsync(string slug);
    Task<List<ProductListDto>> GetFeaturedAsync(int count = 8);
    Task<List<ProductListDto>> GetTopSellingAsync(int count = 8);
    Task<int> CreateAsync(CreateProductRequest req);
    Task UpdateAsync(int productId, UpdateProductRequest req);
    Task DeleteAsync(int productId);
}

public class ProductService : IProductService
{
    private readonly IProductRepository _repo;
    public ProductService(IProductRepository repo) => _repo = repo;

    public Task<(List<ProductListDto> Items, int Total)> GetProductsAsync(ProductQueryParams query)
        => _repo.GetProductsAsync(query);

    public Task<ProductDetailDto?> GetBySlugAsync(string slug)
        => _repo.GetBySlugAsync(slug);

    public Task<List<ProductListDto>> GetFeaturedAsync(int count = 8)
        => _repo.GetFeaturedAsync(count);

    public Task<List<ProductListDto>> GetTopSellingAsync(int count = 8)
        => _repo.GetTopSellingAsync(count);

    public async Task<int> CreateAsync(CreateProductRequest req)
    {
        if (await _repo.SlugExistsAsync(req.Slug))
            throw new InvalidOperationException($"Slug '{req.Slug}' đã tồn tại.");
        return await _repo.CreateAsync(req);
    }

    public async Task UpdateAsync(int productId, UpdateProductRequest req)
    {
        if (await _repo.SlugExistsAsync(req.Slug, productId))
            throw new InvalidOperationException($"Slug '{req.Slug}' đã tồn tại.");
        await _repo.UpdateAsync(productId, req);
        if (req.ImageUrls.Any())
            await _repo.AddImagesAsync(productId, req.ImageUrls);
    }

    public Task DeleteAsync(int productId) => _repo.SoftDeleteAsync(productId);
}

public interface ICategoryService
{
    Task<List<CategoryDto>> GetAllAsync();
    Task<int> CreateAsync(CreateCategoryRequest req);
    Task UpdateAsync(int id, CreateCategoryRequest req);
    Task DeleteAsync(int id);
}

public class CategoryService : ICategoryService
{
    private readonly ICategoryRepository _repo;
    public CategoryService(ICategoryRepository repo) => _repo = repo;

    public async Task<List<CategoryDto>> GetAllAsync()
    {
        var cats = await _repo.GetAllAsync();
        return MapToDto(cats);
    }

    private List<CategoryDto> MapToDto(List<Models.Category> cats)
    {
        return cats.Select(c => new CategoryDto
        {
            CategoryId = c.CategoryId,
            CategoryName = c.CategoryName,
            Slug = c.Slug,
            ParentId = c.ParentId,
            SortOrder = c.SortOrder,
            IsActive = c.IsActive,
            Children = MapToDto(c.Children)
        }).ToList();
    }

    public Task<int> CreateAsync(CreateCategoryRequest req) => _repo.CreateAsync(req);
    public Task UpdateAsync(int id, CreateCategoryRequest req) => _repo.UpdateAsync(id, req);
    public Task DeleteAsync(int id) => _repo.DeleteAsync(id);
}

public interface IBrandService
{
    Task<List<BrandDto>> GetAllAsync();
    Task<int> CreateAsync(CreateBrandRequest req);
    Task UpdateAsync(int id, CreateBrandRequest req);
    Task DeleteAsync(int id);
}

public class BrandService : IBrandService
{
    private readonly IBrandRepository _repo;
    public BrandService(IBrandRepository repo) => _repo = repo;
    public Task<List<BrandDto>> GetAllAsync() => _repo.GetAllAsync();
    public Task<int> CreateAsync(CreateBrandRequest req) => _repo.CreateAsync(req);
    public Task UpdateAsync(int id, CreateBrandRequest req) => _repo.UpdateAsync(id, req);
    public Task DeleteAsync(int id) => _repo.DeleteAsync(id);
}
