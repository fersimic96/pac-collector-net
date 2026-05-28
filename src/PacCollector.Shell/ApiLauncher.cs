using System.Diagnostics;

namespace PacCollector.Shell;

// si el Api no esta corriendo como service, lo arrancamos como child process
// del Shell. busca el binario al lado del Shell (publish output).
internal static class ApiLauncher
{
    public static void SpawnSibling()
    {
        var shellDir = AppContext.BaseDirectory;
        var apiName = OperatingSystem.IsWindows() ? "PacCollector.Api.exe" : "PacCollector.Api";
        var apiPath = Path.Combine(shellDir, apiName);
        if (!File.Exists(apiPath)) return;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = apiPath,
                WorkingDirectory = shellDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            Process.Start(psi);
        }
        catch { /* best-effort */ }
    }
}
