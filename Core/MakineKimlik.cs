using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Security.Cryptography;
using System.Text;

namespace UnsalBakim.Core
{
    /// <summary>
    /// v110 (Ali Bey'in sorusu: "Envanter'deki ID ne ise yariyor"): ID artik rastgele bir GUID
    /// degil, DONANIMDAN turetilen kisa ve SABIT bir kimliktir ("HW" + 10 hane). Boylece makine
    /// yeniden kurulsa / ProgramData silinse bile Sheet'te AYNI satiri gunceller.
    ///
    /// Kaynak (sirayla): anakart UUID'si + seri no + (varsa) en kucuk PCI ag kartinin MAC'i.
    /// UUID gecersizse (hepsi 0/F, bilinen sahte degerler) sadece seri no + MAC kullanilir; hicbiri
    /// yoksa kimlik DEGISTIRILMEZ (eski GUID aynen kalir -- guvenli varsayilan).
    ///
    /// Gecis: eski GUID, AppSettings.EskiMachineId olarak saklanir ve Envanter/MakineLog
    /// gonderimlerinde 'eskiMachineId' olarak yollanir; Apps Script (v110) eski satiri YENI kimlige
    /// tasir -- Sheet'te cift satir olusmaz.
    /// </summary>
    public static class MakineKimlik
    {
        private static readonly string[] SahteUuid =
        {
            "00000000-0000-0000-0000-000000000000",
            "FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF",
            "03000200-0400-0500-0006-000700080009",
            "00020003-0004-0005-0006-000700080009"
        };

        private static readonly string[] SahteSeri =
        {
            "", "0", "none", "default string", "to be filled by o.e.m.", "system serial number",
            "not applicable", "not specified", "123456789", "0123456789", "invalid"
        };

        private static bool _denendi;
        private static string _onbellek;

        /// <summary>Donanim kimligi ("HW" + 10 hex) ya da uretilemezse null. Surec basina BIR KEZ hesaplanir.</summary>
        public static string DonanimKimligi()
        {
            if (_denendi) return _onbellek;
            _denendi = true;
            try
            {
                string uuid = "", seri = "";
                using (var s = new ManagementObjectSearcher("SELECT UUID, IdentifyingNumber FROM Win32_ComputerSystemProduct"))
                {
                s.Options.Timeout = TimeSpan.FromSeconds(15);
                using (var c = s.Get())
                {
                    foreach (ManagementObject o in c)
                    {
                        uuid = Convert.ToString(o["UUID"] ?? "").Trim().ToUpperInvariant();
                        seri = Convert.ToString(o["IdentifyingNumber"] ?? "").Trim();
                        break;
                    }
                }
                }
                bool uuidGecerli = uuid.Length >= 32 && !SahteUuid.Contains(uuid) &&
                                   uuid.Replace("-", "").Distinct().Count() > 2;
                bool seriGecerli = !SahteSeri.Contains(seri.ToLowerInvariant());
                var macler = FizikselMacler(true);
                string mac = macler.Count > 0 ? macler.OrderBy(x => x, StringComparer.Ordinal).First() : "";

                // En az iki bagimsiz kaynak olmali (klonlanmis BIOS'larda carpismasin).
                int kaynak = (uuidGecerli ? 1 : 0) + (seriGecerli ? 1 : 0) + (mac.Length > 0 ? 1 : 0);
                if (kaynak < 2) return _onbellek = null;

                string anahtar = (uuidGecerli ? uuid : "-") + "|" + (seriGecerli ? seri.ToUpperInvariant() : "-") + "|" + mac;
                using (var sha = SHA256.Create())
                {
                    var h = sha.ComputeHash(Encoding.UTF8.GetBytes(anahtar));
                    var sb = new StringBuilder("HW");
                    for (int i = 0; i < 5; i++) sb.Append(h[i].ToString("X2"));
                    return _onbellek = sb.ToString();
                }
            }
            catch (Exception ex)
            {
                try { Logger.WriteFileOnly("kurulum.txt", "Donanim kimligi alinamadi (eski kimlik korunuyor): " + ex.Message); } catch { }
                return _onbellek = null;
            }
        }

        private static readonly string[] SanalAd =
        {
            "virtual", "virtualbox", "vmware", "hyper-v", "tap-", "tap ", "vpn", "wan miniport",
            "bluetooth", "loopback", "teredo", "isatap", "wireguard", "tunnel", "npcap", "zerotier",
            "hamachi", "fortinet", "cisco anyconnect", "openvpn", "proton", "warp", "radmin"
        };

        /// <summary>Fiziksel (PCI veya USB) ag kartlarinin MAC'leri, "AA:BB:CC:DD:EE:FF" bicimiyle.
        /// sadecePci=true ise sadece kart yuvasina/anakarta takili (PCI\...) kartlar -- USB dongle haric.</summary>
        public static List<string> FizikselMacler(bool sadecePci)
        {
            var liste = new List<string>();
            try
            {
                using (var s = new ManagementObjectSearcher("SELECT MACAddress, PNPDeviceID, Name FROM Win32_NetworkAdapter WHERE PhysicalAdapter = TRUE"))
                {
                s.Options.Timeout = TimeSpan.FromSeconds(15);
                using (var c = s.Get())
                {
                    foreach (ManagementObject o in c)
                    {
                        string mac = Convert.ToString(o["MACAddress"] ?? "").Trim().ToUpperInvariant();
                        string pnp = Convert.ToString(o["PNPDeviceID"] ?? "").Trim().ToUpperInvariant();
                        string ad = Convert.ToString(o["Name"] ?? "").ToLowerInvariant();
                        if (mac.Length != 17) continue;
                        bool pci = pnp.StartsWith(@"PCI\");
                        bool usb = pnp.StartsWith(@"USB\");
                        if (!(pci || (!sadecePci && usb))) continue;
                        if (SanalAd.Any(x => ad.Contains(x))) continue;
                        if (!liste.Contains(mac)) liste.Add(mac);
                    }
                }
                }
            }
            catch { }
            return liste;
        }

        /// <summary>Envanter icin GOSTERILECEK MAC: once fiziksel kartlardan AKTIF (Up) olani, yoksa
        /// herhangi bir fiziksel karti, o da yoksa null (cagiran eski yonteme duser).</summary>
        public static string GosterilecekMac()
        {
            try
            {
                var fiziksel = FizikselMacler(false);
                if (fiziksel.Count == 0) return null;
                foreach (var nic in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                    var b = nic.GetPhysicalAddress().GetAddressBytes();
                    if (b.Length != 6) continue;
                    string m = BitConverter.ToString(b).Replace("-", ":").ToUpperInvariant();
                    if (fiziksel.Contains(m)) return m;
                }
                return fiziksel[0];
            }
            catch { return null; }
        }
    }
}
