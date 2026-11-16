using System;
using System.Net;

namespace UnsalBakim.Core
{
    /// <summary>
    /// v92 (Ali Bey'in istegi: "sheet teki verileri temizleme secenegi mumkunmu" -- "Tum sheet
    /// alt sekmeleri lab bazinda" cevabi uzerine): PANELDEN Google Sheet verisini silme.
    /// v93 (Ali Bey'in geri bildirimi: "envanter tumu secince silindi, log silinmedi"): Log sayfasi
    /// icin de ayni "lab bazinda silme" eklendi -- Envanter ile BIREBIR AYNI mantik calisir.
    ///
    /// UC AYRI SILME TURU var, cunku Sheet'teki sayfalar FARKLI SEKILLERDE tutuluyor:
    ///   - Envanter / Log sayfalari: MAKINE BASINA (olay basina) bir satir, B sutununda bilgisayar
    ///     adi -- bu yuzden "lab bazinda temizleme" burada GERCEKTEN anlamli: sadece secilen
    ///     lab'daki (veya TUMU ise hepsinin) satirlari silinir. (Log'un TEK farki: basligi yok,
    ///     veri 1. satirdan baslar -- Apps Script tarafinda bu ayarlanir, C# tarafinda fark yok.)
    ///   - Kapat / YenidenBaslat / EnvanterIste / Sinav / Politika sayfalari: OKUL BASINA TEK
    ///     satir (Hedef sutunu o satirdaki komutun KIME uygulanacagini belirtir, AYRI SATIRLAR
    ///     DEGIL) -- bu yuzden burada "lab secimi" diye bir sey YOK; bu grubu temizlemek, o anda
    ///     bekleyen TUM filo komutlarini/politikayi okul genelinde sifirlar (iptal eder).
    /// Panelde bu fark Ali Bey'e acikca gosterilir (bkz. MainForm.cs'teki aciklama metni).
    /// </summary>
    public static class FiloSheetTemizle
    {
        private const string Log = "filo_sheet_temizle.txt";

        private sealed class ZamanAsimliWebClient : WebClient
        {
            protected override WebRequest GetWebRequest(Uri address)
            {
                var req = base.GetWebRequest(address);
                if (req != null) req.Timeout = 10000;
                return req;
            }
        }

        /// <summary>Envanter sayfasinda hedef'e (TUMU/BMTAL-LABn/tek makine) uyan makine
        /// satirlarini siler.</summary>
        public static string EnvanterTemizle(string hedef) => Gonder("envanter", hedef);

        /// <summary>v93: Log sayfasinda hedef'e (TUMU/BMTAL-LABn/tek makine) uyan olay
        /// satirlarini siler -- Envanter ile BIREBIR AYNI mantik.</summary>
        public static string LogTemizle(string hedef) => Gonder("log", hedef);

        /// <summary>Kapat/YenidenBaslat/EnvanterIste/Sinav/Politika sayfalarindaki, bu okula ait
        /// TEK satirlari siler (bekleyen tum filo komutlarini/politikayi sifirlar). hedef burada
        /// KULLANILMAZ (komut sayfalari okul basina tek satir tutar).</summary>
        public static string KomutlariTemizle() => Gonder("komutlar", "TUMU");

        private static string Gonder(string hedefSheet, string hedef)
        {
            if (string.IsNullOrWhiteSpace(Musteri.WebAppUrl))
                return "Drive log ozelligi kurulmadi (WebAppUrl bos); temizlenemedi.";
            try
            {
                string json = "{\"tip\":\"temizle\",\"token\":\"" + Kacir(Musteri.ApiToken) + "\",\"musteriId\":\"" +
                              Kacir(Musteri.MusteriId) + "\",\"hedefSheet\":\"" + Kacir(hedefSheet) + "\",\"hedef\":\"" +
                              Kacir(string.IsNullOrWhiteSpace(hedef) ? "TUMU" : hedef.Trim()) + "\"}";
                try { ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; } catch { }
                string yanit;
                using (var wc = new ZamanAsimliWebClient())
                {
                    wc.Headers["Content-Type"] = "application/json";
                    yanit = wc.UploadString(Musteri.WebAppUrl, "POST", json);
                }
                Logger.WriteFileOnly(Log, "Temizle istegi (" + hedefSheet + ", hedef=" + hedef + ") yaniti: " + yanit);
                if (yanit != null && yanit.Trim().StartsWith("OK:"))
                {
                    string sayi = yanit.Trim().Substring(3);
                    if (hedefSheet == "envanter") return sayi + " makine kaydi Envanter sayfasindan silindi.";
                    if (hedefSheet == "log") return sayi + " olay kaydi Log sayfasindan silindi.";
                    return sayi == "0"
                        ? "Zaten bekleyen filo komutu/politikasi yoktu."
                        : sayi + " sayfadaki bekleyen filo komutu/politika satiri sifirlandi.";
                }
                return "Temizlenemedi, Apps Script yaniti: '" + yanit + "' (script guncel olmayabilir -- 'temizle' tipi eklenmis mi?).";
            }
            catch (Exception ex) { return "Temizlenemedi: " + ex.Message; }
        }

        private static string Kacir(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
