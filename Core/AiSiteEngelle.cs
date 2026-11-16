using System;
using System.IO;
using Microsoft.Win32;

namespace UnsalBakim.Core
{
    /// <summary>
    /// v55: Yapay zeka (ChatGPT, Gemini, Claude.ai, vb.) web sitelerine erisimi engeller.
    /// BILEREK YoutubeKisit'ten AYRI tutulur -- iki farkli ayar/anahtar:
    ///   - YoutubeKisit.YoutubeKisitli: SADECE YouTube.
    ///   - AppSettings.AiSiteEngelle (bu dosya): AI siteleri.
    /// v57: Artik ikisi de AYNI mantikla calisiyor -- HER ZAMAN acik/kapali (filo??yerel ayar,
    /// saate/molaya bakilmaz), ogretmenin/okulun bilincli tercihine gore degistirilir.
    /// Bu ayni zamanda haftalik AI arka plan taramasindan (YapayZeka.cs, Claude API cagrisi) da
    /// TAMAMEN BAGIMSIZDIR -- o, ogrencinin bilgisayarindaki calisan bilinmeyen oyunlari yakalamak
    /// icin Anthropic API'sine GIDEN bir ozellik; bu dosya ise ogrencinin TARAYICIDAN AI sitelerine
    /// GITMESINI engelleyen bir web filtresi. Ikisi karistirilmamali.
    /// Engel: Edge/Chrome URLBlocklist + Firefox WebsiteFilter (diger iki site listesinden -- oyun
    /// sitesi 1+, YouTube 91001+ -- AYRI bir indis araliginda, 93001+).
    /// </summary>
    public static class AiSiteEngelle
    {
        private const string Log = "ai_site.txt";
        private const int Taban = 93001; // SiteEngelle (1+) ve YoutubeKisit (91001+) ile CAKISMAZ.

        // AI ile buyutulebilir: ek_ai_site.txt
        public static readonly System.Collections.Generic.List<string> Siteler = new System.Collections.Generic.List<string>
        {
            "chatgpt.com", "chat.openai.com", "openai.com", "gemini.google.com", "bard.google.com",
            "copilot.microsoft.com", "claude.ai", "perplexity.ai", "poe.com", "character.ai",
            "deepseek.com", "grok.com", "x.ai", "huggingface.co", "you.com", "pi.ai", "blackbox.ai"
        };

        private static string EkDosya =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "UnsalBakim", "ek_ai_site.txt");

        static AiSiteEngelle()
        {
            try { if (File.Exists(EkDosya)) foreach (var l in File.ReadAllLines(EkDosya)) { var a = l.Trim(); if (a.Length > 0 && !a.StartsWith("#") && !Siteler.Contains(a)) Siteler.Add(a); } } catch { }
        }

        /// <summary>AI/elle yeni AI sitesi ekler (kalici).</summary>
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
            try { EdgeChrome(@"SOFTWARE\Policies\Microsoft\Edge"); } catch (Exception ex) { Logger.WriteFileOnly(Log, "Edge AI site politikasi uygulanamadi: " + ex.Message); }
            try { EdgeChrome(@"SOFTWARE\Policies\Google\Chrome"); } catch (Exception ex) { Logger.WriteFileOnly(Log, "Chrome AI site politikasi uygulanamadi: " + ex.Message); }
            try { Firefox(true); } catch (Exception ex) { Logger.WriteFileOnly(Log, "Firefox AI site politikasi uygulanamadi: " + ex.Message); }
            Logger.WriteFileOnly(Log, "AI web siteleri ENGELLI (" + Siteler.Count + " site).");
        }

        public static void KaldirEngel()
        {
            try { EdgeChromeKaldir(@"SOFTWARE\Policies\Microsoft\Edge"); } catch { }
            try { EdgeChromeKaldir(@"SOFTWARE\Policies\Google\Chrome"); } catch { }
            try { IcerikTemizle(@"SOFTWARE\Policies\Microsoft\Edge"); } catch { }
            try { IcerikTemizle(@"SOFTWARE\Policies\Google\Chrome"); } catch { }
            try { Firefox(false); } catch { }
            Logger.WriteFileOnly(Log, "AI web sitesi engeli kaldirildi (serbest).");
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
