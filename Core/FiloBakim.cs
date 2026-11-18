using System;
using System.IO;
using System.Net;

namespace UnsalBakim.Core
{
    /// <summary>
    /// v116 (Ali Bey'in istegi: "menu filoya 2 secenek eklesek filodan hafif bakim calistir,
    /// agir bakim calistir secenegi agir bakim secilirse uyari versin kisiye devam-dur secenegi
    /// sunsun" -- ve ek olarak "ogretmen ve ogrenci icin gecerli olmali"): FiloKapat/
    /// FiloYenidenBaslat ile BIREBIR AYNI "tek seferlik komut" deseni, ama IKI ayri komut tipi
    /// (Hafif/Agir) tek sinifta -- her biri kendi Sheet'inde ("HafifBakim"/"AgirBakim") ve kendi
    /// yerel onbellek dosyasinda.
    ///
    /// ONEMLI FARK: Kapat/YenidenBaslat/Sinav/Politika "KISITLAMA/zorlama" turudur ve OGRETMEN
    /// makinesi bunlardan MUAF'tir (bkz. Program.cs). Ali Bey'in bu ozellik icin acik istegi
    /// ("ogretmen ve ogrenci icin gecerli olmali") bunun TERSI: bakim komutu HER makinede
    /// (ogretmen dahil) calisir -- Envanter-iste ile AYNI "muafiyet DISI, ogretmen muafiyet
    /// bloğunun DISINDA cagrilir" kuralina tabi (bkz. Program.cs'teki cagri noktasi).
    ///
    /// GUVENLIK: Teshis'in aksine bu SALT-OKUNUR degildir -- gercek bakim islemlerini
    /// (Maintenance.HafifBakim / Maintenance.AgirBakim) CALISTIRIR. Agir Bakim ozellikle
    /// 30-90 dk surebilir; bu yuzden PANELDEN (ogretmen tarafinda) gonderilmeden once bir
    /// Devam/Dur onay penceresi gosterilir (bkz. MainForm.cs) -- hedef makinede ekstra bir
    /// onay YOKTUR (Kapat/YenidenBaslat ile ayni: sessiz/arka planda calisir).
    ///
    /// APPS SCRIPT EKI GEREKIR: Kod.gs'e 'hafifbakim'/'agirbakim' POST tipleri + topludurum
    /// yanitina 11. ve 12. satir (hafifBakimSatiri_/agirBakimSatiri_) eklenmesi gerekiyor.
    /// Eski (henuz yeniden dagitilmamis) script bu satirlari donmezse BOS sayilir (hicbir yeni
    /// komut yok), HATA FIRLATILMAZ (FiloDurum.cs'teki geriye donuk uyumluluk ile ayni desen).
    /// </summary>
    public static class FiloBakim
    {
        private const string LogHafif = "filo_hafif_bakim.txt";
        private const string LogAgir = "filo_agir_bakim.txt";

        private static string OnbellekYolu(bool agir) =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "UnsalBakim",
                agir ? "agirbakim_son_uygulanan.txt" : "hafifbakim_son_uygulanan.txt");

        private sealed class ZamanAsimliWebClient : WebClient
        {
            protected override WebRequest GetWebRequest(Uri address)
            {
                var req = base.GetWebRequest(address);
                if (req != null) req.Timeout = 10000;
                return req;
            }
        }

        /// <summary>PANELDEN cagrilir: hedefteki makine(ler)e yeni bir Hafif/Agir Bakim komutu
        /// gonderir. agir=false -> "hafifbakim" tipi, agir=true -> "agirbakim" tipi.</summary>
        public static string FiloBaslat(string hedef, bool agir)
        {
            if (string.IsNullOrWhiteSpace(Musteri.WebAppUrl))
                return "Drive log ozelligi kurulmadi (WebAppUrl bos); yazilamadi.";
            try
            {
                string zaman = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                string json = "{\"tip\":\"" + (agir ? "agirbakim" : "hafifbakim") + "\",\"token\":\"" + Kacir(Musteri.ApiToken) + "\",\"musteriId\":\"" +
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
                    return (agir ? "Agir bakim" : "Hafif bakim") + " komutu gonderildi (hedef: " + hedef + "). Hedefteki makine(ler) en gec ~5 dakika " +
                           "icinde (acik ve agdaysa) bakima baslayacak" + (agir ? " (30-90 dakika surebilir)." : " (birkac dakika surer).");
                return "Yazilamadi, Apps Script yaniti: '" + yanit + "' (script guncel olmayabilir -- '" + (agir ? "agirbakim" : "hafifbakim") + "' tipi eklenmis mi?).";
            }
            catch (Exception ex) { return "Yazilamadi: " + ex.Message; }
        }

        /// <summary>FiloDurum.Al'in birlesik istegiyle ONCEDEN alinmis degerler uzerinde calisir --
        /// kendi ag istegini atmaz (FiloKapat/EnvanterIste ile AYNI desen). Her iki komut tipi de
        /// (varsa) BAGIMSIZ olarak kontrol edilir/uygulanir.</summary>
        internal static void FiloKontrolEtVeUygulaDeger(string hafifSunucuZaman, string agirSunucuZaman, AppSettings ayar)
        {
            TekKomutKontrolEtVeUygula(hafifSunucuZaman, false, ayar);
            TekKomutKontrolEtVeUygula(agirSunucuZaman, true, ayar);
        }

        private static void TekKomutKontrolEtVeUygula(string sunucuZaman, bool agir, AppSettings ayar)
        {
            string log = agir ? LogAgir : LogHafif;
            try
            {
                if (string.IsNullOrWhiteSpace(sunucuZaman)) return;

                // FiloKapat.cs'teki AYNI savunma: beklenmeyen/HTML icerik asla "yeni komut" sayilmaz.
                if (!DateTime.TryParseExact(sunucuZaman.Trim(), "yyyy-MM-dd HH:mm:ss.fff",
                        System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                {
                    Logger.WriteFileOnly(log, "Gecersiz/tanimsiz bakim komutu icerigi goz ardi edildi (HTML hata sayfasi olabilir): '" + sunucuZaman.Trim() + "'");
                    return;
                }

                string yerelZaman = OnbellekOku(agir);
                if (string.Equals(sunucuZaman, yerelZaman, StringComparison.Ordinal)) return; // zaten uygulandi

                // Once "uygulandi" damgasini yaz (bakim sirasinda/sonrasinda tekrar denemeyi
                // onlemek icin) -- sonra bakimi calistir.
                OnbellekYaz(agir, sunucuZaman);
                Logger.Write(log, "Filo " + (agir ? "agir" : "hafif") + " bakim komutu alindi (" + sunucuZaman + ") -- bakim baslatiliyor.");
                if (agir) Maintenance.AgirBakim(ayar);
                else Maintenance.HafifBakim(ayar);
                Logger.Write(log, "Filo " + (agir ? "agir" : "hafif") + " bakim tamamlandi (" + sunucuZaman + ").");
            }
            catch (Exception ex) { Logger.WriteFileOnly(log, "Filo " + (agir ? "agir" : "hafif") + " bakim kontrolu hatasi: " + ex.Message); }
        }

        private static string OnbellekOku(bool agir)
        {
            try { string p = OnbellekYolu(agir); return File.Exists(p) ? File.ReadAllText(p).Trim() : null; }
            catch { return null; }
        }

        private static void OnbellekYaz(bool agir, string zaman)
        {
            try
            {
                string p = OnbellekYolu(agir);
                Directory.CreateDirectory(Path.GetDirectoryName(p));
                File.WriteAllText(p, zaman ?? "");
            }
            catch { }
        }

        private static string Kacir(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
