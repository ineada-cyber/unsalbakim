using System;
using System.Net;
using System.Text;

namespace UnsalBakim.Core
{
    /// <summary>v110: Pilot guncelleme + Koruma acil anahtari icin panelden Sheet'e yazma.</summary>
    public static class FiloV110
    {
        private sealed class ZamanAsimliWebClient : WebClient
        {
            protected override WebRequest GetWebRequest(Uri address)
            {
                var req = base.GetWebRequest(address);
                if (req != null) req.Timeout = 15000;
                return req;
            }
        }

        private static string Gonder(string tip, string ekAlanlar)
        {
            if (string.IsNullOrWhiteSpace(Musteri.WebAppUrl)) return "WebAppUrl bos";
            string json = "{\"tip\":\"" + tip + "\",\"token\":\"" + MakineLog.J(Musteri.ApiToken) +
                          "\",\"musteriId\":\"" + MakineLog.J(Musteri.MusteriId) + "\"," + ekAlanlar +
                          ",\"guncelleyen\":\"" + MakineLog.J(Environment.UserName + "@" + Environment.MachineName) + "\"}";
            try { ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; } catch { }
            using (var wc = new ZamanAsimliWebClient())
            {
                wc.Encoding = Encoding.UTF8;
                wc.Headers["Content-Type"] = "application/json; charset=utf-8";
                return (wc.UploadString(Musteri.WebAppUrl, "POST", json) ?? "").Trim();
            }
        }

        /// <summary>hedef bos/TUMU = pilot kisiti KALDIR (herkes guncellenir).</summary>
        public static string PilotAyarla(string hedef)
        {
            try
            {
                string h = (hedef ?? "").Trim();
                if (h.Equals("TUMU", StringComparison.OrdinalIgnoreCase)) h = "";
                string y = Gonder("pilot", "\"hedef\":\"" + MakineLog.J(h) + "\",\"surum\":\"\"");
                if (y != "OK") return "Yazilamadi, Apps Script yaniti: '" + y + "' (Apps Script v110 dagitildi mi?)";
                return h == ""
                    ? "Pilot KALDIRILDI: Drive'daki yeni surumu TUM makineler alir (ilk 5 dk'lik turda)."
                    : "Pilot AYARLANDI: Drive'a yuklenen yeni surumu SADECE '" + h + "' alir; digerleri\r\n" +
                      "pilot kaldirilana kadar BEKLER. (Not: bu kisiti v110 ve sonrasi makineler uygular;\r\n" +
                      "henuz v110'a gecmemis makineler eskisi gibi hemen guncellenir.)";
            }
            catch (Exception ex) { return "Yazilamadi: " + ex.Message; }
        }

        public static string KorumaAyarla(string hedef, bool kapat)
        {
            try
            {
                string h = string.IsNullOrWhiteSpace(hedef) ? "TUMU" : hedef.Trim();
                string y = Gonder("korumaacil", "\"hedef\":\"" + MakineLog.J(h) + "\",\"mod\":\"" + (kapat ? "KAPALI" : "NORMAL") + "\"");
                if (y != "OK") return "Yazilamadi, Apps Script yaniti: '" + y + "' (Apps Script v110 dagitildi mi?)";
                return kapat
                    ? "KORUMA KAPATMA gonderildi (hedef: " + h + "). Hedefteki OGRENCI makinelerinde en gec ~5 dk\r\n" +
                      "icinde AppLocker ve klasor kilitleri geri alinir (kapali makineler acilinca). Diger engeller\r\n" +
                      "(oyun/VPN/site/sinav) ETKILENMEZ. Sorun cozulunce 'Korumayi normale dondur'e basin."
                    : "Koruma NORMALE donduruldu: makineler ~5 dk icinde korumalari yeniden uygular.";
            }
            catch (Exception ex) { return "Yazilamadi: " + ex.Message; }
        }
    }
}
