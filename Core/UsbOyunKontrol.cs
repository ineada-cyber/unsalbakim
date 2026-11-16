using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;

namespace UnsalBakim.Core
{
    /// <summary>
    /// USB bellekten calistirilmis olabilecek oyunlari ve oyun kurucularini denetler.
    /// Dosyalari silmez; yalnizca cikarilabilir surucuden calisan oyunla iliskili sureci kapatir.
    /// Baska USB uygulamalarini topluca engellemez.
    /// </summary>
    public static class UsbOyunKontrol
    {
        private static readonly string[] OyunImzalari =
        {
            "roblox", "minecraft", "valorant", "league of legends", "leagueclient", "fortnite",
            "counter-strike", "counter strike", "cs2", "csgo", "pubg", "tslgame", "gta5", "gta v",
            "mxbikes", "knight online", "metin2", "wolfteam", "point blank", "zula", "warzone",
            "apex legends", "r5apex", "dota2", "dota 2", "rainbow six", "rocket league", "among us",
            "fall guys", "genshin", "overwatch", "world of warcraft", "brawlhalla", "crossfire",
            "team fortress", "phasmophobia", "dead by daylight", "tlauncher", "sklauncher", "prismlauncher",
            "gameloop", "bluestacks", "ldplayer", "noxplayer", "noxvmhandle", "steam", "epicgameslauncher",
            "riotclient",
            // v89 (Ali Bey'in istegi, 9 Kasim 2026: "oyun azaldi ama hala var, usb ile yukleme
            // olabilir"): USB'den TASINABILIR (portable) calisan emulatorler -- kurulum gerektirmedigi
            // icin USB'den dogrudan calistirmak icin EN KOLAY yontem budur (yaninda ROM dosyalari da
            // USB'de tasinir).
            "mumuplayer", "mumu player", "memuplayer", "memu play", "andyroid", "genymotion",
            "ryujinx", "yuzu", "rpcs3", "pcsx2", "dolphin emu", "ppsspp", "duckstation",
            "cemu", "citra", "retroarch", "epsxe", "project64", "pcsxr", "xenia",
            "moonlight", "parsec", "geforce now", "shadow pc", "steam link", "chiaki",
            // v100: GameControl.Surecler ile senkron -- USB'den tasinabilir "Auto Clicker"
            // varyantlari da bu kapsamda taransin.
            "auto clicker"
        };

        private static readonly string[] KurulumIsaretleri =
        {
            "setup.exe", "install.exe", "installer.exe", "setup.msi", "install.msi"
        };

        public static int OyunSurecleriniKapat()
        {
            int kapatilan = 0;
            var removableRoots = CikarilabilirSurucuKokleriniAl();
            if (removableRoots.Count == 0) return 0;

            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    "SELECT ProcessId, Name, ExecutablePath, CommandLine FROM Win32_Process"))
                using (var results = searcher.Get())
                {
                    foreach (ManagementObject item in results)
                    {
                        try
                        {
                            string name = item["Name"] as string ?? string.Empty;
                            string imagePath = item["ExecutablePath"] as string ?? string.Empty;
                            string commandLine = item["CommandLine"] as string ?? string.Empty;
                            string pidText = Convert.ToString(item["ProcessId"]);
                            int pid;
                            if (!int.TryParse(pidText, out pid) || pid == Process.GetCurrentProcess().Id) continue;

                            string evidence = (name + " " + imagePath + " " + commandLine).ToLowerInvariant();
                            bool fromUsb = SurucuyaAit(imagePath, removableRoots) || KomutSatiriUsbYolu(commandLine, removableRoots);
                            if (!fromUsb) continue;

                            bool game = OyunImzasiVar(evidence);
                            bool gameInstaller = KurulumIsaretleriVar(evidence) && OyunImzasiVar(Path.GetDirectoryName(imagePath) + " " + commandLine);
                            if (!game && !gameInstaller) continue;

                            try
                            {
                                Process.GetProcessById(pid).Kill();
                                Logger.WriteFileOnly("oyun.txt", "USB oyun/kurulum sureci durduruldu: " + name + " (PID " + pid + ")");
                                EngelKayit.Ekle(name, "usb");
                                kapatilan++;
                            }
                            catch (Exception ex)
                            {
                                Logger.WriteFileOnly("oyun.txt", "USB oyun sureci durdurulamadi: " + name + " (PID " + pid + "): " + ex.Message);
                            }
                        }
                        catch { }
                        finally { item.Dispose(); }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.WriteFileOnly("oyun.txt", "USB oyun sureci taranamadi: " + ex.Message);
            }

            if (kapatilan > 0)
                Logger.WriteFileOnly("oyun.txt", "USB denetiminde durdurulan oyun/kurulum sureci: " + kapatilan + ".");
            return kapatilan;
        }

        private static List<string> CikarilabilirSurucuKokleriniAl()
        {
            // Removable (DriveType=2) + USB fiziki diske bagli olup Windows tarafindan
            // Fixed (DriveType=3) raporlanan bazi USB bellekleri de kapsa.
            var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (DriveInfo drive in DriveInfo.GetDrives())
                {
                    try
                    {
                        if (drive.DriveType == DriveType.Removable && drive.IsReady)
                            roots.Add(Path.GetFullPath(drive.RootDirectory.FullName));
                    }
                    catch { }
                }
            }
            catch (Exception ex) { Logger.WriteFileOnly("oyun.txt", "Cikarilabilir suruculer okunamadi: " + ex.Message); }

            try
            {
                using (var disks = new ManagementObjectSearcher(
                    "SELECT DeviceID FROM Win32_LogicalDisk WHERE DriveType=3"))
                using (var found = disks.Get())
                {
                    foreach (ManagementObject logicalDisk in found)
                    {
                        try
                        {
                            string deviceId = logicalDisk["DeviceID"] as string;
                            if (string.IsNullOrWhiteSpace(deviceId) || !UsbFizikselDiskeBagliMi(deviceId)) continue;
                            string root = deviceId.TrimEnd('\\') + "\\";
                            var info = new DriveInfo(root);
                            if (info.IsReady) roots.Add(Path.GetFullPath(root));
                        }
                        catch { }
                        finally { logicalDisk.Dispose(); }
                    }
                }
            }
            catch (Exception ex) { Logger.WriteFileOnly("oyun.txt", "USB sabit disk eslestirmesi okunamadi: " + ex.Message); }

            return new List<string>(roots);
        }

        private static bool UsbFizikselDiskeBagliMi(string logicalDeviceId)
        {
            // Win32_LogicalDisk -> Win32_DiskPartition -> Win32_DiskDrive.
            // Böylece kimi USB belleklerin DriveType=Fixed olarak raporlanması da yakalanır.
            try
            {
                string logical = WmiDegeriniKacir(logicalDeviceId);
                using (var parts = new ManagementObjectSearcher(
                    "ASSOCIATORS OF {Win32_LogicalDisk.DeviceID='" + logical + "'} WHERE AssocClass=Win32_LogicalDiskToPartition"))
                using (var partResults = parts.Get())
                {
                    foreach (ManagementObject part in partResults)
                    {
                        try
                        {
                            string partitionId = part["DeviceID"] as string;
                            if (string.IsNullOrWhiteSpace(partitionId)) continue;
                            using (var drives = new ManagementObjectSearcher(
                                "ASSOCIATORS OF {Win32_DiskPartition.DeviceID='" + WmiDegeriniKacir(partitionId) + "'} WHERE AssocClass=Win32_DiskDriveToDiskPartition"))
                            using (var driveResults = drives.Get())
                            {
                                foreach (ManagementObject physical in driveResults)
                                {
                                    try
                                    {
                                        string iface = physical["InterfaceType"] as string ?? string.Empty;
                                        string pnp = physical["PNPDeviceID"] as string ?? string.Empty;
                                        if (iface.Equals("USB", StringComparison.OrdinalIgnoreCase) ||
                                            pnp.IndexOf("USBSTOR", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                            pnp.StartsWith("USB\\", StringComparison.OrdinalIgnoreCase)) return true;
                                    }
                                    finally { physical.Dispose(); }
                                }
                            }
                        }
                        finally { part.Dispose(); }
                    }
                }
            }
            catch (Exception ex) { Logger.WriteFileOnly("oyun.txt", "USB disk tanimlama hatasi: " + ex.Message); }
            return false;
        }

        private static string WmiDegeriniKacir(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("'", "\\'");
        }

        private static bool SurucuyaAit(string path, List<string> roots)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            string full;
            try { full = Path.GetFullPath(path); } catch { return false; }
            foreach (string root in roots)
                if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static bool KomutSatiriUsbYolu(string commandLine, List<string> roots)
        {
            if (string.IsNullOrWhiteSpace(commandLine)) return false;
            foreach (string root in roots)
                if (commandLine.IndexOf(root, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        private static bool OyunImzasiVar(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            string value = text.ToLowerInvariant();
            foreach (string token in OyunImzalari)
                if (value.Contains(token)) return true;
            return false;
        }

        private static bool KurulumIsaretleriVar(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            string value = text.ToLowerInvariant();
            foreach (string token in KurulumIsaretleri)
                if (value.Contains(token)) return true;
            return false;
        }
    }
}
