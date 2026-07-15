using BadEfMigration.Domain.Reservations;
using Microsoft.EntityFrameworkCore;

namespace BadEfMigration.Persistence;

public sealed class ReservationDbContext(DbContextOptions<ReservationDbContext> options) : DbContext(options)
{
    public DbSet<Reservation> Reservations => Set<Reservation>();
}
