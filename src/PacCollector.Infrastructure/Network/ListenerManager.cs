using System.Net;
using PacCollector.Application.UseCases;
using PacCollector.Infrastructure.Config;

namespace PacCollector.Infrastructure.Network;

// orquesta el ciclo de vida de los 3 servers (UDP/TCP/Print). Permite arrancar y
// parar los listeners desde la UI (start_listeners / stop_listeners commands).
public sealed class ListenerManager
{
    private readonly ConfigStore _config;
    private readonly HandleBeaconUseCase _handleBeacon;
    private readonly ReceiveSampleUseCase _receiveSample;
    private readonly ReceivePrintUseCase _receivePrint;
    private readonly Action<string>? _log;

    private CancellationTokenSource? _limsCts;
    private CancellationTokenSource? _printCts;
    private Task? _udpTask;
    private Task? _tcpTask;
    private Task? _printTask;
    private readonly Lock _gate = new();

    public ListenerManager(
        ConfigStore config,
        HandleBeaconUseCase handleBeacon,
        ReceiveSampleUseCase receiveSample,
        ReceivePrintUseCase receivePrint,
        Action<string>? log = null)
    {
        _config = config;
        _handleBeacon = handleBeacon;
        _receiveSample = receiveSample;
        _receivePrint = receivePrint;
        _log = log;
    }

    public const ushort UdpPort = 3000;
    public const ushort TcpPort = 9980;

    public bool LimsRunning
    {
        get { lock (_gate) return _udpTask is not null || _tcpTask is not null; }
    }

    public bool PrintRunning
    {
        get { lock (_gate) return _printTask is not null; }
    }

    public void StartLims()
    {
        lock (_gate)
        {
            if (_udpTask is not null || _tcpTask is not null) return;
            _limsCts = new CancellationTokenSource();

            var cfg = _config.Snapshot();
            var udpBind = new IPEndPoint(IPAddress.Any, UdpPort);
            var tcpBind = new IPEndPoint(IPAddress.Any, TcpPort);
            var udp = new UdpServer(udpBind, cfg.General.SelectedIp, TcpPort, _handleBeacon, _log);
            var tcp = new TcpServer(tcpBind, _receiveSample, _log);
            _udpTask = Task.Run(() => udp.RunAsync(_limsCts.Token));
            _tcpTask = Task.Run(() => tcp.RunAsync(_limsCts.Token));
        }
    }

    public void StopLims()
    {
        Task? udp, tcp;
        CancellationTokenSource? cts;
        lock (_gate)
        {
            cts = _limsCts;
            udp = _udpTask;
            tcp = _tcpTask;
            _limsCts = null;
            _udpTask = null;
            _tcpTask = null;
        }
        cts?.Cancel();
        try { udp?.GetAwaiter().GetResult(); } catch { /* ignore */ }
        try { tcp?.GetAwaiter().GetResult(); } catch { /* ignore */ }
        cts?.Dispose();
    }

    public void StartPrint()
    {
        lock (_gate)
        {
            if (_printTask is not null) return;
            _printCts = new CancellationTokenSource();
            var cfg = _config.Snapshot();
            var bind = new IPEndPoint(IPAddress.Any, cfg.General.PrintPort);
            var print = new PrintServer(bind, _receivePrint, _log);
            _printTask = Task.Run(() => print.RunAsync(_printCts.Token));
        }
    }

    public void StopPrint()
    {
        Task? print;
        CancellationTokenSource? cts;
        lock (_gate)
        {
            cts = _printCts;
            print = _printTask;
            _printCts = null;
            _printTask = null;
        }
        cts?.Cancel();
        try { print?.GetAwaiter().GetResult(); } catch { /* ignore */ }
        cts?.Dispose();
    }

    public void StopAll()
    {
        StopLims();
        StopPrint();
    }
}
