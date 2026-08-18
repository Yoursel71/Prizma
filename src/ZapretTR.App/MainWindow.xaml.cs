using System.ComponentModel;
using System.Windows;
using ZapretTR.App.ViewModels;

namespace ZapretTR.App;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainWindowViewModel();
        DataContext = _viewModel;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _viewModel.DisposeAsync().AsTask().GetAwaiter().GetResult();
        base.OnClosing(e);
    }
}
