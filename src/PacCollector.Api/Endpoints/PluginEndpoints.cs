using PacCollector.Domain.Ports;

namespace PacCollector.Api.Endpoints;

public sealed record SetPluginEnabledRequest(bool Enabled);

public static class PluginEndpoints
{
    public static void MapPluginEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/plugins", (IPluginRegistry plugins) => Results.Ok(plugins.List()));

        app.MapPatch("/api/plugins/{id}/enabled",
            (string id, SetPluginEnabledRequest req, IPluginRegistry plugins) =>
            {
                plugins.SetEnabled(id, req.Enabled);
                return Results.NoContent();
            });
    }
}
