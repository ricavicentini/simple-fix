using Oms.Shared.Domain.Enums;

namespace Oms.Shared.Application.Contracts;

public interface IOrderValidator
{
    bool TryValidate(
        string symbol,
        char side,
        int quantity,
        decimal price,
        out string? reason,
        out OrderSide parsedSide);
}
