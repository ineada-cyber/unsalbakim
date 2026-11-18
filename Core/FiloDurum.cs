using System;
using System.Net;

namespace UnsalBakim.Core
{
    /// <summary>Politika + Sinav + Kapat'in UCUNUN de BIRLESIK sonucu -- tek bir ag istegiyle alinir.</summary>
    public struct FiloDurumToplu
    {
        public FiloAyarlari Politika;
        public DateTime? SinavBitis;
        public string KapatZaman;
        /// <summary>v80: "Envanter gonder" -- Kapat ile AYNI tek-seferlik komut zamani (4. satir).</summary>
        public string EnvanterIsteZaman;
        /// <summary>v89: "Yeniden baslat" -- Kapat ile AYNI tek-seferlik komut zamani (5. satir).</summary>
        public string YenidenBaslatZaman;
        /// <summary>v94: "Dosya gonder" -- Kapat ile AYNI tek-seferlik komut deseni, 4 alanli
        /// pipe-string olarak gelir (6. satir): Tur|HedefKlasor|DosyaAdi|Zaman.</summary>
        public string DosyaTur;
        public string DosyaHedefKlasor;
        public string DosyaDosyaAdi;
        public string DosyaZaman;
        /// <summary>v94: "Buyuk ekran mesaji" -- Kapat ile AYNI tek-seferlik komut deseni, 2 alanli
        /// pipe-string olarak gelir (7. satir): Zaman|MesajMetni.</summary>
        public string MesajZaman;
        public string MesajMetni;
        /// <summary>v110: Teshis (8. satir): Zaman|sorgu1,sorgu2 -- zaman dogrulanmissa dolu.</summary>
        public string TeshisZaman;
        public string TeshisSorgular;
        /// <summary>v110: Pilot guncelleme (9. satir): "" (kisit yok) / "IZIN" / "BEKLE:n".</summary>
        public string PilotDurum;
        /// <summary>v110: Koruma acil anahtari (10. satir) = "KAPALI" mi.</summary>
        public bool KorumaKapali;
        /// <summary>v116: Hafif/Agir Bakim (11./12. satir) -- Kapat/YenidenBaslat ile AYNI
        /// tek-seferlik komut zamani deseni. OGRETMEN muafiyeti YOKTUR (Ali Bey'in acik istegi:
        /// "ogretmen ve ogrenci icin gecerli olmali") -- bkz. FiloBakim.cs.</summary>
        public string HafifBakimZaman;
        public string AgirBakimZaman;
        /// <summary>v110: Bu turda sunucudan GECERLI bir yanit alindi mi (false = onbellek/ag yok).
        /// Kalici durumlar (koruma anahtari) SADECE true iken degistirilir.</summary>
        public bool AgBasarili;
        /// <summary>v110: Yanit 10 satirli (v110 Apps Script) mi -- degilse 8-10. satirlar hic yok.</summary>
        public bool V110Yanit;
        /// <summary>v116: Yanit 12 satirli (v116 Apps Script) mi -- degilse 11-12. satirlar hic yok.</summary>
        public bool V116Yanit;
    }

    /// <summary>
    /// v57 (3. revizyon): Politika + Sinav + Kapat icin AYRI AYRI 3 ag istegi (?liste=politika,
    /// ?liste=sinav, ?liste=kapat) atmak yerine, TEK bir birlesik istek (?liste=topludurum) ile
    /// ucunu de birden ceker.
    ///
    /// NEDEN: Ali Bey'in "lab sayisi buyurse ne yapariz" sorusuna yanit. Google Apps Script'in
    /// web app cagrilari icin GUNLUK bir kotasi YOK (bu, zamanli ic tetikleyiciler icin gecerli,
    /// bizim durumumuzla ilgisiz) -- gercek sinir hesap basina "ES ZAMANLI calistirma" (30).
    /// Fleet buyudukce (daha fazla lab/makine), ayni anda sunucuya vuran istek sayisi da buyur;
    /// kontrol araligini uzatmak (5dk -> 10dk) bunu SADECE yavaslatir, kalici bir cozum degildir
    /// -- gercek, olcekle BAGIMSIZ kazanc, her dongude atilan istek SAYISINI azaltmaktir. Bu
    /// sinif, 3 istegi 1'e dusurerek es-zamanli carpisma riskini (kac lab/makine olursa olsun)
    /// 3'e 1 kucultur.
    ///
    /// Yanit formati (3 satir, '\n' ile ayrilmis): 1) politika (FiloPolitika.Coz ile AYNI 6
    /// alanli pipe-string), 2) sinav bitis zamani (veya bos), 3) kapat komut zamani (veya bos).
    /// Herhangi bir satirin bu makinenin Hedef'ine UYMAMASI (veya kayit olmamasi) durumunda o
    /// satir BOS gelir -- ilgili alt sistem bunu "zorlama/komut yok" olarak yorumlar (eskisiyle
    /// AYNI davranis, sadece ucu TEK istekte).
    ///
    /// APPS SCRIPT EKI: DriveLog_v57.gs'teki 'topludurum' doGet dalina bakin.
    /// </summary>
    public static class FiloDurum
    {
        private const string Log = "filo_durum_toplu.txt";

        private sealed class ZamanAsimliWebClient : WebClient
        {
            protected override WebRequest GetWebRequest(Uri address)
            {
                var req = base.GetWebRequest(address);
                if (req != null) req.Timeout = 10000;
                return req;
            }
        }

        /// <summary>5 dk'lik (eski: 10 dk) birlesik denetim dongusunden cagrilir. Basarili olursa
        /// UCUNUN de sonucunu doner VE Politika/Sinav'in kendi yerel onbeleklerini TAZELER (Kapat
        /// icin ayri bir onbellek kavrami yok -- komut zamani doğrudan FiloKapat'a iletilir).
        /// Basarisiz olursa (ag/Apps Script sorunu) Politika ve Sinav kendi yerel onbekleklerine
        /// duser, Kapat icin ise "bu turda yeni bir komut yok" (null) donulur -- bir ag sorunu
        /// hicbir makineyi yanlislikla kapatmaz/zorla engellemez.</summary>
        public static FiloDurumToplu Al(bool agKontrolEt)
        {
            if (!agKontrolEt || string.IsNullOrWhiteSpace(Musteri.WebAppUrl))
                return OnbellektenDerle();

            try
            {
                try { ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; } catch { }
                string metin;
                using (var wc = new ZamanAsimliWebClient())
                {
                    wc.Headers["User-Agent"] = "UnsalBakim/1.0";
                    string url = Musteri.WebAppUrl + "?liste=topludurum&token=" + Uri.EscapeDataString(Musteri.ApiToken) +
                                 "&musteriId=" + Uri.EscapeDataString(Musteri.MusteriId) +
                                 "&bilgisayarAdi=" + Uri.EscapeDataString(Environment.MachineName);
                    metin = wc.DownloadString(url);
                }
                var satirlar = (metin ?? "").Replace("\r\n", "\n").Split('\n');
                string politikaMetin = satirlar.Length > 0 ? satirlar[0] : "";
                // Toplu protokolun ilk satiri EN AZ 6 alanlidir (5 adet '|' -- eski Apps Script),
                // v69'dan itibaren GUNCEL script 9 alan (8 '|') doner (AiHaftalik/AiGun/AiSaat
                // eklendi). HTML giris sayfasi, Google hata metni veya yarim yanit asla bos komut
                // gibi yorumlanmaz. Bu kontrol, beklenmeyen cevap geldiginde onceki politika/sinav
                // onbelleklerini korur.
                // DUZELTME (v85, 6 Kasim 2026 -- canli ortamda yakalandi): kontrol onceden "ayiracSayisi
                // != 5" idi, yani v69'dan beri guncel script'in dondugu GECERLI 8-'|' cevabi HER
                // ZAMAN "format hatali" sayilip REDDEDILIYORDU (Politika/Sinav/Kapat/Envanter-iste
                // hicbiri hic uygulanamiyordu -- script guncel olsa bile). Artik EN AZ 5 '|' yeterli;
                // eski VE yeni script formatlari da kabul edilir, sadece gercekten bozuk/HTML cevaplar reddedilir.
                int ayiracSayisi = 0;
                foreach (char ch in politikaMetin) if (ch == '|') ayiracSayisi++;
                if (satirlar.Length < 3 || ayiracSayisi < 5 || politikaMetin.IndexOf('<') >= 0 || politikaMetin.IndexOf('>') >= 0)
                    throw new FormatException("Toplu filo yaniti beklenen 3 satirli/en az 6 alanli protokole uymuyor; onbellek korunacak. Ilk satir: " +
                        (politikaMetin.Length > 100 ? politikaMetin.Substring(0, 100) : politikaMetin));
                string sinavMetin = satirlar.Length > 1 ? satirlar[1] : "";
                string kapatMetin = satirlar.Length > 2 ? satirlar[2] : "";
                // v80: "Envanter gonder" 4. satir olarak EKLENDI -- eski (henuz yeniden dagitilmamis)
                // Apps Script hala 3 satir donuyorsa bu satir BOS sayilir (hicbir yeni komut yok),
                // HATA FIRLATILMAZ (geriye donuk uyumluluk).
                string envanterIsteMetin = satirlar.Length > 3 ? satirlar[3] : "";
                // v89: "Yeniden baslat" 5. satir olarak EKLENDI -- eski (henuz yeniden dagitilmamis)
                // Apps Script hala 4 satir donuyorsa bu satir BOS sayilir (hicbir yeni komut yok),
                // HATA FIRLATILMAZ (Envanter-iste ile AYNI geriye donuk uyumluluk deseni).
                string yenidenBaslatMetin = satirlar.Length > 4 ? satirlar[4] : "";
                // v94: Dosya (6. satir) ve Mesaj (7. satir) -- eski (henuz yeniden dagitilmamis)
                // Apps Script bunlari hic dondurmuyorsa BOS sayilir, HATA FIRLATILMAZ (ayni geriye
                // donuk uyumluluk deseni).
                string dosyaMetin = satirlar.Length > 5 ? satirlar[5] : "";
                string mesajMetinSatiri = satirlar.Length > 6 ? satirlar[6] : "";
                // v110: Teshis (8.), Pilot guncelleme (9.), Koruma acil (10.) -- eski Apps Script
                // bunlari dondurmez; o zaman BOS sayilir (hicbir yeni komut / kisitlama yok).
                bool v110Yanit = satirlar.Length >= 10;
                string teshisSatiri = satirlar.Length > 7 ? satirlar[7] : "";
                string pilotSatiri = satirlar.Length > 8 ? satirlar[8].Trim() : "";
                string korumaSatiri = satirlar.Length > 9 ? satirlar[9].Trim() : "";
                // v116: Hafif Bakim (11.), Agir Bakim (12.) -- eski Apps Script bunlari dondurmez;
                // o zaman BOS sayilir (hicbir yeni bakim komutu yok), HATA FIRLATILMAZ.
                bool v116Yanit = satirlar.Length >= 12;
                string hafifBakimMetin = satirlar.Length > 10 ? satirlar[10].Trim() : "";
                string agirBakimMetin = satirlar.Length > 11 ? satirlar[11].Trim() : "";
                var teshisAlan = (teshisSatiri ?? "").Split(new[] { '|' }, 2);
                string teshisZamanGecerli = GecerliKapatZamaniMi(teshisAlan[0]) ? teshisAlan[0].Trim() : null;
                if (pilotSatiri != "" && pilotSatiri != "IZIN" && !System.Text.RegularExpressions.Regex.IsMatch(pilotSatiri, "^BEKLE:[0-9]{0,6}$"))
                {
                    Logger.WriteFileOnly(Log, "Pilot alaninda tanimsiz icerik goz ardi edildi: '" + pilotSatiri + "'");
                    pilotSatiri = "";
                }
                if (v110Yanit) GuncellemeKontrol.PilotDurumYaz(pilotSatiri);

                // v57 hata duzeltmesi: Apps Script'ten beklenen 3 satirli yanit yerine bir HATA/
                // oturum-acma HTML sayfasi donerse (ornek: dagitim guncel degil, yetkisiz istek,
                // gecici Google hatasi), bu HTML'in 3. satiri ("<head>" gibi) kapatMetin'e dusup
                // -- onbellekteki degerden FARKLI oldugu icin -- "YENI komut" sanilip makine
                // GERCEKTEN KAPATILIYORDU (bir ogretmen test sirasinda canli yakaladi). Artik
                // kapatMetin, FiloBaslat'in yazdigi "yyyy-MM-dd HH:mm:ss.fff" bicimine uymuyorsa
                // GECERSIZ sayilir ve hicbir komut yokmus gibi davranilir (kapanma tetiklenmez).
                string kapatGecerli = GecerliKapatZamaniMi(kapatMetin) ? kapatMetin.Trim() : null;
                if (kapatGecerli == null && !string.IsNullOrWhiteSpace(kapatMetin))
                    Logger.WriteFileOnly(Log, "Kapat alaninda tanimsiz/gecersiz icerik goz ardi edildi (HTML hata sayfasi olabilir): '" + kapatMetin.Trim() + "'");

                // v80: Envanter-iste zamani da KAPAT ile AYNI bicimde dogrulanir (HTML hata sayfasi
                // asla "yeni komut" sayilmaz).
                string envanterIsteGecerli = GecerliKapatZamaniMi(envanterIsteMetin) ? envanterIsteMetin.Trim() : null;
                if (envanterIsteGecerli == null && !string.IsNullOrWhiteSpace(envanterIsteMetin))
                    Logger.WriteFileOnly(Log, "Envanter-iste alaninda tanimsiz/gecersiz icerik goz ardi edildi (HTML hata sayfasi olabilir): '" + envanterIsteMetin.Trim() + "'");

                // v89: Yeniden-baslat zamani da KAPAT ile AYNI bicimde dogrulanir (HTML hata sayfasi
                // asla "yeni komut" sayilmaz).
                string yenidenBaslatGecerli = GecerliKapatZamaniMi(yenidenBaslatMetin) ? yenidenBaslatMetin.Trim() : null;
                if (yenidenBaslatGecerli == null && !string.IsNullOrWhiteSpace(yenidenBaslatMetin))
                    Logger.WriteFileOnly(Log, "Yeniden-baslat alaninda tanimsiz/gecersiz icerik goz ardi edildi (HTML hata sayfasi olabilir): '" + yenidenBaslatMetin.Trim() + "'");

                // v94: Dosya satiri 4 alanli (Tur|HedefKlasor|DosyaAdi|Zaman) -- Zaman alani KAPAT
                // ile AYNI bicimde dogrulanir; gecersizse TUM dosya komutu yok sayilir (eksik/bozuk
                // bir dosya komutuyla yanlislikla indirme yapilmaz).
                var dosyaAlan = (dosyaMetin ?? "").Split('|');
                string dosyaZamanHam = dosyaAlan.Length > 3 ? dosyaAlan[3] : "";
                string dosyaZamanGecerli = GecerliKapatZamaniMi(dosyaZamanHam) ? dosyaZamanHam.Trim() : null;
                if (dosyaZamanGecerli == null && !string.IsNullOrWhiteSpace(dosyaZamanHam))
                    Logger.WriteFileOnly(Log, "Dosya alaninda tanimsiz/gecersiz icerik goz ardi edildi (HTML hata sayfasi olabilir): '" + dosyaZamanHam.Trim() + "'");

                // v94: Mesaj satiri 2 alanli (Zaman|MesajMetni) -- Zaman alani AYNI sekilde
                // dogrulanir.
                var mesajAlan = (mesajMetinSatiri ?? "").Split(new[] { '|' }, 2);
                string mesajZamanHam = mesajAlan.Length > 0 ? mesajAlan[0] : "";
                string mesajZamanGecerli = GecerliKapatZamaniMi(mesajZamanHam) ? mesajZamanHam.Trim() : null;
                if (mesajZamanGecerli == null && !string.IsNullOrWhiteSpace(mesajZamanHam))
                    Logger.WriteFileOnly(Log, "Mesaj alaninda tanimsiz/gecersiz icerik goz ardi edildi (HTML hata sayfasi olabilir): '" + mesajZamanHam.Trim() + "'");

                // v116: Hafif/Agir Bakim zamanlari da KAPAT ile AYNI bicimde dogrulanir (HTML hata
                // sayfasi asla "yeni komut" sayilmaz).
                string hafifBakimGecerli = GecerliKapatZamaniMi(hafifBakimMetin) ? hafifBakimMetin.Trim() : null;
                if (hafifBakimGecerli == null && !string.IsNullOrWhiteSpace(hafifBakimMetin))
                    Logger.WriteFileOnly(Log, "Hafif bakim alaninda tanimsiz/gecersiz icerik goz ardi edildi (HTML hata sayfasi olabilir): '" + hafifBakimMetin.Trim() + "'");
                string agirBakimGecerli = GecerliKapatZamaniMi(agirBakimMetin) ? agirBakimMetin.Trim() : null;
                if (agirBakimGecerli == null && !string.IsNullOrWhiteSpace(agirBakimMetin))
                    Logger.WriteFileOnly(Log, "Agir bakim alaninda tanimsiz/gecersiz icerik goz ardi edildi (HTML hata sayfasi olabilir): '" + agirBakimMetin.Trim() + "'");

                return new FiloDurumToplu
                {
                    Politika = FiloPolitika.CozVeOnbellekYaz(politikaMetin),
                    SinavBitis = InternetEngeli.CozVeOnbellekYaz(sinavMetin),
                    KapatZaman = kapatGecerli,
                    EnvanterIsteZaman = envanterIsteGecerli,
                    YenidenBaslatZaman = yenidenBaslatGecerli,
                    DosyaTur = dosyaZamanGecerli != null && dosyaAlan.Length > 0 ? dosyaAlan[0] : null,
                    DosyaHedefKlasor = dosyaZamanGecerli != null && dosyaAlan.Length > 1 ? dosyaAlan[1] : null,
                    DosyaDosyaAdi = dosyaZamanGecerli != null && dosyaAlan.Length > 2 ? dosyaAlan[2] : null,
                    DosyaZaman = dosyaZamanGecerli,
                    MesajZaman = mesajZamanGecerli,
                    MesajMetni = mesajZamanGecerli != null && mesajAlan.Length > 1 ? mesajAlan[1] : null,
                    TeshisZaman = teshisZamanGecerli,
                    TeshisSorgular = teshisZamanGecerli != null && teshisAlan.Length > 1 ? teshisAlan[1] : null,
                    PilotDurum = pilotSatiri,
                    KorumaKapali = korumaSatiri == "KAPALI",
                    AgBasarili = true,
                    V110Yanit = v110Yanit,
                    HafifBakimZaman = hafifBakimGecerli,
                    AgirBakimZaman = agirBakimGecerli,
                    V116Yanit = v116Yanit
                };
            }
            catch (Exception ex)
            {
                Logger.WriteFileOnly(Log, "Toplu filo durumu alinamadi (Politika/Sinav kendi onbeleklerine dusuyor, Kapat icin bu turda islem yapilmayacak): " + ex.Message);
                return OnbellektenDerle();
            }
        }

        private static FiloDurumToplu OnbellektenDerle() => new FiloDurumToplu
        {
            Politika = FiloPolitika.Durum(false),
            SinavBitis = InternetEngeli.OnbellekOkuDisari(),
            KapatZaman = null, // Kapat icin yerel onbellek kavrami yok (one-shot komut) -- ag yoksa bu turda islem yapilmaz.
            EnvanterIsteZaman = null, // v80: ayni sekilde -- ag yoksa bu turda yeni bir envanter istegi uygulanmaz.
            YenidenBaslatZaman = null, // v89: ayni sekilde -- ag yoksa bu turda yeni bir yeniden-baslatma istegi uygulanmaz.
            DosyaTur = null, DosyaHedefKlasor = null, DosyaDosyaAdi = null, DosyaZaman = null, // v94: ayni sekilde.
            MesajZaman = null, MesajMetni = null, // v94: ayni sekilde.
            TeshisZaman = null, TeshisSorgular = null, PilotDurum = null, KorumaKapali = false,
            AgBasarili = false, V110Yanit = false, // v110: ag yoksa kalici durumlar DEGISTIRILMEZ.
            HafifBakimZaman = null, AgirBakimZaman = null, V116Yanit = false // v116: ag yoksa bu turda yeni bir bakim komutu uygulanmaz.
        };

        /// <summary>v57 hata duzeltmesi: kapat satirinin FiloBaslat'in ürettiği gercek zaman damgasi
        /// bicimine (yyyy-MM-dd HH:mm:ss.fff) uyup uymadigini dogrular -- bkz. Al() icindeki not.</summary>
        private static bool GecerliKapatZamaniMi(string s)
        {
            s = (s ?? "").Trim();
            if (s.Length == 0) return false;
            return DateTime.TryParseExact(s, "yyyy-MM-dd HH:mm:ss.fff",
                System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _);
        }
    }
}
