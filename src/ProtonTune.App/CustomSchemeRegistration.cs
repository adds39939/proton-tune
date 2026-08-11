using Microsoft.Extensions.DependencyInjection;
using Photino.Blazor;
using ProtonTune.Core.Hosting;

namespace ProtonTune.App;

/// <summary>
/// Connects the window to whatever has asked to serve a URL scheme.
/// </summary>
/// <remarks>
/// This is the only place the two sides meet: the handlers know nothing about Photino, and the
/// host knows nothing about what any of them serve. Adding one is a registration in the layer
/// that owns it, and nothing here changes.
/// </remarks>
internal static class CustomSchemeRegistration
{
    /// <summary>
    /// Registers every <see cref="ICustomSchemeHandler" /> in the container with the window.
    /// </summary>
    /// <remarks>
    /// Must be called before the window is run, since that is when the native window is created
    /// and the set of schemes it will answer to is fixed. Photino aggregates handlers sharing a
    /// scheme, so several may register the same one and each is asked in turn.
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
