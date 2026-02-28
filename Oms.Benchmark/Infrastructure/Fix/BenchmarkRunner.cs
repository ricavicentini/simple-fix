using System.Collections.Concurrent;
using System.Diagnostics;
using QuickFix;
using QuickFix.Fields;
using QuickFix.FIX44;
using QuickFix.Logger;
using QuickFix.Store;
using QuickFix.Transport;

namespace Oms.Benchmark.Infrastructure.Fix;

public sealed class BenchmarkRunner : MessageCracker, IApplication
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _pending = new();
    private SessionID? _session;
    private IInitiator? _initiator;

    public async Task RunAsync()
    {
        var configFile = Environment.GetEnvironmentVariable("BENCHMARK_CONFIG") ?? "benchmark-client.cfg";
        var configPath = Path.Combine(AppContext.BaseDirectory, configFile);
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException(
                $"FIX config not found at '{configPath}'. Build the project and ensure benchmark-client.cfg is copied to output.");
        }

        var settings = new SessionSettings(configPath);
        _initiator = new SocketInitiator(this, new MemoryStoreFactory(), settings, CreateLogFactory(settings), new DefaultMessageFactory());
        _initiator.Start();

        var timeout = DateTime.UtcNow.AddSeconds(10);
        while (_session is null && DateTime.UtcNow < timeout)
        {
            await Task.Delay(50);
        }

        if (_session is null)
        {
            throw new Exception("Session not connected. Start server and client first.");
        }

        const int count = 100000;
        var sw = Stopwatch.StartNew();

        for (var i = 0; i < count; i++)
        {
            var clOrdId = $"B{i}";
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending[clOrdId] = tcs;

            var msg = new NewOrderSingle(new ClOrdID(clOrdId), new Symbol("PETR4"), new Side(Side.BUY), new TransactTime(DateTime.UtcNow), new OrdType(OrdType.LIMIT));
            msg.SetField(new OrderQty(1));
            msg.SetField(new Price(10.01m));

            Session.SendToTarget(msg, _session);

            var completed = await Task.WhenAny(tcs.Task, Task.Delay(5000));
            if (completed != tcs.Task)
            {
                throw new Exception($"Timeout waiting response for {clOrdId}");
            }
        }

        sw.Stop();
        var avgMs = sw.Elapsed.TotalMilliseconds / count;

        Console.WriteLine($"Total: {sw.Elapsed.TotalMilliseconds:F2} ms");
        Console.WriteLine($"Avg round-trip: {avgMs:F4} ms");

        _initiator.Stop();
    }

    public void OnCreate(SessionID sessionID) { }
    public void OnLogon(SessionID sessionID) => _session = sessionID;
    public void OnLogout(SessionID sessionID) => _session = null;
    public void ToAdmin(QuickFix.Message message, SessionID sessionID) { }
    public void ToApp(QuickFix.Message message, SessionID sessionID) { }
    public void FromAdmin(QuickFix.Message message, SessionID sessionID) { }
    public void FromApp(QuickFix.Message message, SessionID sessionID) => Crack(message, sessionID);

    public void OnMessage(ExecutionReport message, SessionID sessionID)
    {
        var clOrdId = message.ClOrdID.Value;
        if (_pending.TryRemove(clOrdId, out var tcs))
        {
            tcs.TrySetResult(true);
        }
    }

    private static ILogFactory CreateLogFactory(SessionSettings settings)
    {
        if (IsFixFileLogDisabled())
        {
            return new NullLogFactory();
        }

        return new FileLogFactory(settings);
    }

    private static bool IsFixFileLogDisabled()
    {
        var value = Environment.GetEnvironmentVariable("FIX_DISABLE_FILE_LOG");
        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }
}
