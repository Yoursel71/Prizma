using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using Prizma.App.Infrastructure;
using Prizma.Core.Benchmarking;
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
    private bool _isBenchmarkRunning;
    private double _benchmarkProgress;
    private string _benchmarkProgressText = "Henüz ölçüm yapılmadı";
    private string _recommendationHint = "128 yerel stratejiyi erişim, gecikme ve hızla karşılaştırır.";

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
        FindRecommendedProfileCommand = new AsyncRelayCommand(FindRecommendedProfileAsync, () => CanRunRecommendation);
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

        BenchmarkTopResults = new ObservableCollection<BenchmarkResultViewModel>();
        LoadRecommendationCache();
        SelectedProfile = Profiles.FirstOrDefault(profile => profile.Recommended) ?? Profiles.FirstOrDefault();
        ApplyState(_engine.State);
        _ = RefreshServiceStateAsync();
    }

    public ObservableCollection<ConnectionProfile> Profiles { get; }
    public ObservableCollection<string> RecentLogs { get; }
    public ObservableCollection<BenchmarkResultViewModel> BenchmarkTopResults { get; }
    public AsyncRelayCommand ToggleCommand { get; }
    public AsyncRelayCommand ManageServiceCommand { get; }
    public AsyncRelayCommand DownloadCommand { get; }
    public AsyncRelayCommand FindRecommendedProfileCommand { get; }
    public string EnginePath => _engine.EngineDirectory;
    public string DeveloperButtonText => "Geliştirici";
    public string RecommendationButtonText => IsBenchmarkRunning ? "Ölçülüyor…" : "✦  Önerilen profili bul";
    public string RecommendationHint { get => _recommendationHint; private set => SetProperty(ref _recommendationHint, value); }
    public bool IsBenchmarkRunning
    {
        get => _isBenchmarkRunning;
        private set
        {
            if (!SetProperty(ref _isBenchmarkRunning, value)) return;
            OnPropertyChanged(nameof(RecommendationButtonText));
            OnPropertyChanged(nameof(CanRunRecommendation));
            FindRecommendedProfileCommand.RaiseCanExecuteChanged();
        }
    }
    public double BenchmarkProgress { get => _benchmarkProgress; private set => SetProperty(ref _benchmarkProgress, value); }
    public string BenchmarkProgressText { get => _benchmarkProgressText; private set => SetProperty(ref _benchmarkProgressText, value); }
    public bool CanRunRecommendation => !IsBenchmarkRunning &&
                                        _serviceState == WindowsServiceState.NotInstalled &&
                                        _currentState is EngineState.Stopped or EngineState.Faulted;

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
    public string StatusChipText => IsBenchmarkRunning ? "ADAPTIVE ÖLÇÜM" :
        _serviceState == WindowsServiceState.Running ? "HİZMET AKTİF" : _currentState switch
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

    private Task FindRecommendedProfileAsync() => RunRecommendationAsync(showConfirmation: true);

    public async Task RunRecommendationAsync(bool showConfirmation)
    {
        if (showConfirmation)
        {
            var confirmation = MessageBox.Show(
                "Prizma bu ağda 128 güvenli stratejiyi sırayla dener. Ölçüm 3–10 dakika sürebilir ve en fazla 40 MB veri kullanır. Bu sırada bağlantı kısa aralıklarla yeniden kurulabilir.\n\nDevam edilsin mi?",
                "Prizma Adaptive", MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (confirmation != MessageBoxResult.Yes) return;
        }

        if (_serviceState != WindowsServiceState.NotInstalled)
        {
            if (showConfirmation)
                MessageBox.Show("Ölçüm için önce kurulu Prizma hizmetini kaldırın. İki paket motoru aynı anda çalıştırılmaz.",
                    "Prizma Adaptive", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsBenchmarkRunning = true;
        CanToggle = false;
        CanSelectProfile = false;
        BenchmarkTopResults.Clear();
        BenchmarkProgress = 0;
        BenchmarkProgressText = "Adaylar hazırlanıyor…";
        RecommendationHint = "Erişim başarısı öncelikli; hız ve gecikme eşitliği bozar.";

        try
        {
            if (_engine.State == EngineState.Running) await _engine.StopAsync();
            var candidates = new BenchmarkCandidateGenerator().Generate();
            var options = BenchmarkRunOptions.CreateTurkeyDefaults();
            var scorer = new ProfileBenchmarkScorer();
            var runner = new ProfileBenchmarkRunner(_engine, new HttpConnectionProbe(), scorer);
            var progress = new Progress<BenchmarkProgress>(item =>
            {
                BenchmarkProgress = item.TotalProfiles == 0 ? 0 : item.CompletedProfiles * 100d / item.TotalProfiles;
                BenchmarkProgressText = item.CompletedProfiles >= item.TotalProfiles
                    ? "Sonuçlar doğrulanıyor…"
                    : $"{item.CompletedProfiles}/{item.TotalProfiles} · {item.CurrentProfile.Description}";
                StateDescription = BenchmarkProgressText;
            });

            AddLog($"Adaptive turnuva başladı: {candidates.Count} aday, üst sınır 40 MB.");
            var run = await runner.RunAsync(candidates, options, progress);
            foreach (var ranked in run.TopProfiles)
            {
                BenchmarkTopResults.Add(BenchmarkResultViewModel.From(ranked, scorer));
            }

            var winner = run.TopProfiles.FirstOrDefault();
            if (winner is null || winner.Result.AccessibilityRate < 1)
            {
                SaveRecommendationCache(null);
                throw new InvalidOperationException("Bütün doğrulama hedeflerine ulaşan kararlı bir profil bulunamadı.");
            }

            var recommended = CreateRecommendedProfile(winner.Result);
            var previous = Profiles.FirstOrDefault(profile => profile.Id == recommended.Id);
            if (previous is not null) Profiles.Remove(previous);
            Profiles.Insert(0, recommended);
            SelectedProfile = recommended;
            SaveRecommendationCache(recommended);

            BenchmarkProgress = 100;
            BenchmarkProgressText = $"Tamamlandı · 1 numara: {winner.Result.Profile.Description}";
            RecommendationHint = $"Kazanan saklandı · {winner.Result.MedianLatency.TotalMilliseconds:0} ms · {winner.Result.ThroughputMbps:0.0} Mbps";
            AddLog($"Adaptive kazananı saklandı. Veri: {run.DownloadedBytes / 1024d / 1024d:0.0} MB.");
        }
        catch (OperationCanceledException)
        {
            BenchmarkProgressText = "Ölçüm iptal edildi.";
            AddLog("Adaptive ölçüm iptal edildi.");
        }
        catch (Exception exception)
        {
            BenchmarkProgressText = "Ölçüm tamamlanamadı";
            RecommendationHint = exception.Message;
            AddLog("Adaptive hata: " + exception.Message);
            if (showConfirmation)
                MessageBox.Show(exception.Message, "Prizma Adaptive", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            IsBenchmarkRunning = false;
            ApplyState(_engine.State);
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
        if (IsBenchmarkRunning)
        {
            StateTitle = "Profil turnuvası";
            StateDescription = BenchmarkProgressText;
            ActionLabel = "Ölçülüyor";
            PowerGlyph = "…";
            HealthTitle = "Bağlantı karşılaştırılıyor";
            HealthDescription = "Erişim öncelikli; gecikme ve Mbps eşitliği bozuyor.";
            CanToggle = false;
            CanSelectProfile = false;
            OnPropertyChanged(nameof(IsRunning));
            OnPropertyChanged(nameof(StatusChipText));
            OnPropertyChanged(nameof(CanRunRecommendation));
            FindRecommendedProfileCommand.RaiseCanExecuteChanged();
            return;
        }

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
            OnPropertyChanged(nameof(CanRunRecommendation));
            FindRecommendedProfileCommand.RaiseCanExecuteChanged();
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
        OnPropertyChanged(nameof(CanRunRecommendation));
        FindRecommendedProfileCommand.RaiseCanExecuteChanged();
    }

    private static ConnectionProfile CreateRecommendedProfile(ProfileBenchmarkResult winner) => new()
    {
        Id = "local-adaptive-recommended",
        Name = "Bu ağ için önerilen",
        Description = $"128 aday arasından seçildi: {winner.Profile.Description}",
        Badge = "ADAPTIVE · #1",
        Risk = winner.Profile.Risk,
        Recommended = true,
        Arguments = winner.Profile.Arguments.ToArray()
    };

    private void LoadRecommendationCache()
    {
        try
        {
            var path = RecommendationCachePath();
            if (!File.Exists(path)) return;
            var cache = JsonSerializer.Deserialize<RecommendationCache>(File.ReadAllText(path));
            if (cache is null) return;
            if (!string.Equals(cache.NetworkSignature, CurrentNetworkSignature(), StringComparison.Ordinal))
            {
                AddLog("Ağ değişti; önceki Adaptive önerisi yeniden doğrulanmalı.");
                return;
            }

            if (cache.RecommendedProfile is not null) Profiles.Insert(0, cache.RecommendedProfile);
            foreach (var result in cache.TopResults) BenchmarkTopResults.Add(result);
            RecommendationHint = cache.RecommendedProfile is null
                ? $"Son ölçüm tanı üretti; Geliştirici ekranına bakın · {cache.MeasuredAt.LocalDateTime:g}"
                : $"Bu ağ için son ölçüm: {cache.MeasuredAt.LocalDateTime:g}";
        }
        catch (Exception exception)
        {
            AddLog("Adaptive geçmişi okunamadı: " + exception.Message);
        }
    }

    private void SaveRecommendationCache(ConnectionProfile? recommended)
    {
        var path = RecommendationCachePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var cache = new RecommendationCache(
            CurrentNetworkSignature(), DateTimeOffset.Now, recommended, BenchmarkTopResults.ToArray());
        File.WriteAllText(path, JsonSerializer.Serialize(cache, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string RecommendationCachePath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Prizma", "profile-lab.json");

    private static string CurrentNetworkSignature()
    {
        var material = string.Join('|', NetworkInterface.GetAllNetworkInterfaces()
            .Where(adapter => adapter.OperationalStatus == OperationalStatus.Up &&
                              adapter.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .Select(adapter =>
            {
                var properties = adapter.GetIPProperties();
                return string.Join(';', adapter.Id,
                    string.Join(',', properties.GatewayAddresses.Select(item => item.Address.ToString()).Order()),
                    string.Join(',', properties.DnsAddresses.Select(item => item.ToString()).Order()));
            }).Order());
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)))[..16];
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

public sealed record BenchmarkResultViewModel(
    int Rank, string ProfileName, double Score, double LatencyMs,
    double ThroughputMbps, double SuccessRate, string Grade, string? FailureSummary)
{
    public static BenchmarkResultViewModel From(RankedProfileResult ranked, ProfileBenchmarkScorer scorer)
    {
        var score = scorer.CalculateScore(ranked.Result);
        var grade = score switch { >= 90 => "A", >= 80 => "B", >= 70 => "C", >= 55 => "D", _ => "E" };
        var failures = ranked.Result.AccessibilityResults
            .Where(result => !result.Reachable)
            .Select(result => $"{result.Target.Host}: {result.Error ?? "erişilemedi"}")
            .ToList();
        if (ranked.Result.ThroughputResult is { Reachable: false } throughput)
            failures.Add($"Hız: {throughput.Error ?? "ölçülemedi"}");
        return new BenchmarkResultViewModel(ranked.Rank, ranked.Result.Profile.Name, score,
            ranked.Result.MedianLatency == TimeSpan.MaxValue ? 0 : ranked.Result.MedianLatency.TotalMilliseconds,
            ranked.Result.ThroughputMbps, ranked.Result.AccessibilityRate * 100, grade,
            failures.Count == 0 ? null : string.Join(" · ", failures));
    }
}

public sealed record RecommendationCache(
    string NetworkSignature,
    DateTimeOffset MeasuredAt,
    ConnectionProfile? RecommendedProfile,
    IReadOnlyList<BenchmarkResultViewModel> TopResults);
