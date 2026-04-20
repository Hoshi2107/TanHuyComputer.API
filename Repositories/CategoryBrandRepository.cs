using Dapper;
using Microsoft.Data.SqlClient;
using TanHuyComputer.API.DTOs;
using TanHuyComputer.API.Models;

namespace TanHuyComputer.API.Repositories;

// ===== CATEGORY REPOSITORY =====
public interface ICategoryRepository
{
    Task<List<Category>> GetAllAsync();
    Task<Category?> GetBySlugAsync(string slug);
    Task<int> CreateAsync(CreateCategoryRequest req);
    Task UpdateAsync(int id, CreateCategoryRequest req);
    Task DeleteAsync(int id);
}

public class CategoryRepository : ICategoryRepository
{
    private readonly string _conn;
    public CategoryRepository(IConfiguration config) => _conn = config.GetConnectionString("DefaultConnection")!;
    private SqlConnection Conn() => new SqlConnection(_conn);

    public async Task<List<Category>> GetAllAsync()
    {
        using var conn = Conn();
        var cats = (await conn.QueryAsync<Category>(
            @"SELECT category_id AS CategoryId, category_name AS CategoryName, slug AS Slug,
                     parent_id AS ParentId, sort_order AS SortOrder, is_active AS IsActive
              FROM Categories ORDER BY sort_order")).ToList();

        var lookup = cats.ToDictionary(c => c.CategoryId);
        var roots = new List<Category>();
        foreach (var cat in cats)
        {
            if (cat.ParentId.HasValue && lookup.TryGetValue(cat.ParentId.Value, out var parent))
                parent.Children.Add(cat);
            else
                roots.Add(cat);
        }
        return roots;
    }

    public async Task<Category?> GetBySlugAsync(string slug)
    {
        using var conn = Conn();
        return await conn.QueryFirstOrDefaultAsync<Category>(
            @"SELECT category_id AS CategoryId, category_name AS CategoryName, slug AS Slug,
                     parent_id AS ParentId, sort_order AS SortOrder, is_active AS IsActive
              FROM Categories WHERE slug=@Slug", new { Slug = slug });
    }

    public async Task<int> CreateAsync(CreateCategoryRequest req)
    {
        using var conn = Conn();
        return await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO Categories (category_name, slug, parent_id, sort_order, is_active)
              VALUES (@CategoryName, @Slug, @ParentId, @SortOrder, @IsActive);
              SELECT SCOPE_IDENTITY();", req);
    }

    public async Task UpdateAsync(int id, CreateCategoryRequest req)
    {
        using var conn = Conn();
        await conn.ExecuteAsync(
            @"UPDATE Categories SET category_name=@CategoryName, slug=@Slug, parent_id=@ParentId,
                                    sort_order=@SortOrder, is_active=@IsActive WHERE category_id=@Id",
            new { req.CategoryName, req.Slug, req.ParentId, req.SortOrder, req.IsActive, Id = id });
    }

    public async Task DeleteAsync(int id)
    {
        using var conn = Conn();
        await conn.ExecuteAsync("DELETE FROM Categories WHERE category_id=@Id", new { Id = id });
    }
}

// ===== BRAND REPOSITORY =====
public interface IBrandRepository
{
    Task<List<BrandDto>> GetAllAsync();
    Task<int> CreateAsync(CreateBrandRequest req);
    Task UpdateAsync(int id, CreateBrandRequest req);
    Task DeleteAsync(int id);
}

public class BrandRepository : IBrandRepository
{
    private readonly string _conn;
    public BrandRepository(IConfiguration config) => _conn = config.GetConnectionString("DefaultConnection")!;
    private SqlConnection Conn() => new SqlConnection(_conn);

    public async Task<List<BrandDto>> GetAllAsync()
    {
        using var conn = Conn();
        return (await conn.QueryAsync<BrandDto>(
            @"SELECT brand_id AS BrandId, brand_name AS BrandName, slug AS Slug,
                     logo_url AS LogoUrl, is_active AS IsActive FROM Brands WHERE is_active=1 ORDER BY brand_name")).ToList();
    }

    public async Task<int> CreateAsync(CreateBrandRequest req)
    {
        using var conn = Conn();
        return await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO Brands (brand_name, slug, logo_url, is_active)
              VALUES (@BrandName, @Slug, @LogoUrl, @IsActive); SELECT SCOPE_IDENTITY();", req);
    }

    public async Task UpdateAsync(int id, CreateBrandRequest req)
    {
        using var conn = Conn();
        await conn.ExecuteAsync(
            "UPDATE Brands SET brand_name=@BrandName, slug=@Slug, logo_url=@LogoUrl, is_active=@IsActive WHERE brand_id=@Id",
            new { req.BrandName, req.Slug, req.LogoUrl, req.IsActive, Id = id });
    }

    public async Task DeleteAsync(int id)
    {
        using var conn = Conn();
        await conn.ExecuteAsync("UPDATE Brands SET is_active=0 WHERE brand_id=@Id", new { Id = id });
    }
}
