using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace UnsalBakim.Core
{
    /// <summary>
    /// Yalnizca taninan oyun urun adlarini hedefler. Oyun baslaticilari, araclar,
    /// gelistirici/editor yazilimlari, anti-cheat ve destek bilesenleri otomatik silinmez.
    /// </summary>
    public static class OyunKaldirici
    {
        private sealed class OyunKaydi
        {
            public string Ad;
            public string Komut;
            public string SessizKomut;
            public string NormalKomut;
        }

        // Urun adi imzalari. Steam/Epic/Riot ve launcher isimleri bilerek eklenmedi.
        private static readonly string[] OyunAnahtarlari =
        {
            "valorant", "league of legends", "fortnite", "roblox", "minecraft",
            "counter-strike", "counter strike", "cs:go", "pubg", "grand theft auto",
            "gta v", "gta5", "knight online", "metin2", "wolfteam", "point blank",
            "zula", "mx bikes", "mxbikes", "call of duty", "warzone", "apex legends",
            "dota 2", "dota2", "rainbow six", "rocket league", "among us", "fall guys",
            "genshin impact", "world of warcraft", "overwatch", "ea sports fc", "fifa ",
            "efootball", "pes ", "brawlhalla", "brawl stars", "crossfire", "team fortress 2",
            "terraria", "phasmophobia", "dead by daylight"
        };

        // Eslesme oyun adini iceriyor olsa bile bunlar oyun disi bilesen/proje araclaridir.
        private static readonly string[] OyunDisiImzalar =
        {
            "launcher", "studio", "editor", "dedicated server", "server tools", "server",
            "sdk", "software development", "development kit", "anti-cheat", "anticheat",
            "vanguard", "redist", "redistributable", "support", "crash handler", "crash report",
            "updater", "update service", "mod manager", "modloader", "mod loader", "modpack",
            "map editor", "creator", "education edition", "educational", "benchmark",
            "overlay", "webview", "runtime", "voice chat", "uninstall helper", "dedicated",
            "tracker", "stats", "statistics", "companion", "companion app", "companionapp",
            "mod tool", "modding tool", "game tools", "tools for", "game server", "server manager",
            "installer", "install service", "download manager", "patcher", "launcher service",
            "configuration tool", "configuration utility", "configuration editor", "save editor",
            "savegame editor", "trainer", "cheat", "wallhack", "skin changer", "map pack"
        };

        /// <summary>Kurulu ve taninan hedef oyun adlarini dondurur; kaldirma yapmaz.</summary>
        public static IList<string> KuruluHedefOyunlariGetir()
        {
            var sonuc = new List<string>();
            var gorulen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (OyunKaydi kayit in KayitlariOku())
                if (!string.IsNullOrWhiteSpace(kayit.Ad) && gorulen.Add(kayit.Ad)) sonuc.Add(kayit.Ad);
            sonuc.Sort(StringComparer.OrdinalIgnoreCase);
            return sonuc;
        }

        /// <summary>Arayuzden acikca istenen, whitelist icindeki secili oyunlari kaldirmayi dener.</summary>
        public static int Calistir(IEnumerable<string> secilenOyunlar, bool loglariTara)
        {
            if (loglariTara)
            {
                try { OyunLogKontrol.Tara(false); }
                catch (Exception ex) { Logger.WriteFileOnly("oyun_log.txt", "Log tarama hatasi: " + ex.Message); }
            }

            var secilen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (secilenOyunlar != null)
                foreach (string ad in secilenOyunlar)
                    if (!string.IsNullOrWhiteSpace(ad) && OyunMu(ad)) secilen.Add(ad.Trim());

            int basarili = 0;
            var komutlar = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (OyunKaydi kayit in KayitlariOku(false))
            {
                if (!secilen.Contains(kayit.Ad) || string.IsNullOrWhiteSpace(kayit.Komut)) continue;
                if (!komutlar.Add(kayit.Komut.Trim())) continue;
                Logger.WriteFileOnly("kurulum.txt", "Secilmis oyun icin kaldirma deneniyor: " + kayit.Ad);
                if (Kaldir(kayit.Komut))
                {
                    basarili++;
                    Logger.WriteFileOnly("kurulum.txt", "Kaldirma islemi basarili (cikis kodu 0/3010): " + kayit.Ad);
                    EngelKayit.Ekle(kayit.Ad, "kaldirma");
                }
                else Logger.WriteFileOnly("kurulum.txt", "Kaldirma basarisiz veya desteklenmeyen sessiz komut: " + kayit.Ad);
            }
            Logger.WriteFileOnly("kurulum.txt", "Secilen oyun kaldirma denemesi bitti; basarili islem: " + basarili + ".");
            return basarili;
        }

        /// <summary>
        /// Periyodik arka plan denetimi icin: yalnizca whitelist'teki kurulu oyunlari
        /// sessiz kaldirmayi dener. Gecmis Prefetch/UserAssist izi tek basina kaldirma sebebi olmaz.
        /// Bilinmeyen sessiz kaldirma komutlari calistirilmaz; hicbir pencere acilmaz.
        /// </summary>
        public static int OtomatikHedefOyunlariKaldir()
        {
            int basarili = 0;
            int aday = 0;
            var komutlar = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            // SYSTEM gorevinde kullanici tarafindan yazilabilir HKCU/HKU komutlarini asla calistirma.
            // Otomatik kaldirma yalnizca makine genelindeki (HKLM) kayitlar uzerinden yapilir.
            foreach (OyunKaydi kayit in KayitlariOku(false))
            {
                if (!OyunMu(kayit.Ad) || string.IsNullOrWhiteSpace(kayit.NormalKomut) && string.IsNullOrWhiteSpace(kayit.SessizKomut))
                    continue;
                string kimlik = !string.IsNullOrWhiteSpace(kayit.SessizKomut) ? kayit.SessizKomut.Trim() : kayit.NormalKomut.Trim();
                if (!komutlar.Add(kimlik)) continue;
                aday++;
                bool sonuc = KaldirSessizGuvenli(kayit);
                if (sonuc)
                {
                    basarili++;
                    Logger.WriteFileOnly("kurulum.txt", "Otomatik hedef oyun kaldirildi (cikis kodu 0/3010): " + kayit.Ad);
                    EngelKayit.Ekle(kayit.Ad, "kaldirma");
                }
            }
            Logger.WriteFileOnly("oyun_log.txt", "Kurulu oyun bakimi tamamlandi. Guvenli eslesme: " + aday + ", basarili kaldirma: " + basarili + ".");
            return basarili;
        }

        // Yalnizca MainForm'dan elle cagrilir (arka plan dongusunde kullanilmaz) -> her zaman ekrana da yaz.
        public static int KuruluOyunlariRaporla()
        {
            IList<string> oyunlar = KuruluHedefOyunlariGetir();
            Logger.Write("oyun_log.txt", "Kurulu whitelist hedef oyun sayisi: " + oyunlar.Count + ".");
            foreach (string oyun in oyunlar) Logger.Write("oyun_log.txt", "KURULU HEDEF OYUN: " + oyun);
            if (oyunlar.Count == 0)
                Logger.Write("oyun_log.txt", "Bilgi: Kurulu programlar arasinda taninan (whitelist) oyun bulunamadi.");
            return oyunlar.Count;
        }

        private static bool KaldirSessizGuvenli(OyunKaydi kayit)
        {
            string komut = !string.IsNullOrWhiteSpace(kayit.SessizKomut) ? kayit.SessizKomut : kayit.NormalKomut;
            if (string.IsNullOrWhiteSpace(komut)) return false;

            try
            {
                // MSI kaldirma yalnizca HKLM kurulum kayitlarindan gelen urun GUID'iyle yapilir.
                if (komut.IndexOf("msiexec", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    string guid = GuidBul(komut);
                    if (string.IsNullOrWhiteSpace(guid))
                    {
                        Logger.WriteFileOnly("kurulum.txt", "Otomatik kaldirma atlandi; MSI urun GUID'i yok: " + kayit.Ad);
                        return false;
                    }
                    return CalistirBekle("msiexec.exe", "/x " + guid + " /qn /norestart", 180000);
                }

                string exe, args;
                Ayir(komut, out exe, out args);
                if (string.IsNullOrWhiteSpace(exe)) return false;
                string dosya = Path.GetFileName(exe);
                bool innoKaldirici = dosya.StartsWith("unins", StringComparison.OrdinalIgnoreCase) &&
                    dosya.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);

                // SYSTEM yetkisiyle rastgele QuietUninstallString calistirilmasin.
                // Yalnizca mevcut, Inno Setup adlandirma bicimine uyan Program Files kaldiricisi kabul edilir.
                if (!innoKaldirici || !File.Exists(exe) || !ProgramFilesAltinda(exe))
                {
                    Logger.WriteFileOnly("kurulum.txt", "Otomatik kaldirma guvenli bulunmadigi icin atlandi: " + kayit.Ad);
                    return false;
                }
                args = (args + " /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-").Trim();
                return CalistirBekle(exe, args, 180000);
            }
            catch (Exception ex)
            {
                Logger.WriteFileOnly("kurulum.txt", "Otomatik kaldirma hatasi (" + kayit.Ad + "): " + ex.Message);
                return false;
            }
        }

        private static bool ProgramFilesAltinda(string exe)
        {
            string full;
            try { full = Path.GetFullPath(exe); }
            catch { return false; }

            string[] roots =
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetEnvironmentVariable("ProgramW6432")
            };
            foreach (string root in roots)
            {
                if (string.IsNullOrWhiteSpace(root)) continue;
                try
                {
                    string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                        + Path.DirectorySeparatorChar;
                    if (full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
                }
                catch { }
            }
            return false;
        }

        private static List<OyunKaydi> KayitlariOku(bool tumKullanicilar = true)
        {
            var sonuc = new List<OyunKaydi>();
            Tara(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", sonuc);
            Tara(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall", sonuc);
            if (!tumKullanicilar) return sonuc;
            Tara(Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", sonuc);
            Tara(Registry.CurrentUser, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall", sonuc);

            // Yuklu diger kullanici profillerinin uninstall kayitlarini da tara.
            try
            {
                using (RegistryKey users = Registry.Users)
                {
                    foreach (string sid in users.GetSubKeyNames())
                    {
                        if (!sid.StartsWith("S-1-5-21-", StringComparison.OrdinalIgnoreCase) ||
                            sid.EndsWith("_Classes", StringComparison.OrdinalIgnoreCase)) continue;
                        try
                        {
                            using (RegistryKey profile = users.OpenSubKey(sid))
                            {
                                if (profile == null) continue;
                                Tara(profile, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", sonuc);
                                Tara(profile, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall", sonuc);
                            }
                        }
                        catch (Exception ex) { Logger.WriteFileOnly("oyun_log.txt", "Kullanici kurulum kaydi okunamadi: " + ex.Message); }
                    }
                }
            }
            catch (Exception ex) { Logger.WriteFileOnly("oyun_log.txt", "Yuklu kullanici kayitlari okunamadi: " + ex.Message); }
            return sonuc;
        }

        private static void Tara(RegistryKey kok, string yol, List<OyunKaydi> sonuc)
        {
            if (kok == null) return;
            try
            {
                using (RegistryKey key = kok.OpenSubKey(yol))
                {
                    if (key == null) return;
                    foreach (string altAd in key.GetSubKeyNames())
                    {
                        try
                        {
                            using (RegistryKey sub = key.OpenSubKey(altAd))
                            {
                                if (sub == null) continue;
                                string ad = sub.GetValue("DisplayName") as string;
                                if (!OyunMu(ad)) continue;
                                string sessiz = sub.GetValue("QuietUninstallString") as string;
                                string normal = sub.GetValue("UninstallString") as string;
                                sonuc.Add(new OyunKaydi
                                {
                                    Ad = ad.Trim(),
                                    SessizKomut = sessiz,
                                    NormalKomut = normal,
                                    Komut = !string.IsNullOrWhiteSpace(sessiz) ? sessiz : normal
                                });
                            }
                        }
                        catch (Exception ex) { Logger.WriteFileOnly("oyun_log.txt", "Bir kurulum kaydi okunamadi: " + ex.Message); }
                    }
                }
            }
            catch (Exception ex) { Logger.WriteFileOnly("oyun_log.txt", "Kurulu program kayitlari okunamadi: " + ex.Message); }
        }

        private static bool OyunMu(string ad)
        {
            if (string.IsNullOrWhiteSpace(ad)) return false;
            string s = ad.Trim().ToLowerInvariant();
            foreach (string disi in OyunDisiImzalar)
                if (s.Contains(disi)) return false;
            foreach (string oyun in OyunAnahtarlari)
            {
                if (oyun == "cs:go" || oyun == "pes ")
                {
                    if (s.Contains(oyun)) return true;
                }
                else if (oyun == "gta5" || oyun == "mxbikes" || oyun == "dota2")
                {
                    if (s.Replace(" ", "").Replace("-", "").Contains(oyun)) return true;
                }
                else if (s.Contains(oyun)) return true;
            }
            // Kisa adlar yalnizca tam urun adi ise kabul edilir; "Trust" gibi programlara eslesmesin.
            if (s == "rust" || s.StartsWith("rust ", StringComparison.Ordinal) || s.StartsWith("rust-", StringComparison.Ordinal)) return true;
            return s == "cs2" || s == "counter strike 2" || s == "counter-strike 2";
        }

        private static bool Kaldir(string cmd)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(cmd)) return false;
                if (cmd.IndexOf("msiexec", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    string kod = GuidBul(cmd);
                    if (string.IsNullOrEmpty(kod)) return false;
                    return CalistirBekle("msiexec.exe", "/x " + kod + " /qn /norestart", 180000);
                }
                string exe, args;
                Ayir(cmd, out exe, out args);
                if (string.IsNullOrWhiteSpace(exe)) return false;
                string dosya = Path.GetFileName(exe);
                bool innoKaldirici = dosya.StartsWith("unins", StringComparison.OrdinalIgnoreCase) &&
                    dosya.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
                if (!innoKaldirici || !File.Exists(exe) || !ProgramFilesAltinda(exe))
                {
                    Logger.WriteFileOnly("kurulum.txt", "Secili oyun kaldirmasi guvenli kaldirici kosulunu saglamadigi icin atlandi.");
                    return false;
                }
                args = (args + " /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-").Trim();
                return CalistirBekle(exe, args, 180000);
            }
            catch (Exception ex)
            {
                Logger.WriteFileOnly("kurulum.txt", "Uninstaller calistirilamadi: " + ex.Message);
                return false;
            }
        }

        private static string GuidBul(string cmd)
        {
            int a = cmd.IndexOf('{');
            int b = cmd.IndexOf('}');
            return a >= 0 && b > a ? cmd.Substring(a, b - a + 1) : null;
        }

        private static void Ayir(string cmd, out string exe, out string args)
        {
            exe = null;
            args = "";
            cmd = (cmd ?? "").Trim();
            if (cmd.Length == 0) return;
            if (cmd.StartsWith("\""))
            {
                int son = cmd.IndexOf('\"', 1);
                if (son > 0)
                {
                    exe = cmd.Substring(1, son - 1);
                    args = cmd.Substring(son + 1).Trim();
                }
                return;
            }
            int exeSonu = cmd.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (exeSonu >= 0)
            {
                exe = cmd.Substring(0, exeSonu + 4).Trim().Trim('\"');
                args = cmd.Substring(exeSonu + 4).Trim();
                return;
            }
            int bosluk = cmd.IndexOf(' ');
            if (bosluk < 0) exe = cmd;
            else { exe = cmd.Substring(0, bosluk).Trim('\"'); args = cmd.Substring(bosluk + 1).Trim(); }
        }

        private static bool CalistirBekle(string exe, string args, int timeoutMs)
        {
            try
            {
                var psi = new ProcessStartInfo(exe, args)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using (Process p = Process.Start(psi))
                {
                    if (p == null) return false;
                    if (!p.WaitForExit(timeoutMs)) { try { p.Kill(); } catch { } return false; }
                    return p.ExitCode == 0 || p.ExitCode == 3010;
                }
            }
            catch { return false; }
        }
    }
}
