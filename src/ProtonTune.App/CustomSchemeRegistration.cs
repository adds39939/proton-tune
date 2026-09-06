using Microsoft.Extensions.DependencyInjection;
using Photino.Blazor;
using ProtonTune.Core.Hosting;

namespace ProtonTune.App;

/// <summary>
/// Connects the window to whatever has asked to serve a URL scheme.
/// </summary>
/// <remarks>
/// The only place the two sides meet: handlers know nothing about Photino, and the host knows
/// nothing about what they serve, so adding one changes nothing here.
/// </remarks>
internal static class CustomSchemeRegistration
{
    /// <summary>
    /// Registers every <see cref="ICustomSchemeHandler" /> in the container with the window.
    /// </summary>
    /// <remarks>
    /// Must be called before the window runs, which is when the set of schemes is fixed. Photino
    /// aggregates handlers sharing a scheme and asks each in turn.
    /// </remarks>
    public static PhotinoBlazorApp RegisterCustomSchemes(this PhotinoBlazorApp app)
    {
        foreach (var handler in app.Services.GetServices<ICustomSchemeHandler>())
        {
            app.MainWindow.RegisterCustomSchemeHandler(
                handler.Scheme,
                (_, _, url, out contentType) =>
                {
                    var content = handler.Open(url);
                    contentType = content?.ContentType;

                    return content?.Content;
                });
        }

        return app;
    }
}
