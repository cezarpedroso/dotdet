namespace BadEfMigration.Persistence;

public sealed class ReservationExportCache
{
    private readonly ReservationDbContext _database;

    public ReservationExportCache(ReservationDbContext database)
    {
        _database = database;
    }

    public int TrackedReservationCount() => _database.Reservations.Local.Count;
}
