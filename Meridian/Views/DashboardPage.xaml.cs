using Meridian.Presentation;
using Meridian.Services;

namespace Meridian.Views;

public sealed partial class DashboardPage : Page
{
    private readonly DispatcherTimer _clockTimer;

    public DashboardPage()
    {
        this.InitializeComponent();

        // Wire MVUX DataContext — DashboardViewModel is source-generated from DashboardModel
        var marketData = App.Services.GetRequiredService<IMarketDataService>();
        DataContext = new Presentation.DashboardViewModel(marketData);

        // Live clock — 1s interval
        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) => UpdateClock();
        _clockTimer.Start();
        UpdateClock();
    }

    private void UpdateClock()
    {
        var now = DateTime.Now;
        ClockText.Text = now.ToString("ddd, MMM d · hh:mm:ss tt");
    }
}
