using System;
using System.Management;
using Microsoft.Win32;

namespace UnsalBakim.Core
{
    /// <summary>
    /// Kamera (webcam) ac/kapa. Iki katman:
    ///   1) CIHAZ duzeyi: Camera + Image sinifindaki aygitlari Disable-PnpDevice/Enable-PnpDevice ile
    ///      kapatir/acar (donanim gercekten devre disi; standart kullanici geri acamaz - HKLM/SYSTEM).
    ///   2) GIZLILIK politikasi: LetAppsAccessCamera=2 (ForceDeny) + webcam ConsentStore=Deny
    ///      (Store/UWP uygulamalari icin ek guvence).
    /// VarMi(): makinede kamera aygiti var mi (WMI). Menu yalnizca kamera varsa ac/kapa gosterir.
    /// </summary>
    public static class KameraKontrol
    {
        private const string Log = "kamera.txt";
        private const string AppPrivacy = @"SOFTWARE\Policies\Microsoft\Windows\AppPrivacy";
        private const string ConsentWebcam = @"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\webcam";

        /// <summary>Makinede kamera (Camera/Image sinifi) aygiti var mi?</summary>
        public static bool VarMi()
        {
            try
            {
                using (var ara = new ManagementObjectSearcher(
                    "SELECT PNPClass FROM Win32_PnPEntity WHERE PNPClass='Camera' OR PNPClass='Image'"))
                {
                    foreach (ManagementObject mo in ara.Get())
                        return true;
                }
            }
            catch { }
            return false;
        }

        /// <summary>Kamerayi KAPAT: cihazi devre disi birak + gizlilik politikasiyla erisimi reddet.</summary>
        public static void Kapat()
        {
            // 1) Cihaz devre disi (Camera + Image)
            NativeRunner.Run("powershell.exe",
                "-NoProfile -ExecutionPolicy Bypass -Command \"" +
                "Get-PnpDevice -Class Camera,Image -ErrorAction SilentlyContinue | " +
                "Where-Object { $_.Status -eq 'OK' } | " +
                "Disable-PnpDevice -Confirm:$false -ErrorAction SilentlyContinue\"",
                Log, false, 120000);

            // 2) Gizlilik politikasi (Store/UWP + yeni masaustu uygulamalari): kamera erisimini zorla reddet
            try
            {
                using (var k = Registry.LocalMachine.CreateSubKey(AppPrivacy))
                    if (k != null) k.SetValue("LetAppsAccessCamera", 2, RegistryValueKind.DWord); // 2 = ForceDeny
            }
            catch { }
            try
            {
                using (var k = Registry.LocalMachine.CreateSubKey(ConsentWebcam))
                    if (k != null) k.SetValue("Value", "Deny", RegistryValueKind.String);
            }
            catch { }

            Logger.Write(Log, "Kamera KAPATILDI (cihaz devre disi + gizlilik politikasi reddet).");
        }

        /// <summary>Kamerayi AC: cihazi etkinlestir + gizlilik politikasini kaldir.</summary>
        public static void Ac()
        {
            // 1) Cihazi etkinlestir (Camera + Image)
            NativeRunner.Run("powershell.exe",
                "-NoProfile -ExecutionPolicy Bypass -Command \"" +
                "Get-PnpDevice -Class Camera,Image -ErrorAction SilentlyContinue | " +
                "Enable-PnpDevice -Confirm:$false -ErrorAction SilentlyContinue\"",
                Log, false, 120000);

            // 2) Gizlilik politikasini serbest birak
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(AppPrivacy, true))
                    if (k != null) { try { k.DeleteValue("LetAppsAccessCamera", false); } catch { } }
            }
            catch { }
            try
            {
                using (var k = Registry.LocalMachine.CreateSubKey(ConsentWebcam))
                    if (k != null) k.SetValue("Value", "Allow", RegistryValueKind.String);
            }
            catch { }

            Logger.Write(Log, "Kamera ACILDI (cihaz etkin + gizlilik politikasi serbest).");
        }

        /// <summary>Kisa durum metni: kac kamera aygiti var ve kaci etkin.</summary>
        public static string Durum()
        {
            int toplam = 0, etkin = 0;
            try
            {
                using (var ara = new ManagementObjectSearcher(
                    "SELECT Name, Status, ConfigManagerErrorCode, PNPClass FROM Win32_PnPEntity WHERE PNPClass='Camera' OR PNPClass='Image'"))
                {
                    foreach (ManagementObject mo in ara.Get())
                    {
                        toplam++;
                        // ConfigManagerErrorCode 0 = calisiyor; 22 = devre disi
                        int kod = -1;
                        try { kod = Convert.ToInt32(mo["ConfigManagerErrorCode"]); } catch { }
                        if (kod == 0) etkin++;
                    }
                }
            }
            catch { }
            if (toplam == 0) return "Bu bilgisayarda kamera aygiti bulunamadi.";
            return "Kamera aygiti: " + toplam + " adet; etkin: " + etkin + ", kapali: " + (toplam - etkin) + ".";
        }
    }
}
