using System;
using System.IO;
using System.Net;

namespace UnsalBakim.Core
{
    /// <summary>
    /// v80 (Ali Bey'in istegi: "Filo menusune envanter gonder koyalim, calisan tum makinelerin
    /// envanterini makinelerden istesin"): FiloKapat.cs ile BIREBIR AYNI "tek seferlik komut"
    /// deseni -- ogretmen, Filo Yonetimi panelindeki Hedef (TUMU / lab adi / tek bilgisayar)
    /// secimiyle, hedefteki makine(ler)e "envanterini SIMDI gonder" komutu yollar. Her makine
    /// 5 dk'lik denetim dongusunde kendi hedefine uyan EN SON komutun zaman damgasini okur; bu
    /// damga YENI ise (yerel olarak son uygulanandan farkliysa) envanterini hemen gonderir ve
    /// damgayi yerel olarak kaydeder -- boylece makine daha sonra (gunler sonra) yeniden kontrol
    /// ettiginde AYNI eski komut icin tekrar gondermez; sadece ogretmen YENIDEN "Envanter gonder"
    /// derse tekrar calisir. Bu, Sinav/Kapat'in aksine bir KISITLAMA degildir -- OGRETMEN
    /// makineleri de (filo zorlamasindan muaf olsalar bile) bu komuta uyar (bkz. Program.cs).
    /// </summary>
    public static class EnvanterIste
    {
        private const string Log = "envanter_iste.txt";

        private static string OnbellekYolu =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "UnsalBakim", "envanter_iste_son_uygulanan.txt");

        private sealed class ZamanAsimliWebClient : WebClient
        {
            protected override WebRequest GetWebRequest(Uri address)
            {
                var req = base.GetWebRequest(address);
                if (req != null) req.Timeout = 10000;
                return req;
            }
        }

        /// <summary>PANELDEN cagrilir: hedefteki makine(ler)e yeni bir "envanter gonder" komutu yollar.</summary>
        public static string FiloBaslat(string hedef)
        {
            if (string.IsNullOrWhiteSpace(Musteri.WebAppUrl))
                return "Drive log ozelligi kurulmadi (WebAppUrl bos); yazilamadi.";
            try
            {
                string zaman = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                string json = "{\"tip\":\"envanteriste\",\"token\":\"" + Kacir(Musteri.ApiToken) + "\",\"musteriId\":\"" +
                              Kacir(Musteri.MusteriId) + "\",\"hedef\":\"" + Kacir(string.IsNullOrWhiteSpace(hedef) ? "TUMU" : hedef.Trim()) +
                              "\",\"zaman\":\"" + zaman + "\",\"guncelleyen\":\"" +
                              Kacir(Environment.UserName + "@" + Environment.MachineName) + "\"}";
                try { ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; } catch { }
                string yanit;
                using (var wc = new ZamanAsimliWebClient())
                {
                    wc.Headers["Content-Type"] = "application/json";
                    yanit = wc.UploadString(Musteri.WebAppUrl, "POST", json);
                }
                if (yanit != null && yanit.Trim() == "OK")
                    return "Envanter istegi gonderildi (hedef: " + hedef + "). Hedefteki makine(ler) en gec ~5 dakika " +
                           "icinde (acik ve agdaysa) kendi envanterini merkeze gonderecek.";
                return "Yazilamadi, Apps Script yaniti: '" + yanit + "' (script guncel olmayabilir).";
            }
            catch (Exception ex) { return "Yazilamadi: " + ex.Message; }
        }

        /// <summary>v80 (3. revizyon deseniyle AYNI): FiloDurum.Al'in birlesik istegiyle ONCEDEN
        /// alinmis bir deger uzerinde calisir -- kendi ag istegini atmaz.</summary>
        internal static void FiloKontrolEtVeUygulaDeger(string sunucuZaman, AppSettings ayar)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(sunucuZaman)) return;

                // FiloKapat.cs'teki AYNI savunma: beklenmeyen/HTML icerik asla "yeni komut" sayilmaz.
                if (!DateTime.TryParseExact(sunucuZaman.Trim(), "yyyy-MM-dd HH:mm:ss.fff",
                        System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                {
                    Logger.WriteFileOnly(Log, "Gecersiz/tanimsiz envanter-iste komutu icerigi goz ardi edildi (HTML hata sayfasi olabilir): '" + sunucuZaman.Trim() + "'");
                    return;
                }

                string yerelZaman = OnbellekOku();
                if (string.Equals(sunucuZaman, yerelZaman, StringComparison.Ordinal)) return; // zaten uygulandi

                OnbellekYaz(sunucuZaman);
                Logger.Write(Log, "Filo envanter istegi alindi (" + sunucuZaman + ") -- envanter simdi gonderiliyor.");
                DriveLog.EnvanterGonder(ayar);
                // v109: ayni komutla MakineLog da gonderilir (USB ile log toplamaya gerek kalmasin).
                try { MakineLog.Gonder(ayar, true); } catch { }
            }
            catch (Exception ex) { Logger.WriteFileOnly(Log, "Filo envanter istegi kontrolu hatasi: " + ex.Message); }
        }

        private static string OnbellekOku()
        {
            try { return File.Exists(OnbellekYolu) ? File.ReadAllText(OnbellekYolu).Trim() : null; }
            catch { return null; }
        }

        private static void OnbellekYaz(string zaman)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(OnbellekYolu));
                File.WriteAllText(OnbellekYolu, zaman ?? "");
            }
            catch { }
        }

        private static string Kacir(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
