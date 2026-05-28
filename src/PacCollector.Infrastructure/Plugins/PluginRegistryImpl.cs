using System.Collections.Concurrent;
using PacCollector.Domain.Entities;
using PacCollector.Domain.Ports;
using PacCollector.Infrastructure.Plugins.Builtin;
using PacCollector.Infrastructure.Plugins.Print;

namespace PacCollector.Infrastructure.Plugins;

// registro central de todos los plugins de equipos. Combina:
//   - plugins LIMS JSON: uno por cada PacInstrumentSpec builtin (7 equipos)
//   - plugins print: uno por cada PrintPluginSpec cargado de JSON
// FindForType: busca un plugin no-print por AnalyzerType
// FindForPrint: itera los plugins print preguntando AcceptsPrintFormat
public sealed class PluginRegistryImpl : IPluginRegistry
{
    private readonly List<RegisteredPlugin> _plugins;

    public PluginRegistryImpl(IEnumerable<IInstrumentPlugin> plugins)
    {
        _plugins = plugins.Select(p => new RegisteredPlugin(p, true)).ToList();
    }

    // factory recomendada: carga los 7 LIMS + 3 print desde recursos embebidos
    public static PluginRegistryImpl LoadBuiltin(string? printPluginsOverrideDir = null)
    {
        var plugins = new List<IInstrumentPlugin>();
        foreach (var spec in BuiltinSpecs.All)
            plugins.Add(new PacFamilyPlugin(spec));
        foreach (var spec in PrintPluginSpecLoader.LoadAll(printPluginsOverrideDir))
            plugins.Add(new ConfigurablePrintPlugin(spec));
        return new PluginRegistryImpl(plugins);
    }

    public IInstrumentPlugin? FindForType(string analyzerType)
    {
        foreach (var reg in _plugins)
        {
            if (!reg.Enabled) continue;
            if (reg.Plugin.IsPrintPlugin) continue;
            if (reg.Plugin.SupportedTypes.Contains(analyzerType, StringComparer.Ordinal))
                return reg.Plugin;
        }
        return null;
    }

    public IInstrumentPlugin? FindForPrint(ReadOnlyMemory<byte> raw)
    {
        foreach (var reg in _plugins)
        {
            if (!reg.Enabled) continue;
            if (!reg.Plugin.IsPrintPlugin) continue;
            if (reg.Plugin.AcceptsPrintFormat(raw)) return reg.Plugin;
        }
        return null;
    }

    public IReadOnlyList<PluginInfo> List()
        => _plugins
            .Select(r => new PluginInfo(
                Id: r.Plugin.Id,
                DisplayName: r.Plugin.DisplayName,
                Version: r.Plugin.Version,
                Vendor: r.Plugin.Vendor,
                SupportedTypes: r.Plugin.SupportedTypes,
                Source: new PluginSource.Builtin(),
                Enabled: r.Enabled))
            .ToList();

    public void SetEnabled(string id, bool enabled)
    {
        var reg = _plugins.FirstOrDefault(r => string.Equals(r.Plugin.Id, id, StringComparison.Ordinal));
        if (reg is not null) reg.Enabled = enabled;
    }

    private sealed class RegisteredPlugin
    {
        public IInstrumentPlugin Plugin { get; }
        public bool Enabled { get; set; }
        public RegisteredPlugin(IInstrumentPlugin plugin, bool enabled)
        {
            Plugin = plugin;
            Enabled = enabled;
        }
    }
}
