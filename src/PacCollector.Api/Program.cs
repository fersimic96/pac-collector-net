using System.Text.Json;
using System.Text.Json.Serialization;
using PacCollector.Api.Endpoints;
using PacCollector.Api.Services;
using PacCollector.Application.Services;
using PacCollector.Application.UseCases;
using PacCollector.Domain.Ports;
using PacCollector.Infrastructure.Config;
using PacCollector.Infrastructure.EventBus;
using PacCollector.Infrastructure.Filesystem;
using PacCollector.Infrastructure.Network;
using PacCollector.Infrastructure.Persistence;
using PacCollector.Infrastructure.Plugins;

var builder = WebApplication.CreateBuilder(args);

// dual-mode: con --service corre como Windows Service. Sin args corre standalone Kestrel.
if (args.Contains("--service"))
    builder.Host.UseWindowsService();
builder.WebHost.UseUrls("http://127.0.0.1:5174");

// ── data paths (resueltos contra el directorio del usuario) ──
var dataDir = ResolveDataDir(builder.Configuration);
var dbDir = Path.Combine(dataDir, "db");
var recentDir = Path.Combine(dataDir, "recent");
var configPath = Path.Combine(dataDir, "settings.json");
var instrumentsPath = Path.Combine(dataDir, "instruments.json");
var limsPluginsOverrideDir = Path.Combine(dataDir, "plugins", "lims");
var printPluginsOverrideDir = Path.Combine(dataDir, "plugins", "print");
Directory.CreateDirectory(dataDir);
Directory.CreateDirectory(dbDir);
Directory.CreateDirectory(recentDir);
Directory.CreateDirectory(limsPluginsOverrideDir);
Directory.CreateDirectory(printPluginsOverrideDir);

// ── singletons del lado infrastructure ──
var configStore = ConfigStore.Load(configPath);
var instrumentRepo = JsonInstrumentRepository.Load(instrumentsPath);
var pluginRegistry = PluginRegistryImpl.LoadBuiltin(limsPluginsOverrideDir, printPluginsOverrideDir);
var eventBus = new ChannelEventBus();

builder.Services.AddSingleton(configStore);
builder.Services.AddSingleton<IInstrumentRepository>(instrumentRepo);
builder.Services.AddSingleton<IPluginRegistry>(pluginRegistry);
builder.Services.AddSingleton(pluginRegistry); // impl concreta para uploads/reload
builder.Services.AddSingleton<ChannelEventBus>(eventBus);
builder.Services.AddSingleton<IEventBus>(eventBus);
builder.Services.AddSingleton<ISampleRepository>(_ => new InMemorySampleRepository());
builder.Services.AddSingleton<IFileWriter>(sp =>
    new FileWriterImpl(dbDir, recentDir, sp.GetRequiredService<ConfigStore>()));

// ── application services ──
builder.Services.AddSingleton<SampleProcessingService>();
builder.Services.AddSingleton<HandleBeaconUseCase>();
builder.Services.AddSingleton<ReceiveSampleUseCase>();
builder.Services.AddSingleton<ReceivePrintUseCase>();
builder.Services.AddSingleton<ListInstrumentsUseCase>();
builder.Services.AddSingleton<ListSamplesUseCase>();
builder.Services.AddSingleton<UpdateInstrumentAliasUseCase>();

// ── network listener manager ──
builder.Services.AddSingleton(sp => new ListenerManager(
    sp.GetRequiredService<ConfigStore>(),
    sp.GetRequiredService<HandleBeaconUseCase>(),
    sp.GetRequiredService<ReceiveSampleUseCase>(),
    sp.GetRequiredService<ReceivePrintUseCase>(),
    log: msg => Console.WriteLine($"[net] {msg}")));

// ── api helpers ──
builder.Services.AddSingleton<NetworkInfoService>();
builder.Services.AddSingleton<SystemService>();
builder.Services.AddSingleton(sp => new PluginUploadService(
    sp.GetRequiredService<PluginRegistryImpl>(),
    limsPluginsOverrideDir,
    printPluginsOverrideDir));

// JSON: camelCase para propiedades + snake_case para enums (matchea convencion del frontend)
// NO ignorar nulls: el frontend espera que todos los campos opcionales esten presentes con null
builder.Services.ConfigureHttpJsonOptions(opt =>
{
    opt.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    opt.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
    opt.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
});

// CORS permisivo solo para el frontend local
builder.Services.AddCors(opt => opt.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();
app.UseCors();
app.UseWebSockets();

// servir el frontend buildeado (cuando exista wwwroot/)
var wwwroot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
if (Directory.Exists(wwwroot))
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

// arrancar los listeners si la config dice auto_start_server
if (configStore.Snapshot().General.AutoStartServer)
{
    app.Services.GetRequiredService<ListenerManager>().StartLims();
    if (configStore.Snapshot().General.PrintServerEnabled)
        app.Services.GetRequiredService<ListenerManager>().StartPrint();
}

// ── routes ──
app.MapHealthEndpoints();
app.MapSampleEndpoints();
app.MapInstrumentEndpoints();
app.MapPluginEndpoints();
app.MapConfigEndpoints();
app.MapListenerEndpoints();
app.MapNetworkEndpoints();
app.MapSystemEndpoints();
app.MapWebSocketEndpoints();

app.Run();

static string ResolveDataDir(IConfiguration config)
{
    var fromConfig = config["DataDir"];
    if (!string.IsNullOrWhiteSpace(fromConfig)) return fromConfig;
    var basePath = OperatingSystem.IsWindows()
        ? Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)
        : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    return Path.Combine(basePath, "PacCollector");
}

// para WebApplicationFactory<Program> en los tests
public partial class Program { }
