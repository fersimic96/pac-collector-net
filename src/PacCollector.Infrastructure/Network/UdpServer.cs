using System.Net;
using System.Net.Sockets;
using PacCollector.Application.UseCases;

namespace PacCollector.Infrastructure.Network;

// escucha la baliza UDP del equipo en :3000 y responde "ACK <ip> <tcp_port>"
// configuredIp: si no es null, se usa esa IP en el ACK. Si es null, se detecta
// la IP local cara al remoto.
public sealed class UdpServer
{
    private readonly IPEndPoint _bindAddr;
    private readonly string? _configuredIp;
    private readonly ushort _tcpPort;
    private readonly HandleBeaconUseCase _handleBeacon;
    private readonly Action<string>? _log;

    public UdpServer(
        IPEndPoint bindAddr,
        string? configuredIp,
        ushort tcpPort,
        HandleBeaconUseCase handleBeacon,
        Action<string>? log = null)
    {
        _bindAddr = bindAddr;
        _configuredIp = configuredIp;
        _tcpPort = tcpPort;
        _handleBeacon = handleBeacon;
        _log = log;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        using var udp = new UdpClient(_bindAddr);
        try { udp.EnableBroadcast = true; } catch { /* best-effort */ }
        _log?.Invoke($"UDP listening on {_bindAddr}");

        while (!ct.IsCancellationRequested)
        {
            UdpReceiveResult result;
            try
            {
                result = await udp.ReceiveAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                _log?.Invoke($"UDP recv error: {e.Message}");
                continue;
            }

            var data = result.Buffer;
            var remote = result.RemoteEndPoint;

            if (IsBeacon(data))
            {
                var localIp = _configuredIp
                    ?? LocalIpForRemote(remote)?.ToString()
                    ?? "127.0.0.1";
                var ack = Protocol.BuildAck(localIp, _tcpPort);
                try
                {
                    var ackBytes = System.Text.Encoding.ASCII.GetBytes(ack);
                    await udp.SendAsync(ackBytes, remote, ct).ConfigureAwait(false);
                    _log?.Invoke($"ACK to {remote} → IP={localIp}");
                    _handleBeacon.Execute(remote.Address.ToString());
                }
                catch (Exception e) when (e is not OutOfMemoryException)
                {
                    _log?.Invoke($"UDP send ACK to {remote}: {e.Message}");
                }
            }
            else
            {
                _log?.Invoke($"UDP non-beacon from {remote} ({data.Length}B)");
                try { await udp.SendAsync(Protocol.Nak, remote, ct).ConfigureAwait(false); }
                catch { /* best-effort NAK */ }
            }
        }
    }

    private static bool IsBeacon(ReadOnlySpan<byte> data)
        => data.Length == Protocol.Beacon.Length && data.SequenceEqual(Protocol.Beacon);

    // determina la IP local que ve el remoto: abre un socket UDP "connected"
    // sin mandar nada y lee la local_addr asignada por el sistema.
    private static IPAddress? LocalIpForRemote(IPEndPoint remote)
    {
        try
        {
            using var probe = new Socket(remote.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            probe.Connect(remote);
            return (probe.LocalEndPoint as IPEndPoint)?.Address;
        }
        catch
        {
            return null;
        }
    }
}
