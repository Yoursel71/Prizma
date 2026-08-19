using System.ComponentModel;
using System.Windows;
using Prizma.App.ViewModels;

namespace Prizma.App;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainWindowViewModel();
        DataContext = _viewModel;
    }

    internal MainWindowViewModel ViewModel => _viewModel;

    protected override void OnClosing(CancelEventArgs e)
    {
        _viewModel.DisposeAsync().AsTask().GetAwaiter().GetResult();
        base.OnClosing(e);
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        if (MaximizeButton is not null)
        {
            MaximizeButton.Content = WindowState == WindowState.Maximized ? "❐" : "□";
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeButton_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void DeveloperButton_Click(object sender, RoutedEventArgs e)
    {
        var existingWindow = OwnedWindows.OfType<DeveloperWindow>().FirstOrDefault();
        if (existingWindow is not null)
        {
            if (existingWindow.WindowState == WindowState.Minimized)
            {
                existingWindow.WindowState = WindowState.Normal;
            }

            existingWindow.Activate();
            return;
        }

        new DeveloperWindow
        {
            Owner = this,
            DataContext = _viewModel
        }.Show();
    }
}
