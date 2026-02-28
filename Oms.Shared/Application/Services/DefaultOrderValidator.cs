using Oms.Shared.Application.Contracts;
using Oms.Shared.Domain.Enums;

namespace Oms.Shared.Application.Services;

public sealed class DefaultOrderValidator : IOrderValidator
{
    public bool TryValidate(
        string symbol,
        char side,
        int quantity,
        decimal price,
        out string? reason,
        out OrderSide parsedSide)
    {
        parsedSide = side switch
        {
            '1' => OrderSide.Buy,
            '2' => OrderSide.Sell,
            _ => default
        };

        if (symbol is not ("PETR4" or "VALE3"))
        {
            reason = "Invalid symbol";
            return false;
        }

        if (side is not ('1' or '2'))
        {
            reason = "Invalid side";
            return false;
        }

        if (quantity <= 0 || quantity >= 100000)
        {
            reason = "Invalid quantity";
            return false;
        }

        if (price <= 0 || price >= 1000)
        {
            reason = "Invalid price";
            return false;
        }

        if (price != decimal.Round(price, 2))
        {
            reason = "Price must have 2 decimal places";
            return false;
        }

        reason = null;
        return true;
    }
}
