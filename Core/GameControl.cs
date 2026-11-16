using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using Microsoft.Win32;

namespace UnsalBakim.Core
{
    /// <summary>Oyun kontrol: calistirma engeli (IFEO), surec kapatma, kaldirma.
    /// Not: Ticari surumde IFEO yerine Guvenlik Duvari/AppLocker da secenek olabilir (yol haritasi).</summary>
    public static class GameControl
    {
        private const string IFEO = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options";
        private const string MARKER = "UnsalBakim";

        // AI'nin ogrendigi ek oyun adlari burada tutulur; her calismada listeye yuklenir.
        private static string EkDosya =>
            Path.Combine(Logger.LogDir.Replace("\\LOG", ""), "ek_oyunlar.txt");

        // Her surec basladiginda (3 dk gorev = yeni surec) ogrenilen ek oyunlari listeye kat.
        static GameControl()
        {
            try
            {
                if (File.Exists(EkDosya))
                    foreach (var ln in File.ReadAllLines(EkDosya))
                    {
                        var a = ln.Trim();
                        if (a.Length > 0 && !a.StartsWith("#") && !Surecler.Contains(a))
                            Surecler.Add(a);
                    }
            }
            catch { }
        }

        /// <summary>AI veya elle bulunan yeni bir oyun adini kalici listeye ekler (bir dahaki denetimde yakalanir).</summary>
        public static bool EkleOyun(string ad)
        {
            ad = (ad ?? "").Replace(".exe", "").Trim();
            if (ad.Length == 0) return false;
            if (Surecler.Contains(ad)) return false;
            Surecler.Add(ad);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(EkDosya));
                File.AppendAllText(EkDosya, ad + Environment.NewLine);
            }
            catch { }
            return true;
        }

        // Engellenecek oyun surecleri (oyun_liste ile ayni; MX Bikes dahil)
        public static List<string> Surecler = new List<string>
        {
            "RobloxPlayerBeta","RobloxPlayerLauncher","Roblox","Minecraft","MinecraftLauncher",
            "VALORANT","VALORANT-Win64-Shipping","RiotClientServices","LeagueClient","LeagueClientUx",
            "FortniteClient-Win64-Shipping","FortniteLauncher","EpicGamesLauncher","steam",
            "csgo","cs2","dota2","GTA5","mxbikes","mxbikes_dedicated","gameloop","BlueStacks","LDPlayer","dnplayer","NoxVMHandle",
            // Minecraft alternatif baslaticilari (Legacy Launcher / TLauncher vb.) ve Bedrock
            "TLauncher","Legacy Launcher","LegacyLauncher","SKlauncher","MultiMC","PrismLauncher",
            "LunarClient","BadlionClient","FeatherLauncher","ATLauncher","GDLauncher","HeliosLauncher",
            "Minecraft.Windows",
            // Yaygin cevrim ici oyunlar (launcher disindaki oyun surecleri)
            "TslGame", "TslGame_BE", "r5apex", "ApexLegends", "RainbowSix", "RainbowSix_BE",
            "GenshinImpact", "Overwatch", "Overwatch.exe", "RocketLeague", "Among Us",
            "FallGuys_client", "Brawlhalla", "BrawlhallaGame", "Crossfire", "CrossFire",
            "DeadByDaylight-Win64-Shipping", "RustClient", "Phasmophobia", "TeamFortress2",
            "hl2",
            // Oyun YAPMA/gelistirme programlari (madde: "yeni bulunan acikilar ve istekler" #4/#8).
            // Ogrenciler ders disi zamanlarda AI (Claude/ChatGPT vb.) ile kod uretip kendi oyununu
            // derleyebiliyor; bu motorlar/editorler o surecin ortak paydasidir. NOT: genel amacli
            // gelistirme araclari (VS Code, Python, gcc/mingw) BILEREK eklenmedi -- okul bilgisayarlarinda
            // bilgisayar dersi icin gerekli olabilir, kapatmak ders calismasini da engeller.
            "Unity", "UnityHub", "Unity Hub", "UnityHelper", "Godot", "GodotEngine",
            "GameMakerStudio", "GameMakerStudio2", "GameMaker", "GameMakerStudio.Windows",
            "Construct3", "Construct 3", "RPG Maker", "RPGMaker", "GDevelop", "RobloxStudioBeta",
            "UE4Editor", "UnrealEditor", "Unreal Engine",
            // v100 (BMTAL-LAB2-OGR1'de goruldu -- v99'da yanlislikla VpnKontrol.cs'e eklenmisti,
            // bir VPN degil, oyun/hile amacli otomatik tiklama araci oldugu icin buraya tasindi):
            // OyunEngelle ile ayni kapidan (VpnEngelle'den BAGIMSIZ) kontrol edilmesi gerekiyordu.
            "IO Auto Clicker", "Auto Clicker to Automate Mouse Clicks",
            // v105 (Ali Bey'in istegi, 11 Kasim 2026): Discord -- oyun DEGIL ama ders disi
            // sohbet/dikkat dagitici olarak ayni OyunEngelle kapisindan kontrol edilsin istendi.
            // Ekran goruntusu beklemeye GEREK YOK -- Discord'un calisan surec adi TUM surumlerde
            // SABIT "Discord" (kurulum yolu/surum numarasi degisse bile degismez, Hotspot Shield
            // gibi bir varyant sorunu yok), bu yuzden dogrudan TAM ad listesine eklendi.
            "Discord"
        };

        // v100: VpnKontrol.cs'teki onek-yakalama/otomatik-ogrenme mekanizmasinin AYNISI buraya
        // da tasindi -- "Auto Clicker" gibi araclar surume/dagitima gore farkli tam ad
        // kullanabilir (orn. "IO Auto Clicker" vs "Auto Clicker to Automate Mouse Clicks" --
        // ikisi de BMTAL-LAB2-OGR1'de ayri ayri goruldu). Listede TAM adi olmasa bile bu
        // oneklerden biriyle BASLAYAN bir surec de yakalanir, kapatilir VE kendi GERCEK dosya
        // adiyla IFEO kaydi otomatik olusturulur (bkz. SurecKapat).
        private static readonly string[] OnekYakalama =
        {
            "auto clicker", "io auto clicker"
        };

        // v102 fix (Ali Bey'in 1 nolu PC'de gonderdigi ekran goruntusu, 11 Kasim 2026):
        // "Massive v0.16.0" adinda baska bir oto-tiklama araci yakalandi. Bu kez SURECIN
        // GERCEK .exe adi DOGRULANAMADI (Ali Bey'in fotosu sadece programin kendi penceresini
        // ve bir hata kutusunu gosteriyordu, Gorev Yoneticisi/Ayrintilar goruntusu degildi) --
        // bu yuzden OnekYakalama (surec ADI) listesine KORKARAK/TAHMINLE eklemek yerine, ekran
        // goruntusunde ACIKCA goruleni, PENCERE BASLIGINI ("Massive v0.16.0") kullaniyoruz. Bu
        // daha GUVENLI: gercek dosya adi PyInstaller gibi bir araci ile tamamen farkli (orn.
        // "main.exe") olsa bile, kullanicinin GORDUGU ve bizim de ekran goruntusunde TEYIT
        // ETTIGIMIZ pencere basligi degismez.
        private static readonly string[] BaslikYakalama =
        {
            "Massive"
        };

        private static bool OnekIleBasliyorMu(string ad)
        {
            foreach (var onek in OnekYakalama)
                if (ad.StartsWith(onek, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static bool BaslikIleEsleserMi(string baslik)
        {
            if (string.IsNullOrWhiteSpace(baslik)) return false;
            foreach (var on in BaslikYakalama)
                if (baslik.StartsWith(on, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>Calisan oyunlari kapatir.</summary>
        public static int SurecKapat()
        {
            const string log = "oyun.txt";
            int sayi = 0;
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in Surecler) set.Add(s.Replace(".exe", ""));
            string taskkill = Path.Combine(Environment.SystemDirectory, "taskkill.exe");
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    bool tamEslesme = set.Contains(p.ProcessName);
                    // v100: onek-yakalama (bkz. OnekYakalama) -- "Auto Clicker" vb. araclarin
                    // listede tam adi olmayan varyantlarini da yakalar.
                    bool onekEslesme = !tamEslesme && OnekIleBasliyorMu(p.ProcessName);
                    // v102 (bkz. BaslikYakalama): "Massive" gibi, surec ADI dogrulanamayan ama
                    // PENCERE BASLIGINDAN kesin tanindigi icin AYNI onek-yakalama mantigini
                    // (yakala + kendi gercek dosya adiyla kalici IFEO) tetikleyen araclar.
                    bool baslikEslesme = false;
                    if (!tamEslesme && !onekEslesme)
                    {
                        string baslik = null;
                        try { baslik = p.MainWindowTitle; } catch { }
                        baslikEslesme = BaslikIleEsleserMi(baslik);
                    }
                    if (!tamEslesme && !onekEslesme && !baslikEslesme) continue;

                    string tamAd = null;
                    try { tamAd = Path.GetFileName(p.MainModule.FileName); } catch { }

                    p.Kill();
                    Logger.WriteFileOnly(log, "[KAPATILDI] " + p.ProcessName + (baslikEslesme ? " (pencere basligindan yakalandi)" : ""));
                    EngelKayit.Ekle(p.ProcessName, "surec-kapatma");
                    sayi++;

                    // v100 (VpnKontrol.cs'teki ayni mekanizmanin tasinmis hali): onek/baslik
                    // eslesmesiyle yakalanan bir surec icin, bir daha calisamamasi icin KENDI tam
                    // dosya adiyla da IFEO kaydi olustur.
                    if ((onekEslesme || baslikEslesme) && !string.IsNullOrWhiteSpace(tamAd))
                    {
                        try
                        {
                            using (var kok = Registry.LocalMachine.CreateSubKey(IFEO))
                            using (var k = kok.CreateSubKey(tamAd))
                            {
                                k.SetValue("Debugger", "\"" + taskkill + "\" /f /im \"" + tamAd + "\"", RegistryValueKind.String);
                                k.SetValue(MARKER, 1, RegistryValueKind.DWord);
                            }
                            Logger.WriteFileOnly(log, "Onek/baslik eslesmesiyle yakalanan surec icin otomatik IFEO engeli eklendi: " + tamAd);
                        }
                        catch { }
                    }
                }
                catch { }
            }
            // Minecraft Java'yi HANGI baslaticidan acilirsa acilsin yakala.
            sayi += MinecraftJavaKapat();
            // Bilinen oyunlarin USB bellekten calisan surumlerini de tespit edip kapat.
            try { sayi += UsbOyunKontrol.OyunSurecleriniKapat(); }
            catch (Exception ex) { Logger.WriteFileOnly(log, "USB oyun taramasi hatasi: " + ex.Message); }
            return sayi;
        }

        /// <summary>
        /// Legacy Launcher / TLauncher vb. Minecraft'i javaw.exe uzerinden baslatir;
        /// surec adi "javaw" oldugu icin normal listeye takilmaz. Burada komut satirinda
        /// "minecraft" gecen java/javaw sureclerini bulup kapatiriz (her baslatici icin gecerli).
        /// </summary>
        public static int MinecraftJavaKapat()
        {
            const string log = "oyun.txt";
            int n = 0;
            try
            {
                using (var arayici = new ManagementObjectSearcher(
                    "SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name='javaw.exe' OR Name='java.exe'"))
                {
                    foreach (ManagementObject mo in arayici.Get())
                    {
                        string cmd = (mo["CommandLine"] as string ?? "").ToLowerInvariant();
                        if (cmd.Contains("minecraft") || cmd.Contains("net.minecraft") || cmd.Contains(".minecraft"))
                        {
                            try
                            {
                                int pid = Convert.ToInt32(mo["ProcessId"]);
                                Process.GetProcessById(pid).Kill();
                                Logger.WriteFileOnly(log, "[KAPATILDI] Minecraft (java) pid=" + pid);
                                EngelKayit.Ekle("Minecraft (java)", "surec-kapatma");
                                n++;
                            }
                            catch { }
                        }
                    }
                }
            }
            catch { }
            return n;
        }

        /// <summary>IFEO ile oyunlarin calismasini engeller (silinse/kurulsa da acilmaz).</summary>
        public static int CalismaEngelle()
        {
            const string log = "oyun.txt";
            int n = 0;
            // Her denetimde (kurulum / 3 dk arka plan gorev / manuel buton) guvenlik duvari + AV
            // kapaliysa geri ac. Boylece ogrenci korumayi kapatsa da kisa surede tekrar acilir.
            try { PerformansAyar.KorumaGuvence(); } catch { }
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
                            n++;
                        }
                    }
                    catch { }
                }
            }
            Logger.WriteFileOnly(log, "Calisma engeli guncel: " + n + " surec (IFEO).");
            // IFEO'yu Norton/Defender bazen "supheli" sayip silebilir. Bu yuzden AV'nin
            // dokunmadigi RESMI bir politika olan DisallowRun ile de oyun exe'lerini engelle.
            try { int d = DisallowRunEngelle(); Logger.WriteFileOnly(log, "DisallowRun oyun engeli: " + d + " exe."); } catch { }
            // Web (tarayici) oyun sitelerini de engelle (YouTube Playables + oyun portallari)
            try { SiteEngelle.Engelle(); } catch { }
            // Tarayici UZANTISI olarak dagitilan oyunlari da engelle (site engelinin kapsamadigi
            // chrome-extension:// ad alani); zaten kurulu olanlar da bu politika ile devre disi kalir.
            try { UzantiEngelle.Engelle(); } catch { }
            return n;
        }

        // Explorer "DisallowRun" politikasi: listelenen exe adlari calistirilamaz.
        // HKLM altinda oldugu icin standart kullanici kaldiramaz; AV bunu silmez.
        private const string PolExplorer = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer";
        private const int DrTaban = 5000; // mevcut DisallowRun girdileriyle cakismamak icin

        private static int DisallowRunEngelle()
        {
            int n = 0;
            using (var pol = Registry.LocalMachine.CreateSubKey(PolExplorer))
            {
                if (pol == null) return 0;
                pol.SetValue("DisallowRun", 1, RegistryValueKind.DWord);
                using (var dr = Registry.LocalMachine.CreateSubKey(PolExplorer + @"\DisallowRun"))
                {
                    if (dr == null) return 0;
                    int i = DrTaban;
                    foreach (var s in Surecler)
                    {
                        string exe = s.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? s : s + ".exe";
                        try { dr.SetValue(i.ToString(), exe, RegistryValueKind.String); i++; n++; } catch { }
                    }
                }
            }
            return n;
        }

        private static void DisallowRunKaldir()
        {
            try
            {
                using (var dr = Registry.LocalMachine.OpenSubKey(PolExplorer + @"\DisallowRun", true))
                {
                    if (dr != null)
                        for (int i = DrTaban; i < DrTaban + Surecler.Count + 25; i++)
                            try { dr.DeleteValue(i.ToString(), false); } catch { }
                }
                // Bizim disimizda DisallowRun girdisi kalmadiysa politikayi tamamen kapat
                using (var dr = Registry.LocalMachine.OpenSubKey(PolExplorer + @"\DisallowRun"))
                {
                    if (dr == null || dr.GetValueNames().Length == 0)
                        using (var pol = Registry.LocalMachine.OpenSubKey(PolExplorer, true))
                            if (pol != null) try { pol.DeleteValue("DisallowRun", false); } catch { }
                }
            }
            catch { }
        }

        /// <summary>IFEO oyun engellerini kaldirir (yalnizca bizim isaretledigimiz).</summary>
        public static int EngelKaldir()
        {
            const string log = "oyun.txt";
            int n = 0;
            // v52 duzeltmesi: IFEO anahtari yoksa ONCEDEN direkt "return 0" ile cikiliyordu --
            // DisallowRunKaldir/SiteEngelle/UzantiEngelle kaldirma cagrilari (asagida) o durumda
            // HIC calismiyordu. IFEO engeli hic kurulmamis (veya zaten elle silinmis) ama site/
            // uzanti/DisallowRun engeli hala duruyorsa, EngelKaldir() "hepsini kaldir" cagrisi
            // sessizce yarim is yapiyordu. Simdi IFEO yoksa da asagidaki uc temizlik calisir.
            using (var kok = Registry.LocalMachine.OpenSubKey(IFEO, true))
            {
                if (kok != null)
                {
                    foreach (var alt in kok.GetSubKeyNames())
                    {
                        try
                        {
                            using (var k = kok.OpenSubKey(alt))
                            {
                                if (k != null && k.GetValue(MARKER) != null)
                                {
                                    k.Close();
                                    kok.DeleteSubKeyTree(alt, false);
                                    n++;
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
            Logger.WriteFileOnly(log, "Oyun calisma engelleri kaldirildi: " + n);
            // DisallowRun oyun engelini de kaldir
            try { DisallowRunKaldir(); } catch { }
            // Web oyun site engelini de kaldir
            try { SiteEngelle.KaldirEngel(); } catch { }
            // Tarayici uzantisi engelini de kaldir
            try { UzantiEngelle.KaldirEngel(); } catch { }
            return n;
        }

        public static int EngelliSayisi()
        {
            int n = 0;
            try
            {
                using (var kok = Registry.LocalMachine.OpenSubKey(IFEO))
                {
                    if (kok == null) return 0;
                    foreach (var alt in kok.GetSubKeyNames())
                        using (var k = kok.OpenSubKey(alt))
                            if (k != null && k.GetValue(MARKER) != null) n++;
                }
            }
            catch { }
            return n;
        }
    }
}
