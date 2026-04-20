using Dapper;
using Microsoft.Data.SqlClient;
using TanHuyComputer.API.DTOs;
using TanHuyComputer.API.Models;

namespace TanHuyComputer.API.Repositories;

// ===== REVIEW REPOSITORY =====
public interface IReviewRepository
{
    Task<(List<ReviewDto> Items, int Total)> GetByProductAsync(int productId, int page, int pageSize);
    Task<(List<ReviewDto> Items, int Total)> GetAllAsync(int page, int pageSize, string? status, int? productId);
    Task<int> CreateAsync(CreateReviewRequest req, int userId);
    Task ApproveAsync(int reviewId);
    Task DeleteAsync(int reviewId);
    Task UpdateProductRatingAsync(int productId);
}

public class ReviewRepository : IReviewRepository
{
    private readonly string _conn;
    public ReviewRepository(IConfiguration config) => _conn = config.GetConnectionString("DefaultConnection")!;
    private SqlConnection Conn() => new SqlConnection(_conn);

    public async Task<(List<ReviewDto> Items, int Total)> GetByProductAsync(int productId, int page, int pageSize)
    {
        using var conn = Conn();
        var sql = @"
            SELECT r.review_id AS ReviewId, r.user_id AS UserId, u.full_name AS UserFullName,
                   u.avatar_url AS UserAvatarUrl, r.rating, r.comment, r.status,
                   r.admin_reply AS AdminReply, r.created_at AS CreatedAt
            FROM Reviews r JOIN Users u ON r.user_id=u.user_id
            WHERE r.product_id=@ProductId AND r.status='approved'
            ORDER BY r.created_at DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            SELECT COUNT(*) FROM Reviews WHERE product_id=@ProductId AND status='approved';";

        using var multi = await conn.QueryMultipleAsync(sql,
            new { ProductId = productId, Offset = (page - 1) * pageSize, PageSize = pageSize });
        var items = (await multi.ReadAsync<ReviewDto>()).ToList();
        var total = await multi.ReadFirstAsync<int>();
        return (items, total);
    }

    public async Task<(List<ReviewDto> Items, int Total)> GetAllAsync(int page, int pageSize, string? status, int? productId)
    {
        using var conn = Conn();
        var conditions = new List<string>();
        if (!string.IsNullOrEmpty(status)) conditions.Add("r.status=@Status");
        if (productId.HasValue) conditions.Add("r.product_id=@ProductId");
        var where = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";

        var sql = $@"
            SELECT r.review_id AS ReviewId, r.user_id AS UserId, u.full_name AS UserFullName,
                   u.avatar_url AS UserAvatarUrl, r.rating, r.comment, r.status,
                   r.admin_reply AS AdminReply, r.created_at AS CreatedAt,
                   r.product_id AS ProductId, p.product_name AS ProductName, p.slug AS ProductSlug
            FROM Reviews r
            JOIN Users u ON r.user_id=u.user_id
            JOIN Products p ON r.product_id=p.product_id
            {where}
            ORDER BY r.created_at DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            SELECT COUNT(*) FROM Reviews r {where};";

        using var multi = await conn.QueryMultipleAsync(sql,
            new { Status = status, ProductId = productId, Offset = (page - 1) * pageSize, PageSize = pageSize });
        var items = (await multi.ReadAsync<ReviewDto>()).ToList();
        var total = await multi.ReadFirstAsync<int>();
        return (items, total);
    }

    public async Task<int> CreateAsync(CreateReviewRequest req, int userId)
    {
        using var conn = Conn();
        return await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO Reviews (product_id, user_id, order_id, rating, comment, status, created_at)
              VALUES (@ProductId, @UserId, @OrderId, @Rating, @Comment, 'approved', GETDATE());
              SELECT SCOPE_IDENTITY();",
            new { req.ProductId, UserId = userId, req.OrderId, req.Rating, req.Comment });
    }

    public async Task ApproveAsync(int reviewId)
    {
        using var conn = Conn();
        await conn.ExecuteAsync("UPDATE Reviews SET status='approved' WHERE review_id=@Id", new { Id = reviewId });
    }

    public async Task DeleteAsync(int reviewId)
    {
        using var conn = Conn();
        await conn.ExecuteAsync("DELETE FROM Reviews WHERE review_id=@Id", new { Id = reviewId });
    }

    public async Task UpdateProductRatingAsync(int productId)
    {
        using var conn = Conn();
        await conn.ExecuteAsync(
            @"UPDATE Products SET
                avg_rating = ISNULL((SELECT AVG(CAST(rating AS FLOAT)) FROM Reviews WHERE product_id=@Id AND status='approved'), 0),
                total_reviews = (SELECT COUNT(*) FROM Reviews WHERE product_id=@Id AND status='approved')
              WHERE product_id=@Id",
            new { Id = productId });
    }
}

// ===== COUPON REPOSITORY =====
public interface ICouponRepository
{
    Task<Coupon?> GetByCodeAsync(string code);
    Task<(List<CouponDto> Items, int Total)> GetAllAsync(int page, int pageSize);
    Task<int> CreateAsync(CreateCouponRequest req, int createdBy);
    Task UpdateAsync(int id, CreateCouponRequest req);
    Task IncrementUsedCountAsync(int couponId);
}

public class CouponRepository : ICouponRepository
{
    private readonly string _conn;
    public CouponRepository(IConfiguration config) => _conn = config.GetConnectionString("DefaultConnection")!;
    private SqlConnection Conn() => new SqlConnection(_conn);

    public async Task<Coupon?> GetByCodeAsync(string code)
    {
        using var conn = Conn();
        return await conn.QueryFirstOrDefaultAsync<Coupon>(
            @"SELECT coupon_id AS CouponId, code, discount_type AS DiscountType, discount_value AS DiscountValue,
                     min_order_value AS MinOrderValue, max_discount AS MaxDiscount, max_uses AS MaxUses,
                     used_count AS UsedCount, start_date AS StartDate, end_date AS EndDate,
                     is_active AS IsActive FROM Coupons WHERE code=@Code",
            new { Code = code });
    }

    public async Task<(List<CouponDto> Items, int Total)> GetAllAsync(int page, int pageSize)
    {
        using var conn = Conn();
        var sql = @"
            SELECT coupon_id AS CouponId, code, discount_type AS DiscountType, discount_value AS DiscountValue,
                   min_order_value AS MinOrderValue, max_discount AS MaxDiscount, max_uses AS MaxUses,
                   used_count AS UsedCount, start_date AS StartDate, end_date AS EndDate, is_active AS IsActive
            FROM Coupons ORDER BY coupon_id DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            SELECT COUNT(*) FROM Coupons;";

        using var multi = await conn.QueryMultipleAsync(sql, new { Offset = (page - 1) * pageSize, PageSize = pageSize });
        var items = (await multi.ReadAsync<CouponDto>()).ToList();
        var total = await multi.ReadFirstAsync<int>();
        return (items, total);
    }

    public async Task<int> CreateAsync(CreateCouponRequest req, int createdBy)
    {
        using var conn = Conn();
        return await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO Coupons (code, discount_type, discount_value, min_order_value, max_discount,
                                   max_uses, used_count, start_date, end_date, is_active, created_by)
              VALUES (@Code, @DiscountType, @DiscountValue, @MinOrderValue, @MaxDiscount,
                      @MaxUses, 0, @StartDate, @EndDate, @IsActive, @CreatedBy);
              SELECT SCOPE_IDENTITY();",
            new { req.Code, req.DiscountType, req.DiscountValue, req.MinOrderValue, req.MaxDiscount,
                  req.MaxUses, req.StartDate, req.EndDate, req.IsActive, CreatedBy = createdBy });
    }

    public async Task UpdateAsync(int id, CreateCouponRequest req)
    {
        using var conn = Conn();
        await conn.ExecuteAsync(
            @"UPDATE Coupons SET code=@Code, discount_type=@DiscountType, discount_value=@DiscountValue,
                                 min_order_value=@MinOrderValue, max_discount=@MaxDiscount, max_uses=@MaxUses,
                                 start_date=@StartDate, end_date=@EndDate, is_active=@IsActive
              WHERE coupon_id=@Id",
            new { req.Code, req.DiscountType, req.DiscountValue, req.MinOrderValue, req.MaxDiscount,
                  req.MaxUses, req.StartDate, req.EndDate, req.IsActive, Id = id });
    }

    public async Task IncrementUsedCountAsync(int couponId)
    {
        using var conn = Conn();
        await conn.ExecuteAsync("UPDATE Coupons SET used_count=used_count+1 WHERE coupon_id=@Id", new { Id = couponId });
    }
}

// ===== WISHLIST REPOSITORY =====
public interface IWishlistRepository
{
    Task<List<Wishlist>> GetByUserAsync(int userId);
    Task AddAsync(int userId, int productId);
    Task RemoveAsync(int userId, int productId);
    Task<bool> ExistsAsync(int userId, int productId);
}

public class WishlistRepository : IWishlistRepository
{
    private readonly string _conn;
    public WishlistRepository(IConfiguration config) => _conn = config.GetConnectionString("DefaultConnection")!;
    private SqlConnection Conn() => new SqlConnection(_conn);

    public async Task<List<Wishlist>> GetByUserAsync(int userId)
    {
        using var conn = Conn();
        return (await conn.QueryAsync<Wishlist>(
            @"SELECT w.wishlist_id AS WishlistId, w.user_id AS UserId, w.product_id AS ProductId,
                     w.created_at AS CreatedAt, p.product_name AS ProductName,
                     p.thumbnail_url AS ThumbnailUrl, p.price, p.sale_price AS SalePrice, p.slug
              FROM Wishlists w JOIN Products p ON w.product_id=p.product_id
              WHERE w.user_id=@UserId ORDER BY w.created_at DESC",
            new { UserId = userId })).ToList();
    }

    public async Task AddAsync(int userId, int productId)
    {
        using var conn = Conn();
        await conn.ExecuteAsync(
            "IF NOT EXISTS (SELECT 1 FROM Wishlists WHERE user_id=@UserId AND product_id=@ProductId) INSERT INTO Wishlists (user_id, product_id, created_at) VALUES (@UserId, @ProductId, GETDATE())",
            new { UserId = userId, ProductId = productId });
    }

    public async Task RemoveAsync(int userId, int productId)
    {
        using var conn = Conn();
        await conn.ExecuteAsync("DELETE FROM Wishlists WHERE user_id=@UserId AND product_id=@ProductId",
            new { UserId = userId, ProductId = productId });
    }

    public async Task<bool> ExistsAsync(int userId, int productId)
    {
        using var conn = Conn();
        return await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Wishlists WHERE user_id=@UserId AND product_id=@ProductId",
            new { UserId = userId, ProductId = productId }) > 0;
    }
}

// ===== ADDRESS REPOSITORY =====
public interface IAddressRepository
{
    Task<List<AddressDto>> GetByUserAsync(int userId);
    Task<int> CreateAsync(int userId, CreateAddressRequest req);
    Task UpdateAsync(int addressId, int userId, CreateAddressRequest req);
    Task DeleteAsync(int addressId, int userId);
    Task SetDefaultAsync(int addressId, int userId);
    Task<AddressDto?> GetByIdAsync(int addressId);
}

public class AddressRepository : IAddressRepository
{
    private readonly string _conn;
    public AddressRepository(IConfiguration config) => _conn = config.GetConnectionString("DefaultConnection")!;
    private SqlConnection Conn() => new SqlConnection(_conn);

    public async Task<List<AddressDto>> GetByUserAsync(int userId)
    {
        using var conn = Conn();
        return (await conn.QueryAsync<AddressDto>(
            @"SELECT address_id AS AddressId, receiver_name AS ReceiverName, phone, province, district, ward,
                     street_address AS StreetAddress, is_default AS IsDefault
              FROM UserAddresses WHERE user_id=@UserId ORDER BY is_default DESC",
            new { UserId = userId })).ToList();
    }

    public async Task<int> CreateAsync(int userId, CreateAddressRequest req)
    {
        using var conn = Conn();
        if (req.IsDefault)
            await conn.ExecuteAsync("UPDATE UserAddresses SET is_default=0 WHERE user_id=@UserId", new { UserId = userId });
        return await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO UserAddresses (user_id, receiver_name, phone, province, district, ward, street_address, is_default)
              VALUES (@UserId, @ReceiverName, @Phone, @Province, @District, @Ward, @StreetAddress, @IsDefault);
              SELECT SCOPE_IDENTITY();",
            new { UserId = userId, req.ReceiverName, req.Phone, req.Province, req.District, req.Ward, req.StreetAddress, req.IsDefault });
    }

    public async Task UpdateAsync(int addressId, int userId, CreateAddressRequest req)
    {
        using var conn = Conn();
        if (req.IsDefault)
            await conn.ExecuteAsync("UPDATE UserAddresses SET is_default=0 WHERE user_id=@UserId", new { UserId = userId });
        await conn.ExecuteAsync(
            @"UPDATE UserAddresses SET receiver_name=@ReceiverName, phone=@Phone, province=@Province,
                                       district=@District, ward=@Ward, street_address=@StreetAddress, is_default=@IsDefault
              WHERE address_id=@AddressId AND user_id=@UserId",
            new { req.ReceiverName, req.Phone, req.Province, req.District, req.Ward, req.StreetAddress, req.IsDefault, AddressId = addressId, UserId = userId });
    }

    public async Task DeleteAsync(int addressId, int userId)
    {
        using var conn = Conn();
        await conn.ExecuteAsync("DELETE FROM UserAddresses WHERE address_id=@AddressId AND user_id=@UserId",
            new { AddressId = addressId, UserId = userId });
    }

    public async Task SetDefaultAsync(int addressId, int userId)
    {
        using var conn = Conn();
        await conn.ExecuteAsync("UPDATE UserAddresses SET is_default=0 WHERE user_id=@UserId", new { UserId = userId });
        await conn.ExecuteAsync("UPDATE UserAddresses SET is_default=1 WHERE address_id=@AddressId AND user_id=@UserId",
            new { AddressId = addressId, UserId = userId });
    }

    public async Task<AddressDto?> GetByIdAsync(int addressId)
    {
        using var conn = Conn();
        return await conn.QueryFirstOrDefaultAsync<AddressDto>(
            @"SELECT address_id AS AddressId, receiver_name AS ReceiverName, phone, province, district, ward,
                     street_address AS StreetAddress, is_default AS IsDefault
              FROM UserAddresses WHERE address_id=@AddressId",
            new { AddressId = addressId });
    }
}
