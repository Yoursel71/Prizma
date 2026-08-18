using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using ZapretTR.App.Infrastructure;
using ZapretTR.Core.Engine;
using ZapretTR.Core.Models;
using ZapretTR.Core.Profiles;

namespace ZapretTR.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject, IAsyncDisposable
{
    private readonly UnifiedEngineController _engine;
    private ZapretProfile? _selectedProfile;
    private string _stateTitle = "Hazır";
    private string _stateDescription = "Bir profil seçip korumayı başlatabilirsin.";
    private string _actionLabel = "Başlat";
    private string _powerGlyph = "⌁";
    private string _healthTitle = "Motor kontrol ediliyor";
    private string _healthDescription = "Gerekli dosyaların durumuna bakılıyor.";
    private bool _canToggle;
    private bool _canSelectProfile = true;

    public MainWindowViewModel()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var profileDirectory = Path.Combine(baseDirectory, "profiles", "tr");
        _engine = new UnifiedEngineController(Path.Combine(baseDirectory, "engine"));
        _engine.StateChanged += state => Application.Current.Dispatcher.Invoke(() => ApplyState(state));
        _engine.LogReceived += line => Application.Current.Dispatcher.Invoke(() => AddLog(line));

        Profiles = new ObservableCollection<ZapretProfile>();
        RecentLogs = new ObservableCollection<string>();
        ToggleCommand = new AsyncRelayCommand(ToggleAsync, () => CanToggle);
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
    }

    public ObservableCollection<ZapretProfile> Profiles { get; }
    public ObservableCollection<string> RecentLogs { get; }
    public AsyncRelayCommand ToggleCommand { get; }

    public ZapretProfile? SelectedProfile
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

    private void ApplyState(EngineState state)
    {
        switch (state)
        {
            case EngineState.Missing:
                StateTitle = "Motor eksik";
                StateDescription = "ZapretTR.Engine veya WinDivert dosyaları bulunamadı.";
                ActionLabel = "Motor eksik";
                PowerGlyph = "!";
                HealthTitle = "Derleme gerekli";
                HealthDescription = "Release betiği uygulamanın birleşik motorunu ve WinDivert sürücüsünü ekler.";
                CanToggle = false;
                CanSelectProfile = true;
                break;
            case EngineState.Running:
                StateTitle = "Koruma açık";
                StateDescription = $"{SelectedProfile?.Name ?? "Seçili profil"} trafiği yerel ZapretTR motoruyla işliyor.";
                ActionLabel = "Durdur";
                PowerGlyph = "✓";
                HealthTitle = "Bağlantı korunuyor";
                HealthDescription = "TLS bölme, ters paket sıralama ve DNS yönlendirmesi tek motorda çalışıyor.";
                CanToggle = true;
                CanSelectProfile = false;
                break;
            case EngineState.Starting:
                StateTitle = "Başlatılıyor";
                StateDescription = "ZapretTR.Engine ve WinDivert hazırlanıyor.";
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
                PowerGlyph = "⌁";
                HealthTitle = "Hazır";
                HealthDescription = "ZapretTR.Engine ve WinDivert kullanılabilir.";
                CanToggle = SelectedProfile is not null;
                CanSelectProfile = true;
                break;
        }
    }

    private static string? FindExternalEngineProcess()
    {
        foreach (var processName in new[] { "goodbyedpi", "winws2", "ZapretTR.Engine" })
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
