using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security;
using System.Security.Principal;
using System.Text;

namespace UnsalBakim.Core
{
    /// <summary>
    /// Yonetici yetkisi yonetimi. Program asInvoker (standart) baslar.
    /// Yetki gerekiyorsa:
    ///   1) exe'nin yanindaki kurulum.ini'de yonetici hesabi varsa -> SESSIZCE yukseltir
    ///      (CreateProcessWithLogonW; UAC penceresi / sifre SORULMAZ).
    ///   2) ini yok/bos ise -> normal UAC (runas) ile yukseltir (eski davranis).
    /// Sonsuz donguyu onlemek icin cocuk surece "/ic" isareti eklenir.
    /// </summary>
    public static class Yetki
    {
        public const string IC = "/ic";

        public static bool Yukseltilmis()
        {
            try
            {
                using (var id = WindowsIdentity.GetCurrent())
                    return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }

        /// <summary>Kendini yonetici olarak yeniden baslatir. true = yeni surec baslatildi (bu surec cikmali).</summary>
        public static bool YukseltVeCalistir(string[] args, bool credDenendi)
        {
            string exe = Assembly.GetExecutingAssembly().Location;
            string argline = ArgBirlestir(args) + " " + IC;
            string dizin = Path.GetDirectoryName(exe);

            // 1) Sessiz yukseltme: kurulum.ini'den yonetici hesabi
            if (!credDenendi)
            {
                string kul, dom; SecureString pwd;
                // v109: kurulum.ini ile SESSIZ yukseltme SADECE guvenli konumlarda: USB bellek
                // (cikarilabilir surucu / USB veri yolundaki disk) veya kurulu kopya (Program Files).
                // Paket C:'ye (veya baska bir dahili diske) kopyalanmissa, o klasordeki exe'yi
                // calistiran HERKES paneli sifresiz yonetici olarak acabiliyordu -- artik orada
                // normal UAC (yonetici sifresi) istenir.
                bool konumGuvenli = IniKonumuGuvenli(exe);
                if (!konumGuvenli && File.Exists(Path.Combine(dizin, "kurulum.ini")))
                {
                    try { Logger.WriteFileOnly("kurulum.txt", "[Yetki] kurulum.ini guvenli olmayan bir konumda (" + dizin + ") -- sessiz yukseltme REDDEDILDI, normal UAC istenecek."); } catch { }
                }
                if (konumGuvenli && IniKimlikOku(exe, out dom, out kul, out pwd))
                {
                    try
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = exe,
                            Arguments = argline,
                            UseShellExecute = false,      // CreateProcessWithLogonW
                            WorkingDirectory = dizin,
                            UserName = kul,
                            Domain = string.IsNullOrEmpty(dom) ? "." : dom,
                            Password = pwd,
                            LoadUserProfile = true
                        };
                        Process.Start(psi);
                        return true;   // sessizce yukseltildi, sifre SORULMADI
                    }
                    catch { /* hesap/sifre hatali olabilir -> normal UAC'ye dus */ }
                }
            }

            // 2) Normal UAC (runas)
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = argline,
                    UseShellExecute = true,
                    WorkingDirectory = dizin,
                    Verb = "runas"
                };
                Process.Start(psi);
                return true;
            }
            catch { return false; }  // kullanici UAC'yi iptal etti vb.
        }

        /// <summary>v109: exe USB'den (cikarilabilir surucu veya USB veri yolundaki disk) ya da
        /// Program Files altindaki kurulu kopyadan mi calisiyor?</summary>
        public static bool IniKonumuGuvenli(string exe)
        {
            try
            {
                string tam = Path.GetFullPath(exe);
                foreach (var pf in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                                           Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) })
                {
                    if (!string.IsNullOrEmpty(pf) && tam.StartsWith(pf.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)) return true;
                }
                string kok = Path.GetPathRoot(tam);
                if (string.IsNullOrEmpty(kok) || kok.StartsWith("\\\\")) return false; // ag yolu: guvenli sayilmaz
                var di = new DriveInfo(kok);
                if (di.DriveType == DriveType.Removable) return true;
                if (di.DriveType != DriveType.Fixed) return false;
                // USB harici SSD/HDD'ler Windows'ta "Fixed" gorunur -- veri yolu tipine bak.
                string harf = kok.TrimEnd('\\');
                using (var s1 = new System.Management.ManagementObjectSearcher(
                    "ASSOCIATORS OF {Win32_LogicalDisk.DeviceID='" + harf + "'} WHERE AssocClass=Win32_LogicalDiskToPartition"))
                {
                    foreach (System.Management.ManagementObject part in s1.Get())
                    {
                        using (var s2 = new System.Management.ManagementObjectSearcher(
                            "ASSOCIATORS OF {Win32_DiskPartition.DeviceID='" + part["DeviceID"] + "'} WHERE AssocClass=Win32_DiskDriveToDiskPartition"))
                        {
                            foreach (System.Management.ManagementObject disk in s2.Get())
                            {
                                string arayuz = Convert.ToString(disk["InterfaceType"]) ?? "";
                                string pnp = Convert.ToString(disk["PNPDeviceID"]) ?? "";
                                if (arayuz.Equals("USB", StringComparison.OrdinalIgnoreCase) || pnp.StartsWith("USBSTOR", StringComparison.OrdinalIgnoreCase) || pnp.StartsWith("SCSI\\DISK&VEN_", StringComparison.OrdinalIgnoreCase) && pnp.IndexOf("USB", StringComparison.OrdinalIgnoreCase) >= 0)
                                    return true;
                            }
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        /// <summary>kurulum.ini'den hesap bilgisi. Alanlar: kullanici, parola veya parola_sifreli.</summary>
        private static bool IniKimlikOku(string exe, out string domain, out string kullanici, out SecureString parola)
        {
            domain = "."; kullanici = null; parola = null;
            try
            {
                string ini = Path.Combine(Path.GetDirectoryName(exe), "kurulum.ini");
                if (!File.Exists(ini)) return false;

                string kul = null, pOpen = null, pEnc = null;
                foreach (var ln in File.ReadAllLines(ini))
                {
                    var s = ln.Trim();
                    if (s.Length == 0 || s.StartsWith("#") || s.StartsWith(";")) continue;
                    int i = s.IndexOf('=');
                    if (i <= 0) continue;
                    string k = s.Substring(0, i).Trim().ToLowerInvariant();
                    string v = s.Substring(i + 1).Trim();
                    if (k == "kullanici" || k == "user") kul = v;
                    else if (k == "parola" || k == "password") pOpen = v;
                    else if (k == "parola_sifreli" || k == "password_enc") pEnc = v;
                }
                if (string.IsNullOrEmpty(kul)) return false;

                // domain\kullanici ayristir
                int bs = kul.IndexOf('\\');
                if (bs >= 0) { domain = kul.Substring(0, bs); kullanici = kul.Substring(bs + 1); }
                else { domain = "."; kullanici = kul; }

                string acik = !string.IsNullOrEmpty(pEnc) ? SifreCoz(pEnc) : pOpen;
                if (acik == null) acik = "";
                var ss = new SecureString();
                foreach (char c in acik) ss.AppendChar(c);
                ss.MakeReadOnly();
                parola = ss;
                return true;
            }
            catch { return false; }
        }

        private static string ArgBirlestir(string[] args)
        {
            if (args == null || args.Length == 0) return "";
            var sb = new StringBuilder();
            foreach (var a in args)
            {
                if (a == IC) continue;
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(a.IndexOf(' ') >= 0 ? "\"" + a + "\"" : a);
            }
            return sb.ToString();
        }

        // --- Basit gizleme (duz metin sifreyi meraklı ogrenciye karsi gizler; tam guvenlik degildir) ---
        private const string Anahtar = "UnsalBakim-2026";

        public static string SifreGizle(string acik)
        {
            var b = Encoding.UTF8.GetBytes(acik ?? "");
            for (int i = 0; i < b.Length; i++) b[i] ^= (byte)Anahtar[i % Anahtar.Length];
            return Convert.ToBase64String(b);
        }

        /// <summary>exe'nin yanina kurulum.ini yazar (sifresiz/UAC'siz kurulum icin).
        /// Parola gizli yazilir. Yazilan yolu dondurur.
        /// v82 (Ali Bey'in istegi: "ogrenci\UnsalOgrenci.exe kullanmiyorum, kaldiralim"):
        /// ayri, menusuz "ogrenci" exe'si dagitimi KALDIRILDIGI icin bu fonksiyon artik SADECE
        /// exe'nin kendi klasorune yaziyor -- eskiden ayni paketteki "ogrenci" klasorune de
        /// (varsa) ikinci bir kopya yaziyordu, o dagitim hedefi olmadigi icin gereksizdi.</summary>
        public static string KurulumIniYaz(string kullanici, string parolaAcik)
        {
            var satir = new[]
            {
                "# UNSAL BAKIM - sifresiz kurulum icin (panelden olusturuldu)",
                "# Bu dosya exe ile ayni klasorde durur; silerseniz tekrar UAC/parola sorulur.",
                "kullanici=" + (kullanici ?? "").Trim(),
                "parola_sifreli=" + SifreGizle(parolaAcik ?? "")
            };

            string exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string ana = Path.Combine(exeDir, "kurulum.ini");
            File.WriteAllLines(ana, satir);
            return ana;
        }

        public static string SifreCoz(string gizli)
        {
            try
            {
                var b = Convert.FromBase64String(gizli);
                for (int i = 0; i < b.Length; i++) b[i] ^= (byte)Anahtar[i % Anahtar.Length];
                return Encoding.UTF8.GetString(b);
            }
            catch { return null; }
        }
    }
}
