using System;
using Microsoft.Win32;

namespace UnsalBakim.Core
{
    /// <summary>
    /// v110 (Ali Bey'in sorusu: "porno ve yetiskin kanallarini nasil engelleriz"): OGRENCI
    /// makinelerinde tarayici politikalari --
    ///   * DNS-over-HTTPS KAPALI (Chrome/Edge/Brave/Firefox): tarayici kendi gizli DNS'ine gecip
    ///     okulun/FATIH aginin filtresini atlatamasin.
    ///   * Google SafeSearch ZORUNLU (Chrome/Edge/Brave) + Bing SafeSearch SIKI (Edge): arama
    ///     sonuclarinda yetiskin icerik cikmasin.
    /// YouTube kisitlamasina DOKUNMAZ (o ayri ozellik). OGRETMEN makinesinde, SADECE bizim
    /// koydugumuz degerler (isaretle takip edilir) geri alinir -- baska bir yazilimin/politikanin
    /// ayni degerlerine dokunulmaz.
    /// </summary>
    public static class TarayiciGuvenlik
    {
        private const string Log = "rol_guvenlik.txt";
        private const string Isaret = "UnsalBakimTarayiciGuvenlik";

        private static readonly string[] ChromiumYollari =
        {
            @"SOFTWARE\Policies\Google\Chrome",
            @"SOFTWARE\Policies\Microsoft\Edge",
            @"SOFTWARE\Policies\BraveSoftware\Brave"
        };
        private const string FirefoxDoh = @"SOFTWARE\Policies\Mozilla\Firefox\DNSOverHTTPS";

        public static void Uygula(bool ogrenci)
        {
            if (!RolGuvenlik.YetkiliBaglam()) return;
            if (ogrenci) Ac(); else Kaldir();
        }

        private static void Ac()
        {
            foreach (var yol in ChromiumYollari)
            {
                try
                {
                    using (var k = Registry.LocalMachine.CreateSubKey(yol))
                    {
                        if (k == null) continue;
                        k.SetValue("DnsOverHttpsMode", "off", RegistryValueKind.String);
                        k.SetValue("ForceGoogleSafeSearch", 1, RegistryValueKind.DWord);
                        if (yol.Contains(@"\Edge")) k.SetValue("ForceBingSafeSearch", 2, RegistryValueKind.DWord);
                        k.SetValue(Isaret, 1, RegistryValueKind.DWord);
                    }
                }
                catch (Exception ex) { Logger.WriteFileOnly(Log, "[!] Tarayici politikasi yazilamadi (" + yol + "): " + ex.Message); }
            }
            try
            {
                using (var k = Registry.LocalMachine.CreateSubKey(FirefoxDoh))
                {
                    if (k != null)
                    {
                        k.SetValue("Enabled", 0, RegistryValueKind.DWord);
                        k.SetValue("Locked", 1, RegistryValueKind.DWord);
                        k.SetValue(Isaret, 1, RegistryValueKind.DWord);
                    }
                }
            }
            catch (Exception ex) { Logger.WriteFileOnly(Log, "[!] Firefox DoH politikasi yazilamadi: " + ex.Message); }
        }

        private static void Kaldir()
        {
            foreach (var yol in ChromiumYollari)
            {
                try
                {
                    using (var k = Registry.LocalMachine.OpenSubKey(yol, true))
                    {
                        if (k == null || k.GetValue(Isaret) == null) continue;
                        foreach (var ad in new[] { "DnsOverHttpsMode", "ForceGoogleSafeSearch", "ForceBingSafeSearch", Isaret })
                            try { k.DeleteValue(ad, false); } catch { }
                    }
                }
                catch { }
            }
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(FirefoxDoh, true))
                {
                    if (k != null && k.GetValue(Isaret) != null)
                        foreach (var ad in new[] { "Enabled", "Locked", Isaret })
                            try { k.DeleteValue(ad, false); } catch { }
                }
            }
            catch { }
        }

        public static string Durum()
        {
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Google\Chrome"))
                    return k != null && k.GetValue(Isaret) != null ? "acik" : "kapali";
            }
            catch { return "?"; }
        }
    }
}
