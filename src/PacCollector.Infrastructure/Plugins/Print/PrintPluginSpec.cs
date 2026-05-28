namespace PacCollector.Infrastructure.Plugins.Print;

// spec declarativo de un equipo en modo print. Se carga desde JSON externo.
// para agregar un equipo nuevo basta con un .json en plugins/print/
public sealed class PrintPluginSpec
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string AnalyzerType { get; set; } = "";
    public string Vendor { get; set; } = "";
    public string Version { get; set; } = "";

    public PrintReportKind Kind { get; set; } = PrintReportKind.LabelValue;

    // marcador que aparece en el payload para identificar el equipo (ej. "OptiFZP")
    public string HeaderMarker { get; set; } = "";

    // header regex custom; si es null se construye automatico:
    //   {HeaderMarker}\s+S/N:\s*(\S+)\s+V\s+(\S+)
    public string? HeaderRegexOverride { get; set; }

    // label distintivo del equipo que se extrae como extra (ej. "Freeze point", "Cloud point", "IBP")
    public string HeadlineLabel { get; set; } = "";

    // mapping label-del-print → key-del-Extra (ej. "Stop Temperature" → "StopTemperature")
    public List<PrintLabelMapping> ExtraFieldKeys { get; set; } = new();

    // metadata para la UI (descripciones de campos)
    public List<PrintFieldSpec> FieldSpecs { get; set; } = new();
}

public sealed class PrintLabelMapping
{
    public string Label { get; set; } = "";
    public string Key { get; set; } = "";
}

public sealed class PrintFieldSpec
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public string Unit { get; set; } = "";
    public string Group { get; set; } = "";
}
