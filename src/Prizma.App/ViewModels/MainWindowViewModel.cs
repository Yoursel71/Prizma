using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Prizma.App.Infrastructure;
using Prizma.Core.Engine;
using Prizma.Core.Models;
using Prizma.Core.Profiles;
using Prizma.Core.Services;

namespace Prizma.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject, IAsyncDisposable
{
    private readonly UnifiedEngineController _engine;
    private readonly WindowsServiceManager _serviceManager = new();
    private ConnectionProfile? _selectedProfile;
    private string _stateTitle = "Hazır";
    private string _stateDescription = "Bir profil seçip korumayı başlatabilirsin.";
    private string _actionLabel = "Başlat";
    private string _powerGlyph = "⏻";
    private string _healthTitle = "Motor kontrol ediliyor";
    private string _healthDescription = "Gerekli dosyaların durumuna bakılıyor.";
    private bool _canToggle;
    private bool _canSelectProfile = true;
    private EngineState _currentState;
    private WindowsServiceState _serviceState = WindowsServiceState.NotInstalled;
    private string _serviceStatusText = "Windows ile otomatik başlat";
    private string _serviceActionLabel = "Hizmet olarak kur";
    private bool _canManageService = true;

    public MainWindowViewModel()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var profileDirectory = Path.Combine(baseDirectory, "profiles", "tr");
        _engine = new UnifiedEngineController(Path.Combine(baseDirectory, "engine"));
        _engine.StateChanged += state => Application.Current.Dispatcher.Invoke(() => ApplyState(state));
        _engine.LogReceived += line => Application.Current.Dispatcher.Invoke(() => AddLog(line));

        Profiles = new ObservableCollection<ConnectionProfile>();
        RecentLogs = new ObservableCollection<string>();
        ToggleCommand = new AsyncRelayCommand(ToggleAsync, () => CanToggle);
        ManageServiceCommand = new AsyncRelayCommand(ManageServiceAsync, () => CanManageService);
        DownloadCommand = new AsyncRelayCommand(OpenDownloadAsync);
        try
        {
            foreach (var profile in new ProfileCatalog().Load(profileDirectory))
            {
                Profiles.Add(profile);
            }
        }
        catch (Exception exception)
        {
            AddLog("Profil hatası: " + exception.Message);
        }

        SelectedProfile = Profiles.FirstOrDefault(profile => profile.Recommended) ?? Profiles.FirstOrDefault();
        ApplyState(_engine.State);
        _ = RefreshServiceStateAsync();
    }

    public ObservableCollection<ConnectionProfile> Profiles { get; }
    public ObservableCollection<string> RecentLogs { get; }
    public AsyncRelayCommand ToggleCommand { get; }
    public AsyncRelayCommand ManageServiceCommand { get; }
    public AsyncRelayCommand DownloadCommand { get; }

    public ConnectionProfile? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (SetProperty(ref _selectedProfile, value))
            {
                ApplyState(_engine.State);
            }
        }
    }

    public string StateTitle { get => _stateTitle; private set => SetProperty(ref _stateTitle, value); }
    public string StateDescription { get => _stateDescription; private set => SetProperty(ref _stateDescription, value); }
    public string ActionLabel { get => _actionLabel; private set => SetProperty(ref _actionLabel, value); }
    public string PowerGlyph { get => _powerGlyph; private set => SetProperty(ref _powerGlyph, value); }
    public string HealthTitle { get => _healthTitle; private set => SetProperty(ref _healthTitle, value); }
    public string HealthDescription { get => _healthDescription; private set => SetProperty(ref _healthDescription, value); }
    public string ServiceStatusText { get => _serviceStatusText; private set => SetProperty(ref _serviceStatusText, value); }
    public string ServiceActionLabel { get => _serviceActionLabel; private set => SetProperty(ref _serviceActionLabel, value); }
    public bool IsServiceInstalled => _serviceState != WindowsServiceState.NotInstalled;
    public bool CanManageService
    {
        get => _canManageService;
        private set
        {
            if (SetProperty(ref _canManageService, value)) ManageServiceCommand.RaiseCanExecuteChanged();
        }
    }
    public bool IsRunning => _serviceState == WindowsServiceState.Running || _currentState == EngineState.Running;
    public string StatusChipText => _serviceState == WindowsServiceState.Running ? "HİZMET AKTİF" : _currentState switch
    {
        EngineState.Running => "KORUMA AKTİF",
        EngineState.Starting => "MOTOR HAZIRLANIYOR",
        EngineState.Stopping => "MOTOR DURDURULUYOR",
        EngineState.Missing => "MOTOR EKSİK",
        EngineState.Faulted => "DİKKAT GEREKLİ",
        _ => "KORUMA KAPALI"
    };

    public bool CanSelectProfile
    {
        get => _canSelectProfile;
        private set => SetProperty(ref _canSelectProfile, value);
    }

    public bool CanToggle
    {
        get => _canToggle;
        private set
        {
            if (SetProperty(ref _canToggle, value))
            {
                ToggleCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public async ValueTask DisposeAsync() => await _engine.DisposeAsync();

    private async Task ToggleAsync()
    {
        try
        {
            if (_engine.State == EngineState.Running)
            {
                await _engine.StopAsync();
                AddLog("Koruma durduruldu.");
                return;
            }

            if (SelectedProfile is null)
            {
                return;
            }

            var conflictingProcess = FindExternalEngineProcess();
            if (conflictingProcess is not null)
            {
                throw new InvalidOperationException($"{conflictingProcess} zaten çalışıyor. Önce diğer DPI aracını kapatın.");
            }

            AddLog($"{SelectedProfile.Name} başlatılıyor…");
            await _engine.StartAsync(SelectedProfile);
            AddLog("Birleşik koruma etkin.");
        }
        catch (Exception exception)
        {
            AddLog("Hata: " + exception.Message);
            HealthTitle = "Başlatma başarısız";
            HealthDescription = exception.Message;
        }
    }

    private async Task ManageServiceAsync()
    {
        CanManageService = false;
        try
        {
            if (_serviceState == WindowsServiceState.NotInstalled)
            {
                if (SelectedProfile is null) return;
                if (_engine.State == EngineState.Running) await _engine.StopAsync();
                AddLog($"{SelectedProfile.Name} Windows hizmeti olarak kuruluyor…");
                await _serviceManager.InstallAsync(Path.Combine(AppContext.BaseDirectory, "engine"), SelectedProfile);
                AddLog("Hizmet kuruldu; Windows ile otomatik başlayacak.");
            }
            else
            {
                AddLog("Prizma hizmeti kaldırılıyor…");
                await _serviceManager.UninstallAsync();
                AddLog("Hizmet kaldırıldı. Kurulu motor dosyaları güvenli biçimde bırakıldı.");
            }
        }
        catch (Exception exception)
        {
            AddLog("Hizmet hatası: " + exception.Message);
            HealthTitle = "Hizmet işlemi başarısız";
            HealthDescription = exception.Message;
        }
        finally
        {
            await RefreshServiceStateAsync();
            CanManageService = true;
        }
    }

    private static Task OpenDownloadAsync()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://github.com/Yoursel71/Prizma/releases/latest",
            UseShellExecute = true
        });
        return Task.CompletedTask;
    }

    private async Task RefreshServiceStateAsync()
    {
        try
        {
            _serviceState = await _serviceManager.GetStateAsync();
            ServiceStatusText = _serviceState switch
            {
                WindowsServiceState.Running => "Windows açılışında aktif · Çalışıyor",
                WindowsServiceState.Stopped => "Kurulu · Şu anda durmuş",
                _ => "Windows ile otomatik başlat"
            };
            ServiceActionLabel = _serviceState == WindowsServiceState.NotInstalled ? "Hizmet olarak kur" : "Hizmeti kaldır";
            OnPropertyChanged(nameof(IsServiceInstalled));
            ApplyState(_engine.State);
        }
        catch (Exception exception)
        {
            AddLog("Hizmet durumu okunamadı: " + exception.Message);
        }
    }

    private void ApplyState(EngineState state)
    {
        _currentState = state;
        if (_serviceState != WindowsServiceState.NotInstalled)
        {
            StateTitle = _serviceState == WindowsServiceState.Running ? "Her zaman açık" : "Hizmet bekliyor";
            StateDescription = _serviceState == WindowsServiceState.Running
                ? "Koruma arayüz kapalıyken de Windows hizmeti olarak çalışıyor."
                : "Hizmet kurulu ancak çalışmıyor; kaldırıp seçili profille yeniden kurabilirsin.";
            ActionLabel = "Hizmet modu";
            PowerGlyph = _serviceState == WindowsServiceState.Running ? "✓" : "!";
            HealthTitle = _serviceState == WindowsServiceState.Running ? "Arka planda korunuyor" : "Hizmet durmuş";
            HealthDescription = ServiceStatusText;
            CanToggle = false;
            CanSelectProfile = false;
            OnPropertyChanged(nameof(IsRunning));
            OnPropertyChanged(nameof(StatusChipText));
            return;
        }
        switch (state)
        {
            case EngineState.Missing:
                StateTitle = "Motor eksik";
                StateDescription = "Prizma.Engine veya WinDivert dosyaları bulunamadı.";
                ActionLabel = "Motor eksik";
                PowerGlyph = "!";
                HealthTitle = "Derleme gerekli";
                HealthDescription = "Release betiği uygulamanın birleşik motorunu ve WinDivert sürücüsünü ekler.";
                CanToggle = false;
                CanSelectProfile = true;
                break;
            case EngineState.Running:
                StateTitle = "Koruma açık";
                StateDescription = $"{SelectedProfile?.Name ?? "Seçili profil"} trafiği yerel Prizma motoruyla işliyor.";
                ActionLabel = "Durdur";
                PowerGlyph = "✓";
                HealthTitle = "Bağlantı korunuyor";
                HealthDescription = "TLS bölme, ters paket sıralama ve DNS yönlendirmesi tek motorda çalışıyor.";
                CanToggle = true;
                CanSelectProfile = false;
                break;
            case EngineState.Starting:
                StateTitle = "Başlatılıyor";
                StateDescription = "Prizma.Engine ve WinDivert hazırlanıyor.";
                ActionLabel = "Bekle";
                PowerGlyph = "…";
                CanToggle = false;
                CanSelectProfile = false;
                break;
            case EngineState.Stopping:
                StateTitle = "Durduruluyor";
                StateDescription = "Paket işleme kuyruğu kapatılıyor.";
                ActionLabel = "Bekle";
                PowerGlyph = "…";
                CanToggle = false;
                CanSelectProfile = false;
                break;
            case EngineState.Faulted:
                StateTitle = "Kontrol gerekli";
                StateDescription = "Birleşik motor beklenmedik biçimde durdu. Ayrıntılar günlükte.";
                ActionLabel = "Yeniden dene";
                PowerGlyph = "!";
                CanToggle = SelectedProfile is not null;
                CanSelectProfile = true;
                break;
            default:
                StateTitle = "Koruma kapalı";
                StateDescription = "Bağlantı şu anda değiştirilmeden çalışıyor.";
                ActionLabel = "Başlat";
                PowerGlyph = "⏻";
                HealthTitle = "Hazır";
                HealthDescription = "Prizma.Engine ve WinDivert kullanılabilir.";
                CanToggle = SelectedProfile is not null;
                CanSelectProfile = true;
                break;
        }

        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(StatusChipText));
    }

    private static string? FindExternalEngineProcess()
    {
        foreach (var processName in new[] { "goodbyedpi", "winws2", "Prizma.Engine", "ZapretTR.Engine" })
        {
            var processes = Process.GetProcessesByName(processName);
            try
            {
                if (processes.Length > 0)
                {
                    return processName + ".exe";
                }
            }
            finally
            {
                foreach (var process in processes)
                {
                    process.Dispose();
                }
            }
        }

        return null;
    }

    private void AddLog(string line)
    {
        RecentLogs.Insert(0, $"{DateTime.Now:HH:mm:ss}  {line}");
        while (RecentLogs.Count > 40)
        {
            RecentLogs.RemoveAt(RecentLogs.Count - 1);
        }
    }
}
