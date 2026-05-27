namespace PacCollector.Infrastructure.Config;

public sealed class GeneralSettings
{
    public string Delimiter { get; set; } = ";";
    public string Eol { get; set; } = "<none>";
    public bool ShowKey { get; set; } = true;
    public bool ShowUnit { get; set; }
    public bool ShowAnalyzerSn { get; set; } = true;
    public bool ShowSampleId { get; set; } = true;
    public bool ShowStartTime { get; set; } = true;
    public string? DbDir { get; set; }
    public string? RecentDir { get; set; }
    public uint RecentKeep { get; set; } = 50;
    public string? SelectedIp { get; set; }
    public bool AutoStartServer { get; set; } = true;
    public bool PrintServerEnabled { get; set; }
    public ushort PrintPort { get; set; } = 631;
}

public static class EolTranslator
{
    public static string Translate(string eol) => eol switch
    {
        "CR" => "\r",
        "LF" => "\n",
        "CR-LF" => "\r\n",
        _ => string.Empty,
    };
}
