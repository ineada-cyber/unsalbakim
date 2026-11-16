using System;
using System.IO;
using System.Net;
using Microsoft.Win32;

namespace UnsalBakim.Core
{
    /// <summary>
    /// v112 (13 Kasim 2026 incelemesi): ORTAK proxy yonetimi.
    ///
    /// Sorun: Program hem kullanici oturumunda (panel) hem de SYSTEM yetkisiyle calisan zamanlanmis
    /// gorevlerde ag istegi yapiyor. Bazi makineler internete yalnizca KULLANICIYA tanimli bir HTTP
    /// proxy (or. telefondaki Every Proxy) uzerinden cikiyor. .NET, varsayilan proxy olarak calisan
    /// HESABIN WinINET (IE) ayarini kullanir; SYSTEM'in bu ayari bos oldugu icin SYSTEM gorevleri
    /// Google'a (Apps Script/GitHub) ulasamiyordu ("Uzak ad cozulemedi").
    ///
    /// Cozum: Panel (kullanici oturumu) kullanicinin proxy ayarini okuyup ProgramData'ya yazar
    /// (PanelProxyYaz). HER surec baslangicta Uygula() cagirir; bu, proxy.cfg'den okuyup
    /// WebRequest.DefaultWebProxy'yi ayarlar. Boylece istemcideki TUM WebClient'lar (hicbiri kendi
    /// Proxy'sini elle set etmiyor) ayni proxy'yi kullanir. Proxy yoksa DOGRUDAN baglanti (okul/FATIH).
    ///
    /// Not: PAC/WPAD (AutoConfigURL) su an desteklenmiyor; manuel HTTP proxy (en yaygin durum)
    /// destekleniyor. Every Proxy ornegi kimlik dogrulamasiz oldugu icin bu yeterli.
    /// </summary>
    public static class AgProxy
    {
        private const string Log = "kurulum.txt";

        private static string Yol => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "UnsalBakim", "proxy.cfg");

        /// <summary>Her surecte (panel + SYSTEM gorevleri) baslangicta cagrilir: kayitli proxy'yi
        /// varsayilan yapar. proxy.cfg yoksa/DIRECT ise dogrudan baglanti.</summary>
        public static void Uygula()
        {
            try { WebRequest.DefaultWebProxy = Al(); } catch { }
        }

        /// <summary>proxy.cfg'ye gore IWebProxy dondurur; proxy yoksa null (= dogrudan baglanti).</summary>
        public static IWebProxy Al()
        {
            try
            {
                if (!File.Exists(Yol)) return null;
                string s = File.ReadAllText(Yol).Trim();
                if (s.Length == 0 || s.Equals("DIRECT", StringComparison.OrdinalIgnoreCase)) return null;
                string url = s.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? s : "http://" + s;
                return new WebProxy(url, true); // BypassProxyOnLocal = true (yerel adresler proxy'siz)
            }
            catch { return null; }
        }

        /// <summary>SADECE panel/kullanici oturumunda cagrilir: kullanicinin WinINET (IE) proxy ayarini
        /// okuyup ProgramData\proxy.cfg'ye yazar (SYSTEM gorevleri bunu okuyabilsin diye). Ayrica bu
        /// surecte de hemen uygular.</summary>
        public static void PanelProxyYaz()
        {
            try
            {
                string deger = KullaniciProxyOku();
                string klasor = Path.GetDirectoryName(Yol);
                Directory.CreateDirectory(klasor);
                string icerik = string.IsNullOrWhiteSpace(deger) ? "DIRECT" : deger;
                string onceki = File.Exists(Yol) ? File.ReadAllText(Yol).Trim() : "";
                if (!string.Equals(onceki, icerik, StringComparison.OrdinalIgnoreCase))
                {
                    File.WriteAllText(Yol, icerik);
                    try { Logger.WriteFileOnly(Log, "[Proxy] kullanici proxy'si yazildi: " + icerik); } catch { }
                }
                WebRequest.DefaultWebProxy = Al();
            }
            catch (Exception ex) { try { Logger.WriteFileOnly(Log, "[Proxy] yazilamadi: " + ex.Message); } catch { } }
        }

        private static string KullaniciProxyOku()
        {
            using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings"))
            {
                if (k == null) return null;
                object en = k.GetValue("ProxyEnable");
                int enabled = 0; if (en != null) int.TryParse(en.ToString(), out enabled);
                if (enabled != 1) return null;
                string srv = k.GetValue("ProxyServer") as string;
                if (string.IsNullOrWhiteSpace(srv)) return null;
                srv = srv.Trim();
                // "host:port" ya da "http=host:port;https=host:port;..." olabilir.
                if (srv.IndexOf('=') >= 0)
                {
                    string ilk = null;
                    foreach (var part in srv.Split(';'))
                    {
                        var kv = part.Split(new[] { '=' }, 2);
                        if (kv.Length != 2) continue;
                        if (ilk == null) ilk = kv[1].Trim();
                        if (kv[0].Trim().Equals("http", StringComparison.OrdinalIgnoreCase)) return kv[1].Trim();
                    }
                    return ilk; // http= yoksa ilk tanimli protokolun adresi
                }
                return srv;
            }
        }
    }
}
