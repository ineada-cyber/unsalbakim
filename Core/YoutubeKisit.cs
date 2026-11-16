using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace UnsalBakim.Core
{
    /// <summary>
    /// YouTube'u filo??yerel ayara gore ENGELLER/SERBEST BIRAKIR -- Oyun/VPN/AI ile AYNI
    /// zamandan BAGIMSIZ, sabit acik/kapali mantigi (v57). Ders saati/teneffus ayrimi
    /// KALDIRILDI: hoca ne secerse (filodan veya yerelden) o deger molaya/saate
    /// bakilmadan surekli uygulanir; otomatik gecis yoktur, degisiklik hocanin elinde.
    /// Engel: Edge/Chrome URLBlocklist + Firefox WebsiteFilter.
    /// v54: Opera icin desteklendigi dogrulanmamis bir tarayici policy kaydi yazilmaz;
    /// kisit AKTIFKEN opera.exe / opera_gx.exe surecleri kapatilir (ders saati sarti yok,
    /// kisit ne zaman acik ise Opera o surece kapali kalir).
    /// v55: Yapay zeka siteleri (ChatGPT, Gemini, Claude.ai vb.) buradan AYRILDI -- artik ayri,
    /// Core/AiSiteEngelle.cs'te. Bu dosyada yalnizca GERCEK YouTube/video akis alan adlari kalir.
    /// </summary>
    public static class YoutubeKisit
    {
        private const string Log = "youtube.txt";
        private const int Taban = 91001; // oyun site engeli 90001'den ayri

        // v90 (Ali Bey'in gozlemi, 9 Kasim 2026: "sistem ilk acilista operada aciliyor olabilir,
        // vpn kalmis degil aynen duruyor"): Opera'nin KENDI ICINDE ucretsiz, yerlesik bir VPN
        // ozelligi var -- VpnKontrol.cs'teki 3. parti VPN listesi bunu YAKALAYAMAZ (ayri bir .exe
        // degil, opera.exe'nin kendisi). Eski yontem (OperaDurdur -- 5 dk'lik dongude bir kez
        // oldurme) ile acilis ani/doner-kapanis arasinda bir bosluk vardi. VpnKontrol.cs'teki AYNI
        // IFEO (Image File Execution Options) teknigi uygulanarak Opera'nin calismasi, baslatan ne
        // olursa olsun (kisayol, startup, taskbar) ANINDA engellenir -- dongu beklemez.
        private const string IFEO = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options";
        private const string OperaMarker = "UnsalBakimOpera";
        private static readonly string[] OperaExeAdlari = { "opera.exe", "opera_gx.exe" };

        // YouTube siteleri: kisit acikken KAPALI, kapaliyken ACIK.
        // v55: Yapay zeka siteleri BURADAN TASINDI -> Core/AiSiteEngelle.cs (ayri, ders saatinden
        // bagimsiz bir ayar/liste). AI ile buyutulebilir: ek_youtube.txt (sadece YouTube/video domainleri icin).
        public static readonly System.Collections.Generic.List<string> Siteler = new System.Collections.Generic.List<string>
        {
            // YouTube (googlevideo.com = gercek video akis CDN'i, ytimg.com/ggpht.com = kucuk resim
            // CDN'leri; bunlar olmadan ogrenci youtube.com'a hic girmeden google.com uzerinden
            // video akisini dogrudan googlevideo.com'dan cekip engeli atlatabiliyordu)
            "youtube.com", "youtu.be", "youtube-nocookie.com", "googlevideo.com", "ytimg.com", "ggpht.com"
        };

        private static string EkDosya =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "UnsalBakim", "ek_youtube.txt");

        static YoutubeKisit()
        {
            try { if (File.Exists(EkDosya)) foreach (var l in File.ReadAllLines(EkDosya)) { var a = l.Trim(); if (a.Length > 0 && !a.StartsWith("#") && !Siteler.Contains(a)) Siteler.Add(a); } } catch { }
        }

        /// <summary>AI/elle yeni site ekler (kalici).</summary>
        public static bool EkleSite(string d)
        {
            d = (d ?? "").Trim().ToLowerInvariant();
            if (d.Length == 0 || Siteler.Contains(d)) return false;
            Siteler.Add(d);
            try { Directory.CreateDirectory(Path.GetDirectoryName(EkDosya)); File.AppendAllText(EkDosya, d + Environment.NewLine); } catch { }
            return true;
        }

        /// <summary>Kisit ayarini kontrol eder ve YouTube engelini duruma gore ayarlar (gorev her
        /// 1 dk cagirir). Yerel AppSettings.YoutubeKisitli degerini kullanir. v57: saat/ders
        /// kontrolu YOK -- ayar ne ise o surekli uygulanir.</summary>
        public static void SaatiUygula()
        {
            var ayar = new AppSettings(); ayar.Load();
            SaatiUygula(ayar.YoutubeKisitli);
        }

        /// <summary>v55: Filo Yonetimi -- cagiran taraf (Program.cs) EFEKTIF (filo politikasi ??
        /// yerel ayar) degerini hesaplayip buraya verebilir; bu overload AppSettings'e tekrar
        /// bakmaz, verilen degeri esas alir. v57: zamana bakmadan, deger ne ise onu uygular --
        /// Oyun/VPN/AI engelleriyle AYNI davranis.
        /// v57.2 (Ali Bey'in talebi, 3 Kasim 2026): Opera artik YouTube kisitlamasindan TAMAMEN
        /// BAGIMSIZ -- kisit acik/kapali ne olursa olsun HER ZAMAN kapatilir. Opera'da dogrulanmis
        /// bir URLBlocklist yonetim policy yolu olmadigi icin tek guvenilir engelleme yontemi
        /// tarayici surecini kapatmak; bunu kisit durumuna bagli tutmak "YouTube acik ama Opera da
        /// acik" gibi bir bosluk birakiyordu.</summary>
        public static void SaatiUygula(bool kisitliMi)
        {
            bool engelli = EngelVarMi();
            if (kisitliMi) { if (!engelli) Engelle(); }
            else if (engelli) KaldirEngel();
            // v90: Opera, IFEO ile ANINDA (baslatan ne olursa olsun) engellenir -- dongu beklemez.
            // Idempotent oldugu icin her turda yeniden uygulamak zararsiz (zaten varsa degismez).
            OperaIfeoEngelle();
            // Zaten calismakta olan bir Opera varsa (IFEO uygulanmadan ONCE acilmissa) onu da kapat.
            OperaDurdur();
        }

        /// <summary>
        /// v90: Opera'nin calismasini IFEO (Image File Execution Options) ile KOKTEN engeller --
        /// VpnKontrol.cs'teki AYNI teknik. opera.exe/opera_gx.exe HANGI YOLDAN baslatilirsa
        /// baslatilsin (kisayol, startup, taskbar, baska bir program) Windows'un kendisi aninda
        /// taskkill ile kapatir; UnsalBakim'in dongu/polling beklemesi GEREKMEZ. Bu, hem "ilk
        /// acilista Opera aciliyor" hem "Opera icindeki yerlesik VPN" sorununu AYNI ANDA cozer --
        /// Opera hicbir sekilde ayaga kalkamaz.
        /// </summary>
        public static void OperaIfeoEngelle()
        {
            string taskkill = Path.Combine(Environment.SystemDirectory, "taskkill.exe");
            try
            {
                using (var kok = Registry.LocalMachine.CreateSubKey(IFEO))
                {
                    foreach (var exe in OperaExeAdlari)
                    {
                        try
                        {
                            using (var k = kok.CreateSubKey(exe))
                            {
                                k.SetValue("Debugger", "\"" + taskkill + "\" /f /im \"" + exe + "\"", RegistryValueKind.String);
                                k.SetValue(OperaMarker, 1, RegistryValueKind.DWord);
                            }
                        }
                        catch (Exception ex) { Logger.WriteFileOnly(Log, "Opera IFEO engeli (" + exe + ") uygulanamadi: " + ex.Message); }
                    }
                }
            }
            catch (Exception ex) { Logger.WriteFileOnly(Log, "Opera IFEO engeli uygulanamadi: " + ex.Message); }
        }

        /// <summary>v90: OperaIfeoEngelle'nin kaldirilmasi (ornegin "Askiya Al" sirasinda).</summary>
        public static void OperaIfeoEngelKaldir()
        {
            try
            {
                using (var kok = Registry.LocalMachine.OpenSubKey(IFEO, true))
                {
                    if (kok == null) return;
                    foreach (var alt in kok.GetSubKeyNames())
                    {
                        try
                        {
                            using (var k = kok.OpenSubKey(alt))
                                if (k != null && k.GetValue(OperaMarker) != null) { k.Close(); kok.DeleteSubKeyTree(alt, false); }
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex) { Logger.WriteFileOnly(Log, "Opera IFEO engeli kaldirilamadi: " + ex.Message); }
        }

        /// <summary>
        /// Opera'nin guncel Windows surumlerinde URLBlocklist icin desteklenen ve dogrulanmis
        /// bir yonetim policy yolu kaynaklarda teyit edilemedigi icin, ders saatinde Opera
        /// tarayici surecleri kapatilir. Bu islem tum Opera pencerelerini kapatir; acik formlar,
        /// indirilen dosya islemleri veya kaydedilmemis oturumlar etkilenebilir.
        /// v90: artik sadece YEDEK -- asil engelleme OperaIfeoEngelle() ile ANINDA yapiliyor, bu
        /// metod sadece IFEO uygulanmadan ONCE zaten acik olan bir Opera'yi temizler.
        /// </summary>
        private static void OperaDurdur()
        {
            foreach (string ad in new[] { "opera", "opera_gx" })
            {
                Process[] surecler;
                try { surecler = Process.GetProcessesByName(ad); }
                catch (Exception ex)
                {
                    Logger.WriteFileOnly(Log, ad + " surecleri listelenemedi: " + ex.Message);
                    continue;
                }

                foreach (Process p in surecler)
                {
                    int pid = -1;
                    try
                    {
                        pid = p.Id;
                        p.Kill();
                        p.WaitForExit(1500);
                        Logger.WriteFileOnly(Log, "Opera tarayicisi ders saatinde kapatildi (PID " + pid + ").");
                    }
                    catch (Exception ex)
                    {
                        Logger.WriteFileOnly(Log, "Opera kapatilamadi (PID " + pid + "): " + ex.Message);
                    }
                    finally { try { p.Dispose(); } catch { } }
                }
            }
        }

        /// <summary>Engel su an uygulanmis mi (Edge/Chrome/Firefox ilk indis degeri var mi).</summary>
        private static bool EngelVarMi()
        {
            try
            {
                foreach (var yol in new[] { @"SOFTWARE\Policies\Microsoft\Edge\URLBlocklist", @"SOFTWARE\Policies\Google\Chrome\URLBlocklist", @"SOFTWARE\Policies\Mozilla\Firefox\WebsiteFilter\Block" })
                    using (var k = Registry.LocalMachine.OpenSubKey(yol))
                        if (k != null && k.GetValue(Taban.ToString()) != null) return true;
            }
            catch { }
            return false;
        }

        /// <summary>Bizim sitelerimizi iceren URLBlocklist degerlerini (indisten bagimsiz) temizler.</summary>
        private static void IcerikTemizle(string polYol)
        {
            using (var bl = Registry.LocalMachine.OpenSubKey(polYol + @"\URLBlocklist", true))
            {
                if (bl == null) return;
                foreach (var ad in bl.GetValueNames())
                {
                    try
                    {
                        string deger = (bl.GetValue(ad) as string ?? "").ToLowerInvariant();
                        foreach (var s in Siteler)
                            if (deger.Contains(s.ToLowerInvariant())) { bl.DeleteValue(ad, false); break; }
                    }
                    catch { }
                }
            }
        }

        public static void Engelle()
        {
            try { EdgeChrome(@"SOFTWARE\Policies\Microsoft\Edge"); } catch { }
            try { EdgeChrome(@"SOFTWARE\Policies\Google\Chrome"); } catch { }
            try { Firefox(true); } catch { }
            Logger.WriteFileOnly(Log, "YouTube ENGELLI (ders saati).");
        }

        public static void KaldirEngel()
        {
            // Hem indis tabanli hem icerik tabanli temizle (ne yazildiysa gitsin).
            try { EdgeChromeKaldir(@"SOFTWARE\Policies\Microsoft\Edge"); } catch { }
            try { EdgeChromeKaldir(@"SOFTWARE\Policies\Google\Chrome"); } catch { }
            try { IcerikTemizle(@"SOFTWARE\Policies\Microsoft\Edge"); } catch { }
            try { IcerikTemizle(@"SOFTWARE\Policies\Google\Chrome"); } catch { }
            try { Firefox(false); } catch { }
            Logger.WriteFileOnly(Log, "YouTube/AI engeli KALDIRILDI (serbest).");
        }

        private static void EdgeChrome(string polYol)
        {
            using (var bl = Registry.LocalMachine.CreateSubKey(polYol + @"\URLBlocklist"))
            {
                if (bl == null) return;
                int i = Taban;
                foreach (var s in Siteler) { bl.SetValue(i.ToString(), s, RegistryValueKind.String); i++; }
            }
        }

        private static void EdgeChromeKaldir(string polYol)
        {
            using (var bl = Registry.LocalMachine.OpenSubKey(polYol + @"\URLBlocklist", true))
            {
                if (bl == null) return;
                for (int i = Taban; i < Taban + Siteler.Count + 5; i++)
                    try { bl.DeleteValue(i.ToString(), false); } catch { }
            }
        }

        private static void Firefox(bool engelle)
        {
            using (var bl = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Mozilla\Firefox\WebsiteFilter\Block"))
            {
                if (bl == null) return;
                if (engelle)
                {
                    int i = Taban;
                    foreach (var s in Siteler) { bl.SetValue(i.ToString(), "*://*" + s + "*", RegistryValueKind.String); i++; }
                }
                else
                {
                    for (int i = Taban; i < Taban + Siteler.Count + 5; i++)
                        try { bl.DeleteValue(i.ToString(), false); } catch { }
                }
            }
        }
    }
}
