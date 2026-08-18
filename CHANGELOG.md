# Değişiklik günlüğü

## 0.3.0 - Geliştirme

- GoodbyeDPI ve winws2 binary wrapper entegrasyonları kaldırıldı.
- Uygulamaya ait `ZapretTR.Engine` .NET 8 paket motoru eklendi.
- Doğrudan WinDivert P/Invoke receive/modify/reinject döngüsü yazıldı.
- TLS/HTTP split, ters parça sıralaması, TTL=5 fake TLS, SNI maskeleme ve IPv4 UDP DNS yönlendirmesi eklendi.
- Beklenmeyen paket hatalarında özgün paketi gönderen fail-open davranışı eklendi.
- Core katmanı tek `UnifiedEngineController` sözleşmesine geçirildi.
- Profil argümanları yeni allowlist motor CLI'sine taşındı.
- GUI minimalist tek ekran düzenine dönüştürüldü.
- Yalnız resmî WinDivert arşivini doğrulayan release zinciri yazıldı.
- Saf paket dönüştürme ve birleşik motor yaşam döngüsü testleri eklendi.

## 0.1.0 - Geliştirme

- İlk ZapretTR WPF uygulama kabuğu eklendi.
- Deneysel zapret2 süreç yönetimi ve profil prototipi eklendi.
