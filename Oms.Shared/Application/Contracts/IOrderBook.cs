using Oms.Shared.Application.Models;

namespace Oms.Shared.Application.Contracts;

public interface IOrderBook
{
    (bool Ok, string? Error) Add(string clOrdId, string symbol, char side, int qty, decimal price);
    bool Cancel(string clOrdId);
    IReadOnlyList<SnapshotOrder> GetSnapshot();
}
