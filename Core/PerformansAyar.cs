using System;
using System.IO;
using System.Management;
using Microsoft.Win32;

namespace UnsalBakim.Core
{
    /// <summary>
    /// Eski .bat paketinden tasinan ek ayarlar:
    ///  - Gorsel efektleri PERFORMANSA al (mevcut + tum yuklu kullanicilar + yeni kullanicilar/Default)
    ///  - Cop kutusunu kapat (silinen dosya dogrudan silinir) + icerigini bosalt
    ///  - Yuksek performans guc plani
    ///  - Guvenlik duvari ACIK + virus programi (Defender servisi) ACIK (AV'yi kapatmaz)
    ///  - Windows'un bozuk "Otomatik Bakim" policy'sini temizle, Zamanlayici servis + bakim gorevlerini ac, WinSAT
    /// </summary>
    public static class PerformansAyar
    {
        private const string Log = "performans.txt";

        public static void Uygula(AppSettings ayar)
        {
            Logger.Write(Log, "================= PERFORMANS/AYAR BASLADI =================");

            // Guvenlik duvari + virus programi her zaman ACIK kalsin
            FirewallVeAV();

            // v110: Defender istisnasi (Add-MpPreference -ExclusionPath/-ExclusionProcess) KALDIRILDI.
            // Bu okullarda AV olarak Norton var, Defender degil -- istisna hem islevsizdi hem de
            // "program kendini tarama disi birakiyor" davranisi sezgisel AV motorlarinda (ve Google
            // Drive virus taramasinda) kirmizi bayrakti (bkz. 13 Kasim 2026 incelemesi). Guncelleme
            // guvenligi artik dosyayi tarama disi birakmakla degil, imza+SHA-256 dogrulamasiyla saglanir.

            if (ayar.GorselPerformans)
            {
                Logger.UI("Gorsel efektler performansa aliniyor (tum kullanicilar)...");
                GorselTumKullanicilar();
            }

            if (ayar.CopKutusuKapat)
            {
                Logger.UI("Cop kutusu kapatiliyor (silinen dosya dogrudan silinir)...");
                CopKutusuKapat();
                CopKutusuBosalt();
            }

            if (ayar.YuksekPerformansPlan)
            {
                Logger.UI("Yuksek performans guc plani uygulaniyor...");
                NativeRunner.Run("powercfg.exe", "/setactive SCHEME_MIN", Log, false);
            }

            BakimPolicyDuzelt();

            Logger.Write(Log, "================= PERFORMANS/AYAR BITTI =================");
        }

        /// <summary>
        /// HIZLI koruma guvencesi: sik calisan yollarda (oyun engelleme 3 dk vb.) cagrilir.
        /// Guvenlik duvarini hemen acar; Defender gercek zamanli koruma KAPALI ise tam korumayi
        /// geri acar. AV zaten acik ya da 3. parti AV (Norton) aktifse bosa islem yapmaz.
        /// </summary>
        public static void KorumaGuvence()
        {
            // Guvenlik duvari (hizli, pencere yok)
            NativeRunner.Run("netsh.exe", "advfirewall set allprofiles state on", Log, false);

            // Defender gercek zamanli koruma durumunu program ici (WMI) oku - hizli
            bool realtimeKapali = false;
            bool defenderVar = false;
            try
            {
                using (var ara = new ManagementObjectSearcher(
                    @"root\Microsoft\Windows\Defender",
                    "SELECT RealTimeProtectionEnabled, AMRunningMode FROM MSFT_MpComputerStatus"))
                {
                    foreach (ManagementObject mo in ara.Get())
                    {
                        defenderVar = true;
                        // AMRunningMode "Normal" degilse Defender pasiftir (ornn. Norton aktif) -> karisma
                        string mode = (mo["AMRunningMode"] ?? "").ToString();
                        bool rt = false;
                        try { rt = Convert.ToBoolean(mo["RealTimeProtectionEnabled"]); } catch { }
                        if (mode.IndexOf("Normal", StringComparison.OrdinalIgnoreCase) >= 0 && !rt)
                            realtimeKapali = true;
                    }
                }
            }
            catch { defenderVar = false; }

            // Defender aktif moddayken korumasi kapaliysa tam korumayi geri ac
            if (defenderVar && realtimeKapali)
                FirewallVeAV();
        }

        /// <summary>Tamper Protection durumu: true=acik, false=kapali, null=bilinmiyor/Defender pasif.</summary>
        public static bool? TamperAcik()
        {
            try
            {
                using (var ara = new ManagementObjectSearcher(
                    @"root\Microsoft\Windows\Defender",
                    "SELECT IsTamperProtected FROM MSFT_MpComputerStatus"))
                {
                    foreach (ManagementObject mo in ara.Get())
                        return Convert.ToBoolean(mo["IsTamperProtected"]);
                }
            }
            catch { }
            return null;
        }

        /// <summary>Windows Guvenligi "Virus ve tehdit korumasi" sayfasini acar (Tamper dugmesi icin).</summary>
        public static void GuvenlikSayfasiniAc()
        {
            try
            {
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo("windowsdefender://threat") { UseShellExecute = true });
            }
            catch { }
        }

        // ---- Guvenlik duvari + AV ----
        public static void FirewallVeAV()
        {
            // 1) Guvenlik duvari ACIK (tum profiller)
            NativeRunner.Run("netsh.exe", "advfirewall set allprofiles state on", Log, false);

            // 2) Defender servisleri etkin
            NativeRunner.Run("sc.exe", "config WinDefend start= auto", Log, false);
            NativeRunner.Run("sc.exe", "start WinDefend", Log, false);
            NativeRunner.Run("sc.exe", "config WdNisSvc start= auto", Log, false);
            NativeRunner.Run("sc.exe", "config SecurityHealthService start= auto", Log, false);

            // 3) v60.1 GUVENLIK DUZELTMESI (3 Kasim 2026, canli tespit edildi): Defender
            //    KORUMALARINI PowerShell/Set-MpPreference ile "ac" (gercek zamanli + davranis +
            //    PUA + bulut) girisimi BURADAN TAMAMEN KALDIRILDI. SEBEP: deger $false (yani
            //    "kapatma, acik kalsin") olsa bile, Defender'in ML tabanli imzasi
            //    "Trojan:Win32/Commando.A!ml" bu komutu -ExecutionPolicy Bypass ile cagrilan,
            //    imzasiz/karistirilmis bir programdan gelen "Set-MpPreference -DisableRealtimeMonitoring"
            //    kalibi olarak esliyor (gercek fidye yazilimlari Defender'i AYNI YONTEMLE kapatiyor;
            //    Defender boolean degerini DEGIL, komut kalibini kontrol ediyor) ve HER Agir Bakim
            //    calismasinda "Ciddi" seviyede tehdit alarmi uretiyordu -- hem anlamsiz gurultu hem
            //    de UnsalBakim.exe'nin zamanla itibar bazli olarak tamamen karantinaya alinma riski.
            //    Normal sartlarda Windows Defender gercek zamanli korumasi zaten varsayilan ACIKTIR;
            //    bunu "garantiye almaya" calisan bu kod calismiyordu (Tamper Protection acikken
            //    zaten engelleniyordu) VE zarar veriyordu -- net fayda negatif, kaldirildi.
            //    Firewall/servis (sc.exe) komutlari YUKARIDA/asagida AYNEN kaliyor, flaglanmiyorlar.

            // 4) SmartScreen (uygulama ve tarayici denetimi) ACIK
            try
            {
                using (var k = Microsoft.Win32.Registry.LocalMachine.CreateSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer"))
                    k.SetValue("SmartScreenEnabled", "RequireAdmin", Microsoft.Win32.RegistryValueKind.String);
            }
            catch { }
            try
            {
                using (var k = Microsoft.Win32.Registry.LocalMachine.CreateSubKey(
                    @"SOFTWARE\Policies\Microsoft\Windows\System"))
                    k.SetValue("EnableSmartScreen", 1, Microsoft.Win32.RegistryValueKind.DWord);
            }
            catch { }

            // 5) WinRE "Otomatik Onarim" SYSTEM komut istemi acigi kapatilir -- USB boot kapali
            //    olsa bile, acilisi 2-3 kez zorla kesen bir ogrenci bu ekrana (sifresiz, SYSTEM
            //    yetkili komut istemi) dusebiliyor ve "net localgroup Administrators ... /add" ile
            //    kendini admin yapabiliyordu. Bu iki ayar Windows'un kendi onerdigi sertlestirme:
            //    tekrarlanan acilis hatasinda otomatik onarima dusmesin, kurtarma ortami devreye
            //    girmesin (makine normal acilista bozuksa BIOS/disk sorunu zaten ayrica cozulur).
            NativeRunner.Run("bcdedit.exe", "/set {default} bootstatuspolicy ignoreallfailures", Log, false);
            NativeRunner.Run("bcdedit.exe", "/set {default} recoveryenabled no", Log, false);
        }

        /// <summary>
        /// Defender'i ASLA zayiflatmaz; sadece kendi kurulu klasorumuzu ve kendi surecimizi
        /// Windows Defender taramasindan/davranis izlemesinden ISTISNA tutar. Bu, otomatik
        /// guncelleme sirasinda yapilan "calisan exe'yi yeniden adlandir + yeni dosyayi ayni
        /// yola kopyala" islemini Defender'in bazen "supheli/fidye yazilimi benzeri davranis"
        /// olarak yorumlayip indirilen guncellemeyi silmesini/karantinaya almasini onlemek icindir.
        /// Genel koruma (FirewallVeAV) her zaman ACIK kalir -- burada kapatma YOK, sadece dar
        /// bir "bu klasore/surece dokunma" istisnasi var. Hata olursa sessizce gecilir (Tamper
        /// Protection acikken bu komutlar engellenebilir; o zaman normal tarama devam eder).
        /// </summary>
        // v110: Artik HICBIR SEY yapmaz (bilerek bos birakildi -- eski cagiranlar kirilmasin diye
        // metod duruyor). Gerekcesi icin Uygula() icindeki nota bakin.
        public static void DefenderKendiIstisnasi()
        {
            Logger.WriteFileOnly(Log, "Defender istisnasi devre disi (v110): guvenlik imza+SHA-256 ile saglanir.");
        }

        // ---- Gorsel efektler: performans ----
        private static void GorselEfektlerYaz(RegistryKey userRoot)
        {
            try
            {
                using (var k = userRoot.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects"))
                    k.SetValue("VisualFXSetting", 2, RegistryValueKind.DWord);
                using (var k = userRoot.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced"))
                {
                    k.SetValue("TaskbarAnimations", 0, RegistryValueKind.DWord);
                    k.SetValue("ListviewAlphaSelect", 0, RegistryValueKind.DWord);
                    k.SetValue("ListviewShadow", 0, RegistryValueKind.DWord);
                    k.SetValue("IconsOnly", 0, RegistryValueKind.DWord);
                }
                using (var k = userRoot.CreateSubKey(@"Software\Microsoft\Windows\DWM"))
                    k.SetValue("EnableAeroPeek", 0, RegistryValueKind.DWord);
                using (var k = userRoot.CreateSubKey(@"Control Panel\Desktop"))
                {
                    k.SetValue("DragFullWindows", "0", RegistryValueKind.String);
                    k.SetValue("FontSmoothing", "2", RegistryValueKind.String);
                    k.SetValue("MenuShowDelay", "0", RegistryValueKind.String);
                    k.SetValue("UserPreferencesMask",
                        new byte[] { 0x90, 0x12, 0x03, 0x80, 0x10, 0x00, 0x00, 0x00 }, RegistryValueKind.Binary);
                }
                using (var k = userRoot.CreateSubKey(@"Control Panel\Desktop\WindowMetrics"))
                    k.SetValue("MinAnimate", "0", RegistryValueKind.String);
            }
            catch (Exception ex) { Logger.Write(Log, "  [!] gorsel yaz: " + ex.Message); }
        }

        public static void GorselTumKullanicilar()
        {
            // 1) Mevcut kullanici
            GorselEfektlerYaz(Registry.CurrentUser);

            // 2) Halen YUKLU kullanici hive'lari (oturum acik olanlar) - S-1-5-21-..., _Classes haric
            try
            {
                foreach (var ad in Registry.Users.GetSubKeyNames())
                {
                    if (!ad.StartsWith("S-1-5-21", StringComparison.OrdinalIgnoreCase)) continue;
                    if (ad.IndexOf("_Classes", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    try { using (var u = Registry.Users.OpenSubKey(ad, true)) if (u != null) GorselEfektlerYaz(u); }
                    catch { }
                }
            }
            catch { }

            // 3) Offline profiller (C:\Users\*\NTUSER.DAT) + yeni kullanicilar icin Default
            try
            {
                string users = Path.GetPathRoot(Environment.SystemDirectory) + "Users";
                if (Directory.Exists(users))
                {
                    int n = 0;
                    foreach (var dir in Directory.GetDirectories(users))
                    {
                        string ad = Path.GetFileName(dir);
                        if (ad.Equals("Public", StringComparison.OrdinalIgnoreCase) ||
                            ad.Equals("All Users", StringComparison.OrdinalIgnoreCase) ||
                            ad.Equals("Default User", StringComparison.OrdinalIgnoreCase) ||
                            ad.Equals("DefaultAppPool", StringComparison.OrdinalIgnoreCase)) continue;
                        OfflineProfilYaz(Path.Combine(dir, "NTUSER.DAT"), "UB_OFF_" + (n++));
                    }
                    // Yeni acilacak kullanicilar
                    OfflineProfilYaz(Path.Combine(users, "Default", "NTUSER.DAT"), "UB_DEF");
                }
            }
            catch { }

            // Ayarlarin bir kismini oturumu kapatmadan canli uygula
            NativeRunner.Run("rundll32.exe", "user32.dll,UpdatePerUserSystemParameters 1, True", Log, false);
        }

        private static void OfflineProfilYaz(string ntuser, string gecici)
        {
            if (!File.Exists(ntuser)) return;
            // reg load ile yukle (oturum acik profilde kilitli olur -> atlanir, zaten yuklu pass'inde islenir)
            int rc = NativeRunner.Run("reg.exe", "load \"HKU\\" + gecici + "\" \"" + ntuser + "\"", Log, false);
            if (rc != 0) return;
            try
            {
                using (var u = Registry.Users.OpenSubKey(gecici, true))
                    if (u != null) GorselEfektlerYaz(u);
            }
            catch { }
            finally
            {
                try { System.GC.Collect(); System.GC.WaitForPendingFinalizers(); } catch { }
                NativeRunner.Run("reg.exe", "unload \"HKU\\" + gecici + "\"", Log, false);
            }
        }

        // ---- Cop kutusu ----
        public static void CopKutusuKapat()
        {
            try
            {
                using (var k = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer"))
                    k.SetValue("NoRecycleFiles", 1, RegistryValueKind.DWord);
            }
            catch { }
            try
            {
                using (var k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer"))
                    k.SetValue("NoRecycleFiles", 1, RegistryValueKind.DWord);
            }
            catch { }
        }

        public static void CopKutusuBosalt()
        {
            NativeRunner.Run("powershell.exe",
                "-NoProfile -ExecutionPolicy Bypass -Command \"Clear-RecycleBin -Force -ErrorAction SilentlyContinue\"",
                Log, false, 120000);
        }

        // ---- Bozuk bakim policy temizligi + Windows bakim gorevlerini ac ----
        /// <summary>
        /// VERI KORUMA (hizli): Sistem Korumasini (System Restore / Gölge Kopya) sistem
        /// surucusunde ACAR -- sistem ayarlarinin/registry'nin geri alinabilmesi icin. Throttle'i
        /// kaldirir (SystemRestorePointCreationFrequency=0) ki koruma noktalari sik alinabilsin.
        /// DUZELTME (6 Kasim 2026, teknik inceleme raporundaki tespit uzerine): Bu fonksiyonun
        /// "dosyaya sag tik -> Onceki surumler ile kurtarma mumkun" ACIKLAMASI artik gecerli
        /// DEGIL -- Dosya Gezgini'ndeki bu ozellik Windows 8'de ISTEMCI surumlerinden (10/11
        /// dahil) KALDIRILDI, sadece Sunucu surumlerinde/paylasilan klasorlerde var. Yani bu
        /// fonksiyon TEK BIR KULLANICI DOSYASININ kolay kurtarilmasini SAGLAMAZ; sadece sistem
        /// ayarlarinin geri alinmasina yarar. Gercek dosya kurtarma guvencesi, Geri Donusum
        /// Kutusu'nun KAPALI OLMAMASINA baglidir (bkz. ayar.CopKutusuKapat).
        /// DUZELTME (v84, 6 Kasim 2026, Ali Bey'in istegi): varsayilan artik Cop Kutusu'nu
        /// KAPATMIYOR (CopKutusuKapat=false) -- boylece ogrenci makinelerinde silinen bir dosya
        /// icin gercek bir kurtarma yolu (Cop Kutusu) var olmaya devam ediyor.
        /// </summary>
        public static void VeriKoruma()
        {
            try
            {
                string sysDrive = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
                // Sistem korumasi throttle kapat (dk; 0 = her istekte nokta alinabilir)
                using (var k = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore"))
                    if (k != null) { try { k.SetValue("SystemRestorePointCreationFrequency", 0, RegistryValueKind.DWord); } catch { } }

                // Sistem korumasini ac (sistem ayarlari icin golge kopya). Depoyu makul tut (%10).
                NativeRunner.Run("powershell.exe",
                    "-NoProfile -ExecutionPolicy Bypass -Command \"Enable-ComputerRestore -Drive '" + sysDrive + "'\"",
                    Log, false, 120000);
                NativeRunner.Run("vssadmin.exe", "Resize ShadowStorage /For=" + sysDrive.TrimEnd('\\') +
                    " /On=" + sysDrive.TrimEnd('\\') + " /MaxSize=10%", Log, false);
                Logger.Write(Log, "Veri koruma: Sistem Korumasi/golge kopya ACIK (sistem ayarlari geri alinabilir).");
            }
            catch (Exception ex) { Logger.Write(Log, "Veri koruma ayari hatasi: " + ex.Message); }
        }

        /// <summary>TrustedInstaller calisiyorsa (aktif bilesen kurulumu/guncellemesi suruyor)
        /// veya bekleyen bir yeniden baslatma varsa true doner. v83 (teknik inceleme raporundaki
        /// tespit uzerine, 6 Kasim 2026): boyle bir anda Guncelleme onbellegine dokunmak, devam
        /// eden bir kurulumu bozabilir.</summary>
        public static bool GuncellemeDevamEdiyorMu()
        {
            try { if (System.Diagnostics.Process.GetProcessesByName("TrustedInstaller").Length > 0) return true; }
            catch { }
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired"))
                    if (k != null) return true;
            }
            catch { }
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending"))
                    if (k != null) return true;
            }
            catch { }
            return false;
        }

        /// <summary>v82 (Ali Bey'in istegi, 6 Kasim 2026): Windows Guncelleme'nin indirme
        /// onbellegini (SoftwareDistribution\Download) bosaltir -- eski/indirilmis guncelleme
        /// dosyalari disk yer kaplar, silinmesi guvenlidir (Windows gerektiginde yeniden indirir).
        /// Kullanicinin KENDI dosyalarina (Downloads, Masaustu vb.) HICBIR SEKILDE dokunmaz.
        /// DUZELTME (v83, 6 Kasim 2026): aktif bir kurulum/bekleyen yeniden baslatma varsa
        /// ARTIK ATLANIYOR (GuncellemeDevamEdiyorMu).</summary>
        public static void GuncellemeOnbellekTemizle()
        {
            if (GuncellemeDevamEdiyorMu())
            {
                Logger.Write(Log, "Guncelleme onbellek temizligi ATLANDI: devam eden bir kurulum veya bekleyen yeniden baslatma var.");
                return;
            }

            NativeRunner.Run("net.exe", "stop wuauserv", Log, false, 30000);
            NativeRunner.Run("net.exe", "stop bits", Log, false, 30000);
            try
            {
                string yol = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SoftwareDistribution", "Download");
                if (Directory.Exists(yol))
                {
                    foreach (var f in Directory.GetFiles(yol, "*", SearchOption.AllDirectories))
                        try { File.Delete(f); } catch { }
                    foreach (var d in Directory.GetDirectories(yol))
                        try { Directory.Delete(d, true); } catch { }
                }
            }
            catch (Exception ex) { Logger.Write(Log, "SoftwareDistribution\\Download bosaltma hatasi: " + ex.Message); }
            NativeRunner.Run("net.exe", "start bits", Log, false, 30000);
            NativeRunner.Run("net.exe", "start wuauserv", Log, false, 30000);
        }

        public static void BakimPolicyDuzelt()
        {
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\Task Scheduler", true))
                    if (k != null) { try { k.DeleteSubKeyTree("Maintenance", false); } catch { } }
            }
            catch { }

            NativeRunner.Run("sc.exe", "config Schedule start= auto", Log, false);
            NativeRunner.Run("sc.exe", "start Schedule", Log, false);

            string[] gorevler =
            {
                @"\Microsoft\Windows\TaskScheduler\Regular Maintenance",
                @"\Microsoft\Windows\TaskScheduler\Idle Maintenance",
                @"\Microsoft\Windows\TaskScheduler\Manual Maintenance",
                @"\Microsoft\Windows\DiskCleanup\SilentCleanup",
                @"\Microsoft\Windows\Defrag\ScheduledDefrag",
                @"\Microsoft\Windows\Maintenance\WinSAT"
            };
            foreach (var g in gorevler)
                NativeRunner.Run("schtasks.exe", "/change /tn \"" + g + "\" /ENABLE", Log, false);

            // WinSAT'i bir kez calistir (sistem puani/performans degerlendirmesi)
            NativeRunner.Run("schtasks.exe", "/run /tn \"\\Microsoft\\Windows\\Maintenance\\WinSAT\"", Log, false);
        }
    }
}
