namespace Oms.Shared.Application.Models;

public sealed record SnapshotOrder(string Symbol, string Side, int Quantity, decimal Price);
