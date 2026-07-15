namespace BadEfMigration.Domain.Reservations;

public sealed class Reservation
{
    public Guid Id { get; set; }

    public required string GuestName { get; set; }

    public DateOnly ArrivalDate { get; set; }

    public string? LegacyConfirmationCode { get; set; }
}
