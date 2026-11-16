using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace UnsalBakim.Core
{
    /// <summary>
    /// Zamanlanmis gorevler (Gorev Zamanlayici > \UnsalBakim\ klasoru).
    ///   - Hafif_Bakim : her gun, bilgisayar bostayken   (exe /hafif)
    ///   - Agir_Bakim  : haftalik (gun/saat), bostayken   (exe /agir)
    ///   - Oyun_Engelle: her N dakikada bir, surekli       (exe /oyun-engelle)
    /// Gizli kurulumda gorevler "Hidden" isaretlenir ve kurulu exe yolunu kullanir.
    /// </summary>
    public static class TaskHelper
    {
        /// <summary>Gorevlerin isaret edecegi exe. Bos ise calisan exe kullanilir (kurulu kopya icin set edilir).</summary>
        public static string HedefExe;

        private static string ExeKullan =>
            string.IsNullOrEmpty(HedefExe) ? System.Reflection.Assembly.GetExecutingAssembly().Location : HedefExe;

        private const string Klasor = "\\UnsalBakim\\";
        private const string AdHafif = "Hafif_Bakim";
        private const string AdAgir = "Agir_Bakim";
        private const string AdOyun = "Oyun_Engelle";
        private const string AdOyunKaldir = "Oyun_Kaldir";
        private const string AdYoutube = "Youtube_Kisit";
        private const string AdAiHaftalik = "AI_Haftalik";
        private const string AdGuncelleme = "Guncelleme_Kontrol";
        private const string AdDuvarKagidi = "Duvar_Kagidi";
        private const string AdDiskAcil = "Disk_Acil";

        private static readonly Dictionary<string, string> Gunler =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "pazartesi", "Monday" }, { "sali", "Tuesday" }, { "salı", "Tuesday" },
                { "carsamba", "Wednesday" }, { "çarşamba", "Wednesday" },
                { "persembe", "Thursday" }, { "perşembe", "Thursday" },
                { "cuma", "Friday" }, { "cumartesi", "Saturday" }, { "pazar", "Sunday" }
            };

        private static string XmlDizin
        {
            get
            {
                string d = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "UnsalBakim", "gorev");
                try { Directory.CreateDirectory(d); } catch { }
                return d;
            }
        }

        // ---------------------------------------------------------------
        //  Toplu kurulum
        // ---------------------------------------------------------------
        public static void KurulumYap(AppSettings ayar) => KurulumYap(ayar, false);

        public static void KurulumYap(AppSettings ayar, bool gizli)
        {
            // v109: EN KRITIK iki gorev EN BASTA kurulur -- kurulum bu noktadan sonra yarida kalsa
            // bile makine (a) saatlik guncellemeyi ve (b) 5 dk'lik denetim/kurtarma dongusunu
            // kaybetmez; ikisi de "kurulum yarida kaldi" isaretini gorup kurulumu tamamlar
            // (bkz. Installer.YarimKalanKurulumuTamamla).
            if (ayar.OtoGuncellemeAktif) GuncellemeGoreviKur(gizli);
            OyunEngelGoreviKur(5, gizli, false);
            HafifBakimGoreviKur(ayar.HafifSaat, ayar.BostaDk, gizli);
            AgirBakimGoreviKur(ayar.AgirGun, ayar.AgirSaat, ayar.BostaDk, gizli);
            // v52 duzeltmesi: bu gorev artik SADECE oyun engelini degil, VPN/YouTube periyodik
            // tazelemesini de tasiyor. ONCEDEN sadece ayar.OyunEngelle=true ise kuruluyordu -- bir
            // makinede Oyun kapali ama VPN veya YouTube kisiti aciksa, o makine bu gorevi HIC
            // CALISTIRMAZ: VPN/YouTube periyodik tazelenmez. Simdi ucu de kontrol edilir; sadece
            // hicbiri acik degilse gorev kaldirilir.
            // v55 duzeltme: ONCEDEN bu gorev, dort yerel bayragin (Oyun/VPN/YouTube/AI) HEPSI
            // kapaliysa (orn. bir OGRETMEN makinesi) HIC KURULMUYORDU. Ama bu gorev SADECE yerel
            // kisitlamalari degil, Filo Yonetimi'nin ag'dan okudugu ZORUNLU politikayi da kontrol
            // ediyor -- gorev kurulu olmayan bir makine, ileride
            // Filo Yonetimi'nden gonderilecek bir zorunlu kurali ASLA goremez (dort yerel ayari da
            // kapali baslayan bir ogretmen makinesi, yanlislikla "ogrenci" yapilsa veya Filo
            // Yonetimi'nden ozellikle o makineye bir kural zorlansa bile). Bu yuzden gorev artik
            // yerel bayraklardan BAGIMSIZ, HER ZAMAN kurulur; govdesi zaten "filo ?? yerel ayar"
            // mantigiyla calistigi icin hicbir ayar/kural yoksa hicbir sey degistirmez (ogretmen
            // makinesinde fazladan risk yok, sadece 10 dk'da bir hafif bir ag kontrolu eklenir).
            OyunEngelGoreviKur(5, gizli);   // #6/v57: 5 dk birlesik oyun+vpn+youtube+ai+filo denetimi
            // v57: YouTube'un AYRI 1 dk "ders/teneffus gecisi" gorevi (YoutubeGoreviKur) artik
            // GEREKSIZ -- ders/teneffus ayrimi tamamen kaldirildi, yukaridaki 5 dk'lik birlesik
            // gorev zaten HER DONGUDE YoutubeKisit.SaatiUygula(efektifYoutube) cagiriyor. Eski bir
            // kurulumdan (v56 ve oncesi) kalmis olabilecek bu gorev, aktif olarak KALDIRILIR --
            // sadece yeniden yaratilmasi atlanmiyor, Windows Gorev Zamanlayicisi'ndan silinir.
            YoutubeGoreviKaldir();
            // Haftalik AI kontrol ve guncelleme. Ayarlar sekmesindeki kutucukla ac/kapa; gun/saat
            // v69'dan itibaren ayar.AiGun/AiSaat'tan gelir (onceden Pazartesi 09:00'a sabitti).
            if (ayar.AiHaftalikAktif)
                AiHaftalikGoreviKur(ayar.AiGun, ayar.AiSaat, gizli);
            else
                AiHaftalikGoreviKaldir();
            // Saatlik Drive guncelleme kontrolu: yeni surum varsa SESSIZCE (menusuz) indirip
            // kurulu kopyanin uzerine koyar. Ayarlar sekmesindeki kutucukla ac/kapa.
            if (ayar.OtoGuncellemeAktif)
                GuncellemeGoreviKur(gizli);
            else
                GuncellemeGoreviKaldir();
            // Eski surumdeki yalnizca raporlayan gunluk gorevi kaldir; is artik denetim gorevlerinde.
            GorevSil(AdOyunKaldir);

            // v102: Duvar kagidi (bilgisayar adi + okul logosu) -- ac/kapa YOK, HER kurulumda
            // (ogretmen dahil) kosulsuz kurulur. Asil resim uretimi/HKLM yazimi SYSTEM baglamli
            // /oyun-engelle dongusunde (bkz. Program.cs, MasaustuDuvarKagidi.OlusturVeUygula)
            // yapilir; burada SADECE etkilesimli-oturum uygulama gorevinin bir an once (ilk 5 dk'lik
            // donguyu beklemeden) kurulu olmasi saglanir.
            DuvarKagidiGoreviKur(gizli);
        }

        // ---------------------------------------------------------------
        //  Gunluk hafif bakim (bostayken) -> exe /hafif
        // ---------------------------------------------------------------
        public static void HafifBakimGoreviKur(string saat, int bostaDk) => HafifBakimGoreviKur(saat, bostaDk, false);

        public static void HafifBakimGoreviKur(string saat, int bostaDk, bool gizli)
        {
            const string log = "kurulum.txt";
            // v57: Hafif bakim artik TENEFFUS saatlerine gore DEGIL, bilgisayarin ACILISINDA
            // (BootTrigger) tetiklenir -- ders programindan tamamen bagimsiz, ogretmen artik
            // ders saatlerini ayarlamak zorunda degil. Gunde birden fazla acilis olursa tekrar
            // tekrar calismasin diye gorevin kendisi degil, /hafif komutunun kendisi (Program.cs)
            // gunluk bir damga dosyasiyla en fazla 1 kez calismayi garanti eder. "saat" parametresi
            // artik kullanilmiyor (geriye donuk imza uyumlulugu icin tutuldu).
            string tetik = "    <BootTrigger>\r\n      <Enabled>true</Enabled>\r\n    </BootTrigger>";
            string xml = GorevXml(
                aciklama: "Unsal Bakim: Hafif bakim, bilgisayarin ACILISINDA calisir (gunde en fazla 1 kez, /hafif kendi damgasiyla korur).",
                tetik: tetik,
                arguman: "/hafif",
                bostaDk: bostaDk, sureSaat: 1, pildeCalisir: true, idleGerekli: false, gizli: gizli);
            XmlIleKur(AdHafif, xml, log);
            Logger.Write(log, "Hafif bakim gorevi kuruldu (acilista, gunde en fazla 1 kez)" + (gizli ? " [gizli]" : "") + ".");
        }

        // ---------------------------------------------------------------
        //  Haftalik agir bakim (bostayken) -> exe /agir
        // ---------------------------------------------------------------
        public static void AgirBakimGoreviKur(string gun, string saat, int bostaDk) => AgirBakimGoreviKur(gun, saat, bostaDk, false);

        public static void AgirBakimGoreviKur(string gun, string saat, int bostaDk, bool gizli)
        {
            const string log = "kurulum.txt";
            string enGun = Gunler.TryGetValue((gun ?? "Cuma").Trim(), out var g) ? g : "Friday";
            string xml = GorevXml(
                aciklama: "Unsal Bakim: Haftalik agir bakim (" + gun + " " + Saat(saat) + ", bostayken).",
                tetik: HaftalikTetik(enGun, saat),
                arguman: "/agir",
                bostaDk: bostaDk, sureSaat: 6, pildeCalisir: false, idleGerekli: true, gizli: gizli,
                bostaBitinceDur: true);
            XmlIleKur(AdAgir, xml, log);
            SonUygulananAgirYaz(gun, saat);
            Logger.Write(log, "Agir bakim gorevi kuruldu (" + gun + " " + Saat(saat) + ", bostayken)" + (gizli ? " [gizli]" : "") + ".");
        }

        private static string SonUygulananAgirYolu =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "UnsalBakim", "agir_bakim_son.txt");

        private static void SonUygulananAgirYaz(string gun, string saat)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SonUygulananAgirYolu));
                File.WriteAllText(SonUygulananAgirYolu, (gun ?? "") + "|" + (saat ?? ""));
            }
            catch { }
        }

        /// <summary>v57: Agir Bakim gun/saati artik filo ?? yerelden EFEKTIF olarak hesaplaniyor
        /// (Program.cs'teki 10 dk donguden cagrilir). Bu deger, en son UYGULANAN (Windows gorevine
        /// yazilan) deger ile FARKLIYSA, Windows gorevini o anda tek seferlik yeniden kaydeder --
        /// deger degismediyse (buyuk cogunluk) HICBIR SEY yapmaz, gorev dokunulmadan kalir.</summary>
        public static void AgirBakimGuncelleGerekirse(string efektifGun, string efektifSaat, int bostaDk, bool gizli = false)
        {
            try
            {
                string onceki = File.Exists(SonUygulananAgirYolu) ? File.ReadAllText(SonUygulananAgirYolu).Trim() : null;
                string simdiki = (efektifGun ?? "") + "|" + (efektifSaat ?? "");
                if (onceki == simdiki) return; // degismedi, dokunma
                AgirBakimGoreviKur(efektifGun, efektifSaat, bostaDk, gizli);
                Logger.WriteFileOnly("kurulum.txt", "Agir bakim zamanlamasi degisti (" + simdiki + "), gorev yeniden kaydedildi.");
            }
            catch (Exception ex) { Logger.WriteFileOnly("kurulum.txt", "Agir bakim zamanlama kontrolu hatasi: " + ex.Message); }
        }

        // ---------------------------------------------------------------
        //  Haftalik AI kontrol ve guncelleme -> exe /ai-haftalik
        //  v69: gun/saat artik SABIT degil, parametre (onceden hep Pazartesi 09:00'di) --
        //  Agir Bakim ile AYNI desen: yerel Ayarlar degeri, Filo Yonetimi'nden zorlanabilir.
        //  Idle SART DEGIL. StartWhenAvailable=true (GorevXml'de sabit) sayesinde makine o saatte
        //  kapaliysa, acilinca gorev gecikmeli calisir (kacirilmaz).
        //  Anahtar yoksa YapayZeka.KontrolVeGuncelle kendisi "ayarli degil" yazip sessizce cikar.
        // ---------------------------------------------------------------
        public static void AiHaftalikGoreviKur(string gun, string saat, bool gizli)
        {
            const string log = "kurulum.txt";
            string enGun = Gunler.TryGetValue((gun ?? "Pazartesi").Trim(), out var g) ? g : "Monday";
            string xml = GorevXml(
                aciklama: "Unsal Bakim: Haftada bir (" + gun + " " + Saat(saat) + ") AI ile oyun/YouTube/VPN veritabanini gunceller.",
                tetik: HaftalikTetik(enGun, saat),
                arguman: "/ai-haftalik",
                bostaDk: 1, sureSaat: 1, pildeCalisir: true, idleGerekli: false, gizli: gizli);
            XmlIleKur(AdAiHaftalik, xml, log);
            SonUygulananAiYaz(gun, saat);
            Logger.WriteFileOnly(log, "Haftalik AI kontrol gorevi kuruldu (" + gun + " " + Saat(saat) + ")" + (gizli ? " [gizli]" : "") + ".");
        }

        public static void AiHaftalikGoreviKaldir()
        {
            GorevSil(AdAiHaftalik);
            try { if (File.Exists(SonUygulananAiYolu)) File.Delete(SonUygulananAiYolu); } catch { }
            Logger.WriteFileOnly("kurulum.txt", "Haftalik AI kontrol gorevi kaldirildi.");
        }

        private static string SonUygulananAiYolu =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "UnsalBakim", "ai_haftalik_son.txt");

        private static void SonUygulananAiYaz(string gun, string saat)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SonUygulananAiYolu));
                File.WriteAllText(SonUygulananAiYolu, "ACIK|" + (gun ?? "") + "|" + (saat ?? ""));
            }
            catch { }
        }

        /// <summary>v69: AI haftalik gorevinin acik/kapali VE gun/saat EFEKTIF degeri (filo ?? yerel,
        /// Program.cs'teki 10 dk donguden cagrilir) en son UYGULANANDAN FARKLIYSA, gorevi o anda
        /// tek seferlik yeniden kaydeder/kaldirir -- Agir Bakim'daki AgirBakimGuncelleGerekirse ile
        /// AYNI desen. Degismediyse (buyuk cogunluk) HICBIR SEY yapmaz.</summary>
        public static void AiHaftalikGuncelleGerekirse(bool efektifAktif, string efektifGun, string efektifSaat, bool gizli = false)
        {
            try
            {
                string onceki = File.Exists(SonUygulananAiYolu) ? File.ReadAllText(SonUygulananAiYolu).Trim() : null;
                string simdiki = (efektifAktif ? "ACIK" : "KAPALI") + "|" + (efektifGun ?? "") + "|" + (efektifSaat ?? "");
                if (onceki == simdiki) return; // degismedi, dokunma
                if (efektifAktif) AiHaftalikGoreviKur(efektifGun, efektifSaat, gizli);
                else
                {
                    AiHaftalikGoreviKaldir();
                    // AiHaftalikGoreviKaldir SonUygulananAiYolu'nu SILER -- "KAPALI" durumunu da
                    // izleyebilmek icin (tekrar tekrar GorevSil cagirmamak icin) burada yeniden yaz.
                    try { Directory.CreateDirectory(Path.GetDirectoryName(SonUygulananAiYolu)); File.WriteAllText(SonUygulananAiYolu, simdiki); } catch { }
                }
                Logger.WriteFileOnly("kurulum.txt", "AI haftalik zamanlamasi degisti (" + simdiki + "), gorev guncellendi.");
            }
            catch (Exception ex) { Logger.WriteFileOnly("kurulum.txt", "AI haftalik zamanlama kontrolu hatasi: " + ex.Message); }
        }

        // ---------------------------------------------------------------
        //  Saatlik Drive guncelleme kontrolu: her 60 dk -> exe /guncelleme-kontrol
        //  SESSIZ calisir (MessageBox/UI YOK); yeni surum yoksa hicbir dosyaya dokunmaz. Yeni
        //  surum varsa indirip KURULU kopyanin (bu gorevin calistirdigi exe) uzerine otomatik
        //  koyar -- boylece ogretmen makineye gitmeden/menu acmadan surum guncel kalir. Ilk USB
        //  kurulumu zaten en son surumle yapildigi icin pratikte sadece ARA GUNCELLEMELERDE devreye girer.
        // ---------------------------------------------------------------
        public static void GuncellemeGoreviKur(bool gizli)
        {
            const string log = "kurulum.txt";
            string xml = GorevXml(
                aciklama: "Unsal Bakim: Saatte bir Google Drive'da yeni surum olup olmadigini kontrol eder; varsa sessizce indirip kurulu kopyanin uzerine koyar.",
                tetik: OyunTetik(60),
                arguman: "/guncelleme-kontrol",
                bostaDk: 1, sureSaat: 1, pildeCalisir: true, idleGerekli: false, gizli: gizli);
            XmlIleKur(AdGuncelleme, xml, log);
            Logger.WriteFileOnly(log, "Saatlik guncelleme kontrol gorevi kuruldu (60 dk)" + (gizli ? " [gizli]" : "") + ".");
        }

        public static void GuncellemeGoreviKaldir()
        {
            GorevSil(AdGuncelleme);
            Logger.WriteFileOnly("kurulum.txt", "Saatlik guncelleme kontrol gorevi kaldirildi.");
        }

        // ---------------------------------------------------------------
        //  Oyun engelleme: her N dk, surekli (idle sart degil) -> exe /oyun-engelle
        // ---------------------------------------------------------------
        public static void OyunEngelGoreviKur(int dakika) => OyunEngelGoreviKur(dakika, false, true);

        public static void OyunEngelGoreviKur(int dakika, bool gizli) => OyunEngelGoreviKur(dakika, gizli, true);

        public static void OyunEngelGoreviKur(int dakika, bool gizli, bool hemenCalistir)
        {
            const string log = "kurulum.txt";
            // v57 (ikinci revizyon): Istek #6'da 10 dakikaya sabitlenmisti; Ali Bey'in filo ayar
            // yayilma suresini kisaltma istegiyle 5 dakikaya dusuruldu. 3 dakika da teklif
            // edildi ama Apps Script'in gunluk calisma suresi kotasini (ucretsiz Google hesabi)
            // yogun okul saatlerinde zorlayabilecegi icin 5 dakikada karar kilindi -- istek
            // sayisini sadece 2 katina cikariyor (3 dakikada ~3.3 kat olurdu).
            dakika = 5;
            // v106 (Ali Bey'in bildirdigi sorun, 11 Kasim 2026: "cocuklar youtube girdiklerinde
            // emirler okumasi yavasliyor, 5 dk'lik emir kontrolu oncelikli yetkiyle, reset gibi
            // oncelikli olmali"): YouTube/video oynatma CPU'yu yogun kullanirken, bu gorev varsayilan
            // (Normal, oncelik=7) oncelikte calisinca Windows'un zamanlayicisinda video ile ayni
            // sirada bekleyebiliyor -- filo emirlerinin (yeniden baslat, mesaj, dosya vb.) okunmasi
            // gozle gorulur sekilde gecikiyordu. Oncelik 4'e (Yuksek/"High") cekildi -- Windows Gorev
            // Zamanlayici'nin <Priority> degeri 0 (en yuksek) ile 10 (en dusuk) arasinda; 7-8 "Normal"
            // sinifina, 3-4 "Yuksek" sinifina denk gelir. Tam "Gercek Zamanli" (0-2) BILEREK
            // SECILMEDI -- o kadar agresif bir oncelik ekranda ses/goruntu kesilmelerine yol acabilir,
            // amacimiz sadece video ile es zamanli calisirken ACLIK (starvation) cekmemesi.
            string xml = GorevXml(
                aciklama: "Unsal Bakim: Her 5 dk oyun+VPN sureclerini/sitelerini engeller, oyun loglarini tarar, taninan kurulu oyunlari sessiz kaldirmayi dener ve YouTube/AI kisitini/filo ayarlarini tazeler.",
                tetik: OyunTetik(dakika),
                arguman: "/oyun-engelle",
                bostaDk: 1, sureSaat: 0, pildeCalisir: true, idleGerekli: false, gizli: gizli,
                oncelik: 4);
            XmlIleKur(AdOyun, xml, log);
            // Yeniden baslatma beklemeden hemen calistir (gerekirse). UI duzeyindeki tek seferlik islem
            // zaten donguyu calistirdiysa cift kaldirma denemesini onlemek icin hemenCalistir=false kullanilir.
            if (hemenCalistir)
                NativeRunner.Run("schtasks.exe", "/run /tn \"" + Klasor + AdOyun + "\"", log, false);
            Logger.WriteFileOnly(log, "Oyun+VPN+YouTube denetimi gorevi kuruldu (5 dk)." + (gizli ? " [arka plan]" : "") + ".");
        }

        // ---------------------------------------------------------------
        // Geriye donuk uyumluluk: eski kurulumlardaki gunluk gorev. Yeni kurulumda kullanilmaz.
        // ---------------------------------------------------------------
        public static void OyunKaldirGoreviKur(string saat, int bostaDk, bool gizli)
        {
            const string log = "kurulum.txt";
            string xml = GorevXml(
                aciklama: "Unsal Bakim eski gorev uyumlulugu: oyun log kontrolu ve whitelist oyun kaldirma denemesi.",
                tetik: GunlukTetik(saat),
                arguman: "/oyun-kaldir",
                bostaDk: bostaDk, sureSaat: 3, pildeCalisir: false, idleGerekli: true, gizli: gizli);
            XmlIleKur(AdOyunKaldir, xml, log);
            Logger.WriteFileOnly(log, "Eski uyumluluk oyun gorevi kuruldu; yeni kurulumlarda 3 dk gorevi kullanilir.");
        }

        // ---------------------------------------------------------------
        //  YouTube teneffus kisiti gorevi (ESKI, v56 ve oncesi) -- v57'de KALDIRILDI (bkz. Kur()
        //  icindeki YoutubeGoreviKaldir() cagrisi): ders/teneffus ayrimi tamamen kalktigi icin bu
        //  ayri 1 dk'lik gorev GEREKSIZ hale geldi. Olusturma metodu (YoutubeGoreviKur) BILEREK
        //  SILINDI -- sadece kaldirma kaliyor, eski kurulumlardaki gorevi temizlemek icin.
        // ---------------------------------------------------------------
        public static void YoutubeGoreviKaldir()
        {
            GorevSil(AdYoutube);
            Logger.WriteFileOnly("kurulum.txt", "YouTube teneffus kisiti gorevi kaldirildi.");
        }

        // ---------------------------------------------------------------
        //  Durum / kaldirma
        // ---------------------------------------------------------------
        /// <summary>Haftalik AI gorevi schtasks'ta kurulu mu? ("AI durumu" butonu icin.)</summary>
        public static bool AiHaftalikGoreviKuruluMu()
        {
            return NativeRunner.Run("schtasks.exe", "/query /tn \"" + Klasor + AdAiHaftalik + "\"", null, false) == 0;
        }

        public static string GorevDurumu()
        {
            var sb = new StringBuilder();
            foreach (var ad in new[] { AdHafif, AdAgir, AdOyun, AdYoutube, AdAiHaftalik, AdGuncelleme, AdDuvarKagidi })
            {
                int kod = NativeRunner.Run("schtasks.exe", "/query /tn \"" + Klasor + ad + "\"", null, false);
                sb.AppendLine("  " + ad + " : " + (kod == 0 ? "KURULU" : "yok"));
            }
            return sb.ToString().TrimEnd();
        }

        /// <summary>v109: Disk acil temizligini AYRI bir SYSTEM gorevi olarak HEMEN calistirir
        /// (5 dk'lik denetim gorevinin 10 dk sure sinirindan ve surec agacindan BAGIMSIZ).
        /// Tetikleyicisi gecmiste kalmis tek seferlik bir zamandir -- kendiliginden bir daha calismaz.</summary>
        public static bool DiskAcilGoreviCalistir()
        {
            const string log = "akilli_bakim.txt";
            string tetik = "    <TimeTrigger>\r\n      <StartBoundary>2000-01-01T00:00:00</StartBoundary>\r\n      <Enabled>true</Enabled>\r\n    </TimeTrigger>";
            string xml = GorevXml(
                aciklama: "Unsal Bakim: Disk kritik seviyedeyken acil temizlik (tek seferlik, kisisel dosyalara dokunmaz).",
                tetik: tetik, arguman: "/disk-acil",
                bostaDk: 1, sureSaat: 2, pildeCalisir: true, idleGerekli: false, gizli: true);
            XmlIleKur(AdDiskAcil, xml, log);
            return NativeRunner.Run("schtasks.exe", "/run /tn \"" + Klasor + AdDiskAcil + "\"", log, false) == 0;
        }

        public static void TumGorevleriKaldir()
        {
            foreach (var ad in new[] { AdOyun, AdHafif, AdAgir, AdOyunKaldir, AdYoutube, AdAiHaftalik, AdGuncelleme, AdDuvarKagidi, AdDiskAcil })
                GorevSil(ad);
            Logger.Write("kurulum.txt", "Tum UnsalBakim gorevleri kaldirildi.");
        }

        private static void GorevSil(string ad)
        {
            NativeRunner.Run("schtasks.exe", "/delete /tn \"" + Klasor + ad + "\" /f", "kurulum.txt", false);
        }

        // ---------------------------------------------------------------
        //  XML ile gorev kurma (idle/gizli ayarlari korunur)
        // ---------------------------------------------------------------
        private static void XmlIleKur(string ad, string xml, string log)
        {
            string tn = Klasor + ad;
            // v109 duzeltme (Ali Bey'in canli testi: "'...\\gorev\\Guncelleme_Kontrol.xml' yoluna erisim
            // reddedildi"): ONCEDEN gorev ONCE siliniyor, SONRA XML yaziliyordu -- yazma basarisiz
            // olunca gorev silinmis ve YENIDEN KURULMAMIS kaliyordu (saatlik guncelleme gorevi
            // kayboluyordu!). Artik: (1) XML ONCE yazilir; (2) ProgramData\\...\\gorev'e yazilamazsa
            // (izin / guvenlik yazilimi) Windows'un gecici klasorune yazilir; (3) ancak XML hazirsa
            // eski gorev silinip yenisi kurulur. Hicbir yere yazilamazsa ESKI gorev OLDUGU GIBI kalir.
            string dosya = Path.Combine(XmlDizin, ad + ".xml");
            bool gecici = false;
            try { File.WriteAllText(dosya, xml, new UnicodeEncoding(false, true)); } // schtasks /xml UTF-16 ister
            catch (Exception ex1)
            {
                try
                {
                    dosya = Path.Combine(Path.GetTempPath(), "UB_" + ad + "_" + Guid.NewGuid().ToString("N") + ".xml");
                    File.WriteAllText(dosya, xml, new UnicodeEncoding(false, true));
                    gecici = true;
                    Logger.WriteFileOnly(log, "[!] Gorev XML'i ProgramData'ya yazilamadi (" + ex1.Message + ") -- gecici klasor kullanildi.");
                }
                catch (Exception ex2)
                {
                    Logger.Write(log, "[!] Gorev '" + ad + "' GUNCELLENEMEDI (XML yazilamadi: " + ex2.Message + ") -- mevcut gorev oldugu gibi birakildi.");
                    return;
                }
            }
            NativeRunner.Run("schtasks.exe", "/delete /tn \"" + tn + "\" /f", log, false);
            // v45: /create'in cikis kodu ONCEDEN hic kontrol edilmiyordu VE ciktiyiLogla=false
            // oldugu icin basarisizlikta HICBIR IZ (log satiri dahi) kalmiyordu -- "cikis kodu yok
            // sayilip basari varsayiliyor" sinifindan, sessiz, teshisi en zor olasi hata (TUM
            // zamanlanmis gorevler -- bakim, oyun engelleme, guncelleme, AI haftalik -- bu TEK
            // metottan geciyor). Davranis DEGISMEDI (hala sessiz calisiyor, ekranda uyari cikmaz),
            // sadece basarisizlikta artik log dosyasina AC IK bir satir dusuyor.
            int kod = NativeRunner.Run("schtasks.exe", "/create /tn \"" + tn + "\" /xml \"" + dosya + "\" /f", log, false);
            if (gecici) { try { File.Delete(dosya); } catch { } }
            if (kod != 0)
            {
                try { Logger.Write(log, "[!] Gorev olusturulamadi: '" + ad + "' (schtasks cikis kodu: " + kod + ")"); }
                catch { }
            }
        }

        private static string GunlukTetik(string saat)
        {
            return "    <CalendarTrigger>\r\n" +
                   "      <StartBoundary>" + Baslangic(saat) + "</StartBoundary>\r\n" +
                   "      <Enabled>true</Enabled>\r\n" +
                   "      <ScheduleByDay><DaysInterval>1</DaysInterval></ScheduleByDay>\r\n" +
                   "    </CalendarTrigger>";
        }

        private static string HaftalikTetik(string enGun, string saat)
        {
            return "    <CalendarTrigger>\r\n" +
                   "      <StartBoundary>" + Baslangic(saat) + "</StartBoundary>\r\n" +
                   "      <Enabled>true</Enabled>\r\n" +
                   "      <ScheduleByWeek>\r\n" +
                   "        <DaysOfWeek><" + enGun + " /></DaysOfWeek>\r\n" +
                   "        <WeeksInterval>1</WeeksInterval>\r\n" +
                   "      </ScheduleByWeek>\r\n" +
                   "    </CalendarTrigger>";
        }

        // Hemen baslayan (zaman) + yeniden baslatinca baslayan (boot), her ikisi N dk tekrarli
        private static string OyunTetik(int dakika)
        {
            string simdi = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
            string tekrar = "        <Repetition><Interval>PT" + dakika + "M</Interval><StopAtDurationEnd>false</StopAtDurationEnd></Repetition>\r\n";
            return "    <TimeTrigger>\r\n" +
                   "      <StartBoundary>" + simdi + "</StartBoundary>\r\n" +
                   "      <Enabled>true</Enabled>\r\n" +
                   tekrar +
                   "    </TimeTrigger>\r\n" +
                   "    <BootTrigger>\r\n" +
                   "      <Enabled>true</Enabled>\r\n" +
                   "    </BootTrigger>";
        }

        // ---------------------------------------------------------------
        //  v102: Duvar kagidi (bilgisayar adi + okul logosu) -- ETKILESIMLI oturum gorevi
        // ---------------------------------------------------------------
        /// <summary>
        /// Diger TUM gorevlerin aksine bu gorev SYSTEM (S-1-5-18) OLARAK DEGIL, oturum acan
        /// HANGI kullanici olursa olsun o kullanicinin KENDI etkilesimli oturumunda calisir
        /// (Principal: BUILTIN\Users grubu + InteractiveToken + LeastPrivilege -- Gorev
        /// Zamanlayici arayuzundeki "Oturum acildiginda" tetikleyicisinde "Belirli kullanici"
        /// degil "Herhangi bir kullanici" secimiyle BIREBIR ayni XML karsiligi). Bunun nedeni:
        /// SystemParametersInfo(SPI_SETDESKWALLPAPER) SYSTEM/Oturum 0 baglaminda cagrilirsa
        /// etkilesimli kullanicinin gordugu masaustune HICBIR ETKISI OLMAZ (oturum izolasyonu).
        /// Tetikleyici IKILI: (1) LogonTrigger (UserId YOK -> HERHANGI bir oturum acilisinda
        /// ates alir), (2) 20 dk'da bir tekrarlanan TimeTrigger (bir ogrenci oturumu uzun surerse
        /// de duvar kagidini periyodik olarak tazeler -- Ayarlar'dan/sag tik Kisisellestir'den
        /// degistirilse bile kisa surede geri doner).
        /// </summary>
        public static void DuvarKagidiGoreviKur(bool gizli)
        {
            const string log = "kurulum.txt";
            string xml = DuvarKagidiXml(gizli);
            XmlIleKur(AdDuvarKagidi, xml, log);
            Logger.WriteFileOnly(log, "Duvar kagidi (bilgisayar adi + logo) etkilesimli-oturum gorevi kuruldu" + (gizli ? " [gizli]" : "") + ".");
        }

        private static string DuvarKagidiXml(bool gizli)
        {
            string simdi = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-16\"?>\r\n");
            sb.Append("<Task version=\"1.2\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">\r\n");
            sb.Append("  <RegistrationInfo>\r\n");
            sb.Append("    <Description>" + Kacis("Unsal Bakim: oturum acan HER kullanici icin (SYSTEM degil, kendi etkilesimli oturumunda) bilgisayar adi + okul logosu duvar kagidini uygular; oturum acilisinda ve her 20 dk'da bir tazelenir.") + "</Description>\r\n");
            sb.Append("    <Author>Unsal Bakim</Author>\r\n");
            sb.Append("  </RegistrationInfo>\r\n");
            sb.Append("  <Triggers>\r\n");
            sb.Append("    <LogonTrigger>\r\n");
            sb.Append("      <Enabled>true</Enabled>\r\n");
            sb.Append("    </LogonTrigger>\r\n");
            sb.Append("    <TimeTrigger>\r\n");
            sb.Append("      <StartBoundary>" + simdi + "</StartBoundary>\r\n");
            sb.Append("      <Enabled>true</Enabled>\r\n");
            sb.Append("      <Repetition><Interval>PT20M</Interval><StopAtDurationEnd>false</StopAtDurationEnd></Repetition>\r\n");
            sb.Append("    </TimeTrigger>\r\n");
            sb.Append("  </Triggers>\r\n");
            sb.Append("  <Principals>\r\n");
            sb.Append("    <Principal id=\"Author\">\r\n");
            sb.Append("      <GroupId>S-1-5-32-545</GroupId>\r\n"); // BUILTIN\Users -- oturum acan HERHANGI bir kullanici
            sb.Append("      <RunLevel>LeastPrivilege</RunLevel>\r\n");
            sb.Append("    </Principal>\r\n");
            sb.Append("  </Principals>\r\n");
            sb.Append("  <Settings>\r\n");
            sb.Append("    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>\r\n");
            sb.Append("    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>\r\n");
            sb.Append("    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>\r\n");
            sb.Append("    <AllowHardTerminate>true</AllowHardTerminate>\r\n");
            sb.Append("    <StartWhenAvailable>true</StartWhenAvailable>\r\n");
            sb.Append("    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>\r\n");
            sb.Append("    <AllowStartOnDemand>true</AllowStartOnDemand>\r\n");
            sb.Append("    <Enabled>true</Enabled>\r\n");
            sb.Append("    <Hidden>" + (gizli ? "true" : "false") + "</Hidden>\r\n");
            sb.Append("    <RunOnlyIfIdle>false</RunOnlyIfIdle>\r\n");
            sb.Append("    <WakeToRun>false</WakeToRun>\r\n");
            sb.Append("    <ExecutionTimeLimit>PT2M</ExecutionTimeLimit>\r\n");
            sb.Append("    <Priority>7</Priority>\r\n");
            sb.Append("  </Settings>\r\n");
            sb.Append("  <Actions Context=\"Author\">\r\n");
            sb.Append("    <Exec>\r\n");
            // v109: bizim exe'miz DEGIL, Windows'un kendi wscript.exe'si (bkz. MasaustuDuvarKagidi.BetikYolu) --
            // ogrenci makinesinde Windows Bakim klasoru ogrenciye kapali oldugu icin.
            sb.Append("      <Command>" + Kacis(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "wscript.exe")) + "</Command>\r\n");
            sb.Append("      <Arguments>" + Kacis("//B //Nologo \"" + MasaustuDuvarKagidi.BetikYolu + "\"") + "</Arguments>\r\n");
            sb.Append("    </Exec>\r\n");
            sb.Append("  </Actions>\r\n");
            sb.Append("</Task>\r\n");
            return sb.ToString();
        }

        private static string GorevXml(string aciklama, string tetik, string arguman,
                                       int bostaDk, int sureSaat, bool pildeCalisir, bool idleGerekli, bool gizli,
                                       int oncelik = 7, bool bostaBitinceDur = false)
        {
            string disallowBat = pildeCalisir ? "false" : "true";
            string stopBat = pildeCalisir ? "false" : "true";

            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-16\"?>\r\n");
            sb.Append("<Task version=\"1.2\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">\r\n");
            sb.Append("  <RegistrationInfo>\r\n");
            sb.Append("    <Description>" + Kacis(aciklama) + "</Description>\r\n");
            sb.Append("    <Author>Unsal Bakim</Author>\r\n");
            sb.Append("  </RegistrationInfo>\r\n");
            sb.Append("  <Triggers>\r\n");
            sb.Append(tetik + "\r\n");
            sb.Append("  </Triggers>\r\n");
            sb.Append("  <Principals>\r\n");
            sb.Append("    <Principal id=\"Author\">\r\n");
            sb.Append("      <UserId>S-1-5-18</UserId>\r\n");           // SYSTEM
            sb.Append("      <RunLevel>HighestAvailable</RunLevel>\r\n");
            sb.Append("    </Principal>\r\n");
            sb.Append("  </Principals>\r\n");
            sb.Append("  <Settings>\r\n");
            sb.Append("    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>\r\n");
            sb.Append("    <DisallowStartIfOnBatteries>" + disallowBat + "</DisallowStartIfOnBatteries>\r\n");
            sb.Append("    <StopIfGoingOnBatteries>" + stopBat + "</StopIfGoingOnBatteries>\r\n");
            sb.Append("    <AllowHardTerminate>true</AllowHardTerminate>\r\n");
            sb.Append("    <StartWhenAvailable>true</StartWhenAvailable>\r\n");
            sb.Append("    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>\r\n");
            if (idleGerekli)
            {
                sb.Append("    <IdleSettings>\r\n");
                sb.Append("      <Duration>PT" + bostaDk + "M</Duration>\r\n");
                sb.Append("      <WaitTimeout>PT" + (pildeCalisir ? 12 : 8) + "H</WaitTimeout>\r\n");
                // v109: agir bakimda (bostaBitinceDur) biri fareye/klavyeye dokunursa gorev DURUR ve
                // bilgisayar tekrar bosta kalinca YENIDEN baslar -- ogrenci yavaslik hissetmesin.
                sb.Append("      <StopOnIdleEnd>" + (bostaBitinceDur ? "true" : "false") + "</StopOnIdleEnd>\r\n");
                sb.Append("      <RestartOnIdle>" + (bostaBitinceDur ? "true" : "false") + "</RestartOnIdle>\r\n");
                sb.Append("    </IdleSettings>\r\n");
            }
            sb.Append("    <AllowStartOnDemand>true</AllowStartOnDemand>\r\n");
            sb.Append("    <Enabled>true</Enabled>\r\n");
            sb.Append("    <Hidden>" + (gizli ? "true" : "false") + "</Hidden>\r\n");
            sb.Append("    <RunOnlyIfIdle>" + (idleGerekli ? "true" : "false") + "</RunOnlyIfIdle>\r\n");
            sb.Append("    <WakeToRun>false</WakeToRun>\r\n");
            if (sureSaat > 0)
                sb.Append("    <ExecutionTimeLimit>PT" + sureSaat + "H</ExecutionTimeLimit>\r\n");
            else
                sb.Append("    <ExecutionTimeLimit>PT10M</ExecutionTimeLimit>\r\n");
            sb.Append("    <Priority>" + oncelik + "</Priority>\r\n");
            sb.Append("  </Settings>\r\n");
            sb.Append("  <Actions Context=\"Author\">\r\n");
            sb.Append("    <Exec>\r\n");
            sb.Append("      <Command>" + Kacis(ExeKullan) + "</Command>\r\n");
            sb.Append("      <Arguments>" + arguman + "</Arguments>\r\n");
            sb.Append("    </Exec>\r\n");
            sb.Append("  </Actions>\r\n");
            sb.Append("</Task>\r\n");
            return sb.ToString();
        }

        private static string Baslangic(string saat)
        {
            var ts = SaatParse(saat);
            var gun = DateTime.Today.AddDays(1).Add(ts);
            return gun.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
        }

        private static TimeSpan SaatParse(string saat)
        {
            if (TimeSpan.TryParseExact((saat ?? "08:00").Trim(), "hh\\:mm", CultureInfo.InvariantCulture, out var ts))
                return ts;
            return new TimeSpan(8, 0, 0);
        }

        private static string Saat(string saat) => SaatParse(saat).ToString("hh\\:mm");

        private static string Kacis(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }
    }
}
