using System;
using System.Diagnostics;
using System.IO;

namespace UnsalBakim.Core
{
    /// <summary>
    /// USB'den calisan exe'yi bu bilgisayara GIZLI olarak kurar.
    /// Konum: %ProgramFiles%\Windows Bakim  (standart kullanici silemez/degistiremez)
    ///   - klasor + exe: Gizli + Sistem oznitelikli
    ///   - NTFS izinleri: Kullanicilar sadece Oku/Calistir; silme/yazma yok
    ///   - gorevler SYSTEM olarak, "gizli" isaretli kurulur
    /// Boylece ogrenci (standart kullanici) ne dosyayi gorur ne de kaldirabilir.
    /// </summary>
    public static class Installer
    {
        public static string HedefKlasor =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Windows Bakim");

        public static string HedefExe => Path.Combine(HedefKlasor, "UnsalBakim.exe");

        private const string Log = "kurulum.txt";

        /// <summary>v109: "kurulum yarida kaldi" isareti. Kur() basinda yazilir, sonunda silinir.</summary>
        public static string DevamIsaretiYolu =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "UnsalBakim", "kurulum_devam.txt");

        /// <summary>v109: Isaret 15 dk'dan eskiyse (yani kurulum bir onceki denemede yarida
        /// kaldiysa) kurulumu sessizce tamamlar. 5 dk'lik dongu ve saatlik guncelleme gorevi cagirir.</summary>
        public static void YarimKalanKurulumuTamamla()
        {
            try
            {
                if (!File.Exists(DevamIsaretiYolu)) return;
                if ((DateTime.Now - File.GetLastWriteTime(DevamIsaretiYolu)).TotalMinutes < 15) return;
                Logger.Write(Log, "[Kurtarma] Onceki kurulum yarida kalmis -- sessizce tamamlaniyor.");
                var ayar = new AppSettings(); ayar.Load();
                Kur(ayar);
                try { DriveLog.Gonder("kurulum_kurtarildi", "Yarida kalan kurulum tamamlandi (" + License.Surum + ")."); } catch { }
            }
            catch (Exception ex) { Logger.Write(Log, "[Kurtarma] hata: " + ex.Message); }
        }

        /// <summary>v109: Kurulu klasordeki ".eski_" (yeniden adlandirilmis eski exe) ve yarim kalmis
        /// gecici guncelleme dosyalarini temizler. Silinemeyen (hala kilitli) dosya sonraki sefere kalir.</summary>
        public static void EskiArtiklariTemizle()
        {
            try
            {
                if (!Directory.Exists(HedefKlasor)) return;
                foreach (var f in Directory.GetFiles(HedefKlasor))
                {
                    string ad = Path.GetFileName(f);
                    if (ad.IndexOf(".eski_", StringComparison.OrdinalIgnoreCase) >= 0 || ad.EndsWith(".eski", StringComparison.OrdinalIgnoreCase)
                        || ad.Equals("guncelleme_gelen.zip", StringComparison.OrdinalIgnoreCase))
                    {
                        try { File.SetAttributes(f, FileAttributes.Normal); File.Delete(f); } catch { }
                    }
                }
                string gecici = Path.Combine(HedefKlasor, "Guncelleme_gecici");
                if (Directory.Exists(gecici)) try { Directory.Delete(gecici, true); } catch { }
            }
            catch { }
        }

        /// <summary>Kurulumu yapar, kurulan klasor yolunu dondurur.</summary>
        public static string Kur(AppSettings ayar)
        {
            string kaynak = System.Reflection.Assembly.GetExecutingAssembly().Location;

            // v109 (Ali Bey'in istegi: "version guncellemede bilgisayarda bir sey calissa da ust
            // yetki ile yerine getirilmeli"): ONCEDEN burada ILK IS olarak TUM gorevler siliniyordu.
            // Kurulum o noktadan sonra yarida kalirsa (elektrik, Filo'dan Kapat, hata) makinede HIC
            // gorev kalmiyordu -- saatlik guncelleme gorevi de silindigi icin makine kendini bir
            // daha ONARAMIYORDU. Artik gorevler SILINMIYOR (her biri asagida /f ile UZERINE
            // yaziliyor); kilitli exe sorunu zaten "yeniden adlandir + yenisini koy" teknigiyle
            // (EskiTemizle) ve gerekirse benzersiz adla kurmayla cozuluyor. Ayrica bir "kurulum
            // devam ediyor" isareti yaziliyor: kurulum bitmeden kesilirse, 5 dk'lik dongu veya
            // saatlik guncelleme gorevi bunu gorup kurulumu KENDILIGINDEN tamamliyor.
            try { Directory.CreateDirectory(Path.GetDirectoryName(DevamIsaretiYolu)); File.WriteAllText(DevamIsaretiYolu, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")); } catch { }
            int kapatilan = EskiSurecleriKapat();
            if (kapatilan > 0) Logger.WriteFileOnly(Log, "Kurulum: calisan " + kapatilan + " eski UnsalBakim sureci zorla kapatildi.");
            System.Threading.Thread.Sleep(700);
            EskiArtiklariTemizle();

            // Onceki kurulumdan kalan kisitli izin/sahiplik/oznitelikleri temizle
            // (yoksa uzerine yazarken "erisim reddedildi" olabilir)
            if (Directory.Exists(HedefKlasor))
            {
                // SID tabanli (dil bagimsiz): sahipligi Administrators'a al + Tam Denetim ver
                NativeRunner.Run("icacls.exe", "\"" + HedefKlasor + "\" /setowner *S-1-5-32-544 /t /c /q", Log, false);
                NativeRunner.Run("icacls.exe", "\"" + HedefKlasor + "\" /grant *S-1-5-32-544:(OI)(CI)F /t /c /q", Log, false);
                try { new DirectoryInfo(HedefKlasor).Attributes = FileAttributes.Directory; } catch { }
                try
                {
                    foreach (var f in Directory.GetFiles(HedefKlasor))
                        try { File.SetAttributes(f, FileAttributes.Normal); } catch { }
                }
                catch { }
            }

            Directory.CreateDirectory(HedefKlasor);

            // --- exe'yi kur ---
            // Varsayilan ada (UnsalBakim.exe) yazmayi dene; kilitli/erisilemezse
            // BENZERSIZ yeni ad kullan (kilit sorununu tamamen asar), gorevleri ona bagla.
            string hedefExe = HedefExe;
            string cfg = kaynak + ".config";
            if (!string.Equals(Path.GetFullPath(kaynak), Path.GetFullPath(hedefExe), StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    KopyalaGuvenli(kaynak, hedefExe);
                }
                catch
                {
                    // Varsayilan ad kilitli -> benzersiz ad ile kur (kesin calisir)
                    hedefExe = Path.Combine(HedefKlasor, "WinBakim_" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".exe");
                    File.Copy(kaynak, hedefExe, true);
                }
                if (File.Exists(cfg)) { try { File.Copy(cfg, hedefExe + ".config", true); } catch { } }
            }

            // Gizli + Sistem oznitelikleri
            try { File.SetAttributes(hedefExe, FileAttributes.Hidden | FileAttributes.System); } catch { }
            try
            {
                var di = new DirectoryInfo(HedefKlasor);
                di.Attributes |= FileAttributes.Hidden | FileAttributes.System;
            }
            catch { }

            // NTFS izinleri: Ozel icacls YOK. Program Files VARSAYILAN izinleri zaten
            // standart kullanicilara (ogrenci) sadece Oku/Calistir verir; yazma/silme yok.
            // Yoneticiler Tam Denetim'e sahip kalir -> yeniden kurulum sorunsuz olur.
            // Gizlilik icin klasor+exe Gizli+Sistem oznitelikli (yukarida ayarlandi).

            // v57.1 GERI ALINDI (2 Kasim 2026, canli testte bulundu): ProgramDataErisiminiKisitla()
            // metodu asagida DURUYOR ama BURADAN CAGRILMIYOR. Neden: bu metodun varsaydigi
            // "panel yonetici yetkisiyle calisir" onermesi YANLIS -- app.manifest BILEREK
            // asInvoker (standart yetki) ile basliyor (bkz. Yetki.cs) ve Installer.Kur() hicbir
            // yerde once yukseltme (Yetki.YukseltVeCalistir) yapmiyor. Sonuc: ACL kilidi
            // uygulandiktan hemen sonra AYNI (yukseltilmemis) surec LOG\*.txt / gorev\*.xml
            // dosyalarina yazmaya devam etmeye calisiyor ve KENDI KENDINI DISLIYOR -- Ali Bey'in
            // makinesinde canli olarak "Erisim engellendi" / "erisim reddedildi" ile dogrulandi.
            // Panelin cok yerinde (orn. yerel internet engeli baslat/durdur dugmeleri) Logger
            // cagrilari da DOGRUDAN bu surecten calistigi icin, cagriyi Kur() icinde sona almak
            // da yeterli DEGIL -- dogru cozum, Installer.Kur() cagrilmadan once panelin gercekten
            // yukseltilmis oldugunu dogrulamak (Yetki.Yukseltilmis()) ve degilse once yukseltmek;
            // bu, MainForm'daki kurulum akisini (async/relaunch) etkileyen ayri bir degisiklik
            // oldugu icin ACELE YAPILMADI. ProgramData hala sadece Gizli+Sistem oznitelikli
            // (yukarida) -- yani onceki (v57) seviyede korunuyor, daha kotu degil.

            // Ayarlari ProgramData'ya yaz
            try { ayar.Save(); } catch { }

            // Gorevleri KURULAN exe'ye (gercek yola), GIZLI olarak bagla
            TaskHelper.HedefExe = hedefExe;
            TaskHelper.KurulumYap(ayar, true);

            // Oyun denetimi tek gorev uzerinden her 5 dakikada bir calisir (v57, 2. revizyon --
            // bkz. TaskHelper.OyunEngelGoreviKur); ayri rapor gorevi kurulmaz.
            // v100 duzeltme: bu yorum onceden (yanlislikla) "3 dakikada" diyordu -- kod hep 5
            // dakikaydi, sadece yorum satiri eskiydi (bkz. Ogretmen Kilavuzu doc-tutarlilik notu).
            // Performans/ayarlar: gorsel efektler performansa, cop kutusu kapat/bosalt,
            // yuksek performans plani, guvenlik duvari + virus programi ACIK, bozuk bakim policy temizligi
            try { PerformansAyar.Uygula(ayar); } catch (Exception ex) { Logger.Write(Log, "[!] Performans ayari: " + ex.Message); }

            // Oyun engellemeyi hemen uygula (yeniden baslatma beklemeden).
            // v40: "else" dali eklendi -- bir makine ONCEDEN ogrenci profiliyle kurulup oyun
            // engeli uygulanmisken, sonradan ogretmen profiline veya Ayarlar sekmesinden
            // OyunEngelle=KAPALI'ya gecerse, engel de HEMEN kaldirilsin (kamera ac/kapa ile
            // ayni "her iki yonu de sayginla uygula" deseni -- v31 denetiminde bulunan hatanin ayni sinifi).
            // v92: "Askiya Al" ozelligi Ali Bey'in acik istegiyle TAMAMEN KALDIRILDI -- bu artik
            // kosulsuz ayar.OyunEngelle degerine gore uygulanir.
            if (ayar.OyunEngelle)
            {
                GameControl.SurecKapat();
                GameControl.CalismaEngelle();
            }
            else
            {
                try { GameControl.EngelKaldir(); }
                catch (Exception ex) { Logger.Write(Log, "[!] Oyun engeli kaldirilamadi: " + ex.Message); }
            }

            // v30 denetimi: "ac/kapa bas, bir daha hic tazelenmez" sinifindaki bir diger ayar --
            // kamera kapatma secimi (ogretmen elle bir kez bastiginda kaydediliyordu ama her KUR/
            // sessiz-guncelleme sonrasi DEVICE durumuna yeniden uygulanmiyordu; surucu/Windows
            // Update PnP durumunu sifirlarsa teknik olarak geri acilabilirdi). Artik her KUR'da
            // kaydedilmis tercihe gore yeniden uygulanir (hem kapali hem acik kalsin istenen durum).
            try { if (ayar.KameraKapali) KameraKontrol.Kapat(); else KameraKontrol.Ac(); }
            catch (Exception ex) { Logger.Write(Log, "[!] Kamera ayari yeniden uygulanamadi: " + ex.Message); }

            // v82 (Ali Bey'in istegi, 6 Kasim 2026): Denetim Masasi > Guvenlik ve Bakim > Bakim
            // bolumundeki Windows'un kendi "Otomatik Bakim" saatini 12:00 (ogle) sabitler.
            // Kamera ayari gibi her KUR/sessiz-guncelleme sonrasi yeniden uygulanir (harici bir
            // sifirlanmaya karsi guvence; idempotent/zararsiz).
            try
            {
                using (var k = Microsoft.Win32.Registry.LocalMachine.CreateSubKey(
                    @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Schedule\Maintenance"))
                    if (k != null) k.SetValue("MaintenanceTime", 12 * 3600, Microsoft.Win32.RegistryValueKind.DWord);
            }
            catch (Exception ex) { Logger.Write(Log, "[!] Otomatik Bakim saati ayarlanamadi: " + ex.Message); }

            // v109: rol gruplari, klasor izinleri (ProgramData + ogrenci makinesinde Program Files),
            // AppLocker (ogrenci makinesinde, varsayilan DENETIM modu). Kur() her zaman SYSTEM veya
            // yukseltilmis yonetici baglaminda calisir; degilse RolGuvenlik kendisi atlar.
            try { RolGuvenlik.DonguKontrol(ayar, true); } catch (Exception ex) { Logger.Write(Log, "[!] Rol/izin/AppLocker: " + ex.Message); }

            try { if (File.Exists(DevamIsaretiYolu)) File.Delete(DevamIsaretiYolu); } catch { }
            Logger.Write(Log, "GIZLI kurulum tamamlandi: " + HedefKlasor);
            return HedefKlasor;
        }

        /// <summary>Kurulumu kaldirir: gorevler, oyun/VPN/YouTube engelleri, kamera/Defender ayarlari ve kurulu klasor.</summary>
        public static void Kaldir()
        {
            TaskHelper.TumGorevleriKaldir();
            // v52 duzeltmesi: ONCEDEN sadece GameControl.EngelKaldir() cagriliyordu -- VPN engeli,
            // YouTube kisiti, kamera kapatma ve Defender disk haric tutma ayari program kaldirilsa
            // bile makinede KALIYORDU (kilavuzun "kurulumu kaldir -> gorevler, gizli klasor ve
            // engeller kalkar" sozune aykiri). Simdi hepsi tek tek, birbirini etkilemeden kaldirilir.
            try { GameControl.EngelKaldir(); } catch (Exception ex) { Logger.Write(Log, "[!] Oyun engeli kaldirma: " + ex.Message); }
            try { VpnKontrol.KaldirEngel(); } catch (Exception ex) { Logger.Write(Log, "[!] VPN engeli kaldirma: " + ex.Message); }
            try { YoutubeKisit.KaldirEngel(); YoutubeKisit.OperaIfeoEngelKaldir(); } catch (Exception ex) { Logger.Write(Log, "[!] YouTube kisiti kaldirma: " + ex.Message); }
            try { AiSiteEngelle.KaldirEngel(); } catch (Exception ex) { Logger.Write(Log, "[!] AI site engeli kaldirma: " + ex.Message); }
            try { KameraKontrol.Ac(); } catch (Exception ex) { Logger.Write(Log, "[!] Kamera ayari geri alma: " + ex.Message); }
            // v109: AppLocker politikasi ve ProgramData kilidi geri alinir (rol gruplari ve
            // hesaplar SILINMEZ -- bunlar kullanici hesaplaridir, okul isterse elle kaldirir).
            try { AppLockerYonet.Kaldir(); } catch (Exception ex) { Logger.Write(Log, "[!] AppLocker kaldirma: " + ex.Message); }
            try { TarayiciGuvenlik.Uygula(false); } catch { } // v110
            try { RolGuvenlik.ProgramDataKilidiKaldir(); } catch (Exception ex) { Logger.Write(Log, "[!] ProgramData izin geri alma: " + ex.Message); }
            EskiSurecleriKapat();
            System.Threading.Thread.Sleep(500);

            try
            {
                if (Directory.Exists(HedefKlasor))
                {
                    // Sahiplik + Tam Denetim (dil bagimsiz), oznitelikleri ac
                    NativeRunner.Run("icacls.exe", "\"" + HedefKlasor + "\" /setowner *S-1-5-32-544 /t /c /q", Log, false);
                    NativeRunner.Run("icacls.exe", "\"" + HedefKlasor + "\" /grant *S-1-5-32-544:(OI)(CI)F /t /c /q", Log, false);
                    try { new DirectoryInfo(HedefKlasor).Attributes = FileAttributes.Directory; } catch { }
                    foreach (var f in Directory.GetFiles(HedefKlasor, "*", SearchOption.AllDirectories))
                        try { File.SetAttributes(f, FileAttributes.Normal); } catch { }

                    // Dosyalari tek tek sil; kilitli olan varsa yana tasi (rename calisan exe'de bile calisir)
                    foreach (var f in Directory.GetFiles(HedefKlasor, "*", SearchOption.AllDirectories))
                        EskiTemizle(f);

                    try { Directory.Delete(HedefKlasor, true); }
                    catch { GecikmeliSil(HedefKlasor); }  // kalan kilitli dosyalar cikista silinsin
                }
            }
            catch (Exception ex) { Logger.Write(Log, "[!] Klasor temizligi: " + ex.Message); }

            Logger.Write(Log, "Kurulum kaldirildi.");
        }

        /// <summary>ProgramData\UnsalBakim klasorunu sadece SYSTEM ve yerel Administrators
        /// grubuna acar. Hidden/System yalnizca gorunum niteligidir; asil koruma NTFS ACL'dir.</summary>
        private static void ProgramDataErisiminiKisitla()
        {
            string kok = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "UnsalBakim");
            Directory.CreateDirectory(kok);
            // Klasoru ilk olusturan standart kullanici sahip konumunda kalmasin; once
            // sahipligi Administrators'a aktar, sonra eski miras izinlerini kaldir.
            int sahip = NativeRunner.Run("icacls.exe",
                "\"" + kok + "\" /setowner *S-1-5-32-544 /T /C /Q", Log, true);
            if (sahip != 0)
                throw new InvalidOperationException("icacls sahiplik cikis kodu " + sahip + ".");
            int sonuc = NativeRunner.Run("icacls.exe",
                "\"" + kok + "\" /inheritance:r /grant:r *S-1-5-18:(OI)(CI)F *S-1-5-32-544:(OI)(CI)F /T /C /Q",
                Log, true);
            if (sonuc != 0)
                throw new InvalidOperationException("icacls ACL cikis kodu " + sonuc + " (ProgramData erisim izinleri uygulanamadi).");
            try
            {
                var di = new DirectoryInfo(kok);
                di.Attributes |= FileAttributes.Hidden | FileAttributes.System;
            }
            catch { }
            string logKok = Path.Combine(kok, "LOG");
            if (Directory.Exists(logKok))
            {
                try
                {
                    var diLog = new DirectoryInfo(logKok);
                    diLog.Attributes |= FileAttributes.Hidden | FileAttributes.System;
                }
                catch { }
            }
            Logger.Write(Log, "ProgramData\\UnsalBakim ACL: yalnizca SYSTEM ve Administrators Tam Denetim; Gizli+Sistem niteligine alindi.");
        }

        private static void KopyalaGuvenli(string kaynak, string hedef)
        {
            try { if (File.Exists(hedef)) File.SetAttributes(hedef, FileAttributes.Normal); } catch { }
            EskiTemizle(hedef);  // varsa eskisini sil / yana tasi (calisan exe'de bile calisir)

            for (int i = 0; i < 5; i++)
            {
                try { File.Copy(kaynak, hedef, true); return; }
                catch (Exception)
                {
                    if (i == 4) throw;
                    EskiSurecleriKapat();
                    System.Threading.Thread.Sleep(800);
                    try { if (File.Exists(hedef)) File.SetAttributes(hedef, FileAttributes.Normal); } catch { }
                    EskiTemizle(hedef);
                }
            }
        }

        /// <summary>Hedef dosya varsa siler; silinemiyorsa (kilitli/calisan) yana tasir.
        /// Calisan bir exe silinemez ama YENIDEN ADLANDIRILABILIR, boylece uzerine yazilabilir.</summary>
        private static void EskiTemizle(string hedef)
        {
            try
            {
                if (!File.Exists(hedef)) return;
                try { File.SetAttributes(hedef, FileAttributes.Normal); } catch { }
                try { File.Delete(hedef); }
                catch
                {
                    try { File.Move(hedef, hedef + ".eski_" + DateTime.Now.Ticks); } catch { }
                }
            }
            catch { }
        }

        /// <summary>Kurulu kopyayi (ve SYSTEM gorevinin baslattigi exe'yi) kapatir; kendi surecimizi atlar.</summary>
        private static int EskiSurecleriKapat()
        {
            int sayi = 0;
            try
            {
                int me = Process.GetCurrentProcess().Id;
                try { foreach (var p0 in Process.GetProcessesByName("UnsalBakim")) if (p0.Id != me) sayi++; } catch { }
                // SYSTEM gorevinin baslattigi kurulu kopyayi da kapatabilmek icin taskkill (kendi PID haric)
                NativeRunner.Run("taskkill.exe", "/f /im UnsalBakim.exe /fi \"PID ne " + me + "\"", Log, false);
                foreach (var p in Process.GetProcessesByName("UnsalBakim"))
                {
                    if (p.Id == me) continue;
                    try { p.Kill(); p.WaitForExit(3000); } catch { }
                }
            }
            catch { }
            return sayi;
        }

        // Program kapandiktan ~3 sn sonra klasoru silen tek seferlik, bagimsiz cmd (beklenmez)
        private static void GecikmeliSil(string klasor)
        {
            try
            {
                var psi = new ProcessStartInfo("cmd.exe",
                    "/c timeout /t 3 /nobreak >nul & rd /s /q \"" + klasor + "\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process.Start(psi); // WaitForExit YOK: biz cikinca o silecek
            }
            catch (Exception ex) { Logger.Write(Log, "[!] Gecikmeli silme baslatilamadi: " + ex.Message); }
        }
    }
}
