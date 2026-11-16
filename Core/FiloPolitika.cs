using System;
using System.IO;
using System.Net;

namespace UnsalBakim.Core
{
    /// <summary>
    /// v55: "Filo Yonetimi" -- kanitlanmis bir desen (yerel onbellek + periyodik ag kontrolu +
    /// hata/eksik durumda GUVENLI varsayilan) kullanilarak, Ali Bey'in
    /// Yerel Ayarlar sekmesindeki kisitlama ayarlarinin (Oyun, VPN, YouTube, AI web sitesi) HER
    /// BIRINI, Filo Yonetimi sekmesindeki BIREBIR AYNI gorunumlu bir panelden, TEK bir "tum filoya
    /// uygula" ile tum okula ZORUNLU uygulayabilmesini saglar.
    ///
    /// NASIL CALISIR:
    ///   - Drive'daki Google Sheet'te "Politika" sayfasinda musteriId basina TEK satir tutulur:
    ///     OyunEngelle, VpnEngelle, YoutubeKisitli, AiSiteEngelle sutunlarinin HER BIRI ayri ayri
    ///     "ACIK" / "KAPALI" / BOS olabilir. BOS = "bu ayar icin filo zorlamasi YOK, bu makine
    ///     kendi yerel ayarini kullansin". Panelden "Bu ayarlari TUM filoya uygula" dugmesi, o an
    ///     panelde isaretli 4 kutunun durumunu TEK bir yazmada gonderir.
    ///   - Her makine, zaten var olan 10 dakikalik oyun denetim dongusunde (OyunDenetimDongusuCalistir)
    ///     bu satiri TEK bir agdan OKUMA ile ceker (doGet, liste=politika) ve sonucu YEREL bir
    ///     onbellek dosyasina yazar. 1 dakikalik YouTube gorevi gibi cok sik calisan yerler agdan
    ///     SORMAZ, sadece zaten her 10 dakikada guncellenen onbellegi/efektif degeri kullanir.
    ///   - Her ayar icin EFEKTIF deger = filo degeri (ACIK/KAPALI) varsa O, yoksa yerel
    ///     AppSettings degeri. Yani filo zorlamasi yoksa (hepsi bos), davranis v55 oncesiyle
    ///     BIREBIR aynidir -- hicbir makine "varsayilan olarak" filodan etkilenmez.
    ///   - Ag/Drive erisimi yoksa veya Apps Script henuz guncellenmediyse: SESSIZCE en son bilinen
    ///     (yerel onbellekteki) degerlere, o da yoksa GUVENLI varsayilan "4'u de BOS" (zorlama yok,
    ///     yerel ayarlar gecerli) durumuna duser. Bir ag sorunu hicbir zaman bir makineyi
    ///     yanlislikla zorla engellemez/serbest birakmaz.
    ///
    /// APPS SCRIPT EKI: DriveLog_v55.gs'teki 'politika' islenmesine bakin -- doPost'ta
    /// d.oyunEngelle/d.vpnEngelle/d.youtubeKisitli/d.aiSiteEngelle alanlarini "Politika" sayfasina
    /// yazar; doGet'te bu 4 degeri "Oyun|Vpn|Youtube|AiSite" seklinde '|' ile ayrilmis TEK bir
    /// metin olarak doner (token gecersiz/satir yoksa dorduyle de BOS -> "|||").
    ///
    /// GOC NOTU (v55 ilk surumden gecis): Onceki MVP'de Politika sayfasinin sutunlari sadece
    /// [MusteriId, AiSiteEngelle, Guncelleyen, Zaman] idi. Bu surum sutun SIRASINI degistirdigi
    /// icin (yeni: MusteriId, OyunEngelle, VpnEngelle, YoutubeKisitli, AiSiteEngelle, Guncelleyen,
    /// Zaman), eski "Politika" sayfasindaki tek test satiri YANLIS sutuna duşebilir -- bu yuzden
    /// bu surume gecerken Ali Bey'in Sheet'teki eski "Politika" sekmesini SILMESI/temizlemesi
    /// gerekir; script ilk yazimda sayfayi doğru basliklarla yeniden olusturur.
    /// </summary>
    public struct FiloAyarlari
    {
        public bool? Oyun;
        public bool? Vpn;
        public bool? Youtube;
        public bool? AiSite;
        /// <summary>v57: Agir Bakim gun/saati -- bool degil, string (orn. "Cuma"/"12:00").
        /// null/bos = bu makine kendi yerel AgirGun/AgirSaat'ini kullansin (filo zorlamasi yok).</summary>
        public string AgirGun;
        public string AgirSaat;
        /// <summary>v69 (Ali Bey'in istegi): AI haftalik kontrolu -- acik/kapali VE gun/saat,
        /// Agir Bakim ile AYNI desen. AiKontrol null = bu ayar icin filo zorlamasi yok (yerel
        /// AiHaftalikAktif gecerli). AiGun/AiSaat null/bos = yerel AiGun/AiSaat gecerli.</summary>
        public bool? AiKontrol;
        public string AiGun;
        public string AiSaat;

        public static FiloAyarlari Bos => new FiloAyarlari { Oyun = null, Vpn = null, Youtube = null, AiSite = null, AgirGun = null, AgirSaat = null, AiKontrol = null, AiGun = null, AiSaat = null };
    }

    public static class FiloPolitika
    {
        private const string Log = "filo_politika.txt";

        private static string OnbellekYolu =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "UnsalBakim", "politika_onbellek.txt");

        private sealed class ZamanAsimliWebClient : WebClient
        {
            protected override WebRequest GetWebRequest(Uri address)
            {
                var req = base.GetWebRequest(address);
                if (req != null) req.Timeout = 10000; // 10 sn -- sik calisan donguleri bloklamasin
                return req;
            }
        }

        /// <summary>
        /// Filo-geneli 4 ayarin durumu. agKontrolEt=true ise Drive'dan taze okur VE onbellegi
        /// gunceller (10 dakikalik oyun dongusunden cagrilir); false ise SADECE yerel onbellegi
        /// okur, ag'a HIC cikmaz. Her alan: null = bu ayar icin FILO ZORLAMASI YOK (yerel ayar
        /// gecerli), true/false = TUM filoda ZORUNLU. Hata/bilinmeyen durumda GUVENLI varsayilan:
        /// tum alanlar null (zorlama yok).
        /// </summary>
        public static FiloAyarlari Durum(bool agKontrolEt)
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
                    // v57: bilgisayarAdi gonderilir -- Apps Script, kaydedilmis "Hedef" ile bu adi
                    // karsilastirip SADECE eslesirse (veya Hedef=TUMU ise) politikayi doner; yoksa
                    // bu makine icin "zorlama yok" (bos) doner. Filtreleme SUNUCU TARAFINDA yapilir,
                    // C# tarafi hep ayni bicimde (bos veya dolu pipe-string) cevap alir.
                    string url = Musteri.WebAppUrl + "?liste=politika&token=" + Uri.EscapeDataString(Musteri.ApiToken) +
                                 "&musteriId=" + Uri.EscapeDataString(Musteri.MusteriId) +
                                 "&bilgisayarAdi=" + Uri.EscapeDataString(Environment.MachineName);
                    metin = wc.DownloadString(url);
                }
                var d = Coz(metin);
                OnbellekYaz(d);
                return d;
            }
            catch (Exception ex)
            {
                Logger.WriteFileOnly(Log, "Politika kontrol edilemedi (onbellege dusuluyor): " + ex.Message);
                return OnbellekOku();
            }
        }

        /// <summary>PANELDEN cagrilir: 4 ayarin HER BIRINI TEK bir yazmada filoya gonderir.
        /// Her parametre icin null = "bu ayar icin filo zorlamasi yok/kaldir", true/false =
        /// "tum filoda ZORUNLU bu deger". Tumunu kaldirmak icin TumunuKaldir() kullanin.
        /// v57: agirGun/agirSaat eklendi -- bool degil, string; null/bos = zorlama yok.
        /// v57: hedef eklendi -- "TUMU" (varsayilan) veya bir bilgisayar-adi ON-EKI (orn.
        /// "BMTAL-LAB1"). Bos/"TUMU" -> tum filoya uygulanir. Belirli bir on-ek verilirse,
        /// SADECE adi o on-ekle BASLAYAN bilgisayarlar bu kurali gorur (digerleri bu push'u hic
        /// gormemis gibi kendi yerel ayarina doner). Filtreleme Apps Script tarafinda yapilir.</summary>
        public static string Uygula(bool? oyun, bool? vpn, bool? youtube, bool? aiSite, string agirGun = null, string agirSaat = null, string hedef = "TUMU", bool? aiKontrol = null, string aiGun = null, string aiSaat = null)
        {
            if (string.IsNullOrWhiteSpace(Musteri.WebAppUrl))
                return "Drive log ozelligi kurulmadi (WebAppUrl bos); yazilamadi.";
            try
            {
                string json = "{\"tip\":\"politika\",\"token\":\"" + Kacir(Musteri.ApiToken) + "\",\"musteriId\":\"" +
                              Kacir(Musteri.MusteriId) + "\",\"oyunEngelle\":\"" + Metin(oyun) +
                              "\",\"vpnEngelle\":\"" + Metin(vpn) + "\",\"youtubeKisitli\":\"" + Metin(youtube) +
                              "\",\"aiSiteEngelle\":\"" + Metin(aiSite) + "\",\"agirGun\":\"" + Kacir(agirGun ?? "") +
                              "\",\"agirSaat\":\"" + Kacir(agirSaat ?? "") + "\",\"hedef\":\"" +
                              Kacir(string.IsNullOrWhiteSpace(hedef) ? "TUMU" : hedef.Trim()) +
                              "\",\"aiHaftalik\":\"" + Metin(aiKontrol) + "\",\"aiGun\":\"" + Kacir(aiGun ?? "") +
                              "\",\"aiSaat\":\"" + Kacir(aiSaat ?? "") + "\",\"guncelleyen\":\"" +
                              Kacir(Environment.UserName + "@" + Environment.MachineName) + "\"}";
                try { ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; } catch { }
                string yanit;
                using (var wc = new ZamanAsimliWebClient())
                {
                    wc.Headers["Content-Type"] = "application/json";
                    yanit = wc.UploadString(Musteri.WebAppUrl, "POST", json);
                }
                if (yanit != null && yanit.Trim() == "OK")
                {
                    var d = new FiloAyarlari { Oyun = oyun, Vpn = vpn, Youtube = youtube, AiSite = aiSite, AgirGun = agirGun, AgirSaat = agirSaat, AiKontrol = aiKontrol, AiGun = aiGun, AiSaat = aiSaat };
                    OnbellekYaz(d);
                    return "Filo ayarlari gonderildi. En gec 5 dakika icinde tum makinelerde uygulanacak.";
                }
                return "Yazilamadi, Apps Script yaniti: '" + yanit + "' (script guncel olmayabilir -- Politika ekini yapistirip yeniden dagitin).";
            }
            catch (Exception ex) { return "Yazilamadi: " + ex.Message; }
        }

        /// <summary>Tum filo zorlamalarini kaldirir -- her makine kendi yerel ayarlarina doner.</summary>
        public static string TumunuKaldir() => Uygula(null, null, null, null, null, null, "TUMU", null, null, null);

        private static string Metin(bool? v) => v == null ? "" : (v.Value ? "ACIK" : "KAPALI");

        private static bool? Deger(string s)
        {
            s = (s ?? "").Trim();
            if (s.Equals("ACIK", StringComparison.OrdinalIgnoreCase)) return true;
            if (s.Equals("KAPALI", StringComparison.OrdinalIgnoreCase)) return false;
            return null; // bos/taninmayan -> zorlama yok
        }

        private static string DegerStr(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        private static readonly string[] GecerliGunler =
            { "Pazartesi", "Sali", "Carsamba", "Persembe", "Cuma", "Cumartesi", "Pazar" };

        /// <summary>v57 hata duzeltmesi: AgirGun/AgirSaat, Oyun/Vpn/Youtube/AiSite'daki "ACIK/KAPALI
        /// disinda her sey -> null (zorlama yok)" guvenligine sahip degildi -- Apps Script bir HATA/
        /// HTML sayfasi dondurdugunde (bkz. FiloDurum.Al), bu sayfanin bir parcasi dogrudan "zorunlu
        /// gun/saat" olarak kabul edilip yerel degerin UZERINE yaziliyordu (TaskHelper.SaatParse
        /// gecersiz saati 08:00'e sessizce dusurdugu icin cokme olmuyordu, ama Agir Bakim yanlislikla
        /// 08:00'e kayabiliyordu). Artik gun, bilinen 7 gunden biri degilse VEYA saat HH:mm
        /// bicimine uymuyorsa -- IKISI DE -- tamamen null'a (zorlama yok, yerel ayar gecerli)
        /// dusurulur; kismi/gecersiz bir zorlama uygulanmaz.</summary>
        private static string GecerliGunStr(string s)
        {
            s = DegerStr(s);
            if (s == null) return null;
            foreach (var g in GecerliGunler)
                if (string.Equals(g, s, StringComparison.OrdinalIgnoreCase)) return g;
            return null;
        }

        private static string GecerliSaatStr(string s)
        {
            s = DegerStr(s);
            if (s == null) return null;
            return TimeSpan.TryParseExact(s, "hh\\:mm", System.Globalization.CultureInfo.InvariantCulture, out _) ? s : null;
        }

        /// <summary>v57 (3. revizyon): FiloDurum.Al'in birlesik istegi tarafindan kullanilir --
        /// ham metni cozer VE onbellegi tazeler, tek bir ag istegine ragmen eski (ayri istekli)
        /// Durum(true) ile AYNI yan etkiyi (onbellek guncellemesi) saglar.</summary>
        internal static FiloAyarlari CozVeOnbellekYaz(string metin)
        {
            var d = Coz(metin);
            OnbellekYaz(d);
            return d;
        }

        private static FiloAyarlari Coz(string metin)
        {
            var parcalar = (metin ?? "").Split('|');
            string P(int i) => i < parcalar.Length ? parcalar[i] : "";
            string gun = GecerliGunStr(P(4));
            string saat = GecerliSaatStr(P(5));
            if (gun == null || saat == null) { gun = null; saat = null; } // ikisi de gecerli degilse zorlama yok
            // v69: AiGun/AiSaat (indeks 7/8) -- eski/guncellenmemis Apps Script'ten gelen kisa
            // yanitlarda (P(7)/P(8) bos string) zaten GecerliGunStr/GecerliSaatStr null doner --
            // yani script guncellenmeden bu alanlar icin DAIMA "zorlama yok" (guvenli varsayilan).
            string aiGun = GecerliGunStr(P(7));
            string aiSaat = GecerliSaatStr(P(8));
            if (aiGun == null || aiSaat == null) { aiGun = null; aiSaat = null; }
            return new FiloAyarlari
            {
                Oyun = Deger(P(0)),
                Vpn = Deger(P(1)),
                Youtube = Deger(P(2)),
                AiSite = Deger(P(3)),
                AgirGun = gun,
                AgirSaat = saat,
                AiKontrol = Deger(P(6)),
                AiGun = aiGun,
                AiSaat = aiSaat
            };
        }

        private static FiloAyarlari OnbellekOku()
        {
            try
            {
                if (!File.Exists(OnbellekYolu)) return FiloAyarlari.Bos;
                return Coz(File.ReadAllText(OnbellekYolu));
            }
            catch { return FiloAyarlari.Bos; }
        }

        private static void OnbellekYaz(FiloAyarlari d)
        {
            try
            {
                string yol = OnbellekYolu;
                Directory.CreateDirectory(Path.GetDirectoryName(yol));
                File.WriteAllText(yol, Metin(d.Oyun) + "|" + Metin(d.Vpn) + "|" + Metin(d.Youtube) + "|" + Metin(d.AiSite) +
                    "|" + (d.AgirGun ?? "") + "|" + (d.AgirSaat ?? "") + "|" + Metin(d.AiKontrol) +
                    "|" + (d.AiGun ?? "") + "|" + (d.AiSaat ?? ""));
            }
            catch { }
        }

        private static string Kacir(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
