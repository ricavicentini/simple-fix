using System.Collections.Concurrent;
using QuickFix;
using QuickFix.Fields;
using QuickFix.FIX44;
using QuickFix.Logger;
using QuickFix.Store;
using QuickFix.Transport;

namespace Oms.Client.Infrastructure.Fix;

public sealed class FixClientBridge(ILogger<FixClientBridge> logger) : MessageCracker, IApplication, IHostedService
{
    private readonly ConcurrentDictionary<string, SessionID> _externalByClOrdId = new();
    private IAcceptor? _externalAcceptor;
    private IInitiator? _serverInitiator;
    private SessionID? _serverSession;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var acceptorCfg = Environment.GetEnvironmentVariable("FIX_CLIENT_ACCEPTOR_CONFIG") ?? "fix-client-acceptor.cfg";
        var initiatorCfg = Environment.GetEnvironmentVariable("FIX_CLIENT_INITIATOR_CONFIG") ?? "fix-client-initiator.cfg";
        var acceptorSettings = new SessionSettings(acceptorCfg);
        var initiatorSettings = new SessionSettings(initiatorCfg);

        _externalAcceptor = new ThreadedSocketAcceptor(
            this,
            new MemoryStoreFactory(),
            acceptorSettings,
            new FileLogFactory(acceptorSettings),
            new DefaultMessageFactory());

        _serverInitiator = new SocketInitiator(
            this,
            new MemoryStoreFactory(),
            initiatorSettings,
            new FileLogFactory(initiatorSettings),
            new DefaultMessageFactory());

        _externalAcceptor.Start();
        _serverInitiator.Start();

        logger.LogInformation("Client bridge started");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _serverInitiator?.Stop();
        _externalAcceptor?.Stop();
        return Task.CompletedTask;
    }

    public void OnCreate(SessionID sessionID) { }

    public void OnLogon(SessionID sessionID)
    {
        if (sessionID.TargetCompID == "SERVER")
        {
            _serverSession = sessionID;
        }

        logger.LogInformation("Logon {Session}", sessionID);
    }

    public void OnLogout(SessionID sessionID)
    {
        if (_serverSession?.Equals(sessionID) == true)
        {
            _serverSession = null;
        }

        logger.LogInformation("Logout {Session}", sessionID);
    }

    public void ToAdmin(QuickFix.Message message, SessionID sessionID) { }
    public void ToApp(QuickFix.Message message, SessionID sessionID) { }
    public void FromAdmin(QuickFix.Message message, SessionID sessionID) { }
    public void FromApp(QuickFix.Message message, SessionID sessionID) => Crack(message, sessionID);

    public void OnMessage(NewOrderSingle message, SessionID externalSession)
    {
        var clOrdId = message.ClOrdID.Value;
        _externalByClOrdId[clOrdId] = externalSession;

        if (_serverSession is null)
        {
            SendBridgeReject(externalSession, clOrdId, message.Side.Value, (int)message.OrderQty.Value, "Server offline");
            _externalByClOrdId.TryRemove(clOrdId, out _);
            return;
        }

        Session.SendToTarget(message, _serverSession);
    }

    public void OnMessage(OrderCancelRequest message, SessionID externalSession)
    {
        var cancelClOrdId = message.ClOrdID.Value;
        _externalByClOrdId[cancelClOrdId] = externalSession;

        if (_serverSession is null)
        {
            SendBridgeReject(externalSession, cancelClOrdId, message.Side.Value, 0, "Server offline");
            _externalByClOrdId.TryRemove(cancelClOrdId, out _);
            return;
        }

        Session.SendToTarget(message, _serverSession);
    }

    public void OnMessage(ExecutionReport message, SessionID sessionID)
    {
        var clOrdId = message.ClOrdID.Value;

        if (_externalByClOrdId.TryRemove(clOrdId, out var externalSession))
        {
            Session.SendToTarget(message, externalSession);
        }
    }

    private static void SendBridgeReject(SessionID externalSession, string clOrdId, char side, int qty, string reason)
    {
        var report = new ExecutionReport(
            new OrderID($"ORD-{Guid.NewGuid():N}"),
            new ExecID($"EXE-{Guid.NewGuid():N}"),
            new ExecType(ExecType.REJECTED),
            new OrdStatus(OrdStatus.REJECTED),
            new Symbol("N/A"),
            new Side(side),
            new LeavesQty(0),
            new CumQty(qty),
            new AvgPx(0));

        report.SetField(new ClOrdID(clOrdId));
        report.SetField(new Text(reason));

        Session.SendToTarget(report, externalSession);
    }
}
