using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using UnsalBakim.Core;

namespace UnsalBakim
{
    internal static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            Logger.Init();

            // v112: Ortak proxy -- HER surecte (panel + SYSTEM gorevleri) baslangicta kayitli proxy'yi
            // varsayilan yap. Boylece proxy'li ortamlarda SYSTEM gorevleri de ag istegi yapabilir.
            // (Proxy yoksa dogrudan baglanti; okul/FATIH bu sekilde.)
            try { AgProxy.Uygula(); } catch { }

            // --- Sifre gizleme araci: kurulum.ini icin sifreli deger uretir ---
            //     Kullanim:  UnsalBakim.exe /sifrele  (ister komut satiri, ister cift tik)
            if (args.Length >= 1 && args[0].Equals("/sifrele", StringComparison.OrdinalIgnoreCase))
            {
                SifreleAraci(args.Length >= 2 ? args[1] : null);
                return 0;
            }

            // v102: Duvar kagidi (bilgisayar adi + logo) etkilesimli-oturum uygulamasi -- bu
            // BILEREK asagidaki yonetici yetkisi yukseltmesinden ONCE, dogrudan cagiran
            // kullanicinin KENDI oturumunda/yetkisinde calisir (/sifrele ile AYNI desen).
            // Nedeni: (1) ogrenci/ogretmen hesabi standart (yonetici olmayan) olsa bile boyle
            // hicbir UAC istemi CIKMAZ -- zaten bu komutu tetikleyen Duvar_Kagidi Windows gorevi
            // SYSTEM degil, BUILTIN\Users + InteractiveToken ile kurulu (bkz.
            // TaskHelper.DuvarKagidiGoreviKur); (2) SystemParametersInfo cagrisi ancak dogru
            // (etkilesimli) oturum baglaminda bir ETKI yaratir -- SYSTEM'e yukseltilseydi Oturum 0
            // izolasyonu yuzunden ekrana HICBIR SEY yapmazdi (bkz. MasaustuDuvarKagidi.cs basindaki
            // aciklama).
            if (args.Length >= 1 && args[0].Equals("/duvar-kagidi-uygula", StringComparison.OrdinalIgnoreCase))
            {
                try { MasaustuDuvarKagidi.UygulaBuMakineyeAktifOturumda(); } catch { }
                return 0;
            }

            // --- Yonetici yetkisi ---
            // Program asInvoker (standart) baslar. Yetki gerekiyorsa kendini yukseltir:
            //   kurulum.ini'de hesap varsa SESSIZCE (sifre sorulmaz), yoksa normal UAC ile.
            bool icCagri = false;
            {
                var kalan = new System.Collections.Generic.List<string>(args);
                if (kalan.Remove(Yetki.IC)) icCagri = true;
                args = kalan.ToArray();
            }
            if (!Yetki.Yukseltilmis())
            {
                // icCagri=true ise sessiz yukseltme zaten denendi -> tekrar deneme, dogrudan UAC/son
                bool baslatildi = Yetki.YukseltVeCalistir(args, icCagri);
                // v41: ONCEDEN bu sonuc hic kontrol edilmiyordu -- UAC reddedilir/kapanirsa veya
                // antivirus/SmartScreen yukseltmeyi engellerse program HICBIR MESAJ GOSTERMEDEN
                // sessizce kapaniyordu (USB'deki .bat dosyalarindan dogrudan calistirildiginda
                // goruldu; panel zaten yukseltilmis actigi icin bu kod hic calismiyordu, o yuzden
                // sadece .bat'ta fark ediliyordu). Artik basarisizlikta GORUNUR bir uyari veriliyor.
                if (!baslatildi)
                {
                    try
                    {
                        MessageBox.Show(
                            "Yonetici izni alinamadi, islem baslatilamadi.\r\n\r\n" +
                            "Olasi sebepler:\r\n" +
                            "- Yonetici izni (UAC) penceresinde \"Evet\" yerine \"Hayir\" secildi\r\n" +
                            "  ya da pencere acilir acilmaz kapandi/kacirildi\r\n" +
                            "- Antivirus veya Windows SmartScreen bu islemi engelledi\r\n\r\n" +
                            "Tekrar deneyin: cift tikladiktan sonra cikan sari/mavi UAC\r\n" +
                            "penceresinde \"Evet\" secmeyi unutmayin. Antivirus bir uyari/\r\n" +
                            "bildirim gosterdiyse ekran goruntusu alip iletin.",
                            "Unsal Bakim - Yetki hatasi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    catch { }
                    try { Logger.WriteFileOnly("kurulum.txt", "[Yetki] Yukseltme basarisiz veya iptal edildi (args: " + ArgLogIcin(args) + ")."); }
                    catch { }
                }
                return 0;  // bu (yetkisiz) surec cikar; basarili olduysa yukseltilmis kopya isi yapar
            }

            // OGRENCI exe'si: dosya adi "ogrenci" iceriyorsa, arguman olmasa da
            // menusuz sekilde HER SEYI otomatik kurar ve mesaj gosterir.
            string exeAdi = "";
            try { exeAdi = Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().Location).ToLowerInvariant(); } catch { }
            if (args.Length == 0 && exeAdi.Contains("ogrenci"))
            {
                OgrenciTamKur();
                return 0;
            }

            // Arka plan / sessiz modlar (zamanlanmis gorevler bunlari cagirir)
            if (args.Length > 0)
            {
                string a = args[0].ToLowerInvariant();
                var ayar = new AppSettings(); ayar.Load();
                switch (a)
                {
                    case "/ogrenci":
                        OgrenciTamKur();
                        return 0;
                    case "/oyun-kaldir":
                        // Eski gorevlerle uyumluluk: ekran acmadan ayni tam denetim akisini calistir.
                        OyunDenetimDongusuCalistir();
                        return 0;
                    case "/oyun-engelle":
                        // Her 10 dakikada bir: surec engeli + web oyun sitesi politikasi + log taramasi +
                        // yalnizca guvenli whitelist eslesmesi olan kurulu oyunlarin sessiz kaldirma denemesi.
                        OyunDenetimDongusuCalistir();
                        return 0;
                    case "/youtube-saat":
                        // v57: Bu komutu tetikleyen ayri 1 dk'lik Windows gorevi KALDIRILDI
                        // (TaskHelper.YoutubeGoreviKur silindi, Kur() artik YoutubeGoreviKaldir()
                        // cagiriyor) -- ders/teneffus ayrimi kalktigi icin GEREKSIZ hale geldi; 5
                        // dk'lik birlesik oyun dongusu zaten her turda YoutubeKisit.SaatiUygula
                        // cagiriyor. Bu case SADECE eski bir kurulumdan kalma gorevin (kaldirma
                        // henuz calismadan once) bir kez daha calismasi ihtimaline karsi BILEREK
                        // birakildi -- zararsiz, ayni "filo ?? yerel ayar" sonucunu uretir.
                        try
                        {
                            var ayarY = new AppSettings(); ayarY.Load();
                            var filoY = FiloPolitika.Durum(false);
                            YoutubeKisit.SaatiUygula(filoY.Youtube ?? ayarY.YoutubeKisitli);
                        }
                        catch { }
                        return 0;
                    case "/ai-haftalik":
                        // Haftada bir (Pzt sabahi, okul acilmadan once): AI kontrol ve guncelleme.
                        // Ekransiz calisir; sonuc sadece log'a yazilir (MessageBox/UI YOK).
                        // v52 duzeltmesi: bu, zamanlanmis/otomatik bir gorev -- diger otomatik denetim
                        // noktalariyla (10 dk dongu, /youtube-saat, kur-sessiz) AYNI kurala uymali:
                        // filo askiya alinmissa YENI oyun/vpn/site tespit edip ENGELLEMEMELI. Ayarlar
                        // sekmesindeki "AI kontrol ve guncelle" butonu (manuel, MainForm.cs) KAPSAM
                        // DISI -- bir yoneticinin elle, bilerek tetikledigi islem her zaman gecerlidir
                        // (projedeki "manuel eylem otomasyonla sessizce geri alinmaz" kurali).
                        try
                        {
                            Logger.WriteFileOnly("oyun_log.txt", "[Haftalik AI] " + YapayZeka.KontrolVeGuncelle().Replace("\r\n", " | "));
                        }
                        catch (Exception ex) { Logger.WriteFileOnly("oyun_log.txt", "[Haftalik AI] hata: " + ex.Message); }
                        return 0;
                    case "/disk-acil":
                        // v109: disk kritik seviyedeyken AYRI SYSTEM gorevi olarak (bkz. AkilliBakim).
                        try { AkilliBakim.DiskAcilTemizlik(); }
                        catch (Exception ex) { Logger.WriteFileOnly("akilli_bakim.txt", "Disk acil temizlik hatasi: " + ex.Message); }
                        try { var ayarD = new AppSettings(); ayarD.Load(); MakineLog.Gonder(ayarD, false); } catch { }
                        return 0;
                    case "/guncelleme-kontrol":
                        // v109: onceki bir kurulum yarida kaldiysa once onu tamamla.
                        try { Installer.YarimKalanKurulumuTamamla(); } catch { }
                        // Saatte bir (SYSTEM gorevi): Drive'da yeni surum var mi bak; varsa indirip
                        // kurulu kopyanin uzerine koy ve "/kur-sessiz" ile gizli kurulumu/gorevleri
                        // de tazele. Ekransiz calisir; sonuc sadece log'a yazilir (MessageBox/UI YOK).
                        try { Logger.WriteFileOnly("kurulum.txt", "[Guncelleme] " + GuncellemeKontrol.GuncellemeKontroluSessiz()); }
                        catch (Exception ex) { Logger.WriteFileOnly("kurulum.txt", "[Guncelleme] hata: " + ex.Message); }
                        // Ayni saatlik dongude: Drive'daki yeni.txt'de yeni bir 'yonetici' sifresi
                        // var mi bak; varsa sessizce uygula (ozellik kurulmadiysa no-op).
                        try { YoneticiSifre.KontrolEtVeUygula(); }
                        catch (Exception ex) { Logger.WriteFileOnly("kurulum.txt", "[YoneticiSifre] hata: " + ex.Message); }
                        return 0;
                    case "/sinav-kaldir":
                        // v57: Sinav Modu'nun TEK SEFERLIK, suresi dolunca kendiliginden calisan
                        // Windows gorevi tarafindan cagrilir (bkz. Core/InternetEngeli.cs). Ag'a
                        // HIC ihtiyaci yoktur -- sadece firewall kuralini ve kendi gorevini siler.
                        try { InternetEngeli.KaldirSessiz(); Logger.WriteFileOnly("sinav_modu.txt", "Sinav modu suresi doldu, internet engeli otomatik kaldirildi."); }
                        catch (Exception ex) { Logger.WriteFileOnly("sinav_modu.txt", "Otomatik kaldirma hatasi: " + ex.Message); }
                        return 0;
                    case "/kur-sessiz":
                        // Otomatik guncellemeden sonra SYSTEM baglaminda ekransiz tetiklenir
                        // (GuncellemeKontrol.GuncellemeKontroluSessiz -> "/kur-sessiz").
                        // v40: ARTIK OyunEngelle/VpnEngelle/YoutubeKisitli burada HICBIR SEKILDE
                        // degistirilmiyor -- sadece makinede o an KAYITLI olan ayarlar (ister ilk
                        // kurulumda secilen OGRENCI/OGRETMEN profilinden, ister Ayarlar/Oyun Kontrol
                        // sekmesinden sonradan elle degistirilmis olsun) yeniden uygulaniyor. Boylece
                        // bir makinede (orn. ogretmen bilgisayarinda) bu ayarlari elle kapatirsan,
                        // otomatik guncellemeler bunu BIR DAHA geri acmaz.
                        try
                        {
                            string yer = Installer.Kur(ayar);
                            try { if (ayar.VpnEngelle) VpnKontrol.Engelle(); else VpnKontrol.KaldirEngel(); } catch { }
                            try { YoutubeKisit.SaatiUygula(); } catch { }
                            try { if (ayar.AiSiteEngelle) AiSiteEngelle.Engelle(); else AiSiteEngelle.KaldirEngel(); } catch { }
                            Logger.WriteFileOnly("kurulum.txt", "[Guncelleme] Sessiz KUR tamamlandi (" + ayar.MakineTuru + "): " + yer);
                            // v109: sonucu merkeze bildir + MakineLog'u guncelle (filo ayni anda
                            // yuklenmesin diye makineye gore 0-90 sn kaydirilarak).
                            try { DriveLog.Gonder("kurulum_tamam", License.Surum + " sessiz KUR tamamlandi (" + ayar.MakineTuru + ")."); } catch { }
                            try { MakineLog.Gonder(ayar, true); } catch { }
                        }
                        catch (Exception ex)
                        {
                            Logger.WriteFileOnly("kurulum.txt", "[Guncelleme] Sessiz KUR hatasi: " + ex.Message);
                        }
                        return 0;
                    case "/hafif":
                        // v57: Gorev artik TENEFFUS saatlerine gore degil, bilgisayarin ACILISINDA
                        // tetiklenir (BootTrigger) -- ders programindan tamamen bagimsiz. Ayni gun
                        // icinde birden fazla acilis olursa (ogrenci bilgisayari kapatip actiginda)
                        // tekrar tekrar calismasin diye gunluk bir damga dosyasiyla korunur.
                        try
                        {
                            string damgaDizin = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "UnsalBakim");
                            Directory.CreateDirectory(damgaDizin);
                            string damga = Path.Combine(damgaDizin, "son_hafif_bakim.txt");
                            string bugun = DateTime.Now.ToString("yyyy-MM-dd");
                            string onceki = File.Exists(damga) ? File.ReadAllText(damga).Trim() : "";
                            if (onceki == bugun)
                            {
                                Logger.WriteFileOnly("hafif.txt", "Hafif bakim bugun (" + bugun + ") zaten calisti, acilis bazinda tekrar atlaniyor.");
                                return 0;
                            }
                            File.WriteAllText(damga, bugun);
                        }
                        catch { /* damga yazilamazsa bile bakim calissin, sadece korumasiz kalir */ }
                        Maintenance.HafifBakim(ayar, true);   // TRIM: gunde en fazla 1 kez calistigi icin hep uygulanir
                        // v114 (Ali Bey'in istegi, 17 Kasim 2026): ayni gunde-1-kez acilis bloğunda,
                        // sabah otomatik Teshis -- panelden komut beklemeden calisir, sonucu Sheet'e
                        // gonderir. Hata/ag sorunu HafifBakim'i ETKILEMEZ (try/catch kendi icinde).
                        try { Teshis.SabahOtomatikCalistirVeGonder(ayar); } catch { }
                        return 0;
                    case "/agir":
                        Maintenance.AgirBakim(ayar);
                        return 0;
                    // v40: tek, genel bir "gizli kurulum" secenegi yerine, kurulum HER ZAMAN bir
                    // PROFIL ile yapilir -- "/kur-ogrenci" (tum kisitlamalar acik) veya "/kur-ogretmen"
                    // (kisitlama yok). Ikisi de HER ZAMAN gizli kurulum yapar (Program Files altinda
                    // gizli klasor + SYSTEM gorevleri); ayrica "gizli olmayan" bir kurulum secenegi YOK.
                    // "/kur" eski USB/kisayollarla uyumluluk icin "/kur-ogrenci" ile ayni (guvenli varsayilan).
                    case "/kur":
                    case "/kur-ogrenci":
                        KurGizliProfilVeMesajGoster(ayar, "ogrenci");
                        return 0;
                    case "/kur-ogretmen":
                        KurGizliProfilVeMesajGoster(ayar, "ogretmen");
                        return 0;
                    case "/kaldir":
                        try
                        {
                            Installer.Kaldir();
                            MessageBox.Show("Kurulum kaldirildi (gorevler, oyun engelleri ve gizli klasor).",
                                "Unsal Bakim", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("Kaldirma hatasi: " + ex.Message,
                                "Unsal Bakim", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                        return 0;
                }
            }

            // v112: Panel KULLANICI oturumunda calistigi icin, kullanicinin WinINET proxy ayarini
            // okuyup ProgramData'ya yazar -- SYSTEM gorevleri ag isteklerinde bunu kullanir.
            try { AgProxy.PanelProxyYaz(); } catch { }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
            return 0;
        }

        /// <summary>Arka plan oyun dongusu. Sonuclar dosya loglarina yazilir; MessageBox/UI raporu acilmaz.
        /// v92: "Askiya Al" (filo-genelinde TUM kisitlamalari gecici durdurma) ozelligi Ali Bey'in
        /// acik istegiyle TAMAMEN KALDIRILDI -- bu dongu artik kosulsuz calisir.</summary>
        private static void OyunDenetimDongusuCalistir()
        {
            // v57 (3. revizyon): Politika+Sinav+Kapat icin ayri ayri 3 ag istegi atmak yerine TEK
            // birlesik istek (FiloDurum.Al) -- Ali Bey'in "lab sayisi buyurse ne yapariz" sorusuna
            // yanit: fleet buyudukce (daha fazla lab/makine) Apps Script'in hesap-basina es-zamanli
            // calistirma limitine (30) ayni anda çarpan istek sayisini 3'e 1 dusurur. Sinav/Kapat
            // askiya-al'dan BAGIMSIZ, hep kontrol edilir (bir sinav/kapat komutu sirasinda filo
            // "askiya alinmis" olsa dahi bu ikisini etkilemez).
            // v109: onceki kurulum/guncelleme yarida kaldiysa (isaret 15 dk'dan eski) once onu tamamla.
            try { Installer.YarimKalanKurulumuTamamla(); } catch { }

            var filoToplu = FiloDurum.Al(true);

            // v110 (Ali Bey: "makinelerin guncellemeleri de var, once guncelleme calismali"):
            // Drive'da yeni surum varsa (ve pilot izin veriyorsa) ONCE guncelle; bu turda Filo
            // komutlarini ESKI surum CALISTIRMAZ -- komutlar Sheet'te kalir, ~5 dk sonra YENI surum
            // uygular. (FiloDurum.Al once cagrildi ki pilot durumu taze olsun.)
            AppSettings ayarDongu = null;
            try { ayarDongu = new AppSettings(); ayarDongu.Load(); } catch { }
            try
            {
                if (GuncellemeKontrol.DonguGuncellemeKontrol(ayarDongu))
                {
                    Logger.WriteFileOnly("kurulum.txt", "[Dongu] Yeni surum indirildi; bu turdaki Filo komutlari yeni surume birakildi.");
                    return;
                }
            }
            catch (Exception ex) { Logger.WriteFileOnly("kurulum.txt", "[Dongu] guncelleme kontrol hatasi: " + ex.Message); }

            // v110: Uzaktan acil koruma anahtari -- SADECE v110 Apps Script'ten GECERLI yanit geldiyse.
            if (filoToplu.AgBasarili && filoToplu.V110Yanit)
            {
                try { KorumaAcil.Isle(filoToplu.KorumaKapali, true, ayarDongu); }
                catch (Exception ex) { Logger.WriteFileOnly("rol_guvenlik.txt", "Koruma acil kontrol hatasi: " + ex.Message); }
            }

            // v57 (4/5. revizyon): Ali Bey'in acik talebi -- Hedef="TUMU" (veya herhangi bir eslesme)
            // OGRETMEN makinesini filo'nun HICBIR zorlamasindan etkilenmez hale getirir: Sinav Modu
            // (internet askiya alma), Filodan Kapat VE Oyun/VPN/YouTube/AI/Agir-Bakim ZORUNLU
            // politikasi -- hepsi icin SADECE kendi yerel (AppSettings) ayari gecerli olur.
            bool ogretmenMuaf;
            try
            {
                var ayarTur = new AppSettings(); ayarTur.Load();
                ogretmenMuaf = string.Equals(ayarTur.MakineTuru, "ogretmen", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex) { ogretmenMuaf = false; Logger.WriteFileOnly("sinav_modu.txt", "MakineTuru okuma hatasi (muafiyet kontrolu): " + ex.Message); }

            if (!ogretmenMuaf)
            {
                try { InternetEngeli.FiloKontrolEtVeUygulaDeger(filoToplu.SinavBitis); }
                catch (Exception ex) { Logger.WriteFileOnly("sinav_modu.txt", "Filo sinav kontrolu (dongu) hatasi: " + ex.Message); }

                try { FiloKapat.FiloKontrolEtVeUygulaDeger(filoToplu.KapatZaman); }
                catch (Exception ex) { Logger.WriteFileOnly("filo_kapat.txt", "Filo kapatma kontrolu (dongu) hatasi: " + ex.Message); }

                // v89: "Yeniden baslat" -- Kapat ile AYNI ogretmen-muafiyet kuralina tabi
                // (ogretmen makinesi filo'dan zorla yeniden baslatilamaz).
                try { FiloYenidenBaslat.FiloKontrolEtVeUygulaDeger(filoToplu.YenidenBaslatZaman); }
                catch (Exception ex) { Logger.WriteFileOnly("filo_yenidenbaslat.txt", "Filo yeniden baslatma kontrolu (dongu) hatasi: " + ex.Message); }

                // v94: "Dosya gonder" + "Buyuk ekran mesaji" -- Kapat/YenidenBaslat ile AYNI
                // ogretmen-muafiyet kuralina tabi (ogretmen makinesine filodan zorla dosya
                // indirilmez/mesaj gosterilmez; Ali Bey zaten Hedef'i secerken bunu kontrol eder).
                try { FiloDosyaGonder.FiloKontrolEtVeUygulaDeger(filoToplu); }
                catch (Exception ex) { Logger.WriteFileOnly("filo_dosya_gonder.txt", "Filo dosya kontrolu (dongu) hatasi: " + ex.Message); }
                try { FiloMesajGonder.FiloKontrolEtVeUygulaDeger(filoToplu); }
                catch (Exception ex) { Logger.WriteFileOnly("filo_mesaj_gonder.txt", "Filo mesaj kontrolu (dongu) hatasi: " + ex.Message); }
            }
            else
            {
                Logger.WriteFileOnly("sinav_modu.txt", "OGRETMEN makinesi: filo sinav/kapat/yenidenbaslat komutu bu turda uygulanmadi (yerel ayar gecerli).");
            }

            // v80 (Ali Bey'in istegi: "Filo menusune envanter gonder koyalim"): Sinav/Kapat'in
            // aksine bu bir KISITLAMA degil, sadece bilgi gonderme -- OGRETMEN muafiyetinin
            // (yukaridaki if/else) DISINDA, HER makinede (ogretmen dahil) kontrol edilir.
            try
            {
                var ayarEnvanter = new AppSettings(); ayarEnvanter.Load();
                EnvanterIste.FiloKontrolEtVeUygulaDeger(filoToplu.EnvanterIsteZaman, ayarEnvanter);
            }
            catch (Exception ex) { Logger.WriteFileOnly("envanter_iste.txt", "Filo envanter istegi kontrolu (dongu) hatasi: " + ex.Message); }

            // v100 (bagimsiz inceleme raporu, 11 Kasim 2026): rapor, OyunKaldirici.OtomatikHedefOyunlariKaldir()'un
            // hicbir politika kontrolu olmadan (ogretmenMuaf / OyunEngelle bakilmadan) cagirildigini
            // dogru tespit etti. Once bunu "efektifOyun" kapisina tasimistim (asagida); Ali Bey'e
            // sorduktan sonra acik karari: "Bence oyun kaldirma hepsinde olsun" -- yani
            // BaslangicTemizle ile AYNI felsefe: whitelist'teki bilinen oyun/hile araclari HER
            // makinede (ogretmen dahil, OyunEngelle ayarindan BAGIMSIZ) sessizce kaldirilsin. Bu,
            // kasitli ve ikinci kez teyit edilmis bir tasarim karari -- "engelleme" (GameControl,
            // OyunEngelle'ye bagli) ile "bilinen oyunu temizleme" (OyunKaldirici, HER ZAMAN)
            // BILEREK farkli iki kural. Tekrar kosulsuz hale getirildi, DEGISTIRILMEMELI.
            try { OyunLogKontrol.Tara(false); }
            catch (Exception ex) { Logger.WriteFileOnly("oyun_log.txt", "Zamanli oyun log tarama hatasi: " + ex.Message); }
            try { OyunKaldirici.OtomatikHedefOyunlariKaldir(); }
            catch (Exception ex) { Logger.WriteFileOnly("oyun_log.txt", "Zamanli sessiz oyun kaldirma hatasi: " + ex.Message); }
            // v102 (Ali Bey'in acik istegi, 11 Kasim 2026: "Bilgisayarlar acildiginda bilgisayar
            // ismi buyuk olarak yazsin altina Bilisim yazsin, hangi user olursa olsun ekranin
            // ortasinda ve buyuk"): Masaustu duvar kagidini (bilgisayar adi + okul logosu) HER
            // dongude, HER makinede (ogretmen dahil, hicbir ac/kapa ayarina BAGLI DEGIL) tazeler/
            // garanti eder -- BaslangicTemizle ve OyunKaldirici ile AYNI "kosulsuz temel davranis"
            // ailesine katilir. Asil ekrana yansima, burada degil, etkilesimli-oturum gorevinde
            // (bkz. MasaustuDuvarKagidi.cs) olur; burada sadece resim/HKLM/gorev tazelenir.
            try { MasaustuDuvarKagidi.OlusturVeUygula(); }
            catch (Exception ex) { Logger.WriteFileOnly("duvar_kagidi.txt", "Zamanli duvar kagidi olusturma hatasi: " + ex.Message); }
            // v106 (Ali Bey'in acik istegi, 11 Kasim 2026: "Kumar olanlarini ekle, 16 yas ve alti"):
            // Bilinen kumar/bahis sitelerini HER dongude, HER makinede (ogretmen dahil, hicbir
            // ac/kapa ayarina BAGLI DEGIL) engeller -- Oyun Engelleme ile AYNI "ogrenciler 16 yas
            // alti oldugu icin tercih degil, kosulsuz temel davranis" felsefesi.
            try { KumarSiteEngelle.Engelle(); }
            catch (Exception ex) { Logger.WriteFileOnly("kumar_site.txt", "Zamanli kumar site engeli hatasi: " + ex.Message); }
            // v55: Filo Yonetimi -- Oyun/VPN/YouTube/AI web sitesi icin TEK bir Drive okumasi
            // (FiloPolitika.Durum). Her ayar icin: filo politikasi varsa (ACIK/KAPALI) yerel ayarin
            // ONUNE GECER; yoksa (null) yerel AppSettings degeri gecerlidir -- v55 oncesiyle BIREBIR
            // ayni davranis. Not: Oyun engelleme artik burada da (onceden oldugu gibi kosulsuz
            // degil) OyunEngelle/filo bayragina gore uygulanir/kaldirilir.
            try
            {
                var a = new AppSettings(); a.Load();
                // v57 (5. revizyon): Ali Bey'in talebi -- Internet askiya/Kapat'ta oldugu gibi,
                // OGRETMEN makinesi Oyun/VPN/YouTube/AI/Agir-Bakim ZORUNLU filo politikasindan da
                // MUAF: filo ne derse desin, HER ZAMAN kendi yerel (AppSettings) ayari gecerli olur.
                // Bunu, filo'nun kendisini "bos/zorlamasiz" (hepsi null) saymakla yapiyoruz --
                // asagidaki "filo.X ?? a.X" satirlari DEGISMEDEN, otomatik olarak hep a.X'e duser.
                var filo = ogretmenMuaf ? new FiloAyarlari() : filoToplu.Politika; // v57 (3. revizyon): yukarida zaten birlesik istekle alindi

                // v101 (Ali Bey'in acik talebi, 11 Kasim 2026: "oyun engeli ogrenci makinasinda
                // filodan kaldiriliyor olmamali, default her kanal ve seviyede engellemeli"):
                // Oyun Engelleme artik HICBIR makinede filo politikasindan (Sheet/panel) ETKILENMEZ --
                // sadece yerel AppSettings.OyunEngelle (varsayilan ACIK) gecerlidir. Ogretmen zaten
                // yukarida "filo = bos" sayildigi icin bu etkiden muaftı; simdi oyun ozelinde ogrenci
                // de ayni muafiyete kavusuyor -- Sheet'ten yanlislikla/kazayla "Oyun=KAPALI"
                // gonderilmesi (veya bos/hatali bir deger) artik filodaki HICBIR ogrenci makinesinde
                // oyun engellemeyi kapatamaz. Vpn/Youtube/AiSite icin filo ??'li davranis DEGISMEDI --
                // bu kisitlama BILEREK sadece Oyun icin, Ali Bey'in talebine birebir uygun.
                bool efektifOyun = a.OyunEngelle;
                if (efektifOyun) { GameControl.SurecKapat(); GameControl.CalismaEngelle(); }
                else GameControl.EngelKaldir();

                bool efektifVpn = filo.Vpn ?? a.VpnEngelle;
                if (efektifVpn) VpnKontrol.Engelle(); else VpnKontrol.KaldirEngel();

                bool efektifYoutube = filo.Youtube ?? a.YoutubeKisitli;
                YoutubeKisit.SaatiUygula(efektifYoutube);

                bool efektifAi = filo.AiSite ?? a.AiSiteEngelle;
                if (efektifAi) AiSiteEngelle.Engelle(); else AiSiteEngelle.KaldirEngel();

                // v57: Agir Bakim gun/saati de filo ?? yerel -- deger DEGISTIYSE (nadir), Windows
                // gorevi burada tek seferlik yeniden kaydedilir; degismediyse hicbir sey yapilmaz.
                string efektifAgirGun = filo.AgirGun ?? a.AgirGun;
                string efektifAgirSaat = filo.AgirSaat ?? a.AgirSaat;
                TaskHelper.AgirBakimGuncelleGerekirse(efektifAgirGun, efektifAgirSaat, a.BostaDk);

                // v69 (Ali Bey'in istegi): AI haftalik kontrolu de Agir Bakim ile AYNI desen --
                // acik/kapali VE gun/saat, filo ?? yerel. Degistiyse gorev burada yeniden kaydedilir/
                // kaldirilir; degismediyse (buyuk cogunluk) dokunulmaz.
                bool efektifAiHaftalik = filo.AiKontrol ?? a.AiHaftalikAktif;
                string efektifAiGun = filo.AiGun ?? a.AiGun;
                string efektifAiSaat = filo.AiSaat ?? a.AiSaat;
                TaskHelper.AiHaftalikGuncelleGerekirse(efektifAiHaftalik, efektifAiGun, efektifAiSaat);

                // v90 (Ali Bey'in istegi: "startup'ta gereksiz calistirmalardan arindirilmali"):
                // Opera/VPN/oyun/emulator baslangic girdilerini aktif temizler. BILEREK kosulsuz
                // (yorum: "bu kuralin istisnasi da artik yok"). v100 inceleme notu: bagimsiz bir
                // rapor bunun ogretmen makinesinde de calismasini sorguladi; Ali Bey'e soruldu --
                // Ali Bey'in acik yaniti: "Baslangicta af yok" -- yani bu YINE BILEREK, HER
                // makinede (ogretmen dahil) kosulsuz kalsin. Teyit edilmis karar, DEGISTIRILMEDI.
                try { BaslangicTemizle.Temizle(); }
                catch (Exception ex) { Logger.WriteFileOnly("baslangic_temizle.txt", "Zamanli baslangic temizligi hatasi: " + ex.Message); }
            }
            catch (Exception ex) { Logger.WriteFileOnly("filo_politika.txt", "Zamanli filo ayarlari uygulama hatasi: " + ex.Message); }
            // v109: rol gruplari + otomatik rol atamasi + klasor izinleri + AppLocker (saatte bir,
            // kendi damgasiyla) ve disk kritik kontrolu (kritikse ayri gorevde acil temizlik).
            try { var aR = new AppSettings(); aR.Load(); RolGuvenlik.DonguKontrol(aR); }
            catch (Exception ex) { Logger.WriteFileOnly("rol_guvenlik.txt", "Zamanli rol/izin/AppLocker hatasi: " + ex.Message); }
            try { AkilliBakim.DiskKritikDonguKontrol(); }
            catch (Exception ex) { Logger.WriteFileOnly("akilli_bakim.txt", "Zamanli disk kontrolu hatasi: " + ex.Message); }

            // "Degisimi engelleyen" (donma/geri-alma) yazilimlari -- tahmine dayali ilk uygulama,
            // Ali Bey #9'u netlestirince guncellenecek (bkz. Core/DegisimEngelKontrol.cs).
            try { DegisimEngelKontrol.TaraVeKapat(); }
            catch (Exception ex) { Logger.WriteFileOnly("degisim_engel.txt", "Zamanli degisim-engel taramasi hatasi: " + ex.Message); }

            // v110: Filo'dan TESHIS (salt-okunur, sabit liste) -- HER makinede (ogretmen dahil, bilgi
            // toplama). Dongunun EN SONUNDA: sorgular birkac dakika surebilir, diger isleri geciktirmesin.
            try { Teshis.FiloKontrolEtVeUygulaDeger(filoToplu.TeshisZaman, filoToplu.TeshisSorgular, ayarDongu ?? new AppSettings()); }
            catch (Exception ex) { Logger.WriteFileOnly("teshis.txt", "Teshis kontrolu (dongu) hatasi: " + ex.Message); }
        }

        /// <summary>Log satirina yazmak icin argumanlari guvenle birlestirir (bos/null ise "-").</summary>
        private static string ArgLogIcin(string[] args)
        {
            try { return (args == null || args.Length == 0) ? "-" : string.Join(" ", args); }
            catch { return "-"; }
        }

        /// <summary>kurulum.ini icin sifreyi gizler. Duz sifreyi hicbir yere kaydetmez.</summary>
        private static void SifreleAraci(string parola)
        {
            if (string.IsNullOrEmpty(parola))
                parola = Microsoft.VisualBasic.Interaction.InputBox(
                    "Yonetici parolasini yazin.\r\nSize kurulum.ini icin 'parola_sifreli=' satiri verilecek.",
                    "Parola gizle", "");
            if (string.IsNullOrEmpty(parola)) return;

            string gizli = Yetki.SifreGizle(parola);
            string metin =
                "kurulum.ini icine su satiri koyun (duz parola yerine):\r\n\r\n" +
                "parola_sifreli=" + gizli + "\r\n\r\n" +
                "(Panoya kopyalandi.)";
            try { Clipboard.SetText("parola_sifreli=" + gizli); } catch { }
            MessageBox.Show(metin, "Parola gizlendi", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>OGRENCI modu: menusuz, her seyi otomatik + gizli kurar, sonunda mesaj verir.</summary>
        private static void OgrenciTamKur()
        {
            var ayar = new AppSettings();
            ayar.Load();
            // Ogrenci PC'si icin varsayilanlar acik olsun
            ayar.MakineTuru = "ogrenci";
            ayar.OyunEngelle = true;
            ayar.EkranHepAcik = true;
            // v84 (Ali Bey'in istegi, 6 Kasim 2026): Cop Kutusu ARTIK KAPATILMIYOR -- bkz.
            // AppSettings.cs'teki CopKutusuKapat varsayilaninin ayni tarihte degistirilme nedeni.
            ayar.CopKutusuKapat = false;
            ayar.YoutubeKisitli = true;   // ders saatinde YouTube kapali, teneffuste acik
            ayar.VpnEngelle = true;       // tum VPN cesitleri devre disi
            // v55 duzeltme: ONCEDEN burada AiSiteEngelle HIC ayarlanmiyordu -- menusuz
            // UnsalOgrenci.exe ile kurulan bir makine, digerleri gibi otomatik AI engeli
            // ALMIYORDU, AppSettings'in kendi varsayilaninda (kapali) kaliyordu.
            ayar.AiSiteEngelle = true;    // AI web siteleri (ChatGPT, Gemini, Claude.ai vb.) surekli engelli

            var sb = new StringBuilder();
            try
            {
                // 1) Gizli kurulum: Program Files'a gizli kopya + hafif/agir/oyun gorevleri + oyun engelini hemen uygula
                Installer.Kur(ayar);
                sb.AppendLine("  - Hafif bakim       : kuruldu");
                sb.AppendLine("  - Agir bakim        : kuruldu");
                sb.AppendLine("  - Oyun engelleme    : kuruldu (surekli)");

                // 2) Oyun taramasi/kaldirma denemeleri, 10 dk periyotlu arka plan gorevinin icindedir.
                sb.AppendLine("  - Oyun kontrolu    : 10 dakikada bir, ekran raporu olmadan otomatik denetim");
                sb.AppendLine("  - Kurulu oyunlar   : yalnizca taninan oyun adlari icin sessiz kaldirma denemesi");
                sb.AppendLine("  - Cevrim ici oyun  : oyun surecleri + bilinen tarayici/cloud oyun siteleri engellenir");
                sb.AppendLine("  - Virus korumasi    : acildi (gercek zamanli + firewall)");

                // VPN ve YouTube/AI kisitini hemen uygula (loglari beklemeden)
                // v92: "Askiya Al" kaldirildi -- bu otomatik (menusuz) kurulum artik kosulsuz uygular.
                try { if (ayar.VpnEngelle) VpnKontrol.Engelle(); } catch { }
                try { YoutubeKisit.SaatiUygula(); } catch { }
                try { if (ayar.AiSiteEngelle) AiSiteEngelle.Engelle(); } catch { }
                sb.AppendLine("  - VPN               : " + (ayar.VpnEngelle ? "devre disi (tum cesitler engellendi)" : "serbest"));
                // v57: ders/teneffus ayrimi kaldirildi -- YouTube artik SUREKLI acik/kapali
                // (YoutubeKisit.SaatiUygula zamandan bagimsiz calisir). Eski metin "ders saatinde
                // kapali, teneffuste acik" diyordu -- artik DOGRU DEGIL, yanlis bilgi veriyordu.
                sb.AppendLine("  - YouTube           : " + (ayar.YoutubeKisitli ? "kisitli (surekli)" : "serbest"));
                sb.AppendLine("  - AI web siteleri   : " + (ayar.AiSiteEngelle ? "engelli" : "serbest (varsayilan)"));

                MessageBox.Show(
                    "Kurulum tamamlandi. Asagidakiler kuruldu:\r\n\r\n" + sb +
                    "\r\nBu bilgisayar artik korunuyor.\r\n" +
                    "(Gizli kuruldu; standart kullanici goremez/kaldiramaz.)" +
                    TamperNotuVeAc(),
                    "Unsal Bakim", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Kurulum sirasinda hata: " + ex.Message,
                    "Unsal Bakim", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>v40: Profile gore kisitlama bayraklarini ayarlar ve KAYDEDER. "ogretmen" ->
        /// oyun/VPN/YouTube kisitlamasi YOK, ama TUM otomatik bakim (hafif/agir bakim, AI haftalik,
        /// internetten otomatik guncelleme, virus korumasi/firewall) AKTIF kalir -- sadece ogrenciye
        /// yonelik kisitlamalar kapanir. "ogrenci" (varsayilan) -> tum kisitlamalar acik.</summary>
        private static void ProfilUygula(AppSettings ayar, string profil)
        {
            ayar.MakineTuru = profil;
            bool kisitlamaAcik = !string.Equals(profil, "ogretmen", StringComparison.OrdinalIgnoreCase);
            ayar.OyunEngelle = kisitlamaAcik;
            ayar.VpnEngelle = kisitlamaAcik;
            ayar.YoutubeKisitli = kisitlamaAcik;
            // v55 duzeltme: ONCEDEN burada AiSiteEngelle HIC ayarlanmiyordu -- "/kur-ogrenci" ile
            // kurulan bir makine, digerleri gibi otomatik AI engeli almiyor, AppSettings'in
            // kendi varsayilaninda (kapali) kaliyordu. Digerleriyle AYNI kisitlamaAcik mantigina
            // baglandi: ogrenci -> acik, ogretmen -> kapali.
            ayar.AiSiteEngelle = kisitlamaAcik;
            // Bakim/guncelleme/virus korumasi profile bagli DEGIL -- her iki profilde de ACIK
            // (varsayilanlar zaten boyle; burada elle dokunulmuyor ki ogretmenin kendi secimi varsa bozulmasin).
            ayar.Save();
        }

        /// <summary>"/kur-ogrenci" ve "/kur-ogretmen" ortak govdesi: profili uygular, HER ZAMAN
        /// gizli kurulum yapar (Program Files altinda gizli klasor + SYSTEM gorevleri -- "gizli
        /// olmayan" bir kurulum secenegi yoktur), kisitlama fonksiyonlarini hemen (yeniden baslatma
        /// beklemeden) uygular/kaldirir ve sonucu MessageBox ile gosterir.</summary>
        private static void KurGizliProfilVeMesajGoster(AppSettings ayar, string profil)
        {
            try
            {
                ProfilUygula(ayar, profil);
                string yer = Installer.Kur(ayar);
                // v92: "Askiya Al" kaldirildi -- bu profil kurulumu artik kosulsuz uygular.
                try { if (ayar.VpnEngelle) VpnKontrol.Engelle(); else VpnKontrol.KaldirEngel(); } catch { }
                try { YoutubeKisit.SaatiUygula(); } catch { }
                try { if (ayar.AiSiteEngelle) AiSiteEngelle.Engelle(); else AiSiteEngelle.KaldirEngel(); } catch { }

                bool ogretmen = string.Equals(profil, "ogretmen", StringComparison.OrdinalIgnoreCase);
                string baslik = ogretmen ? "Unsal Bakim - Kurulum (Ogretmen)" : "Unsal Bakim - Kurulum (Ogrenci)";
                string kisitlamaSatiri = ogretmen
                    ? "- Oyun/VPN/YouTube kisitlamasi: YOK (bu bir ogretmen bilgisayari)\r\n"
                    : "- Oyun engelleme + YouTube/AI kisiti + VPN/Hotspot engeli: AKTIF\r\n";
                MessageBox.Show(
                    "Gizli kurulum tamamlandi.\r\n\r\nKonum (gizli): " + yer +
                    "\r\n\r\n- Hafif/Agir bakim, haftalik AI kontrol, internetten otomatik guncelleme: AKTIF\r\n" +
                    kisitlamaSatiri +
                    "- Virus korumasi + guvenlik duvari acildi\r\n" +
                    "- Gorevler gizli ve SYSTEM olarak calisir\r\n" +
                    "- Standart kullanici (ogrenci) kaldiramaz" +
                    TamperNotuVeAc(),
                    baslik, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Kurulum hatasi: " + ex.Message,
                    "Unsal Bakim", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// "Kurcalamaya karsi koruma" (Tamper) KAPALI ise: Windows Guvenligi sayfasini acar ve
        /// mesaja tek-tiklik talimat ekler. ACIK/bilinmiyor ise bos dondurur (hoca hic ugrasmaz).
        /// Bu tek madde Windows tarafindan programla ACILAMAZ; o yuzden kullaniciya birakilir.
        /// </summary>
        private static string TamperNotuVeAc()
        {
            try
            {
                var t = UnsalBakim.Core.PerformansAyar.TamperAcik();
                if (t == false)
                {
                    UnsalBakim.Core.PerformansAyar.GuvenlikSayfasiniAc();
                    return "\r\n\r\n---------------------------------------------\r\n" +
                           "SON BIR ADIM (tek tik, acilan pencerede):\r\n" +
                           "\"Kurcalamaya karsi koruma\" dugmesini ACIK yapin.\r\n" +
                           "(Windows bunu programla actirmaz. Zorunlu degil; cihaz\r\n" +
                           " zaten korumali, bu sadece sari uyariyi kaldirir.)";
                }
            }
            catch { }
            return "";
        }
    }
}
