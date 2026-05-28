using System.Net;
using System.Net.Sockets;
using System.Text;
using PacCollector.Application.UseCases;
using PacCollector.Domain.ValueObjects;

namespace PacCollector.Infrastructure.Network;

// servidor TCP que recibe payloads LIMS Ethernet. Los equipos PAC abren la conexion,
// mandan JSON terminado en NUL byte (0x00) y esperan {"Error":"","SaveCheckSum":"XXXX"}.
public sealed class TcpServer
{
    private readonly IPEndPoint _bindAddr;
    private readonly ReceiveSampleUseCase _receiveSample;
    private readonly Action<string>? _log;

    public TcpServer(
        IPEndPoint bindAddr,
        ReceiveSampleUseCase receiveSample,
        Action<string>? log = null)
    {
        _bindAddr = bindAddr;
        _receiveSample = receiveSample;
        _log = log;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        var listener = new TcpListener(_bindAddr);
        listener.Start();
        _log?.Invoke($"TCP listening on {_bindAddr}");
        try
        {
            while (!ct.IsCancellationRequested)
            {
                TcpClient client;
                try { client = await listener.AcceptTcpClientAsync(ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
                _ = HandleConnectionAsync(client, ct);
            }
        }
        finally
        {
            listener.Stop();
        }
    }

    private async Task HandleConnectionAsync(TcpClient client, CancellationToken ct)
    {
        var remote = client.Client.RemoteEndPoint as IPEndPoint;
        var remoteIp = remote?.Address.ToString();
        _log?.Invoke($">>> TCP CONNECTED from {remote}");

        try
        {
            using (client)
            {
                using var stream = client.GetStream();
                var buf = await ReadUntilNullAsync(stream, ct).ConfigureAwait(false);

                TcpLimsResponse response;
                try
                {
                    var checksum = await _receiveSample.ExecuteAsync(buf, remoteIp, ct).ConfigureAwait(false);
                    response = TcpLimsResponse.Ok(checksum.AsString);
                }
                catch (Exception e) when (e is not OutOfMemoryException and not OperationCanceledException)
                {
                    _log?.Invoke($"TCP from {remote}: process error: {e.Message}");
                    // siempre respondemos checksum del raw, incluso si el procesamiento fallo
                    var checksum = PacChecksum.FromBytes(buf.Span);
                    response = TcpLimsResponse.Nack(checksum.AsString);
                }

                var json = response.ToCompactJson();
                var bytes = Encoding.UTF8.GetBytes(json);
                await stream.WriteAsync(bytes, ct).ConfigureAwait(false);
                try { client.Client.Shutdown(SocketShutdown.Both); } catch { /* best-effort */ }
            }
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            _log?.Invoke($"TCP {remote}: {e.Message}");
        }
    }

    // lee bytes hasta encontrar el NUL terminator o cerrar la conexion.
    // timeout per-read de 5s para evitar conexiones cuelgues.
    private static async Task<ReadOnlyMemory<byte>> ReadUntilNullAsync(
        NetworkStream stream,
        CancellationToken ct)
    {
        var buf = new List<byte>(8192);
        var chunk = new byte[Protocol.TcpReadChunk];

        while (true)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(Protocol.TcpReadTimeoutMs);
            int read;
            try
            {
                read = await stream.ReadAsync(chunk, cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                break;
            }
            if (read == 0) break;

            var idx = Array.IndexOf(chunk, Protocol.NullTerminator, 0, read);
            if (idx >= 0)
            {
                buf.AddRange(chunk[..idx]);
                break;
            }
            buf.AddRange(chunk[..read]);
        }

        return buf.ToArray();
    }
}
