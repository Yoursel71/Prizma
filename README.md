# PRİZMA

> İnternet, kendi yönünde.

Türkiye'deki Windows kullanıcıları için açık kaynaklı, minimalist bağlantı dayanıklılığı uygulaması.

[![Release](https://img.shields.io/github/v/release/Yoursel71/Prizma?style=flat-square&color=6D7CFF)](https://github.com/Yoursel71/Prizma/releases/latest)
[![Windows CI](https://img.shields.io/github/actions/workflow/status/Yoursel71/Prizma/prizma.yml?branch=master&style=flat-square)](https://github.com/Yoursel71/Prizma/actions/workflows/prizma.yml)
[![License](https://img.shields.io/github/license/Yoursel71/Prizma?style=flat-square)](LICENSE)

Prizma artık `goodbyedpi.exe` veya `winws2.exe` çalıştırmaz. Uygulamanın kendi [.NET 8 motoru](src/Prizma.Engine/) WinDivert'e doğrudan bağlanır; GoodbyeDPI'nin native fragmentation yaklaşımı ile zapret'in ters-sıralı desync fikrini tek, denetlenebilir paket hattında yeniden uygular.

> [!IMPORTANT]
> Prizma bağımsız bir topluluk projesidir; GoodbyeDPI, zapret veya WinDivert projeleriyle resmî bağlantısı yoktur. Ağ davranışı operatöre göre değişebilir. Yalnızca bulunduğunuz yerde yasal olan amaçlarla kullanın.

## Neler var?

- Windows 10/11 için sade .NET 8 WPF arayüzü
- Uygulamaya ait `Prizma.Engine.exe`; üçüncü taraf DPI motor binary'si yok
- TLS ClientHello'yu sabit konumların yanında SNI başlangıcı veya ikinci seviye alan adının ortasından bölme
- Zapret tarzı çoklu `multidisorder` benzeri ters parça sıralaması
- TTL=5 sahte TLS paketi ve güvenli SNI maskeleme
- HTTP `Host` başlığı dönüşümü
- Zehirli sistem DNS önbelleğini aşan, normal TLS ad/zincir doğrulamalı Cloudflare wire-format DNS-over-HTTPS
- İsteğe bağlı QUIC/HTTP3 engeliyle tarayıcıyı işlenen TCP/TLS yoluna düşürme
- Alan adı son eki allowlist'iyle yalnız hedeflenen HTTP/TLS akışlarını işleme
- **Prizma Adaptive:** bu ağda 128 stratejiyi gerçek erişim, TLS gecikmesi ve küçük Mbps örneğiyle karşılaştırıp ilk 5'i sıralama
- Ağ geçidi/DNS parmak izine bağlı yerel kazananı saklama; ağ değiştiğinde otomatik yeniden doğrulama
- Geçmiş TCP sıra numaralı sahte paket, tekrar ve maksimum payload sınırı
- İlk 5 ölçümünü, motor argümanlarını ve canlı logları gösteren geliştirici merkezi
- Arayüz kapalıyken çalışan, otomatik başlayan gerçek Windows hizmet modu
- Başlat Menüsü kısayolu oluşturan `Kur.cmd` ve uygulama içi İndir/Güncelle bağlantısı
- Fail-open paket hattı: beklenmeyen işleme hatasında özgün paket yeniden gönderilir
- Bilinmeyen profil argümanlarını reddeden allowlist CLI
- Yönetici yetkili motor yaşam döngüsü, log ve kapanış temizliği
- Resmî WinDivert 2.2.2 için sabit SHA-256 doğrulamalı paketleme
- Self-contained `win-x64` release ve sürücüsüz saf paket testleri

## Mimari

```text
Prizma.App       Minimal WPF arayüzü
      │
      ▼
Prizma.Core      Profil, süreç ve durum yönetimi
      │
      ▼
Prizma.Engine    Bize ait paket ayrıştırma/desync motoru
      │
      ▼
WinDivert.dll + WinDivert64.sys
```

WinDivert sürücüsü teknik olarak ayrı `.dll` ve imzalı `.sys` dosyaları gerektirir. Bunun dışında GoodbyeDPI veya zapret çalıştırılabilir dosyası pakete konmaz.

## Derleme

Gereksinimler:

- Windows 10/11 x64
- .NET 8 SDK
- PowerShell 5.1 veya 7+

```powershell
dotnet restore Prizma.sln
dotnet build Prizma.sln --configuration Release
dotnet test Prizma.sln --configuration Release
```

Self-contained paket:

```powershell
./scripts/Build-Release.ps1 -Version 1.0.1
```

Betik yalnız resmî WinDivert `v2.2.2` arşivini indirir ve `63cb41763bb4b20f600b6de04e991a9c2be73279e317d4d82f237b150c5f3f15` SHA-256 özetiyle doğrular. GUI ve motor doğrudan bu kaynak ağacından derlenir. Çıktı `artifacts/release/` altındadır.

## Kurulum

GitHub Releases sayfasından ZIP'i indirip çıkarın ve `Kur.cmd` dosyasını çalıştırın. Betik uygulamayı `%ProgramFiles%\Prizma` altına kopyalar, Başlat Menüsü kısayolunu oluşturur ve uygulamayı açar. `Kaldir.cmd` kısayolu, hizmeti ve kurulu dosyaları kaldırır.

Arayüzdeki **Hizmet olarak kur** düğmesi seçili profili `%ProgramData%\Prizma` altına kopyalar ve `Prizma.Engine` hizmetini otomatik başlangıçla kaydeder. Bundan sonra GUI'nin açık kalması gerekmez. Profil değiştirmek için hizmeti kaldırıp yeni profille yeniden kurun.

## Profiller

- **Türkiye • Dengeli:** `1+2+midsld` ters çoklu split, tek wrong-sequence fake ve sertifika doğrulamalı DoH; QUIC'e dokunmaz
- **Türkiye • Uyumluluk:** sıralı `midsld` split; fake, özel DNS ve QUIC engeli yok
- **Türkiye • Güçlü:** TTL+wrong-sequence fake ve son çare QUIC engeli
- **Türkiye • Roblox:** yalnız `roblox.com`, `rbx.com` ve `rbxcdn.com` HTTP/TLS trafiğine uygulanan hedefli profil

**Önerilen profili bul** düğmesi 128 deterministik adayı tek tek ve aynı motor yaşam döngüsüyle dener. Erişemeyen hızlı bir profil kazanamaz: sıralama önce bütün hedeflere doğru TLS ile erişim, sonra düşük gecikme, ardından yüksek Mbps şeklindedir. Ölçüm 40 MB uygulama payload bütçesiyle sınırlıdır; sertifika doğrulaması hiçbir zaman kapatılmaz. Ayrıntılar: [Adaptive profil laboratuvarı](docs/ADAPTIVE.md).

Profil seçenekleri tek bir shell komutuna çevrilmez. Her token `ProcessStartInfo.ArgumentList` ile iletilir ve motor bilinmeyen seçenekleri reddeder.

## Mevcut sınırlar

1.0.1 motorunun bilinçli sınırları:

- IPv4 TCP 80/443 ve IPv4 UDP DNS işlenir.
- IPv6 DNS ve birden fazla TCP paketine yayılan TLS ClientHello reassembly henüz yoktur.
- QUIC paketi sahteleştirilmez; Güçlü profil UDP/443'ü düşürerek istemcinin TCP/TLS'e geri dönmesini sağlar.
- Alan adı filtresi ilk HTTP isteği veya TLS ClientHello üzerinden çalışır; IP tabanlı akış takibi yapmaz.
- WinDivert sürücüsü üçüncü taraf ve dinamik bağımlılıktır.
- Erken geliştirme paketleri kod imzalı değildir.

## Kaynak ve lisans

Paket işleme tasarımı [GoodbyeDPI-Turkey `release-0.2.3rc3-turkey`](https://github.com/cagritaskn/GoodbyeDPI-Turkey/tree/02fee64e1e44759b38aa4b05a46f8bcedaa3bec8), [ValdikSS/GoodbyeDPI](https://github.com/ValdikSS/GoodbyeDPI) ve [bol-van/zapret2](https://github.com/bol-van/zapret2) kaynakları incelenerek C#'ta yeniden uygulanmıştır. WinDivert dinamik olarak LGPLv3 seçeneği altında kullanılır.

Proje kodu [LICENSE](LICENSE) altındadır. Kaynak sabitlemeleri ve üçüncü taraf bildirimleri için [NOTICE.md](NOTICE.md), güvenlik bildirimi için [SECURITY.md](SECURITY.md) dosyasına bakın.
