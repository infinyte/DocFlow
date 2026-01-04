namespace ECommerce.Domain;

/// <summary>
/// Represents a customer in the e-commerce system.
/// </summary>
public class Customer
{
    public Guid Id { get; init; }
    public required string Name { get; set; }
    public required string Email { get; set; }
    public Address? ShippingAddress { get; set; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// A customer's address - value object with no identity.
/// </summary>
public record Address(
    string Street,
    string City,
    string State,
    string PostalCode,
    string Country);

/// <summary>
/// An order placed by a customer - aggregate root.
/// </summary>
public class Order
{
    public Guid Id { get; init; }
    public Guid CustomerId { get; init; }
    public required OrderStatus Status { get; set; }
    public ICollection<LineItem> LineItems { get; init; } = new List<LineItem>();
    public Money Total => CalculateTotal();
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? ShippedAt { get; set; }

    /// <summary>
    /// Add a product to the order.
    /// </summary>
    public void AddLineItem(Product product, int quantity)
    {
        var lineItem = new LineItem
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            ProductName = product.Name,
            UnitPrice = product.Price,
            Quantity = quantity
        };
        LineItems.Add(lineItem);
    }

    /// <summary>
    /// Calculate the total price of the order.
    /// </summary>
    public Money CalculateTotal()
    {
        var total = LineItems.Sum(li => li.UnitPrice.Amount * li.Quantity);
        var currency = LineItems.FirstOrDefault()?.UnitPrice.Currency ?? "USD";
        return new Money(total, currency);
    }

    /// <summary>
    /// Ship the order.
    /// </summary>
    public void Ship()
    {
        if (Status != OrderStatus.Confirmed)
            throw new InvalidOperationException("Can only ship confirmed orders");
        Status = OrderStatus.Shipped;
        ShippedAt = DateTime.UtcNow;
    }
}

/// <summary>
/// A line item within an order - entity owned by Order aggregate.
/// </summary>
public class LineItem
{
    public Guid Id { get; init; }
    public Guid ProductId { get; init; }
    public required string ProductName { get; set; }
    public required Money UnitPrice { get; set; }
    public int Quantity { get; set; }
}

/// <summary>
/// Represents a monetary amount - value object.
/// </summary>
public record Money(decimal Amount, string Currency)
{
    public static Money Zero(string currency = "USD") => new(0, currency);

    public static Money operator +(Money a, Money b)
    {
        if (a.Currency != b.Currency)
            throw new InvalidOperationException("Cannot add money with different currencies");
        return new Money(a.Amount + b.Amount, a.Currency);
    }
}

/// <summary>
/// A product available for purchase.
/// </summary>
public class Product
{
    public Guid Id { get; init; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required Money Price { get; set; }
    public required string Sku { get; set; }
    public int StockQuantity { get; set; }
}

/// <summary>
/// Status of an order throughout its lifecycle.
/// </summary>
public enum OrderStatus
{
    Draft,
    Confirmed,
    Shipped,
    Delivered,
    Cancelled
}

/// <summary>
/// Event raised when an order is placed.
/// </summary>
public record OrderPlacedEvent(
    Guid OrderId,
    Guid CustomerId,
    decimal TotalAmount,
    DateTime OccurredAt);

/// <summary>
/// Repository interface for persisting orders.
/// </summary>
public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Order>> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task SaveAsync(Order order, CancellationToken cancellationToken = default);
}
