using PacCollector.Infrastructure.Network;

namespace PacCollector.Api.Endpoints;

public sealed record ServerStatus(bool LimsRunning, bool PrintRunning, ushort UdpPort, ushort TcpPort);

public static class ListenerEndpoints
{
    public static void MapListenerEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/server/status", (ListenerManager mgr) =>
            Results.Ok(new ServerStatus(
                LimsRunning: mgr.LimsRunning,
                PrintRunning: mgr.PrintRunning,
                UdpPort: ListenerManager.UdpPort,
                TcpPort: ListenerManager.TcpPort)));

        app.MapPost("/api/listeners/start", (ListenerManager mgr) =>
        {
            mgr.StartLims();
            return Results.NoContent();
        });

        app.MapPost("/api/listeners/stop", (ListenerManager mgr) =>
        {
            mgr.StopLims();
            return Results.NoContent();
        });

        app.MapPost("/api/print-listener/start", (ListenerManager mgr) =>
        {
            mgr.StartPrint();
            return Results.NoContent();
        });

        app.MapPost("/api/print-listener/stop", (ListenerManager mgr) =>
        {
            mgr.StopPrint();
            return Results.NoContent();
        });
    }
}
