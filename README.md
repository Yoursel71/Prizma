# ZapretTR

Türkiye'deki Windows kullanıcıları için, [zapret2](https://github.com/bol-van/zapret2) motorunu yöneten açık kaynaklı masaüstü arayüzü.

> [!IMPORTANT]
> ZapretTR bağımsız bir topluluk projesidir; bol-van veya resmî zapret projesiyle bağlantılı değildir. Türkiye profilleri henüz deneysel başlangıç profilleridir ve operatör bazında doğrulanmış sonuç iddiasında bulunmaz.

## Şu anda çalışanlar

- Windows 10/11 için .NET 8 WPF arayüzü
- Yönetici yetkisiyle güvenli `winws2` süreç yönetimi
- Yapılandırılmış, sürümlenebilir JSON profilleri
- Uyumluluk, Dengeli ve Güçlü başlangıç profilleri
- Motor stdout/stderr günlüklerinin arayüzde gösterimi
- Kapanışta motor sürecini ve alt süreçlerini durdurma
- Sabitlenmiş zapret2 sürümü ve SHA-256 doğrulamalı motor indirme
- Self-contained `win-x64` ZIP üretimi
- Birim testleri ve GitHub Actions

## Durum

Proje erken alfa aşamasındadır. Arayüzün Liquid Glass tasarım sistemi, otomatik bağlantı testi, operatör algılama, sistem tepsisi ve güncelleme sistemi sonraki geliştirme aşamalarındadır.

## Hızlı derleme

Gereksinimler:

- Windows 10 veya Windows 11
- .NET 8 SDK
- PowerShell 7 veya Windows PowerShell 5.1+

```powershell
dotnet restore ZapretTR.sln
dotnet build ZapretTR.sln --configuration Release
dotnet test ZapretTR.sln --configuration Release
```

Çalıştırılabilir, self-contained paket üretmek için:

```powershell
./scripts/Build-Release.ps1 -Version 0.1.0
```

Paket `artifacts/release/` altında oluşur. Betik zapret2 `v1.0.4` arşivini indirir, sabit SHA-256 özetiyle doğrular ve yalnızca gerekli Windows x64 motor dosyalarını pakete ekler.

## Mimari

```text
src/ZapretTR.App     WPF arayüzü, durum ve kullanıcı etkileşimi
src/ZapretTR.Core    Profil okuma, argüman oluşturma ve motor yaşam döngüsü
profiles/tr          Türkiye başlangıç profilleri
tests                Çekirdek birim testleri
scripts              Doğrulanmış motor indirme ve yayın paketleme
```

Profiller tek bir shell komutuna dönüştürülmez. Her seçenek `ProcessStartInfo.ArgumentList` üzerinden ayrı argüman olarak iletilir; böylece shell yorumlama ve komut enjeksiyonu engellenir.

## Güvenlik ve gizlilik

ZapretTR herhangi bir proxy veya uzak sunucu sağlamaz. Trafik yerel zapret2/WinDivert motoru tarafından işlenir. Mevcut alfa sürümü telemetri toplamaz. Güvenlik bildirimi için [SECURITY.md](SECURITY.md) dosyasına bakın.

Uygulamayı ve motor dosyalarını yalnızca güvenilir sürüm sayfasından indirin. İmzalanmamış erken sürümlerde Windows SmartScreen uyarısı görülebilir; yayımlanan SHA-256 değerini doğrulayın.

## Upstream ve lisans

ZapretTR, aktif [bol-van/zapret2](https://github.com/bol-van/zapret2) kaynak ağacını temel alır. zapret2 ve bu depodaki upstream bileşenler MIT lisanslıdır. Paketlenen motorun telif ve lisans metni dağıtıma dahil edilir. Ayrıntılar için [LICENSE](LICENSE) ve [NOTICE.md](NOTICE.md) dosyalarına bakın.

## Yol haritası

- Liquid Glass tasarım sistemi ve özel pencere kabuğu
- Bağlantı tanılama ve otomatik profil önerisi
- Türk Telekom, TurkNet, Vodafone ve diğer ağlardan anonim olmayan, kullanıcı onaylı yerel test matrisi
- Sistem tepsisi ve Windows başlangıcı
- Profil dry-run doğrulaması
- İmzalı installer ve otomatik güncelleme
- İngilizce arayüz ve dokümantasyon

Katkıda bulunmadan önce [CONTRIBUTING.md](CONTRIBUTING.md) belgesini okuyun.
