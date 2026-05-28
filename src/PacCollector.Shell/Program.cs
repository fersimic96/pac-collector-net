using PacCollector.Shell;
using Photino.NET;
using Velopack;

// hook de velopack: maneja install/uninstall/update hooks del installer.
// si la app NO esta instalada via velopack es no-op.
VelopackApp.Build().Run();

const string ApiUrl = "http://127.0.0.1:5174";
const string HealthUrl = ApiUrl + "/api/health";
const string AppTitle = "PAC Collector";
const string ServiceName = "PacCollector";

// 1) si el API service esta corriendo, abrimos directo la ventana sobre el.
// 2) si no, lo arrancamos: SCM en Windows, sino spawn del binario .Api colateral.
if (!await ApiReadyAsync(timeout: TimeSpan.FromMilliseconds(800)))
{
    if (OperatingSystem.IsWindows() && ServiceController.IsServiceInstalled(ServiceName))
        ServiceController.TryStart(ServiceName);
    else
        ApiLauncher.SpawnSibling();

    // esperar hasta 10s a que el api responda
    await WaitForApiAsync(timeout: TimeSpan.FromSeconds(10));
}

var window = new PhotinoWindow()
    .SetTitle(AppTitle)
    .SetUseOsDefaultSize(false)
    .SetSize(1280, 800)
    .SetResizable(true)
    .Center()
    .Load(new Uri(ApiUrl));

window.WaitForClose();

static async Task<bool> ApiReadyAsync(TimeSpan timeout)
{
    using var http = new HttpClient { Timeout = timeout };
    try
    {
        var res = await http.GetAsync(HealthUrl);
        return res.IsSuccessStatusCode;
    }
    catch { return false; }
}

static async Task WaitForApiAsync(TimeSpan timeout)
{
    var deadline = DateTime.UtcNow + timeout;
    while (DateTime.UtcNow < deadline)
    {
        if (await ApiReadyAsync(TimeSpan.FromMilliseconds(500))) return;
        await Task.Delay(250);
    }
}
