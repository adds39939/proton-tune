using ProtonTune.Core.Hosting;

namespace ProtonTune.DebugServer;

/// <summary>
/// Serves the private URL schemes over HTTP, so a browser can fetch what the desktop window's
/// registered handlers would have answered.
/// </summary>
/// <remarks>
/// The mirror of the window's own registration: handlers know nothing about either host, so both
/// ask the same objects the same question and only the address differs.
/// </remarks>
internal static class CustomSchemeEndpoints
{
    public static WebApplication MapCustomSchemes(this WebApplication app)
    {
        app.MapGet($"{HttpArtworkService.Prefix}/{{scheme}}/{{**rest}}", (
            string scheme,
            string rest,
            IEnumerable<ICustomSchemeHandler> handlers) =>
        {
            foreach (var handler in handlers.Where(candidate =>
                         string.Equals(candidate.Scheme, scheme, StringComparison.OrdinalIgnoreCase)))
            {
                if (handler.Open($"{scheme}://{rest}") is { } content)
                {
                    return Results.Stream(content.Content, content.ContentType);
                }
            }

            return Results.NotFound();
        });

        return app;
    }
}
