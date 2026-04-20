using Dapper;
using Microsoft.Data.SqlClient;
using TanHuyComputer.API.DTOs;
using TanHuyComputer.API.Models;

namespace TanHuyComputer.API.Repositories;

// ===== CART REPOSITORY =====
public interface ICartRepository
{
    Task<Cart?> GetCartAsync(int? userId, string? sessionId);
    Task<int> GetOrCreateCartAsync(int? userId, string? sessionId);
    Task AddItemAsync(int cartId, int productId, int quantity, decimal unitPrice);
    Task UpdateItemAsync(int cartId, int productId, int quantity);
    Task RemoveItemAsync(int cartId, int productId);
    Task ClearCartAsync(int cartId);
    Task MergeCartAsync(string sessionId, int userId);
}

public class CartRepository : ICartRepository
{
    private readonly string _conn;
    public CartRepository(IConfiguration config) => _conn = config.GetConnectionString("DefaultConnection")!;
    private SqlConnection Conn() => new SqlConnection(_conn);

    public async Task<Cart?> GetCartAsync(int? userId, string? sessionId)
    {
        using var conn = Conn();
        Cart? cart;
        if (userId.HasValue)
            cart = await conn.QueryFirstOrDefaultAsync<Cart>(
                "SELECT cart_id AS CartId, user_id AS UserId, session_id AS SessionId FROM Carts WHERE user_id=@UserId",
                new { UserId = userId });
        else
            cart = await conn.QueryFirstOrDefaultAsync<Cart>(
                "SELECT cart_id AS CartId, user_id AS UserId, session_id AS SessionId FROM Carts WHERE session_id=@SessionId",
                new { SessionId = sessionId });

        if (cart == null) return null;

        var items = await conn.QueryAsync<CartItem>(
            @"SELECT ci.cart_item_id AS CartItemId, ci.cart_id AS CartId, ci.product_id AS ProductId,
                     ci.quantity AS Quantity, ci.unit_price AS UnitPrice, ci.added_at AS AddedAt,
                     p.product_name AS ProductName, p.thumbnail_url AS ThumbnailUrl,
                     p.slug AS Slug, p.stock_quantity AS StockQuantity
              FROM CartItems ci
              JOIN Products p ON ci.product_id = p.product_id
              WHERE ci.cart_id=@CartId",
            new { CartId = cart.CartId });
        cart.Items = items.ToList();
        return cart;
    }

    public async Task<int> GetOrCreateCartAsync(int? userId, string? sessionId)
    {
        using var conn = Conn();
        int? cartId;
        if (userId.HasValue)
        {
            cartId = await conn.QueryFirstOrDefaultAsync<int?>(
                "SELECT cart_id FROM Carts WHERE user_id=@UserId", new { UserId = userId });
            if (!cartId.HasValue)
            {
                cartId = await conn.ExecuteScalarAsync<int>(
                    "INSERT INTO Carts (user_id, created_at, updated_at) VALUES (@UserId, GETDATE(), GETDATE()); SELECT SCOPE_IDENTITY();",
                    new { UserId = userId });
            }
        }
        else
        {
            cartId = await conn.QueryFirstOrDefaultAsync<int?>(
                "SELECT cart_id FROM Carts WHERE session_id=@SessionId", new { SessionId = sessionId });
            if (!cartId.HasValue)
            {
                cartId = await conn.ExecuteScalarAsync<int>(
                    "INSERT INTO Carts (session_id, created_at, updated_at) VALUES (@SessionId, GETDATE(), GETDATE()); SELECT SCOPE_IDENTITY();",
                    new { SessionId = sessionId });
            }
        }
        return cartId!.Value;
    }

    public async Task AddItemAsync(int cartId, int productId, int quantity, decimal unitPrice)
    {
        using var conn = Conn();
        var existing = await conn.QueryFirstOrDefaultAsync<int?>(
            "SELECT cart_item_id FROM CartItems WHERE cart_id=@CartId AND product_id=@ProductId",
            new { CartId = cartId, ProductId = productId });

        if (existing.HasValue)
            await conn.ExecuteAsync(
                "UPDATE CartItems SET quantity=quantity+@Qty WHERE cart_item_id=@Id",
                new { Qty = quantity, Id = existing });
        else
            await conn.ExecuteAsync(
                "INSERT INTO CartItems (cart_id, product_id, quantity, unit_price, added_at) VALUES (@CartId, @ProductId, @Qty, @Price, GETDATE())",
                new { CartId = cartId, ProductId = productId, Qty = quantity, Price = unitPrice });

        await conn.ExecuteAsync("UPDATE Carts SET updated_at=GETDATE() WHERE cart_id=@CartId", new { CartId = cartId });
    }

    public async Task UpdateItemAsync(int cartId, int productId, int quantity)
    {
        using var conn = Conn();
        await conn.ExecuteAsync(
            "UPDATE CartItems SET quantity=@Qty WHERE cart_id=@CartId AND product_id=@ProductId",
            new { CartId = cartId, ProductId = productId, Qty = quantity });
    }

    public async Task RemoveItemAsync(int cartId, int productId)
    {
        using var conn = Conn();
        await conn.ExecuteAsync(
            "DELETE FROM CartItems WHERE cart_id=@CartId AND product_id=@ProductId",
            new { CartId = cartId, ProductId = productId });
    }

    public async Task ClearCartAsync(int cartId)
    {
        using var conn = Conn();
        await conn.ExecuteAsync("DELETE FROM CartItems WHERE cart_id=@CartId", new { CartId = cartId });
    }

    public async Task MergeCartAsync(string sessionId, int userId)
    {
        using var conn = Conn();
        var sessionCart = await conn.QueryFirstOrDefaultAsync<int?>(
            "SELECT cart_id FROM Carts WHERE session_id=@SessionId", new { SessionId = sessionId });
        if (!sessionCart.HasValue) return;

        var userCartId = await GetOrCreateCartAsync(userId, null);
        var items = await conn.QueryAsync<CartItem>(
            "SELECT product_id AS ProductId, quantity AS Quantity, unit_price AS UnitPrice FROM CartItems WHERE cart_id=@CartId",
            new { CartId = sessionCart });

        foreach (var item in items)
            await AddItemAsync(userCartId, item.ProductId, item.Quantity, item.UnitPrice);

        await conn.ExecuteAsync("DELETE FROM CartItems WHERE cart_id=@CartId", new { CartId = sessionCart });
        await conn.ExecuteAsync("DELETE FROM Carts WHERE cart_id=@CartId", new { CartId = sessionCart });
    }
}

// ===== ORDER REPOSITORY =====
public interface IOrderRepository
{
    Task<string> CreateOrderAsync(Order order, List<OrderItem> items);
    Task<(List<OrderDto> Items, int Total)> GetUserOrdersAsync(int userId, int page, int pageSize);
    Task<(List<OrderDto> Items, int Total)> GetAllOrdersAsync(int page, int pageSize, string? status);
    Task<OrderDetailDto?> GetByCodeAsync(string orderCode, int? userId = null);
    Task<OrderDetailDto?> GetByIdAsync(int orderId);
    Task UpdateStatusAsync(int orderId, string newStatus, string? oldStatus, int? changedBy, string? note);
    Task<bool> CanReviewAsync(int userId, int productId);
}

public class OrderRepository : IOrderRepository
{
    private readonly string _conn;
    public OrderRepository(IConfiguration config) => _conn = config.GetConnectionString("DefaultConnection")!;
    private SqlConnection Conn() => new SqlConnection(_conn);

    public async Task<string> CreateOrderAsync(Order order, List<OrderItem> items)
    {
        using var conn = Conn();
        var orderId = await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO Orders (order_code, user_id, address_id, method_id, coupon_id,
                                  subtotal, discount_amount, shipping_fee, total_amount,
                                  order_status, payment_status, note, created_at, updated_at)
              VALUES (@OrderCode, @UserId, @AddressId, @MethodId, @CouponId,
                      @Subtotal, @DiscountAmount, @ShippingFee, @TotalAmount,
                      @OrderStatus, @PaymentStatus, @Note, GETDATE(), GETDATE());
              SELECT SCOPE_IDENTITY();",
            new { order.OrderCode, order.UserId, order.AddressId, order.MethodId, order.CouponId,
                  order.Subtotal, order.DiscountAmount, order.ShippingFee, order.TotalAmount,
                  order.OrderStatus, order.PaymentStatus, order.Note });

        foreach (var item in items)
        {
            await conn.ExecuteAsync(
                @"INSERT INTO OrderItems (order_id, product_id, product_name, quantity, unit_price, subtotal)
                  VALUES (@OrderId, @ProductId, @ProductName, @Quantity, @UnitPrice, @Subtotal)",
                new { OrderId = orderId, item.ProductId, item.ProductName, item.Quantity, item.UnitPrice, item.Subtotal });

            await conn.ExecuteAsync(
                @"UPDATE Products SET stock_quantity=stock_quantity-@Qty, total_sold=total_sold+@Qty, updated_at=GETDATE()
                  WHERE product_id=@ProductId",
                new { Qty = item.Quantity, item.ProductId });
        }

        await conn.ExecuteAsync(
            @"INSERT INTO OrderStatusHistory (order_id, new_status, changed_at) VALUES (@OrderId, @Status, GETDATE())",
            new { OrderId = orderId, Status = order.OrderStatus });

        return order.OrderCode;
    }

    public async Task<(List<OrderDto> Items, int Total)> GetUserOrdersAsync(int userId, int page, int pageSize)
    {
        using var conn = Conn();
        var sql = @"
            SELECT o.order_id AS OrderId, o.order_code AS OrderCode, o.subtotal, o.discount_amount AS DiscountAmount,
                   o.shipping_fee AS ShippingFee, o.total_amount AS TotalAmount, o.order_status AS OrderStatus,
                   o.payment_status AS PaymentStatus, pm.method_name AS PaymentMethodName, o.note,
                   o.created_at AS CreatedAt,
                   (SELECT COUNT(*) FROM OrderItems oi WHERE oi.order_id=o.order_id) AS ItemCount
            FROM Orders o
            LEFT JOIN PaymentMethods pm ON o.method_id=pm.method_id
            WHERE o.user_id=@UserId
            ORDER BY o.created_at DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            SELECT COUNT(*) FROM Orders WHERE user_id=@UserId;";

        using var multi = await conn.QueryMultipleAsync(sql, new { UserId = userId, Offset = (page - 1) * pageSize, PageSize = pageSize });
        var items = (await multi.ReadAsync<OrderDto>()).ToList();
        var total = await multi.ReadFirstAsync<int>();
        return (items, total);
    }

    public async Task<(List<OrderDto> Items, int Total)> GetAllOrdersAsync(int page, int pageSize, string? status)
    {
        using var conn = Conn();
        var whereClause = status != null ? "WHERE o.order_status=@Status" : "";
        var sql = $@"
            SELECT o.order_id AS OrderId, o.order_code AS OrderCode, o.subtotal, o.discount_amount AS DiscountAmount,
                   o.shipping_fee AS ShippingFee, o.total_amount AS TotalAmount, o.order_status AS OrderStatus,
                   o.payment_status AS PaymentStatus, pm.method_name AS PaymentMethodName,
                   o.created_at AS CreatedAt,
                   u.full_name AS UserFullName, u.email AS UserEmail,
                   (SELECT COUNT(*) FROM OrderItems oi WHERE oi.order_id=o.order_id) AS ItemCount
            FROM Orders o
            LEFT JOIN PaymentMethods pm ON o.method_id=pm.method_id
            LEFT JOIN Users u ON o.user_id=u.user_id
            {whereClause}
            ORDER BY o.created_at DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            SELECT COUNT(*) FROM Orders o {whereClause};";

        using var multi = await conn.QueryMultipleAsync(sql, new { Status = status, Offset = (page - 1) * pageSize, PageSize = pageSize });
        var items = (await multi.ReadAsync<OrderDto>()).ToList();
        var total = await multi.ReadFirstAsync<int>();
        return (items, total);
    }

    public async Task<OrderDetailDto?> GetByCodeAsync(string orderCode, int? userId = null)
    {
        using var conn = Conn();
        var whereExtra = userId.HasValue ? "AND o.user_id=@UserId" : "";
        var order = await conn.QueryFirstOrDefaultAsync<OrderDetailDto>(
            $@"SELECT o.order_id AS OrderId, o.order_code AS OrderCode, o.subtotal,
                      o.discount_amount AS DiscountAmount, o.shipping_fee AS ShippingFee,
                      o.total_amount AS TotalAmount, o.order_status AS OrderStatus,
                      o.payment_status AS PaymentStatus, pm.method_name AS PaymentMethodName,
                      o.note, o.created_at AS CreatedAt,
                      u.full_name AS UserFullName, u.email AS UserEmail, u.phone AS UserPhone
               FROM Orders o
               LEFT JOIN PaymentMethods pm ON o.method_id=pm.method_id
               LEFT JOIN Users u ON o.user_id=u.user_id
               WHERE o.order_code=@OrderCode {whereExtra}",
            new { OrderCode = orderCode, UserId = userId });

        if (order == null) return null;
        await EnrichOrderDetailAsync(conn, order);
        return order;
    }

    public async Task<OrderDetailDto?> GetByIdAsync(int orderId)
    {
        using var conn = Conn();
        var order = await conn.QueryFirstOrDefaultAsync<OrderDetailDto>(
            @"SELECT o.order_id AS OrderId, o.order_code AS OrderCode, o.subtotal,
                     o.discount_amount AS DiscountAmount, o.shipping_fee AS ShippingFee,
                     o.total_amount AS TotalAmount, o.order_status AS OrderStatus,
                     o.payment_status AS PaymentStatus, pm.method_name AS PaymentMethodName,
                     o.note, o.created_at AS CreatedAt,
                     u.full_name AS UserFullName, u.email AS UserEmail, u.phone AS UserPhone
              FROM Orders o
              LEFT JOIN PaymentMethods pm ON o.method_id=pm.method_id
              LEFT JOIN Users u ON o.user_id=u.user_id
              WHERE o.order_id=@OrderId",
            new { OrderId = orderId });

        if (order == null) return null;
        await EnrichOrderDetailAsync(conn, order);
        return order;
    }

    private async Task EnrichOrderDetailAsync(SqlConnection conn, OrderDetailDto order)
    {
        var items = await conn.QueryAsync<OrderItemDto>(
            @"SELECT oi.product_id AS ProductId, oi.product_name AS ProductName,
                     oi.quantity AS Quantity, oi.unit_price AS UnitPrice, oi.subtotal AS Subtotal,
                     p.thumbnail_url AS ThumbnailUrl, p.slug AS Slug
              FROM OrderItems oi
              LEFT JOIN Products p ON oi.product_id=p.product_id
              WHERE oi.order_id=@OrderId",
            new { OrderId = order.OrderId });
        order.Items = items.ToList();

        var address = await conn.QueryFirstOrDefaultAsync<AddressDto>(
            @"SELECT ua.address_id AS AddressId, ua.receiver_name AS ReceiverName, ua.phone AS Phone,
                     ua.province AS Province, ua.district AS District, ua.ward AS Ward,
                     ua.street_address AS StreetAddress
              FROM Orders o
              LEFT JOIN UserAddresses ua ON o.address_id=ua.address_id
              WHERE o.order_id=@OrderId",
            new { OrderId = order.OrderId });
        order.ShippingAddress = address;
    }

    public async Task UpdateStatusAsync(int orderId, string newStatus, string? oldStatus, int? changedBy, string? note)
    {
        using var conn = Conn();
        await conn.ExecuteAsync(
            "UPDATE Orders SET order_status=@NewStatus, updated_at=GETDATE() WHERE order_id=@OrderId",
            new { NewStatus = newStatus, OrderId = orderId });
        await conn.ExecuteAsync(
            @"INSERT INTO OrderStatusHistory (order_id, old_status, new_status, changed_by, note, changed_at)
              VALUES (@OrderId, @OldStatus, @NewStatus, @ChangedBy, @Note, GETDATE())",
            new { OrderId = orderId, OldStatus = oldStatus, NewStatus = newStatus, ChangedBy = changedBy, Note = note });
    }

    public async Task<bool> CanReviewAsync(int userId, int productId)
    {
        using var conn = Conn();
        var count = await conn.ExecuteScalarAsync<int>(
            @"SELECT COUNT(*) FROM Orders o
              JOIN OrderItems oi ON o.order_id=oi.order_id
              WHERE o.user_id=@UserId AND oi.product_id=@ProductId AND o.order_status='Hoàn thành'",
            new { UserId = userId, ProductId = productId });
        return count > 0;
    }
}
