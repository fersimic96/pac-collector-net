namespace PacCollector.Domain.Ports;

public abstract record DomainEvent
{
    public sealed record BeaconReceived(string Ip, DateTimeOffset Ts) : DomainEvent;

    public sealed record InstrumentDiscovered(
        string Serial,
        string AnalyzerType,
        string? Ip) : DomainEvent;

    public sealed record InstrumentTouched(
        string Serial,
        string? Ip) : DomainEvent;

    public sealed record SampleReceived(
        string Uuid,
        string Serial,
        string SampleIdentifier,
        double? Ibp,
        double? Fbp) : DomainEvent;

    public sealed record SampleDuplicateSkipped(
        string Serial,
        string SampleIdentifier) : DomainEvent;

    public sealed record PluginParseFailed(
        string AnalyzerType,
        string Reason) : DomainEvent;

    public sealed record UnknownPayloadReceived(
        string? AnalyzerType,
        string? SourceIp,
        ulong Bytes,
        string Reason,
        string SavedPath) : DomainEvent;

    public sealed record PersistenceFailed(
        string Stage,
        string? Serial,
        string? SampleIdentifier,
        string Reason) : DomainEvent;

    public sealed record ServerError(string Message) : DomainEvent;
}
