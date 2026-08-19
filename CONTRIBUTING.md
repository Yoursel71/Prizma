# Katkı rehberi

Katkılar küçük, test edilebilir ve tek amaçlı olmalıdır. Yeni bir profil eklerken operatör, bağlantı türü, bölge, tarih ve zapret2 sürümü gibi yeniden üretilebilir test koşullarını açıklayın. Kişisel IP adresi, alan adı geçmişi veya hassas günlük eklemeyin.

Değişiklik göndermeden önce:

```powershell
dotnet format Prizma.sln --verify-no-changes
dotnet build Prizma.sln --configuration Release
dotnet test Prizma.sln --configuration Release
```

GUI değişikliklerine açık ve koyu arka planda ekran görüntüsü ekleyin. Yeni motor seçenekleri upstream `docs/manual.en.md` ile uyumlu olmalı ve mümkünse dry-run testi içermelidir.
