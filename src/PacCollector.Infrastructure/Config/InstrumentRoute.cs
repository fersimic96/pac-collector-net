namespace PacCollector.Infrastructure.Config;

public sealed class InstrumentRoute
{
    public HotFolderFormat? HotFolderFormat { get; set; }
    public string? HotFolderDir { get; set; }
    public string? Alias { get; set; }
}
