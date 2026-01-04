namespace Domain.Domain;

public enum OrderStatus
{
    Draft,
    Confirmed,
    Shipped,
    Delivered,
    Cancelled
}

public interface IOrderRepository
{
}

public record Address(
    string Street,
    string City,
    string State,
    string PostalCode,
    string Country);

public record Money(
    decimal Amount,
    string Currency)
{
    public static Money Zero(object currency)
    {
        throw new NotImplementedException();
    }
}

public record OrderPlacedEvent(
    Guid OrderId,
    Guid CustomerId,
    decimal TotalAmount,
    DateTime OccurredAt);

public class Customer
{
    public Guid Id { get; init; }
    public string Name { get; set; }
    public string Email { get; set; }
    public Address ShippingAddress { get; set; }
    public DateTime CreatedAt { get; init; }
}

public class LineItem
{
    public Guid Id { get; init; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; }
    public Money UnitPrice { get; set; }
    public int Quantity { get; set; }
}

public class Product
{
    public Guid Id { get; init; }
    public string Name { get; set; }
    public string Description { get; set; }
    public Money Price { get; set; }
    public string Sku { get; set; }
    public int StockQuantity { get; set; }
}

public class Order
{
    public Guid Id { get; init; }
    public Guid CustomerId { get; set; }
    public OrderStatus Status { get; set; }
    public ICollection<LineItem> LineItems { get; init; } = new List<LineItem>();
    public Money Total { get; set; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ShippedAt { get; set; }

    public void AddLineItem(object product, object quantity)
    {
    }
    public Money CalculateTotal()
    {
        throw new NotImplementedException();
    }
    public void Ship()
    {
    }
}
