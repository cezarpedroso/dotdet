namespace Forge.SampleShop.Domain;

public sealed class OrderLine
{
    public Guid Id { get; set; }

    public string Sku { get; set; } = string.Empty;

    public int Quantity { get; set; }
}
