using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace UnsalBakim.Core
{
    /// <summary>
    /// v109 (Ali Bey'in istegi: "loglari kontrol et dedigimde Drive Sheet'te ayri bir log
    /// sekmesinde bilgisayar adi, uzun hucreye logunu yazsan, oradan kontrol etsen, makinelerden
    /// USB'lerle toplamasak"): her makine, son 24 saatin HATA/UYARI ozetini Google Sheet'teki
    /// "MakineLog" sekmesine yazar -- makine basina TEK satir (her gonderimde AYNI satirin uzerine
    /// yazilir, sayfa buyumez).
    ///
    /// Icerik: surum/tur/disk/geri yukleme noktasi/AppLocker modu basligi + son 24 saatin AppLocker
    /// olaylari (en fazla ~4.000 karakter) + yerel LOG klasorundeki hata/uyari satirlari (tekrarlar
    /// birlestirilir). Toplam en fazla 10.000 karakter. Parola/token/anahtar benzeri degerler
    /// gondermeden ONCE maskelenir.
    ///
    /// Gonderim: gunluk (hafif bakim sonunda), agir bakim sonunda, her guncellemeden sonra ve
    /// Filo'dan "Envanter iste" komutu geldiginde. Ayni anda sunucuya yuklenmemek icin
    /// guncelleme/komut kaynakli gonderimler makine kimligine gore 0-90 sn kaydirilir.
    /// </summary>
    public static class MakineLog
    {
        private const int AzamiKarakter = 10000;
        private const int AppLockerAzami = 4000;

        private static readonly string[] Anahtarlar =
        {
            "hata", "[!]", "basarisiz", "basarisız", "uyari", "[uyari]", "error", "exception", "kabul edilmedi",
            "gonderilemedi", "gönderilemedi", "olusturulamadi", "uygulanamadi", "reddedildi", "disk_kritik",
            "kritik", "zaman asimi", "timeout", "erisim engellendi", "access is denied", "[kurtarma]"
        };

        private static readonly Regex Maske = new Regex(
            @"(?i)(parola|password|sifre|şifre|token|secret|apikey|api_key|bearer|parola_sifreli)(\s*[:=]\s*|\s+)([^\s,;|""']+)",
            RegexOptions.Compiled);
        private static readonly Regex AnthropicAnahtar = new Regex(@"sk-ant-[A-Za-z0-9_\-]+", RegexOptions.Compiled);

        public static string Maskele(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            s = AnthropicAnahtar.Replace(s, "sk-ant-***");
            return Maske.Replace(s, m => m.Groups[1].Value + m.Groups[2].Value + "***");
        }

        /// <summary>Ozet metni + hata sayisi.</summary>
        public static string Topla(AppSettings ayar, out int hataSayisi)
        {
            hataSayisi = 0;
            var sb = new StringBuilder();
            sb.AppendLine("UNSAL BAKIM " + License.Surum + " | " + (ayar?.MakineTuru ?? "?") + " | " + DateTime.Now.ToString("dd.MM.yyyy HH:mm"));
            try { sb.AppendLine("Disk: " + AkilliBakim.DiskOzet()); } catch { }
            try { sb.AppendLine("Geri yukleme noktasi: " + AkilliBakim.GeriYuklemeNoktasiSayisi()); } catch { }
            bool ogretmen = RolGuvenlik.OgretmenMakinesi(ayar);
            sb.AppendLine("AppLocker: " + (ogretmen ? "uygulanmiyor (ogretmen makinesi)" : AppLockerYonet.ModMetni(ayar)));
            try { if (File.Exists(Installer.DevamIsaretiYolu)) sb.AppendLine("[UYARI] Kurulum yarida kalmis (kurtarma bekliyor)."); } catch { }
            try { if (KorumaAcil.Aktif) sb.AppendLine("[UYARI] KORUMA ACIL KAPALI (Filo'dan): AppLocker ve klasor kilitleri devre disi."); } catch { }
            try { sb.AppendLine("Tarayici guvenligi (DoH kapali + SafeSearch): " + TarayiciGuvenlik.Durum()); } catch { }
            // v110: Wi-Fi baglanti gecmisi (son 7 gun) -- FATIH disi aglar [!] ile isaretlenir.
            try
            {
                string wifi = Teshis.WifiBaglantiOzeti(7, 40);
                if (!string.IsNullOrWhiteSpace(wifi))
                {
                    if (wifi.Length > 1500) wifi = wifi.Substring(0, 1500) + "\n...(kesildi)";
                    sb.AppendLine("--- Wi-Fi (son 7 gun) ---");
                    sb.AppendLine(wifi);
                }
            }
            catch { }

            if (!ogretmen)
            {
                try
                {
                    string al = AppLockerYonet.SonOlaylar(24, 60);
                    if (!string.IsNullOrWhiteSpace(al))
                    {
                        if (al.Length > AppLockerAzami) al = al.Substring(0, AppLockerAzami) + "\n...(kesildi)";
                        sb.AppendLine("--- AppLocker (son 24 saat; DENETIM=engellenecekti, ENGEL=engellendi) ---");
                        sb.Append(al);
                    }
                }
                catch { }
            }

            var satirlar = HataSatirlari(24, out hataSayisi);
            sb.AppendLine("--- Hata/uyari (son 24 saat, " + hataSayisi + " satir) ---");
            if (satirlar.Count == 0) sb.AppendLine("(yok)");
            foreach (var s in satirlar)
            {
                if (sb.Length + s.Length + 2 > AzamiKarakter - 20) { sb.AppendLine("...(kesildi)"); break; }
                sb.AppendLine(s);
            }
            string sonuc = Maskele(sb.ToString());
            return sonuc.Length > AzamiKarakter ? sonuc.Substring(0, AzamiKarakter) : sonuc;
        }

        private static List<string> HataSatirlari(int saat, out int toplam)
        {
            toplam = 0;
            var sira = new List<string>();
            var sayac = new Dictionary<string, int>();
            var ilkZaman = new Dictionary<string, string>();
            DateTime sinir = DateTime.Now.AddHours(-saat);
            string dizin = Logger.LogDir;
            if (!Directory.Exists(dizin)) return sira;
            foreach (var f in Directory.GetFiles(dizin, "*.txt").OrderBy(x => Path.GetFileName(x)))
            {
                try
                {
                    if (File.GetLastWriteTime(f) < sinir) continue;
                    string dosyaAdi = Path.GetFileNameWithoutExtension(f);
                    foreach (var ham in SonKisim(f, 400 * 1024))
                    {
                        if (ham.Length < 22 || ham[0] != '[') continue;
                        DateTime t;
                        if (!DateTime.TryParseExact(ham.Substring(1, 19), "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out t)) continue;
                        if (t < sinir) continue;
                        string govde = ham.Substring(21).Trim();
                        string mn = "[" + Environment.MachineName + "]";
                        if (govde.StartsWith(mn)) govde = govde.Substring(mn.Length).Trim();
                        string kucuk = govde.ToLowerInvariant();
                        if (!Anahtarlar.Any(a => kucuk.Contains(a))) continue;
                        toplam++;
                        if (govde.Length > 300) govde = govde.Substring(0, 300) + "...";
                        string anahtar = dosyaAdi + ": " + govde;
                        if (sayac.ContainsKey(anahtar)) sayac[anahtar]++;
                        else { sayac[anahtar] = 1; ilkZaman[anahtar] = t.ToString("HH:mm"); sira.Add(anahtar); }
                    }
                }
                catch { }
            }
            return sira.Select(k => ilkZaman[k] + " " + k + (sayac[k] > 1 ? "  (x" + sayac[k] + ")" : "")).ToList();
        }

        private static IEnumerable<string> SonKisim(string dosya, int bayt)
        {
            string metin;
            using (var fs = new FileStream(dosya, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                long bas = Math.Max(0, fs.Length - bayt);
                fs.Seek(bas, SeekOrigin.Begin);
                using (var sr = new StreamReader(fs, Encoding.UTF8))
                {
                    metin = sr.ReadToEnd();
                }
                if (bas > 0) { int i = metin.IndexOf('\n'); if (i >= 0) metin = metin.Substring(i + 1); }
            }
            return metin.Replace("\r", "").Split('\n');
        }

        /// <summary>Sheet'e gonderir. gecikmeli=true ise makine kimligine gore 0-90 sn bekler (ayni
        /// anda tum filodan istek yagmasin -- Apps Script es zamanli calistirma siniri).</summary>
        public static string Gonder(AppSettings ayar, bool gecikmeli)
        {
            if (string.IsNullOrWhiteSpace(Musteri.WebAppUrl) || ayar == null) return "WebAppUrl yok";
            try
            {
                if (gecikmeli)
                {
                    int h = 0; foreach (char c in (ayar.MachineId ?? Environment.MachineName)) h = (h * 31 + c) & 0x7fffffff;
                    System.Threading.Thread.Sleep((h % 90) * 1000);
                }
                int hata;
                string ozet = Topla(ayar, out hata);
                double bos, top;
                AkilliBakim.DiskKritikMi(out bos, out top);
                string json = "{\"tip\":\"makinelog\"," +
                    "\"token\":\"" + J(Musteri.ApiToken) + "\"," +
                    "\"musteriId\":\"" + J(Musteri.MusteriId) + "\"," +
                    "\"machineId\":\"" + J(ayar.MachineId) + "\"," +
                    "\"eskiMachineId\":\"" + J(ayar.EskiMachineId) + "\"," + // v110
                    "\"bilgisayar\":\"" + J(Environment.MachineName) + "\"," +
                    "\"surum\":\"" + J(License.Surum) + "\"," +
                    "\"tur\":\"" + J(ayar.MakineTuru) + "\"," +
                    "\"diskBos\":\"" + J(bos.ToString("0.0", CultureInfo.InvariantCulture)) + "\"," +
                    "\"hataSayisi\":\"" + hata + "\"," +
                    "\"ozet\":\"" + J(ozet) + "\"}";
                try { ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; } catch { }
                using (var wc = new ZamanAsimliWebClient())
                {
                    wc.Encoding = Encoding.UTF8;
                    wc.Headers["Content-Type"] = "application/json; charset=utf-8";
                    string yanit = (wc.UploadString(Musteri.WebAppUrl, "POST", json) ?? "").Trim();
                    if (!yanit.Equals("OK", StringComparison.OrdinalIgnoreCase))
                    {
                        string m = "MakineLog kabul edilmedi (Apps Script v110 dagitildi mi?): " + (yanit.Length > 150 ? yanit.Substring(0, 150) : yanit);
                        Logger.WriteFileOnly("kurulum.txt", m);
                        return m;
                    }
                    return "OK";
                }
            }
            catch (Exception ex) { Logger.WriteFileOnly("kurulum.txt", "MakineLog gonderilemedi: " + ex.Message); return "MakineLog gonderilemedi: " + ex.Message; }
        }

        private sealed class ZamanAsimliWebClient : WebClient
        {
            protected override WebRequest GetWebRequest(Uri address)
            {
                var req = base.GetWebRequest(address);
                if (req != null) req.Timeout = 30000;
                return req;
            }
        }

        /// <summary>Tam JSON metin kacisi (satir sonu/kontrol karakterleri dahil).</summary>
        public static string J(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length + 16);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
