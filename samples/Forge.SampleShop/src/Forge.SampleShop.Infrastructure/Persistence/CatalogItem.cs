namespace Forge.SampleShop.Infrastructure.Persistence;

// Intentional smell for DotDet: exposed through DbSet but missing Id/CatalogItemId/[Key].
public sealed class CatalogItem
{
    public string Sku { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}
