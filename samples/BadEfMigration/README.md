# Destructive EF Migration

A reservation API with a normal domain model and DbContext plus one migration
that removes a column in `Up()`. The operation is intentionally present for
calibration and uses no external database.

## Expected DotDet result

- Readiness level: Medium
- Expected score range: 80-92
- Categories exercised: EF Core, Dependency Injection, API Readiness

Expected findings:

- EF migration risk for `DropColumn` in `Up()`.
- DI lifetime risk for a singleton export cache capturing the scoped DbContext.

Findings that should not appear:

- A separate destructive finding caused only by `Down()`.
- Missing DbContext or DbSet findings.
- Missing primary-key findings for `Reservation.Id`.
- DI002 for `ReservationDbContext`, which is registered with `AddDbContext`.
- API003/API004/API005: OpenAPI, health checks, and exception handling are configured.
