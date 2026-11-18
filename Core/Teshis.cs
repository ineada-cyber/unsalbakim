using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace UnsalBakim.Core
{
    /// <summary>
    /// v110 (Ali Bey'in istegi: "bu tur cmd isteklerimizi filodan gomulu yollasak, sheete yazsa"):
    /// Filo'dan TESHIS -- hedefteki makine(ler) SABIT bir listeden secilen SALT-OKUNUR sorgulari
    /// calistirir ve sonucu Sheet'teki "Teshis" sayfasina (makine basina tek satir) yazar.
    ///
    /// GUVENLIK: Sheet'ten gelen metin ASLA komut olarak calistirilmaz. Gelen sadece sorgu
    /// ANAHTARLARIDIR (orn. "wifi_ag,disk"); her anahtar asagidaki Sorgular sozlugunde aranir,
    /// listede olmayan anahtar sessizce atlanir. Hicbir sorgu sistemde degisiklik YAPMAZ.
    /// Boylece API anahtari sizsa bile bu kanaldan makinelerde keyfi kod calistirilamaz.
    ///
    /// Komut deseni Kapat/EnvanterIste ile AYNI: KomutZamani yerel olarak son uygulanandan
    /// farkliysa BIR KEZ calisir. Ogretmen makineleri de uyar (bilgi toplama, kisitlama degil).
    /// </summary>
    public static class Teshis
    {
        private const string Log = "teshis.txt";
        private const int SorguZamanAsimi = 45000;
        private const int BolumAzami = 9000;
        private const int ToplamAzami = 40000;

        private static string OnbellekYolu =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "UnsalBakim", "teshis_son_uygulanan.txt");

        /// <summary>Anahtar -> panelde gorunen aciklama. SIRA panelde ve ciktida aynen kullanilir.</summary>
        public static readonly KeyValuePair<string, string>[] Liste =
        {
            new KeyValuePair<string, string>("wifi_ag", "Wi-Fi: bagli ag + cevredeki aglar (BSSID)"),
            new KeyValuePair<string, string>("wifi_olay", "Wi-Fi: son 7 gunun baglanti kayitlari"),
            new KeyValuePair<string, string>("wifi_profil", "Wi-Fi: kayitli ag profilleri"),
            new KeyValuePair<string, string>("wifi_surucu", "Wi-Fi: surucu/kart bilgisi"),
            new KeyValuePair<string, string>("ag", "Ag: ipconfig /all (IP, DNS, ag gecidi)"),
            new KeyValuePair<string, string>("proxy", "Ag: proxy ayarlari (sistem + kullanicilar)"),
            new KeyValuePair<string, string>("aygit", "Aygitlar: hatali/surucusuz aygitlar"),
            new KeyValuePair<string, string>("disk", "Disk: bos alan + bolumler"),
            new KeyValuePair<string, string>("sistem", "Sistem: Windows, acilis zamani, oturum acanlar"),
            new KeyValuePair<string, string>("gorevler", "Unsal Bakim gorevleri (son calisma/sonuc)")
        };

        public static bool GecerliAnahtar(string k) => Liste.Any(x => x.Key == k);

        // ---------------------------------------------------------------- panel tarafi

        private sealed class ZamanAsimliWebClient : WebClient
        {
            private readonly int _ms;
            public ZamanAsimliWebClient(int ms) { _ms = ms; }
            protected override WebRequest GetWebRequest(Uri address)
            {
                var req = base.GetWebRequest(address);
                if (req != null) req.Timeout = _ms;
                return req;
            }
        }

        /// <summary>PANELDEN: hedefteki makine(ler)e teshis komutu yollar.</summary>
        public static string FiloBaslat(string hedef, IEnumerable<string> sorgular)
        {
            if (string.IsNullOrWhiteSpace(Musteri.WebAppUrl))
                return "Drive log ozelligi kurulmadi (WebAppUrl bos).";
            var gecerli = (sorgular ?? Enumerable.Empty<string>()).Where(GecerliAnahtar).Distinct().ToList();
            if (gecerli.Count == 0) return "En az bir teshis sorgusu secin.";
            try
            {
                string zaman = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                string h = string.IsNullOrWhiteSpace(hedef) ? "TUMU" : hedef.Trim();
                string json = "{\"tip\":\"teshisiste\",\"token\":\"" + MakineLog.J(Musteri.ApiToken) +
                              "\",\"musteriId\":\"" + MakineLog.J(Musteri.MusteriId) +
                              "\",\"hedef\":\"" + MakineLog.J(h) +
                              "\",\"zaman\":\"" + zaman +
                              "\",\"sorgular\":\"" + string.Join(",", gecerli) +
                              "\",\"guncelleyen\":\"" + MakineLog.J(Environment.UserName + "@" + Environment.MachineName) + "\"}";
                try { ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; } catch { }
                string yanit;
                using (var wc = new ZamanAsimliWebClient(15000))
                {
                    wc.Encoding = Encoding.UTF8;
                    wc.Headers["Content-Type"] = "application/json; charset=utf-8";
                    yanit = wc.UploadString(Musteri.WebAppUrl, "POST", json);
                }
                if (yanit != null && yanit.Trim() == "OK")
                    return "Teshis istegi gonderildi (hedef: " + h + "; sorgular: " + string.Join(", ", gecerli) + ").\r\n" +
                           "Hedefteki makine(ler) acik ve internete bagliysa en gec ~5-10 dakika icinde sonucu\r\n" +
                           "Sheet'teki 'Teshis' sayfasina yazar. Kapali makineler acildiklarinda calistirir.";
                return "Yazilamadi, Apps Script yaniti: '" + yanit + "' (Apps Script v110 dagitildi mi?).";
            }
            catch (Exception ex) { return "Yazilamadi: " + ex.Message; }
        }

        /// <summary>v114 (Ali Bey'in istegi, 17 Kasim 2026): her sabah acilista (Program.cs /hafif
        /// icinden, HafifBakim ile AYNI gunde-1-kez damgali blok), panelden komut beklemeden, bu
        /// makinenin AppSettings.TeshisSabahAnahtarlari listesini KENDILIGINDEN calistirip sonucu
        /// gonderir. BekleyenYaz/BekleyenGonderDene ile AYNI "kaybolmaz" deseni kullanilir -- ag/Drive
        /// sorununda sonuc diskte kalir, bir sonraki /hafif veya 5 dk'lik oyun dongusunde tekrar
        /// denenir. Bos/gecersiz liste = sessizce atlanir (hicbir sey gonderilmez).</summary>
        public static void SabahOtomatikCalistirVeGonder(AppSettings ayar)
        {
            try
            {
                var anahtarlar = (ayar?.TeshisSabahAnahtarlari ?? "").Split(',')
                    .Select(x => x.Trim().ToLowerInvariant()).Where(GecerliAnahtar).Distinct().ToList();
                if (anahtarlar.Count == 0) return;
                Logger.WriteFileOnly(Log, "Sabah otomatik teshis baslatildi: " + string.Join(",", anahtarlar));
                string sonuc = Calistir(anahtarlar);
                BekleyenYaz(string.Join(",", anahtarlar), sonuc);
                BekleyenGonderDene(ayar);
            }
            catch (Exception ex) { Logger.WriteFileOnly(Log, "Sabah otomatik teshis hatasi: " + ex.Message); }
        }

        // ---------------------------------------------------------------- makine tarafi

        /// <summary>5 dk'lik dongude, FiloDurum.Al'dan gelen degerle cagrilir.</summary>
        internal static void FiloKontrolEtVeUygulaDeger(string zaman, string sorgularHam, AppSettings ayar)
        {
            try
            {
                // v112 (GPT incelemesi): ONCE, daha once calistirilip da GONDERILEMEMIS (bekleyen)
                // sonucu tekrar gondermeyi dene. Boylece gecici bir ag/proxy hatasinda teshis raporu
                // KAYBOLMAZ -- her dongude yeniden gonderilir, sonuc sunucuya ulasana kadar.
                BekleyenGonderDene(ayar);

                if (string.IsNullOrWhiteSpace(zaman)) return;
                if (!DateTime.TryParseExact(zaman.Trim(), "yyyy-MM-dd HH:mm:ss.fff",
                        System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                {
                    Logger.WriteFileOnly(Log, "Gecersiz teshis komut zamani goz ardi edildi: '" + zaman.Trim() + "'");
                    return;
                }
                if (string.Equals(zaman.Trim(), OnbellekOku(), StringComparison.Ordinal)) return;

                var anahtarlar = (sorgularHam ?? "").Split(',').Select(x => x.Trim().ToLowerInvariant())
                                                    .Where(GecerliAnahtar).Distinct().ToList();
                if (anahtarlar.Count == 0)
                {
                    Logger.WriteFileOnly(Log, "Teshis komutunda gecerli sorgu yok: '" + sorgularHam + "'");
                    OnbellekYaz(zaman.Trim()); // gecersiz komut tekrar denenmesin
                    return;
                }
                Logger.WriteFileOnly(Log, "Teshis komutu alindi (" + zaman.Trim() + "): " + string.Join(",", anahtarlar));
                string sonuc = Calistir(anahtarlar);

                // v112: ONCE sonucu "bekleyen" dosyasina yaz, SONRA komutu tuketilmis isaretle.
                // Boylece sorgular TEKRAR calistirilmaz (onbellek) ama sonuc diskte durur ve gonderim
                // basarisiz olsa bile sonraki dongulerde (BekleyenGonderDene) yeniden denenir.
                BekleyenYaz(string.Join(",", anahtarlar), sonuc);
                OnbellekYaz(zaman.Trim());
                BekleyenGonderDene(ayar);
            }
            catch (Exception ex) { Logger.WriteFileOnly(Log, "Teshis hatasi: " + ex.Message); }
        }

        /// <summary>Bekleyen (gonderilememis) teshis sonucu varsa gondermeyi dener; basarili olursa siler.</summary>
        private static void BekleyenGonderDene(AppSettings ayar)
        {
            try
            {
                if (!File.Exists(BekleyenYolu)) return;
                string ham = File.ReadAllText(BekleyenYolu);
                int nl = ham.IndexOf('\n');
                if (nl < 0) { File.Delete(BekleyenYolu); return; }
                string sorgular = ham.Substring(0, nl).Trim();
                string sonuc = ham.Substring(nl + 1);
                string yanit = Gonder(ayar, sorgular, sonuc);
                if (yanit == "OK")
                {
                    try { File.Delete(BekleyenYolu); } catch { }
                    Logger.WriteFileOnly(Log, "Teshis gonderildi (bekleyen temizlendi).");
                }
                else
                {
                    Logger.WriteFileOnly(Log, "Teshis gonderilemedi, bekleyen saklandi, tekrar denenecek: " + yanit);
                }
            }
            catch (Exception ex) { Logger.WriteFileOnly(Log, "Teshis bekleyen gonderim hatasi: " + ex.Message); }
        }

        public static string Calistir(IList<string> anahtarlar)
        {
            var sb = new StringBuilder();
            sb.AppendLine(Environment.MachineName + " | " + License.Surum + " | " + DateTime.Now.ToString("dd.MM.yyyy HH:mm"));
            var basla = DateTime.Now;
            foreach (var k in Liste.Select(x => x.Key).Where(anahtarlar.Contains))
            {
                sb.AppendLine("=== " + k + " ===");
                if ((DateTime.Now - basla).TotalMinutes > 4) { sb.AppendLine("(zaman siniri: atlandi)"); continue; }
                string c;
                try { c = Sorgu(k); } catch (Exception ex) { c = "[HATA] " + ex.Message; }
                c = (c ?? "").Replace("\r", "");
                // Bos satirlari sikistir.
                c = string.Join("\n", c.Split('\n').Select(x => x.TrimEnd()).Where((x, i) => x.Length > 0));
                if (c.Length > BolumAzami) c = c.Substring(0, BolumAzami) + "\n...(kesildi)";
                sb.AppendLine(c.Length == 0 ? "(cikti yok)" : c);
                if (sb.Length > ToplamAzami) break;
            }
            string s = MakineLog.Maskele(sb.ToString());
            return s.Length > ToplamAzami ? s.Substring(0, ToplamAzami) + "\n...(kesildi)" : s;
        }

        /// <summary>SADECE sabit, salt-okunur komutlar. Disaridan gelen hicbir metin buraya ulasmaz.</summary>
        private static string Sorgu(string k)
        {
            switch (k)
            {
                case "wifi_ag":
                    return Yerel("netsh wlan show interfaces") + "\n--- cevredeki aglar ---\n" + Yerel("netsh wlan show networks mode=bssid");
                case "wifi_profil":
                    return Yerel("netsh wlan show profiles");
                case "wifi_surucu":
                    return Yerel("netsh wlan show drivers");
                case "wifi_olay":
                    return WifiBaglantiOzeti(7, 60);
                case "ag":
                    // v113n: ipconfig'in Turkce ay/gun adlarinda (Kasim, Carsamba vb.) kod sayfasi
                    // uyumsuzlugundan "?" cikmasini onlemek icin artik OEM kod sayfasiyla okunuyor
                    // (bkz. NativeRunner.RunCaptureOem yorumu).
                    { int kod; return NativeRunner.RunCaptureOem(SistemExe("ipconfig.exe"), "/all", SorguZamanAsimi, out kod); }
                case "proxy":
                    return Yerel("netsh winhttp show proxy") + "\n--- kullanici proxy ayarlari ---\n" + PS(
                        "Get-ChildItem Registry::HKEY_USERS -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -match '^S-1-5-21-[\\d-]+$' } | ForEach-Object { " +
                        " $k = Join-Path $_.PSPath 'Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings'; " +
                        " $v = Get-ItemProperty -Path $k -ErrorAction SilentlyContinue; if ($v) { " +
                        "  $ad = $_.PSChildName; try { $ad = (New-Object Security.Principal.SecurityIdentifier($_.PSChildName)).Translate([Security.Principal.NTAccount]).Value } catch { } " +
                        "  $ad + ' | ProxyEnable=' + $v.ProxyEnable + ' | ProxyServer=' + $v.ProxyServer + ' | AutoConfigURL=' + $v.AutoConfigURL } }");
                case "aygit":
                    return PS("Get-PnpDevice -ErrorAction SilentlyContinue | Where-Object { $_.Status -ne 'OK' -and $_.Present } | " +
                              "ForEach-Object { $_.Status + ' | ' + $_.Class + ' | ' + $_.FriendlyName + ' | ' + $_.InstanceId }");
                case "disk":
                    string ozet = "";
                    try { ozet = "Ozet: " + AkilliBakim.DiskOzet() + "\n"; } catch { }
                    return ozet + PS("Get-Volume -ErrorAction SilentlyContinue | Where-Object { $_.DriveLetter } | ForEach-Object { " +
                                     "$_.DriveLetter + ': ' + $_.FileSystemLabel + ' | ' + $_.FileSystem + ' | ' + $_.HealthStatus + ' | bos ' + [math]::Round($_.SizeRemaining/1GB,1) + ' / ' + [math]::Round($_.Size/1GB,1) + ' GB' }");
                case "sistem":
                    return PS("$o = Get-CimInstance Win32_OperatingSystem; $c = Get-CimInstance Win32_ComputerSystem; " +
                              "'Windows: ' + $o.Caption + ' ' + $o.Version + ' (' + $o.OSArchitecture + ')'; " +
                              "'Son acilis: ' + $o.LastBootUpTime.ToString('dd.MM.yyyy HH:mm') + ' | acik kalma: ' + [math]::Round(((Get-Date) - $o.LastBootUpTime).TotalHours,1) + ' saat'; " +
                              "'Marka/Model: ' + $c.Manufacturer + ' / ' + $c.Model + ' | RAM ' + [math]::Round($c.TotalPhysicalMemory/1GB,1) + ' GB'; " +
                              "'Oturum: ' + $c.UserName") + "\n--- oturumlar ---\n" + Yerel("query user");
                case "gorevler":
                {
                    // v116 (Ali Bey'in bulgusu: Teshis AI yorumu 0x41303 sonuc kodunu "hatali/11
                    // gundur calismiyor" diye yorumluyordu -- oysa 0x41303, Windows Gorev
                    // Zamanlayicisi'nin standart SCHED_S_TASK_HAS_NOT_RUN sentinel kodu, yani
                    // SADECE "bu gorev kaydedildiginden beri HENUZ tetiklenme firsati olmadi"
                    // anlamina geliyor -- bir HATA DEGIL. KOK NEDEN: GuncellemeKontrol.cs'teki
                    // saatlik sessiz guncelleme HER yeni surumde Installer.Kur() -> TaskHelper.
                    // KurulumYap() ile TUM gorevleri YENIDEN KAYDEDIYOR (bkz. o dosyadaki yorum);
                    // Windows, bir gorev yeniden kaydedildiginde onun "son calisma" GECMISINI
                    // SIFIRLAR.
                    //
                    // v116 EK (Ali Bey'in istegi: "hafif ve agir bakim elde de calissa otomatikte
                    // de calissa AI gormeli"): Hafif_Bakim/Agir_Bakim icin Gorev Zamanlayicisi'nin
                    // KENDI gecmisi yerine Maintenance.cs'nin yazdigi BAGIMSIZ "gercek son calisma"
                    // damgasi kullanilir (bkz. Maintenance.GercekSonCalismaYaz) -- bu damga, bakim
                    // ISTER o Gorev Zamanlayicisi tetikleyicisinden (acilista/haftalik), ISTER
                    // Filo'dan (FiloBakim.cs), ISTER panelden elle (Hizli/Detayli bakim butonu)
                    // baslatilsin HER DURUMDA ayni yere yazilir -- boylece AI yorumu, bakimin
                    // HANGI yoldan tetiklendiginden BAGIMSIZ olarak gercekten calisip calismadigini
                    // gorur. Diger 3 gorev (AI_Haftalik, Duvar_Kagidi, Guncelleme_Kontrol) icin
                    // boyle bagimsiz bir izleme yok, onlar icin eski (Gorev Zamanlayicisi tabanli)
                    // yontem aynen kullanilmaya devam eder.
                    string gercekHafif = GercekSonCalismaOku("hafif_bakim");
                    string gercekAgir = GercekSonCalismaOku("agir_bakim");
                    return PS(
                        "$gercek = @{ 'Hafif_Bakim' = " + PsTekTirnak(gercekHafif) + "; 'Agir_Bakim' = " + PsTekTirnak(gercekAgir) + " }; " +
                        "Get-ScheduledTask -TaskPath '\\UnsalBakim\\' -ErrorAction SilentlyContinue | ForEach-Object { " +
                        "$i = $_ | Get-ScheduledTaskInfo -ErrorAction SilentlyContinue; " +
                        "if ($gercek.ContainsKey($_.TaskName) -and $gercek[$_.TaskName]) { " +
                        "$_.TaskName + ' | ' + $_.State + ' | gercek son calisma (tetikleyiciden BAGIMSIZ, elle/panel/Filo/zamanlayici hepsi dahil): ' + $gercek[$_.TaskName] " +
                        "} else { " +
                        "$kod = [int64]$i.LastTaskResult; " +
                        "$sonuc = if ($kod -eq 0x41303) { 'HENUZ TETIKLENMEDI (normal -- son kurulum/guncellemeden beri gorev gecmisi sifirlanir, HATA DEGIL)' } elseif ($kod -eq 0) { 'basarili' } else { '0x' + ('{0:X}' -f $kod) }; " +
                        "$sonTarih = if ($kod -eq 0x41303) { '-' } elseif ($i.LastRunTime) { $i.LastRunTime.ToString('dd.MM HH:mm') } else { '-' }; " +
                        "$_.TaskName + ' | ' + $_.State + ' | son: ' + $sonTarih + ' | sonuc: ' + $sonuc " +
                        "} }");
                }
            }
            return "(bilinmeyen sorgu)";
        }

        /// <summary>v116: Maintenance.GercekSonCalismaYaz'in yazdigi, tetikleyiciden BAGIMSIZ
        /// "gercek son calisma" damgasini okur (yoksa null -- o bakim hic calismamis demektir).</summary>
        private static string GercekSonCalismaOku(string ad)
        {
            try
            {
                string p = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "UnsalBakim", "son_" + ad + ".txt");
                return File.Exists(p) ? File.ReadAllText(p).Trim() : null;
            }
            catch { return null; }
        }

        /// <summary>Bir metni PowerShell TEK TIRNAKLI degismez (literal) dizgeye cevirir -- null
        /// ise PowerShell $null'a. Tek tirnaklar PowerShell kuraliyla ikiye katlanarak kacirilir.</summary>
        private static string PsTekTirnak(string s) => s == null ? "$null" : "'" + s.Replace("'", "''") + "'";

        /// <summary>Sabit bir komut satirini cmd uzerinden UTF-8 kod sayfasiyla calistirir.</summary>
        private static string Yerel(string sabitKomut)
        {
            int kod;
            string cmd = SistemExe("cmd.exe");
            return NativeRunner.RunCapture(cmd, "/d /c chcp 65001 >nul & " + sabitKomut, SorguZamanAsimi, out kod);
        }

        private static string PS(string betik)
        {
            int kod;
            return NativeRunner.PowerShell(betik, SorguZamanAsimi, out kod);
        }

        private static string SistemExe(string ad)
        {
            string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess)
            {
                string sn = Path.Combine(win, "Sysnative", ad);
                if (File.Exists(sn)) return sn;
            }
            return Path.Combine(win, "System32", ad);
        }

        /// <summary>Okulda izin verilen kablosuz aglar (buyuk/kucuk harf duyarsiz).</summary>
        public static readonly string[] IzinliSsid = { "fatih", "f@tih" };

        /// <summary>WLAN-AutoConfig olay gunlugunden son N gunun baglanti ozetini doner: her SSID icin
        /// baglanti sayisi + son zaman; FATIH disi aglar "[!]" ile isaretlenir. Ayrica son basarisiz
        /// baglanti denemeleri. MakineLog ve Teshis tarafindan kullanilir.</summary>
        public static string WifiBaglantiOzeti(int gun, int azamiOlay)
        {
            string betik =
                "$bas=(Get-Date).AddDays(-" + gun + "); " +
                "$ev = Get-WinEvent -FilterHashtable @{LogName='Microsoft-Windows-WLAN-AutoConfig/Operational'; Id=8001,8002; StartTime=$bas} -MaxEvents " + Math.Max(10, Math.Min(azamiOlay * 5, 500)) + " -ErrorAction SilentlyContinue; " +
                "foreach ($e in $ev) { $x=[xml]$e.ToXml(); $d=@{}; foreach ($n in $x.Event.EventData.Data) { $d[$n.Name]=$n.'#text' }; " +
                " $ssid=$d['SSID']; if (-not $ssid) { $ssid=$d['ProfileName'] }; " +
                " 'OLAY|' + $e.Id + '|' + $e.TimeCreated.ToString('dd.MM HH:mm') + '|' + $ssid + '|' + $d['FailureReason'] }";
            int kod;
            string ham = NativeRunner.PowerShell(betik, SorguZamanAsimi, out kod);
            var bag = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var son = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var hatalar = new List<string>();
            foreach (var satir in ham.Replace("\r", "").Split('\n'))
            {
                if (!satir.StartsWith("OLAY|")) continue;
                var p = satir.Split(new[] { '|' }, 5);
                if (p.Length < 4) continue;
                string ssid = (p[3] ?? "").Trim();
                if (ssid.Length == 0) ssid = "(adsiz)";
                if (p[1] == "8001")
                {
                    if (!bag.ContainsKey(ssid)) { bag[ssid] = 0; son[ssid] = p[2]; } // olaylar yeniden eskiye gelir
                    bag[ssid]++;
                }
                else if (hatalar.Count < 10)
                    hatalar.Add(p[2] + " BASARISIZ: " + ssid + (p.Length > 4 && p[4].Trim().Length > 0 ? " (" + p[4].Trim() + ")" : ""));
            }
            var sb = new StringBuilder();
            if (bag.Count == 0 && hatalar.Count == 0)
                return "Son " + gun + " gunde Wi-Fi baglanti kaydi yok (kablolu veya Wi-Fi kullanilmamis).";
            foreach (var kv in bag.OrderByDescending(x => x.Value))
            {
                bool izinli = IzinliSsid.Any(i => string.Equals(i, kv.Key, StringComparison.OrdinalIgnoreCase));
                sb.AppendLine((izinli ? "" : "[!] FATIH DISI AG: ") + kv.Key + " -- " + kv.Value + " baglanti, son " + son[kv.Key]);
            }
            foreach (var h in hatalar) sb.AppendLine(h);
            return sb.ToString().TrimEnd();
        }

        private static string Gonder(AppSettings ayar, string sorgular, string sonuc)
        {
            if (string.IsNullOrWhiteSpace(Musteri.WebAppUrl)) return "WebAppUrl yok";
            try
            {
                // Ayni anda tum filodan istek yagmasin (Apps Script es zamanli calistirma siniri).
                int h = 0; foreach (char c in (ayar?.MachineId ?? Environment.MachineName)) h = (h * 31 + c) & 0x7fffffff;
                System.Threading.Thread.Sleep((h % 60) * 1000);
                string json = "{\"tip\":\"teshis\",\"token\":\"" + MakineLog.J(Musteri.ApiToken) + "\"," +
                    "\"musteriId\":\"" + MakineLog.J(Musteri.MusteriId) + "\"," +
                    "\"machineId\":\"" + MakineLog.J(ayar?.MachineId ?? "") + "\"," +
                    "\"bilgisayar\":\"" + MakineLog.J(Environment.MachineName) + "\"," +
                    "\"sorgular\":\"" + MakineLog.J(sorgular) + "\"," +
                    "\"sonuc\":\"" + MakineLog.J(sonuc) + "\"}";
                try { ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; } catch { }
                using (var wc = new ZamanAsimliWebClient(30000))
                {
                    wc.Encoding = Encoding.UTF8;
                    wc.Headers["Content-Type"] = "application/json; charset=utf-8";
                    string yanit = (wc.UploadString(Musteri.WebAppUrl, "POST", json) ?? "").Trim();
                    return yanit.Equals("OK", StringComparison.OrdinalIgnoreCase) ? "OK" : "Kabul edilmedi: " + (yanit.Length > 150 ? yanit.Substring(0, 150) : yanit);
                }
            }
            catch (Exception ex) { return "Gonderilemedi: " + ex.Message; }
        }

        private static string OnbellekOku()
        {
            try { return File.Exists(OnbellekYolu) ? File.ReadAllText(OnbellekYolu).Trim() : null; }
            catch { return null; }
        }

        private static void OnbellekYaz(string zaman)
        {
            try { Directory.CreateDirectory(Path.GetDirectoryName(OnbellekYolu)); File.WriteAllText(OnbellekYolu, zaman ?? ""); }
            catch { }
        }

        // v112: Gonderilmeyi bekleyen teshis sonucu (ilk satir: sorgular, kalani: sonuc).
        private static string BekleyenYolu =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "UnsalBakim", "teshis_bekleyen.txt");

        private static void BekleyenYaz(string sorgular, string sonuc)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(BekleyenYolu));
                File.WriteAllText(BekleyenYolu, (sorgular ?? "") + "\n" + (sonuc ?? ""));
            }
            catch (Exception ex) { Logger.WriteFileOnly(Log, "Teshis bekleyen yazilamadi: " + ex.Message); }
        }
    }
}
