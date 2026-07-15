namespace CleanMinimalApi.Domain.Orders;

public sealed record Order(Guid Id, string CustomerName, DateTimeOffset CreatedAt);
