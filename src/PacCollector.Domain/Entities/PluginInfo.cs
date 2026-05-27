namespace PacCollector.Domain.Entities;

public abstract record PluginSource
{
    public sealed record Builtin : PluginSource;
    public sealed record External(string Path) : PluginSource;
}

public sealed record PluginInfo(
    string Id,
    string DisplayName,
    string Version,
    string Vendor,
    IReadOnlyList<string> SupportedTypes,
    PluginSource Source,
    bool Enabled)
{
    public bool IsBuiltin => Source is PluginSource.Builtin;
}
