# ZapretTR

Türkiye'deki Windows kullanıcıları için açık kaynaklı ve minimalist bağlantı koruma uygulaması.

ZapretTR artık `goodbyedpi.exe` veya `winws2.exe` çalıştırmaz. Uygulamanın kendi [.NET 8 motoru](src/ZapretTR.Engine/) WinDivert'e doğrudan bağlanır; GoodbyeDPI'nin native fragmentation yaklaşımı ile zapret'in ters-sıralı desync fikrini tek, denetlenebilir paket hattında yeniden uygular.

> [!IMPORTANT]
> ZapretTR bağımsız bir topluluk projesidir; GoodbyeDPI, zapret veya WinDivert projeleriyle resmî bağlantısı yoktur. Ağ davranışı operatöre göre değişebilir. Yalnızca bulunduğunuz yerde yasal olan amaçlarla kullanın.

## Neler var?

- Windows 10/11 için sade .NET 8 WPF arayüzü
- Uygulamaya ait `ZapretTR.Engine.exe`; üçüncü taraf DPI motor binary'si yok
- TLS ClientHello ve HTTP paketlerini yapılandırılabilir konumdan bölme
- Zapret tarzı ters parça sıralaması
- TTL=5 sahte TLS paketi ve güvenli SNI maskeleme
- HTTP `Host` başlığı dönüşümü
- Süreç içi IPv4 UDP DNS yönlendirmesi ve cevap geri eşleme
- Fail-open paket hattı: beklenmeyen işleme hatasında özgün paket yeniden gönderilir
- Bilinmeyen profil argümanlarını reddeden allowlist CLI
- Yönetici yetkili motor yaşam döngüsü, log ve kapanış temizliği
- Resmî WinDivert 2.2.2 için sabit SHA-256 doğrulamalı paketleme
- Self-contained `win-x64` release ve sürücüsüz saf paket testleri

## Mimari

```text
ZapretTR.App       Minimal WPF arayüzü
      │
      ▼
ZapretTR.Core      Profil, süreç ve durum yönetimi
      │
      ▼
ZapretTR.Engine    Bize ait paket ayrıştırma/desync motoru
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
dotnet restore ZapretTR.sln
dotnet build ZapretTR.sln --configuration Release
dotnet test ZapretTR.sln --configuration Release
```

Self-contained paket:

```powershell
./scripts/Build-Release.ps1 -Version 0.3.0
```

Betik yalnız resmî WinDivert `v2.2.2` arşivini indirir ve `63cb41763bb4b20f600b6de04e991a9c2be73279e317d4d82f237b150c5f3f15` SHA-256 özetiyle doğrular. GUI ve motor doğrudan bu kaynak ağacından derlenir. Çıktı `artifacts/release/` altındadır.

## Profiller

- **Türkiye • Dengeli:** reverse split, TTL=5 fake ve DNS yönlendirmesi
- **Türkiye • Uyumluluk:** sıralı split, sahte paket yok
- **Türkiye • Güçlü:** ilk bayttan daha agresif reverse split
- **Türkiye • Roblox:** DNS'i değiştirmeyen sade TLS profili

Profil seçenekleri tek bir shell komutuna çevrilmez. Her token `ProcessStartInfo.ArgumentList` ile iletilir ve motor bilinmeyen seçenekleri reddeder.

## Mevcut sınırlar

0.3.0 motoru bilinçli olarak dar bir MVP'dir:

- IPv4 TCP 80/443 ve IPv4 UDP DNS işlenir.
- IPv6 DNS, QUIC/HTTP3, TLS reassembly ve hostlist henüz yoktur.
- WinDivert sürücüsü üçüncü taraf ve dinamik bağımlılıktır.
- Erken geliştirme paketleri kod imzalı değildir.

## Kaynak ve lisans

Paket işleme tasarımı [GoodbyeDPI-Turkey `release-0.2.3rc3-turkey`](https://github.com/cagritaskn/GoodbyeDPI-Turkey/tree/02fee64e1e44759b38aa4b05a46f8bcedaa3bec8), [ValdikSS/GoodbyeDPI](https://github.com/ValdikSS/GoodbyeDPI) ve [bol-van/zapret2](https://github.com/bol-van/zapret2) kaynakları incelenerek C#'ta yeniden uygulanmıştır. WinDivert dinamik olarak LGPLv3 seçeneği altında kullanılır.

Proje kodu [LICENSE](LICENSE) altındadır. Kaynak sabitlemeleri ve üçüncü taraf bildirimleri için [NOTICE.md](NOTICE.md), güvenlik bildirimi için [SECURITY.md](SECURITY.md) dosyasına bakın.
