using PacCollector.Domain.Entities;
using PacCollector.Domain.Errors;
using PacCollector.Domain.ValueObjects;

namespace PacCollector.Domain.Ports;

public interface IInstrumentPlugin
{
    string Id { get; }
    string DisplayName { get; }
    string Version { get; }
    string Vendor { get; }
    IReadOnlyList<string> SupportedTypes { get; }

    Sample ParseMessage(
        ReadOnlySpan<byte> raw,
        string? sourceIp,
        DateTimeOffset receivedAt);

    IReadOnlyDictionary<string, FieldMeta> FieldDescriptions { get; }

    bool IsPrintPlugin => false;

    bool AcceptsPrintFormat(ReadOnlySpan<byte> raw) => false;

    Sample ParsePrintMessage(
        ReadOnlySpan<byte> raw,
        string? sourceIp,
        DateTimeOffset receivedAt)
        => throw new MalformedMessageException("this plugin does not support print mode");
}
