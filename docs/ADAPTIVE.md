# Prizma Adaptive profil laboratuvarı

Prizma evrensel bir “en hızlı profil” varsaymaz. DPI davranışı; ISS, rota, modem ve zamana göre değişebildiğinden kazanan bu bilgisayarın mevcut ağında ölçülür.

## Arama uzayı

Laboratuvar 160 deterministik kombinasyonu sınar:

- TLS bölme: `1`, `2`, `midsld`, `1+2+midsld`
- Gönderim: sıralı veya ters
- Sahte politika: kapalı, geçmiş TCP sıra numarası, hatalı TCP checksum, hedefli TTL 5, hedefli TTL 5 + geçmiş sıra
- QUIC: açık veya TCP/TLS'e geri düşürme
- DNS: sistem DNS'i veya `cloudflare-dns.com` adına normal TLS doğrulaması yapan wire-format DoH; bootstrap bağlantısı zehirli sistem DNS'inden bağımsız olarak `1.1.1.1` adresine kurulur

Fake adayları varsayılan olarak Roblox, Discord ve YouTube alan adlarıyla sınırlandırılır. TTL+SEQ adayında sıra numarası da geçmişe çekilir; wrong-checksum ayrı bir uyumluluk hattında ölçülür. Global QUIC engeli yalnız ölçüm gerçekten gerekli gösterirse seçilir.

## Ölçüm ve sıralama

Her aday ayrı bir motor sürecinde çalışır; bağlantı havuzu ve HTTP önbelleği yeniden oluşturulur. Roblox ve tarafsız Cloudflare hedeflerinde normal TLS sertifika doğrulamasıyla erişim/gecikme, küçük sabit boyutlu yanıtta Mbps ölçülür. Toplam indirilen uygulama verisi 48 MB ile sınırlıdır.

Her adaydan önce Windows DNS çözümleyici önbelleği temizlenir. Bu, bir önceki adayın temiz veya zehirli cevabının sonraki DNS hattını etkilemesini önler. DoH sorgusu başarısız olursa motor sessizce sistem DNS'ine düşmez; istemciye `SERVFAIL` döndürür. TLS sertifika adı daima `cloudflare-dns.com` kalır ve sertifika doğrulaması kapatılamaz.

Sıralama sözlüksel öncelik kullanır:

1. erişim başarı oranı;
2. başarılı hedef sayısı;
3. düşük medyan TLS/TTFB gecikmesi;
4. yüksek Mbps;
5. aynı sonuçta deterministik profil kimliği.

Bu nedenle çok hızlı ama Roblox'a erişemeyen bir aday, bütün hedeflere erişen daha yavaş bir adayın önüne geçemez. Arayüzdeki 0–100 puan yalnız okunabilir bir özet göstergesidir; kazananı belirleyen yukarıdaki güvenli sıralamadır.

Kazanan `%LocalAppData%\Prizma\profile-lab.json` içinde ağ geçidi ve DNS parmak iziyle saklanır. Ağ değiştiğinde öneri yüklenmez ve yeni ölçüm istenir. Test sırasında kurulu Windows hizmeti kabul edilmez; aynı anda iki WinDivert motoru çalıştırılmaz.

## Araştırma temeli

- [zapret2 blockcheck2 çoklu TLS adayları](https://github.com/bol-van/zapret2/blob/master/blockcheck2.d/standard/20-multi.sh)
- [zapret2 QUIC adayları](https://github.com/bol-van/zapret2/blob/master/blockcheck2.d/standard/90-quic.sh)
- [GoodbyeDPI çalışma biçimi ve modern modlar](https://github.com/ValdikSS/GoodbyeDPI#how-does-it-work)
- [ByeDPI bölme/disorder notları](https://github.com/hufrea/byedpi)
- [Geneva araştırması: tekrar, kötü durum skoru ve karmaşıklık cezası](https://geneva.cs.umd.edu/papers/geneva_ccs19.pdf)
- [Cloudflare açık kaynak hız ölçümü](https://github.com/cloudflare/speedtest)
- [Cloudflare wire-format DoH API](https://developers.cloudflare.com/1.1.1.1/encryption/dns-over-https/make-api-requests/dns-wireformat/)
- [RFC 8484 — DNS Queries over HTTPS](https://www.rfc-editor.org/rfc/rfc8484.html)

Bu kaynakların çalıştırılabilir dosyaları pakete alınmaz. Teknikler temiz odada, Prizma'nın kendi C# paket hattında yeniden uygulanır.
