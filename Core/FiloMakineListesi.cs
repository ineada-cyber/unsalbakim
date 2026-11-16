using System;
using System.Collections.Generic;
using System.Net;

namespace UnsalBakim.Core
{
    /// <summary>
    /// v91 (Ali Bey'in istegi: "Lab secilince altinda [TUMU'nun altinda] o lab'daki OGR'ler
    /// listelenmeli, kullanimi kolay olsun"): Drive'daki Envanter sayfasindan GERCEK makine
    /// adlarini ceker -- Filo Yonetimi panelindeki Hedef kutusunu "TUMU + BMTAL-LAB1..10"
    /// sabit listesinin YANI SIRA, her lab'in altina o lab'da kayitli bilgisayarlari da
    /// EKLEYEREK doldurmak icin kullanilir.
    /// v68'de kaldirilan FiloEnvanter.cs'in YERINE -- o, lab secildiginde OTOMATIK (her
    /// secimde ayri bir ag istegiyle) calisiyordu; Ali Bey'in "karisik/kafa karistirici"
    /// bulup kaldirttigi karmasikligi onlemek icin burada TEK bir "Yenile" dugmesiyle, TEK
    /// bir istekte TUM makine listesi cekilip panel ACIKKEN BELLEKTE tutulur.
    /// </summary>
    public static class FiloMakineListesi
    {
        private const string Log = "filo_makineler.txt";

        private sealed class ZamanAsimliWebClient : WebClient
        {
            protected override WebRequest GetWebRequest(Uri address)
            {
                var req = base.GetWebRequest(address);
                if (req != null) req.Timeout = 10000;
                return req;
            }
        }

        /// <summary>Drive'daki Envanter sayfasindaki TUM bilgisayar adlarini ceker (bos/gecersiz
        /// yaniti bos listeye cevirir; ag/Apps Script sorununda istisna firlatir -- cagiran
        /// taraf kullaniciya hata gosterebilsin diye YUTULMAZ).</summary>
        public static List<string> Listele()
        {
            if (string.IsNullOrWhiteSpace(Musteri.WebAppUrl))
                throw new InvalidOperationException("Drive log ozelligi kurulmadi (WebAppUrl bos).");

            try { ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; } catch { }
            string metin;
            using (var wc = new ZamanAsimliWebClient())
            {
                wc.Headers["User-Agent"] = "UnsalBakim/1.0";
                string url = Musteri.WebAppUrl + "?liste=makineler&token=" + Uri.EscapeDataString(Musteri.ApiToken) +
                             "&musteriId=" + Uri.EscapeDataString(Musteri.MusteriId);
                metin = wc.DownloadString(url);
            }

            var sonuc = new List<string>();
            if (string.IsNullOrWhiteSpace(metin)) return sonuc;
            // HTML/hata sayfasi asla makine adi listesi sayilmaz.
            if (metin.IndexOf('<') >= 0 || metin.Trim().Equals("AUTH_ERROR", StringComparison.OrdinalIgnoreCase) ||
                metin.IndexOf("Yetkisiz", StringComparison.OrdinalIgnoreCase) >= 0)
                throw new InvalidOperationException("Beklenmeyen yanit (script guncel olmayabilir -- yeniden dagitilmis mi?).");

            foreach (var satir in metin.Replace("\r\n", "\n").Split('\n'))
            {
                var ad = satir.Trim();
                if (ad.Length > 0 && !sonuc.Contains(ad)) sonuc.Add(ad);
            }
            sonuc.Sort(StringComparer.OrdinalIgnoreCase);
            Logger.WriteFileOnly(Log, "Makine listesi cekildi: " + sonuc.Count + " adet.");
            return sonuc;
        }
    }
}
