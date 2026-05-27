using PacCollector.Application.Services;
using PacCollector.Domain.ValueObjects;

namespace PacCollector.Application.UseCases;

public sealed class ReceiveSampleUseCase
{
    private readonly SampleProcessingService _processing;

    public ReceiveSampleUseCase(SampleProcessingService processing) => _processing = processing;

    public async Task<PacChecksum> ExecuteAsync(
        ReadOnlyMemory<byte> raw,
        string? sourceIp,
        CancellationToken ct = default)
    {
        var checksum = PacChecksum.FromBytes(raw.Span);
        await _processing.ProcessRawMessageAsync(raw, sourceIp, ct);
        return checksum;
    }
}
