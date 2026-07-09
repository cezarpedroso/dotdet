using Microsoft.EntityFrameworkCore;

namespace Forge.SampleShop.Domain;

// Intentional smell for DotDet: the Domain layer references EF Core.
public sealed class Order
{
    public Guid OrderId { get; set; }

    public string CustomerEmail { get; set; } = string.Empty;

    public List<OrderLine> Lines { get; set; } = new();

    public void ConfigureWithFramework(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>().HasKey(order => order.OrderId);
    }
}
