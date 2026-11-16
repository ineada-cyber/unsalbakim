using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace UnsalBakim.Core
{
    /// <summary>
    /// v102 (Ali Bey'in acik istegi, 11 Kasim 2026: "Bilgisayarlar acildiginda bilgisayar ismi
    /// buyuk olarak yazsin, altina Bilisim yazsin; hangi user olursa olsun ekranin ortasinda ve
    /// buyuk"): Her makineye, o makinenin adini (Environment.MachineName) ve okul logosunu iceren
    /// bir masaustu duvar kagidi uretir ve zorunlu kilar.
    ///
    /// IKI PARCAYA BOLUNMUS calisir, CUNKU bu ozelligi tetikleyen /oyun-engelle gorevi SYSTEM
    /// hesabiyla, Oturum 0'da (Session 0) calisir -- Windows Vista'dan beri SYSTEM, etkilesimli
    /// bir kullanicinin masaustune DOGRUDAN SystemParametersInfo ile ulasamaz (oturum izolasyonu):
    ///   1) OlusturVeUygula() -- SYSTEM baglaminda (her /oyun-engelle dongusunde, 5 dk'da bir)
    ///      cagrilir: resmi uretir/diske yazar, HKLM Policies anahtarini yazar (yedek/varsayilan
    ///      katman) ve etkilesimli-oturum gorevinin (Duvar_Kagidi) KURULU olmasini garanti eder.
    ///   2) UygulaBuMakineyeAktifOturumda() -- bizzat etkilesimli KULLANICI baglaminda (yeni
    ///      Duvar_Kagidi Windows gorevi; logon + 20 dk'da bir tetikli, BUILTIN\Users grubu +
    ///      InteractiveToken ile, bkz. TaskHelper.DuvarKagidiGoreviKur) cagrilir: HKCU'yu yazar ve
    ///      SystemParametersInfo ile ANINDA uygular -- bu adim oturum acan kullanicinin KENDI
    ///      tokeniyla calistigi icin Oturum 0 izolasyonuna TAKILMAZ.
    /// Hicbir yerde kullanici onayi/aç-kapa anahtari YOK -- Oyun Engelleme'deki "gomulu, filodan
    /// ve yerelden bagimsiz" desenle AYNI felsefe: bu da BaslangicTemizle/OyunKaldirici ailesine
    /// katilan, kosulsuz bir temel davranis.
    /// </summary>
    public static class MasaustuDuvarKagidi
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

        private const int SPI_SETDESKWALLPAPER = 20;
        private const int SPIF_UPDATEINIFILE = 0x01;
        private const int SPIF_SENDCHANGE = 0x02;

        // v109: Resim artik ProgramData\UnsalBakim (ogrenciye KAPATILAN klasor) yerine AYRI bir
        // klasorde -- ProgramData\UnsalBakimDuvar. Bu klasorun izinleri: SYSTEM + Administrators
        // Tam Denetim, Users (ogrenci dahil) SADECE Okuma -- Windows'un duvar kagidini kullanicinin
        // oturumunda okuyabilmesi icin gerekli, ama ogrenci resmi/betigi DEGISTIREMEZ.
        public static string DuvarKlasor =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "UnsalBakimDuvar");

        private static string DosyaYolu => Path.Combine(DuvarKlasor, "duvar_kagidi.png");

        /// <summary>v109: Duvar_Kagidi gorevinin calistirdigi betik. Gorev artik BIZIM exe'mizi
        /// DEGIL, Windows'un kendi wscript.exe'sini calistirir -- ogrenci makinesinde Program
        /// Files\Windows Bakim klasoru ogrenciye kapatildigi (ACL + AppLocker) icin, kullanici
        /// oturumunda bizim exe'mizin calismasi artik MUMKUN DEGIL ve gerekmiyor.</summary>
        public static string BetikYolu => Path.Combine(DuvarKlasor, "uygula.vbs");

        private static void KlasorVeBetikHazirla()
        {
            bool yeni = !Directory.Exists(DuvarKlasor);
            Directory.CreateDirectory(DuvarKlasor);
            if (yeni || !File.Exists(BetikYolu))
            {
                // Miras KAPALI: SYSTEM/Administrators Tam Denetim, Users sadece Oku+Calistir.
                NativeRunner.Run("icacls.exe", "\"" + DuvarKlasor + "\" /inheritance:r /grant:r *S-1-5-18:(OI)(CI)F *S-1-5-32-544:(OI)(CI)F *S-1-5-32-545:(OI)(CI)RX /T /C /Q", "duvar_kagidi.txt", false);
            }
            string png = DosyaYolu.Replace("\"", "");
            // v109 duzeltme: PowerShell betigi KOMUT SATIRINDA kodlanmis (-EncodedCommand) degil, ayri
            // ve okunabilir bir .ps1 dosyasinda -- Norton -EncodedCommand'i zararli sayip durduruyordu.
            string ps1Yolu = Path.Combine(DuvarKlasor, "uygula.ps1");
            string ps1 =
                "# UNSAL BAKIM v109 - duvar kagidini bu oturumda hemen uygular\r\n" +
                "$p = '" + png.Replace("'", "''") + "'\r\n" +
                "if (-not (Test-Path $p)) { exit 0 }\r\n" +
                "Add-Type -Namespace UB -Name W -MemberDefinition '[DllImport(\"user32.dll\",CharSet=CharSet.Unicode)] public static extern int SystemParametersInfo(int a,int b,string c,int d);'\r\n" +
                "[UB.W]::SystemParametersInfo(20,0,$p,3) | Out-Null\r\n";
            string eskiPs1 = File.Exists(ps1Yolu) ? File.ReadAllText(ps1Yolu) : null;
            if (eskiPs1 != ps1) File.WriteAllText(ps1Yolu, ps1, System.Text.Encoding.ASCII);
            string vbs =
                "' UNSAL BAKIM v109 - duvar kagidi (oturum acan kullanicinin kendi oturumunda, gizli)\r\n" +
                "On Error Resume Next\r\n" +
                "Set fso = CreateObject(\"Scripting.FileSystemObject\")\r\n" +
                "If Not fso.FileExists(\"" + png + "\") Then WScript.Quit 0\r\n" +
                "Set sh = CreateObject(\"WScript.Shell\")\r\n" +
                "sh.RegWrite \"HKCU\\Control Panel\\Desktop\\WallpaperStyle\", \"10\", \"REG_SZ\"\r\n" +
                "sh.RegWrite \"HKCU\\Control Panel\\Desktop\\TileWallpaper\", \"0\", \"REG_SZ\"\r\n" +
                "sh.RegWrite \"HKCU\\Control Panel\\Desktop\\Wallpaper\", \"" + png + "\", \"REG_SZ\"\r\n" +
                "sh.Run \"powershell.exe -NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -File \"\"" + ps1Yolu + "\"\"\", 0, False\r\n";
            string eski = File.Exists(BetikYolu) ? File.ReadAllText(BetikYolu) : null;
            if (eski != vbs) File.WriteAllText(BetikYolu, vbs, System.Text.Encoding.ASCII);
        }

        /// <summary>SYSTEM baglaminda (oyun denetim dongusu) cagrilir: resmi uretir, HKLM
        /// politika anahtarini yazar ve etkilesimli-oturum uygulama gorevini kurar/tazeler. Her
        /// adim kendi try/catch'i icinde -- biri basarisiz olsa digerleri yine de denenir.</summary>
        public static void OlusturVeUygula()
        {
            try { KlasorVeBetikHazirla(); }
            catch (Exception ex) { Logger.WriteFileOnly("duvar_kagidi.txt", "Klasor/betik hazirlama hatasi: " + ex.Message); }

            try { ResmiUret(DosyaYolu); }
            catch (Exception ex) { Logger.WriteFileOnly("duvar_kagidi.txt", "Resim uretme hatasi: " + ex.Message); }

            try { HklmPolitikaYaz(DosyaYolu); }
            catch (Exception ex) { Logger.WriteFileOnly("duvar_kagidi.txt", "HKLM politika yazma hatasi: " + ex.Message); }

            try { TaskHelper.DuvarKagidiGoreviKur(true); }
            catch (Exception ex) { Logger.WriteFileOnly("duvar_kagidi.txt", "Gorev kurma hatasi: " + ex.Message); }
        }

        /// <summary>Etkilesimli kullanici baglaminda (Duvar_Kagidi Windows gorevi) cagrilir: HKCU
        /// degerlerini yazar ve SystemParametersInfo ile ANINDA uygular. Tamamen SESSIZ -- hic
        /// MessageBox/pencere GOSTERMEZ, hata olursa sadece dosyaya loglar.</summary>
        public static void UygulaBuMakineyeAktifOturumda()
        {
            try
            {
                string yol = DosyaYolu;
                // SYSTEM tarafi (OlusturVeUygula) henuz hic calismamis/resmi uretmemis olabilir
                // (orn. ilk acilis, ilk 5 dk'lik dongu henuz tetiklenmeden once bir kullanici
                // oturum actiysa) -- bu durumda sessizce atla, bir sonraki tetiklemede (20 dk'da
                // bir tekrar eden TimeTrigger) tekrar denenir.
                if (!File.Exists(yol)) return;

                try
                {
                    using (var k = Registry.CurrentUser.CreateSubKey(@"Control Panel\Desktop"))
                    {
                        if (k != null)
                        {
                            k.SetValue("Wallpaper", yol, RegistryValueKind.String);
                            k.SetValue("WallpaperStyle", "10", RegistryValueKind.String); // 10 = Doldur (Fill)
                            k.SetValue("TileWallpaper", "0", RegistryValueKind.String);
                        }
                    }
                }
                catch { }

                SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, yol, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
            }
            catch (Exception ex) { Logger.WriteFileOnly("duvar_kagidi.txt", "Etkilesimli uygulama hatasi: " + ex.Message); }
        }

        /// <summary>Yedek/varsayilan katman: Grup Ilkesi tarzi HKLM anahtari -- henuz hic oturum
        /// acmamis yeni bir profil veya (nadir) etkilesimli gorevin calismadigi bir an icin
        /// Windows'un kendi politika okumasina birakilan ek bir guvence. Asil/aninda etkiyi
        /// UygulaBuMakineyeAktifOturumda() saglar.</summary>
        private static void HklmPolitikaYaz(string yol)
        {
            using (var k = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\System"))
            {
                if (k == null) return;
                k.SetValue("Wallpaper", yol, RegistryValueKind.String);
                k.SetValue("WallpaperStyle", "10", RegistryValueKind.String);
            }
        }

        private static void ResmiUret(string hedefYol)
        {
            // v102 duzeltmesi (Ali Bey'in bildirdigi sorun: "logo ustu gozukmuyor"): ONCEDEN burada
            // System.Windows.Forms.Screen.PrimaryScreen.Bounds ile GERCEK ekran cozunurlugu
            // okunmaya calisiliyordu -- ama bu kod SYSTEM/"Oturum 0" baglaminda (bkz. sinif
            // basindaki aciklama) calisiyor, ve Oturum 0'in KENDI (gorunmez, etkilesimsiz)
            // oturumunun varsayilan/sanal cozunurlugunu dondurur -- kullanicinin GERCEKTEN gordugu
            // monitorun cozunurlugunu DEGIL. Sonuc: uretilen resim farkli bir en-boy oraniyla
          // cikiyor, Windows "Doldur" (Fill) modu de bunu gercek ekrana sigdirirken USTTEN VE
            // ALTTAN kirpiyordu (logonun ust yarisi/okul adi yazisi kayboluyordu).
            // DUZELTME: cozunurlugu TAHMIN ETMEYE CALISMAK yerine, tuvali SABIT 16:9 (1920x1080)
            // yap -- gunumuzde neredeyse TUM monitorler 16:9 oldugundan, Windows "Doldur" modu bu
            // orani HANGI gercek cozunurlukte olursa olsun (1366x768, 1920x1080, 2560x1440 vb.)
            // HICBIR KIRPMA OLMADAN tam oturtur (oran esit oldugu icin duz olcekleme yeterli).
            // Nadir bir 4:3/5:4 monitor olsa bile, tasarim dikeyde ortalanmis oldugundan yan
            // kirpilma (sadece bos gradyan kenarlari) onemli hicbir ogeyi kesmez.
            const int w = 1920, h = 1080;

            string makineAdi = Environment.MachineName ?? "";
            const float olcek = 1f;

            using (var bmp = new Bitmap(w, h))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                    // Lacivert gradyan zemin (Ali Bey'in sectigi "logo ustte" taslak).
                    Color ust = Color.FromArgb(8, 16, 30);
                    Color alt = Color.FromArgb(22, 34, 54);
                    using (var br = new LinearGradientBrush(new Rectangle(0, 0, w, h + 1), ust, alt, LinearGradientMode.Vertical))
                        g.FillRectangle(br, 0, 0, w, h);

                    int logoBoyut = (int)(560 * olcek);
                    int logoUst = (int)(40 * olcek);
                    try
                    {
                        using (var logo = LogoYukle())
                        {
                            if (logo != null) g.DrawImage(logo, (w - logoBoyut) / 2, logoUst, logoBoyut, logoBoyut);
                        }
                    }
                    catch { }

                    using (var fAd = new Font("Arial", 68f * olcek, FontStyle.Bold, GraphicsUnit.Pixel))
                    using (var fBilisim = new Font("Arial", 78f * olcek, FontStyle.Bold, GraphicsUnit.Pixel))
                    using (var fEtiket = new Font("Arial", 17f * olcek, FontStyle.Regular, GraphicsUnit.Pixel))
                    using (var frAd = new SolidBrush(Color.White))
                    using (var frBilisim = new SolidBrush(Color.FromArgb(245, 180, 70)))
                    using (var frEtiket = new SolidBrush(Color.FromArgb(160, 175, 195)))
                    {
                        float yAd = logoUst + logoBoyut + 70f * olcek;
                        OrtaYaziCiz(g, makineAdi, fAd, frAd, w / 2f, yAd);

                        float yBilisim = yAd + 155f * olcek;
                        // "BILISIM" degil, Turkce karakterlerle "BILISIM" -> B-i-L-i-S-i-M (uc tane
                        // noktali I/i): İ = buyuk noktali I (İ), Ş = buyuk S-cedilla (Ş).
                        OrtaYaziCiz(g, "BİLİŞİM", fBilisim, frBilisim, w / 2f, yBilisim);

                        OrtaYaziCiz(g, "Bodrum Mesleki ve Teknik Anadolu Lisesi", fEtiket, frEtiket, w / 2f, h - 45f * olcek);
                    }
                }

                Directory.CreateDirectory(Path.GetDirectoryName(hedefYol));
                // Once gecici dosyaya yaz, sonra yerine tasi -- etkilesimli gorev tam o anda
                // dosyayi okumaya calisirsa YARIM YAZILMIS bir PNG ile karsilasmasin diye.
                string gecici = hedefYol + ".tmp";
                bmp.Save(gecici, ImageFormat.Png);
                if (File.Exists(hedefYol)) File.Delete(hedefYol);
                File.Move(gecici, hedefYol);
            }
        }

        private static void OrtaYaziCiz(Graphics g, string metin, Font f, Brush fr, float cx, float cy)
        {
            var boyut = g.MeasureString(metin, f);
            g.DrawString(metin, f, fr, cx - boyut.Width / 2f, cy - boyut.Height / 2f);
        }

        private static Image LogoYukle()
        {
            var asm = Assembly.GetExecutingAssembly();
            using (var s = asm.GetManifestResourceStream("UnsalBakim.Resources.okul_logo.png"))
            {
                if (s == null) return null;
                using (var tmp = Image.FromStream(s))
                    return new Bitmap(tmp); // akisi (stream) kapanmadan ONCE bagimsiz bir kopya
            }
        }
    }
}
