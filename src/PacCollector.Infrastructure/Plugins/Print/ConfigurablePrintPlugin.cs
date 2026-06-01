using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using PacCollector.Domain.Entities;
using PacCollector.Domain.Errors;
using PacCollector.Domain.Ports;
using PacCollector.Domain.ValueObjects;

namespace PacCollector.Infrastructure.Plugins.Print;

// plugin agnostico de equipo para reportes en modo print/Iris.
// el "perfil" del equipo (header marker, mapping de labels, fields) viene de un
// PrintPluginSpec cargado desde JSON. Para agregar un equipo nuevo basta con un .json,
// no hace falta tocar este archivo salvo que necesite un parser kind nuevo.
public sealed class ConfigurablePrintPlugin : IInstrumentPlugin
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static readonly string[] DateFormats =
    {
        "d MMM yyyy H:mm",
        "d MMM yyyy HH:mm",
    };

    private readonly PrintPluginSpec _spec;
    private readonly Regex _headerRegex;
    private readonly IReadOnlyDictionary<string, FieldMeta> _fields;

    public ConfigurablePrintPlugin(PrintPluginSpec spec)
    {
        _spec = spec;
        _headerRegex = string.IsNullOrEmpty(spec.HeaderRegexOverride)
            ? PrintRegex.BuildHeaderRegex(spec.HeaderMarker)
            : new Regex(spec.HeaderRegexOverride, RegexOptions.Compiled);

        var fields = new Dictionary<string, FieldMeta>(StringComparer.Ordinal);
        foreach (var f in CommonPrintFields)
            fields[f.Key] = new FieldMeta(f.Label, f.Unit, f.Group);
        foreach (var f in spec.FieldSpecs)
            fields[f.Key] = new FieldMeta(f.Label, f.Unit, f.Group);
        _fields = fields;
    }

    public string Id => _spec.Id;
    public string DisplayName => _spec.DisplayName;
    public string Version => _spec.Version;
    public string Vendor => _spec.Vendor;
    public IReadOnlyList<string> SupportedTypes => new[] { _spec.AnalyzerType };
    public IReadOnlyDictionary<string, FieldMeta> FieldDescriptions => _fields;
    public bool IsPrintPlugin => true;

    public bool AcceptsPrintFormat(ReadOnlyMemory<byte> raw)
    {
        var head = raw.Span[..Math.Min(raw.Length, 8192)];
        var text = Encoding.UTF8.GetString(head);
        return text.Contains(_spec.HeaderMarker, StringComparison.Ordinal);
    }

    public Sample ParseMessage(ReadOnlyMemory<byte> raw, string? sourceIp, DateTimeOffset receivedAt)
        => throw new MalformedMessageException(
            $"{_spec.Id} only handles print-mode payloads, not LIMS JSON");

    public Sample ParsePrintMessage(ReadOnlyMemory<byte> raw, string? sourceIp, DateTimeOffset receivedAt)
    {
        var rawText = Encoding.UTF8.GetString(raw.Span);
        var cleaned = PclStripper.Strip(rawText);

        var headerMatch = _headerRegex.Match(cleaned);
        if (!headerMatch.Success)
            throw new MalformedMessageException(
                $"{_spec.HeaderMarker} header not found in print payload");

        var serialRaw = headerMatch.Groups[1].Value.Trim();
        var firmware = headerMatch.Groups[2].Value.Trim();
        var serial = AnalyzerSerial.Create(serialRaw);

        return _spec.Kind switch
        {
            PrintReportKind.LabelValue => ParseLabelValue(cleaned, rawText, serial, firmware, sourceIp, receivedAt),
            PrintReportKind.Distillation => ParseDistillation(cleaned, rawText, serial, firmware, sourceIp, receivedAt),
            _ => throw new MalformedMessageException($"unsupported print kind: {_spec.Kind}"),
        };
    }

    // ── label:value (cubre FZP/CPP y cualquier equipo con reporte simple) ──
    private Sample ParseLabelValue(
        string cleaned,
        string rawText,
        AnalyzerSerial serial,
        string firmware,
        string? sourceIp,
        DateTimeOffset receivedAt)
    {
        var startAt = ResultDateOrNull(cleaned);
        var operatorName = CaptureFirst(cleaned, PrintRegex.Operator(), 1);
        var sampleIdentifier = CaptureFirst(cleaned, PrintRegex.SampleId(), 1) ?? "";
        var program = CaptureFirst(cleaned, PrintRegex.Product(), 1);

        var extra = new SortedDictionary<string, string>(StringComparer.Ordinal);
        if (firmware.Length > 0) extra["FirmwareVersion"] = firmware;

        foreach (var (label, key) in CommonLabelToExtra)
        {
            var v = CaptureLabel(cleaned, label);
            if (v is not null) extra[key] = v;
        }
        var cycleMatch = PrintRegex.CycleResult().Match(cleaned);
        if (cycleMatch.Success)
        {
            extra["Cycle"] = cycleMatch.Groups[1].Value;
            extra["ResultIndex"] = cycleMatch.Groups[2].Value;
        }

        var headlineKey = SanitizeKey(_spec.HeadlineLabel);
        var headlineValue = CaptureLabel(cleaned, _spec.HeadlineLabel);
        if (headlineValue is not null) extra[headlineKey] = headlineValue;

        foreach (var map in _spec.ExtraFieldKeys)
        {
            var v = LabelMappingExtractor.Extract(map, cleaned);
            if (v is not null) extra[map.Key] = v;
        }

        // adjuntar el bloque HP-GL crudo si esta presente
        var hpglIdx = rawText.IndexOf("%1BIN;", StringComparison.Ordinal);
        if (hpglIdx >= 0) extra["hpgl_curve"] = rawText[hpglIdx..];

        bool? endOfTest = null;
        if (extra.TryGetValue("Ending", out var ending) && ending.Length > 0)
            endOfTest = ending.Contains("detected", StringComparison.Ordinal);

        return new Sample
        {
            Uuid = Guid.NewGuid().ToString(),
            Serial = serial,
            AnalyzerType = _spec.AnalyzerType,
            SampleIdentifier = sampleIdentifier,
            Operator = operatorName,
            Program = program,
            StartAt = startAt,
            EndAt = null,
            Ibp = null,
            Fbp = null,
            Residue = null,
            Recovery = null,
            FbpVolume = null,
            EndOfTest = endOfTest,
            AlarmBitmask = null,
            Curve = DistillationCurve.Empty(),
            Extra = extra,
            SourceIp = sourceIp,
            ReceivedAt = receivedAt,
            RawJson = rawText,
        };
    }

    // ── distillation (cubre PMD y cualquier equipo con tabla %Recuperado vs T°) ──
    private Sample ParseDistillation(
        string cleaned,
        string rawText,
        AnalyzerSerial serial,
        string firmware,
        string? sourceIp,
        DateTimeOffset receivedAt)
    {
        var fields = TwoColumnFieldCollector.Collect(cleaned);

        fields.TryGetValue("Operator", out var operatorName);
        fields.TryGetValue("Sample ID", out var sampleIdentifier);
        fields.TryGetValue("Product", out var program);

        var startAt = PmdRunDateOrNull(cleaned) ?? ResultDateOrNull(cleaned);

        var table = DistillationTableParser.Parse(cleaned);

        var extra = new SortedDictionary<string, string>(StringComparer.Ordinal);
        if (firmware.Length > 0) extra["FirmwareVersion"] = firmware;
        foreach (var map in _spec.ExtraFieldKeys)
        {
            // si el mapping tiene Pattern, evaluar regex sobre el texto completo.
            // si no, mirar el dict de dos columnas (mas eficiente y respeta el layout).
            string? value;
            if (!string.IsNullOrEmpty(map.Pattern))
                value = LabelMappingExtractor.Extract(map, cleaned);
            else
                value = fields.TryGetValue(map.Label, out var v) && v.Length > 0 ? v : null;

            if (value is not null) extra[map.Key] = value;
        }
        var hpglIdx = rawText.IndexOf("%1BIN;", StringComparison.Ordinal);
        if (hpglIdx >= 0) extra["hpgl_curve"] = rawText[hpglIdx..];

        DistillationCurve curve;
        try { curve = DistillationCurve.Create(table.CurvePoints); }
        catch (InvalidCurvePointException) { curve = DistillationCurve.Empty(); }

        bool? endOfTest = (table.Ibp is not null && table.Fbp is not null) ? true : null;

        return new Sample
        {
            Uuid = Guid.NewGuid().ToString(),
            Serial = serial,
            AnalyzerType = _spec.AnalyzerType,
            SampleIdentifier = sampleIdentifier ?? "",
            Operator = operatorName,
            Program = program,
            StartAt = startAt,
            EndAt = null,
            Ibp = table.Ibp,
            Fbp = table.Fbp,
            Residue = table.ResiduePct,
            Recovery = table.RecoveryPct,
            FbpVolume = null,
            EndOfTest = endOfTest,
            AlarmBitmask = null,
            Curve = curve,
            Extra = extra,
            SourceIp = sourceIp,
            ReceivedAt = receivedAt,
            RawJson = rawText,
        };
    }

    // ── helpers ──

    private static string? CaptureFirst(string text, Regex re, int group)
    {
        var m = re.Match(text);
        if (!m.Success || m.Groups.Count <= group) return null;
        var v = m.Groups[group].Value.Trim();
        return v.Length == 0 ? null : v;
    }

    private static string? CaptureLabel(string text, string label)
    {
        var pat = $@"(?m)^\s*{Regex.Escape(label)}\s*:\s*(.+?)\s*$";
        Regex re;
        try { re = new Regex(pat); }
        catch (ArgumentException) { return null; }
        var m = re.Match(text);
        if (!m.Success) return null;
        return TwoColumnFieldCollector.CleanValue(m.Groups[1].Value);
    }

    private static DateTimeOffset? ResultDateOrNull(string cleaned)
        => DateOrNull(PrintRegex.ResultDate(), cleaned);

    private static DateTimeOffset? PmdRunDateOrNull(string cleaned)
        => DateOrNull(PrintRegex.PmdRunDate(), cleaned);

    private static DateTimeOffset? DateOrNull(Regex re, string cleaned)
    {
        var m = re.Match(cleaned);
        if (!m.Success || m.Groups.Count < 3) return null;
        var combined = $"{m.Groups[1].Value.Trim()} {m.Groups[2].Value.Trim()}";
        if (DateTime.TryParseExact(
                combined, DateFormats, Inv,
                DateTimeStyles.AllowInnerWhite | DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var dt))
            return new DateTimeOffset(dt, TimeSpan.Zero);
        return null;
    }

    // PascalCase del label: "Freeze point" → "FreezePoint", "Cloud point" → "CloudPoint"
    private static string SanitizeKey(string label)
    {
        var sb = new StringBuilder(label.Length);
        var upperNext = true;
        foreach (var c in label)
        {
            if (char.IsLetterOrDigit(c))
            {
                if (upperNext) { sb.Append(char.ToUpperInvariant(c)); upperNext = false; }
                else sb.Append(c);
            }
            else upperNext = true;
        }
        return sb.ToString();
    }

    // metadata UI compartida entre todos los plugins print
    private static readonly (string Key, string Label, string Unit, string Group)[] CommonPrintFields =
    {
        ("FirmwareVersion", "Firmware", "", "Identificación"),
        ("StopTemperature", "Temperatura de parada", "°C", "Configuración"),
        ("RoundedResult", "Resultado redondeado", "", "Configuración"),
        ("Ending", "Estado final", "", "Resultado"),
        ("Cycle", "Ciclo", "", "Resultado"),
        ("ResultIndex", "Índice de resultado", "", "Resultado"),
        ("HeadSN", "Serie del cabezal", "", "Identificación"),
        ("hpgl_curve", "Gráfico HP-GL crudo", "", "Curva"),
    };

    private static readonly (string Label, string Key)[] CommonLabelToExtra =
    {
        ("Stop Temperature", "StopTemperature"),
        ("Rounded result", "RoundedResult"),
        ("Ending", "Ending"),
        ("HeadSN", "HeadSN"),
    };
}
