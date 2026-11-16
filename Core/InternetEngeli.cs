using System;
using System.IO;
using System.Net;
using System.Globalization;

namespace UnsalBakim.Core
{
    /// <summary>
    /// v57: "Sinav Modu" -- hedeflenen bilgisayar(lar)da TUM internet (kablolu + WiFi) gecici
    /// olarak kesilir. Diger 4 ayardan (Oyun/VPN/YouTube/AI) KOKTEN farkli bir ozellik: onlar
    /// internet baglantisini ACIK tutup sadece belirli siteleri/surecleri engelliyor -- bu, aglin
    /// KENDISINI kesiyor.
    ///
    /// ONEMLI TASARIM KARARLARI (Ali Bey ile tartisilip netlesen):
    ///   1. Ag ADAPTORU DEVRE DISI BIRAKILMAZ -- Windows Guvenlik Duvari'nda TUM giden/gelen
    ///      trafigi engelleyen bir kural eklenir. Boylece makine "agdan tamamen dusup kendini
    ///      kilitleme" riskine girmez; zamanlanmis gorevler, yerel donguler calismaya devam eder,
    ///      sadece internet TRAFIGI gider.
    ///   2. SURESIZ engelleme YOKTUR -- her baslatma bir SURE (dakika) alir ve bu sure, YEREL bir
    ///      tek-seferlik Windows gorevi ile, AGA/DRIVE'A HIC IHTIYAC DUYMADAN, kendiliginden
    ///      kalkar. "Unutuldu, uzaktan acamiyoruz" durumu hic olusmaz -- FILODAN baslatilan bir
    ///      engel de ayni yerel zamanlanmis gorevle kendiliginden kalkar, filo'ya bagimli degildir.
    ///   3. Varsayilan: SERBEST (engel KAPALI).
    ///   4. v57: Hem BU bilgisayardan yerel baslatma HEM Filo Yonetimi'nden hedefli (TUMU / bir
    ///      lab / tek bir bilgisayar adi) uzaktan baslatma desteklenir -- FiloPolitika.cs'teki
    ///      AYNI "Hedef" deseni (bilgisayar adi on-eki, sunucu tarafinda filtrelenir).
    ///      Yerel ve filo kaynakli engel birbirine karismaz: durum dosyasinda KAYNAK ("yerel"/
    ///      "filo") tutulur; 10 dk'lik denetim dongusu SADECE kendi actigi (kaynak=filo) bir
    ///      engeli yonetir, hocanin elle (yerel) basladigi bir engele hic dokunmaz.
    /// </summary>
    public static class InternetEngeli
    {
        private const string Log = "sinav_modu.txt";
        private const string KuralAdi = "UnsalBakim_SinavInternetEngeli";
        private const string GorevAdi = "UnsalBakim_SinavInternetEngeli_Kaldir";

        private static string DurumYolu =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "UnsalBakim", "sinav_modu.txt");
        private static string OnbellekYolu =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "UnsalBakim", "sinav_filo_onbellek.txt");

        private sealed class ZamanAsimliWebClient : WebClient
        {
            protected override WebRequest GetWebRequest(Uri address)
            {
                var req = base.GetWebRequest(address);
                if (req != null) req.Timeout = 10000;
                return req;
            }
        }

        /// <summary>Internet engelini (kablolu+WiFi, firewall tabanli) BU bilgisayarda, YEREL
        /// olarak baslatir. dakika sonunda, ag'a ihtiyac duymadan kendiliginden kalkar.</summary>
        public static string Baslat(int dakika)
        {
            if (dakika <= 0) dakika = 90;
            if (dakika > 600) dakika = 600; // guvenlik siniri: tek seferde en fazla 10 saat
            var bitis = DateTime.Now.AddMinutes(dakika);
            bool kuralTamam = BaslatDahili(bitis, "yerel");
            string temel = "Internet engeli baslatildi. " + bitis.ToString("HH:mm") + "'de (yaklasik " + dakika + " dk sonra) kendiliginden kalkacak.";
            if (!kuralTamam)
                temel += "\r\n\r\nUYARI: Guvenlik duvari kurali eklenirken hata olustu -- engel GERCEKTE " +
                    "calismiyor olabilir (bir baska guvenlik yazilimi, orn. Norton, Windows Guvenlik " +
                    "Duvari'ni devre disi birakmis olabilir). Bir tarayicida sayfa acilip acilmadigini " +
                    "kontrol edin; detay icin sinav_modu.txt logina bakin.";
            return temel;
        }

        /// <summary>true = her iki netsh kurali da (dir=out, dir=in) basariyla eklendi.</summary>
        private static bool BaslatDahili(DateTime bitis, string kaynak)
        {
            try
            {
                // Once varsa eskiyi temizle (ust uste birikmesin). Eski durum damgasi da silinir;
                // aksi halde yeni kurulum basarisiz olsa bile eski durum "uygulandi" sanilabilir.
                KuraliSil();
                GoreviSil();
                try { if (File.Exists(DurumYolu)) File.Delete(DurumYolu); } catch { }

                // Tum giden VE gelen trafigi engelle (TCP/UDP/hepsi -- profil: any).
                // v57 hata ayiklama eklemesi: ONCEDEN bu iki netsh cagrisinin ciktisi/hata kodu HIC
                // loglanmiyordu (ciktiyiLogla=false) -- netsh sessizce basarisiz olsa bile ("Internet
                // engeli baslatildi" mesaji HER ZAMAN gosteriliyordu), fark edilmesi imkansizdi. Bir
                // test makinesinde (Norton 360 gibi ucuncu parti bir guvenlik duvari Windows Guvenlik
                // Duvari kurallarini devre disi birakip/yok sayip gercek trafigi etkilememesi) tam da
                // bu durum yasandi -- panel "AKTIF" diyordu ama internet calismaya devam ediyordu.
                // Artik netsh ciktisi/hata kodu loglaniyor VE basarisizlikta durumuna UYARI ekleniyor.
                int cOut = NativeRunner.Run("netsh.exe",
                    "advfirewall firewall add rule name=\"" + KuralAdi + "\" dir=out action=block enable=yes profile=any",
                    Log, true);
                int cIn = NativeRunner.Run("netsh.exe",
                    "advfirewall firewall add rule name=\"" + KuralAdi + "\" dir=in action=block enable=yes profile=any",
                    Log, true);
                if (cOut != 0 || cIn != 0)
                {
                    Logger.Write(Log, "HATA: netsh kurali eklenemedi (out=" + cOut + ", in=" + cIn +
                        "). Internet engeli uygulanmis sayilmadi; sonraki filo denetiminde yeniden denenecek.");
                    KuraliSil();
                    GoreviSil();
                    return false;
                }

                // Suresi dolunca kendiliginden kaldiracak TEK SEFERLIK gorev (ag'a ihtiyac yok).
                // v62 HATA DUZELTMESI (BMTAL-LAB2-OGR1 loglarinda bulundu, 4 Kasim 2026): ONCEDEN
                // bitis.ToString("d", CultureInfo.CurrentCulture) kullaniliyordu -- bu, .NET'in
                // CurrentCulture'a gore uretttigi KISA TARIH bicimidir (tr-TR icin orn. "4.11.2026",
                // NOKTALI). schtasks.exe ise /sd parametresi icin KENDI (Win32/COM tabanli, .NET'ten
                // BAGIMSIZ) bir tarih ayristirici kullaniyor ve bu makinede "dd/mm/yyyy" (egik
                // cizgili, iki haneli) bekliyordu -- ikisi UYUSMUYORDU. Sonuc: schtasks "[!] ERROR:
                // Invalid Start Date" ile basarisiz oluyor, BaslatDahili de (sonsuz kilitlenme
                // riskini onlemek icin, TASARIM GEREGI) HEMEN ONCESINDE eklenen netsh firewall
                // kurallarini GERI ALIYORDU -- yani "Internet Kapat" komutu sessizce hicbir sey
                // yapmamis gibi sonuclaniyordu (filo'dan veya yerelden, HER IKI YOLDAN da, cunku
                // ikisi de AYNI BaslatDahili'yi cagiriyor). Artik CultureInfo.CurrentCulture'a HIC
                // guvenilmiyor; tarih dogrudan schtasks'in bu makinede istedigi "dd/mm/yyyy" bicimine
                // SABIT olarak yaziliyor (InvariantCulture, iki haneli gun/ay, egik cizgili).
                string tarih = bitis.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
                string saat = bitis.ToString("HH:mm", CultureInfo.InvariantCulture);
                string exe = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string komut = "/create /tn \"" + GorevAdi + "\" /tr \"\\\"" + exe + "\\\" /sinav-kaldir\" " +
                                "/sc once /sd " + tarih + " /st " + saat + " /ru SYSTEM /rl HIGHEST /f";
                int cTask = NativeRunner.Run("schtasks.exe", komut, Log, true);
                if (cTask != 0)
                {
                    Logger.Write(Log, "HATA: Internet engelini otomatik kaldiracak gorev kurulamadi (schtasks=" + cTask +
                        "). Sonsuz kilitlenme riskini onlemek icin firewall kurallari geri alindi; sonraki filo denetiminde yeniden denenecek.");
                    KuraliSil();
                    GoreviSil();
                    return false;
                }

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(DurumYolu));
                    File.WriteAllText(DurumYolu, bitis.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "|" + kaynak);
                }
                catch (Exception stateEx)
                {
                    Logger.Write(Log, "HATA: Internet engeli durum dosyasi yazilamadi: " + stateEx.Message + ". Firewall kurallari geri aliniyor.");
                    KuraliSil();
                    GoreviSil();
                    return false;
                }

                Logger.Write(Log, "Sinav modu baslatildi [" + kaynak + "] -- " + saat + "'de kendiliginden kalkacak. netsh out/in=0, schtasks=0.");
                return true;
            }
            catch (Exception ex) { Logger.Write(Log, "Sinav modu baslatma hatasi: " + ex.Message); return false; }
        }

        /// <summary>Internet engelini HEMEN kaldirir (sinav suresi dolmadan da kullanilabilir).</summary>
        public static string Kaldir()
        {
            try
            {
                KuraliSil();
                GoreviSil();
                try { if (File.Exists(DurumYolu)) File.Delete(DurumYolu); } catch { }
                Logger.Write(Log, "Sinav modu (internet engeli) kaldirildi.");
                return "Internet engeli kaldirildi, baglanti normale dondu.";
            }
            catch (Exception ex)
            {
                Logger.Write(Log, "Sinav modu kaldirma hatasi: " + ex.Message);
                return "Kaldirilamadi: " + ex.Message;
            }
        }

        /// <summary>Komut satirindan (/sinav-kaldir, zamanlanmis gorev tarafindan) cagrilir.</summary>
        public static void KaldirSessiz()
        {
            try { KuraliSil(); GoreviSil(); if (File.Exists(DurumYolu)) File.Delete(DurumYolu); }
            catch { }
        }

        public static string Durum()
        {
            try
            {
                var (bitis, kaynak) = DurumOku();
                if (bitis == null) return "Internet engeli KAPALI (serbest).";
                if (bitis.Value <= DateTime.Now) return "Internet engeli suresi dolmus olmali (kendiliginden kalkmis olabilir). 'Durumu goster' ile tekrar kontrol edin.";
                var kalan = bitis.Value - DateTime.Now;
                return "Internet engeli AKTIF [" + kaynak + "] -- " + bitis.Value.ToString("HH:mm") + "'de kalkacak (yaklasik " + (int)kalan.TotalMinutes + " dk kaldi).";
            }
            catch (Exception ex) { return "Durum okunamadi: " + ex.Message; }
        }

        private static (DateTime? bitis, string kaynak) DurumOku()
        {
            if (!File.Exists(DurumYolu)) return (null, null);
            var parcalar = File.ReadAllText(DurumYolu).Trim().Split('|');
            if (parcalar.Length >= 1 && TarihCoz(parcalar[0], out var b))
                return (b, parcalar.Length >= 2 ? parcalar[1] : "yerel");
            return (null, null);
        }

        // -----------------------------------------------------------------------------------
        //  FILO (uzaktan, hedefli) -- Filo Yonetimi panelinden, belirli bir hedefe (TUMU / lab /
        //  tek bilgisayar) gonderilen sinav-modu komutu. FiloPolitika.cs'teki AYNI "Hedef"
        //  deseni: sunucu tarafinda (Apps Script) bilgisayar adina gore filtrelenir.
        // -----------------------------------------------------------------------------------

        /// <summary>PANELDEN cagrilir: hedefteki makine(ler)de dakika sonra kendiliginden
        /// kalkacak bir internet engeli komutu Drive'a yazar.</summary>
        public static string FiloBaslat(int dakika, string hedef)
        {
            if (dakika <= 0) dakika = 90;
            if (dakika > 600) dakika = 600;
            if (string.IsNullOrWhiteSpace(Musteri.WebAppUrl))
                return "Drive log ozelligi kurulmadi (WebAppUrl bos); yazilamadi.";
            try
            {
                var bitis = DateTime.Now.AddMinutes(dakika);
                string json = "{\"tip\":\"sinav\",\"token\":\"" + Kacir(Musteri.ApiToken) + "\",\"musteriId\":\"" +
                              Kacir(Musteri.MusteriId) + "\",\"hedef\":\"" + Kacir(string.IsNullOrWhiteSpace(hedef) ? "TUMU" : hedef.Trim()) +
                              "\",\"bitisZamani\":\"" + bitis.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "\",\"guncelleyen\":\"" +
                              Kacir(Environment.UserName + "@" + Environment.MachineName) + "\"}";
                try { ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; } catch { }
                string yanit;
                using (var wc = new ZamanAsimliWebClient())
                {
                    wc.Headers["Content-Type"] = "application/json";
                    yanit = wc.UploadString(Musteri.WebAppUrl, "POST", json);
                }
                if (yanit != null && yanit.Trim() == "OK")
                    return "Internet pasif modu komutu gonderildi (hedef: " + hedef + "). En gec ~5 dakika icinde hedefteki makine(ler)de baslayip, " +
                           bitis.ToString("HH:mm") + "'de kendiliginden kalkacak.";
                return "Yazilamadi, Apps Script yaniti: '" + yanit + "' (script guncel olmayabilir).";
            }
            catch (Exception ex) { return "Yazilamadi: " + ex.Message; }
        }

        /// <summary>v57 (5. revizyon): Ali Bey'in talebi -- Filo'dan baslatilan internet pasif modu
        /// bir sureyle SINIRLI DEGIL GIBI DAVRANIR; hedefteki makine(ler)de hoca ELLE "Internet Ac"
        /// (FiloIptal) demeden kalkmasi BEKLENMEZ.
        /// v57.2 GUVENLIK DUZELTMESI (3 Kasim 2026, incelemede bulundu): ONCEDEN bitis zamani 10 yil
        /// sonraya yaziliyordu. SORUN: bu engel makinenin TUM giden/gelen trafigini (netsh, profile=
        /// any) kestigi icin, "Internet Ac" komutunu Google Apps Script'ten OGRENMEK icin gereken ag
        /// erisimi de kesiliyor -- yani makine kendi kendini filo kanalindan ULASILAMAZ hale
        /// getirebiliyordu (fiziksel mudahale disinda cikis yolu yoktu). Simdi bitis zamani 24 saat
        /// sonraya yazilir: BaslatDahili'nin kurdugu YEREL (aga IHTIYAC DUYMAYAN, Windows Gorev
        /// Zamanlayicisi tabanli) tek seferlik kaldirma gorevi, agdan BAGIMSIZ calisarak makineyi en
        /// gec 24 saat icinde KENDILIGINDEN acar -- bir makine hicbir zaman kalici kilitli kalmaz.
        /// Hoca daha uzun sureye ihtiyac duyarsa "Internet Kapat"a tekrar basmasi yeterlidir (her
        /// basis suresi 24 saat ileri tazeler); normal ders/okul kullaniminda bu fark edilmez.</summary>
        public static string FiloBaslatSuresiz(string hedef)
        {
            if (string.IsNullOrWhiteSpace(Musteri.WebAppUrl))
                return "Drive log ozelligi kurulmadi (WebAppUrl bos); yazilamadi.";
            try
            {
                var bitis = DateTime.Now.AddHours(24); // v57.2: guvenlik siniri -- asagidaki XML dogrulamasina bakin
                string json = "{\"tip\":\"sinav\",\"token\":\"" + Kacir(Musteri.ApiToken) + "\",\"musteriId\":\"" +
                              Kacir(Musteri.MusteriId) + "\",\"hedef\":\"" + Kacir(string.IsNullOrWhiteSpace(hedef) ? "TUMU" : hedef.Trim()) +
                              "\",\"bitisZamani\":\"" + bitis.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "\",\"guncelleyen\":\"" +
                              Kacir(Environment.UserName + "@" + Environment.MachineName) + "\"}";
                try { ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; } catch { }
                string yanit;
                using (var wc = new ZamanAsimliWebClient())
                {
                    wc.Headers["Content-Type"] = "application/json";
                    yanit = wc.UploadString(Musteri.WebAppUrl, "POST", json);
                }
                if (yanit != null && yanit.Trim() == "OK")
                    return "Internet Kapat komutu gonderildi (hedef: " + hedef + "). En gec ~5 dakika icinde " +
                           "hedefteki makine(ler)de baslar; 'Internet Ac' demeden en fazla 24 saat surer " +
                           "(guvenlik siniri -- makine ag erisimi olmadan kalici kilitlenmez). Daha uzun " +
                           "surerse bu dugmeye tekrar basin.";
                return "Yazilamadi, Apps Script yaniti: '" + yanit + "' (script guncel olmayabilir).";
            }
            catch (Exception ex) { return "Yazilamadi: " + ex.Message; }
        }

        /// <summary>PANELDEN cagrilir: filo-geneli internet pasif modu komutunu hemen iptal eder
        /// (gecmis bir bitis zamani yazarak) -- hedefteki makineler bir sonraki 5 dk'lik kontrolde
        /// kendi engellerini (eger filo kaynakliysa) kaldirir.</summary>
        public static string FiloIptal(string hedef)
        {
            if (string.IsNullOrWhiteSpace(Musteri.WebAppUrl))
                return "Drive log ozelligi kurulmadi (WebAppUrl bos); yazilamadi.";
            try
            {
                string json = "{\"tip\":\"sinav\",\"token\":\"" + Kacir(Musteri.ApiToken) + "\",\"musteriId\":\"" +
                              Kacir(Musteri.MusteriId) + "\",\"hedef\":\"" + Kacir(string.IsNullOrWhiteSpace(hedef) ? "TUMU" : hedef.Trim()) +
                              "\",\"bitisZamani\":\"\",\"guncelleyen\":\"" +
                              Kacir(Environment.UserName + "@" + Environment.MachineName) + "\"}";
                try { ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; } catch { }
                string yanit;
                using (var wc = new ZamanAsimliWebClient())
                {
                    wc.Headers["Content-Type"] = "application/json";
                    yanit = wc.UploadString(Musteri.WebAppUrl, "POST", json);
                }
                return (yanit != null && yanit.Trim() == "OK")
                    ? "Filo internet pasif modu iptal edildi."
                    : "Iptal edilemedi, Apps Script yaniti: '" + yanit + "'.";
            }
            catch (Exception ex) { return "Iptal edilemedi: " + ex.Message; }
        }

        /// <summary>10 dk'lik denetim dongusunden cagrilir: filodan bu makineye yonelik aktif bir
        /// sinav komutu var mi diye bakar (agKontrolEt=true ise Drive'dan taze okur), ve YEREL
        /// durumla (sadece kaynak=filo olan kismi) senkronize eder. Hocanin yerel/elle basladigi
        /// bir engele HICBIR ZAMAN dokunmaz.</summary>
        public static void FiloKontrolEtVeUygula(bool agKontrolEt)
        {
            DateTime? filoBitis = agKontrolEt ? FiloOku() : OnbellekOku();
            FiloKontrolEtVeUygulaDeger(filoBitis);
        }

        /// <summary>v57 (3. revizyon): FiloDurum.Al'in birlesik istegiyle ONCEDEN alinmis bir
        /// deger uzerinde calisir -- kendi ag istegini atmaz (3 ayri istegi 1'e dusurme).</summary>
        internal static void FiloKontrolEtVeUygulaDeger(DateTime? filoBitis)
        {
            try
            {
                var (yerelBitis, yerelKaynak) = DurumOku();

                if (filoBitis != null && filoBitis.Value > DateTime.Now)
                {
                    bool zatenUygulanmis = yerelKaynak == "filo" && yerelBitis != null &&
                                           Math.Abs((yerelBitis.Value - filoBitis.Value).TotalMinutes) < 1;
                    if (!zatenUygulanmis)
                    {
                        bool uygulandi = BaslatDahili(filoBitis.Value, "filo");
                        if (!uygulandi)
                            Logger.WriteFileOnly(Log, "FILO KOMUTU UYGULANAMADI; durum dosyasi yazilmadi. Bir sonraki 5 dakikalik dongude yeniden denenecek.");
                        else
                            Logger.WriteFileOnly(Log, "FILO INTERNET PASIF MODU uygulandi; bitis=" + filoBitis.Value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                    }
                    // v100 (inceleme/tavsiye belgesi, 11 Kasim 2026): daha once bu dal (komut
                    // GORULDU ama "zaten uygulanmis" oldugu icin atlandi) HIC loglanmiyordu --
                    // log'a sadece "hic komut gelmedi" ile "basariyla uygulandi" ayirt edilebiliyordu,
                    // "goruldu ama atlandi" durumu "hic gelmedi" ile ayni (sessiz) gorunuyordu. Bu,
                    // "İnternet Pasif Modu uygulanmiyor" sikayetlerinde asil nedeni (ag mi, HTML/
                    // format hatasi mi, yoksa gercekten uygulanmis olup panelde/ögretmende yanlis
                    // beklenti mi) ayirt etmeyi zorlastiriyordu. Artik ayri bir satirla kaydedilir.
                    else
                        Logger.WriteFileOnly(Log, "Filo internet pasif modu komutu goruldu (bitis=" +
                            filoBitis.Value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) +
                            ") ama bu makinede zaten uygulanmis durumda -- atlaniyor (yinelenen uygulama degil).");
                }
                else if (yerelKaynak == "filo")
                {
                    // Filo komutu bitti/iptal oldu -- SADECE filonun actigi engeli kaldir.
                    Logger.WriteFileOnly(Log, "Filo internet pasif modu komutu artik yok/suresi gecti -- filonun actigi engel kaldiriliyor.");
                    Kaldir();
                }
                // filoBitis == null VE yerelKaynak != "filo": normal/sessiz durum (aktif komut
                // yok, yerel engel de yok) -- her 5 dakikada BOYLE OLMASI BEKLENEN (en yaygin)
                // durum oldugu icin BILEREK loglanmiyor (log dosyasini sismesin diye); ag/format
                // hatasi yuzunden onbellekten dusulen durum FiloOku/CozVeOnbellekYaz icinde ZATEN
                // ayri satirla loglaniyor (bkz. yukarida).
            }
            catch (Exception ex) { Logger.WriteFileOnly(Log, "Filo sinav kontrolu hatasi: " + ex.Message); }
        }

        private static DateTime? FiloOku()
        {
            if (string.IsNullOrWhiteSpace(Musteri.WebAppUrl)) return OnbellekOku();
            try
            {
                try { ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; } catch { }
                string metin;
                using (var wc = new ZamanAsimliWebClient())
                {
                    wc.Headers["User-Agent"] = "UnsalBakim/1.0";
                    string url = Musteri.WebAppUrl + "?liste=sinav&token=" + Uri.EscapeDataString(Musteri.ApiToken) +
                                 "&musteriId=" + Uri.EscapeDataString(Musteri.MusteriId) +
                                 "&bilgisayarAdi=" + Uri.EscapeDataString(Environment.MachineName);
                    metin = wc.DownloadString(url);
                }
                string ham = (metin ?? "").Trim();
                if (ham.Length == 0) { OnbellekYaz(null); return null; } // Sunucudan bos = iptal/eslesme yok.
                if (TarihCoz(ham, out var d)) { OnbellekYaz(d); return d; }
                Logger.WriteFileOnly(Log, "Filo internet pasif modu yaniti tarih bicimine uymuyor; HTML/hata yaniti olabilecegi icin mevcut onbellek korunuyor: " + ham);
                return OnbellekOku();
            }
            catch (Exception ex)
            {
                Logger.WriteFileOnly(Log, "Filo sinav durumu kontrol edilemedi (onbellege dusuluyor): " + ex.Message);
                return OnbellekOku();
            }
        }

        /// <summary>v57 (3. revizyon): FiloDurum.Al'in birlesik istegi tarafindan kullanilir --
        /// ham metni cozer VE onbellegi tazeler (FiloOku'nun ag-sonrasi yan etkisiyle AYNI).</summary>
        internal static DateTime? CozVeOnbellekYaz(string metin)
        {
            string ham = (metin ?? "").Trim();
            if (ham.Length == 0) { OnbellekYaz(null); return null; } // Gecerli bos cevap = aktif komut yok/iptal.
            if (TarihCoz(ham, out var d)) { OnbellekYaz(d); return d; }
            Logger.WriteFileOnly(Log, "Toplu filo cevabindaki internet pasif modu tarihi gecersiz; HTML/hata cevabi olabilecegi icin onbellek silinmedi: " + ham);
            return OnbellekOku();
        }

        /// <summary>v57 (3. revizyon): FiloDurum.Al'in ag hatasi durumunda dusecegi yerel onbellek
        /// okuma yolu -- private OnbellekOku'ya disaridan erisim.</summary>
        internal static DateTime? OnbellekOkuDisari() => OnbellekOku();

        private static DateTime? OnbellekOku()
        {
            try
            {
                if (!File.Exists(OnbellekYolu)) return null;
                var m = File.ReadAllText(OnbellekYolu).Trim();
                return TarihCoz(m, out var d) ? d : (DateTime?)null;
            }
            catch { return null; }
        }

        private static void OnbellekYaz(DateTime? b)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(OnbellekYolu));
                File.WriteAllText(OnbellekYolu, b == null ? "" : b.Value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            }
            catch { }
        }

        /// <summary>Filo tarihlerini kulturden bagimsiz cozer. Yeni protokol ISO-8601 benzeri
        /// yyyy-MM-dd HH:mm:ss kullanir; dd.MM.yyyy bicimi yalnizca eski onbellek uyumlulugu icindir.</summary>
        private static bool TarihCoz(string metin, out DateTime tarih)
        {
            string s = (metin ?? "").Trim();
            string[] bicimler = { "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm:ss.fff", "dd.MM.yyyy HH:mm:ss", "dd.MM.yyyy HH:mm" };
            if (DateTime.TryParseExact(s, bicimler, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal, out tarih)) return true;
            // Onceki surumlerden kalan yerel onbellekleri kaybetmemek icin TR uyumlu geri-donus.
            return DateTime.TryParse(s, CultureInfo.GetCultureInfo("tr-TR"),
                DateTimeStyles.AssumeLocal, out tarih);
        }

        private static void KuraliSil()
        {
            NativeRunner.Run("netsh.exe", "advfirewall firewall delete rule name=\"" + KuralAdi + "\"", Log, false);
        }

        private static void GoreviSil()
        {
            NativeRunner.Run("schtasks.exe", "/delete /tn \"" + GorevAdi + "\" /f", Log, false);
        }

        private static string Kacir(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
