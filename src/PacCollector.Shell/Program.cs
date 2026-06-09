using System.Diagnostics;
using PacCollector.Shell;
using Velopack;

// hook de velopack: maneja install/uninstall/update hooks del installer.
// si la app NO esta instalada via velopack es no-op.
VelopackApp.Build().Run();

// PacCollector.Shell.exe es un LAUNCHER liviano:
//   1) chequea si PacCollector.Api.exe esta corriendo. Si no, lo arranca en background.
//   2) espera a que /api/health responda.
//   3) abre el browser default apuntando a http://127.0.0.1:5174 (la UI).
//   4) sale. El Api sigue corriendo en background como cualquier otro proceso.
//
// Esta arquitectura reemplaza la idea original de Photino+WebView2 que daba pantalla
// en blanco en algunas PCs Windows (path del usuario con espacios, WebView2 cache
// flaky, etc). Usar el browser nativo del usuario es 100% confiable, soporta DevTools,
// es lo que se conoce, y mantiene exactamente la misma UX (atajo en menu inicio
// abre la "app").
//
// El Api persiste en background. Cerrar la ventana del browser NO mata el Api.
// Para apagar el Api: Administrador de tareas -> PacCollector.Api.exe -> finalizar.

const string DefaultApiUrl = "http://127.0.0.1:5174";
var apiUrl = Environment.GetEnvironmentVariable("PAC_SHELL_URL") ?? DefaultApiUrl;
const string ServiceName = "PacCollector";

// ── log a archivo (winexe sin consola) ──
var logDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "PacCollector",
    "logs");
Directory.CreateDirectory(logDir);
var logPath = Path.Combine(logDir, $"shell-{DateTime.Now:yyyyMMdd-HHmmss}.log");
void Log(string msg)
{
    try { File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}{Environment.NewLine}"); } catch { }
}
Log($"launcher starting; target URL: {apiUrl}");

// 1) si el Api esta corriendo, salteamos el arranque
if (!await ApiReadyAsync(TimeSpan.FromMilliseconds(800)))
{
    Log("Api not running, starting...");
    if (OperatingSystem.IsWindows() && ServiceController.IsServiceInstalled(ServiceName))
    {
        Log("Service installed, SCM TryStart");
        ServiceController.TryStart(ServiceName);
    }
    else
    {
        Log("Spawning sibling Api.exe");
        ApiLauncher.SpawnSibling();
    }
    var ready = await WaitForApiAsync(TimeSpan.FromSeconds(15));
    Log($"Api ready after wait: {ready}");
    if (!ready)
    {
        Log("ERROR: Api no respondio en 15s, abrimos browser igual");
    }
}
else
{
    Log("Api was already running");
}

// 2) abrir el browser default con la URL. ProcessStartInfo + UseShellExecute=true
// hace que Windows abra el browser default registrado para http:// (Edge/Chrome/Firefox).
try
{
    Log($"opening browser at {apiUrl}");
    Process.Start(new ProcessStartInfo
    {
        FileName = apiUrl,
        UseShellExecute = true,
    });
    Log("browser launched, exiting");
}
catch (Exception e)
{
    Log($"failed to launch browser: {e.Message}");
}

// 3) salimos inmediato. El Api sigue corriendo en background.
return 0;

async Task<bool> ApiReadyAsync(TimeSpan timeout)
{
    using var http = new HttpClient { Timeout = timeout };
    try
    {
        var res = await http.GetAsync(apiUrl + "/api/health");
        return res.IsSuccessStatusCode;
    }
    catch { return false; }
}

async Task<bool> WaitForApiAsync(TimeSpan timeout)
{
    var deadline = DateTime.UtcNow + timeout;
    while (DateTime.UtcNow < deadline)
    {
        if (await ApiReadyAsync(TimeSpan.FromMilliseconds(500))) return true;
        await Task.Delay(250);
    }
    return false;
}
