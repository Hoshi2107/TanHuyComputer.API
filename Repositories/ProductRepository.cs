using Dapper;
using Microsoft.Data.SqlClient;
using TanHuyComputer.API.DTOs;
using TanHuyComputer.API.Models;

namespace TanHuyComputer.API.Repositories;

public interface IProductRepository
{
    Task<(List<ProductListDto> Items, int Total)> GetProductsAsync(ProductQueryParams query);
    Task<ProductDetailDto?> GetBySlugAsync(string slug);
    Task<ProductDetailDto?> GetByIdAsync(int productId);
    Task<List<ProductListDto>> GetFeaturedAsync(int count = 8);
    Task<List<ProductListDto>> GetTopSellingAsync(int count = 8);
    Task<int> CreateAsync(CreateProductRequest request);
    Task UpdateAsync(int productId, UpdateProductRequest request);
    Task SoftDeleteAsync(int productId);
    Task<bool> SlugExistsAsync(string slug, int? excludeId = null);
    Task AddImagesAsync(int productId, List<string> imageUrls);
    Task UpdateStockAsync(int productId, int quantityChange, string changeType, string? note, int? changedBy);
}

public class ProductRepository : IProductRepository
{
    private readonly string _connectionString;
    public ProductRepository(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("DefaultConnection")!;
    }
    private SqlConnection CreateConnection() => new SqlConnection(_connectionString);

    public async Task<(List<ProductListDto> Items, int Total)> GetProductsAsync(ProductQueryParams query)
    {
        using var conn = CreateConnection();
        var where = new List<string> { "p.is_active = 1" };
        var p = new DynamicParameters();

        if (query.CategoryId.HasValue)
        {
            where.Add("p.category_id = @CategoryId");
            p.Add("CategoryId", query.CategoryId);
        }
        if (!string.IsNullOrEmpty(query.CategorySlug))
        {
            where.Add("(c.slug = @CategorySlug OR c.parent_id IN (SELECT category_id FROM Categories WHERE slug = @CategorySlug))");
            p.Add("CategorySlug", query.CategorySlug);
        }
        if (query.BrandId.HasValue)
        {
            where.Add("p.brand_id = @BrandId");
            p.Add("BrandId", query.BrandId);
        }
        if (query.MinPrice.HasValue)
        {
            where.Add("ISNULL(p.sale_price, p.price) >= @MinPrice");
            p.Add("MinPrice", query.MinPrice);
        }
        if (query.MaxPrice.HasValue)
        {
            where.Add("ISNULL(p.sale_price, p.price) <= @MaxPrice");
            p.Add("MaxPrice", query.MaxPrice);
        }
        if (!string.IsNullOrEmpty(query.Search))
        {
            where.Add("p.product_name LIKE @Search");
            p.Add("Search", $"%{query.Search}%");
        }
        if (query.IsFeatured.HasValue)
        {
            where.Add("p.is_featured = @IsFeatured");
            p.Add("IsFeatured", query.IsFeatured);
        }

        var whereClause = string.Join(" AND ", where);
        var orderBy = query.Sort switch
        {
            "price_asc" => "ISNULL(p.sale_price, p.price) ASC",
            "price_desc" => "ISNULL(p.sale_price, p.price) DESC",
            "best_selling" => "p.total_sold DESC",
            _ => "p.created_at DESC"
        };

        p.Add("Offset", (query.Page - 1) * query.PageSize);
        p.Add("PageSize", query.PageSize);

        var sql = $@"
            SELECT p.product_id AS ProductId, p.product_name AS ProductName, p.slug AS Slug,
                   c.category_name AS CategoryName, b.brand_name AS BrandName,
                   p.brand_id AS BrandId, p.category_id AS CategoryId,
                   p.price, p.sale_price AS SalePrice, p.stock_quantity AS StockQuantity,
                   p.thumbnail_url AS ThumbnailUrl, p.avg_rating AS AvgRating,
                   p.total_reviews AS TotalReviews, p.total_sold AS TotalSold,
                   p.is_featured AS IsFeatured, p.is_active AS IsActive, p.created_at AS CreatedAt
            FROM Products p
            LEFT JOIN Categories c ON p.category_id = c.category_id
            LEFT JOIN Brands b ON p.brand_id = b.brand_id
            WHERE {whereClause}
            ORDER BY {orderBy}
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

            SELECT COUNT(*) FROM Products p
            LEFT JOIN Categories c ON p.category_id = c.category_id
            WHERE {whereClause};";

        using var multi = await conn.QueryMultipleAsync(sql, p);
        var items = (await multi.ReadAsync<ProductListDto>()).ToList();
        var total = await multi.ReadFirstAsync<int>();
        return (items, total);
    }

    public async Task<ProductDetailDto?> GetBySlugAsync(string slug)
    {
        using var conn = CreateConnection();
        var product = await conn.QueryFirstOrDefaultAsync<ProductDetailDto>(
            @"SELECT p.product_id AS ProductId, p.product_name AS ProductName, p.slug AS Slug,
                     p.category_id AS CategoryId, c.category_name AS CategoryName,
                     p.brand_id AS BrandId, b.brand_name AS BrandName,
                     p.price, p.sale_price AS SalePrice, p.stock_quantity AS StockQuantity,
                     p.description, p.specifications, p.thumbnail_url AS ThumbnailUrl,
                     p.avg_rating AS AvgRating, p.total_reviews AS TotalReviews, p.total_sold AS TotalSold,
                     p.is_featured AS IsFeatured, p.is_active AS IsActive, p.created_at AS CreatedAt
              FROM Products p
              LEFT JOIN Categories c ON p.category_id = c.category_id
              LEFT JOIN Brands b ON p.brand_id = b.brand_id
              WHERE p.slug = @Slug AND p.is_active = 1",
            new { Slug = slug });

        if (product == null) return null;
        await EnrichProductDetailAsync(conn, product);
        return product;
    }

    public async Task<ProductDetailDto?> GetByIdAsync(int productId)
    {
        using var conn = CreateConnection();
        var product = await conn.QueryFirstOrDefaultAsync<ProductDetailDto>(
            @"SELECT p.product_id AS ProductId, p.product_name AS ProductName, p.slug AS Slug,
                     p.category_id AS CategoryId, c.category_name AS CategoryName,
                     p.brand_id AS BrandId, b.brand_name AS BrandName,
                     p.price, p.sale_price AS SalePrice, p.stock_quantity AS StockQuantity,
                     p.description, p.specifications, p.thumbnail_url AS ThumbnailUrl,
                     p.avg_rating AS AvgRating, p.total_reviews AS TotalReviews, p.total_sold AS TotalSold,
                     p.is_featured AS IsFeatured, p.is_active AS IsActive, p.created_at AS CreatedAt
              FROM Products p
              LEFT JOIN Categories c ON p.category_id = c.category_id
              LEFT JOIN Brands b ON p.brand_id = b.brand_id
              WHERE p.product_id = @ProductId",
            new { ProductId = productId });

        if (product == null) return null;
        await EnrichProductDetailAsync(conn, product);
        return product;
    }

    private async Task EnrichProductDetailAsync(SqlConnection conn, ProductDetailDto product)
    {
        var images = await conn.QueryAsync<ProductImageDto>(
            @"SELECT image_id AS ImageId, image_url AS ImageUrl, alt_text AS AltText, sort_order AS SortOrder
              FROM ProductImages WHERE product_id=@Id ORDER BY sort_order",
            new { Id = product.ProductId });
        product.Images = images.ToList();

        var warranty = await conn.QueryFirstOrDefaultAsync<WarrantyPolicyDto>(
            @"SELECT title AS Title, content AS Content, warranty_months AS WarrantyMonths
              FROM WarrantyPolicies WHERE product_id=@Id",
            new { Id = product.ProductId });
        product.WarrantyPolicy = warranty;

        var returns = await conn.QueryAsync<ReturnPolicyDto>(
            @"SELECT title AS Title, content AS Content, return_days AS ReturnDays, conditions AS Conditions
              FROM ReturnPolicies",
            new { });
        product.ReturnPolicies = returns.ToList();
    }

    public async Task<List<ProductListDto>> GetFeaturedAsync(int count = 8)
    {
        using var conn = CreateConnection();
        var items = await conn.QueryAsync<ProductListDto>(
            @"SELECT TOP (@Count) p.product_id AS ProductId, p.product_name AS ProductName, p.slug AS Slug,
                     c.category_name AS CategoryName, b.brand_name AS BrandName,
                     p.brand_id AS BrandId, p.category_id AS CategoryId,
                     p.price, p.sale_price AS SalePrice, p.stock_quantity AS StockQuantity,
                     p.thumbnail_url AS ThumbnailUrl, p.avg_rating AS AvgRating,
                     p.total_reviews AS TotalReviews, p.total_sold AS TotalSold,
                     p.is_featured AS IsFeatured, p.is_active AS IsActive, p.created_at AS CreatedAt
              FROM Products p
              LEFT JOIN Categories c ON p.category_id = c.category_id
              LEFT JOIN Brands b ON p.brand_id = b.brand_id
              WHERE p.is_featured=1 AND p.is_active=1
              ORDER BY p.created_at DESC",
            new { Count = count });
        return items.ToList();
    }

    public async Task<List<ProductListDto>> GetTopSellingAsync(int count = 8)
    {
        using var conn = CreateConnection();
        try
        {
            var items = await conn.QueryAsync<ProductListDto>(
                @"SELECT TOP (@Count) product_id AS ProductId, product_name AS ProductName, slug AS Slug,
                         category_name AS CategoryName, brand_name AS BrandName, brand_id AS BrandId,
                         category_id AS CategoryId, price, sale_price AS SalePrice,
                         stock_quantity AS StockQuantity, thumbnail_url AS ThumbnailUrl,
                         avg_rating AS AvgRating, total_reviews AS TotalReviews, total_sold AS TotalSold,
                         is_featured AS IsFeatured, is_active AS IsActive, created_at AS CreatedAt
                  FROM vw_TopSellingProducts
                  WHERE is_active = 1",
                new { Count = count });
            return items.ToList();
        }
        catch
        {
            // Fallback nếu view chưa tồn tại
            return await GetFeaturedAsync(count);
        }
    }

    public async Task<int> CreateAsync(CreateProductRequest request)
    {
        using var conn = CreateConnection();
        var productId = await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO Products (product_name, slug, category_id, brand_id, price, sale_price,
                                   stock_quantity, low_stock_alert, description, specifications,
                                   thumbnail_url, is_featured, is_active, avg_rating, total_reviews,
                                   total_sold, created_at, updated_at)
              VALUES (@ProductName, @Slug, @CategoryId, @BrandId, @Price, @SalePrice,
                      @StockQuantity, @LowStockAlert, @Description, @Specifications,
                      @ThumbnailUrl, @IsFeatured, @IsActive, 0, 0, 0, GETDATE(), GETDATE());
              SELECT SCOPE_IDENTITY();",
            request);
        if (request.ImageUrls.Any())
            await AddImagesAsync(productId, request.ImageUrls);
        return productId;
    }

    public async Task UpdateAsync(int productId, UpdateProductRequest request)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync(
            @"UPDATE Products SET product_name=@ProductName, slug=@Slug, category_id=@CategoryId,
                                  brand_id=@BrandId, price=@Price, sale_price=@SalePrice,
                                  stock_quantity=@StockQuantity, low_stock_alert=@LowStockAlert,
                                  description=@Description, specifications=@Specifications,
                                  thumbnail_url=@ThumbnailUrl, is_featured=@IsFeatured,
                                  is_active=@IsActive, updated_at=GETDATE()
              WHERE product_id=@ProductId",
            new { request.ProductName, request.Slug, request.CategoryId, request.BrandId,
                  request.Price, request.SalePrice, request.StockQuantity, request.LowStockAlert,
                  request.Description, request.Specifications, request.ThumbnailUrl,
                  request.IsFeatured, request.IsActive, ProductId = productId });
    }

    public async Task SoftDeleteAsync(int productId)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE Products SET is_active=0, updated_at=GETDATE() WHERE product_id=@ProductId",
            new { ProductId = productId });
    }

    public async Task<bool> SlugExistsAsync(string slug, int? excludeId = null)
    {
        using var conn = CreateConnection();
        var count = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Products WHERE slug=@Slug AND (@ExcludeId IS NULL OR product_id!=@ExcludeId)",
            new { Slug = slug, ExcludeId = excludeId });
        return count > 0;
    }

    public async Task AddImagesAsync(int productId, List<string> imageUrls)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync("DELETE FROM ProductImages WHERE product_id=@ProductId", new { ProductId = productId });
        for (int i = 0; i < imageUrls.Count; i++)
        {
            await conn.ExecuteAsync(
                "INSERT INTO ProductImages (product_id, image_url, sort_order) VALUES (@ProductId, @ImageUrl, @SortOrder)",
                new { ProductId = productId, ImageUrl = imageUrls[i], SortOrder = i });
        }
    }

    public async Task UpdateStockAsync(int productId, int quantityChange, string changeType, string? note, int? changedBy)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync(
            @"UPDATE Products SET stock_quantity = stock_quantity + @Change, updated_at=GETDATE()
              WHERE product_id=@ProductId;
              DECLARE @After INT = (SELECT stock_quantity FROM Products WHERE product_id=@ProductId);
              INSERT INTO InventoryLogs (product_id, change_type, quantity_change, quantity_after, note, created_by, created_at)
              VALUES (@ProductId, @ChangeType, @Change, @After, @Note, @CreatedBy, GETDATE())",
            new { ProductId = productId, Change = quantityChange, ChangeType = changeType, Note = note, CreatedBy = changedBy });
    }
}
