using Microsoft.EntityFrameworkCore;

namespace RiskyMinimalApi.Core.Inventory;

public sealed class InventoryDbContext(DbContextOptions<InventoryDbContext> options) : DbContext(options)
{
    public DbSet<InventoryItem> Inventory => Set<InventoryItem>();
}
