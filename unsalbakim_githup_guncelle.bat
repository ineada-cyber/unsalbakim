@echo off
chcp 65001 >nul
setlocal EnableExtensions
title UNSALBAKIM_GITHUP_GUNCELLE
rem ================================================================
rem  unsalbakim_githup_guncelle.bat  -  Ali Ünsal (yalnız kişisel kullanım)
rem  UNSAL BAKIM kaynak kodu değişikliklerini GitHub'daki (GENEL/PUBLIC)
rem  "unsalbakim" deposuna gönderir.
rem
rem  Nereye konur : UnsalBakim.sln dosyasının bulunduğu klasöre
rem                 (gelistirme\src). Çift tıklayınca:
rem   1) Bu klasörü Belgeler\GitHub\unsalbakim klasörüne eşitler
rem      (bin, obj, .vs, exe, ayarlar.txt, gizli_anahtar* KOPYALANMAZ)
rem   2) İçinde API / imza anahtarı var mı diye son kez bakar
rem   3) Değişenleri gösterir, kısa not ister, Commit + Push yapar
rem
rem  ÖNEMLİ: bu depo GENEL/PUBLIC -- buraya giden her şeyi HERKES
rem  görebilir. Bu script sadece src\ (C# kaynak kodu) klasörünü
rem  gönderir; dokumanlar\ (kılavuz+diyalog) ve AppsScript\ (sunucu
rem  kodu) BİLEREK gönderilMEZ. Not: Core\Musteri.cs içindeki Super
rem  Kullanıcı şifresi ve API token gibi sabitler src\ içinde olduğu
rem  için onlar da HERKESE AÇIK olur (program zaten imzalı/obfuske
rem  edilmeden dağıtıldığından ek bir risk değil, ama bilinçli olun).
rem ================================================================

set "REPO=%USERPROFILE%\Documents\GitHub\unsalbakim"
set "KAYNAK=%~dp0"
if "%KAYNAK:~-1%"=="\" set "KAYNAK=%KAYNAK:~0,-1%"

echo.
echo  ===== UNSAL BAKIM -^> GitHub (genel depo: ineada-cyber/unsalbakim) =====
echo.

rem --- 1. Kontroller ---------------------------------------------------
if not exist "%KAYNAK%\UnsalBakim.sln" (
  echo  [HATA] Bu dosya UNSAL BAKIM proje klasöründe değil -- gelistirme\src klasörüne koyun.
  echo         UnsalBakim.sln ile aynı klasöre koyun.
  goto SON
)
if not exist "%REPO%\.git" (
  echo  [HATA] Depo klasörü bulunamadı: %REPO%
  echo         GitHub Desktop ile unsalbakim deposunu bu yere klonlayın.
  goto SON
)

rem --- git.exe bul ---------------------------------------------------
set "GIT="
where git >nul 2>&1 && set "GIT=git"
if not defined GIT if exist "%ProgramFiles%\Git\cmd\git.exe" set "GIT=%ProgramFiles%\Git\cmd\git.exe"
if not defined GIT for /d %%D in ("%LOCALAPPDATA%\GitHubDesktop\app-*") do (
  if exist "%%D\resources\app\git\cmd\git.exe" set "GIT=%%D\resources\app\git\cmd\git.exe"
)
if not defined GIT (
  echo  [HATA] git bulunamadı. GitHub Desktop kurulu mu?
  goto SON
)

rem --- 2. Eşitle (proje klasörü başka yerdeyse) ---------------------------
if /i "%KAYNAK%"=="%REPO%" (
  echo  Proje zaten depo klasöründe, kopyalama atlandı.
) else (
  echo  Dosyalar depo klasörüne eşitleniyor...
  robocopy "%KAYNAK%" "%REPO%" /MIR /R:1 /W:1 /NFL /NDL /NJH /NJS /NP ^
    /XD .git .vs bin obj ^
    /XF ayarlar.txt gizli_anahtar*.xml *.exe *.pdb *.zip *.user *.suo ^
        README.md .gitignore .gitattributes >nul
  if errorlevel 8 (
    echo  [HATA] Kopyalama başarısız. Dosyalardan biri açık olabilir; Visual Studio'yu kapatıp tekrar deneyin.
    goto SON
  )
)

cd /d "%REPO%"

rem --- 3. Değişiklik var mı ----------------------------------------------
"%GIT%" add -A
"%GIT%" diff --cached --quiet
if not errorlevel 1 (
  echo.
  echo  Değişiklik yok, GitHub zaten güncel.
  goto SON
)

rem --- 4. Gizli anahtar güvenlik kontrolü -------------------------------
"%GIT%" grep --cached -l -E "sk-ant-[A-Za-z0-9_-]{20,}|sk-proj-[A-Za-z0-9_-]{20,}|<D>[A-Za-z0-9+/=]{20,}|-----BEGIN (RSA )?PRIVATE KEY-----" >"%TEMP%\unsalbakim_anahtar.txt" 2>nul
if not errorlevel 1 (
  "%GIT%" reset -q
  echo.
  echo  [DUR] Aşağıdaki dosyalarda API ya da gizli imza anahtarı var:
  type "%TEMP%\unsalbakim_anahtar.txt"
  echo.
  echo  Hiçbir şey gönderilmedi. Anahtarı dosyadan çıkarıp tekrar çalıştırın.
  del "%TEMP%\unsalbakim_anahtar.txt" >nul 2>&1
  goto SON
)
del "%TEMP%\unsalbakim_anahtar.txt" >nul 2>&1

rem --- 5. Göster, not al, kaydet ----------------------------------------
echo.
echo  Değişen dosyalar  (A=yeni  M=değişti  D=silindi):
"%GIT%" status --short
echo.
rem v1 duzeltme: elle yazip Enter'a basmaya GEREK KALMASIN diye -- cift tiklayinca
rem not otomatik tarih/saatten olusur; farkli bir not istenirse bu .bat'i
rem komut satirindan "unsalbakim_githup_guncelle.bat v113 - filo duzeltme" seklinde
rem parametreyle de calistirabilirsiniz.
set "NOT=%~1"
if not defined NOT set "NOT=Guncelleme %date% %time:~0,5%"
set "NOT=%NOT:"='%"
echo  Kisa not: %NOT%

"%GIT%" commit -q -m "%NOT%"
if errorlevel 1 (
  echo  [HATA] Commit yapılamadı.
  goto SON
)
echo  Bilgisayarda kaydedildi: %NOT%

rem --- 6. GitHub'a gönder ------------------------------------------------
echo  GitHub'a gönderiliyor...
"%GIT%" push -q origin main
if errorlevel 1 (
  echo.
  echo  [UYARI] Kayıt bilgisayarda hazır ama GitHub'a gönderilemedi
  echo          ^(internet ya da GitHub girişi^). GitHub Desktop açılıyor:
  echo          üstteki "Push origin" düğmesine basmanız yeterli.
  if exist "%LOCALAPPDATA%\GitHubDesktop\GitHubDesktop.exe" start "" "%LOCALAPPDATA%\GitHubDesktop\GitHubDesktop.exe"
  goto SON
)
echo.
echo  TAMAM - GitHub güncellendi:  https://github.com/ineada-cyber/unsalbakim

:SON
echo.
rem v2 duzeltme (Ali Bey'in bulgusu: "bat hic calismiyor, cok hizli kapaniyor"):
rem "timeout" komutu, cift tiklayinca bazen "Input redirection is not
rem supported" hatasi verip ANINDA cikiyor -- pencere o yuzden goz acip
rem kapayincaya kadar kapaniyordu, hata/sonuc mesaji okunamiyordu. "timeout"
rem yerine girdi yonlendirme sorunu olmayan "ping" ile ayni gecikme saglandi.
echo  (Bu pencere 10 saniye sonra kendiliginden kapanacak...)
ping -n 11 127.0.0.1 >nul
endlocal
