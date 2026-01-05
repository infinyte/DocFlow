namespace SampleCdm;

public class Product  // Should map to Pet
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public ProductCategory Category { get; set; } = null!;
    public decimal Price { get; set; }
    public ProductStatus Status { get; set; }
}

public class ProductCategory  // Should map to Category
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
}

public enum ProductStatus { Available, Pending, Sold }

public class Order
{
    public Guid OrderId { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public DateTime OrderDate { get; set; }
    public OrderStatus Status { get; set; }
}

public enum OrderStatus { Placed, Approved, Delivered }
