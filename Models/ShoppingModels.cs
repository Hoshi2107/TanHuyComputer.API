namespace TanHuyComputer.API.Models;

public class Review
{
    public int ReviewId { get; set; }
    public int ProductId { get; set; }
    public int UserId { get; set; }
    public int? OrderId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public string Status { get; set; } = "pending"; // pending, approved, rejected
    public string? AdminReply { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation
    public string? UserFullName { get; set; }
    public string? UserAvatarUrl { get; set; }
}

public class Wishlist
{
    public int WishlistId { get; set; }
    public int UserId { get; set; }
    public int ProductId { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation
    public string? ProductName { get; set; }
    public string? ThumbnailUrl { get; set; }
    public decimal Price { get; set; }
    public decimal? SalePrice { get; set; }
    public string? Slug { get; set; }
}

public class StockNotification
{
    public int NotifId { get; set; }
    public int UserId { get; set; }
    public int ProductId { get; set; }
    public bool IsSent { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class Coupon
{
    public int CouponId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string DiscountType { get; set; } = string.Empty; // percent, fixed
    public decimal DiscountValue { get; set; }
    public decimal? MinOrderValue { get; set; }
    public decimal? MaxDiscount { get; set; }
    public int? MaxUses { get; set; }
    public int UsedCount { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; }
    public int? CreatedBy { get; set; }
}

public class Cart
{
    public int CartId { get; set; }
    public int? UserId { get; set; }
    public string? SessionId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<CartItem> Items { get; set; } = new();
}

public class CartItem
{
    public int CartItemId { get; set; }
    public int CartId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public DateTime AddedAt { get; set; }

    // Navigation
    public string? ProductName { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? Slug { get; set; }
    public int StockQuantity { get; set; }
}

public class PaymentMethod
{
    public int MethodId { get; set; }
    public string MethodName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}
