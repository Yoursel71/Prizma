using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using ZapretTR.App.Infrastructure;
using ZapretTR.Core.Engine;
using ZapretTR.Core.Models;
using ZapretTR.Core.Profiles;

namespace ZapretTR.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject, IAsyncDisposable
{
    private readonly EngineController _engine;
    private ZapretProfile? _selectedProfile;
    private string _stateTitle = "Hazır";
    private string _stateDescription = "Bir profil seçip korumayı başlatabilirsin.";
    private string _actionLabel = "Korumayı Başlat";
    private string _powerGlyph = "⌁";
    private string _healthTitle = "Motor kontrol ediliyor";
    private string _healthDescription = "Gerekli dosyaların durumuna bakılıyor.";
    private bool _canToggle;

    public MainWindowViewModel()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var profileDirectory = Path.Combine(baseDirectory, "profiles", "tr");
        var engineDirectory = Path.Combine(baseDirectory, "engine");

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
        _engine = new EngineController(engineDirectory);
        _engine.StateChanged += state => Application.Current.Dispatcher.Invoke(() => ApplyState(state));
        _engine.LogReceived += line => Application.Current.Dispatcher.Invoke(() => AddLog(line));
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
            if (SetProperty(ref _selectedProfile, value) && _engine is not null)
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
    public string EngineVersionText => _engine.State == EngineState.Missing ? "Dosyalar eksik" : "Motor hazır";

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
            }
            else if (SelectedProfile is not null)
            {
                AddLog($"{SelectedProfile.Name} profili başlatılıyor…");
                await _engine.StartAsync(SelectedProfile);
                AddLog("Koruma etkin.");
            }
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
                StateTitle = "Motor bekleniyor";
                StateDescription = "winws2 paketi bulunamadı. Derleme betiği motoru doğrulayarak ekleyecek.";
                ActionLabel = "Motor Eksik";
                PowerGlyph = "!";
                HealthTitle = "Motor dosyaları eksik";
                HealthDescription = "Paketleme adımını çalıştırın veya doğrulanmış zapret2 motorunu engine klasörüne ekleyin.";
                CanToggle = false;
                break;
            case EngineState.Running:
                StateTitle = "Koruma açık";
                StateDescription = $"{SelectedProfile?.Name ?? "Seçili"} profili trafiği işliyor.";
                ActionLabel = "Korumayı Durdur";
                PowerGlyph = "✓";
                HealthTitle = "Her şey yolunda";
                HealthDescription = "winws2 çalışıyor. Bağlantı tanılama modülü bir sonraki aşamada eklenecek.";
                CanToggle = true;
                break;
            case EngineState.Starting:
                StateTitle = "Başlatılıyor";
                StateDescription = "Motor ve WinDivert sürücüsü hazırlanıyor.";
                ActionLabel = "Lütfen bekle";
                PowerGlyph = "…";
                CanToggle = false;
                break;
            case EngineState.Stopping:
                StateTitle = "Durduruluyor";
                StateDescription = "Ağ işleme süreci güvenli biçimde kapatılıyor.";
                ActionLabel = "Lütfen bekle";
                PowerGlyph = "…";
                CanToggle = false;
                break;
            case EngineState.Faulted:
                StateTitle = "Kontrol gerekli";
                StateDescription = "Motor beklenmedik biçimde durdu. Ayrıntılar günlüklerde.";
                ActionLabel = "Yeniden Dene";
                PowerGlyph = "!";
                CanToggle = SelectedProfile is not null;
                break;
            default:
                StateTitle = "Koruma kapalı";
                StateDescription = "Bağlantı şu anda değişiklik yapılmadan çalışıyor.";
                ActionLabel = "Korumayı Başlat";
                PowerGlyph = "⌁";
                HealthTitle = "Sistem hazır";
                HealthDescription = "Motor ve Lua strateji dosyaları bulundu.";
                CanToggle = SelectedProfile is not null;
                break;
        }

        OnPropertyChanged(nameof(EngineVersionText));
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
