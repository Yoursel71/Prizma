# Profil araştırma notları

Bu dosya ZapretTR 0.4 profillerinin neden bu şekilde kurulduğunu ve hangi davranışın gerçekten uygulandığını kaydeder. Amaç başka projelerin komutlarını körlemesine kopyalamak değil, aynı ağ tekniğini kendi paket motorumuzda denetlenebilir biçimde yeniden uygulamaktır.

## İncelenen açık kaynak yaklaşımları

- **bol-van/zapret2:** `multisplit` ve `multidisorder` birden fazla işaret noktasını çözer, sıralar ve özgün TCP verisini parçalara ayırır. Güncel `blockcheck2` TLS taramasında `1`, `sniext+1`, `host+1`, `midsld` ve `1,midsld` gibi adayları dener. ZapretTR'nin `midsld` işareti, SNI içindeki ikinci seviye etiketin ortasını çözer.
- **ValdikSS/GoodbyeDPI:** modern mod kümeleri HTTP/TLS bölme, ters parça sırası, sahte paket ve isteğe bağlı `-q` QUIC engelini birlikte kullanır. Sabit TTL seçeneğinin siteleri bozabileceği upstream tarafından özellikle belirtilir; bu nedenle ZapretTR'de yalnız Dengeli/Güçlü/Hedefli profillerde TTL=5 bulunur.
- **hufrea/byedpi:** Windows için SNI'ye göre bölme ile disorder yaklaşımının birlikte kullanılmasını, ayrıca host allowlist'iyle etkinin sınırlandırılmasını belgeler. ZapretTR'nin `--host-suffix` seçeneği aynı güvenlik sınırını kendi motorunda uygular.
- **Flowseal/zapret-discord-youtube:** güncel Windows stratejilerinde alan adı listeleriyle UDP/QUIC ve TCP/TLS için ayrı davranışlar kullanır. Bu ayrım nedeniyle ZapretTR Güçlü profili QUIC'i kapatırken Dengeli ve Uyumluluk profilleri açık bırakır.

## 0.4 profil matrisi

| Profil | TLS bölme | Sıra | Sahte | QUIC | Kapsam |
|---|---|---|---|---|---|
| Dengeli | `1,midsld` | Ters | TTL 5 | Açık | HTTP/80 + TLS/443 |
| Uyumluluk | `2,sni` | Normal | Yok | Açık | HTTP/80 + TLS/443 |
| Güçlü | `1,midsld` | Ters | TTL 5 | UDP/443 düşürülür | HTTP/80 + TLS/443 |
| Roblox | `1,midsld` | Ters | TTL 5 | Açık | Roblox alan adı son ekleri |

DNS yönlendirmesi işletim sisteminin adaptör ayarını değiştirmez. Yalnız motorun yakaladığı IPv4 UDP/53 sorgularını `77.88.8.8:1253` hedefine çevirir ve cevapları özgün kaynak görünümüne geri eşler.

## Kaynaklar

- https://github.com/bol-van/zapret2/blob/master/docs/readme.md
- https://github.com/bol-van/zapret2/blob/master/blockcheck2.d/standard/30-faked.sh
- https://github.com/ValdikSS/GoodbyeDPI
- https://github.com/hufrea/byedpi
- https://github.com/Flowseal/zapret-discord-youtube
