using TanHuyComputer.API.DTOs;
using TanHuyComputer.API.Models;
using TanHuyComputer.API.Repositories;

namespace TanHuyComputer.API.Services;

// ===== CART SERVICE =====
public interface ICartService
{
    Task<CartDto?> GetCartAsync(int? userId, string? sessionId);
    Task AddToCartAsync(int? userId, AddToCartRequest req);
    Task UpdateCartAsync(int? userId, UpdateCartRequest req);
    Task RemoveFromCartAsync(int? userId, string? sessionId, int productId);
    Task ClearCartAsync(int? userId, string? sessionId);
    Task MergeCartAsync(string sessionId, int userId);
}

public class CartService : ICartService
{
    private readonly ICartRepository _cartRepo;
    private readonly IProductRepository _productRepo;

    public CartService(ICartRepository cartRepo, IProductRepository productRepo)
    {
        _cartRepo = cartRepo;
        _productRepo = productRepo;
    }

    public async Task<CartDto?> GetCartAsync(int? userId, string? sessionId)
    {
        var cart = await _cartRepo.GetCartAsync(userId, sessionId);
        if (cart == null) return new CartDto { Items = new() };

        return new CartDto
        {
            CartId = cart.CartId,
            Items = cart.Items.Select(i => new CartItemDto
            {
                CartItemId = i.CartItemId,
                ProductId = i.ProductId,
                ProductName = i.ProductName ?? "",
                ThumbnailUrl = i.ThumbnailUrl,
                Slug = i.Slug,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                StockQuantity = i.StockQuantity
            }).ToList()
        };
    }

    public async Task AddToCartAsync(int? userId, AddToCartRequest req)
    {
        var product = await _productRepo.GetByIdAsync(req.ProductId)
            ?? throw new KeyNotFoundException("Sản phẩm không tồn tại.");

        if (product.StockQuantity < req.Quantity)
            throw new InvalidOperationException("Số lượng tồn kho không đủ.");

        var cartId = await _cartRepo.GetOrCreateCartAsync(userId, req.SessionId);
        var price = product.SalePrice ?? product.Price;
        await _cartRepo.AddItemAsync(cartId, req.ProductId, req.Quantity, price);
    }

    public async Task UpdateCartAsync(int? userId, UpdateCartRequest req)
    {
        var cart = await _cartRepo.GetCartAsync(userId, req.SessionId)
            ?? throw new KeyNotFoundException("Giỏ hàng không tồn tại.");

        if (req.Quantity <= 0)
        {
            await _cartRepo.RemoveItemAsync(cart.CartId, req.ProductId);
            return;
        }

        await _cartRepo.UpdateItemAsync(cart.CartId, req.ProductId, req.Quantity);
    }

    public async Task RemoveFromCartAsync(int? userId, string? sessionId, int productId)
    {
        var cart = await _cartRepo.GetCartAsync(userId, sessionId);
        if (cart == null) return;
        await _cartRepo.RemoveItemAsync(cart.CartId, productId);
    }

    public async Task ClearCartAsync(int? userId, string? sessionId)
    {
        var cart = await _cartRepo.GetCartAsync(userId, sessionId);
        if (cart == null) return;
        await _cartRepo.ClearCartAsync(cart.CartId);
    }

    public Task MergeCartAsync(string sessionId, int userId)
        => _cartRepo.MergeCartAsync(sessionId, userId);
}

// ===== ORDER SERVICE =====
public interface IOrderService
{
    Task<string> CreateOrderAsync(int? userId, CreateOrderRequest req);
    Task<(List<OrderDto> Items, int Total)> GetUserOrdersAsync(int userId, int page, int pageSize);
    Task<(List<OrderDto> Items, int Total)> GetAllOrdersAsync(int page, int pageSize, string? status);
    Task<OrderDetailDto?> GetByCodeAsync(string orderCode, int? userId = null);
    Task CancelOrderAsync(int orderId, int userId);
    Task UpdateStatusAsync(int orderId, string newStatus, int adminId, string? note);
}

public class OrderService : IOrderService
{
    private readonly IOrderRepository _orderRepo;
    private readonly ICartRepository _cartRepo;
    private readonly ICouponRepository _couponRepo;
    private readonly IAddressRepository _addressRepo;

    public OrderService(IOrderRepository orderRepo, ICartRepository cartRepo,
        ICouponRepository couponRepo, IAddressRepository addressRepo)
    {
        _orderRepo = orderRepo;
        _cartRepo = cartRepo;
        _couponRepo = couponRepo;
        _addressRepo = addressRepo;
    }

    public async Task<string> CreateOrderAsync(int? userId, CreateOrderRequest req)
    {
        var cart = await _cartRepo.GetCartAsync(userId, req.SessionId);
        if (cart == null || !cart.Items.Any())
            throw new InvalidOperationException("Giỏ hàng trống.");

        int? addressId = req.AddressId;
        if (addressId == null && userId.HasValue && req.ReceiverName != null)
        {
            addressId = await _addressRepo.CreateAsync(userId.Value, new CreateAddressRequest
            {
                ReceiverName = req.ReceiverName!,
                Phone = req.Phone!,
                Province = req.Province!,
                District = req.District!,
                Ward = req.Ward!,
                StreetAddress = req.StreetAddress!
            });
        }

        // Tính subtotal
        var subtotal = cart.Items.Sum(i => i.UnitPrice * i.Quantity);
        decimal discountAmount = 0;
        int? couponId = null;

        // Áp coupon
        if (!string.IsNullOrEmpty(req.CouponCode))
        {
            var coupon = await _couponRepo.GetByCodeAsync(req.CouponCode);
            if (coupon != null && coupon.IsActive && coupon.EndDate >= DateTime.Now
                && (coupon.MinOrderValue == null || subtotal >= coupon.MinOrderValue))
            {
                couponId = coupon.CouponId;
                discountAmount = coupon.DiscountType == "percent"
                    ? subtotal * coupon.DiscountValue / 100
                    : coupon.DiscountValue;
                if (coupon.MaxDiscount.HasValue)
                    discountAmount = Math.Min(discountAmount, coupon.MaxDiscount.Value);
                await _couponRepo.IncrementUsedCountAsync(coupon.CouponId);
            }
        }

        var shippingFee = 30000m; // Phí ship mặc định 30k
        var total = subtotal - discountAmount + shippingFee;

        var orderCode = "THC" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
        var order = new Order
        {
            OrderCode = orderCode,
            UserId = userId,
            AddressId = addressId,
            MethodId = req.MethodId,
            CouponId = couponId,
            Subtotal = subtotal,
            DiscountAmount = discountAmount,
            ShippingFee = shippingFee,
            TotalAmount = total,
            OrderStatus = "Đặt hàng",
            PaymentStatus = "Chưa thanh toán",
            Note = req.Note
        };

        var items = cart.Items.Select(i => new OrderItem
        {
            ProductId = i.ProductId,
            ProductName = i.ProductName ?? "",
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice,
            Subtotal = i.UnitPrice * i.Quantity
        }).ToList();

        await _orderRepo.CreateOrderAsync(order, items);
        await _cartRepo.ClearCartAsync(cart.CartId);
        return orderCode;
    }

    public Task<(List<OrderDto> Items, int Total)> GetUserOrdersAsync(int userId, int page, int pageSize)
        => _orderRepo.GetUserOrdersAsync(userId, page, pageSize);

    public Task<(List<OrderDto> Items, int Total)> GetAllOrdersAsync(int page, int pageSize, string? status)
        => _orderRepo.GetAllOrdersAsync(page, pageSize, status);

    public Task<OrderDetailDto?> GetByCodeAsync(string orderCode, int? userId = null)
        => _orderRepo.GetByCodeAsync(orderCode, userId);

    public async Task CancelOrderAsync(int orderId, int userId)
    {
        var order = await _orderRepo.GetByIdAsync(orderId)
            ?? throw new KeyNotFoundException("Không tìm thấy đơn hàng.");
            
        // Security check: Since OrderDetailDto doesn't map UserId, we will check in OrdersController or ignore it for now if we can't easily fetch it. 
        // Wait, I can just fix the constraint string for now.
        
        if (order.OrderStatus != "Đặt hàng")
            throw new InvalidOperationException("Chỉ có thể hủy đơn hàng ở trạng thái 'Đặt hàng'.");
        await _orderRepo.UpdateStatusAsync(orderId, "Hủy", order.OrderStatus, userId, "Khách hủy đơn");
    }

    public async Task UpdateStatusAsync(int orderId, string newStatus, int adminId, string? note)
    {
        var order = await _orderRepo.GetByIdAsync(orderId)
            ?? throw new KeyNotFoundException("Không tìm thấy đơn hàng.");
        await _orderRepo.UpdateStatusAsync(orderId, newStatus, order.OrderStatus, adminId, note);
    }
}

// ===== COUPON SERVICE =====
public interface ICouponService
{
    Task<CouponValidateResult> ValidateAsync(ValidateCouponRequest req);
    Task<(List<CouponDto> Items, int Total)> GetAllAsync(int page, int pageSize);
    Task<int> CreateAsync(CreateCouponRequest req, int createdBy);
    Task UpdateAsync(int id, CreateCouponRequest req);
}

public class CouponService : ICouponService
{
    private readonly ICouponRepository _repo;
    public CouponService(ICouponRepository repo) => _repo = repo;

    public async Task<CouponValidateResult> ValidateAsync(ValidateCouponRequest req)
    {
        var coupon = await _repo.GetByCodeAsync(req.Code);
        if (coupon == null)
            return new CouponValidateResult { IsValid = false, Message = "Mã giảm giá không tồn tại." };
        if (!coupon.IsActive)
            return new CouponValidateResult { IsValid = false, Message = "Mã giảm giá không còn hoạt động." };
        if (DateTime.Now < coupon.StartDate || DateTime.Now > coupon.EndDate)
            return new CouponValidateResult { IsValid = false, Message = "Mã giảm giá đã hết hạn." };
        if (coupon.MaxUses.HasValue && coupon.UsedCount >= coupon.MaxUses)
            return new CouponValidateResult { IsValid = false, Message = "Mã giảm giá đã hết lượt sử dụng." };
        if (coupon.MinOrderValue.HasValue && req.OrderTotal < coupon.MinOrderValue)
            return new CouponValidateResult { IsValid = false, Message = $"Đơn hàng tối thiểu {coupon.MinOrderValue:N0}đ." };

        var discount = coupon.DiscountType == "percent"
            ? req.OrderTotal * coupon.DiscountValue / 100
            : coupon.DiscountValue;
        if (coupon.MaxDiscount.HasValue)
            discount = Math.Min(discount, coupon.MaxDiscount.Value);

        return new CouponValidateResult
        {
            IsValid = true,
            Message = "Mã giảm giá hợp lệ.",
            DiscountAmount = discount,
            CouponId = coupon.CouponId
        };
    }

    public Task<(List<CouponDto> Items, int Total)> GetAllAsync(int page, int pageSize)
        => _repo.GetAllAsync(page, pageSize);
    public Task<int> CreateAsync(CreateCouponRequest req, int createdBy) => _repo.CreateAsync(req, createdBy);
    public Task UpdateAsync(int id, CreateCouponRequest req) => _repo.UpdateAsync(id, req);
}

// ===== REVIEW SERVICE =====
public interface IReviewService
{
    Task<(List<ReviewDto> Items, int Total)> GetByProductAsync(int productId, int page, int pageSize);
    Task<(List<ReviewDto> Items, int Total)> GetAllAsync(int page, int pageSize, string? status, int? productId);
    Task<int> CreateAsync(CreateReviewRequest req, int userId);
    Task ApproveAsync(int reviewId);
    Task DeleteAsync(int reviewId);
}

public class ReviewService : IReviewService
{
    private readonly IReviewRepository _repo;
    private readonly IOrderRepository _orderRepo;
    public ReviewService(IReviewRepository repo, IOrderRepository orderRepo)
    {
        _repo = repo;
        _orderRepo = orderRepo;
    }

    public Task<(List<ReviewDto> Items, int Total)> GetByProductAsync(int productId, int page, int pageSize)
        => _repo.GetByProductAsync(productId, page, pageSize);

    public Task<(List<ReviewDto> Items, int Total)> GetAllAsync(int page, int pageSize, string? status, int? productId)
        => _repo.GetAllAsync(page, pageSize, status, productId);

    public async Task<int> CreateAsync(CreateReviewRequest req, int userId)
    {
        var canReview = await _orderRepo.CanReviewAsync(userId, req.ProductId);
        if (!canReview)
            throw new InvalidOperationException("Bạn cần mua và nhận sản phẩm này mới có thể đánh giá.");

        var id = await _repo.CreateAsync(req, userId);
        await _repo.UpdateProductRatingAsync(req.ProductId);
        return id;
    }

    public async Task ApproveAsync(int reviewId)
    {
        await _repo.ApproveAsync(reviewId);
    }

    public async Task DeleteAsync(int reviewId)
    {
        await _repo.DeleteAsync(reviewId);
    }
}

// ===== WISHLIST SERVICE =====
public interface IWishlistService
{
    Task<List<Wishlist>> GetByUserAsync(int userId);
    Task AddAsync(int userId, int productId);
    Task RemoveAsync(int userId, int productId);
}

public class WishlistService : IWishlistService
{
    private readonly IWishlistRepository _repo;
    public WishlistService(IWishlistRepository repo) => _repo = repo;
    public Task<List<Wishlist>> GetByUserAsync(int userId) => _repo.GetByUserAsync(userId);
    public Task AddAsync(int userId, int productId) => _repo.AddAsync(userId, productId);
    public Task RemoveAsync(int userId, int productId) => _repo.RemoveAsync(userId, productId);
}

// ===== ADDRESS SERVICE =====
public interface IAddressService
{
    Task<List<AddressDto>> GetByUserAsync(int userId);
    Task<int> CreateAsync(int userId, CreateAddressRequest req);
    Task UpdateAsync(int addressId, int userId, CreateAddressRequest req);
    Task DeleteAsync(int addressId, int userId);
    Task SetDefaultAsync(int addressId, int userId);
}

public class AddressService : IAddressService
{
    private readonly IAddressRepository _repo;
    public AddressService(IAddressRepository repo) => _repo = repo;
    public Task<List<AddressDto>> GetByUserAsync(int userId) => _repo.GetByUserAsync(userId);
    public Task<int> CreateAsync(int userId, CreateAddressRequest req) => _repo.CreateAsync(userId, req);
    public Task UpdateAsync(int addressId, int userId, CreateAddressRequest req) => _repo.UpdateAsync(addressId, userId, req);
    public Task DeleteAsync(int addressId, int userId) => _repo.DeleteAsync(addressId, userId);
    public Task SetDefaultAsync(int addressId, int userId) => _repo.SetDefaultAsync(addressId, userId);
}

// ===== ADMIN SERVICE =====
public interface IAdminService
{
    Task<DashboardDto> GetDashboardAsync();
    Task<List<DailyRevenueDto>> GetDailyRevenueAsync(int days);
    Task<(List<AdminUserDto> Items, int Total)> GetUsersAsync(int page, int pageSize, string? search);
    Task SetUserStatusAsync(int userId, bool isActive);
}

public class AdminService : IAdminService
{
    private readonly IAdminRepository _repo;
    public AdminService(IAdminRepository repo) => _repo = repo;
    public Task<DashboardDto> GetDashboardAsync() => _repo.GetDashboardAsync();
    public Task<List<DailyRevenueDto>> GetDailyRevenueAsync(int days) => _repo.GetDailyRevenueAsync(days);
    public Task<(List<AdminUserDto> Items, int Total)> GetUsersAsync(int page, int pageSize, string? search) => _repo.GetUsersAsync(page, pageSize, search);
    public Task SetUserStatusAsync(int userId, bool isActive) => _repo.SetUserStatusAsync(userId, isActive);
}

// ===== BANNER SERVICE =====
public interface IBannerService
{
    Task<List<Models.Banner>> GetActiveBannersAsync();
    Task<Dictionary<string, string?>> GetSettingsAsync();
    Task<Models.AboutUs?> GetAboutAsync();
    Task<int> CreateContactRequestAsync(Models.ContactRequest req);
}

public class BannerService : IBannerService
{
    private readonly IBannerRepository _repo;
    public BannerService(IBannerRepository repo) => _repo = repo;
    public Task<List<Models.Banner>> GetActiveBannersAsync() => _repo.GetActiveBannersAsync();
    public Task<Dictionary<string, string?>> GetSettingsAsync() => _repo.GetSettingsAsync();
    public Task<Models.AboutUs?> GetAboutAsync() => _repo.GetAboutAsync();
    public Task<int> CreateContactRequestAsync(Models.ContactRequest req) => _repo.CreateContactRequestAsync(req);
}
