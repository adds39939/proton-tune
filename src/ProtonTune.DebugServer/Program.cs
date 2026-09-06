using Microsoft.AspNetCore.Components.Web;
using ProtonTune.Core.Hosting;
using ProtonTune.DebugServer;
using ProtonTune.DebugServer.Components;
using ProtonTune.Services.DependencyExtensions;
using ProtonTune.Services.Steam;
using ProtonTune.UI.DependencyExtensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services
    .AddProtonTuneServices()
    .AddProtonTuneUI();

builder.Services.Decorate<IGameArtworkService, HttpArtworkService>();

var app = builder.Build();

app.MapStaticAssets();
app.UseAntiforgery();

app.MapCustomSchemes();

app.MapRazorComponents<Root>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(ProtonTune.UI.App).Assembly);

app.Run();
