using System;
using System.IO;
using Microsoft.Win32;

namespace UnsalBakim.Core
{
    /// <summary>
    /// v106 (Ali Bey'in istegi, 11 Kasim 2026: "Kumar olanlarini ekle, 16 yas ve alti"): Bilinen
    /// kumar/bahis web sitelerine erisimi engeller. AiSiteEngelle.cs'teki (chatgpt.com vb.) AYNI
    /// mekanizma/desen kopyalanarak yazildi (Edge/Chrome URLBlocklist + Firefox WebsiteFilter),
    /// SADECE kendi KENDINE AIT bir indis araliginda (95001+) -- SiteEngelle (1+), YoutubeKisit
    /// (91001+) ve AiSiteEngelle (93001+) ile CAKISMAZ.
    ///
    /// BILEREK ac/kapa ayari YOK -- Oyun Engelleme'deki (v101) ile AYNI felsefe: ogrenciler 16 yas
    /// ve altinda oldugu icin kumar/bahis engeli bir tercih degil, kosulsuz bir temel davranis.
    /// BaslangicTemizle/OyunKaldirici/MasaustuDuvarKagidi ailesine katilir: HER makinede (ogretmen
    /// dahil), HER bakim dongusunde kosulsuz uygulanir, hicbir panelde goruntulenmez/degistirilemez.
    /// </summary>
    public static class KumarSiteEngelle
    {
        private const string Log = "kumar_site.txt";
        private const int Taban = 95001; // SiteEngelle (1+), YoutubeKisit (91001+), AiSiteEngelle (93001+) ile CAKISMAZ.

        // AI ile buyutulebilir: ek_kumar_site.txt. Liste, Turkiye'de en cok bilinen lisansli/lisanssiz
        // bahis-kumar platformlarini kapsar; zaman icinde yeni alan adlari eklenebilir.
        public static readonly System.Collections.Generic.List<string> Siteler = new System.Collections.Generic.List<string>
        {
            "nesine.com", "bilyoner.com", "misli.com", "tuttur.com", "iddaa.com", "oley.com",
            "bets10.com", "betboo.com", "mobilbahis.com", "sekabet.com", "betpark.com",
            "superbahis.com", "betist.com", "1xbet.com", "casinomaxi.com", "wonbahis.com",
            "youwin.com", "betkom.com", "betturkey.com", "bahsegel.com", "bahiscom.com",
            "pinbahis.com", "matbet.com", "grandpashabet.com"
        };

        private static string EkDosya =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "UnsalBakim", "ek_kumar_site.txt");

        static KumarSiteEngelle()
        {
            try { if (File.Exists(EkDosya)) foreach (var l in File.ReadAllLines(EkDosya)) { var a = l.Trim(); if (a.Length > 0 && !a.StartsWith("#") && !Siteler.Contains(a)) Siteler.Add(a); } } catch { }
        }

        /// <summary>AI/elle yeni bir kumar/bahis sitesi ekler (kalici).</summary>
        public static bool EkleSite(string d)
        {
            d = (d ?? "").Trim().ToLowerInvariant();
            if (d.Length == 0 || Siteler.Contains(d)) return false;
            Siteler.Add(d);
            try { Directory.CreateDirectory(Path.GetDirectoryName(EkDosya)); File.AppendAllText(EkDosya, d + Environment.NewLine); } catch { }
            return true;
        }

        /// <summary>Engel su an uygulanmis mi (Edge/Chrome/Firefox ilk indis degeri var mi).</summary>
        public static bool EngelVarMi()
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

        public static void Engelle()
        {
            try { EdgeChrome(@"SOFTWARE\Policies\Microsoft\Edge"); } catch (Exception ex) { Logger.WriteFileOnly(Log, "Edge kumar site politikasi uygulanamadi: " + ex.Message); }
            try { EdgeChrome(@"SOFTWARE\Policies\Google\Chrome"); } catch (Exception ex) { Logger.WriteFileOnly(Log, "Chrome kumar site politikasi uygulanamadi: " + ex.Message); }
            try { Firefox(true); } catch (Exception ex) { Logger.WriteFileOnly(Log, "Firefox kumar site politikasi uygulanamadi: " + ex.Message); }
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
