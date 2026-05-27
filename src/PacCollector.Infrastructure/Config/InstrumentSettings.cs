namespace PacCollector.Infrastructure.Config;

public sealed class InstrumentSettings
{
    public bool Enabled { get; set; } = true;
    public string? Alias { get; set; }
    public string? OutputDir { get; set; }
    public string? RecentDir { get; set; }
    public bool? ShowKey { get; set; }
    public bool? ShowUnit { get; set; }
    public List<string>? SelectedParameters { get; set; }
    public string? HotFolderDir { get; set; }
    public HotFolderFormat? HotFolderFormat { get; set; }
}
