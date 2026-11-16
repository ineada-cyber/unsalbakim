using System;
using System.Collections.Generic;
using System.Net;

namespace UnsalBakim.Core
{
    /// <summary>
    /// Merkezi Google Sheet aktarimi.
    /// v54 Apps Script kaynagi: gelistirme/AppsScript/DriveLog_v54.gs
    /// Bu script, Log filtresini Europe/Istanbul takvim gunune gore duzeltir; Log, Envanter
    /// ve Durum akisini birlikte korur. v54 Apps Script yeni surum olarak dagitilmalidir.
    /// </summary>
    public static class DriveLog
    {
        // v50: WebAppUrl/ApiToken artik Musteri.cs'te TEK yerde tutuluyor (degerler v49'dakiyle
        // AYNI, sadece tasindi). Kodun geri kalaninda kisa olsun diye yerel degiskenlere atiyoruz.
        private static string WebAppUrl => Musteri.WebAppUrl;
        private static string ApiToken => Musteri.ApiToken;

        private static void SunucuYanitiKontrolEt(string islem, string yanit, bool v54Gerekli = false)
        {
            string sonuc = (yanit ?? "").Trim();
            if (v54Gerekli && string.Equals(sonuc, "OK_V54", StringComparison.OrdinalIgnoreCase)) return;
            if (!v54Gerekli && string.Equals(sonuc, "OK", StringComparison.OrdinalIgnoreCase)) return;
            if (v54Gerekli && string.Equals(sonuc, "OK", StringComparison.OrdinalIgnoreCase))
            {
                Logger.WriteFileOnly("kurulum.txt", "Drive " + islem + ": Envanter sunucuya gitti, ancak eski Apps Script yanit verdi. " +
                    "GUNLUK Log kaydi icin DriveLog_v54.gs yeni surum olarak dagitilmalidir.");
                return;
            }
            if (sonuc.Length > 200) sonuc = sonuc.Substring(0, 200);
            Logger.WriteFileOnly("kurulum.txt", "Drive " + islem + " kabul edilmedi; Apps Script yaniti: " +
                (sonuc.Length == 0 ? "<bos>" : sonuc));
        }

        // Varsayilan WebClient'in zaman asimi YOK -- Apps Script yavas/yanitsiz kalirsa dugme/gorev
        // SURESIZ takilabiliyordu (v36 duzeltmesi). Sabit bir sure sonra hata firlatir.
        private sealed class ZamanAsimliWebClient : WebClient
        {
            protected override WebRequest GetWebRequest(Uri address)
            {
                var req = base.GetWebRequest(address);
                if (req != null) req.Timeout = 20000; // 20 sn
                return req;
            }
        }

        /// <summary>Bir guncelleme olayini (basarili/basarisiz) Drive'daki Google Sheet'e kaydeder.
        /// WebAppUrl bos ise hicbir sey yapmaz (ag erisimi dahi denenmez).</summary>
        public static void Gonder(string olay, string detay)
        {
            if (string.IsNullOrWhiteSpace(WebAppUrl)) return;
            try
            {
                string bilgisayar = Environment.MachineName;
                string mac = MacAdresi();
                // v50: musteriId eklendi -- Apps Script su an bunu OKUMUYOR/kullanmiyor (tek musteri
                // var, gerek yok), ama ileride ortak/merkezi bir sisteme gecilirse kayitlar hangi
                // musteriye ait oldugunu baştan tasisin diye simdiden gonderiliyor (bkz. Musteri.cs).
                string json = "{\"tip\":\"log\",\"token\":\"" + Kacir(ApiToken) + "\",\"musteriId\":\"" + Kacir(Musteri.MusteriId) +
                              "\",\"bilgisayar\":\"" + Kacir(bilgisayar) + "\",\"mac\":\"" + Kacir(mac) +
                              "\",\"olay\":\"" + Kacir(olay) + "\",\"detay\":\"" + Kacir(detay) + "\"}";
                try { ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; } catch { }
                using (var wc = new ZamanAsimliWebClient())
                {
                    wc.Headers["Content-Type"] = "application/json";
                    string yanit = wc.UploadString(WebAppUrl, "POST", json);
                    SunucuYanitiKontrolEt("Log gonderimi", yanit);
                }
            }
            catch (Exception ex) { Logger.WriteFileOnly("kurulum.txt", "Drive log gonderilemedi: " + ex.Message); }
        }

        /// <summary>v49: Bu makinenin envanterini (CPU/RAM/disk/Windows/lisans/surum + kimlik
        /// bilgileri) Drive'daki "Envanter" sayfasina gonderir -- MachineId'ye gore UPSERT (ayni
        /// makine her gun aynI satiri gunceller, yeni satir eklemez). Hafif Bakim gorevinden
        /// GUNLUK otomatik cagrilir; ayrica panelden elle de tetiklenebilir. WebAppUrl bos ise
        /// veya Apps Script henuz guncellenmediyse basarisiz olabilir; sunucu cevabi yerel
        /// kurulum.txt dosyasina yazilir. hafifBakimTamamlandi=true HafifBakim() akisi icindir (zamanli veya elle baslatilmis); v54 Apps Script ayni POST icinde Log'a tamamlandi olayi ekler.</summary>
        public static void EnvanterGonder(AppSettings ayar, bool hafifBakimTamamlandi = false)
        {
            if (string.IsNullOrWhiteSpace(WebAppUrl)) return;
            try
            {
                var sis = SystemInfo.EnvanterAlanlari();
                string Al(string k) => sis.TryGetValue(k, out var v) ? v : "";
                string json = "{\"tip\":\"envanter\",\"token\":\"" + Kacir(ApiToken) + "\"," +
                    "\"musteriId\":\"" + Kacir(Musteri.MusteriId) + "\"," +
                    "\"machineId\":\"" + Kacir(ayar.MachineId) + "\"," +
                    "\"eskiMachineId\":\"" + Kacir(ayar.EskiMachineId) + "\"," + // v110
                    "\"bilgisayar\":\"" + Kacir(Environment.MachineName) + "\"," +
                    // v106 (Ali Bey'in istegi: "marka,model,seri numaralarini menuden alabiliyor
                    // muyuz" -- merkezi Filo Envanterinde gorunsun istedi):
                    "\"marka\":\"" + Kacir(Al("marka")) + "\"," +
                    "\"model\":\"" + Kacir(Al("model")) + "\"," +
                    "\"seri_no\":\"" + Kacir(Al("seri_no")) + "\"," +
                    "\"kullanici\":\"" + Kacir(Environment.UserName) + "\"," +
                    "\"mac\":\"" + Kacir(MacAdresi()) + "\"," +
                    "\"cpu\":\"" + Kacir(Al("cpu")) + "\"," +
                    "\"ram\":\"" + Kacir(Al("ram_gb")) + "\"," +
                    "\"disk_toplam\":\"" + Kacir(Al("disk_toplam_gb")) + "\"," +
                    "\"disk_bos\":\"" + Kacir(Al("disk_bos_gb")) + "\"," +
                    "\"windows\":\"" + Kacir(Al("windows")) + "\"," +
                    "\"lisans\":\"" + Kacir(Al("lisans")) + "\"," +
                    "\"surum\":\"" + Kacir(License.Surum) + "\"" +
                    (hafifBakimTamamlandi ? ",\"bakimTamamlandi\":true" : "") + "}";
                try { ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; } catch { }
                using (var wc = new ZamanAsimliWebClient())
                {
                    wc.Headers["Content-Type"] = "application/json";
                    string yanit = wc.UploadString(WebAppUrl, "POST", json);
                    SunucuYanitiKontrolEt("Envanter gonderimi", yanit, hafifBakimTamamlandi);
                }
            }
            catch (Exception ex) { Logger.WriteFileOnly("kurulum.txt", "Envanter gonderilemedi: " + ex.Message); }
        }

        /// <summary>v49: "Kurulumu tamamen kaldir" calistiginda cagrilir -- Sheet'teki satir
        /// SILINMEZ, "Durum" sutunu "KALDIRILDI" yapilir (bkz. Apps Script dokumantasyonu).
        /// Internet yoksa bildirim ulasmaz; bu durumda satir "AKTIF" gorunmeye devam eder,
        /// sonraki 7/30 gun kurallarinda "uzun suredir gorulmuyor" olarak fark edilir.</summary>
        public static void KaldirildiBildir(string machineId)
        {
            if (string.IsNullOrWhiteSpace(WebAppUrl) || string.IsNullOrWhiteSpace(machineId)) return;
            try
            {
                string json = "{\"tip\":\"kaldirildi\",\"token\":\"" + Kacir(ApiToken) + "\",\"musteriId\":\"" + Kacir(Musteri.MusteriId) + "\",\"machineId\":\"" + Kacir(machineId) + "\"}";
                try { ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; } catch { }
                using (var wc = new ZamanAsimliWebClient())
                {
                    wc.Headers["Content-Type"] = "application/json";
                    string yanit = wc.UploadString(WebAppUrl, "POST", json);
                    SunucuYanitiKontrolEt("Kaldirildi bildirimi", yanit);
                }
            }
            catch { } // Kaldirma islemi ZATEN sessiz-en-iyi-caba; burada da ayni.
        }

        /// <summary>Panelden merkezi Log sayfasini varsayilan tarayicida acar.
        /// v54 Apps Script'te gun=1 Istanbul takvim gununu, gun=7 bugun dahil son yedi
        /// takvim gununu ifade eder. 0/negatif ise son 200 kayit gosterilir.
        /// v52 duzeltmesi: Apps Script'in "Log" dalina (sifre sizintisi acigini kapatmak icin) token
        /// zorunlulugu eklendi, ama bu metot token GONDERMIYORDU -- "Merkezi kayit" dugmeleri bu yuzden
        /// "Yetkisiz: eksik veya hatali token" donduruyordu (Ali Bey'in bizzat test edip yakaladigi
        /// gercek bir regresyon). Simdi AcEnvanter() ile ayni sekilde token ekleniyor.</summary>
        public static string Ac(int gunGeriye = 0)
        {
            if (string.IsNullOrWhiteSpace(WebAppUrl))
                return "Drive log ozelligi henuz kurulmadi (Apps Script Web App URL'si bos). " +
                       "Kurulum adimlari gelistirme/AppsScript/DriveLog_v54.gs dosyasindadir.";
            try
            {
                string url = WebAppUrl + "?liste=log&token=" + ApiToken;
                if (gunGeriye > 0) url += "&gun=" + gunGeriye;
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
                return "Log sayfasi tarayicida aciliyor...";
            }
            catch (Exception ex) { return "Acilamadi: " + ex.Message; }
        }

        /// <summary>v55: Filo bazinda ENGELLENEN OYUNLAR raporu -- TUM bilgisayarlardan, merkezi
        /// Log sayfasindaki "oyun_engellendi" olaylarini (DriveLog_v55.gs'teki ?liste=oyunlar dali)
        /// yapilandirilmis (pipe ile ayrilmis) metin olarak ceker ve "tarih saat | oyun adi |
        /// bilgisayar" satirlari olarak dondurur (en yeni ustte). gunGeriye: 1=bugun (Istanbul
        /// takvim gunu), 7=son 7 gun, 0/negatif=son 200 kayit icindeki oyun olaylari.
        /// Ag hatasi, bos WebAppUrl veya eski (guncellenmemis) Apps Script durumunda BOS liste
        /// doner ve hata aciklamasi 'hata' parametresine yazilir -- hicbir zaman eski/yanlis
        /// veri gostermez.</summary>
        public static List<string> OyunRaporuGetir(int gunGeriye, out string hata)
        {
            hata = null;
            var satirlar = new List<string>();
            if (string.IsNullOrWhiteSpace(WebAppUrl))
            {
                hata = "Drive log ozelligi henuz kurulmadi (Apps Script Web App URL'si bos).";
                return satirlar;
            }
            try
            {
                string url = WebAppUrl + "?liste=oyunlar&token=" + Uri.EscapeDataString(ApiToken);
                if (gunGeriye > 0) url += "&gun=" + gunGeriye;
                try { ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; } catch { }
                string metin;
                using (var wc = new ZamanAsimliWebClient())
                {
                    wc.Headers["User-Agent"] = "UnsalBakim/1.0";
                    metin = wc.DownloadString(url);
                }
                if (metin != null && metin.IndexOf("Yetkisiz", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    hata = "Yetkisiz: token hatali ya da Apps Script eski surumde (bu rapor icin DriveLog_v55.gs yeniden dagitilmali).";
                    return satirlar;
                }
                foreach (var ham in (metin ?? "").Split('\n'))
                {
                    string s = ham.TrimEnd('\r');
                    if (s.Length == 0) continue;
                    var p = s.Split('|');
                    if (p.Length < 3) continue;
                    satirlar.Add(p[0] + "   |  " + p[1] + "   |  " + p[2]);
                }
            }
            catch (Exception ex) { hata = "Oyun raporu alinamadi: " + ex.Message; }
            return satirlar;
        }

        /// <summary>v74: Filo bazinda AI TESPITLERI raporu -- AI'nin kalici listeye ekledigi
        /// oyun/vpn/youtube-ai-site tespitlerini (YapayZeka.cs'teki DriveLog.Gonder("ai_tespit", ...)
        /// cagrilarindan, Apps Script'teki ?liste=aitespit dalindan) ceker. Satirlar: "makina  |
        /// tespit  |  tarih saat  |  kullanici" (en yeni ustte). gunGeriye: 7=haftalik (son 7 gun),
        /// 0/negatif=tumu (son 200 kayit). Ag hatasi, bos WebAppUrl veya eski Apps Script
        /// durumunda BOS liste doner, hata 'hata' parametresine yazilir.</summary>
        public static List<string> AiTespitRaporuGetir(int gunGeriye, out string hata)
        {
            hata = null;
            var satirlar = new List<string>();
            if (string.IsNullOrWhiteSpace(WebAppUrl))
            {
                hata = "Drive log ozelligi henuz kurulmadi (Apps Script Web App URL'si bos).";
                return satirlar;
            }
            try
            {
                string url = WebAppUrl + "?liste=aitespit&token=" + Uri.EscapeDataString(ApiToken);
                if (gunGeriye > 0) url += "&gun=" + gunGeriye;
                try { ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; } catch { }
                string metin;
                using (var wc = new ZamanAsimliWebClient())
                {
                    wc.Headers["User-Agent"] = "UnsalBakim/1.0";
                    metin = wc.DownloadString(url);
                }
                if (metin != null && metin.IndexOf("Yetkisiz", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    hata = "Yetkisiz: token hatali ya da Apps Script eski surumde (bu rapor icin DriveLog_v57.gs yeniden dagitilmali).";
                    return satirlar;
                }
                foreach (var ham in (metin ?? "").Split('\n'))
                {
                    string s = ham.TrimEnd('\r');
                    if (s.Length == 0) continue;
                    var p = s.Split('|');
                    if (p.Length < 4) continue;
                    satirlar.Add(p[0] + "   |  " + p[1] + "   |  " + p[2] + "   |  " + p[3]);
                }
            }
            catch (Exception ex) { hata = "AI tespit raporu alinamadi: " + ex.Message; }
            return satirlar;
        }

        /// <summary>v49: "Tum makineler - envanter listesi" dugmesi: Envanter sayfasini (Apps
        /// Script'teki ?liste=envanter dali) varsayilan tarayicida acar. Eski (guncellenmemis)
        /// Apps Script surumunde bu parametre taninmaz, eski Log davranisina duser -- yine de
        /// bir sey acilir, "beklenmedik bos sayfa" olmaz.</summary>
        /// <summary>v111 (Ali Bey'in istegi): Teshis sonuclarini + gonderilen istekleri tarayicida
        /// tablo olarak acar (Apps Script liste=teshis).</summary>
        public static string AcTeshis()
        {
            if (string.IsNullOrWhiteSpace(WebAppUrl))
                return "Drive log ozelligi henuz kurulmadi (Apps Script Web App URL'si bos).";
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(WebAppUrl + "?liste=teshis&token=" + ApiToken) { UseShellExecute = true });
                return "Teshis raporu tarayicida aciliyor...";
            }
            catch (Exception ex) { return "Acilamadi: " + ex.Message; }
        }

        public static string AcEnvanter()
        {
            if (string.IsNullOrWhiteSpace(WebAppUrl))
                return "Drive log ozelligi henuz kurulmadi (Apps Script Web App URL'si bos).";
            try
            {
                // v49 guvenlik duzeltmesi: token URL'ye eklenir -- Apps Script artik bunu zorunlu
                // kiliyor (yukaridaki doGet yorumuna bakin). Token olmadan sadece "Yetkisiz" metni doner.
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(WebAppUrl + "?liste=envanter&token=" + ApiToken) { UseShellExecute = true });
                return "Envanter listesi tarayicida aciliyor...";
            }
            catch (Exception ex) { return "Acilamadi: " + ex.Message; }
        }

        /// <summary>v70 (Ali Bey'in istegi: "yaziciya dokecek sekilde, gorunus formatina getir"):
        /// AYNI envanter verisini, yazdirmaya hazir (yatay sayfa, basliklar her sayfada tekrarlar,
        /// ekranda "Yazdir" dugmesi) bir goruntude acar -- yukardaki AcEnvanter() HICBIR SEKILDE
        /// degismedi, bu TAMAMEN AYRI/ek bir secenek (?liste=envanter&yazdir=1). Eski (henuz
        /// redeploy edilmemis) Apps Script'te yazdir parametresi taninmaz, normal goruntuye duser
        /// -- yine de bir sey acilir, hata vermez.</summary>
        public static string AcEnvanterYazdirma()
        {
            if (string.IsNullOrWhiteSpace(WebAppUrl))
                return "Drive log ozelligi henuz kurulmadi (Apps Script Web App URL'si bos).";
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(WebAppUrl + "?liste=envanter&yazdir=1&token=" + ApiToken) { UseShellExecute = true });
                return "Envanter yazdirma gorunumu tarayicida aciliyor... (sag ustteki 'Yazdir' dugmesine basin)";
            }
            catch (Exception ex) { return "Acilamadi: " + ex.Message; }
        }

        private static string MacAdresi()
        {
            // v110: once FIZIKSEL kart (VirtualBox/VPN/Bluetooth sanal kartlari haric).
            try { string fm = MakineKimlik.GosterilecekMac(); if (!string.IsNullOrEmpty(fm)) return fm; } catch { }
            try
            {
                foreach (var nic in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                    if (nic.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback) continue;
                    var b = nic.GetPhysicalAddress().GetAddressBytes();
                    if (b.Length == 0) continue;
                    return BitConverter.ToString(b).Replace("-", ":");
                }
            }
            catch { }
            return "bilinmiyor";
        }

        private static string Kacir(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
