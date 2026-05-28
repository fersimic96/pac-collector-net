namespace PacCollector.Infrastructure.Plugins.Builtin;

public sealed record PacFieldSpec(string Key, string Label, string Unit, string Group);

public sealed record PacInstrumentSpec(
    string PluginId,
    string DisplayName,
    string AnalyzerType,
    string Vendor,
    string Version,
    IReadOnlyList<PacFieldSpec> FieldSpecs);
