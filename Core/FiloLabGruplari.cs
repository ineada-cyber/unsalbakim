using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace UnsalBakim.Core
{
    /// <summary>
    /// v105 (Ali Bey'in istegi: "hoca ORNEGIN BMTAL-LAB1-ORT1 menuden ONA SADECE BMTAL-LAB1 ICIN
    /// FILO ISLEMI YAPABILMELI, DIGER BOLUMLE ILGISI OLMAMALI"; naming otomatik ayristirilamadigi
    /// icin -- "bazi makineler farkli adlandirilmis" -- Ali Bey'in istegiyle ELLE YONETILEN bir
    /// liste secildi: "Menuden bu gruplari lazim oldukca silme, ekleme guncelleme ve girildiginde
    /// var olanlar gorulebilir").
    ///
    /// Lab Gruplari: grup adi (orn. "BMTAL-LAB1") -> o gruba ait makine-adi ON-EKLERI (',' ile
    /// ayrilmis, orn. "BMTAL-LAB1,BMTAL-LAB1-ORT1,LAB1-") eslemesini TUM FILODA PAYLASIMLI tutar.
    /// Daha onceki "Politika"/"Makine Listesi" ozellikleriyle AYNI kanalla (Drive/Apps Script)
    /// calisir -- bkz. AppsScript/DriveLog_v57.gs, 'labgruplari' (GET) / 'labgrubuyaz' /
    /// 'labgrubusil' (POST). Boylece HERHANGI bir ogretmenin Filo Yonetimi -> Lab Gruplari
    /// menusunden yaptigi ekleme/silme/guncelleme, EN GEC birkac dakika icinde TUM filoda ayni
    /// goruntulenir -- her makinenin kendi listesini ayri ayri yonetmesine gerek yoktur.
    ///
    /// ONEMLI: Bu sinif SADECE depo/erisim katmanidir. "Bu makine hangi grupta" ve "Hedef
    /// kisitlamasi nasil uygulanir" (kendi grubu disina cikilamamasi, Super Kullanici ile bypass)
    /// mantigi TAMAMEN Core/FiloKisitlama.cs'tedir -- sunucu (Apps Script) sadece bir DEPO
    /// gorevi gorur, hicbir kisitlama/erisim kontrolu YAPMAZ (bkz. FiloKisitlama.cs basindaki not).
    /// </summary>
    public static class FiloLabGruplari
    {
        private const string Log = "lab_gruplari.txt";

        private static string OnbellekYolu =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "UnsalBakim", "lab_gruplari_onbellek.txt");

        private sealed class ZamanAsimliWebClient : WebClient
        {
            protected override WebRequest GetWebRequest(Uri address)
            {
                var req = base.GetWebRequest(address);
                if (req != null) req.Timeout = 10000;
                return req;
            }
        }

        /// <summary>TUM lab gruplarini (GrupAdi -> Uyeler) doner. agKontrolEt=false ise SADECE
        /// yerel onbellegi okur (ag'a hic cikmaz -- panel acilirken UI is parcacigini BLOKE
        /// ETMEMEK icin kullanilir). agKontrolEt=true ise Drive'dan taze okur VE onbellegi
        /// gunceller; ag/Apps Script sorununda SESSIZCE en son bilinen onbellege duser. Hic
        /// onbellek de yoksa (ilk calisma, hic "Yenile"ye basilmamis) BOS sozluk doner -- bu,
        /// FiloKisitlama tarafindan GUVENLI varsayilan olarak "hicbir grup bilinmiyor, bu makine
        /// HICBIR gruba ait degil" seklinde yorumlanir (yani TAMAMEN KILITLI -- yanlislikla
        /// serbest birakma YOKTUR).</summary>
        public static Dictionary<string, string> Listele(bool agKontrolEt)
        {
            if (!agKontrolEt) return OnbellekOku();
            if (string.IsNullOrWhiteSpace(Musteri.WebAppUrl)) return OnbellekOku();

            try
            {
                try { ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; } catch { }
                string metin;
                using (var wc = new ZamanAsimliWebClient())
                {
                    wc.Headers["User-Agent"] = "UnsalBakim/1.0";
                    string url = Musteri.WebAppUrl + "?liste=labgruplari&token=" + Uri.EscapeDataString(Musteri.ApiToken) +
                                 "&musteriId=" + Uri.EscapeDataString(Musteri.MusteriId);
                    metin = wc.DownloadString(url);
                }
                var d = Coz(metin);
                OnbellekYaz(d);
                return d;
            }
            catch (Exception ex)
            {
                Logger.WriteFileOnly(Log, "Lab gruplari cekilemedi (onbellege dusuluyor): " + ex.Message);
                return OnbellekOku();
            }
        }

        /// <summary>PANELDEN cagrilir: bir grubu ekler (yoksa) veya gunceller (ayni grupAdi -- buyuk/
        /// kucuk harf duyarsiz -- zaten varsa, uyeler alani UZERINE YAZILIR). Basariliysa null,
        /// basarisizsa kullaniciya gosterilecek hata metni doner.</summary>
        public static string Yaz(string grupAdi, string uyeler)
        {
            if (string.IsNullOrWhiteSpace(Musteri.WebAppUrl))
                return "Drive log ozelligi kurulmadi (WebAppUrl bos); yazilamadi.";
            if (string.IsNullOrWhiteSpace(grupAdi))
                return "Grup adi bos olamaz.";
            try
            {
                string json = "{\"tip\":\"labgrubuyaz\",\"token\":\"" + Kacir(Musteri.ApiToken) + "\",\"musteriId\":\"" +
                              Kacir(Musteri.MusteriId) + "\",\"grupAdi\":\"" + Kacir(grupAdi.Trim()) +
                              "\",\"uyeler\":\"" + Kacir((uyeler ?? "").Trim()) + "\",\"guncelleyen\":\"" +
                              Kacir(Environment.UserName + "@" + Environment.MachineName) + "\"}";
                try { ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; } catch { }
                string yanit;
                using (var wc = new ZamanAsimliWebClient())
                {
                    wc.Headers["Content-Type"] = "application/json";
                    yanit = wc.UploadString(Musteri.WebAppUrl, "POST", json);
                }
                if (yanit != null && yanit.Trim() == "OK") { Listele(true); return null; }
                return "Yazilamadi, Apps Script yaniti: '" + yanit + "' (script guncel olmayabilir -- v105 Lab Gruplari ekini DriveLog_v57.gs'ten yapistirip yeniden dagitin).";
            }
            catch (Exception ex) { return "Yazilamadi: " + ex.Message; }
        }

        /// <summary>PANELDEN cagrilir: bir grubu siler. Basariliysa null, basarisizsa hata metni doner.</summary>
        public static string Sil(string grupAdi)
        {
            if (string.IsNullOrWhiteSpace(Musteri.WebAppUrl))
                return "Drive log ozelligi kurulmadi (WebAppUrl bos); silinemedi.";
            try
            {
                string json = "{\"tip\":\"labgrubusil\",\"token\":\"" + Kacir(Musteri.ApiToken) + "\",\"musteriId\":\"" +
                              Kacir(Musteri.MusteriId) + "\",\"grupAdi\":\"" + Kacir((grupAdi ?? "").Trim()) + "\"}";
                try { ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; } catch { }
                string yanit;
                using (var wc = new ZamanAsimliWebClient())
                {
                    wc.Headers["Content-Type"] = "application/json";
                    yanit = wc.UploadString(Musteri.WebAppUrl, "POST", json);
                }
                if (yanit != null && yanit.Trim() == "OK") { Listele(true); return null; }
                return "Silinemedi, Apps Script yaniti: '" + yanit + "'.";
            }
            catch (Exception ex) { return "Silinemedi: " + ex.Message; }
        }

        private static Dictionary<string, string> Coz(string metin)
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(metin)) return d;
            foreach (var satir in metin.Replace("\r\n", "\n").Split('\n'))
            {
                var s = satir.Trim();
                if (s.Length == 0) continue;
                int i = s.IndexOf('|');
                if (i <= 0) continue;
                string grupAdi = s.Substring(0, i).Trim();
                string uyeler = s.Substring(i + 1).Trim();
                if (grupAdi.Length == 0) continue;
                d[grupAdi] = uyeler;
            }
            return d;
        }

        private static Dictionary<string, string> OnbellekOku()
        {
            try
            {
                if (!File.Exists(OnbellekYolu)) return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                return Coz(File.ReadAllText(OnbellekYolu));
            }
            catch { return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); }
        }

        private static void OnbellekYaz(Dictionary<string, string> d)
        {
            try
            {
                string yol = OnbellekYolu;
                Directory.CreateDirectory(Path.GetDirectoryName(yol));
                var satirlar = new List<string>();
                foreach (var kv in d) satirlar.Add(kv.Key + "|" + kv.Value);
                File.WriteAllLines(yol, satirlar);
            }
            catch { }
        }

        private static string Kacir(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
