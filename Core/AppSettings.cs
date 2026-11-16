using System;
using System.Collections.Generic;
using System.IO;

namespace UnsalBakim.Core
{
    /// <summary>Basit anahtar=deger ayar dosyasi (ProgramData\UnsalBakim\ayarlar.cfg).</summary>
    public class AppSettings
    {
        private readonly Dictionary<string, string> _d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static string Path1 =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "UnsalBakim", "ayarlar.cfg");

        // Varsayilanlar
        public bool EkranHepAcik = true;
        // v84 (Ali Bey'in istegi, 6 Kasim 2026): varsayilan ARTIK ACIK (Cop Kutusu kapatilmaz) --
        // onceki varsayilan (true=kapali) ile birlikte, Windows 10/11'de tek-dosya "Onceki surumler"
        // kurtarma ozelligi de olmadigindan, ogrenci bir dosyayi yanlislikla silince HICBIR kurtarma
        // yolu kalmiyordu. Istenirse Yerel Ayarlar sekmesinden yine kapatilabilir.
        public bool CopKutusuKapat = false;
        public bool GorselPerformans = true;
        public bool YuksekPerformansPlan = true;
        public bool OyunEngelle = true;
        // v28: VPN/Hotspot ve YouTube kisitlari da OyunEngelle gibi varsayilan ACIK (onceden false'tu;
        // "/kur" gorevi kuruyordu ama bayrak kapali oldugu icin hicbir sey yapmiyordu -- okulda coğu
        // makinede fiilen devre disi kalmasina sebep oldu). Istenirse Oyun Kontrol sekmesinden kapatilabilir.
        public bool VpnEngelle = true;
        public bool YoutubeKisitli = true;
        // v55: AI web sitesi (ChatGPT, Gemini, Claude.ai vb.) engeli -- BILEREK YoutubeKisitli'den
        // AYRI ve varsayilan KAPALI (izin ver). Ders saatine gore degil, surekli acik/kapali.
        // Bkz. Core/AiSiteEngelle.cs. Haftalik AI arka plan taramasiyla (AiHaftalikAktif) KARISTIRILMAMALI.
        public bool AiSiteEngelle = false;
        // v40: Her kurulum artik bir PROFIL secimiyle yapilir -- "ogrenci" (tum kisitlamalar
        // acik) veya "ogretmen" (kisitlama yok). Profil SADECE kurulum ANINDA (KUR_OGRENCI /
        // KUR_OGRETMEN) OyunEngelle/VpnEngelle/YoutubeKisitli degerlerini bir kez belirler ve
        // kaydeder; bundan sonra otomatik guncellemeler (saatlik Drive kontrolu, /kur-sessiz)
        // bu degerlere BIR DAHA DOKUNMAZ -- sadece o anki kayitli degerleri yeniden uygular
        // (Installer.Kur ve OyunDenetimDongusuCalistir'daki gibi). Ogretmen bir makinede
        // Ayarlar/Oyun Kontrol sekmesinden bunlari istedigi zaman elle degistirebilir, kalici kalir.
        public string MakineTuru = "ogrenci";
        public bool KameraKapali = false;
        public bool AiHaftalikAktif = true;
        // v69 (Ali Bey'in istegi, 5 Kasim 2026): AI haftalik kontrolunun gun/saati onceden
        // koda SABITTI (Pazartesi 09:00), panelde degistirilemiyordu. Agir Bakim'daki AYNI
        // desen (yerel deger + Filo Yonetimi'nden zorlanabilir) buraya da uygulandi.
        public string AiGun = "Pazartesi";
        public string AiSaat = "09:00";
        public bool OtoGuncellemeAktif = true;
        // v100 duzeltme: bu alan artik GERCEKTEN KULLANILMIYOR -- TaskHelper.OyunEngelGoreviKur
        // HER cagri noktasinda dakika parametresini zaten 5'e sabitliyor (v57, 2. revizyon).
        // Deger sadece yaniltici olmasin diye 10 -> 5 yapildi; zamanlamayi degistirmez.
        public int OyunEngelDk = 5;
        public string HafifSaat = "08:00";
        public int BostaDk = 10;
        public string AgirGun = "Cuma";
        public string AgirSaat = "12:00";
        // v57 (ikinci revizyon): Filo Yonetimi panelindeki Hedef secimi -- Ali Bey'in istegiyle,
        // ogretmen panele her girdiginde/her islemde TEKRAR SECMEK ZORUNDA KALMASIN diye, bu
        // BILGISAYARDA (bu panelde) son secilen Hedef KALICI olarak saklanir ve panel her
        // acildiginda OTOMATIK olarak geri yuklenir -- bir kere "LAB1" secilince, o
        // bilgisayardaki TUM filo islemleri (ayar push, sinav modu, kapat) tekrar secime
        // gerek kalmadan LAB1'i hedefler; degistirmek icin yine panelden secilebilir.
        public string FiloHedef = "TUMU";
        // v49: Envanter sistemi icin KALICI makine kimligi. MAC adresi tek basina guvenilir
        // bir anahtar degil (kablolu/kablosuz NIC degisebilir) -- bu yuzden ilk calismada BIR
        // KEZ uretilip ayarlar.cfg'ye yazilir, sonraki her Load()'da aynen okunur, hicbir
        // guncelleme bunu DEGISTIRMEZ (Installer.Kur ayarlari SILMIYOR, sadece Save() cagiriyor).
        // Yalnizca ProgramData klasoru SILINIP (orn. "Kurulumu tamamen kaldir") yeniden kurulursa
        // yeni bir kimlik uretilir -- bu, "yeniden imajlanan makine = yeni kayit" olarak kabul
        // edilebilir bir durumdur; eski kayit Sheet'te MAC/bilgisayar adindan elle eslestirilebilir.
        public string MachineId = "";
        // v109: AppLocker (yalnizca UB_Ogrenci grubuna) -- false = Denetim (AuditOnly, sadece
        // kaydeder), true = Zorunlu (engeller). Varsayilan BILEREK Denetim: ders yazilimlari
        // birkac gun denetlenmeden zorunlu moda gecilmez (panelden Koruma sekmesinde degisir).
        public bool AppLockerZorunlu = false;
        // v110: donanim kimligine gecisten ONCEKI (rastgele GUID) kimlik -- Apps Script eski
        // satiri yeni kimlige tasiyabilsin diye Envanter/MakineLog ile birlikte gonderilir.
        public string EskiMachineId = "";

        public void Load()
        {
            try
            {
                // v49: eskiden burada dosya yoksa direkt "return" vardi -- bu, asagidaki MachineId
                // uretme kodunu da (dosyanin disinda oldugu icin degil, METODUN TAMAMINDAN ciktigi
                // icin) atlardi. Simdi "if" govdesine alindi ki ilk calismada (ayarlar.cfg henuz
                // yokken) da MachineId uretilip kaydedilsin.
                if (File.Exists(Path1))
                {
                foreach (var ln in File.ReadAllLines(Path1))
                {
                    var s = ln.Trim();
                    if (s.Length == 0 || s.StartsWith("#")) continue;
                    int i = s.IndexOf('=');
                    if (i <= 0) continue;
                    _d[s.Substring(0, i).Trim()] = s.Substring(i + 1).Trim();
                }
                EkranHepAcik = GetBool("EkranHepAcik", EkranHepAcik);
                CopKutusuKapat = GetBool("CopKutusuKapat", CopKutusuKapat);
                GorselPerformans = GetBool("GorselPerformans", GorselPerformans);
                YuksekPerformansPlan = GetBool("YuksekPerformansPlan", YuksekPerformansPlan);
                OyunEngelle = GetBool("OyunEngelle", OyunEngelle);
                VpnEngelle = GetBool("VpnEngelle", VpnEngelle);
                YoutubeKisitli = GetBool("YoutubeKisitli", YoutubeKisitli);
                AiSiteEngelle = GetBool("AiSiteEngelle", AiSiteEngelle);
                MakineTuru = Get("MakineTuru", MakineTuru);
                KameraKapali = GetBool("KameraKapali", KameraKapali);
                AiHaftalikAktif = GetBool("AiHaftalikAktif", AiHaftalikAktif);
                AiGun = Get("AiGun", AiGun);
                AiSaat = Get("AiSaat", AiSaat);
                OtoGuncellemeAktif = GetBool("OtoGuncellemeAktif", OtoGuncellemeAktif);
                OyunEngelDk = 5; // v100: gercek periyot hep 5 dk (TaskHelper'da sabit); deger sadece bilgi amacli.
                HafifSaat = Get("HafifSaat", HafifSaat);
                BostaDk = GetInt("BostaDk", BostaDk);
                AgirGun = Get("AgirGun", AgirGun);
                AgirSaat = Get("AgirSaat", AgirSaat);
                FiloHedef = Get("FiloHedef", FiloHedef);
                MachineId = Get("MachineId", MachineId);
                AppLockerZorunlu = GetBool("AppLockerZorunlu", AppLockerZorunlu);
                EskiMachineId = Get("EskiMachineId", EskiMachineId);
                }
            }
            catch { }
            // v49: ilk calisma (veya eski bir ayarlar.cfg'de hic MachineId yoksa) -- bir kez uret
            // ve HEMEN kaydet ki ayni oturumda bile ayni ID kalsin.
            if (string.IsNullOrWhiteSpace(MachineId))
            {
                MachineId = MakineKimlik.DonanimKimligi() ?? Guid.NewGuid().ToString("N");
                Save();
            }
            // v110: eski (rastgele GUID) kimlikten donanim kimligine TEK SEFERLIK gecis. Donanim
            // kimligi uretilemezse (WMI hatasi vb.) HICBIR SEY degismez.
            else if (!MachineId.StartsWith("HW", StringComparison.Ordinal))
            {
                try
                {
                    string hw = MakineKimlik.DonanimKimligi();
                    if (!string.IsNullOrEmpty(hw))
                    {
                        EskiMachineId = MachineId;
                        MachineId = hw;
                        Save();
                    }
                }
                catch { }
            }
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path1));
                var lines = new List<string>
                {
                    "# Unsal Bakim ayar dosyasi",
                    "EkranHepAcik=" + (EkranHepAcik ? "1" : "0"),
                    "CopKutusuKapat=" + (CopKutusuKapat ? "1" : "0"),
                    "GorselPerformans=" + (GorselPerformans ? "1" : "0"),
                    "YuksekPerformansPlan=" + (YuksekPerformansPlan ? "1" : "0"),
                    "OyunEngelle=" + (OyunEngelle ? "1" : "0"),
                    "VpnEngelle=" + (VpnEngelle ? "1" : "0"),
                    "YoutubeKisitli=" + (YoutubeKisitli ? "1" : "0"),
                    "AiSiteEngelle=" + (AiSiteEngelle ? "1" : "0"),
                    "MakineTuru=" + MakineTuru,
                    "KameraKapali=" + (KameraKapali ? "1" : "0"),
                    "AiHaftalikAktif=" + (AiHaftalikAktif ? "1" : "0"),
                    "AiGun=" + AiGun,
                    "AiSaat=" + AiSaat,
                    "OtoGuncellemeAktif=" + (OtoGuncellemeAktif ? "1" : "0"),
                    "OyunEngelDk=5",
                    "HafifSaat=" + HafifSaat,
                    "BostaDk=" + BostaDk,
                    "AgirGun=" + AgirGun,
                    "AgirSaat=" + AgirSaat,
                    "FiloHedef=" + FiloHedef,
                    "MachineId=" + MachineId,
                    "AppLockerZorunlu=" + (AppLockerZorunlu ? "1" : "0"),
                    "EskiMachineId=" + EskiMachineId
                };
                File.WriteAllLines(Path1, lines);
            }
            catch { }
        }

        private string Get(string k, string d) => _d.TryGetValue(k, out var v) ? v : d;
        private bool GetBool(string k, bool d) => _d.TryGetValue(k, out var v) ? (v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase)) : d;
        private int GetInt(string k, int d) => _d.TryGetValue(k, out var v) && int.TryParse(v, out var n) ? n : d;
    }
}
