using System.Text;
using System.Text.Json;
using PacCollector.Domain.Entities;
using PacCollector.Domain.Errors;
using PacCollector.Domain.Ports;
using PacCollector.Domain.ValueObjects;

namespace PacCollector.Application.Services;

// upsert instrumento, dedup, guardar, escribir archivos, emitir evento
public sealed class SampleProcessingService
{
    private readonly IPluginRegistry _plugins;
    private readonly ISampleRepository _samples;
    private readonly IInstrumentRepository _instruments;
    private readonly IFileWriter _files;
    private readonly IEventBus _events;

    public SampleProcessingService(
        IPluginRegistry plugins,
        ISampleRepository samples,
        IInstrumentRepository instruments,
        IFileWriter files,
        IEventBus events)
    {
        _plugins = plugins;
        _samples = samples;
        _instruments = instruments;
        _files = files;
        _events = events;
    }

    public async Task<bool> ProcessRawMessageAsync(
        ReadOnlyMemory<byte> raw,
        string? sourceIp,
        CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        string? rawText = null;
        try { rawText = Encoding.UTF8.GetString(raw.Span); }
        catch { rawText = null; }

        string? analyzerType = null;
        JsonDocument? parsed = null;
        if (rawText is not null)
        {
            try
            {
                parsed = JsonDocument.Parse(rawText);
                if (parsed.RootElement.TryGetProperty("AnalyzerType", out var t) && t.ValueKind == JsonValueKind.String)
                    analyzerType = t.GetString()?.Trim();
            }
            catch { parsed = null; }
        }

        var plugin = analyzerType is null ? null : _plugins.FindForType(analyzerType);
        if (analyzerType is null || plugin is null)
        {
            var reason = analyzerType is null
                ? (rawText is null ? "invalid UTF-8 in payload"
                    : parsed is null ? "invalid JSON"
                    : "missing AnalyzerType field")
                : $"no plugin registered for AnalyzerType '{analyzerType}'";
            var saved = await _files.WriteUnknownPayloadAsync(raw, analyzerType, sourceIp, reason, now, ct);
            _events.Publish(new DomainEvent.UnknownPayloadReceived(
                AnalyzerType: analyzerType,
                SourceIp: sourceIp,
                Bytes: (ulong)raw.Length,
                Reason: reason,
                SavedPath: saved.Path));
            parsed?.Dispose();
            return false;
        }
        parsed?.Dispose();

        Sample sample;
        try
        {
            sample = plugin.ParseMessage(raw, sourceIp, now);
        }
        catch (DomainException e)
        {
            _events.Publish(new DomainEvent.PluginParseFailed(analyzerType, e.Message));
            throw;
        }

        if (string.IsNullOrWhiteSpace(sample.SampleIdentifier))
            sample.SampleIdentifier = SynthesizeSampleId(sample);

        await UpsertInstrumentAsync(sample, analyzerType, sourceIp, now, ct);

        if (await _samples.ExistsForRunAsync(sample.Serial.AsString, sample.SampleIdentifier, sample.StartAt, ct))
        {
            _events.Publish(new DomainEvent.SampleDuplicateSkipped(
                Serial: sample.Serial.AsString,
                SampleIdentifier: sample.SampleIdentifier));
            return false;
        }

        await RunPersistStepAsync("save_sample", sample,
            () => _samples.SaveReceivedSampleAsync(sample, ct));
        await RunPersistStepAsync("increment_sample_count", sample,
            () => _instruments.IncrementSampleCountAsync(sample.Serial.AsString, ct));
        await RunPersistStepAsync("write_artifacts", sample,
            () => _files.WriteSampleArtifactsAsync(sample, ct));

        _events.Publish(new DomainEvent.SampleReceived(
            Uuid: sample.Uuid,
            Serial: sample.Serial.AsString,
            SampleIdentifier: sample.SampleIdentifier,
            Ibp: sample.Ibp,
            Fbp: sample.Fbp));

        return true;
    }

    public async Task<bool> ProcessPrintMessageAsync(
        ReadOnlyMemory<byte> raw,
        string? sourceIp,
        CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        var plugin = _plugins.FindForPrint(raw);
        if (plugin is null)
        {
            const string reason = "no print plugin matched";
            var saved = await _files.WriteUnknownPayloadAsync(raw, null, sourceIp, reason, now, ct);
            _events.Publish(new DomainEvent.UnknownPayloadReceived(
                AnalyzerType: null,
                SourceIp: sourceIp,
                Bytes: (ulong)raw.Length,
                Reason: reason,
                SavedPath: saved.Path));
            return false;
        }

        Sample sample;
        try
        {
            sample = plugin.ParsePrintMessage(raw, sourceIp, now);
        }
        catch (DomainException e)
        {
            _events.Publish(new DomainEvent.PluginParseFailed("print", e.Message));
            throw;
        }

        var analyzerType = sample.AnalyzerType;

        if (string.IsNullOrWhiteSpace(sample.SampleIdentifier))
            sample.SampleIdentifier = SynthesizeSampleId(sample);

        await UpsertInstrumentAsync(sample, analyzerType, sourceIp, now, ct);

        if (await _samples.ExistsForRunAsync(sample.Serial.AsString, sample.SampleIdentifier, sample.StartAt, ct))
        {
            _events.Publish(new DomainEvent.SampleDuplicateSkipped(
                Serial: sample.Serial.AsString,
                SampleIdentifier: sample.SampleIdentifier));
            return false;
        }

        await RunPersistStepAsync("save_sample[print]", sample,
            () => _samples.SaveReceivedSampleAsync(sample, ct));
        await RunPersistStepAsync("increment_sample_count[print]", sample,
            () => _instruments.IncrementSampleCountAsync(sample.Serial.AsString, ct));
        await RunPersistStepAsync("write_artifacts[print]", sample,
            () => _files.WriteSampleArtifactsAsync(sample, ct));

        _events.Publish(new DomainEvent.SampleReceived(
            Uuid: sample.Uuid,
            Serial: sample.Serial.AsString,
            SampleIdentifier: sample.SampleIdentifier,
            Ibp: sample.Ibp,
            Fbp: sample.Fbp));

        return true;
    }

    private async Task UpsertInstrumentAsync(
        Sample sample,
        string analyzerType,
        string? sourceIp,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var existing = await _instruments.FindBySerialAsync(sample.Serial.AsString, ct);
        Instrument instrument;
        if (existing is not null)
        {
            existing.Touch(sourceIp, now);
            instrument = existing;
        }
        else
        {
            instrument = Instrument.NewDiscovered(
                AnalyzerSerial.Create(sample.Serial.AsString),
                analyzerType,
                sourceIp,
                now);
            _events.Publish(new DomainEvent.InstrumentDiscovered(
                Serial: sample.Serial.AsString,
                AnalyzerType: analyzerType,
                Ip: sourceIp));
        }
        await _instruments.UpsertOnContactAsync(instrument, ct);
    }

    private async Task RunPersistStepAsync(string stage, Sample sample, Func<Task> step)
    {
        try
        {
            await step();
        }
        catch (Exception e)
        {
            _events.Publish(new DomainEvent.PersistenceFailed(
                Stage: stage,
                Serial: sample.Serial.AsString,
                SampleIdentifier: sample.SampleIdentifier,
                Reason: e.Message));
            throw;
        }
    }

    // genera un id deterministico cuando el equipo no manda SampleIdentifier
    private static string SynthesizeSampleId(Sample sample)
    {
        var seed = new StringBuilder();
        seed.Append(sample.Serial.AsString);
        seed.Append('|');
        seed.Append(sample.StartAt?.ToUnixTimeMilliseconds() ?? 0);
        seed.Append('|');
        seed.Append(sample.ReceivedAt.ToUnixTimeMilliseconds());
        var hash = (ulong)seed.ToString().GetHashCode();
        var token = hash.ToString("x");
        return $"auto-{token[..Math.Min(token.Length, 10)]}";
    }
}
