using BadEfMigration.Domain.Reservations;
using BadEfMigration.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddJsonConsole();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddDbContext<ReservationDbContext>(options => options.UseInMemoryDatabase("Reservations"));
builder.Services.AddSingleton<ReservationExportCache>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.MapOpenApi();
app.MapHealthChecks("/health");
app.MapGet("/api/reservations", async (ReservationDbContext database) =>
    Results.Ok(await database.Reservations.AsNoTracking().ToListAsync()));
app.MapPost("/api/reservations", async (Reservation reservation, ReservationDbContext database) =>
{
    database.Reservations.Add(reservation);
    await database.SaveChangesAsync();
    return Results.Created($"/api/reservations/{reservation.Id}", reservation);
});

app.Run();
