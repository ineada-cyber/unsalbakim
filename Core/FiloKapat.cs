using System;
using System.IO;
using System.Net;

namespace UnsalBakim.Core
{
    /// <summary>
    /// v57: "Filo'dan Kapat" -- aksam ogrenciler makineleri acik unutmus olabilir; ogretmen,
    /// Filo Yonetimi panelindeki Hedef (TUMU / lab adi / tek bilgisayar) secimiyle, hedefteki
    /// makine(ler)i uzaktan KAPATABILIR. FiloPolitika.cs / InternetEngeli.cs'teki AYNI "Hedef"
    /// deseni (bilgisayar adi on-eki, sunucu tarafinda filtrelenir).
    ///
    /// TASARIM: Durum-bazli (sure/bitis) degil, TEK SEFERLIK KOMUT-bazli. Sunucuya her "Kapat"
    /// tiklamasinda YENI bir zaman damgasi (komut kimligi) yazilir. Her makine 5 dk'lik denetim
    /// dongusunde kendi hedefine uyan EN SON komutun zaman damgasini okur; bu damga, o makinenin
    /// YEREL olarak son uyguladigi damgadan farkliysa (yani YENI bir komutsa) kapanir ve damgayi
    /// yerel olarak kaydeder -- boylece makine daha sonra (gunler sonra) yeniden acildiginda AYNI
    /// eski komut icin TEKRAR kapanmaz; sadece ogretmen YENIDEN "Kapat" derse tekrar calisir.
    /// </summary>
    public static class FiloKapat
    {
        private const string Log = "filo_kapat.txt";

        private static string OnbellekYolu =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "UnsalBakim", "kapat_son_uygulanan.txt");

        private sealed class ZamanAsimliWebClient : WebClient
        {
            protected override WebRequest GetWebRequest(Uri address)
            {
                var req = base.GetWebRequest(address);
                if (req != null) req.Timeout = 10000;
                return req;
            }
        }

        /// <summary>PANELDEN cagrilir: hedefteki makine(ler)e yeni bir kapatma komutu gonderir.</summary>
        public static string FiloBaslat(string hedef)
        {
            if (string.IsNullOrWhiteSpace(Musteri.WebAppUrl))
                return "Drive log ozelligi kurulmadi (WebAppUrl bos); yazilamadi.";
            try
            {
                string zaman = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                string json = "{\"tip\":\"kapat\",\"token\":\"" + Kacir(Musteri.ApiToken) + "\",\"musteriId\":\"" +
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
                    return "Kapatma komutu gonderildi (hedef: " + hedef + "). Hedefteki makine(ler) en gec ~5 dakika " +
                           "icinde (acik ve kullanimda ise, uyarisiz/aninda) kapanacak.";
                return "Yazilamadi, Apps Script yaniti: '" + yanit + "' (script guncel olmayabilir).";
            }
            catch (Exception ex) { return "Yazilamadi: " + ex.Message; }
        }

        /// <summary>5 dk'lik denetim dongusunden cagrilir: filodan bu makineye yonelik YENI bir
        /// kapatma komutu var mi diye bakar; varsa makineyi kapatir ve komutu "uygulandi" olarak
        /// yerel isaretler (ayni komut icin bir daha kapanmaz).</summary>
        public static void FiloKontrolEtVeUygula(bool agKontrolEt)
        {
            string sunucuZaman = agKontrolEt ? FiloOku() : null;
            FiloKontrolEtVeUygulaDeger(sunucuZaman);
        }

        /// <summary>v57 (3. revizyon): FiloDurum.Al'in birlesik istegiyle ONCEDEN alinmis bir
        /// deger uzerinde calisir -- kendi ag istegini atmaz (3 ayri istegi 1'e dusurme).</summary>
        internal static void FiloKontrolEtVeUygulaDeger(string sunucuZaman)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(sunucuZaman)) return;

                // v57 hata duzeltmesi: Apps Script'ten beklenen deger yerine bir HATA/HTML sayfasi
                // dondugunde (yetkisiz istek, eski dagitim, gecici Google hatasi), o HTML'in bir
                // satiri ("<head>" gibi) buraya "yeni komut" olarak gelip makineyi GERCEKTEN
                // KAPATIYORDU -- bir ogretmen testte canli yakaladi. FiloDurum.Al zaten bu bicim
                // kontrolunu yapiyor, ama bu metot FiloOku() (eski, dogrudan) yolundan da
                // cagrilabildigi icin burada da AYNI dogrulama tekrarlanir (savunma katmani).
                if (!DateTime.TryParseExact(sunucuZaman.Trim(), "yyyy-MM-dd HH:mm:ss.fff",
                        System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                {
                    Logger.WriteFileOnly(Log, "Gecersiz/tanimsiz kapat komutu icerigi goz ardi edildi (HTML hata sayfasi olabilir): '" + sunucuZaman.Trim() + "'");
                    return;
                }

                string yerelZaman = OnbellekOku();
                if (string.Equals(sunucuZaman, yerelZaman, StringComparison.Ordinal)) return; // zaten uygulandi

                // Once "uygulandi" damgasini yaz (kapanma sirasinda/sonrasinda tekrar denemeyi
                // onlemek icin) -- sonra kapat.
                OnbellekYaz(sunucuZaman);
                Logger.Write(Log, "Filo kapatma komutu alindi (" + sunucuZaman + ") -- makine kapatiliyor.");
                // v96 (Ali Bey'in istegi: "sure vermeden islemi yapsin"): eskiden /t 30 (30 sn
                // uyari) idi -- artik /t 0, SURESIZ/ANINDA kapanir.
                // v106 duzeltme (Ali Bey'in bulgusu -- FiloYenidenBaslat.cs ile AYNI sorun: "oyun
                // veya bir sitede olanlarin makinasi gec kapaniyor"): /f (force) eklendi -- acik
                // oyun/tarayici/kaydetmemis-uygulama SORULMADAN/BEKLENMEDEN kapatilir, aninda kapanir.
                NativeRunner.Run("shutdown.exe",
                    "/s /f /t 0 /c \"Unsal Bakim: uzaktan (filo) kapatma komutu\"", Log, false);
            }
            catch (Exception ex) { Logger.WriteFileOnly(Log, "Filo kapatma kontrolu hatasi: " + ex.Message); }
        }

        private static string FiloOku()
        {
            if (string.IsNullOrWhiteSpace(Musteri.WebAppUrl)) return null;
            try
            {
                try { ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; } catch { }
                string metin;
                using (var wc = new ZamanAsimliWebClient())
                {
                    wc.Headers["User-Agent"] = "UnsalBakim/1.0";
                    string url = Musteri.WebAppUrl + "?liste=kapat&token=" + Uri.EscapeDataString(Musteri.ApiToken) +
                                 "&musteriId=" + Uri.EscapeDataString(Musteri.MusteriId) +
                                 "&bilgisayarAdi=" + Uri.EscapeDataString(Environment.MachineName);
                    metin = wc.DownloadString(url);
                }
                return string.IsNullOrWhiteSpace(metin) ? null : metin.Trim();
            }
            catch (Exception ex)
            {
                Logger.WriteFileOnly(Log, "Filo kapatma durumu kontrol edilemedi: " + ex.Message);
                return null;
            }
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
