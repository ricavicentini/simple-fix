using System.Collections.Concurrent;
using Oms.Shared.Application.Contracts;
using Oms.Shared.Application.Models;
using Oms.Shared.Domain.Entities;
using Oms.Shared.Domain.Enums;

namespace Oms.Shared.Application.Services;

public sealed class OrderBook(IOrderValidator validator) : IOrderBook
{
    private readonly ConcurrentDictionary<string, LiveOrder> _orders = new();
    private long _sequence;

    public (bool Ok, string? Error) Add(string clOrdId, string symbol, char side, int qty, decimal price)
    {
        if (!validator.TryValidate(symbol, side, qty, price, out var reason, out var parsedSide))
        {
            return (false, reason);
        }

        var seq = Interlocked.Increment(ref _sequence);
        var order = new LiveOrder(clOrdId, symbol, parsedSide, qty, price, seq);
        var added = _orders.TryAdd(clOrdId, order);
        return added ? (true, null) : (false, "Duplicate ClOrdID");
    }

    public bool Cancel(string clOrdId) => _orders.TryRemove(clOrdId, out _);

    public IReadOnlyList<SnapshotOrder> GetSnapshot()
    {
        return _orders.Values
            .GroupBy(o => new { o.Symbol, o.Side })
            .OrderBy(g => g.Key.Symbol)
            .ThenBy(g => g.Key.Side)
            .SelectMany(g => g
                .OrderBy(o => o.Price)
                .ThenBy(o => o.Sequence)
                .Select(o => new SnapshotOrder(
                    o.Symbol,
                    o.Side == OrderSide.Buy ? "BUY" : "SELL",
                    o.Quantity,
                    o.Price)))
            .ToList();
    }
}
