using Oms.Shared.Domain.Enums;

namespace Oms.Shared.Domain.Entities;

public sealed record LiveOrder(
    string ClOrdId,
    string Symbol,
    OrderSide Side,
    int Quantity,
    decimal Price,
    long Sequence);
