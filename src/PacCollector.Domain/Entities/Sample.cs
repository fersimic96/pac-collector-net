using PacCollector.Domain.ValueObjects;

namespace PacCollector.Domain.Entities;

public sealed class Sample
{
    public string Uuid { get; set; } = string.Empty;
    public AnalyzerSerial Serial { get; set; }
    public string AnalyzerType { get; set; } = string.Empty;
    public string SampleIdentifier { get; set; } = string.Empty;

    public string? Operator { get; set; }
    public string? Program { get; set; }

    public DateTimeOffset? StartAt { get; set; }
    public DateTimeOffset? EndAt { get; set; }

    public double? Ibp { get; set; }
    public double? Fbp { get; set; }
    public double? Residue { get; set; }
    public double? Recovery { get; set; }
    public double? FbpVolume { get; set; }

    public bool? EndOfTest { get; set; }
    public ulong? AlarmBitmask { get; set; }

    public DistillationCurve Curve { get; set; } = DistillationCurve.Empty();

    public SortedDictionary<string, string> Extra { get; set; } = new(StringComparer.Ordinal);

    public string? SourceIp { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public string RawJson { get; set; } = string.Empty;

    public bool IsComplete() => EndOfTest ?? false;
    public bool HasAlarms() => AlarmBitmask is { } b && b != 0;
    public bool HasCurve() => !Curve.IsEmpty;
}
