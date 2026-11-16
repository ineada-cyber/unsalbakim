using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace UnsalBakim.Core
{
    /// <summary>
    /// VPN engelleme: bilinen VPN programlarini (a) calistirma engeli (IFEO), (b) calisiyorsa kapatma,
    /// (c) VPN saglayici/indirme sitelerini tarayici politikasiyla engelleme.
    /// "VPN devre disi" tek dugmeyle acilir/kaldirilir. Ogrenci kaldiramaz (HKLM).
    /// </summary>
    public static class VpnKontrol
    {
        private const string IFEO = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options";
        private const string MARKER = "UnsalBakimVpn";
        private const string Log = "vpn.txt";
        private const int Taban = 92001; // oyun(90001)/youtube(91001) disinda

        // Bilinen VPN surec/exe adlari (AI ile buyutulebilir: ek_vpn_exe.txt)
        public static readonly System.Collections.Generic.List<string> Surecler = new System.Collections.Generic.List<string>
        {
            "openvpn","openvpn-gui","openvpnconnect","wireguard","wg","nordvpn","NordVPN",
            "expressvpn","expressvpnd","protonvpn","ProtonVPN","surfshark","Surfshark",
            "cyberghost","HotspotShield","hssmgr","Hotspot Shield (32)","Hotspot Shield (64)",
            "tunnelbear","windscribe","WindscribeService",
            // v99 (Ali Bey'in istegi: BMTAL-LAB2-OGR1'de goruldu): Cloudflare WARP (DNS/VPN
            // benzeri trafik yonlendirme -- bu makinede script.google.com'a aralikli DNS/SSL
            // hatalarina da sebep oluyordu). "IO Auto Clicker" BURADAN KALDIRILDI (v100) --
            // bir hile/oyun araci, VPN degil; GameControl.cs'e (OyunEngelle ile ayni kapiya)
            // tasindi, bkz. GameControl.Surecler. Burada birakmak, okul "VPN engelleme"yi
            // kapatip "Oyun engelleme"yi acik tutarsa Auto Clicker'in HIC yakalanmamasi riskini
            // dogurdugu icin (iki ayar birbirinden tamamen bagimsiz) duzeltildi.
            "Cloudflare WARP","warp-svc",
            "hmavpn","HMA","privateinternetaccess","pia-client","piavpn","mullvad","mullvad-vpn",
            "outline","outline-client","psiphon","psiphon3","hola","holavpn","browsec","zenmate",
            "ultrasurf","softether","vpnclient","vpngate","tor","torbrowser",
            "speedify","betternet","touchvpn","urban-vpn","urbanvpn","1clickvpn","setupvpn","thunder-vpn"
        };

        // VPN saglayici / indirme siteleri (AI ile buyutulebilir: ek_vpn_site.txt)
        public static readonly System.Collections.Generic.List<string> Siteler = new System.Collections.Generic.List<string>
        {
            "nordvpn.com","expressvpn.com","protonvpn.com","surfshark.com","cyberghostvpn.com",
            "hotspotshield.com","tunnelbear.com","windscribe.com","hidemyass.com","privateinternetaccess.com",
            "mullvad.net","getoutline.org","psiphon.ca","psiphon3.com","hola.org","browsec.com",
            "zenmate.com","ultrasurf.us","softether.org","vpngate.net","speedify.com","betternet.co",
            "urban-vpn.com","setupvpn.com","touchvpn.net","1clickvpn.net","torproject.org"
        };

        private static string EkExe => Path.Combine(VeriKlasor(), "ek_vpn_exe.txt");
        private static string EkSite => Path.Combine(VeriKlasor(), "ek_vpn_site.txt");
        private static string VeriKlasor() =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "UnsalBakim");

        static VpnKontrol()
        {
            try { if (File.Exists(EkExe)) foreach (var l in File.ReadAllLines(EkExe)) { var a = l.Trim(); if (a.Length > 0 && !a.StartsWith("#") && !Surecler.Contains(a)) Surecler.Add(a); } } catch { }
            try { if (File.Exists(EkSite)) foreach (var l in File.ReadAllLines(EkSite)) { var a = l.Trim(); if (a.Length > 0 && !a.StartsWith("#") && !Siteler.Contains(a)) Siteler.Add(a); } } catch { }
        }

        /// <summary>AI/elle yeni VPN exe adi ekler (kalici).</summary>
        public static bool EkleExe(string ad)
        {
            ad = (ad ?? "").Replace(".exe", "").Trim();
            if (ad.Length == 0 || Surecler.Contains(ad)) return false;
            Surecler.Add(ad);
            try { Directory.CreateDirectory(VeriKlasor()); File.AppendAllText(EkExe, ad + Environment.NewLine); } catch { }
            return true;
        }

        /// <summary>AI/elle yeni VPN sitesi ekler (kalici).</summary>
        public static bool EkleSite(string d)
        {
            d = (d ?? "").Trim().ToLowerInvariant();
            if (d.Length == 0 || Siteler.Contains(d)) return false;
            Siteler.Add(d);
            try { Directory.CreateDirectory(VeriKlasor()); File.AppendAllText(EkSite, d + Environment.NewLine); } catch { }
            return true;
        }

        /// <summary>VPN'i engelle: IFEO + calisanlari kapat + siteleri engelle.</summary>
        public static void Engelle()
        {
            // 1) Calisan VPN'leri kapat
            KapatCalisanlar();
            // 2) IFEO ile calistirma engeli
            string taskkill = Path.Combine(Environment.SystemDirectory, "taskkill.exe");
            using (var kok = Registry.LocalMachine.CreateSubKey(IFEO))
            {
                foreach (var s in Surecler)
                {
                    string exe = s.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? s : s + ".exe";
                    try
                    {
                        using (var k = kok.CreateSubKey(exe))
                        {
                            k.SetValue("Debugger", "\"" + taskkill + "\" /f /im \"" + exe + "\"", RegistryValueKind.String);
                            k.SetValue(MARKER, 1, RegistryValueKind.DWord);
                        }
                    }
                    catch { }
                }
            }
            // 3) Siteleri engelle
            try { SiteEngelle2(@"SOFTWARE\Policies\Microsoft\Edge", true); } catch { }
            try { SiteEngelle2(@"SOFTWARE\Policies\Google\Chrome", true); } catch { }
            try { Firefox(true); } catch { }
            // 4) Windows'un KENDI Mobil Etkin Nokta (Mobile Hotspot/ICS) ozelligini de kapat
            //    (VPN uygulamalarindan ayri bir yol; "Hotspot" hem 3. parti VPN/Hotspot
            //    uygulamalarini hem de Windows'un kendi tetherlemesini kapsar).
            try { MobilHotspotEngelle(); } catch { }
            Logger.WriteFileOnly(Log, "VPN engeli uygulandi (" + Surecler.Count + " exe, " + Siteler.Count + " site + Windows Mobil Hotspot).");
        }

        /// <summary>
        /// Windows'un KENDI Mobil Etkin Nokta (Mobile Hotspot / Internet Connection Sharing)
        /// ozelligini kapatir. Iki katmanli: (a) resmi GPO degeri NC_ShowSharedAccessUI=0
        /// (paylasim arayuzunu tamamen kaldirir, Ayarlar > Mobil Etkin Nokta sayfasi acilmaz),
        /// (b) destek servisi "icssvc"yi devre disi birakir (ayarlardan acilsa bile calismaz).
        /// 3. parti VPN/Hotspot programlarindan BAGIMSIZDIR; o uygulamalar yukardaki
        /// Surecler/Siteler listesiyle (ornn. "HotspotShield") ayrica engellenir.
        /// </summary>
        public static void MobilHotspotEngelle()
        {
            try
            {
                using (var k = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\Network Connections"))
                {
                    k.SetValue("NC_ShowSharedAccessUI", 0, RegistryValueKind.DWord);
                }
            }
            catch { }
            try { NativeRunner.Run("sc.exe", "config icssvc start= disabled", Log, false); } catch { }
            try { NativeRunner.Run("sc.exe", "stop icssvc", Log, false); } catch { }
        }

        /// <summary>Windows Mobil Etkin Nokta engelini kaldirir (servisi talep-uzerine durumuna getirir).</summary>
        public static void MobilHotspotEngelKaldir()
        {
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\Network Connections", true))
                    k?.DeleteValue("NC_ShowSharedAccessUI", false);
            }
            catch { }
            try { NativeRunner.Run("sc.exe", "config icssvc start= demand", Log, false); } catch { }
        }

        public static void KaldirEngel()
        {
            using (var kok = Registry.LocalMachine.OpenSubKey(IFEO, true))
            {
                if (kok != null)
                    foreach (var alt in kok.GetSubKeyNames())
                    {
                        try
                        {
                            using (var k = kok.OpenSubKey(alt))
                                if (k != null && k.GetValue(MARKER) != null) { k.Close(); kok.DeleteSubKeyTree(alt, false); }
                        }
                        catch { }
                    }
            }
            try { SiteEngelle2(@"SOFTWARE\Policies\Microsoft\Edge", false); } catch { }
            try { SiteEngelle2(@"SOFTWARE\Policies\Google\Chrome", false); } catch { }
            try { Firefox(false); } catch { }
            try { MobilHotspotEngelKaldir(); } catch { }
            Logger.WriteFileOnly(Log, "VPN engeli kaldirildi.");
        }

        // v98 (Ali Bey'in makinesinde "Hotspot Shield (32)" gorulmesi uzerine): bazi programlar
        // (ozellikle Hotspot Shield) surume/bitlige gore ARA BOSLUKLU ve "(32)"/"(64)" ekli bir
        // surec adiyla calisabiliyor -- Surecler listesindeki TAM ad ile eslesmeyebilir. Windows'un
        // kendisi IFEO kaydinda joker karakter ("Hotspot*") desteklemiyor (kayit adi calisan surecin
        // TAM dosya adiyla eslesmek ZORUNDA), bu yuzden tek basina listeye eklemek yetmeyebilir.
        // Bu yuzden burada bir ONEK (prefix) yakalama katmani eklendi: asagidaki OnekYakalama
        // listesindeki herhangi bir onekle BASLAYAN her surec de (Surecler'de tam adi olmasa bile)
        // yakalanir, kapatilir VE kendi GERCEK/TAM calisan dosya adi okunup o surec icin KENDI IFEO
        // kaydi da OTOMATIK olusturulur -- boylece ileride baska bir surum/isim degisikligi
        // (orn. "Hotspot Shield (64)") olsa bile program kendi kendini "ogrenir" ve bir daha
        // acilamaz, elle her varyanti listeye eklemeye gerek kalmaz.
        // v99: ayni onek-yakalama/otomatik-ogrenme mekanizmasi "cloudflare warp" icin de
        // genisletildi -- tam ad/surum varyasyonu gosterebilir. ("auto clicker"/"io auto
        // clicker" v100'de buradan kaldirildi -- bkz. GameControl.cs, ayni mekanizma orada.)
        private static readonly string[] OnekYakalama =
        {
            "hotspot shield", "hotspotshield", "cloudflare warp", "warp-svc"
        };

        private static bool OnekIleBasliyorMu(string ad)
        {
            foreach (var onek in OnekYakalama)
                if (ad.StartsWith(onek, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public static int KapatCalisanlar()
        {
            int n = 0;
            var set = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in Surecler) set.Add(s.Replace(".exe", ""));
            string taskkill = Path.Combine(Environment.SystemDirectory, "taskkill.exe");
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    bool tamEslesme = set.Contains(p.ProcessName);
                    bool onekEslesme = !tamEslesme && OnekIleBasliyorMu(p.ProcessName);
                    if (!tamEslesme && !onekEslesme) continue;

                    string tamAd = null;
                    try { tamAd = Path.GetFileName(p.MainModule.FileName); } catch { }

                    p.Kill();
                    EngelKayit.Ekle("VPN: " + p.ProcessName, "vpn-kapatma");
                    n++;

                    // v98: onek eslesmesiyle (listede TAM adi olmadan) yakalanan bir surec icin,
                    // bir daha calisamamasi icin KENDI tam dosya adiyla IFEO kaydi da olustur.
                    if (onekEslesme && !string.IsNullOrWhiteSpace(tamAd))
                    {
                        try
                        {
                            using (var kok = Registry.LocalMachine.CreateSubKey(IFEO))
                            using (var k = kok.CreateSubKey(tamAd))
                            {
                                k.SetValue("Debugger", "\"" + taskkill + "\" /f /im \"" + tamAd + "\"", RegistryValueKind.String);
                                k.SetValue(MARKER, 1, RegistryValueKind.DWord);
                            }
                            Logger.WriteFileOnly(Log, "Onek eslesmesiyle yakalanan surec icin otomatik IFEO engeli eklendi: " + tamAd);
                        }
                        catch { }
                    }
                }
                catch { }
            }
            return n;
        }

        private static void SiteEngelle2(string polYol, bool engelle)
        {
            if (engelle)
            {
                using (var bl = Registry.LocalMachine.CreateSubKey(polYol + @"\URLBlocklist"))
                {
                    if (bl == null) return;
                    int i = Taban;
                    foreach (var s in Siteler) { bl.SetValue(i.ToString(), s, RegistryValueKind.String); i++; }
                }
            }
            else
            {
                using (var bl = Registry.LocalMachine.OpenSubKey(polYol + @"\URLBlocklist", true))
                {
                    if (bl == null) return;
                    for (int i = Taban; i < Taban + Siteler.Count + 5; i++)
                        try { bl.DeleteValue(i.ToString(), false); } catch { }
                }
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
