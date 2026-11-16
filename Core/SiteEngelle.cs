using System;
using System.IO;
using Microsoft.Win32;

namespace UnsalBakim.Core
{
    /// <summary>
    /// Edge/Chrome URLBlocklist ve Firefox WebsiteFilter ile yaygin tarayici/cloud oyunlarini engeller.
    /// Mevcut URLBlocklist veri degerlerini korur; yalnizca bu uygulamanin sahiplik kaydindaki girdileri kaldirir.
    /// Liste tum internet oyunlarini kapsayamaz; yeni siteler Siteler listesine eklenebilir.
    /// </summary>
    public static class SiteEngelle
    {
        private const int Taban = 1;
        private const string SahiplikKoku = @"SOFTWARE\UnsalBakim\SiteEngelle\Owned";

        // Web oyun / cloud oyun siteleri (AI ile buyutulebilir: ek_oyun_site.txt). Ders/teneffus
        // farki YOK: oyun siteleri HER ZAMAN kapali (youtube/AI'den ayri, bunlar YoutubeKisit'te).
        public static readonly System.Collections.Generic.List<string> Siteler = new System.Collections.Generic.List<string>
        {
            "youtube.com/playables",
            "poki.com", "crazygames.com", "friv.com", "y8.com", "miniclip.com",
            "kizi.com", "silvergames.com", "coolmathgames.com", "addictinggames.com",
            "agame.com", "games.com", "now.gg", "roblox.com", "minecraft.net/play",
            "tlauncher.org", "llaun.ch", "gamesnacks.com", "gamepix.com", "armorgames.com",
            "kongregate.com", "lagged.com", "gameflare.com", "kbhgames.com", "playhop.com",
            "yandex.com/games", "gamaverse.com", "play-games.com", "gamemonetize.com",
            "agar.io", "slither.io", "diep.io", "paper-io.com", "skribbl.io", "krunker.io",
            "shellshock.io", "surviv.io", "iogames.space", "xbox.com/play", "play.geforcenow.com",
            "geforcenow.com", "boosteroid.com", "playboosteroid.com", "amazon.com/luna"
        };

        private static string EkDosya =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "UnsalBakim", "ek_oyun_site.txt");

        static SiteEngelle()
        {
            try { if (File.Exists(EkDosya)) foreach (var l in File.ReadAllLines(EkDosya)) { var a = l.Trim(); if (a.Length > 0 && !a.StartsWith("#") && !Siteler.Contains(a)) Siteler.Add(a); } } catch { }
        }

        /// <summary>AI/elle yeni web oyun sitesi ekler (kalici).</summary>
        public static bool EkleSite(string d)
        {
            d = (d ?? "").Trim().ToLowerInvariant();
            if (d.Length == 0 || Siteler.Contains(d)) return false;
            Siteler.Add(d);
            try { Directory.CreateDirectory(Path.GetDirectoryName(EkDosya)); File.AppendAllText(EkDosya, d + Environment.NewLine); } catch { }
            return true;
        }

        public static void Engelle()
        {
            try { ApplyBrowser("Edge", @"SOFTWARE\Policies\Microsoft\Edge"); } catch (Exception ex) { Logger.WriteFileOnly("oyun.txt", "Edge oyun site politikasi uygulanamadi: " + ex.Message); }
            try { ApplyBrowser("Chrome", @"SOFTWARE\Policies\Google\Chrome"); } catch (Exception ex) { Logger.WriteFileOnly("oyun.txt", "Chrome oyun site politikasi uygulanamadi: " + ex.Message); }
            try { ApplyFirefox(); } catch (Exception ex) { Logger.WriteFileOnly("oyun.txt", "Firefox oyun site politikasi uygulanamadi: " + ex.Message); }
            Logger.WriteFileOnly("oyun.txt", "Tarayici ve cloud oyun sitesi engeli yenilendi (" + Siteler.Count + " URL kalibi).");
        }

        public static void KaldirEngel()
        {
            try { RemoveBrowser("Edge", @"SOFTWARE\Policies\Microsoft\Edge"); } catch { }
            try { RemoveBrowser("Chrome", @"SOFTWARE\Policies\Google\Chrome"); } catch { }
            try { RemoveFirefox(); } catch { }
            Logger.WriteFileOnly("oyun.txt", "Yalnizca UnsalBakim tarafindan eklenmis web oyun sitesi engelleri kaldirildi.");
        }

        private static void ApplyBrowser(string ownerName, string policyPath)
        {
            string policy = policyPath + @"\URLBlocklist";
            string owner = SahiplikKoku + @"\" + ownerName;
            using (RegistryKey block = Registry.LocalMachine.CreateSubKey(policy))
            using (RegistryKey owned = Registry.LocalMachine.CreateSubKey(owner))
            {
                if (block == null || owned == null) return;
                RemoveOwnedValues(block, owned);
                CompactList(block); // Chrome/Edge registry listeleri 1,2,3... ardışık indeks ister.
                foreach (string site in Siteler)
                {
                    string name = NextFreeName(block, Taban);
                    block.SetValue(name, site, RegistryValueKind.String);
                    owned.SetValue(name, site, RegistryValueKind.String);
                }
            }
        }

        private static void RemoveBrowser(string ownerName, string policyPath)
        {
            using (RegistryKey block = Registry.LocalMachine.OpenSubKey(policyPath + @"\URLBlocklist", true))
            using (RegistryKey owned = Registry.LocalMachine.OpenSubKey(SahiplikKoku + @"\" + ownerName, true))
            {
                if (block == null || owned == null) return;
                RemoveOwnedValues(block, owned);
                CompactList(block);
            }
        }

        private static void ApplyFirefox()
        {
            const string policy = @"SOFTWARE\Policies\Mozilla\Firefox\WebsiteFilter\Block";
            using (RegistryKey block = Registry.LocalMachine.CreateSubKey(policy))
            using (RegistryKey owned = Registry.LocalMachine.CreateSubKey(SahiplikKoku + @"\Firefox"))
            {
                if (block == null || owned == null) return;
                RemoveOwnedValues(block, owned);
                CompactList(block);
                foreach (string site in Siteler)
                {
                    string name = NextFreeName(block, Taban);
                    string pattern = FirefoxPattern(site);
                    block.SetValue(name, pattern, RegistryValueKind.String);
                    owned.SetValue(name, pattern, RegistryValueKind.String);
                }
            }
        }

        private static void RemoveFirefox()
        {
            using (RegistryKey block = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Mozilla\Firefox\WebsiteFilter\Block", true))
            using (RegistryKey owned = Registry.LocalMachine.OpenSubKey(SahiplikKoku + @"\Firefox", true))
            {
                if (block == null || owned == null) return;
                RemoveOwnedValues(block, owned);
                CompactList(block);
            }
        }

        // Firefox WebsiteFilter expects WebExtension match patterns; a bare or
        // middle-wildcard host such as *poki.com* is not a valid host pattern.
        private static string FirefoxPattern(string site)
        {
            if (string.IsNullOrWhiteSpace(site)) return "";
            string cleaned = site.Trim().Trim('/');
            int slash = cleaned.IndexOf('/');
            string host = slash < 0 ? cleaned : cleaned.Substring(0, slash);
            string path = slash < 0 ? "/*" : "/" + cleaned.Substring(slash + 1) + "*";
            return "*://*." + host + path;
        }

        private sealed class ListItem
        {
            public int Index;
            public object Value;
            public RegistryValueKind Kind;
        }

        /// <summary>
        /// Numeric registry values define a policy list. Preserve their data/types but rewrite
        /// numeric value names contiguously from 1, otherwise Chromium-based browsers may ignore
        /// our entries that start at a high index or that follow a gap.
        /// </summary>
        private static void CompactList(RegistryKey block)
        {
            if (block == null) return;
            var entries = new System.Collections.Generic.List<ListItem>();
            foreach (string name in block.GetValueNames())
            {
                int index;
                if (!int.TryParse(name, out index)) continue;
                try
                {
                    entries.Add(new ListItem
                    {
                        Index = index,
                        Value = block.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames),
                        Kind = block.GetValueKind(name)
                    });
                }
                catch { }
            }
            entries.Sort((a, b) => a.Index.CompareTo(b.Index));
            foreach (string name in block.GetValueNames())
            {
                int index;
                if (int.TryParse(name, out index))
                {
                    try { block.DeleteValue(name, false); } catch { }
                }
            }
            for (int i = 0; i < entries.Count; i++)
            {
                try { block.SetValue((i + 1).ToString(), entries[i].Value, entries[i].Kind); }
                catch { }
            }
        }

        private static void RemoveOwnedValues(RegistryKey block, RegistryKey owned)
        {
            foreach (string name in owned.GetValueNames())
            {
                string ourValue = owned.GetValue(name) as string;
                string current = block.GetValue(name) as string;
                // Bir yonetici ayni kaydi degistirmisse onun degerini silme.
                if (ourValue != null && string.Equals(current, ourValue, StringComparison.Ordinal))
                {
                    try { block.DeleteValue(name, false); } catch { }
                }
                try { owned.DeleteValue(name, false); } catch { }
            }
        }

        private static string NextFreeName(RegistryKey key, int start)
        {
            int n = start;
            while (key.GetValue(n.ToString()) != null && n < int.MaxValue - 1) n++;
            return n.ToString();
        }
    }
}
