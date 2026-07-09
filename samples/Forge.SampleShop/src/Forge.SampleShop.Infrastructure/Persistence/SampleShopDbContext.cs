using Forge.SampleShop.Domain;
using Microsoft.EntityFrameworkCore;

namespace Forge.SampleShop.Infrastructure.Persistence;

public sealed class SampleShopDbContext : DbContext
{
    public SampleShopDbContext(DbContextOptions<SampleShopDbContext> options)
        : base(options)
    {
    }

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<CatalogItem> CatalogItems => Set<CatalogItem>();
}
