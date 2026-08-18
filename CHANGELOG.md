# Değişiklik günlüğü

## 1.0.0 — PRİZMA

- Ürün, kaynak ağacı, motor, Windows hizmeti ve paketler PRİZMA adı altında yeniden markalandı.
- Yeni `Prizma.exe`, `Prizma.Engine.exe`, `%ProgramFiles%\Prizma` ve `Prizma.Engine` hizmet sözleşmesi eklendi.
- Repo, release bağlantıları ve CI paket adları yeni markaya taşındı.

## 0.3.0 - Geliştirme

- GoodbyeDPI ve winws2 binary wrapper entegrasyonları kaldırıldı.
- Uygulamaya ait `Prizma.Engine` .NET 8 paket motoru eklendi.
- Doğrudan WinDivert P/Invoke receive/modify/reinject döngüsü yazıldı.
- TLS/HTTP split, ters parça sıralaması, TTL=5 fake TLS, SNI maskeleme ve IPv4 UDP DNS yönlendirmesi eklendi.
- Beklenmeyen paket hatalarında özgün paketi gönderen fail-open davranışı eklendi.
- Core katmanı tek `UnifiedEngineController` sözleşmesine geçirildi.
- Profil argümanları yeni allowlist motor CLI'sine taşındı.
- GUI minimalist tek ekran düzenine dönüştürüldü.
- Yalnız resmî WinDivert arşivini doğrulayan release zinciri yazıldı.
- Saf paket dönüştürme ve birleşik motor yaşam döngüsü testleri eklendi.

## 0.1.0 - Geliştirme

- İlk Prizma WPF uygulama kabuğu eklendi.
- Deneysel zapret2 süreç yönetimi ve profil prototipi eklendi.
