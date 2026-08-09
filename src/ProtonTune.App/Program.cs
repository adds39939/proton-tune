using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Photino.Blazor;
using ProtonTune.Services.DependencyExtensions;
using ProtonTune.UI.DependencyExtensions;

namespace ProtonTune.App;

internal class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        WebKitEnvironment.EnsureOsIsLinux();
        WebKitEnvironment.DisableDmaBufRenderer();

        var appBuilder = PhotinoBlazorAppBuilder.CreateDefault();

        appBuilder.Services
            .AddLogging(logging => logging.AddConsole())
            .AddProtonTuneServices()
            .AddProtonTuneUI();
        
        appBuilder.RootComponents.Add<UI.App>("app");
        
        var app = appBuilder.Build();
    
        app.MainWindow.SetTitle("Proton Tune");

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            app.MainWindow.ShowMessage("Unhandled Exception", e.ExceptionObject.ToString());
        };
    
        app.Run();
    }
}
