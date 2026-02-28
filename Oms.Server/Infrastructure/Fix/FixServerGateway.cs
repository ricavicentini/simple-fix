using Oms.Shared.Application.Contracts;
using QuickFix;
using QuickFix.Fields;
using QuickFix.Logger;
using QuickFix.Store;

namespace Oms.Server.Infrastructure.Fix;

public sealed class FixServerGateway(IOrderBook store, ILogger<FixServerGateway> logger)
    : MessageCracker, IApplication, IHostedService
{
    private IAcceptor? _acceptor;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var cfg = Environment.GetEnvironmentVariable("FIX_SERVER_CONFIG") ?? "fix-server.cfg";
        var settings = new SessionSettings(cfg);
        _acceptor = new ThreadedSocketAcceptor(
            this,
            new MemoryStoreFactory(),
            settings,
            new FileLogFactory(settings),
            new DefaultMessageFactory());

        _acceptor.Start();
        logger.LogInformation("FIX server started with config {Config}", cfg);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _acceptor?.Stop();
        return Task.CompletedTask;
    }

    public void OnCreate(SessionID sessionID) { }
    public void OnLogon(SessionID sessionID) => logger.LogInformation("Logon {Session}", sessionID);
    public void OnLogout(SessionID sessionID) => logger.LogInformation("Logout {Session}", sessionID);
    public void ToAdmin(QuickFix.Message message, SessionID sessionID) { }
    public void ToApp(QuickFix.Message message, SessionID sessionID) { }
    public void FromAdmin(QuickFix.Message message, SessionID sessionID) { }

    public void FromApp(QuickFix.Message message, SessionID sessionID) => Crack(message, sessionID);

    public void OnMessage(QuickFix.FIX44.NewOrderSingle message, SessionID sessionID)
    {
        var clOrdId = message.ClOrdID.Value;
        var symbol = message.Symbol.Value;
        var side = message.Side.Value;
        var qty = (int)message.OrderQty.Value;
        var price = message.Price.Value;

        var result = store.Add(clOrdId, symbol, side, qty, price);

        if (result.Ok)
        {
            SendExecutionReport(sessionID, clOrdId, symbol, side, qty, ExecType.NEW, OrdStatus.NEW, "Accepted");
            return;
        }

        SendExecutionReport(sessionID, clOrdId, symbol, side, qty, ExecType.REJECTED, OrdStatus.REJECTED, result.Error ?? "Rejected");
    }

    public void OnMessage(QuickFix.FIX44.OrderCancelRequest message, SessionID sessionID)
    {
        var cancelClOrdId = message.ClOrdID.Value;
        var targetClOrdId = message.IsSetField(Tags.OrigClOrdID)
            ? message.OrigClOrdID.Value
            : cancelClOrdId;

        var side = message.Side.Value;

        if (store.Cancel(targetClOrdId))
        {
            SendExecutionReport(sessionID, cancelClOrdId, "N/A", side, 0, ExecType.CANCELED, OrdStatus.CANCELED, "Canceled");
            return;
        }

        SendExecutionReport(sessionID, cancelClOrdId, "N/A", side, 0, ExecType.REJECTED, OrdStatus.REJECTED, "Order not found");
    }

    private static void SendExecutionReport(
        SessionID sessionID,
        string clOrdId,
        string symbol,
        char side,
        int qty,
        char execType,
        char ordStatus,
        string text)
    {
        var report = new QuickFix.FIX44.ExecutionReport(
            new OrderID($"ORD-{Guid.NewGuid():N}"),
            new ExecID($"EXE-{Guid.NewGuid():N}"),
            new ExecType(execType),
            new OrdStatus(ordStatus),
            new Symbol(symbol),
            new Side(side),
            new LeavesQty(0),
            new CumQty(qty),
            new AvgPx(0));

        report.SetField(new ClOrdID(clOrdId));
        report.SetField(new Text(text));
        Session.SendToTarget(report, sessionID);
    }
}
