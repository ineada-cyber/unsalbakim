using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;

namespace UnsalBakim.Core
{
    /// <summary>
    /// TALEP UZERINE (manuel) AI oyun asistani. Claude API ile, o an calisan ve statik listede
    /// OLMAYAN gorunur programlardan hangilerinin OYUN oldugunu belirler; bulunanlari KAPATIR ve
    /// kalici listeye ekler (bir dahaki 3 dk denetiminde otomatik yakalanir). KALDIRMA YAPMAZ.
    /// Anahtar sifreli saklanir. ONCE exe'nin yanindaki apikey.dat'ten (USB ile tasinir), yoksa
    /// ProgramData'dan okunur. DIKKAT: anahtar USB'de tasindiginda sifreleme zayiftir; USB fiziksel
    /// kontrol altinda tutulmali ve API anahtarina harcama limiti konmalidir (sizma = finansal risk).
    /// Gizlilik: yalnizca surec adi + program aciklamasi gonderilir (pencere basligi/dosya icerigi degil).
    /// </summary>
    public static class YapayZeka
    {
        // Anahtar iki yerde aranir: ONCE exe'nin yani (USB ile tasinir), sonra ProgramData (yerel).
        private static string YanDosya
        {
            get { try { return Path.Combine(Path.GetDirectoryName(
                System.Reflection.Assembly.GetExecutingAssembly().Location), "apikey.dat"); } catch { return null; } }
        }
        private static string YerelDosya =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                         "UnsalBakim", "apikey.dat");

        // Okuma: once USB/yan, yoksa yerel.
        private static string AnahtarDosya =>
            (YanDosya != null && File.Exists(YanDosya)) ? YanDosya : YerelDosya;

        // Varsayilan model (ucuz/hizli). Gerekirse apikey.dat'in 2. satirinda model= ile degistirilebilir.
        private const string VarsayilanModel = "claude-haiku-5-5";

        // Asla AI'ye gonderilmeyecek / asla kapatilmayacak temel programlar (yanlis kapatmayi onler).
        private static readonly HashSet<string> Guvenli = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "explorer","chrome","msedge","firefox","iexplore","opera","brave","winword","excel",
            "powerpnt","outlook","onenote","code","devenv","notepad","notepad++","taskmgr","cmd",
            "powershell","pwsh","WindowsTerminal","conhost","SearchApp","SearchHost","StartMenuExperienceHost",
            "ShellExperienceHost","ApplicationFrameHost","SystemSettings","TextInputHost","dwm","winlogon",
            "csrss","services","lsass","svchost","RuntimeBroker","SecurityHealthSystray","Norton","NortonSecurity",
            "ns","Teams","Zoom","WhatsApp","Telegram","Spotify","vlc","UnsalBakim","acrord32","AdobeARM"
        };

        // v65 (Ali Bey'in ACIK talebiyle, 5 Kasim 2026): apikey.dat HICBIR makinede yoksa/bossa
        // kullanilacak DAHILI VARSAYILAN anahtar -- "yenisi girilene kadar sabit kalsin" istendi.
        // Diger apikey.dat alanlariyla AYNI (zayif, sadece goz onunde durmasin diye) XOR yontemiyle
        // gizlenmis tutuluyor (Yetki.SifreGizle/SifreCoz, sabit "UnsalBakim-2026" anahtariyla) --
        // bu GERCEK bir koruma DEGIL, sadece mevcut apikey.dat tasarimiyla TUTARLI bir gizleme.
        // ONEMLI GUVENLIK NOTU (Ali Bey'e ayrica soylendi): bu anahtar artik HER TEK ogrenci/
        // ogretmen bilgisayarinda calisir durumda bulunacak; derlenmis programdan (agi izleyerek
        // veya decompile ile) cikarilabilir. Anthropic Console'da bu anahtara bir harcama LIMITI
        // konmasi KESINLIKLE onerilir -- istemci tarafina gomulen hicbir sir tam korunamaz, tek
        // gercek onlem sizinti olsa bile zarari sinirlamaktir.
        private const string VarsayilanAnahtarSifreli =
            "JgVeAAI2TB4aHwADU1hlMhQfN0EIJjsmN3lFcgZGPgkJUwEuMh9bVF5XdkdYBlhKVgorKVszHXtQewd1PSY7CQ0NMzxZNR1zcgpSNDwgOTgKOy0nQEYLXVkbMgojBT8HBQwoOEZ5YnN3FA==";

        /// <summary>Bu makinede KULLANICI TARAFINDAN girilmis bir apikey.dat var mi (dahili
        /// varsayilan HARIC). Panelde "ozel anahtar mi, varsayilan mi kullaniliyor" ayrimi icin.</summary>
        public static bool AnahtarVarMi()
        {
            try { return File.Exists(AnahtarDosya) && new FileInfo(AnahtarDosya).Length > 0; } catch { return false; }
        }

        /// <summary>Panelde "AI durumu" icin kisa ozet: anahtar var mi, haftalik gorev acik mi,
        /// gorev kurulu mu (schtasks), ve oyun_log.txt'teki SON "[Haftalik AI]"/"AI KontrolVeGuncelle"
        /// satirlari (en son ne zaman, ne sonuc verdi).</summary>
        public static string Durum(bool haftalikAktifAyari, bool gorevKurulu, string gun = "Pazartesi", string saat = "09:00")
        {
            var sb = new StringBuilder();
            sb.AppendLine("AI anahtari      : " + (AnahtarVarMi()
                ? "bu bilgisayara OZEL girilmis (sifreli saklanir)"
                : "dahili VARSAYILAN anahtar kullaniliyor (ozel anahtar girilmemis)"));
            sb.AppendLine("Haftalik AI ayari: " + (haftalikAktifAyari ? "ACIK (" + gun + " " + saat + ")" : "KAPALI"));
            sb.AppendLine("Haftalik gorev   : " + (gorevKurulu ? "kurulu" : "kurulu DEGIL"));

            string sonSatir = null, sonSonuc = null;
            try
            {
                string dosya = Path.Combine(Logger.LogDir, "oyun_log.txt");
                if (File.Exists(dosya))
                {
                    foreach (var satir in File.ReadLines(dosya))
                    {
                        if (satir.Contains("[Haftalik AI]")) sonSatir = satir;
                        else if (satir.Contains("AI KontrolVeGuncelle:")) sonSonuc = satir;
                    }
                }
            }
            catch { }
            sb.AppendLine();
            sb.AppendLine("Son haftalik calisma : " + (sonSatir ?? "henuz calismadi"));
            if (sonSonuc != null) sb.AppendLine("Son sonuc (ozet)     : " + sonSonuc);
            return sb.ToString();
        }

        public static void AnahtarKaydet(string anahtar, string model = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine(Yetki.SifreGizle((anahtar ?? "").Trim()));
            sb.AppendLine("model=" + (string.IsNullOrWhiteSpace(model) ? VarsayilanModel : model.Trim()));
            string icerik = sb.ToString();
            // HER IKI yere yaz:
            //  - ProgramData (KALICI): yeniden derleme / yeni surum / re-extract'te SILINMEZ.
            //  - exe'nin yani (USB ile TASINIR).
            try { Directory.CreateDirectory(Path.GetDirectoryName(YerelDosya)); File.WriteAllText(YerelDosya, icerik); } catch { }
            try { if (YanDosya != null) File.WriteAllText(YanDosya, icerik); } catch { }
        }

        private static string AnahtarOku(out string model)
        {
            model = VarsayilanModel;
            try
            {
                var lines = File.ReadAllLines(AnahtarDosya);
                if (lines.Length > 0)
                {
                    string key = Yetki.SifreCoz(lines[0].Trim()) ?? "";
                    for (int i = 1; i < lines.Length; i++)
                        if (lines[i].StartsWith("model=", StringComparison.OrdinalIgnoreCase))
                            model = lines[i].Substring(6).Trim();
                    if (!string.IsNullOrWhiteSpace(key)) return key;
                }
            }
            catch { }
            // v65: apikey.dat yoksa/bossa/cozulemediyse -- dahili varsayilan anahtara dus.
            try { return Yetki.SifreCoz(VarsayilanAnahtarSifreli) ?? ""; } catch { return ""; }
        }

        /// <summary>Manuel AI taramasi. Rapor metni dondurur (ekrana basilir).</summary>
        public static string TaraVeOner()
        {
            // Oyun engelleme ogretmen tarafindan KAPATILMISSA (Ayarlar sekmesi), AI de oyun
            // tespit/kapatma yapmaz -> VPN/YouTube "kaldir" ile ayni mantik: ayar neyse o gecerli.
            var oyunAyar = new AppSettings(); oyunAyar.Load();
            if (!oyunAyar.OyunEngelle)
                return "Oyun engelleme kapali (Ayarlar sekmesi); AI oyun taramasi atlandi.";

            string model;
            string anahtar = AnahtarOku(out model);
            if (string.IsNullOrWhiteSpace(anahtar))
                return "AI anahtari ayarli degil. Once 'AI anahtarini ayarla' ile Claude API anahtarini girin.";

            // 1) Aday programlar: statik listede olmayan, guvenli olmayan, Microsoft sistem bileseni
            //    olmayan tum kullanici programlari. Penceresi OLAN ya da aciklamasi olanlar dahil
            //    (tam ekran oyunlar ve launcher'lar da yakalansin diye genis tutuldu).
            var adaylar = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); // ad -> aciklama+baslik
            var statik = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in GameControl.Surecler) statik.Add(s.Replace(".exe", ""));
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    string ad = p.ProcessName;
                    if (statik.Contains(ad) || Guvenli.Contains(ad)) continue;
                    if (adaylar.ContainsKey(ad)) continue;

                    // v52 duzeltmesi: ONCEDEN burada p.MainWindowTitle (pencere basligi) de okunup
                    // "bilgi" alanina eklenip Claude API'ye GONDERILIYORDU -- dosyanin ustundeki
                    // "Gizlilik: yalnizca surec adi + program aciklamasi gonderilir (pencere basligi
                    // ... degil)" yorumuyla DOGRUDAN CELISIYORDU. Pencere basligi (sekme adi, belge
                    // adi, sohbet basligi vb. icerebilir) artik SADECE "penceresi var mi" (true/false,
                    // kontrol amacli, metni degil) icin kullanilir; API'ye hic gonderilmez.
                    bool pencere = p.MainWindowHandle != IntPtr.Zero;
                    string acik = "", sirket = "";
                    try { var fi = p.MainModule.FileVersionInfo; acik = fi.FileDescription ?? ""; sirket = fi.CompanyName ?? ""; } catch { }

                    // Penceresi olmayan Microsoft/Windows sistem surecini atla (svchost, RuntimeBroker vb.)
                    if (!pencere && sirket.IndexOf("Microsoft", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    // Ne penceresi ne aciklamasi olan arka plan surecini atla
                    if (!pencere && string.IsNullOrEmpty(acik)) continue;

                    adaylar[ad] = acik;
                    if (adaylar.Count >= 40) break;   // maliyeti sinirla
                }
                catch { }
            }

            // Teshis icin adaylari loga yaz
            Logger.WriteFileOnly("oyun_log.txt", "AI aday sayisi: " + adaylar.Count +
                (adaylar.Count > 0 ? " -> " + string.Join(", ", adaylar.Keys) : ""));

            if (adaylar.Count == 0)
                return "Su an incelenecek aday program bulunamadi (oyun calismiyor olabilir ya da zaten listede).";

            // 2) Istek metni
            var liste = new StringBuilder();
            foreach (var kv in adaylar)
                liste.AppendLine("- " + kv.Key + (string.IsNullOrEmpty(kv.Value) ? "" : "  (" + kv.Value + ")"));

            string soru =
                "Asagida bir okul bilgisayarinda calisan programlarin listesi var (surec adi ve aciklamasi). " +
                "Bunlardan hangileri VIDEO OYUNU? Yalnizca gercek oyunlarin SUREC ADLARINI dondur, her satira bir tane, " +
                "baska hicbir aciklama yazma. Oyun yoksa sadece YOK yaz. " +
                "Launcher/Steam/Epic/Riot, gelistirme araci, tarayici, ofis/egitim/medya programlari OYUN DEGILDIR.\n\n" +
                liste;

            // 3) API cagrisi
            string cevap;
            try { cevap = Cagir(anahtar, model, soru); }
            catch (WebException wex)
            {
                // Soket/baglanti hatasi mi, HTTP hatasi mi ayir.
                if (wex.Response is HttpWebResponse hr)
                {
                    int kod = (int)hr.StatusCode;
                    if (kod == 401) return "AI: Anahtar gecersiz (401). Yeni/dogru API anahtari girin.";
                    if (kod == 404) return "AI: Model bulunamadi (404). apikey.dat 2. satir 'model=' degerini kontrol edin.";
                    if (kod == 429) return "AI: Kota/limit (429). Hesapta kredi/limit kontrol edin.";
                    return "AI: Sunucu hatasi (" + kod + ").";
                }
                // Response yok = baglanti kurulamadi (soket): internet yok / okul guvenlik duvari / proxy.
                return "AI baglanti (soket) hatasi: Bu bilgisayardan api.anthropic.com'a ulasilamiyor. " +
                       "Sebep genelde internet yok, okul guvenlik duvari/filtre ya da proxy'dir. " +
                       "(" + wex.Status + ")";
            }
            catch (Exception ex) { return "AI baglantisi basarisiz: " + ex.Message; }

            if (string.IsNullOrWhiteSpace(cevap) || cevap.Trim().Equals("YOK", StringComparison.OrdinalIgnoreCase))
                return "AI taramasi tamamlandi. " + adaylar.Count + " aday incelendi; oyun bulunamadi.";

            // 4) AI'nin oyun dedigi adlari isle: kapat + listeye ogret
            var rapor = new StringBuilder();
            rapor.AppendLine("AI taramasi: " + adaylar.Count + " aday incelendi.");
            int islenen = 0;
            foreach (var satir in cevap.Split('\n'))
            {
                string ad = satir.Trim().TrimStart('-', '*', ' ').Replace(".exe", "").Trim();
                if (ad.Length == 0) continue;
                // Yalnizca gercekten aday olanlari kabul et (AI uydurursa gUvenli kal)
                if (!adaylar.ContainsKey(ad)) continue;
                if (Guvenli.Contains(ad)) continue;

                // Kapat
                try
                {
                    foreach (var p in Process.GetProcessesByName(ad))
                        try { p.Kill(); } catch { }
                }
                catch { }
                // Listeye ogret (kalici) + kayit
                bool yeni = GameControl.EkleOyun(ad);
                EngelKayit.Ekle(ad, "ai-kapatma");
                rapor.AppendLine("  [OYUN] " + ad + (yeni ? "  -> kapatildi + listeye eklendi" : "  -> kapatildi"));
                // v74: calisirken yakalanan oyunlar da (yukaridaki "yeni yontem arastirmasi" eklemeleri
                // gibi) "AI tespitleri" raporuna dussun -- sadece YENI (ilk kez listeye giren) olanlari
                // gonderiyoruz, zaten bilinen bir oyun tekrar yakalandiginda rapor sismesin diye.
                if (yeni) { try { DriveLog.Gonder("ai_tespit", "Oyun (calisirken yakalandi)|" + ad + "|" + Environment.UserName); } catch { } }
                islenen++;
            }
            if (islenen == 0)
                rapor.AppendLine("AI oyun olarak dogrulanan yeni program bulamadi.");
            else
            {
                // Yeni ogrenilenler icin engelleri de hemen guncelle
                try { GameControl.CalismaEngelle(); } catch { }
                rapor.AppendLine("\nYeni oyunlar artik 5 dakikalik denetimde de otomatik yakalanacak.");
            }
            Logger.WriteFileOnly("oyun_log.txt", "AI tarama: " + adaylar.Count + " aday, " + islenen + " oyun islendi.");
            return rapor.ToString();
        }

        /// <summary>
        /// TEK dugme: "AI kontrol ve guncelleme". Iki is yapar:
        ///  1) Su an calisan BILINMEYEN oyunlari tespit edip kapatir + ogretir (TaraVeOner).
        ///  2) Claude'a "ogrencilerin engel asmak icin kullandigi GUNCEL yontemler" sorar; donen
        ///     oyun/vpn exe adlarini ve oyun/vpn/youtube-ai alan adlarini ilgili KALICI listelere ekler,
        ///     sonra engelleri yeniden uygular. Boylece veritabani kendini gunceller.
        /// </summary>
        public static string KontrolVeGuncelle()
        {
            string model;
            string anahtar = AnahtarOku(out model);
            if (string.IsNullOrWhiteSpace(anahtar))
                return "AI anahtari ayarli degil. Once 'AI anahtarini ayarla' ile Claude API anahtarini girin.";

            var rapor = new StringBuilder();

            // --- 1) Calisan bilinmeyen oyunlari tara/kapat/ogret ---
            try { rapor.AppendLine(TaraVeOner()); }
            catch (Exception ex) { rapor.AppendLine("Calisan oyun taramasi hatasi: " + ex.Message); }
            rapor.AppendLine();
            rapor.AppendLine("--- Veritabani guncelleme (yeni yontem arastirmasi) ---");

            // --- 2) Guncel bypass yontemlerini sor ---
            string soru =
                "Turkiye'deki bir okulda ogrenciler, yonetici filtrelerini asmak icin bilgisayarda bazi programlar ve " +
                "web siteleri kullaniyor. Engel listemizi guncellemek istiyoruz. GUNCEL ve YAYGIN olanlari ver. " +
                "Cevabi SADECE asagidaki 5 bolum halinde, her satira TEK bir deger yazarak dondur; bolum basliklarini " +
                "AYNEN kullan, aciklama/numara/ek metin EKLEME. Bir bolum bossa sadece basligi yaz.\n" +
                "[OYUNEXE]  (oyun launcher/emulator surec/exe adlari, ornek: tlauncher, bluestacks, ldplayer)\n" +
                "[OYUNSITE] (tarayici/cloud oyun site alan adlari, ornek: poki.com, now.gg)\n" +
                "[VPNEXE]   (vpn/proxy program surec/exe adlari, ornek: nordvpn, betternet, psiphon)\n" +
                "[VPNSITE]  (vpn/proxy saglayici alan adlari, ornek: nordvpn.com, 1clickvpn.net)\n" +
                "[AISITE]   (youtube'a dolayli erisim saglayan youtube proxy + yapay zeka sohbet alan adlari, ornek: gemini.google.com, perplexity.ai)";

            string cevap;
            try { cevap = Cagir(anahtar, model, soru, 900); }
            catch (WebException wex)
            {
                if (wex.Response is HttpWebResponse hr)
                {
                    int kod = (int)hr.StatusCode;
                    if (kod == 401) { rapor.AppendLine("AI: Anahtar gecersiz (401)."); return rapor.ToString(); }
                    if (kod == 404) { rapor.AppendLine("AI: Model bulunamadi (404). apikey.dat 'model=' satirini kontrol edin."); return rapor.ToString(); }
                    if (kod == 429) { rapor.AppendLine("AI: Kota/limit (429)."); return rapor.ToString(); }
                    rapor.AppendLine("AI: Sunucu hatasi (" + kod + ")."); return rapor.ToString();
                }
                rapor.AppendLine("AI baglanti (soket) hatasi: api.anthropic.com'a ulasilamiyor (internet/guvenlik duvari/proxy). (" + wex.Status + ")");
                return rapor.ToString();
            }
            catch (Exception ex) { rapor.AppendLine("AI baglantisi basarisiz: " + ex.Message); return rapor.ToString(); }

            // --- Donen cevabi bolumlere ayir ve kalici listelere ekle ---
            int eklenenOyunExe = 0, eklenenOyunSite = 0, eklenenVpnExe = 0, eklenenVpnSite = 0, eklenenAiSite = 0;
            string bolum = "";
            foreach (var ham in (cevap ?? "").Split('\n'))
            {
                string satir = ham.Trim();
                if (satir.Length == 0) continue;
                string ust = satir.ToUpperInvariant();
                if (ust.StartsWith("[OYUNEXE")) { bolum = "OYUNEXE"; continue; }
                if (ust.StartsWith("[OYUNSITE")) { bolum = "OYUNSITE"; continue; }
                if (ust.StartsWith("[VPNEXE")) { bolum = "VPNEXE"; continue; }
                if (ust.StartsWith("[VPNSITE")) { bolum = "VPNSITE"; continue; }
                if (ust.StartsWith("[AISITE")) { bolum = "AISITE"; continue; }

                // Deger satiri: tire/yildiz/numara temizle
                string deger = satir.TrimStart('-', '*', '•', ' ', '\t').Trim();
                // "1. x" / "1) x" gibi numaralari ayikla
                int nk = deger.IndexOfAny(new[] { '.', ')' });
                if (nk > 0 && nk <= 3) { bool hepsiRakam = true; for (int i = 0; i < nk; i++) if (!char.IsDigit(deger[i])) { hepsiRakam = false; break; } if (hepsiRakam) deger = deger.Substring(nk + 1).Trim(); }
                if (deger.Length == 0 || deger.Equals("YOK", StringComparison.OrdinalIgnoreCase)) continue;
                // Sadece ilk token (aciklama sizarsa kes); bosluk iceriyorsa ilk parcayi al
                int bosluk = deger.IndexOf(' ');
                if (bosluk > 0) deger = deger.Substring(0, bosluk).Trim();
                deger = deger.Trim().Trim(',', ';', '"', '\'');
                if (deger.Length < 2 || deger.Length > 60) continue;

                try
                {
                    // v74 (Ali Bey'in istegi: "AI tespitlerini raporlara ekleyelim" -- makina, tur/isim,
                    // tarih/saat, kullanici): her basarili eklemede merkeze TEK satirlik bir olay
                    // gonderiliyor (DriveLog.Gonder zaten bilgisayar+mac'i otomatik ekliyor; kullanici
                    // adi burada detay icine "TUR|DEGER|KULLANICI" olarak kodlaniyor -- Raporlar'daki
                    // "AI tespitleri" bu olaylari okuyup tabloya cevirir).
                    string kullaniciAdi = Environment.UserName;
                    switch (bolum)
                    {
                        case "OYUNEXE":
                            if (GameControl.EkleOyun(deger)) { eklenenOyunExe++; DriveLog.Gonder("ai_tespit", "Oyun (program)|" + deger + "|" + kullaniciAdi); }
                            break;
                        case "OYUNSITE":
                            if (deger.Contains(".") && SiteEngelle.EkleSite(deger)) { eklenenOyunSite++; DriveLog.Gonder("ai_tespit", "Oyun (site)|" + deger + "|" + kullaniciAdi); }
                            break;
                        case "VPNEXE":
                            if (VpnKontrol.EkleExe(deger)) { eklenenVpnExe++; DriveLog.Gonder("ai_tespit", "VPN (program)|" + deger + "|" + kullaniciAdi); }
                            break;
                        case "VPNSITE":
                            if (deger.Contains(".") && VpnKontrol.EkleSite(deger)) { eklenenVpnSite++; DriveLog.Gonder("ai_tespit", "VPN (site)|" + deger + "|" + kullaniciAdi); }
                            break;
                        // v55: AI siteleri artik YoutubeKisit'ten AYRI (bkz. Core/AiSiteEngelle.cs).
                        case "AISITE":
                            if (deger.Contains(".") && AiSiteEngelle.EkleSite(deger)) { eklenenAiSite++; DriveLog.Gonder("ai_tespit", "YouTube/AI site|" + deger + "|" + kullaniciAdi); }
                            break;
                    }
                }
                catch { }
            }

            rapor.AppendLine("Yeni oyun programi : " + eklenenOyunExe);
            rapor.AppendLine("Yeni oyun sitesi   : " + eklenenOyunSite);
            rapor.AppendLine("Yeni VPN programi  : " + eklenenVpnExe);
            rapor.AppendLine("Yeni VPN sitesi    : " + eklenenVpnSite);
            rapor.AppendLine("Yeni YouTube/AI site: " + eklenenAiSite);

            int toplam = eklenenOyunExe + eklenenOyunSite + eklenenVpnExe + eklenenVpnSite + eklenenAiSite;
            if (toplam > 0)
            {
                // Engelleri hemen yeniden uygula (yeni eklenenler devreye girsin) -- HER ZAMAN ayarlanmis
                // bayraklara gore: ogretmen "kaldir" demisse (OyunEngelle/VpnEngelle/YoutubeKisitli kapali)
                // AI yeniden ACMAZ. VPN ve YouTube zaten boyle calisiyordu; Oyun da artik ayni kurala uyar.
                try { var a = new AppSettings(); a.Load();
                      if (a.OyunEngelle) GameControl.CalismaEngelle();
                      if (a.VpnEngelle) VpnKontrol.Engelle();
                } catch { }
                try { if (eklenenAiSite > 0) YoutubeKisit.KaldirEngel(); YoutubeKisit.SaatiUygula(); } catch { }
                rapor.AppendLine();
                rapor.AppendLine(toplam + " yeni kayit veritabanina eklendi ve engeller guncellendi.");
                rapor.AppendLine("(Kalici: yeniden derleme/yeni surum silmez.)");
            }
            else
            {
                rapor.AppendLine();
                rapor.AppendLine("Veritabani zaten guncel; yeni kayit eklenmedi.");
            }
            Logger.WriteFileOnly("oyun_log.txt", "AI KontrolVeGuncelle: +oyunExe=" + eklenenOyunExe +
                " +oyunSite=" + eklenenOyunSite + " +vpnExe=" + eklenenVpnExe + " +vpnSite=" + eklenenVpnSite +
                " +aiSite=" + eklenenAiSite);
            return rapor.ToString();
        }

        // --- Claude API (api.anthropic.com/v1/messages) ---
        private static string Cagir(string anahtar, string model, string kullaniciMetni, int maxTokens = 400)
        {
            // TLS sağlamlaştırma: TLS 1.2 (ve varsa 1.3). Bazı makinelerde eski varsayilan TLS soket hatasi verir.
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)12288; /* Tls13 */ } catch { }
                ServicePointManager.Expect100Continue = false;
                ServicePointManager.DefaultConnectionLimit = 10;
            }
            catch { }
            var govde = new Dictionary<string, object>
            {
                { "model", model },
                { "max_tokens", maxTokens },
                { "messages", new object[] { new Dictionary<string, object> {
                    { "role", "user" }, { "content", kullaniciMetni } } } }
            };
            string json = new JavaScriptSerializer().Serialize(govde);

            // Bazi okul makinelerinde WinHTTP sistem proxy ayari eski/bozuk kalmis olabilir; bu durumda
            // sistem proxy'siyle deneme "soket hatasi" (ConnectFailure/Timeout) verir ama proxy'siz
            // dogrudan baglanti calisabilir. Once sistem proxy'si, olmazsa PROXYSIZ bir kez daha dene.
            WebException sonHata = null;
            foreach (bool sistemProxyKullan in new[] { true, false })
            {
                try
                {
                    return CagirDene(anahtar, json, sistemProxyKullan);
                }
                catch (WebException wex)
                {
                    sonHata = wex;
                    // HTTP yaniti olan hata (401/404/429/5xx) proxy degisikligiyle duzelmez; hemen firlat.
                    if (wex.Response is HttpWebResponse) throw;
                    // Response yoksa (soket hatasi) -> proxy'siz tekrar denemeye devam et.
                }
            }
            throw sonHata;
        }

        private static string CagirDene(string anahtar, string json, bool sistemProxyKullan)
        {
            var req = (HttpWebRequest)WebRequest.Create("https://api.anthropic.com/v1/messages");
            req.Method = "POST";
            req.ContentType = "application/json";
            req.Headers["x-api-key"] = anahtar;
            req.Headers["anthropic-version"] = "2023-06-01";
            req.UserAgent = "UnsalBakim/1.0";
            req.KeepAlive = false;
            req.Timeout = 30000;
            if (sistemProxyKullan) { try { req.Proxy = WebRequest.GetSystemWebProxy(); } catch { } }
            else { req.Proxy = null; }   // dogrudan baglanti (bozuk/eski WinHTTP proxy ayarini atla)
            using (var sw = new StreamWriter(req.GetRequestStream(), new UTF8Encoding(false)))
                sw.Write(json);

            string yanit;
            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                yanit = sr.ReadToEnd();

            // content[0].text
            try
            {
                var d = new JavaScriptSerializer().DeserializeObject(yanit) as Dictionary<string, object>;
                var content = d["content"] as object[];
                var ilk = content[0] as Dictionary<string, object>;
                return (ilk["text"] ?? "").ToString();
            }
            catch { return yanit; }
        }
    }
}
