using System;
using System.IO;
using System.Net;

namespace UnsalBakim.Core
{
    /// <summary>
    /// v94 (Ali Bey'in istegi: "Filo'dan dosya gonderebilir miyiz, FTP gibi"): "Filo'dan Kapat"
    /// ile BIREBIR AYNI TEK-SEFERLIK komut deseni (bkz. FiloKapat.cs'in basindaki tasarim notu).
    ///
    /// TASARIM (Ali Bey'in kendi onerisi): gercek bir FTP sunucusu YOK. Bunun yerine, Drive'da
    /// 6 SABIT dosya var (yukle.pdf/txt/jpg/docx/exe/zip -- Musteri.cs'teki DosyaXxxId sabitleri,
    /// guncelleme.zip ile AYNI mantik). Ali Bey gondermek istedigi dosyayi, TURUNE uyan sabit
    /// dosyanin UZERINE "surum olarak" yukler (silip yeniden yuklemez -- aksi halde ZipDosyaId'de
    /// yasanan 404 hatasi burada da tekrarlanir), sonra panelden Tur/HedefKlasor/DosyaAdi secip
    /// "Gonder" der. Google Sheet'ten SADECE bu 3 kucuk alan + komut zamani gecer -- dosyanin
    /// kendisi HER ZAMAN dogrudan Drive'dan indirilir (Sheet hucre boyutu limitleri yuzunden
    /// gercek dosya icerigi Sheet'e asla sigdirilamaz).
    /// </summary>
    public static class FiloDosyaGonder
    {
        private const string Log = "filo_dosya_gonder.txt";

        private static string OnbellekYolu =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "UnsalBakim", "dosya_son_uygulanan.txt");

        private sealed class ZamanAsimliWebClient : WebClient
        {
            protected override WebRequest GetWebRequest(Uri address)
            {
                var req = base.GetWebRequest(address);
                if (req != null) req.Timeout = 10000;
                return req;
            }
        }

        /// <summary>PANELDEN cagrilir: hedefteki makine(ler)e yeni bir "dosya indir" komutu
        /// gonderir. tur: "pdf"/"txt"/"jpg"/"docx"/"exe"/"zip" (Musteri.cs'teki sabitlerle eslesir).</summary>
        public static string FiloBaslat(string hedef, string tur, string hedefKlasor, string dosyaAdi)
        {
            if (string.IsNullOrWhiteSpace(Musteri.WebAppUrl))
                return "Drive log ozelligi kurulmadi (WebAppUrl bos); yazilamadi.";
            if (string.IsNullOrWhiteSpace(DosyaIdSec(tur)))
                return "Bu tur (" + tur + ") icin Drive dosya ID'si henuz tanimlanmamis (Musteri.cs'te DosyaXxxId bos) -- once Ali Bey'in Drive'da ilgili yukle.* dosyasini olusturup ID'sini kodda ayarlamasi gerekir.";
            if (string.IsNullOrWhiteSpace(hedefKlasor) || string.IsNullOrWhiteSpace(dosyaAdi))
                return "Hedef klasor ve dosya adi bos birakilamaz.";
            try
            {
                string zaman = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                string json = "{\"tip\":\"dosya\",\"token\":\"" + Kacir(Musteri.ApiToken) + "\",\"musteriId\":\"" +
                              Kacir(Musteri.MusteriId) + "\",\"hedef\":\"" + Kacir(string.IsNullOrWhiteSpace(hedef) ? "TUMU" : hedef.Trim()) +
                              "\",\"tur\":\"" + Kacir(tur) + "\",\"hedefKlasor\":\"" + Kacir(hedefKlasor.Trim()) +
                              "\",\"dosyaAdi\":\"" + Kacir(dosyaAdi.Trim()) + "\",\"zaman\":\"" + zaman +
                              "\",\"guncelleyen\":\"" + Kacir(Environment.UserName + "@" + Environment.MachineName) + "\"}";
                try { ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; } catch { }
                string yanit;
                using (var wc = new ZamanAsimliWebClient())
                {
                    wc.Headers["Content-Type"] = "application/json";
                    yanit = wc.UploadString(Musteri.WebAppUrl, "POST", json);
                }
                if (yanit != null && yanit.Trim() == "OK")
                    return "Dosya gonderme komutu gonderildi (hedef: " + hedef + ", tur: " + tur + "). Hedefteki makine(ler) en gec ~5 dakika " +
                           "icinde '" + hedefKlasor.Trim() + "' klasorune '" + dosyaAdi.Trim() + "' olarak indirecek.";
                return "Yazilamadi, Apps Script yaniti: '" + yanit + "' (script guncel olmayabilir).";
            }
            catch (Exception ex) { return "Yazilamadi: " + ex.Message; }
        }

        /// <summary>v57 (3. revizyon) deseniyle AYNI: FiloDurum.Al'in birlesik istegiyle ONCEDEN
        /// alinmis degerler uzerinde calisir -- kendi ag istegini atmaz.</summary>
        public static void FiloKontrolEtVeUygulaDeger(FiloDurumToplu toplu)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(toplu.DosyaZaman)) return;

                string yerelZaman = OnbellekOku();
                if (string.Equals(toplu.DosyaZaman, yerelZaman, StringComparison.Ordinal)) return; // zaten uygulandi

                string dosyaId = DosyaIdSec(toplu.DosyaTur);
                if (string.IsNullOrWhiteSpace(dosyaId))
                {
                    Logger.Write(Log, "Dosya komutu alindi ama tur (" + toplu.DosyaTur + ") icin Drive ID tanimli degil -- atlaniyor.");
                    OnbellekYaz(toplu.DosyaZaman); // ayni tanimsiz komut icin tekrar tekrar denemesin
                    return;
                }
                if (string.IsNullOrWhiteSpace(toplu.DosyaHedefKlasor) || string.IsNullOrWhiteSpace(toplu.DosyaDosyaAdi))
                {
                    Logger.Write(Log, "Dosya komutu alindi ama hedef klasor/dosya adi eksik -- atlaniyor.");
                    OnbellekYaz(toplu.DosyaZaman);
                    return;
                }

                // v113 (GUVENLIK): hedef klasor/dosya adi Sheet'ten (sunucudan) geliyor -- dogrudan
                // guvenilmez girdi. Once "uygulandi" damgasini yazmadan ONCE yol guvenligini dogrula;
                // gecersizse hicbir sey indirilmez/yazilmaz, sadece loglanir.
                string guvenlikHata;
                string hedefYol;
                if (!HedefGuvenliMi(toplu.DosyaHedefKlasor, toplu.DosyaDosyaAdi, out hedefYol, out guvenlikHata))
                {
                    Logger.WriteFileOnly(Log, "[GUVENLIK] Dosya komutu reddedildi (" + toplu.DosyaZaman + ") -- " + guvenlikHata);
                    OnbellekYaz(toplu.DosyaZaman); // ayni gecersiz komut icin tekrar tekrar denemesin
                    return;
                }

                // Once "uygulandi" damgasini yaz (indirme sirasinda/sonrasinda tekrar denemeyi
                // onlemek icin) -- sonra indir. (FiloKapat.cs ile AYNI sira/mantik.)
                OnbellekYaz(toplu.DosyaZaman);
                Logger.Write(Log, "Filo dosya komutu alindi (" + toplu.DosyaZaman + ", tur=" + toplu.DosyaTur +
                                  ") -- '" + hedefYol + "' olarak indiriliyor.");

                byte[] veri;
                try { ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; } catch { }
                using (var wc = new ZamanAsimliWebClient())
                {
                    veri = wc.DownloadData("https://drive.google.com/uc?export=download&id=" + dosyaId);
                }
                Directory.CreateDirectory(Path.GetDirectoryName(hedefYol));
                File.WriteAllBytes(hedefYol, veri);
                Logger.Write(Log, "Dosya basariyla indirildi: '" + hedefYol + "' (" + veri.Length + " bayt).");
            }
            catch (Exception ex) { Logger.WriteFileOnly(Log, "Filo dosya kontrolu/indirme hatasi: " + ex.Message); }
        }

        /// <summary>v113 (GUVENLIK DUZELTMESI): toplu.DosyaHedefKlasor/DosyaDosyaAdi Google Sheet'ten
        /// gelir -- yani bu makineyi yoneten kisiden DEGIL, Sheet'i (veya Apps Script yanitini)
        /// degistirebilen HERHANGI BIRINDEN. Once bu alanlar hic dogrulanmadan Directory.CreateDirectory/
        /// Path.Combine/File.WriteAllBytes'a gidiyordu -- "../../" ile yol kacisi veya Baslangic
        /// (Startup) klasoru gibi bir hedef verilerek, 6 sabit Drive dosyasindan biri (orn. "exe" turu)
        /// icerik olarak kullanilip, makineye baska bir kullanicinin oturum acmasinda calisacak bir
        /// dosya birakilabilirdi. Bu metot: (1) dosya adinin yol ayraci/".." icermeden SADECE bir dosya
        /// adi olmasini, (2) hedef klasorun bir ag paylasimi (UNC) olmamasini, (3) hedef klasorun
        /// Windows/Program Files/Baslangic (Startup, hem kullanici hem TUM KULLANICILAR) gibi otomatik
        /// calistirma/sistem konumlarindan biri OLMAMASINI zorunlu kilar. "exe" turunun kendisini
        /// yasaklamiyoruz (Ali Bey'in bilerek tasarladigi, amaclanan bir kullanim -- bkz. yukarida
        /// dosya basindaki TASARIM notu); sadece NEREYE yazilabilecegini sinirliyoruz.</summary>
        private static bool HedefGuvenliMi(string klasorHam, string dosyaAdiHam, out string hedefYol, out string neden)
        {
            hedefYol = null;
            neden = null;

            string dosyaAdi = (dosyaAdiHam ?? "").Trim();
            if (dosyaAdi.Length == 0 ||
                dosyaAdi.IndexOfAny(new[] { '\\', '/', ':' }) >= 0 ||
                dosyaAdi.Contains("..") ||
                dosyaAdi.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                !string.Equals(dosyaAdi, Path.GetFileName(dosyaAdi), StringComparison.Ordinal))
            {
                neden = "dosya adi gecersiz (sadece tek bir dosya adi olmali, yol ayraci/'..' icermemeli): '" + dosyaAdiHam + "'";
                return false;
            }

            string klasorGiris = (klasorHam ?? "").Trim();
            if (klasorGiris.Length == 0 || klasorGiris.StartsWith("\\\\") || klasorGiris.StartsWith("//"))
            {
                neden = "hedef klasor bos olamaz veya bir ag paylasimi (UNC) olamaz: '" + klasorHam + "'";
                return false;
            }

            string tamKlasor;
            try { tamKlasor = Path.GetFullPath(klasorGiris); }
            catch (Exception ex)
            {
                neden = "hedef klasor cozumlenemedi: " + ex.Message;
                return false;
            }

            string tamKlasorUst = tamKlasor.TrimEnd('\\').ToUpperInvariant();

            string[] yasakliKokler;
            try
            {
                yasakliKokler = new[]
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    Environment.GetFolderPath(Environment.SpecialFolder.System),
                    Environment.GetFolderPath(Environment.SpecialFolder.SystemX86),
                    Environment.GetFolderPath(Environment.SpecialFolder.Startup),      // kullanicinin Baslangic'i
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup) // TUM KULLANICILAR Baslangic'i
                };
            }
            catch { yasakliKokler = new string[0]; }

            foreach (var kokHam in yasakliKokler)
            {
                if (string.IsNullOrWhiteSpace(kokHam)) continue;
                string kok = kokHam.TrimEnd('\\').ToUpperInvariant();
                if (kok.Length == 0) continue;
                if (tamKlasorUst == kok || tamKlasorUst.StartsWith(kok + "\\"))
                {
                    neden = "hedef klasor izin verilmeyen bir sistem/otomatik-calistirma konumu (" + kokHam + ") altinda: '" + tamKlasor + "'";
                    return false;
                }
            }
            // Baslangic klasoru farkli kullanicilarda farkli profil yollarinda olabilir (SpecialFolder
            // sadece BU oturumun kullanicisininkini doner) -- yol icinde gecen "\STARTUP\" segmentini
            // de ayrica reddederek diger kullanicilarin Baslangic klasorlerini de kapsa.
            if (tamKlasorUst.Contains("\\STARTUP\\") || tamKlasorUst.EndsWith("\\STARTUP"))
            {
                neden = "hedef klasor bir Baslangic (Startup) klasoru gibi gorunuyor: '" + tamKlasor + "'";
                return false;
            }

            hedefYol = Path.Combine(tamKlasor, dosyaAdi);
            return true;
        }

        private static string DosyaIdSec(string tur)
        {
            switch ((tur ?? "").Trim().ToLowerInvariant())
            {
                case "pdf": return Musteri.DosyaPdfId;
                case "txt": return Musteri.DosyaTxtId;
                case "jpg": return Musteri.DosyaJpgId;
                case "docx": return Musteri.DosyaDocxId;
                case "exe": return Musteri.DosyaExeId;
                case "zip": return Musteri.DosyaZipId;
                default: return null;
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
