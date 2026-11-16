using System;
using System.IO;
using System.Net;

namespace UnsalBakim.Core
{
    /// <summary>
    /// v89 (Ali Bey'in istegi: "Filo'ya bir menu daha ekleyelim, hedefteki makineleri filodan
    /// yeniden baslatalim"): FiloKapat.cs ile BIREBIR AYNI "tek seferlik komut" deseni -- ogretmen,
    /// Filo Yonetimi panelindeki Hedef (TUMU / lab adi / tek bilgisayar) secimiyle, hedefteki
    /// makine(ler)i uzaktan YENIDEN BASLATIR (kapatma degil). FiloKapat gibi bir KISITLAMA/
    /// zorlama turudur -- OGRETMEN makinesi muaf (bkz. Program.cs, FiloKapat ile AYNI blokta).
    ///
    /// TASARIM: Durum-bazli degil, TEK SEFERLIK KOMUT-bazli (FiloKapat ile AYNI). Her makine 5
    /// dk'lik denetim dongusunde kendi hedefine uyan EN SON komutun zaman damgasini okur; bu damga
    /// YENI ise (yerel olarak son uygulanandan farkliysa) makineyi yeniden baslatir ve damgayi
    /// yerel olarak kaydeder -- boylece makine daha sonra (gunler sonra) yeniden kontrol ettiginde
    /// AYNI eski komut icin tekrar yeniden baslamaz; sadece ogretmen YENIDEN "Yeniden Baslat"
    /// derse tekrar calisir.
    ///
    /// APPS SCRIPT EKI GEREKIR: DriveLog_v57.gs'e 'yenidenbaslat' POST tipi + topludurum yanitina
    /// 5. satir (yenidenBaslatSatiri_) eklenmesi gerekiyor -- bkz. ekte verilen guncel .gs dosyasi.
    /// Eski (henuz yeniden dagitilmamis) script 4 satir donerse bu satir BOS sayilir (hicbir yeni
    /// komut yok), HATA FIRLATILMAZ (FiloDurum.cs'teki geriye donuk uyumluluk ile ayni desen).
    /// </summary>
    public static class FiloYenidenBaslat
    {
        private const string Log = "filo_yenidenbaslat.txt";

        private static string OnbellekYolu =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "UnsalBakim", "yenidenbaslat_son_uygulanan.txt");

        private sealed class ZamanAsimliWebClient : WebClient
        {
            protected override WebRequest GetWebRequest(Uri address)
            {
                var req = base.GetWebRequest(address);
                if (req != null) req.Timeout = 10000;
                return req;
            }
        }

        /// <summary>PANELDEN cagrilir: hedefteki makine(ler)e yeni bir yeniden baslatma komutu gonderir.</summary>
        public static string FiloBaslat(string hedef)
        {
            if (string.IsNullOrWhiteSpace(Musteri.WebAppUrl))
                return "Drive log ozelligi kurulmadi (WebAppUrl bos); yazilamadi.";
            try
            {
                string zaman = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                string json = "{\"tip\":\"yenidenbaslat\",\"token\":\"" + Kacir(Musteri.ApiToken) + "\",\"musteriId\":\"" +
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
                    return "Yeniden baslatma komutu gonderildi (hedef: " + hedef + "). Hedefteki makine(ler) en gec ~5 dakika " +
                           "icinde (acik ve kullanimda ise, uyarisiz/aninda) yeniden baslayacak.";
                return "Yazilamadi, Apps Script yaniti: '" + yanit + "' (script guncel olmayabilir -- 'yenidenbaslat' tipi eklenmis mi?).";
            }
            catch (Exception ex) { return "Yazilamadi: " + ex.Message; }
        }

        /// <summary>FiloDurum.Al'in birlesik istegiyle ONCEDEN alinmis bir deger uzerinde calisir --
        /// kendi ag istegini atmaz (FiloKapat/EnvanterIste ile AYNI desen).</summary>
        internal static void FiloKontrolEtVeUygulaDeger(string sunucuZaman)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(sunucuZaman)) return;

                // FiloKapat.cs'teki AYNI savunma: beklenmeyen/HTML icerik asla "yeni komut" sayilmaz.
                if (!DateTime.TryParseExact(sunucuZaman.Trim(), "yyyy-MM-dd HH:mm:ss.fff",
                        System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                {
                    Logger.WriteFileOnly(Log, "Gecersiz/tanimsiz yeniden-baslatma komutu icerigi goz ardi edildi (HTML hata sayfasi olabilir): '" + sunucuZaman.Trim() + "'");
                    return;
                }

                string yerelZaman = OnbellekOku();
                if (string.Equals(sunucuZaman, yerelZaman, StringComparison.Ordinal)) return; // zaten uygulandi

                // Once "uygulandi" damgasini yaz (yeniden baslama sirasinda/sonrasinda tekrar
                // denemeyi onlemek icin) -- sonra yeniden baslat.
                OnbellekYaz(sunucuZaman);
                Logger.Write(Log, "Filo yeniden baslatma komutu alindi (" + sunucuZaman + ") -- makine yeniden baslatiliyor.");
                // v96 (Ali Bey'in istegi: "sure vermeden islemi yapsin"): eskiden /t 30 (30 sn
                // uyari) idi -- artik /t 0, SURESIZ/ANINDA yeniden baslar.
                // v106 duzeltme (Ali Bey'in bulgusu: "oyun veya bir sitede olanlarin makinasi gec
                // restart oluyor, reset/interrupt gucunde emir veremiyor musun"): /t 0 TEK BASINA
                // yeterli DEGILDI -- /f (force) OLMADAN Windows, acik bir oyun/tarayici "kaydetmeden
                // kapat" gibi bir uyari gosterirse KULLANICI CEVABINI BEKLER, bu da gecikmeye yol
                // aciyordu. /f eklenince TUM uygulamalar SORULMADAN/BEKLENMEDEN kapatilip aninda
                // yeniden baslatiliyor -- tam istenen "zorla/kesin" davranis.
                NativeRunner.Run("shutdown.exe",
                    "/r /f /t 0 /c \"Unsal Bakim: uzaktan (filo) yeniden baslatma komutu\"", Log, false);
            }
            catch (Exception ex) { Logger.WriteFileOnly(Log, "Filo yeniden baslatma kontrolu hatasi: " + ex.Message); }
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
