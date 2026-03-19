using System.Diagnostics.CodeAnalysis;
using Meridian.Services;
using Meridian.Views;
using Uno.Resizetizer;

namespace Meridian;

public partial class App : Application
{
    public App()
    {
        this.InitializeComponent();
    }

    public static IServiceProvider Services { get; private set; } = null!;

    protected Window? MainWindow { get; private set; }
    protected IHost? Host { get; private set; }

    [SuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "Uno.Extensions APIs are used in a way that is safe for trimming in this template context.")]
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var builder = this.CreateBuilder(args)
            .Configure(host => host
#if DEBUG
                .UseEnvironment(Environments.Development)
#endif
                .UseLogging(configure: (context, logBuilder) =>
                {
                    logBuilder
                        .SetMinimumLevel(
                            context.HostingEnvironment.IsDevelopment() ?
                                LogLevel.Information :
                                LogLevel.Warning)
                        .CoreLogLevel(LogLevel.Warning);
                }, enableUnoLogging: true)
                .ConfigureServices((context, services) =>
                {
                    services.AddSingleton<IMarketDataService, MockMarketDataService>();
                    services.AddSingleton(sp =>
                    {
                        var key = Environment.GetEnvironmentVariable("FINNHUB_API_KEY")
                            ?? "d6u0709r01qjm9brvoigd6u0709r01qjm9brvoj0";
                        return new FinnhubService(key,
                            ["AAPL", "NVDA", "MSFT", "GOOGL", "META", "TSLA"]);
                    });
                })
            );
        MainWindow = builder.Window;

#if DEBUG
        MainWindow.UseStudio();
#endif
        MainWindow.SetWindowIcon();

        Host = builder.Build();
        Services = Host.Services;

        // Set desktop window size for 1400px + padding layout
        MainWindow.AppWindow.Resize(new Windows.Graphics.SizeInt32 { Width = 1500, Height = 900 });

        if (MainWindow.Content is not Frame rootFrame)
        {
            rootFrame = new Frame();
            MainWindow.Content = rootFrame;
        }

        if (rootFrame.Content == null)
        {
            rootFrame.Navigate(typeof(DashboardPage), args.Arguments);
        }

        MainWindow.Activate();
    }
}
