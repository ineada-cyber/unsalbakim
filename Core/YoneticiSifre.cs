using System;
using System.Diagnostics;
using System.IO;
using System.Net;

namespace UnsalBakim.Core
{
    /// <summary>
    /// Drive'daki "yeni.txt"ye yazilan parolayi okuyup "yonetici" yerel hesabinin sifresini
    /// SYSTEM yetkisiyle (eski sifreyi bilmeye gerek olmadan) degistirir. Hesap yoksa olusturup
    /// Administrators grubuna ekler. Degisiklik oldugunda DriveLog'a (Google Sheet) SADECE
    /// "degisti, ne zaman, hangi hesap" bilgisini dusurur.
    ///
    /// v55 GUVENLIK DUZELTMESI: ONCEDEN burada "DriveLog'a eski/yeni parola duz metin yazilir"
    /// yaziyordu ve KontrolEtVeUygula() gercekten de parolanin kendisini Sheet'e gonderiyordu.
    /// Bu, Sheet'e erisen herkesin (link, ekran goruntusu, PDF) yonetici parolasini gorebilmesi
    /// demekti -- v53 oncesi butun kayitlarda gercek parolalar durmaktaydi. Artik parolanin
    /// KENDISI hicbir sekilde DriveLog'a gonderilmiyor; Sheet'te sadece "ne zaman degisti"
    /// goruluyor, parolanin kendisi SADECE bu makinede (SifreGizle ile hafif gizlenmis) tutuluyor.
    /// GECMIS KAYITLAR: v53 ve oncesi surumlerde Sheet'e yazilmis GERCEK parolalar olabilir --
    /// bu satirlar Sheet'ten elle temizlenmeli ve o parola(lar) artik GUVENLI SAYILMAMALI
    /// (degistirilmeli).
    ///
    /// Yerel "son uygulanan sifre" kaydi (Yetki.SifreGizle ile hafif gizlenmis, tam sifreleme
    /// degil -- ayni dosyadaki apikey.dat/kurulum.ini ile ayni seviye koruma) karsilastirma icin
    /// tutulur; boylece her saat ayni sifreyi tekrar tekrar uygulamaz, sadece GERCEKTEN
    /// degistiginde islem yapar.
    /// </summary>
    public static class YoneticiSifre
    {
        private const string Log = "yonetici_sifre.txt";
        private const string Hesap = "yonetici";

        // Ali Bey'in Drive'da olusturdugu "yeni.txt" dosyasinin ID'si (27.10.2026). Artik aktif.
        private const string YeniSifreDosyaId = "1jaT3WadEmKYJpUNy2g9fgIjJZrcMt2ZB";

        private static string IndirUrl(string id) => "https://drive.google.com/uc?export=download&id=" + id;

        // Varsayilan WebClient'in zaman asimi YOK -- ag/Drive yavas/yanitsiz kalirsa dugme/gorev
        // SURESIZ takilabiliyordu (bu v36 duzeltmesi). Sabit bir sure sonra hata firlatir.
        private sealed class ZamanAsimliWebClient : WebClient
        {
            protected override WebRequest GetWebRequest(Uri address)
            {
                var req = base.GetWebRequest(address);
                if (req != null) req.Timeout = 20000; // 20 sn
                return req;
            }
        }

        private static string SonSifreYolu =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "UnsalBakim", "son_yonetici_sifre.dat");

        /// <summary>Saatlik gorevden cagrilir. Drive'daki yeni.txt'yi okur; yerel kayittan
        /// farkliysa "yonetici" hesabina uygular ve Drive log'a eski->yeni satirini yazar.</summary>
        public static void KontrolEtVeUygula()
        {
            if (string.IsNullOrWhiteSpace(YeniSifreDosyaId)) return; // ozellik henuz kurulmadi

            string yeniSifre;
            try
            {
                try { ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; } catch { }
                using (var wc = new ZamanAsimliWebClient())
                {
                    wc.Headers["User-Agent"] = "UnsalBakim/1.0";
                    yeniSifre = wc.DownloadString(IndirUrl(YeniSifreDosyaId));
                }
            }
            catch (Exception ex)
            {
                Logger.WriteFileOnly(Log, "yeni.txt indirilemedi: " + ex.Message);
                return;
            }
            yeniSifre = (yeniSifre ?? "").Trim();
            if (yeniSifre.Length == 0) return;

            string eskiKayit = SonUygulananSifreOku();
            if (string.Equals(eskiKayit, yeniSifre, StringComparison.Ordinal))
            {
                // v38: daha once (v37 ve oncesi hatasi yuzunden) "uygulandi" diye yanlislikla
                // kaydedilmis olabilir -- hesap GERCEKTE yoksa yine de yeniden dene.
                HesapVarMi(Hesap, out bool halaVarMi);
                if (halaVarMi) return; // gercekten uygulanmis ve hesap mevcut, atla
                Logger.WriteFileOnly(Log, "[UYARI] Sifre daha once 'uygulandi' olarak isaretlenmisti ama '" + Hesap +
                    "' hesabi bu makinede yok; yeniden deneniyor.");
            }

            try
            {
                HesapVarMi(Hesap, out bool varMi);
                if (!varMi)
                {
                    // v38: ciktiyiLogla ARTIK true -- net.exe basarisiz olursa (ornegin yetki/UAC veya
                    // Windows parola politikasi) sebebi artik log'da [!] satiri olarak gorulur.
                    // (v37'ye kadar bu ciktilar sessizce atiliyordu, hicbir ipucu kalmiyordu.)
                    int kodEkle = NativeRunner.Run("net.exe", "user " + Hesap + " \"" + yeniSifre.Replace("\"", "") + "\" /add", Log, true, 20000);
                    int kodGrup = NativeRunner.Run("net.exe", "localgroup Administrators " + Hesap + " /add", Log, true, 20000);

                    // v38: cikis koduna KORU KORUNE guvenmek yerine hesabin GERCEKTEN olusup
                    // olusmadigini tekrar kontrol ediyoruz -- v37'ye kadar bu kontrol yoktu; net.exe
                    // sessizce basarisiz olsa bile islem "basarili" sayilip bir DAHA ASLA denenmiyordu
                    // (gercek sebep: bu, hesabin neden olusmadigini acikliyordu).
                    HesapVarMi(Hesap, out bool olustuMu);
                    if (!olustuMu)
                    {
                        Logger.WriteFileOnly(Log, "[HATA] '" + Hesap + "' hesabi OLUSTURULAMADI (net user /add cikis kodu: " + kodEkle +
                            "). Genelde yetki (UAC) veya Windows parola politikasi sorunudur; yukaridaki [!] " +
                            "satirlarina bakin. Bir sonraki saatlik kontrolde tekrar denenecek.");
                        return; // basarisiz -> son uygulanan sifre KAYDEDILMEZ, boylece tekrar denenir
                    }
                    Logger.WriteFileOnly(Log, "'" + Hesap + "' hesabi yoktu, olusturuldu ve Administrators'a eklendi " +
                        "(ekleme kodu:" + kodEkle + " grup kodu:" + kodGrup + ").");
                }
                else
                {
                    int kodSifre = NativeRunner.Run("net.exe", "user " + Hesap + " \"" + yeniSifre.Replace("\"", "") + "\"", Log, true, 20000);
                    if (kodSifre != 0)
                    {
                        Logger.WriteFileOnly(Log, "[HATA] '" + Hesap + "' sifresi degistirilemedi (cikis kodu: " + kodSifre +
                            "). Yukaridaki [!] satirlarina bakin. Bir sonraki saatlik kontrolde tekrar denenecek.");
                        return; // basarisiz -> son uygulanan sifre KAYDEDILMEZ, boylece tekrar denenir
                    }
                    Logger.WriteFileOnly(Log, "'" + Hesap + "' sifresi degistirildi (" + DateTime.Now + ").");
                }

                // v55 GUVENLIK DUZELTMESI: ONCEDEN burada gercek eski/yeni parola DUZ METIN olarak
                // merkezi Google Sheet'e (DriveLog.Gonder) gonderiliyordu -- Sheet'e erisen herkes
                // (paylasim linki, ekran goruntusu, PDF disa aktarma) yonetici parolasini
                // GOREBILIYORDU. Artik parolanin KENDISI hic gonderilmiyor; sadece olayin
                // GERCEKLESTIGI ve ne zaman oldugu kaydediliyor.
                try { DriveLog.Gonder("yonetici_sifre_degisti", "Hesap parolasi degistirildi (" + Hesap + "); sonuc=basarili."); } catch { }

                SonUygulananSifreYaz(yeniSifre);
            }
            catch (Exception ex)
            {
                Logger.WriteFileOnly(Log, "Sifre degistirme hatasi: " + ex.Message);
            }
        }

        private static void HesapVarMi(string hesap, out bool varMi)
        {
            int kod = NativeRunner.Run("net.exe", "user " + hesap, Log, false, 15000);
            varMi = (kod == 0);
        }

        private static string SonUygulananSifreOku()
        {
            try
            {
                if (!File.Exists(SonSifreYolu)) return null;
                string satir = File.ReadAllText(SonSifreYolu).Trim();
                int i = satir.IndexOf('|');
                string gizli = i >= 0 ? satir.Substring(i + 1) : satir;
                return Yetki.SifreCoz(gizli);
            }
            catch { return null; }
        }

        private static DateTime? SonUygulamaZamani()
        {
            try
            {
                if (!File.Exists(SonSifreYolu)) return null;
                string satir = File.ReadAllText(SonSifreYolu).Trim();
                int i = satir.IndexOf('|');
                if (i < 0) return null;
                DateTime t;
                return DateTime.TryParse(satir.Substring(0, i), out t) ? (DateTime?)t : null;
            }
            catch { return null; }
        }

        private static void SonUygulananSifreYaz(string acikSifre)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SonSifreYolu));
                File.WriteAllText(SonSifreYolu, DateTime.Now.ToString("o") + "|" + Yetki.SifreGizle(acikSifre));
            }
            catch { }
        }

        /// <summary>Panelden "Yonetici hesabi durumu" dugmesi icin: hesap var mi, Administrators'ta mi,
        /// sifre en son NE ZAMAN senkronize edildi (sifrenin kendisi GOSTERILMEZ -- gizli kalir).</summary>
        public static string Durum()
        {
            if (string.IsNullOrWhiteSpace(YeniSifreDosyaId))
                return "Ozellik kurulmadi (Drive'da yeni.txt henuz tanimlanmamis).";

            string kullaniciCikti = KomutCiktisi("net.exe", "user " + Hesap);
            bool hesapVar = kullaniciCikti != null;
            string grupCikti = KomutCiktisi("net.exe", "localgroup Administrators");
            bool adminMi = hesapVar && grupCikti != null &&
                grupCikti.IndexOf(Hesap, StringComparison.OrdinalIgnoreCase) >= 0;

            var zaman = SonUygulamaZamani();
            string sifreDurum = zaman.HasValue
                ? "Son sifre senkronizasyonu: " + zaman.Value.ToString("dd.MM.yyyy HH:mm") + " (sifrenin kendisi v55'ten itibaren hicbir yere -- Drive log Sheet'ine dahi -- gonderilmez)."
                : "Bu makinede henuz hic sifre senkronizasyonu yapilmamis (saatlik gorev calismamis veya yeni.txt ile ayni sifre zaten uygulanmis olabilir).";

            return
                "'" + Hesap + "' hesabi: " + (hesapVar ? "VAR" : "YOK (henuz ilk saatlik kontrol calismamis olabilir)") + "\r\n" +
                "Administrators grubunda: " + (adminMi ? "EVET" : "HAYIR") + "\r\n" +
                sifreDurum;
        }

        private static string KomutCiktisi(string exe, string args)
        {
            try
            {
                var psi = new ProcessStartInfo(exe, args)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (var p = Process.Start(psi))
                {
                    string cikti = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(15000);
                    return p.ExitCode == 0 ? cikti : null;
                }
            }
            catch { return null; }
        }
    }
}
