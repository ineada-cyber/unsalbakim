using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Text;
using Microsoft.Win32;

namespace UnsalBakim.Core
{
    /// <summary>
    /// v109 (Ali Bey'in istekleri, 12 Kasim 2026): "disk kritikse otomatik ne yapabiliriz",
    /// "kurulum artiklarini silebilir miyiz", "agir bakim gunde 1 bostayken", "geri yukleme
    /// noktasi 1 tane yeter, eskileri silelim".
    ///
    /// Ilkeler:
    ///  - KISISEL DOSYALARA ve COP KUTUSU'NA ASLA dokunulmaz (Cop Kutusu ogrencinin yanlislikla
    ///    sildigi dosyanin tek kurtarma yolu -- v84/v85 karari).
    ///  - Genel "registry temizleyici" YOK (v83 karari) -- sadece dosyasi artik olmayan baslangic
    ///    girdileri gibi DAR ve kanitli temizlik.
    ///  - Agir onarim (RestoreHealth/SFC/CHKDSK) sadece BULGU varsa calisir.
    ///  - Geri yukleme noktasi: once YENISI alinir, basariliysa eskiler silinir (tek nokta kalir).
    /// </summary>
    public static class AkilliBakim
    {
        private const string Log = "akilli_bakim.txt";

        private static string Kok =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "UnsalBakim");
        private static string OnarimIsareti => Path.Combine(Kok, "onarim_gerekli.txt");
        private static string DiskAcilDamga => Path.Combine(Kok, "disk_acil_son.txt");

        private static string SistemSurucu =>
            (Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)) ?? "C:\\").TrimEnd('\\');

        // =====================================================================================
        // DISK DURUMU
        // =====================================================================================
        public static bool DiskKritikMi(out double bosGb, out double toplamGb)
        {
            bosGb = 0; toplamGb = 0;
            try
            {
                var d = new DriveInfo(SistemSurucu + "\\");
                bosGb = d.AvailableFreeSpace / 1073741824.0;
                toplamGb = d.TotalSize / 1073741824.0;
                if (toplamGb <= 0) return false;
                return bosGb < 15.0 || (bosGb / toplamGb) < 0.10;
            }
            catch { return false; }
        }

        public static string DiskOzet()
        {
            double b, t;
            bool k = DiskKritikMi(out b, out t);
            return SistemSurucu + " bos " + b.ToString("0.0") + " GB / " + t.ToString("0") + " GB (%" + (t > 0 ? (100 * b / t).ToString("0") : "?") + ")" + (k ? " -- KRITIK" : "");
        }

        /// <summary>5 dk'lik dongude cagrilir (ucuz). Disk kritikse ve BUGUN henuz acil temizlik
        /// yapilmadiysa, temizligi AYRI bir arka plan surecinde ("/disk-acil") baslatir -- uzun
        /// surebilecegi icin 5 dk'lik denetim dongusunu BLOKLAMAZ.</summary>
        public static void DiskKritikDonguKontrol()
        {
            double b, t;
            if (!DiskKritikMi(out b, out t)) return;
            string bugun = DateTime.Now.ToString("yyyy-MM-dd");
            try { if (File.Exists(DiskAcilDamga) && File.ReadAllText(DiskAcilDamga).Trim() == bugun) return; } catch { }
            try { Directory.CreateDirectory(Kok); File.WriteAllText(DiskAcilDamga, bugun); } catch { }
            try
            {
                if (TaskHelper.DiskAcilGoreviCalistir())
                    Logger.WriteFileOnly(Log, "Disk kritik (" + DiskOzet() + ") -- acil temizlik ayri SYSTEM gorevi olarak baslatildi.");
                else
                {
                    // Gorev kurulamadiysa yedek yol: dogrudan ayri surec.
                    string exe = Assembly.GetExecutingAssembly().Location;
                    Process.Start(new ProcessStartInfo(exe, "/disk-acil") { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(exe) });
                    Logger.WriteFileOnly(Log, "Disk kritik (" + DiskOzet() + ") -- acil temizlik ayri surecte baslatildi.");
                }
            }
            catch (Exception ex) { Logger.WriteFileOnly(Log, "[!] Acil temizlik baslatilamadi: " + ex.Message); }
        }

        /// <summary>"/disk-acil" ile (SYSTEM) calisir. Kademeli: once risksiz temizlik, yetmezse
        /// bilesen deposu/hazirda bekletme/geri yukleme alani, hala yetmezse sadece RAPOR.</summary>
        public static void DiskAcilTemizlik()
        {
            double once, sonra, t;
            DiskKritikMi(out once, out t);
            Logger.Write(Log, "=== DISK ACIL TEMIZLIK BASLADI (" + DiskOzet() + ") ===");

            // Kademe 1 -- risksiz
            try { Maintenance.TempTemizleHerkes(1); } catch (Exception ex) { Logger.Write(Log, "[!] Temp: " + ex.Message); }
            try { PerformansAyar.GuncellemeOnbellekTemizle(); } catch (Exception ex) { Logger.Write(Log, "[!] WU onbellek: " + ex.Message); }
            try { int k; NativeRunner.PowerShell("Delete-DeliveryOptimizationCache -Force -ErrorAction SilentlyContinue", 120000, out k); } catch { }
            try { CokmeDokumleriTemizle(); } catch (Exception ex) { Logger.Write(Log, "[!] Dokum: " + ex.Message); }
            try { Installer.EskiArtiklariTemizle(); } catch { }
            try { BosProgramKlasorleriniTemizle(); } catch { }
            NativeRunner.Run("schtasks.exe", "/run /tn \"\\Microsoft\\Windows\\DiskCleanup\\SilentCleanup\"", Log, false);

            bool hala = DiskKritikMi(out sonra, out t);
            Logger.Write(Log, "Kademe 1 sonrasi: " + DiskOzet());

            // Kademe 2 -- guvenli ama daha buyuk
            if (hala)
            {
                try
                {
                    string hib = Path.Combine(SistemSurucu + "\\", "hiberfil.sys");
                    if (File.Exists(hib)) { NativeRunner.Run("powercfg.exe", "/h off", Log, false); Logger.Write(Log, "Hazirda bekletme kapatildi (hiberfil.sys silindi; hizli baslatma da kapanir)."); }
                }
                catch { }
                try { GeriYuklemeAlaniSinirla(); GeriYuklemeNoktalariniTekeIndir(); } catch { }
                NativeRunner.Run("DISM.exe", "/Online /Cleanup-Image /StartComponentCleanup", Log, false, 1800000);
                hala = DiskKritikMi(out sonra, out t);
                Logger.Write(Log, "Kademe 2 sonrasi: " + DiskOzet());
            }

            // Kademe 3 -- SILMEZ, sadece raporlar
            string rapor = "";
            if (hala)
            {
                rapor = BuyukKlasorRaporu();
                Logger.Write(Log, "Yer hala kritik. En buyuk klasorler (SILINMEDI, sadece rapor):\r\n" + rapor);
            }

            string detay = "Once: " + once.ToString("0.0") + " GB bos, sonra: " + sonra.ToString("0.0") + " GB bos (" + t.ToString("0") + " GB disk)" +
                           (hala ? " -- HALA KRITIK. En buyuk: " + rapor.Replace("\r\n", "; ") : " -- yer acildi.");
            try { DriveLog.Gonder("disk_kritik", detay); } catch { }
            Logger.Write(Log, "=== DISK ACIL TEMIZLIK BITTI: " + detay + " ===");
        }

        /// <summary>Kisisel klasorler ve buyuk sistem klasorleri (salt okunur tarama, zaman sinirli).</summary>
        public static string BuyukKlasorRaporu()
        {
            var sonuc = new List<KeyValuePair<string, long>>();
            var sw = Stopwatch.StartNew();
            var adaylar = new List<string>();
            try
            {
                string users = Path.Combine(SistemSurucu + "\\", "Users");
                foreach (var prof in Directory.GetDirectories(users))
                    foreach (var alt in new[] { "Downloads", "Desktop", "Documents", "Videos", "Pictures", "Music", @"AppData\Local", @"AppData\Roaming" })
                        adaylar.Add(Path.Combine(prof, alt));
            }
            catch { }
            adaylar.Add(Path.Combine(SistemSurucu + "\\", "Windows.old"));
            adaylar.Add(Path.Combine(SistemSurucu + "\\", "$WINDOWS.~BT"));
            adaylar.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Installer"));
            adaylar.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SoftwareDistribution"));
            foreach (var a in adaylar)
            {
                if (sw.Elapsed.TotalMinutes > 4) break;
                if (!Directory.Exists(a)) continue;
                long b = KlasorBoyutu(a, sw);
                if (b > 500L * 1048576) sonuc.Add(new KeyValuePair<string, long>(a, b));
            }
            return string.Join("\r\n", sonuc.OrderByDescending(x => x.Value).Take(10)
                .Select(x => (x.Value / 1073741824.0).ToString("0.0") + " GB  " + x.Key));
        }

        private static long KlasorBoyutu(string kok, Stopwatch sw)
        {
            long toplam = 0;
            var yigin = new Stack<string>(); yigin.Push(kok);
            while (yigin.Count > 0 && sw.Elapsed.TotalMinutes < 4)
            {
                string d = yigin.Pop();
                try
                {
                    var di = new DirectoryInfo(d);
                    if ((di.Attributes & FileAttributes.ReparsePoint) != 0 && d != kok) continue;
                    foreach (var f in di.EnumerateFiles()) { try { toplam += f.Length; } catch { } }
                    foreach (var s in di.EnumerateDirectories()) yigin.Push(s.FullName);
                }
                catch { }
            }
            return toplam;
        }

        // =====================================================================================
        // DOKUM / ARTIK TEMIZLIGI (dar, kanitli)
        // =====================================================================================
        public static void CokmeDokumleriTemizle()
        {
            string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            long bayt = 0;
            bayt += DosyaSilEskiyse(Path.Combine(win, "MEMORY.DMP"), 0);
            bayt += KlasorIcerikSil(Path.Combine(win, "Minidump"), 14);
            bayt += KlasorIcerikSil(Path.Combine(win, "LiveKernelReports"), 14);
            string wer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Microsoft", "Windows", "WER");
            bayt += KlasorIcerikSil(Path.Combine(wer, "ReportArchive"), 7);
            bayt += KlasorIcerikSil(Path.Combine(wer, "ReportQueue"), 7);
            bayt += KlasorIcerikSil(Path.Combine(wer, "Temp"), 1);
            // CBS / DISM loglari (Windows kendi yenisini yazar)
            bayt += KlasorIcerikSil(Path.Combine(win, "Logs", "CBS"), 30, "*.cab");
            if (bayt > 0) Logger.WriteFileOnly(Log, "Cokme dokumu / hata raporu temizligi: ~" + (bayt / 1048576) + " MB");
        }

        private static long DosyaSilEskiyse(string f, int gun)
        {
            try
            {
                if (!File.Exists(f)) return 0;
                if (gun > 0 && (DateTime.Now - File.GetLastWriteTime(f)).TotalDays < gun) return 0;
                long b = new FileInfo(f).Length;
                File.SetAttributes(f, FileAttributes.Normal); File.Delete(f);
                return b;
            }
            catch { return 0; }
        }

        private static long KlasorIcerikSil(string d, int gun, string desen = "*")
        {
            long b = 0;
            try
            {
                if (!Directory.Exists(d)) return 0;
                foreach (var f in Directory.GetFiles(d, desen, SearchOption.AllDirectories)) b += DosyaSilEskiyse(f, gun);
                foreach (var s in Directory.GetDirectories(d, "*", SearchOption.AllDirectories).OrderByDescending(x => x.Length))
                    try { if (!Directory.EnumerateFileSystemEntries(s).Any()) Directory.Delete(s); } catch { }
            }
            catch { }
            return b;
        }

        /// <summary>Program Files / ProgramData altinda TAMAMEN BOS kalmis (kaldirilmis programlardan
        /// kalan) klasorleri siler. Icinde tek bir dosya bile olan klasore dokunmaz; Windows/Microsoft
        /// klasorlerine hic girmez.</summary>
        public static void BosProgramKlasorleriniTemizle()
        {
            var kokler = new[] {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) };
            int n = 0;
            foreach (var kok in kokler.Where(k => !string.IsNullOrEmpty(k) && Directory.Exists(k)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                foreach (var d in SafeDirs(kok))
                {
                    string ad = Path.GetFileName(d);
                    if (ad.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase) || ad.StartsWith("Windows", StringComparison.OrdinalIgnoreCase)
                        || ad.StartsWith("UnsalBakim", StringComparison.OrdinalIgnoreCase) || ad.Equals("Package Cache", StringComparison.OrdinalIgnoreCase)
                        || ad.StartsWith("Common Files", StringComparison.OrdinalIgnoreCase) || ad.Equals("Packages", StringComparison.OrdinalIgnoreCase))
                        continue;
                    try
                    {
                        var di = new DirectoryInfo(d);
                        if ((di.Attributes & (FileAttributes.ReparsePoint | FileAttributes.System)) != 0) continue;
                        if (!Directory.EnumerateFileSystemEntries(d, "*", SearchOption.AllDirectories).Any(x => File.Exists(x)))
                        {
                            if ((DateTime.Now - di.LastWriteTime).TotalDays < 7) continue;
                            Directory.Delete(d, true); n++;
                            Logger.WriteFileOnly(Log, "Bos program klasoru silindi: " + d);
                        }
                    }
                    catch { }
                }
            }
            if (n > 0) Logger.WriteFileOnly(Log, "Bos program klasoru temizligi: " + n + " klasor.");
        }

        private static IEnumerable<string> SafeDirs(string kok)
        {
            try { return Directory.GetDirectories(kok); } catch { return new string[0]; }
        }

        /// <summary>Baslangicta (Run anahtarlari) calismak uzere kayitli ama DOSYASI ARTIK OLMAYAN
        /// girdileri siler. Sadece yolu KESIN cozulebilen (.exe ile biten, tam yol) girdilere dokunur;
        /// rundll32/msiexec gibi komutlar ve belirsiz yollar ATLANIR.</summary>
        public static void YetimBaslangicGirdileriniTemizle()
        {
            int n = 0;
            var anahtarlar = new List<RegistryKey>();
            try { anahtarlar.Add(Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true)); } catch { }
            try { anahtarlar.Add(Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", true)); } catch { }
            try
            {
                foreach (var sid in Registry.Users.GetSubKeyNames())
                {
                    if (!sid.StartsWith("S-1-5-21-") || sid.EndsWith("_Classes")) continue;
                    try { anahtarlar.Add(Registry.Users.OpenSubKey(sid + @"\Software\Microsoft\Windows\CurrentVersion\Run", true)); } catch { }
                }
            }
            catch { }
            foreach (var k in anahtarlar.Where(x => x != null))
            {
                try
                {
                    foreach (var ad in k.GetValueNames())
                    {
                        string komut = Convert.ToString(k.GetValue(ad, "", RegistryValueOptions.DoNotExpandEnvironmentNames)) ?? "";
                        string yol = ExeYolunuCoz(komut);
                        if (yol == null) continue;
                        if (File.Exists(yol)) continue;
                        try { k.DeleteValue(ad, false); n++; Logger.WriteFileOnly(Log, "Yetim baslangic girdisi silindi: " + k.Name + " -> " + ad + " = " + komut); } catch { }
                    }
                }
                catch { }
                finally { try { k.Close(); } catch { } }
            }
            if (n > 0) Logger.WriteFileOnly(Log, "Yetim baslangic girdisi temizligi: " + n + " girdi.");
        }

        private static string ExeYolunuCoz(string komut)
        {
            try
            {
                string s = Environment.ExpandEnvironmentVariables((komut ?? "").Trim());
                if (s.Length == 0) return null;
                string yol;
                if (s.StartsWith("\""))
                {
                    int e = s.IndexOf('"', 1);
                    if (e <= 1) return null;
                    yol = s.Substring(1, e - 1);
                }
                else
                {
                    int i = s.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
                    if (i < 0) return null;
                    yol = s.Substring(0, i + 4);
                    // tirnaksiz ve bosluklu yol -- "C:\Program Files\x\y.exe /arg" belirsiz olabilir; sadece
                    // tam olarak bu yol ile bitiyor ya da ardindan bosluk geliyorsa kabul et.
                    if (s.Length > i + 4 && s[i + 4] != ' ') return null;
                }
                if (!yol.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return null;
                if (!Path.IsPathRooted(yol) || yol.StartsWith("\\\\")) return null;
                string ad = Path.GetFileName(yol).ToLowerInvariant();
                if (ad == "rundll32.exe" || ad == "msiexec.exe" || ad == "cmd.exe" || ad == "powershell.exe" || ad == "wscript.exe") return null;
                // Surucu yoksa (cikarilmis USB vb.) karar verme
                string kok = Path.GetPathRoot(yol);
                if (string.IsNullOrEmpty(kok) || !Directory.Exists(kok)) return null;
                return yol;
            }
            catch { return null; }
        }

        // =====================================================================================
        // SAGLIK KONTROLU (hizli) -> agir onarim sadece bulgu varsa
        // =====================================================================================
        /// <summary>Gunluk (hafif bakimda) -- birkac saniye. Windows bileseni deposu ve disk "kirli"
        /// isareti kontrol edilir; sorun varsa isaret dosyasi birakilir, agir bakim onarir.</summary>
        public static void HizliSaglikKontrolu(string log)
        {
            string durum = BilesenDeposuDurumu();
            bool kirli = DiskKirliMi();
            Logger.Write(log, "Saglik: Windows bileseni deposu = " + durum + ", disk kirli isareti = " + (kirli ? "VAR" : "yok"));
            if ((!durum.Equals("Healthy", StringComparison.OrdinalIgnoreCase) && durum != "?") || kirli)
            {
                try { Directory.CreateDirectory(Kok); File.WriteAllText(OnarimIsareti, DateTime.Now.ToString("yyyy-MM-dd HH:mm") + " depo=" + durum + " kirli=" + kirli); } catch { }
                Logger.Write(log, "[UYARI] Onarim gerekli -- bir sonraki agir bakimda RestoreHealth/SFC" + (kirli ? "/CHKDSK" : "") + " calisacak.");
                try { DriveLog.Gonder("saglik_uyari", "Bilesen deposu: " + durum + ", disk kirli: " + (kirli ? "evet" : "hayir")); } catch { }
            }
            try { SmartOzet(log); } catch { }
        }

        public static bool OnarimIsaretiVar() => File.Exists(OnarimIsareti);
        public static void OnarimIsaretiniSil() { try { if (File.Exists(OnarimIsareti)) File.Delete(OnarimIsareti); } catch { } }

        /// <summary>Repair-WindowsImage -CheckHealth: "Healthy" / "Repairable" / "NonRepairable"
        /// (dile bagli degil). Okunamazsa "?".</summary>
        public static string BilesenDeposuDurumu()
        {
            int kod;
            string c = NativeRunner.PowerShell("try { (Repair-WindowsImage -Online -CheckHealth -ErrorAction Stop).ImageHealthState } catch { '?' }", 180000, out kod);
            foreach (var s in c.Replace("\r", "").Split('\n'))
            {
                var t = s.Trim();
                if (t == "Healthy" || t == "Repairable" || t == "NonRepairable") return t;
            }
            return "?";
        }

        public static bool DiskKirliMi()
        {
            try
            {
                using (var s = new ManagementObjectSearcher("SELECT DirtyBitSet FROM Win32_Volume WHERE DriveLetter='" + SistemSurucu + "'"))
                    foreach (ManagementObject v in s.Get())
                        if (v["DirtyBitSet"] is bool b && b) return true;
            }
            catch { }
            return false;
        }

        private static void SmartOzet(string log)
        {
            int kod;
            string c = NativeRunner.PowerShell("Get-PhysicalDisk -ErrorAction SilentlyContinue | ForEach-Object { $_.FriendlyName + ' = ' + $_.HealthStatus }", 60000, out kod);
            foreach (var s in c.Replace("\r", "").Split('\n'))
            {
                var t = s.Trim();
                // Sadece "Disk adi = Durum" bicimindeki satirlar -- PowerShell'in diger ciktilari
                // (ilerleme/CLIXML vb.) ASLA disk uyarisi sayilmaz.
                if (t.Length == 0 || t.IndexOf(" = ", StringComparison.Ordinal) <= 0 || t.StartsWith("<") || t.StartsWith("#")) continue;
                bool kotu = !t.EndsWith("Healthy", StringComparison.OrdinalIgnoreCase);
                Logger.Write(log, (kotu ? "[UYARI] " : "") + "Disk sagligi: " + t);
                if (kotu) try { DriveLog.Gonder("disk_saglik_uyari", t); } catch { }
            }
        }

        // =====================================================================================
        // GERI YUKLEME NOKTASI -- tek nokta
        // =====================================================================================
        public static int GeriYuklemeNoktasiSayisi()
        {
            int n = 0;
            try
            {
                string vol = null;
                using (var s = new ManagementObjectSearcher("SELECT DeviceID FROM Win32_Volume WHERE DriveLetter='" + SistemSurucu + "'"))
                    foreach (ManagementObject v in s.Get()) vol = Convert.ToString(v["DeviceID"]);
                using (var s = new ManagementObjectSearcher("SELECT VolumeName FROM Win32_ShadowCopy"))
                    foreach (ManagementObject sc in s.Get())
                        if (vol == null || string.Equals(Convert.ToString(sc["VolumeName"]), vol, StringComparison.OrdinalIgnoreCase)) n++;
            }
            catch { return -1; }
            return n;
        }

        public static void GeriYuklemeAlaniSinirla()
        {
            NativeRunner.Run("vssadmin.exe", "resize shadowstorage /for=" + SistemSurucu + " /on=" + SistemSurucu + " /maxsize=5%", Log, false);
        }

        /// <summary>En yenisi kalacak sekilde en eski noktalari siler (1'e indirir).</summary>
        public static void GeriYuklemeNoktalariniTekeIndir()
        {
            int n = GeriYuklemeNoktasiSayisi();
            int silinen = 0;
            for (int i = 0; i < 20 && n > 1; i++)
            {
                NativeRunner.Run("vssadmin.exe", "delete shadows /for=" + SistemSurucu + " /oldest /quiet", Log, false);
                int yeni = GeriYuklemeNoktasiSayisi();
                if (yeni >= n || yeni < 0) break;
                silinen += n - yeni; n = yeni;
            }
            if (silinen > 0) Logger.WriteFileOnly(Log, "Eski geri yukleme noktasi silindi: " + silinen + " (kalan: " + n + ").");
        }

        /// <summary>Agir bakimda: once YENI nokta alinir; basariliysa eskiler silinip tek nokta birakilir.
        /// Yeni nokta alinamazsa eskilere DOKUNULMAZ.</summary>
        public static void GeriYuklemeNoktasiAl(string log)
        {
            try { PerformansAyar.VeriKoruma(); } catch { }
            GeriYuklemeAlaniSinirla();
            // Windows varsayilan olarak 24 saatte en fazla 1 nokta olusturur ve fazlasini SESSIZCE
            // atlar -- bakim noktasinin gercekten olusmasi icin bu siklik siniri kaldirilir.
            try
            {
                using (var k = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore"))
                    k?.SetValue("SystemRestorePointCreationFrequency", 0, RegistryValueKind.DWord);
            }
            catch { }
            int once = GeriYuklemeNoktasiSayisi();
            NativeRunner.Run("powershell.exe",
                "-NoProfile -ExecutionPolicy Bypass -Command \"Checkpoint-Computer -Description 'Unsal Bakim Oncesi' -RestorePointType MODIFY_SETTINGS\"",
                log, false, 300000);
            int sonra = GeriYuklemeNoktasiSayisi();
            if (sonra > once && sonra >= 1)
            {
                Logger.Write(log, "Yeni geri yukleme noktasi alindi (" + once + " -> " + sonra + "); eskiler siliniyor, tek nokta kalacak.");
                GeriYuklemeNoktalariniTekeIndir();
            }
            else Logger.Write(log, "[UYARI] Yeni geri yukleme noktasi olusturulamadi (sayi " + once + " -> " + sonra + ") -- eski noktalara DOKUNULMADI.");
        }

        // =====================================================================================
        // AG SIFIRLAMA -- artik SADECE panelden, elle
        // =====================================================================================
        public static void AgAyarlariniSifirla()
        {
            const string log = "agir.txt";
            Logger.Write(log, "Ag ayarlari sifirlaniyor (elle, panelden): winsock + TCP/IP...");
            NativeRunner.Run("netsh.exe", "winsock reset", log, true);
            NativeRunner.Run("netsh.exe", "int ip reset", log, true);
            NativeRunner.Run("ipconfig.exe", "/flushdns", log, false);
            Logger.Write(log, "Ag sifirlama tamamlandi. Etkili olmasi icin bilgisayar YENIDEN BASLATILMALI. (Elle girilmis sabit IP/DNS varsa yeniden girilmesi gerekebilir.)");
        }
    }
}
