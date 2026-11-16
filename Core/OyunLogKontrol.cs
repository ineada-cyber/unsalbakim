using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;

namespace UnsalBakim.Core
{
    /// <summary>
    /// Windows'ta mevcutsa oyun calistirilmis olabilecegine dair izleri arar.
    /// Prefetch ve UserAssist tam/eksiksiz gecmis degildir; yalnizca kanit olarak raporlanir.
    /// </summary>
    public static class OyunLogKontrol
    {
        private static readonly string[] BilinenOyunIzleri =
        {
            "valorant", "leagueclient", "league of legends", "fortnite", "roblox",
            "minecraft", "cs2", "csgo", "counter-strike", "pubg", "gta5", "gta v",
            "mxbikes", "zula", "knight online", "metin2", "wolfteam", "point blank",
            "warzone", "apex legends", "dota2", "rainbow six", "rocket league",
            "among us", "fall guys", "genshin", "overwatch", "world of warcraft",
            "tlauncher", "sklauncher", "prism launcher", "lunarclient", "badlionclient",
            "gameloop", "bluestacks", "ldplayer", "noxvmhandle",
            // v89 (Ali Bey'in istegi, 9 Kasim 2026: "oyun azaldi ama hala var"): emulatorler --
            // okullarda en sik kacirilan yontem, "oyun.exe" yerine zararsiz gorunen bir emulator
            // calistirip ICINDE mobil/konsol oyunu oynamak.
            "mumuplayer", "mumu player", "memuplayer", "memu play", "andyroid", "genymotion",
            "ryujinx", "yuzu", "rpcs3", "pcsx2", "dolphin emu", "ppsspp", "duckstation",
            "cemu", "citra", "retroarch", "epsxe", "project64", "pcsxr", "xenia",
            "moonlight", "parsec", "geforce now", "shadow pc", "steam link", "chiaki",
            // v100: GameControl.Surecler/UsbOyunKontrol.OyunImzalari ile senkron.
            "auto clicker"
        };

        public static int Tara(bool ayrintili = true)
        {
            const string log = "oyun_log.txt";
            var bulunan = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                string dizin = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Prefetch");
                if (Directory.Exists(dizin))
                {
                    foreach (string dosya in Directory.GetFiles(dizin, "*.pf"))
                    {
                        string ad = Path.GetFileNameWithoutExtension(dosya);
                        if (OyunMu(ad)) bulunan.Add("Prefetch: " + ad);
                    }
                }
            }
            catch (Exception ex) { Logger.WriteFileOnly(log, "Prefetch okunamadi: " + ex.Message); }

            // Elle calistirmada CurrentUser, SYSTEM gorevinde ise Registry.Users altindaki
            // yuklu kullanici profilleri taranir. Yuklu olmayan profillerin NTUSER.DAT'i acilmaz.
            try { TaraUserAssist(Registry.CurrentUser, bulunan); }
            catch (Exception ex) { Logger.WriteFileOnly(log, "CurrentUser UserAssist okunamadi: " + ex.Message); }
            try
            {
                using (RegistryKey users = Registry.Users)
                {
                    foreach (string sid in users.GetSubKeyNames())
                    {
                        if (sid.EndsWith("_Classes", StringComparison.OrdinalIgnoreCase)) continue;
                        if (sid.Equals(".DEFAULT", StringComparison.OrdinalIgnoreCase)) continue;
                        try { using (RegistryKey profile = users.OpenSubKey(sid)) TaraUserAssist(profile, bulunan); } catch { }
                    }
                }
            }
            catch (Exception ex) { Logger.WriteFileOnly(log, "Yuklu kullanici profilleri okunamadi: " + ex.Message); }

            // ayrintili=true (elle/panelden cagrildi) -> ekrana da yaz; false (3 dk arka plan) -> yalnizca dosya.
            Action<string, string> yaz = ayrintili ? (Action<string, string>)Logger.Write : Logger.WriteFileOnly;
            yaz(log, "Oyun gecmisi taramasi tamamlandi. Bulunan iz: " + bulunan.Count + ".");
            if (ayrintili)
            {
                foreach (string iz in bulunan) yaz(log, "GECMIS IZI: " + iz);
                if (bulunan.Count == 0)
                    yaz(log, "Bilgi: Erisilebilir Prefetch/UserAssist kaynaklarinda bilinen oyun izi bulunamadi. Bu, oyun oynanmadigini kanitlamaz.");
            }
            return bulunan.Count;
        }

        private static void TaraUserAssist(RegistryKey kok, HashSet<string> bulunan)
        {
            if (kok == null) return;
            using (RegistryKey ua = kok.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\UserAssist"))
            {
                if (ua == null) return;
                foreach (string guid in ua.GetSubKeyNames())
                {
                    using (RegistryKey count = ua.OpenSubKey(guid + @"\Count"))
                    {
                        if (count == null) continue;
                        foreach (string valueName in count.GetValueNames())
                        {
                            string decoded = Rot13(valueName);
                            if (OyunMu(decoded)) bulunan.Add("UserAssist: " + decoded);
                        }
                    }
                }
            }
        }

        private static bool OyunMu(string metin)
        {
            if (string.IsNullOrEmpty(metin)) return false;
            string s = metin.ToLowerInvariant();
            foreach (string oyun in BilinenOyunIzleri)
                if (s.Contains(oyun)) return true;
            return false;
        }

        private static string Rot13(string s)
        {
            char[] c = s.ToCharArray();
            for (int i = 0; i < c.Length; i++)
            {
                if (c[i] >= 'a' && c[i] <= 'z') c[i] = (char)('a' + (c[i] - 'a' + 13) % 26);
                else if (c[i] >= 'A' && c[i] <= 'Z') c[i] = (char)('A' + (c[i] - 'A' + 13) % 26);
            }
            return new string(c);
        }
    }
}
