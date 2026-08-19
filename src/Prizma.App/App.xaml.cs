using System.Windows;

namespace Prizma.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var window = new MainWindow();
        MainWindow = window;
        window.Show();

        if (!e.Args.Contains("--adaptive-auto", StringComparer.OrdinalIgnoreCase)) return;
        window.Loaded += async (_, _) =>
        {
            await window.ViewModel.RunRecommendationAsync(showConfirmation: false);
            Shutdown();
        };
    }
}
