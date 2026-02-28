using Oms.Shared.Application.Models;
using Oms.Shared.Application.Services;

namespace Oms.Tests;

public class OrderRequirementsTests
{
    [Fact]
    public void NewOrder_WithValidFields_ShouldBeAccepted()
    {
        var book = CreateBook();

        var result = book.Add("A1", "PETR4", '1', 10, 99.99m);

        Assert.True(result.Ok);
        Assert.Null(result.Error);
        Assert.Single(book.GetSnapshot());
    }

    [Theory]
    [InlineData("ABCD4", '1', 10, 10.00, "Invalid symbol")]
    [InlineData("PETR4", '3', 10, 10.00, "Invalid side")]
    [InlineData("PETR4", '1', 0, 10.00, "Invalid quantity")]
    [InlineData("PETR4", '1', 100000, 10.00, "Invalid quantity")]
    [InlineData("PETR4", '1', 1, 0.00, "Invalid price")]
    [InlineData("PETR4", '1', 1, 1000.00, "Invalid price")]
    [InlineData("PETR4", '1', 1, 10.001, "Price must have 2 decimal places")]
    public void NewOrder_WithInvalidFields_ShouldBeRejected(
        string symbol,
        char side,
        int quantity,
        decimal price,
        string expectedError)
    {
        var book = CreateBook();

        var result = book.Add("A1", symbol, side, quantity, price);

        Assert.False(result.Ok);
        Assert.Equal(expectedError, result.Error);
        Assert.Empty(book.GetSnapshot());
    }

    [Fact]
    public void NewOrder_WithDuplicateClOrdId_ShouldBeRejected()
    {
        var book = CreateBook();

        var first = book.Add("DUP1", "PETR4", '1', 10, 10.01m);
        var second = book.Add("DUP1", "VALE3", '2', 20, 20.02m);

        Assert.True(first.Ok);
        Assert.False(second.Ok);
        Assert.Equal("Duplicate ClOrdID", second.Error);
        Assert.Single(book.GetSnapshot());
    }

    [Fact]
    public void CancelOrder_WhenOrderExists_ShouldRemoveOrder()
    {
        var book = CreateBook();
        book.Add("C1", "PETR4", '1', 5, 30.00m);

        var canceled = book.Cancel("C1");

        Assert.True(canceled);
        Assert.Empty(book.GetSnapshot());
    }

    [Fact]
    public void CancelOrder_WhenOrderDoesNotExist_ShouldFail()
    {
        var book = CreateBook();

        var canceled = book.Cancel("NOT_FOUND");

        Assert.False(canceled);
    }

    [Fact]
    public void Snapshot_ShouldBeGroupedAndOrderedByRequirement()
    {
        var book = CreateBook();

        // PETR4/BUY: same price keeps time priority (B2 before B3)
        book.Add("B1", "PETR4", '1', 1, 10.00m);
        book.Add("B2", "PETR4", '1', 2, 9.00m);
        book.Add("B3", "PETR4", '1', 3, 9.00m);

        // PETR4/SELL
        book.Add("S1", "PETR4", '2', 4, 8.00m);

        // VALE3/SELL
        book.Add("S2", "VALE3", '2', 5, 7.00m);

        var snapshot = book.GetSnapshot();

        Assert.Equal(5, snapshot.Count);
        Assert.Equal(new SnapshotOrder("PETR4", "BUY", 2, 9.00m), snapshot[0]);
        Assert.Equal(new SnapshotOrder("PETR4", "BUY", 3, 9.00m), snapshot[1]);
        Assert.Equal(new SnapshotOrder("PETR4", "BUY", 1, 10.00m), snapshot[2]);
        Assert.Equal(new SnapshotOrder("PETR4", "SELL", 4, 8.00m), snapshot[3]);
        Assert.Equal(new SnapshotOrder("VALE3", "SELL", 5, 7.00m), snapshot[4]);
    }

    [Fact]
    public void Snapshot_ShouldContainOnlyLiveOrders()
    {
        var book = CreateBook();

        book.Add("A1", "PETR4", '1', 1, 11.00m);
        book.Add("A2", "VALE3", '2', 2, 12.00m);
        book.Cancel("A1");

        var snapshot = book.GetSnapshot();

        Assert.Single(snapshot);
        Assert.Equal(new SnapshotOrder("VALE3", "SELL", 2, 12.00m), snapshot[0]);
    }

    private static OrderBook CreateBook() => new(new DefaultOrderValidator());
}
