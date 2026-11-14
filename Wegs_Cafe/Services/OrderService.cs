using Wegs_Cafe.Helpers;
using Wegs_Cafe.Models;

namespace Wegs_Cafe.Services;

public class OrderService
{
    public FirebaseRepository<Order> Orders { get; }

    public OrderService(FirebaseService firebase)
    {
        Orders = new FirebaseRepository<Order>(firebase, Constants.Nodes.Orders);
    }

    /// <summary>Builds an order from cart items and saves it.</summary>
    public Task<Order> PlaceOrderAsync(string userId, string customerName, IEnumerable<CartItem> cart, string? notes = null)
    {
        var items = cart.Select(c => new OrderItem
        {
            ProductId = c.Item.Id ?? string.Empty,
            Name = c.Item.Name,
            UnitPrice = c.Item.Price,
            Quantity = c.Quantity,
            Notes = c.Notes
        }).ToList();

        var order = new Order
        {
            UserId = userId,
            CustomerName = customerName,
            Items = items,
            Total = items.Sum(i => i.Subtotal),
            Notes = notes
        };

        return Orders.AddAsync(order);
    }

    public async Task<List<Order>> GetOrdersByUserAsync(string userId)
        => (await Orders.GetAllAsync())
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .ToList();

    public async Task UpdateStatusAsync(Order order, OrderStatus status)
    {
        order.Status = status;
        await Orders.UpdateAsync(order);
    }
}
