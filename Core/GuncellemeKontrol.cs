using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;

namespace UnsalBakim.Core
{
    /// <summary>
    /// Google Drive'daki sabit linklerden surum kontrolu ve (istenirse) yeni surumun dosyalarini
    /// indirip calisan exe'nin (panel VEYA gizli kurulu kopya -- hangisi bu kodu calistiriyorsa O)
    /// UZERINE otomatik yerlestirme.
    ///
    /// Uc giris noktasi:
    ///   - KontrolEt()              : sadece okur/karsilastirir, hicbir dosyaya dokunmaz (panel butonu).
    ///   - YeniSurumuIndirVeDegistir(): panelden ELLE tetiklenir, indirir+degistirir+PANELI yeniden baslatir.
    ///   - GuncellemeKontroluSessiz(): SAATLIK SYSTEM gorevinden (arguman: /guncelleme-kontrol) cagrilir;
    ///     MessageBox/UI YOK, sadece log. Yeni surum yoksa HICBIR SEYE DOKUNMADAN cikar. Varsa indirip
    ///     KURULU (gizli) kopyanin uzerine koyar; sonraki herhangi bir zamanlanmis gorev calismasinda
    ///     (hafif bakim, oyun denetimi, vs.) artik YENI surum kullanilir -- ayrica yeniden baslatmaya
    ///     veya "KUR" calistirmaya gerek YOK, cunku zamanlanmis gorevler zaten hep AYNI sabit yola
    ///     (Installer.HedefExe) isaret eder; degisen sadece o yoldaki dosyanin icerigidir.
    ///
    /// NOT: exe dosyasi DOGRUDAN Drive'a konmaz -- Google Drive bazi .exe indirmelerinde "bu dosya
    /// cihaza zarar verebilir / virus taramasi yapilamadi" onay sayfasi gosterip gercek dosya yerine
    /// HTML donduruyor (confirm-token ile asma denemeleri Google'in surekli degisen davranisi
    /// yuzunden guvenilir degil). Bunun yerine exe+config TEK bir zip icine konup o indiriliyor;
    /// zip dosyalari bu uyariyi tetiklemiyor. (DERLE_VE_KARISTIR.bat bu zip'i otomatik uretir:
    /// beta\USB_BELLEK\bin\guncelleme.zip)
    /// </summary>
    public static class GuncellemeKontrol
    {
        // v50: Drive dosya ID'leri artik Musteri.cs'te TEK yerde tutuluyor (degerler v49'dakiyle
        // AYNI, sadece tasindi -- bkz. Musteri.cs basindaki aciklama).
        private static string IndirUrl(string id) => "https://drive.google.com/uc?export=download&id=" + id;

        // v45: Varsayilan WebClient'in zaman asimi YOK -- ag/Drive yavas/yanitsiz kalirsa bu kod
        // SURESIZ takilabiliyordu. DriveLog.cs/YoneticiSifre.cs'te v36'da bulunup duzeltilen AYNI
        // hata sinifi, bu dosyaya o zaman uygulanmamisti (saatlik "/guncelleme-kontrol" gorevi
        // etkileniyordu). Sabit bir sure sonra hata firlatir, gorev takilmadan loga dusup biter.
        private sealed class ZamanAsimliWebClient : WebClient
        {
            protected override WebRequest GetWebRequest(Uri address)
            {
                var req = base.GetWebRequest(address);
                if (req != null) req.Timeout = 20000; // 20 sn
                return req;
            }
        }

        private static void TlsHazirla()
        {
            // Bazi makinelerde yalniz Tls12 ile Drive baglantisi "SSL/TLS" hatasi verebiliyor
            // (eski .NET/OS yama durumu). YapayZeka.Cagir() ile ayni, daha savunmaci yaklasim:
            // desteklenen tum protokolleri (Tls11/Tls12/Tls13) bayrakla; bir tanesi bu .NET'te
            // tanimsizsa o satir sessizce atlanir, digerleri yine de eklenir.
            try
            {
                var p = SecurityProtocolType.Tls12;
                try { p |= (SecurityProtocolType)768; } catch { }   // Tls11 (eski .NET'te enum'da olmayabilir)
                try { p |= (SecurityProtocolType)12288; } catch { } // Tls13
                ServicePointManager.SecurityProtocol = p;
            }
            catch { try { ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; } catch { } }
        }

        private static string HtmlBaslik(byte[] v)
        {
            try
            {
                string m = System.Text.Encoding.UTF8.GetString(v, 0, Math.Min(v.Length, 20000));
                var x = System.Text.RegularExpressions.Regex.Match(m, "<title>([^<]{0,150})</title>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                return x.Success ? x.Groups[1].Value.Trim() : "";
            }
            catch { return ""; }
        }

        /// <summary>v110: surum kaynagi. Guvenli (GitHub+imza) yapilandirildiysa ORADAN (imza
        /// dogrulanarak) alinir; degilse eski Drive surum.txt'ye duser. Guvenli yol yapilandirilmissa
        /// ve basarisiz olursa (imza/ag) HATA firlatir -- Drive'a sessizce DUSMEZ (guvenlik: imzasiz
        /// kaynaga geri donulmesin).</summary>
        private static int SurumGetir()
        {
            if (GuncellemeGuvenli.Hazir) return GuncellemeGuvenli.SurumAl();
            return DriveSurumGetir();
        }

        private static int DriveSurumGetir()
        {
            TlsHazirla();
            string metin;
            using (var wc = new ZamanAsimliWebClient())
            {
                wc.Headers["User-Agent"] = "UnsalBakim/1.0";
                metin = wc.DownloadString(IndirUrl(Musteri.SurumDosyaId));
            }
            int drive;
            if (!int.TryParse(metin.Trim(), out drive))
                throw new Exception("surum.txt okunamadi: '" + metin.Trim() + "'");
            return drive;
        }

        /// <summary>Drive'daki surum.txt'yi okuyup bu makinedeki surumle karsilastirir.
        /// Sadece bilgi dondurur; hicbir dosyaya dokunmaz. (Panelden elle cagrilir.)</summary>
        public static string KontrolEt()
        {
            int drive;
            try { drive = SurumGetir(); }
            catch (Exception ex) { return "Guncelleme kontrol edilemedi (" + (GuncellemeGuvenli.Hazir ? "GitHub/imza" : "internet/Drive") + "): " + ex.Message; }

            if (drive > License.SurumNo)
                return "YENI SURUM VAR: v" + drive + " (bu makine: v" + License.SurumNo + "). " +
                       "Indirmek icin 'Yeni surumu indir' dugmesine basin.";
            return "Guncel: v" + License.SurumNo + " (Drive'da yeni surum yok).";
        }

        /// <summary>Drive'daki guncelleme.zip'i indirip dogrular; basariliysa calisan exe'nin KENDI
        /// dosyalarinin (yol: assemblyYolu) UZERINE yerlestirir. Dogrulama basarisizsa HICBIR DOSYAYA
        /// DOKUNMAZ. "Calisirken kendi uzerine yazamama" sorunu, dosyayi SILMEDEN once YENIDEN
        /// ADLANDIRIP (Installer.cs'teki EskiTemizle ile ayni NTFS teknigi) sonra ayni isme yeni
        /// dosyayi koyma yontemiyle asilir.</summary>
        private static string IndirVeYerineKoy(string assemblyYolu, out bool basarili)
        {
            basarili = false;
            string buradan = Path.GetDirectoryName(assemblyYolu);
            string cfgYol = assemblyYolu + ".config";
            string gecici = Path.Combine(buradan, "Guncelleme_gecici");
            string zipYol = Path.Combine(buradan, "guncelleme_gelen.zip");

            try
            {
                TlsHazirla();
                byte[] veri;
                if (GuncellemeGuvenli.Hazir)
                {
                    // v110: GitHub'tan imzali+hash'li indir. Manifest imzasi dogrulanir, sonra zip'in
                    // SHA-256'si manifesttekiyle karsilastirilir. Biri bile tutmazsa Exception -> hicbir
                    // dosya degismez.
                    int msurum; string msha;
                    GuncellemeGuvenli.ManifestDogrula(out msurum, out msha);
                    if (msurum <= License.SurumNo) { basarili = false; return "Guncel (v" + License.SurumNo + "); indirme yapilmadi."; }
                    veri = GuncellemeGuvenli.ZipIndirVeDogrula(msha);
                }
                else
                {
                    using (var wc = new ZamanAsimliWebClient())
                    {
                        wc.Headers["User-Agent"] = "UnsalBakim/1.0";
                        veri = wc.DownloadData(IndirUrl(Musteri.ZipDosyaId));
                    }
                }

                // v110: Drive zip yerine bir uyari sayfasi (HTML) dondururse sayfanin basligi hata
                // mesajina eklenir -- sebep (or. Drive'in virus uyarisi) panelde/logda gorulsun.
                // Uyari sayfasi ASLA atlanmaz; dosya duzeltilip yeniden yuklenmelidir.
                if (veri.Length < 2 || veri[0] != (byte)'P' || veri[1] != (byte)'K')
                {
                    string ilkBaslik = HtmlBaslik(veri);
                    if (veri.Length < 2 || veri[0] != (byte)'P' || veri[1] != (byte)'K')
                        return "Drive'dan gelen dosya gecerli bir zip degil" + (ilkBaslik.Length > 0 ? " (Drive sayfasi: '" + ilkBaslik + "')" : "") + " " +
                           "(paylasim izni 'Baglantiya sahip olan herkes' olmayabilir, ya da link yanlis). " +
                           "Hicbir dosya degistirilmedi.";
                }

                if (Directory.Exists(gecici)) { try { Directory.Delete(gecici, true); } catch { } }
                File.WriteAllBytes(zipYol, veri);
                ZipFile.ExtractToDirectory(zipYol, gecici);
                try { File.Delete(zipYol); } catch { }

                string yeniExe = Path.Combine(gecici, "UnsalBakim.exe");
                string yeniCfg = Path.Combine(gecici, "UnsalBakim.exe.config");
                if (!File.Exists(yeniExe))
                {
                    try { Directory.Delete(gecici, true); } catch { }
                    return "Zip icinde UnsalBakim.exe bulunamadi. Hicbir dosya degistirilmedi.";
                }
                // v48: ChatGPT'nin diyalog belgesi uzerindeki UCUNCU incelemesinde bulunan eksik --
                // yeniCfg'nin zip icinde bulunmasi ZORUNLU DEGILDI (asagida sadece "varsa kopyala"
                // deniyordu). Derleme paketinde config eksik kalirsa: eski config zaten yana tasinmis
                // (yedeklenmis) olur, yenisi gelmez, ve islem sessizce "basarili" sayilirdi -- kurulumda
                // config HIC olmazdi. Simdi config de exe gibi ZORUNLU: yoksa hicbir dosya degismiyor.
                if (!File.Exists(yeniCfg))
                {
                    try { Directory.Delete(gecici, true); } catch { }
                    return "Zip icinde UnsalBakim.exe.config bulunamadi. Hicbir dosya degistirilmedi.";
                }
                var basByte = new byte[2];
                using (var fs = File.OpenRead(yeniExe)) fs.Read(basByte, 0, 2);
                if (basByte[0] != (byte)'M' || basByte[1] != (byte)'Z')
                {
                    try { Directory.Delete(gecici, true); } catch { }
                    return "Indirilen dosya gecerli bir program degil. Hicbir dosya degistirilmedi.";
                }

                // Calisan exe/config'i YANA TASI (silinemez ama yeniden adlandirilabilir), sonra
                // AYNI ISME yeni dosyayi koy. Bu noktadan once her sey dogrulanmis durumda.
                string eskiExe = assemblyYolu + ".eski_" + DateTime.Now.Ticks;
                try { File.Move(assemblyYolu, eskiExe); }
                catch (Exception ex)
                {
                    try { Directory.Delete(gecici, true); } catch { }
                    return "Calisan dosya degistirilemedi (" + ex.Message + "). Hicbir dosya degistirilmedi.";
                }
                // v45'te eklenen ilk geri-alma sadece EXE'yi yedekliyordu; config DOSYASI yedeklenmeden
                // direkt silinip yerine yenisi kopyalaniyordu. ChatGPT'nin ikinci incelemesinde haklı
                // olarak bulunan eksik: exe kopyalama basarili olup config kopyalama basarisiz olursa,
                // eski config zaten silinmis ve hicbir yedegi olmadigi icin KAYBOLUYORDU -- geri alma
                // "basarili" denip exe eski haline donuyordu ama config YOK oluyordu (uyumsuz, calismayan
                // bir kurulum kalirdi). v47: config de AYNI "yana tasi, basarisizsa geri koy" desenine
                // alindi; ayrica geri alma adimlarinin GERCEKTEN basarili olup olmadigi ayri ayri izlenip
                // donus mesaji buna gore yaziliyor (ONCEDEN basarisiz geri almada bile "GERI YUKLENDI"
                // deniyordu -- yaniltici).
                string eskiCfg = (File.Exists(cfgYol)) ? (cfgYol + ".eski_" + DateTime.Now.Ticks) : null;
                if (eskiCfg != null)
                {
                    try { File.Move(cfgYol, eskiCfg); }
                    catch
                    {
                        // Config yana tasinamadi (nadir) -- devam etmiyoruz, hicbir sey degismedi sayilir.
                        try { File.Move(eskiExe, assemblyYolu); } catch { }
                        try { Directory.Delete(gecici, true); } catch { }
                        return "Yapilandirma dosyasi hazirlanamadi. Hicbir dosya degistirilmedi.";
                    }
                }
                try
                {
                    File.Copy(yeniExe, assemblyYolu, true);
                    if (File.Exists(yeniCfg)) File.Copy(yeniCfg, cfgYol, true);
                }
                catch (Exception ex)
                {
                    // Geri alma: yarim kalmis yeni dosyalari temizleyip eski exe+config'i AYNEN geri koy.
                    // Her adimin GERCEK sonucunu takip et -- basarisizlikta mesaj da onu yansitsin.
                    bool exeGeriDondu = false, cfgGeriDondu = true; // config yoksa zaten sorun yok
                    try { if (File.Exists(assemblyYolu)) File.Delete(assemblyYolu); } catch { }
                    try { File.Move(eskiExe, assemblyYolu); exeGeriDondu = File.Exists(assemblyYolu); } catch { }
                    if (eskiCfg != null)
                    {
                        cfgGeriDondu = false;
                        try { if (File.Exists(cfgYol)) File.Delete(cfgYol); } catch { }
                        try { File.Move(eskiCfg, cfgYol); cfgGeriDondu = File.Exists(cfgYol); } catch { }
                    }
                    try { Directory.Delete(gecici, true); } catch { }

                    if (exeGeriDondu && cfgGeriDondu)
                        return "Guncelleme yarida kaldi, ESKI SURUM (exe+ayar dosyasi) GERI YUKLENDI (" + ex.Message + ").";
                    return "Guncelleme yarida kaldi VE eski surum TAM geri yuklenemedi " +
                           "(exe:" + (exeGeriDondu ? "ok" : "BASARISIZ") + " config:" + (cfgGeriDondu ? "ok" : "BASARISIZ") +
                           ") -- bu makineye ELLE bakilmali! (" + ex.Message + ")";
                }
                try { File.Delete(eskiExe); } catch { }   // genelde silinebilir; silinemezse onemli degil
                if (eskiCfg != null) { try { File.Delete(eskiCfg); } catch { } }
                try { Directory.Delete(gecici, true); } catch { }

                basarili = true;
                return "Guncellendi.";
            }
            catch (Exception ex)
            {
                try { if (Directory.Exists(gecici)) Directory.Delete(gecici, true); } catch { }
                return "Hata: " + ex.Message;
            }
        }

        /// <summary>PANELDEN elle tetiklenir (onay penceresinden sonra). Indirir, panelin KENDI
        /// dosyasinin uzerine koyar, PANELI yeniden baslatir. Bu, GIZLI kurulumu/zamanlanmis
        /// gorevleri henuz guncellemez -- onlari yeni surume tasimak icin panel yeniden actiktan
        /// sonra yine elle "KUR" dugmesine basmak GEREKIR (bilinçli tasarim: bir derlemede sorun
        /// cikarsa okuldaki butun makineler birden, kontrolsuzce etkilenmesin diye).</summary>
        public static string YeniSurumuIndirVeDegistir()
        {
            string buradanExe = System.Reflection.Assembly.GetExecutingAssembly().Location;
            bool basarili;
            string sonuc = IndirVeYerineKoy(buradanExe, out basarili);
            if (!basarili)
                return "Guncelleme basarisiz: " + sonuc;

            Logger.Write("kurulum.txt", "Panel dosyasi guncellendi (Drive, elle). Yeniden baslatiliyor. " +
                "UNUTMA: zamanlanmis gorevlere/gizli kuruluma yansitmak icin tekrar 'KUR' dugmesine basin.");
            try { Process.Start(buradanExe); } catch { }
            Environment.Exit(0);
            return "Guncellendi, yeniden baslatiliyor...";   // buraya pratikte ulasilmaz
        }

        /// <summary>SAATLIK SYSTEM gorevinden cagrilir (arguman: /guncelleme-kontrol). Ekransiz/sessiz.
        /// Once Drive'daki surumu okur; bu makinedekinden YENI DEGILSE hicbir indirme/degisiklik
        /// yapmadan cikar (her saat basi gereksiz indirme yapmaz). Yeniyse indirir, KURULU (gizli)
        /// kopyanin uzerine koyar VE ARDINDAN yeni exe'yi "/kur-sessiz" ile baslatip GIZLI KURULUMU
        /// (Installer.Kur -> zamanlanmis gorevler + ayarlar + oyun engeli vb.) da otomatik, ekransiz
        /// tazeler -- boylece makineye gitmeden/menu acmadan hem dosya hem kurulum/gorevler guncel
        /// kalir. Yeni "/kur-sessiz" sureci kendi icinde eski sureci (gerekirse) kapatir.</summary>
        public static string GuncellemeKontroluSessiz()
        {
            // v110: saatlik gorev ile 5 dk'lik dongu AYNI ANDA guncellemeye kalkmasin.
            System.Threading.Mutex mtx = null;
            bool alindi = false;
            try
            {
                try { mtx = new System.Threading.Mutex(false, @"Global\UnsalBakimGuncelleme"); } catch { mtx = null; }
                if (mtx != null)
                {
                    try { alindi = mtx.WaitOne(0); } catch (System.Threading.AbandonedMutexException) { alindi = true; }
                    if (!alindi) return "Baska bir guncelleme islemi suruyor; bu turda atlandi.";
                }
                return GuncellemeKontroluSessizIc();
            }
            finally
            {
                if (alindi) try { mtx.ReleaseMutex(); } catch { }
                try { mtx?.Dispose(); } catch { }
            }
        }

        // ---------------------------------------------------------------- v110: pilot + dongu

        private static string Kok => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "UnsalBakim");
        private static string PilotYolu => Path.Combine(Kok, "pilot_guncelleme.txt");
        private static string SonGuncellemeYolu => Path.Combine(Kok, "guncelleme_son_basarili.txt");
        private static string SonHataYolu => Path.Combine(Kok, "guncelleme_son_hata.txt");

        /// <summary>FiloDurum.Al (v110 Apps Script yaniti geldiginde) pilot durumunu yerel olarak saklar --
        /// saatlik guncelleme gorevi ag istegi atmadan buradan okur.</summary>
        internal static void PilotDurumYaz(string durum)
        {
            try
            {
                durum = (durum ?? "").Trim();
                string onceki = File.Exists(PilotYolu) ? File.ReadAllText(PilotYolu).Trim() : "";
                if (onceki == durum) return;
                Directory.CreateDirectory(Kok);
                File.WriteAllText(PilotYolu, durum);
                Logger.WriteFileOnly("kurulum.txt", "[Pilot] guncelleme durumu: '" + (durum == "" ? "kisit yok" : durum) + "'");
            }
            catch { }
        }

        /// <summary>Pilot kisiti bu surumu engelliyor mu? Dosya yoksa/bossa/IZIN ise HAYIR (eski davranis).</summary>
        public static bool PilotBekletiyor(int driveSurum, out string neden)
        {
            neden = "";
            return false; // v111: Pilot ozelligi kaldirildi (GitHub duzeninde gereksiz). Hicbir makine beklemez.
#pragma warning disable 0162
            try
            {
                string d = File.Exists(PilotYolu) ? File.ReadAllText(PilotYolu).Trim() : "";
                if (!d.StartsWith("BEKLE:", StringComparison.Ordinal)) return false;
                int esik;
                string sayi = d.Substring(6);
                if (sayi.Length > 0 && int.TryParse(sayi, out esik) && driveSurum < esik) return false;
                neden = "pilot guncelleme aktif, bu makine hedefte degil (" + d + ")";
                return true;
            }
            catch { return false; }
#pragma warning restore 0162
        }

        /// <summary>v110 (Ali Bey: "once guncelleme calismali"): 5 dk'lik dongude, Filo komutlarindan
        /// ONCE cagrilir. Yeni surum varsa (ve pilot izin veriyorsa) guncellemeyi baslatir ve TRUE
        /// doner -- cagiran o turda baska is yapmaz; komutlar Sheet'te kalir ve YENI surum calistirir.
        /// Basarisiz bir denemeden sonra 60 dk beklenir (Drive kotasi/bant genisligi bosa harcanmasin).</summary>
        public static bool DonguGuncellemeKontrol(AppSettings ayar)
        {
            try
            {
                if (ayar != null && !ayar.OtoGuncellemeAktif) return false;
                try { if (File.Exists(SonHataYolu) && (DateTime.Now - File.GetLastWriteTime(SonHataYolu)).TotalMinutes < 60) return false; } catch { }
                int drive;
                try { drive = SurumGetir(); } catch { return false; } // ag yok/imza hatasi: sessizce devam
                if (drive <= License.SurumNo) return false;
                string sonuc = GuncellemeKontroluSessiz();
                Logger.WriteFileOnly("kurulum.txt", "[Guncelleme-dongu] " + sonuc);
                return sonuc.StartsWith("GUNCELLENDI", StringComparison.Ordinal);
            }
            catch (Exception ex) { Logger.WriteFileOnly("kurulum.txt", "[Guncelleme-dongu] hata: " + ex.Message); return false; }
        }

        private static string GuncellemeKontroluSessizIc()
        {
            int drive; string beklenenSha = null;
            try
            {
                if (GuncellemeGuvenli.Hazir) GuncellemeGuvenli.ManifestDogrula(out drive, out beklenenSha);
                else drive = DriveSurumGetir();
            }
            catch (Exception ex) { return "Kontrol edilemedi (" + (GuncellemeGuvenli.Hazir ? "GitHub/imza" : "internet/Drive") + "): " + ex.Message; }

            if (drive <= License.SurumNo)
                return "Guncel (v" + License.SurumNo + "); islem yapilmadi.";

            string pilotNeden;
            if (PilotBekletiyor(drive, out pilotNeden))
                return "v" + drive + " mevcut ama BEKLETILIYOR: " + pilotNeden + ".";

            // Ayni surum son 30 dk icinde zaten basariyla indirildiyse (dosya diskte yeni, bu surec eski
            // koddan calisiyor) tekrar indirme.
            try
            {
                if (File.Exists(SonGuncellemeYolu) && (DateTime.Now - File.GetLastWriteTime(SonGuncellemeYolu)).TotalMinutes < 30 &&
                    File.ReadAllText(SonGuncellemeYolu).Trim() == drive.ToString())
                    return "v" + drive + " az once indirildi; yeni surumun calismasi bekleniyor.";
            }
            catch { }

            // v110: Defender on-istisnasi KALDIRILDI (bkz. PerformansAyar).

            string buradanExe = System.Reflection.Assembly.GetExecutingAssembly().Location;
            bool basarili;
            string sonuc = IndirVeYerineKoy(buradanExe, out basarili);
            if (!basarili)
            {
                try { Directory.CreateDirectory(Kok); File.WriteAllText(SonHataYolu, DateTime.Now.ToString("s") + " " + sonuc); } catch { }
                try { DriveLog.Gonder("guncelleme_basarisiz", "v" + License.SurumNo + " -> v" + drive + ": " + sonuc); } catch { }
                return "v" + drive + " indirilemedi: " + sonuc;
            }

            try { Directory.CreateDirectory(Kok); File.WriteAllText(SonGuncellemeYolu, drive.ToString()); File.Delete(SonHataYolu); } catch { }
            try { DriveLog.Gonder("guncelleme_basarili", "v" + License.SurumNo + " -> v" + drive); } catch { }

            // Dosya guncellendi -> yeni surumu "/kur-sessiz" ile baslat: GIZLI kurulumu/zamanlanmis
            // gorevleri de otomatik, ekransiz tazeler (MessageBox YOK). Bu cagrinin kendisi burada
            // BEKLENMEZ; yeni surec bagimsiz calisir, bu gorev normal sekilde biter.
            try { Process.Start(buradanExe, "/kur-sessiz"); }
            catch (Exception ex)
            {
                return "GUNCELLENDI: v" + License.SurumNo + " -> v" + drive + " ama otomatik KUR baslatilamadi (" +
                       ex.Message + "). Bir sonraki gorev calismasinda dosya zaten yeni surum olacak; " +
                       "gorevleri/ayarlari tazelemek icin panelden 'KUR' dugmesine basin.";
            }
            return "GUNCELLENDI: v" + License.SurumNo + " -> v" + drive + " (sessiz, SYSTEM gorevi); " +
                   "gizli kurulum/gorevler de otomatik tazeleniyor (/kur-sessiz baslatildi).";
        }
    }
}
