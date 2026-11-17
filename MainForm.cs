using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using System.Windows.Forms;
using UnsalBakim.Core;

namespace UnsalBakim
{
    public class MainForm : Form
    {
        private readonly AppSettings _ayar = new AppSettings();
        private RichTextBox _log;
        private Label _durum;
        private Label _genelBakisMetni;
        private Label _sayfaBasligi;
        private Label _kpiOyunDeger, _kpiYoutubeDeger, _kpiGuncellemeDeger;
        private readonly List<Button> _menuButonlari = new List<Button>();
        private readonly List<Panel> _sekmePanelleri = new List<Panel>();

        // v104 (Ali Bey'in istegi: "menu tasarimini renkleri, ekran buyuklugunu AOMEI Backupper
        // veya partition manager gibi" -- secilen yon: "2 gibi daha albenili havali ciddi ticari
        // havada"): koyu/teal "hacker" temasi yerine acik zeminli, mavi vurgulu, lacivert sol
        // menulu "kurumsal yazilim" gorunumu. Renkler
        private readonly Color RenkBg = Color.FromArgb(244, 247, 250);      // icerik alani zemin (acik gri)
        private readonly Color RenkPanel = Color.White;                     // kart/giris kutusu zemin (beyaz)
        private readonly Color RenkFg = Color.FromArgb(33, 40, 48);         // ana metin (koyu)
        private readonly Color RenkVurgu = Color.FromArgb(21, 101, 216);    // mavi vurgu (baslik cubugu, butonlar)
        private readonly Color RenkYanMenu = Color.FromArgb(14, 46, 94);    // sol menu zemin (lacivert)
        private readonly Color RenkYanMenuHover = Color.FromArgb(21, 66, 128); // sol menu uzerine gelince
        private readonly Color RenkYanMenuMetin = Color.FromArgb(197, 217, 247); // sol menude secili olmayan metin
        private readonly Color RenkKenar = Color.FromArgb(222, 228, 234);   // ince ayrac/kenarlik cizgisi

        public MainForm()
        {
            _ayar.Load();
            Logger.OnLine = s => UiLog(s);
            KurArayuz();
            // v109: panel ProgramData\UnsalBakim'e yazabiliyor mu? (Yazamazsa ayarlar KAYDEDILEMEZ,
            // gorevler guncellenemez -- sessizce degil, acikca uyar.)
            try
            {
                string test = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "UnsalBakim", "yazma_testi.tmp");
                System.IO.File.WriteAllText(test, "ok"); System.IO.File.Delete(test);
            }
            catch (Exception ex)
            {
                bool yuk = Yetki.Yukseltilmis();
                Logger.UI("[!] DIKKAT: Panel C:\\ProgramData\\UnsalBakim klasorune YAZAMIYOR (" + ex.Message + ").");
                Logger.UI("    Yonetici yetkisi: " + (yuk ? "VAR -- engel buyuk ihtimalle guvenlik yazilimi (Norton vb.) veya klasor izni." : "YOK -- paneli yonetici olarak acin."));
                Logger.UI("    Bu durumda 'Ayarlari kaydet' ve gorev guncellemeleri UYGULANMAZ. Kurulum sekmesinden bir kez 'KUR' yapmak izinleri duzeltir.");
            }
        }

        private void KurArayuz()
        {
            Text = "Unsal Bakim  [" + License.Surum + "]";
            // v77 (Ali Bey'in istegi: "ikonsuz sikinti buyuk, konuyla ilgili ikon bul"):
            // exe'ye artik gercek bir ikon gomulu (UnsalBakim.csproj -> ApplicationIcon,
            // Resources\unsalbakim.ico). Form'un kendi baslik cubugu/gorev cubugu ikonunu
            // da AYNI exe ikonundan alip baglıyoruz; ayrica .ico dosyasini kopyalamaya ya
            // da gomulu kaynak (EmbeddedResource) eklemeye gerek yok.
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            BackColor = RenkBg;
            ForeColor = RenkFg;
            Font = new Font("Segoe UI", 9.5f);
            // v104 (Ali Bey'in istegi: "ekran buyuklugu"): eski varsayilan (880x660) dar
            // kaliyordu -- yeni sol menu + kart duzeniyle rahat calismak icin buyutuldu.
            MinimumSize = new Size(900, 640);
            Size = new Size(1080, 740);
            StartPosition = FormStartPosition.CenterScreen;
            // v104 duzeltme (Ali Bey'in istegi: "acilista tam ekranla baslasin"): program artik
            // HER ZAMAN maksimize (tam ekran) acilir. StartPosition/Size yine de yukarida tutulur
            // -- WindowState kullanicinin elle "geri yukle"yle kucultmesi halinde pencerenin
            // donecegi makul bir varsayilan boyut/konum saglar.
            WindowState = FormWindowState.Maximized;

            // Baslik -- tam genislikte, gradyanli (lacivert -> acik mavi) ust cubuk (sol menunun
            // de USTUNDEN gecer). v104 (Ali Bey'in istegi: "albenisi yuksek, ciddi ticari olsun"):
            // duz tek renk yerine kose-koseye gradyan ile daha "premium" bir gorunum.
            var baslik = new Panel { Dock = DockStyle.Top, Height = 62 };
            baslik.Paint += (s, e) =>
            {
                using (var br = new LinearGradientBrush(baslik.ClientRectangle,
                           Color.FromArgb(12, 46, 94), Color.FromArgb(47, 143, 232), LinearGradientMode.ForwardDiagonal))
                    e.Graphics.FillRectangle(br, baslik.ClientRectangle);
            };
            var lbl = new Label
            {
                Text = "UNSAL BAKIM",
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 15f, FontStyle.Bold),
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(20, 9)
            };
            var alt = new Label
            {
                // v104 (Ali Bey'in istegi: "uygun version ... olmali"): surum numarasi artik
                // sadece pencere baslik cubugunda degil, burada da acikca goruluyor.
                Text = "Windows Bakim, Hizlandirma ve Oyun Kontrol  -  " + License.Surum + "  -  " + License.Durum,
                ForeColor = Color.FromArgb(214, 230, 252),
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(22, 36)
            };
            baslik.Controls.Add(lbl);
            baslik.Controls.Add(alt);

            // v55: 8 sekmeli duzen -- her mevcut dugme/islev KORUNDU, sadece gruplama
            // degisti (bkz. gelistirme dokumanlari / dagitim raporu). Hicbir ozellik SILINMEDI.
            // v104: TabControl'un kendi sekme seridi yerine, soldaki ozel lacivert menu ile
            // gezinilir -- 8 "Sekme" metodunun ICERIGI (FlowLayoutPanel/kontroller) HICBIR
            // SEKILDE DEGISTIRILMEDI, sadece disaridaki tasiyici TabPage yerine duz Panel oldu.
            // v104 (Ali Bey'in istegi: "uygun version ve help olmali"): 9. sekme olarak Yardim eklendi.
            var sekmeler = new (string Ad, string Ikon, Panel Panel)[]
            {
                ("Genel Bakis", "●", SekmeGenelBakis()),
                ("Bakim", "⚙", SekmeBakim()),
                ("Kurulum", "⬇", SekmeKurulum()),
                ("Koruma", "\U0001F6E1", SekmeKoruma()),
                ("Yerel Ayarlar", "\U0001F527", SekmeYerelAyarlar()),
                ("Filo Yonetimi", "\U0001F4E1", SekmeFiloYonetimi()),
                ("Raporlar", "\U0001F4CA", SekmeRaporlar()),
                ("Kullanicilar", "\U0001F464", SekmeKullanici()),
                ("Yardim", "ℹ", SekmeYardim())
            };

            var icerikAlani = new Panel { Dock = DockStyle.Fill, BackColor = RenkBg };
            foreach (var s in sekmeler)
            {
                _sekmePanelleri.Add(s.Panel);
                icerikAlani.Controls.Add(s.Panel);
            }

            _sayfaBasligi = new Label
            {
                Dock = DockStyle.Top,
                Height = 40,
                Text = sekmeler[0].Ad,
                ForeColor = RenkFg,
                Font = new Font("Segoe UI Semibold", 12.5f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(22, 0, 0, 0),
                BackColor = RenkBg
            };

            var sagPanel = new Panel { Dock = DockStyle.Fill, BackColor = RenkBg };
            sagPanel.Controls.Add(icerikAlani);
            sagPanel.Controls.Add(_sayfaBasligi);

            var solMenu = new Panel { Dock = DockStyle.Left, Width = 212, BackColor = RenkYanMenu };
            var navAkis = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(0, 10, 0, 0)
            };
            for (int i = 0; i < sekmeler.Length; i++)
            {
                int idx = i; // kapali degiskeni yakala
                var b = new Button
                {
                    Text = "   " + sekmeler[i].Ikon + "    " + sekmeler[i].Ad,
                    Width = 212,
                    Height = 46,
                    TextAlign = ContentAlignment.MiddleLeft,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = RenkYanMenu,
                    ForeColor = RenkYanMenuMetin,
                    Font = new Font("Segoe UI", 10f),
                    Margin = new Padding(0),
                    Cursor = Cursors.Hand
                };
                b.FlatAppearance.BorderSize = 0;
                b.FlatAppearance.MouseOverBackColor = RenkYanMenuHover;
                b.Click += (s, e) => SekmeSec(idx, sekmeler);
                _menuButonlari.Add(b);
                navAkis.Controls.Add(b);
            }
            solMenu.Controls.Add(navAkis);
            SekmeSec(0, sekmeler);

            // Log -- acik temaya uyumlu, sade bir "islem gunlugu" kutusu
            _log = new RichTextBox
            {
                Dock = DockStyle.Bottom,
                Height = 170,
                ReadOnly = true,
                BackColor = Color.White,
                ForeColor = RenkFg,
                Font = new Font("Consolas", 9f),
                BorderStyle = BorderStyle.FixedSingle
            };

            _durum = new Label { Dock = DockStyle.Bottom, Height = 24, Text = "Hazir.", ForeColor = Color.FromArgb(110, 120, 130), BackColor = RenkBg, Padding = new Padding(10, 4, 0, 0) };

            Controls.Add(sagPanel);
            Controls.Add(solMenu);
            Controls.Add(_durum);
            Controls.Add(_log);
            Controls.Add(baslik);

            UiLog("Unsal Bakim " + License.Surum + " baslatildi. " + License.Durum);
            if (!License.BetaGecerli)
                UiLog("[UYARI] Beta suresi doldu. Islevler sinirli olabilir.");
        }

        // v104: sol menuden bir sekme secildiginde cagrilir -- ilgili icerik panelini
        // gosterir/digerlerini gizler, ust sayfa basligini gunceller, ve menu butonlarinin
        // "secili" gorunumunu (mavi zemin + beyaz yazi) tek bu sekmede tutar.
        private void SekmeSec(int idx, (string Ad, string Ikon, Panel Panel)[] sekmeler)
        {
            for (int k = 0; k < sekmeler.Length; k++) sekmeler[k].Panel.Visible = (k == idx);
            _sayfaBasligi.Text = sekmeler[idx].Ad;
            for (int k = 0; k < _menuButonlari.Count; k++)
            {
                bool secili = (k == idx);
                _menuButonlari[k].BackColor = secili ? RenkVurgu : RenkYanMenu;
                _menuButonlari[k].ForeColor = secili ? Color.White : RenkYanMenuMetin;
                _menuButonlari[k].Font = new Font("Segoe UI" + (secili ? " Semibold" : ""), 10f, secili ? FontStyle.Bold : FontStyle.Regular);
            }
        }

        // =====================================================================================
        // 1) GENEL BAKIS (yeni, v55) -- salt-okunur ozet. Gercek bir hata-takip altyapisi
        //    olmadigi icin KASITLI olarak "bekleyen hata sayisi" gibi UYDURMA bir widget
        //    EKLENMEDI; burada sadece gercekten bilinen/okunabilir degerler gosterilir.
        // =====================================================================================
        private Panel SekmeGenelBakis()
        {
            var tp = YeniSekme("Genel Bakis");
            var p = AkisPanel();

            // v104 (Ali Bey'in istegi: "albenisi yuksek, ciddi ticari olsun"): detayli metin
            // listesinden ONCE, ilk bakista goze carpan birkac renkli "KPI" ozet kutusu.
            var kpiSatiri = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = true, Margin = new Padding(0, 0, 0, 4) };
            var kpiSurum = KpiKutusu("Surum", License.Surum, RenkVurgu);
            var kpiOyun = KpiKutusu("Oyun Engelleme", "Acik", Color.FromArgb(30, 158, 90));
            var kpiYoutube = KpiKutusu("YouTube", "-", RenkVurgu);
            var kpiGuncelleme = KpiKutusu("Guncelleme", "-", RenkVurgu);
            _kpiOyunDeger = kpiOyun.Deger;
            _kpiYoutubeDeger = kpiYoutube.Deger;
            _kpiGuncellemeDeger = kpiGuncelleme.Deger;
            kpiSatiri.Controls.Add(kpiSurum.Kutu);
            kpiSatiri.Controls.Add(kpiOyun.Kutu);
            kpiSatiri.Controls.Add(kpiYoutube.Kutu);
            kpiSatiri.Controls.Add(kpiGuncelleme.Kutu);
            p.Controls.Add(kpiSatiri);

            _genelBakisMetni = new Label
            {
                AutoSize = true,
                ForeColor = RenkFg,
                Font = new Font("Consolas", 10f),
                Margin = new Padding(6, 6, 0, 10)
            };
            GenelBakisYenile();
            p.Controls.Add(_genelBakisMetni);

            p.Controls.Add(Buton("Yenile", async () =>
            {
                await System.Threading.Tasks.Task.CompletedTask;
                _ayar.Load();
                GenelBakisYenile();
            }));

            tp.Controls.Add(p);
            return tp;
        }

        private void GenelBakisYenile()
        {
            var filo = FiloPolitika.Durum(false);
            string Efektif(bool? filoDeger, bool yerelDeger, string acikKelime, string kapaliKelime)
            {
                if (filoDeger == null) return (yerelDeger ? acikKelime : kapaliKelime) + " (yerel ayar)";
                return "FILO ZORUNLU: " + (filoDeger.Value ? acikKelime : kapaliKelime);
            }

            _genelBakisMetni.Text =
                "Surum              : " + License.Surum + Environment.NewLine +
                "Lisans durumu       : " + License.Durum + Environment.NewLine +
                "Makine turu         : " + (string.IsNullOrWhiteSpace(_ayar.MakineTuru) ? "(henuz kurulmadi)" : _ayar.MakineTuru.ToUpperInvariant()) + Environment.NewLine +
                Environment.NewLine +
                "Koruma ayarlari (bu bilgisayar; FILO ZORUNLU gorulen bir ayar tum okulu etkiler):" + Environment.NewLine +
                // v101: Oyun artik filodan ETKILENMIYOR (gomulu/sabit) -- eski Sheet kaydinda
                // hala bir deger kalmis olsa bile goz ardi edilir, gercek/yerel durum gosterilir.
                "  - Oyun engelleme    : " + (_ayar.OyunEngelle ? "acik" : "kapali") + " (gomulu, filodan etkilenmez)" + Environment.NewLine +
                "  - VPN/Hotspot engeli: " + Efektif(filo.Vpn, _ayar.VpnEngelle, "acik", "kapali") + Environment.NewLine +
                // v57: ders saati/teneffus ayrimi kaldirildi -- YouTube artik SUREKLI acik/kapali
                // (bkz. YoutubeKisit.cs). Etiket eskiden "YouTube ders saati" idi, "ders saatinde
                // kisitli, teneffuste serbest" gibi yanlis bir izlenim veriyordu -- duzeltildi.
                "  - YouTube           : " + Efektif(filo.Youtube, _ayar.YoutubeKisitli, "kisitli", "serbest") + Environment.NewLine +
                "  - AI web sitesi     : " + Efektif(filo.AiSite, _ayar.AiSiteEngelle, "engelli", "serbest") + Environment.NewLine +
                "  - Kamera            : " + (_ayar.KameraKapali ? "kapali" : "acik") + Environment.NewLine +
                Environment.NewLine +
                "  - Haftalik AI taramasi : " + (_ayar.AiHaftalikAktif ? "acik (" + _ayar.AiGun + " " + _ayar.AiSaat + ")" : "kapali") + Environment.NewLine +
                "  - Otomatik guncelleme  : " + (_ayar.OtoGuncellemeAktif ? "acik (saatlik)" : "kapali");

            // v104: ust KPI kutularini da ayni verilerle guncelle (ilk acilista da, Yenile'de de).
            if (_kpiOyunDeger != null)
            {
                bool youtubeKisitli = (filo.Youtube ?? _ayar.YoutubeKisitli);
                _kpiOyunDeger.Text = _ayar.OyunEngelle ? "Acik" : "Kapali";
                _kpiYoutubeDeger.Text = youtubeKisitli ? "Kisitli" : "Serbest";
                _kpiGuncellemeDeger.Text = _ayar.OtoGuncellemeAktif ? "Otomatik" : "Kapali";
            }
        }

        // =====================================================================================
        // 2) BAKIM
        // =====================================================================================
        private Panel SekmeBakim()
        {
            var tp = YeniSekme("Bakim");
            var p = AkisPanel();
            p.Controls.Add(Buton("Hizli bakim (birkac dakika surer)", async () => await Calistir("Hafif bakim", () => Maintenance.HafifBakim(_ayar))));
            p.Controls.Add(Buton("Detayli bakim (30-90 dakika surer)", async () =>
            {
                if (MessageBox.Show("Detayli bakim 30-90 dakika surebilir. Baslatilsin mi?", "Onay", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    await Calistir("Agir bakim", () => Maintenance.AgirBakim(_ayar));
            }));

            // v109: ag sifirlama artik OTOMATIK bakimda YOK -- sadece burada, onayla.
            p.Controls.Add(Buton("Ag ayarlarini sifirla (onayli)", async () =>
            {
                if (MessageBox.Show("Ag ayarlari (winsock + TCP/IP) sifirlanacak.\r\n\r\n- Elle girilmis SABIT IP/DNS ayarlari silinebilir.\r\n- Etkili olmasi icin bilgisayar yeniden baslatilmalidir.\r\n\r\nDevam edilsin mi?",
                        "Ag sifirlama", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                    await Calistir("Ag sifirlama", () => AkilliBakim.AgAyarlariniSifirla());
            }));
            p.Controls.Add(Buton("Disk durumu / acil temizlik", async () => await Calistir("Disk kontrol", () =>
            {
                double b, t;
                bool kritik = AkilliBakim.DiskKritikMi(out b, out t);
                Logger.UI("Disk: " + AkilliBakim.DiskOzet() + " | Geri yukleme noktasi: " + AkilliBakim.GeriYuklemeNoktasiSayisi());
                if (kritik) { Logger.UI("Disk KRITIK -- acil temizlik baslatiliyor (birkac dakika surebilir)..."); AkilliBakim.DiskAcilTemizlik(); Logger.UI("Sonra: " + AkilliBakim.DiskOzet()); }
                else Logger.UI("Disk kritik degil (esik: %10 veya 15 GB) -- acil temizlik gerekmiyor.");
            })));
            p.Controls.Add(Buton("MakineLog'u simdi gonder", async () => await Calistir("MakineLog", () =>
            {
                string sonuc = MakineLog.Gonder(_ayar, false);
                Logger.UI(sonuc == "OK" ? "MakineLog gonderildi (Sheet -> MakineLog sekmesi, bu makinenin satiri)." : "[!] " + sonuc);
            })));

            p.Controls.Add(new Label { Text = "Otomatik gorevler", ForeColor = RenkVurgu, AutoSize = true, Margin = new Padding(6, 14, 0, 4) });
            p.Controls.Add(Buton("Otomatik gorevlerin durumunu goster", async () => await Calistir("Gorev durumu", () =>
                Logger.UI("Planli gorevler:" + Environment.NewLine + TaskHelper.GorevDurumu()))));

            p.Controls.Add(new Label { Text = "Surum", ForeColor = RenkVurgu, AutoSize = true, Margin = new Padding(6, 14, 0, 4) });
            // v22: Google Drive uzerinden surum kontrolu. SADECE kontrol/indirme yapar;
            // calisan/kurulu kopyaya DOKUNMAZ -- kurmak (eski exe'nin uzerine kopyalamak + "KUR")
            // her zaman ogretmenin elle yaptigi, bilinçli bir adim olarak kaliyor.
            p.Controls.Add(Buton("Yeni surum var mi, kontrol et", async () => await Calistir("Guncelleme kontrol", () =>
                Logger.UI(GuncellemeKontrol.KontrolEt()))));
            p.Controls.Add(Buton("Yeni surumu indir ve bu panele kur", async () =>
            {
                if (MessageBox.Show(
                        "Yeni surum indirilip bu panelin dosyalarinin (bin\\UnsalBakim.exe) uzerine\r\n" +
                        "otomatik yerlestirilecek, panel yeniden baslayacak.\r\n\r\n" +
                        "NOT: Bu sadece PANEL dosyasini gunceller; gizli kurulumu/zamanlanmis\r\n" +
                        "gorevleri yeni surume tasimak icin panel yeniden actiktan sonra yine\r\n" +
                        "'KUR' dugmesine basmaniz gerekir.\r\n\r\nDevam edilsin mi?",
                        "Guncelleme", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    await Calistir("Guncelleme indir ve uygula", () =>
                        Logger.UI(GuncellemeKontrol.YeniSurumuIndirVeDegistir()));
                }
            }));

            p.Controls.Add(new Label { Text = "Envanter / Yonetici", ForeColor = RenkVurgu, AutoSize = true, Margin = new Padding(6, 14, 0, 4) });
            // v49: Makine envanteri (CPU/RAM/disk/Windows/lisans/surum) -- Hafif Bakim gorevinden
            // zaten GUNLUK otomatik gonderiliyor; bu dugme elle tetikleme icin (listeyi gormek
            // icin: Raporlar sekmesi).
            p.Controls.Add(Buton("Envanter bilgimi simdi gonder (bu bilgisayar)", async () => await Calistir("Envanter", () =>
            {
                DriveLog.EnvanterGonder(_ayar);
                Logger.UI("Envanter gonderildi (WebAppUrl bos veya Apps Script eskiyse sessizce atlanmis olabilir).");
            })));
            p.Controls.Add(Buton("Yonetici hesabini kontrol et (hesap var mi, sifresi guncel mi)", async () => await Calistir("Yonetici durumu", () =>
                Logger.UI(YoneticiSifre.Durum()))));
            p.Controls.Add(Buton("Yonetici sifresini hemen guncelle (saati beklemeden)", async () => await Calistir("Yonetici sifre kontrolu", () =>
            {
                YoneticiSifre.KontrolEtVeUygula();
                Logger.UI(YoneticiSifre.Durum());
            })));

            tp.Controls.Add(p);
            return tp;
        }

        // =====================================================================================
        // 3) KURULUM (yeni, v55) -- Bakim sekmesinden tasindi: Ogrenci/Ogretmen profil secimi,
        //    USB sifresiz kurulum ayari, Kurulumu tamamen kaldir (yazili onay ile).
        // =====================================================================================
        private Panel SekmeKurulum()
        {
            var tp = YeniSekme("Kurulum");
            var p = AkisPanel();

            p.Controls.Add(new Label { Text = "Makine profili", ForeColor = RenkVurgu, AutoSize = true, Margin = new Padding(6, 6, 0, 4) });
            // v40: kurulum artik HER ZAMAN bir profille yapilir -- "gizli olmayan" ayri bir kurulum
            // secenegi yok, ikisi de her zaman gizli kurar (Program Files + SYSTEM gorevleri).
            p.Controls.Add(Buton("OGRENCI bilgisayari olarak kur (gizli, tum kisitlamalar aktif)", async () =>
                await KurGizliProfil("ogrenci",
                    "Bu bilgisayar OGRENCI bilgisayari olarak GIZLI kurulacak:\r\n" +
                    "- Oyun engelleme + YouTube kisiti + VPN/Hotspot engeli + AI web\r\n" +
                    "  sitesi engeli -- HEPSI otomatik AKTIF olur (hicbir ek islem gerekmez)\r\n" +
                    "- Varsa ONCEKI kurulum once sonlandirilir\r\n" +
                    "- Program Files altinda gizli klasor + SYSTEM gorevleri\r\n" +
                    "- Ogrenci (standart kullanici) goremez/kaldiramaz\r\n\r\nDevam edilsin mi?")));
            p.Controls.Add(Buton("OGRETMEN bilgisayari olarak kur (gizli, kisitlama yok)", async () =>
                await KurGizliProfil("ogretmen",
                    "Bu bilgisayar OGRETMEN bilgisayari olarak GIZLI kurulacak:\r\n" +
                    "- Oyun/VPN/YouTube/AI web sitesi kisitlamasi OLMAYACAK (hicbiri aktif olmaz)\r\n" +
                    "- Hafif/Agir bakim, haftalik AI kontrol, otomatik guncelleme yine AKTIF kalir\r\n" +
                    "- Varsa ONCEKI kurulum once sonlandirilir\r\n" +
                    "- Program Files altinda gizli klasor + SYSTEM gorevleri\r\n" +
                    "- Masaustu + Baslat menusune UNSAL BAKIM kisayolu kurulur (v77)\r\n\r\nDevam edilsin mi?")));

            p.Controls.Add(new Label { Text = "USB", ForeColor = RenkVurgu, AutoSize = true, Margin = new Padding(6, 14, 0, 4) });
            p.Controls.Add(Buton("USB icin sifre sormadan kurulum ayarla", async () =>
            {
                // v57 hata duzeltmesi: InputBox/MessageBox cagrilari ONCEDEN Calistir(...) icinde,
                // yani arka plan (Task.Run) is parcacigindan aciliyordu -- WinForms modal
                // diyaloglarini arka plan (MTA) is parcacigindan acmak kararsiz davranisa yol
                // acabilir. Artik tamami dogrudan UI is parcacigindan (bu olay isleyicisinde)
                // calisiyor; Yetki.KurulumIniYaz cok hizli oldugu icin ayrica Task.Run'a gerek yok.
                // Ayrica parola artik duz InputBox yerine maskeli (yildizli) ParolaSor ile soruluyor.
                await Task.CompletedTask;
                string kul = Microsoft.VisualBasic.Interaction.InputBox(
                    "Yonetici hesabi (ornek:  .\\Yonetici  veya  BILGISAYAR\\admin  veya  DOMAIN\\kullanici):",
                    "Sifresiz kurulum - hesap", ".\\Yonetici");
                if (string.IsNullOrWhiteSpace(kul)) { Logger.UI("Iptal edildi."); return; }
                string pwd = ParolaSor("Bu hesabin parolasi:", "Sifresiz kurulum - parola");
                if (pwd == null) { Logger.UI("Iptal edildi."); return; }
                string ini = Yetki.KurulumIniYaz(kul, pwd);
                Logger.UI("kurulum.ini yazildi: " + ini);
                Logger.UI("Artik bu USB'den kurulum yapilirken sifre sorulmaz. (Parola gizli yazildi.)");
                MessageBox.Show(
                    "Kaydedildi:\r\n" + ini + "\r\n\r\nBu USB'yi ogrenci bilgisayarina takip exe'yi calistirin; " +
                    "kurulum artik sifre sormadan otomatik yapilacak.\r\n\r\n" +
                    "Guvenlik: bu dosya parolayi (gizli) USB'de tutar; USB'yi yaninizda bulundurun.",
                    "Sifresiz kurulum ayarlandi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }));

            p.Controls.Add(new Label { Text = "Kaldirma", ForeColor = Color.OrangeRed, Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold), AutoSize = true, Margin = new Padding(6, 18, 0, 4) });
            var btnKaldir = Buton("Kurulumu tamamen kaldir (bu bilgisayardan)", async () =>
            {
                if (!YaziliOnayAl("KALDIR",
                        "Kurulum, tum gorevler ve oyun/VPN/YouTube/AI engelleri BU BILGISAYARDAN kaldirilacak.\r\n" +
                        "Bu islemi ONAYLAMAK icin asagidaki kutuya buyuk harfle KALDIR yazin:"))
                {
                    Logger.UI("Kaldirma iptal edildi (onay metni eslesmedi).");
                    return;
                }
                await Calistir("Kaldir", () =>
                {
                    // v49: Envanter Sheet'ine "KALDIRILDI" bildirimi -- SILMEDEN once (ProgramData
                    // zaten Kaldir() tarafindan dokunulmuyor ama sira onemli degil, MachineId her
                    // halukarda okunabilir durumda).
                    try { DriveLog.KaldirildiBildir(_ayar.MachineId); } catch { }
                    Installer.Kaldir();
                    Logger.UI("Kurulum kaldirildi.");
                });
            });
            btnKaldir.BackColor = Color.FromArgb(197, 48, 48); // v104: acik temada okunakli olsun diye canli kirmizi
            btnKaldir.ForeColor = Color.White;
            p.Controls.Add(btnKaldir);

            tp.Controls.Add(p);
            return tp;
        }

        // =====================================================================================
        // 4) KORUMA (Oyun Kontrol'den yeniden adlandirildi) -- Oyun, YouTube, AI web sitesi
        //    (v55'te YENI ve YouTube'dan BAGIMSIZ), VPN, Kamera, Yapay zeka haftalik tarama.
        // =====================================================================================
        private Panel SekmeKoruma()
        {
            var tp = YeniSekme("Koruma");
            var p = AkisPanel();

            p.Controls.Add(new Label { Text = "Oyun", ForeColor = RenkVurgu, AutoSize = true, Margin = new Padding(6, 6, 0, 4) });
            p.Controls.Add(Buton("Oyun kayitlarini tara", async () => await Calistir("Oyun log kontrol", () =>
            {
                OyunLogKontrol.Tara(true);
                OyunKaldirici.KuruluOyunlariRaporla();
                Logger.UI("Oyun log kontrolu tamamlandi.");
            })));
            p.Controls.Add(Buton("Oyunlari kaldir ve engelle (simdi)", async () =>
            {
                await Calistir("Oyun temizle-kaldir / engelle", () =>
                {
                    GameControl.SurecKapat();
                    GameControl.CalismaEngelle();
                    OyunLogKontrol.Tara(true);
                    OyunKaldirici.OtomatikHedefOyunlariKaldir();
                    // v57 hata duzeltmesi: burada 10 (dk) ile sabit kodlu gorev kurulumu,
                    // TaskHelper.KurulumYap'in kurdugu v57 birlesik 5 dk'lik gorevi SESSIZCE 10
                    // dk'ya GERI ALIYORDU -- bu dugmeye her basildiginda denetim periyodu yanlislikla
                    // eski degere donuyordu. Artik gercek (5 dk) periyotla ayni.
                    _ayar.OyunEngelDk = 5;
                    TaskHelper.OyunEngelGoreviKur(5, false, false);
                    Logger.UI("Oyun kontrolu tamamlandi.");
                });
            }));
            // "Oyun engelini kaldir" dugmesi KALDIRILDI: kalici degildi, 10 dk gorev geri kuruyordu
            // (kafa karistiriyordu). Oyun engellemeyi KALICI kapatmak icin TEK yer: Yerel Ayarlar
            // -> "Oyun/site engelleme" kutucugunu kaldir -> "Ayarlari kaydet ve uygula".
            p.Controls.Add(Buton("Kac oyun su anda engelleniyor, goster", async () => await Calistir("Durum", () =>
                Logger.UI("Calisma engeli olan oyun sayisi: " + GameControl.EngelliSayisi()))));

            p.Controls.Add(new Label { Text = "YouTube (surekli acik/kapali -- v57: ders saati ayrimi kaldirildi)", ForeColor = RenkVurgu, AutoSize = true, Margin = new Padding(6, 14, 0, 4) });
            p.Controls.Add(Buton("YouTube'u engelle (surekli)", async () => await Calistir("YouTube kisit", () =>
            {
                _ayar.YoutubeKisitli = true; _ayar.Save();
                YoutubeKisit.SaatiUygula();         // su anki duruma gore hemen uygula
                Logger.UI("YouTube engeli aktif (surekli, moladan bagimsiz). Kisit acikken Opera otomatik kapatilir.");
            })));
            p.Controls.Add(Buton("YouTube kisitlamasini kaldir", async () => await Calistir("YouTube kisit kaldir", () =>
            {
                _ayar.YoutubeKisitli = false; _ayar.Save();   // bayrak kapali -> gorev calissa bile engellemez
                YoutubeKisit.KaldirEngel();
                Logger.UI("YouTube kisitlamasi kaldirildi; her zaman acik. (Tarayici aciksa bir kez kapat-ac.)");
            })));

            // v55: AI web sitesi engeli -- BILEREK YouTube'dan AYRI bir bolum. Ders saatine
            // gore DEGIL, surekli acik/kapali. Bu, YapayZeka.cs'teki HAFTALIK ARKA PLAN
            // TARAMASIYLA (asagida, "Yapay zeka" basligi altinda) KARISTIRILMAMALIDIR -- o,
            // bilinmeyen oyun/program tespiti icin calisan tamamen AYRI bir ozelliktir.
            p.Controls.Add(new Label { Text = "AI web siteleri (ChatGPT, Gemini, Claude.ai vb. -- ders saatinden BAGIMSIZ)", ForeColor = RenkVurgu, AutoSize = true, Margin = new Padding(6, 14, 0, 4) });
            p.Controls.Add(new Label
            {
                Text = "OGRENCI kurulumunda varsayilan: ENGELLI. Bu makinede tek basina acip\r\n" +
                       "kapatmak icin asagidakini kullanin. (Tum filoyu etkileyen zorunlu kural\r\n" +
                       "icin: Filo Yonetimi sekmesi.)",
                ForeColor = Color.Gray, AutoSize = true, Margin = new Padding(6, 0, 0, 4)
            });
            p.Controls.Add(Buton("AI web sitelerini bu bilgisayarda ENGELLE", async () => await Calistir("AI site engeli", () =>
            {
                _ayar.AiSiteEngelle = true; _ayar.Save();
                // v55: TaskHelper'daki (v57: 5 dk'lik birlesik) gorev zaten AiSiteEngelle bayragina bakarak
                // kurulu kaliyor (bkz. Core/TaskHelper.cs) -- burada ek gorev kurmaya gerek yok,
                // sadece hemen uygulamak yeterli.
                AiSiteEngelle.Engelle();
                Logger.UI("AI web siteleri bu bilgisayarda engellendi.");
            })));
            p.Controls.Add(Buton("AI web sitesi engelini KALDIR (serbest)", async () => await Calistir("AI site engeli kaldir", () =>
            {
                _ayar.AiSiteEngelle = false; _ayar.Save();
                AiSiteEngelle.KaldirEngel();
                Logger.UI("AI web sitesi engeli kaldirildi; serbest. (Tarayici aciksa bir kez kapat-ac.)");
            })));

            p.Controls.Add(new Label { Text = "VPN", ForeColor = RenkVurgu, AutoSize = true, Margin = new Padding(6, 14, 0, 4) });
            p.Controls.Add(Buton("VPN'i tamamen engelle (tum turleri)", async () => await Calistir("VPN engelle", () =>
            {
                _ayar.VpnEngelle = true; _ayar.Save();
                int k = VpnKontrol.KapatCalisanlar();
                VpnKontrol.Engelle();
                Logger.UI("VPN devre disi: bilinen VPN programlari engellendi + siteleri kapatildi (kapatilan: " + k + ").");
            })));
            p.Controls.Add(Buton("VPN engelini kaldir", async () => await Calistir("VPN engel kaldir", () =>
            {
                _ayar.VpnEngelle = false; _ayar.Save();
                VpnKontrol.KaldirEngel();
                Logger.UI("VPN engeli kaldirildi.");
            })));

            // v109: AppLocker -- sadece OGRENCI makinesinde ve sadece UB_Ogrenci grubuna uygulanir.
            p.Controls.Add(new Label { Text = "Program calistirma kilidi (AppLocker -- sadece ogrenci hesaplari, ogrenci makinesinde)", ForeColor = RenkVurgu, AutoSize = true, Margin = new Padding(6, 14, 0, 4) });
            p.Controls.Add(Buton("AppLocker durumu ve kayitlari", async () => await Calistir("AppLocker durum", () =>
            {
                if (RolGuvenlik.OgretmenMakinesi(_ayar)) { Logger.UI("Bu bir OGRETMEN makinesi -- AppLocker uygulanmiyor."); return; }
                Logger.UI("AppLocker modu: " + AppLockerYonet.ModMetni(_ayar));
                string o = AppLockerYonet.SonOlaylar(24, 80);
                Logger.UI(string.IsNullOrWhiteSpace(o) ? "Son 24 saatte AppLocker kaydi yok." : "Son 24 saat (DENETIM = zorunlu modda ENGELLENECEKTI):\r\n" + o);
            })));
            p.Controls.Add(Buton("AppLocker: DENETIM modu", async () => await Calistir("AppLocker denetim", () =>
            {
                _ayar.AppLockerZorunlu = false; _ayar.Save();
                AppLockerYonet.Uygula(_ayar, true);
                Logger.UI("AppLocker DENETIM modunda: ogrenci programlari calisir, sadece kaydedilir.");
            })));
            p.Controls.Add(Buton("AppLocker: ZORUNLU mod", async () =>
            {
                if (RolGuvenlik.OgretmenMakinesi(_ayar)) { MessageBox.Show("Bu bir OGRETMEN makinesi -- AppLocker uygulanmaz.", "AppLocker"); return; }
                string onay = Microsoft.VisualBasic.Interaction.InputBox(
                    "ZORUNLU modda ogrenci hesaplari; Masaustu, Indirilenler, AppData, Temp ve USB'den program/kurulum CALISTIRAMAZ.\r\n\r\n" +
                    "Once birkac gun DENETIM kayitlarina bakip ders yazilimlarinin (Python, derleyiciler vb.) engellenmeyecegini dogrulayin.\r\n\r\n" +
                    "Devam etmek icin ZORUNLU yazin:", "AppLocker - Zorunlu mod", "");
                if (onay != "ZORUNLU") return;
                await Calistir("AppLocker zorunlu", () =>
                {
                    _ayar.AppLockerZorunlu = true; _ayar.Save();
                    AppLockerYonet.Uygula(_ayar, true);
                    Logger.UI("AppLocker ZORUNLU modda. Sorun olursa 'DENETIM moduna al' ile hemen geri alin.");
                });
            }));

            // Kamera: yalnizca bu bilgisayarda kamera aygiti VARSA ac/kapa gosterilir.
            if (KameraKontrol.VarMi())
            {
                p.Controls.Add(new Label { Text = "Kamera", ForeColor = RenkVurgu, AutoSize = true, Margin = new Padding(6, 14, 0, 4) });
                p.Controls.Add(Buton("Kamerayi KAPAT", async () => await Calistir("Kamera kapat", () =>
                {
                    KameraKontrol.Kapat();
                    _ayar.KameraKapali = true; _ayar.Save();
                    Logger.UI(KameraKontrol.Durum());
                })));
                p.Controls.Add(Buton("Kamerayi AC", async () => await Calistir("Kamera ac", () =>
                {
                    KameraKontrol.Ac();
                    _ayar.KameraKapali = false; _ayar.Save();
                    Logger.UI(KameraKontrol.Durum());
                })));
            }

            // v114 (Ali Bey'in istegi, 17 Kasim 2026): bu bolum PANELDEN GIZLENDI -- okul agi
            // (FATIH/icerik filtresi) api.anthropic.com'u cozemiyor/engelliyor, bu yuzden haftalik AI
            // taramasi hicbir zaman gercek anlamda calismadi (log: "AI baglanti (soket) hatasi:
            // api.anthropic.com'a ulasilamiyor ... NameResolutionFailure"). Ozellik Ali Bey'in istegiyle
            // SADECE DEVRE DISI birakildi (kod SILINMEDI, ileride ag izni acilirsa geri donebilir).
            // Fiili olarak KAPATMAK icin: Filo Yonetimi sekmesinden "AI haftalik kontrolu icin filo
            // kurali ayarla" + "ZORUNLU: AI haftalik kontrolu acik" KAPALI, Hedef=TUMU, gonder --
            // bu, FiloPolitika uzerinden tum filoya yayilir ve her makine AI_Haftalik gorevini
            // schtasks'tan otomatik KALDIRIR (TaskHelper.AiHaftalikGoreviKaldir). Asagidaki iki satir
            // (anahtar girme + durum gosterme dugmeleri) o yuzden artik eklenmiyor:
            //
            // p.Controls.Add(new Label { Text = "Yapay zeka - haftalik tarama (bilinmeyen oyun/program tespiti; ac/kapa: Yerel Ayarlar)", ForeColor = RenkVurgu, AutoSize = true, Margin = new Padding(6, 14, 0, 4) });
            // p.Controls.Add(Buton("Yapay zeka anahtarini gir", async () =>
            // {
            //     await System.Threading.Tasks.Task.CompletedTask;
            //     AiAnahtarAyarla();
            // }));
            // p.Controls.Add(Buton("Yapay zeka durumunu goster", async () => await Calistir("AI durumu", () =>
            //     Logger.UI(YapayZeka.Durum(_ayar.AiHaftalikAktif, TaskHelper.AiHaftalikGoreviKuruluMu(), _ayar.AiGun, _ayar.AiSaat)))));
            // v69 (Ali Bey'in istegi, 5 Kasim 2026): "Yapay zekayi hemen calistir (deneme)" dugmesi
            // KALDIRILDI -- gercek Claude API cagrisi yapan bu dugme panelde dururken yanlislikla
            // (merak/test amacli) tiklanip gereksiz API maliyeti olusturabilirdi. Haftalik otomatik
            // gorev (artik gun/saati ayarlanabilir -- Yerel Ayarlar / Filo Yonetimi) zaten yeterli.

            tp.Controls.Add(p);
            return tp;
        }

        private void AiAnahtarAyarla()
        {
            string k = Microsoft.VisualBasic.Interaction.InputBox(
                "Claude API anahtarinizi yapistirin (sk-ant-...).\r\n" +
                "Yalniz bu makinede sifreli saklanir; ogrenci kurulumuna gitmez.",
                "AI anahtari", "");
            if (string.IsNullOrWhiteSpace(k)) return;
            try
            {
                YapayZeka.AnahtarKaydet(k);
                Logger.UI("AI anahtari kaydedildi (sifreli).");
                MessageBox.Show("AI anahtari kaydedildi. Yerel Ayarlar sekmesindeki secili gun/saatte\r\nhaftalik olarak otomatik calisacak.",
                    "AI", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { Logger.UI("AI anahtari kaydedilemedi: " + ex.Message); }
        }

        // Engellenen oyunlar listesini ayri bir pencerede gosterir (tarih-saat / oyun / kategori / yontem).
        // v55 duzeltme: Filo-geneli oyun raporu (3 sutun: tarih saat | oyun adi | bilgisayar),
        // bu bilgisayarin yerel oyun raporuyla (4 sutun: tarih saat | oyun | kategori | yontem)
        // AYNI pencereyi paylasiyor ama sutunlari FARKLI -- sabit kodlu baslik satiri yanlis
        // olurdu. kolonBaslik verilmezse (null) eski 4 sutunlu varsayilan baslik kullanilir.
        private void ListeGoster(string baslik, System.Collections.Generic.List<string> satirlar, string kolonBaslik = null)
        {
            var f = new Form
            {
                Text = baslik,
                Width = 780,
                Height = 540,
                StartPosition = FormStartPosition.CenterParent,
                BackColor = RenkBg,
                ForeColor = RenkFg
            };
            var box = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                Dock = DockStyle.Fill,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                BackColor = Color.White,
                ForeColor = RenkVurgu,
                Font = new Font("Consolas", 9.5f),
                BorderStyle = BorderStyle.None
            };
            string head = kolonBaslik ?? "TARIH        SAAT       |  OYUN                          |  KATEGORI  |  YONTEM";
            if (satirlar == null || satirlar.Count == 0)
                box.Text = "Kayit yok.\r\n\r\n(Henuz engellenen veya kaldirilan oyun yok. Bir oyun " +
                           "yakalandiginda / kaldirildiginda burada tarih-saat, ad, kategori ve yontemle gorunur.)";
            else
                box.Text = head + "\r\n" + new string('-', 80) + "\r\n" +
                           string.Join("\r\n", satirlar) + "\r\n\r\nToplam: " + satirlar.Count + " kayit.";
            f.Controls.Add(box);
            f.ShowDialog(this);
        }

        // v105: Super Kullanici kilidi icin kucuk bir sifre sorma penceresi -- ListeGoster ile
        // AYNI "basit ozel Form" deseni. Iptal edilirse veya pencere kapatilirsa null doner (cagiran
        // taraf bunu "hicbir sey yapma" olarak yorumlar); OK ile kapatilirsa yazilan metni (bos da
        // olabilir) doner.
        private string SuperSifreSor()
        {
            string sonuc = null;
            using (var f = new Form
            {
                Text = "Super Kullanici",
                Width = 380,
                Height = 170,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                StartPosition = FormStartPosition.CenterParent,
                BackColor = RenkBg,
                ForeColor = RenkFg
            })
            {
                var lbl = new Label { Text = "Super Kullanici sifresi:", AutoSize = true, Location = new Point(16, 18), ForeColor = RenkFg };
                var txt = new TextBox { Location = new Point(16, 44), Width = 320, UseSystemPasswordChar = true, BackColor = Color.White, ForeColor = RenkFg };
                var btnTamam = new Button { Text = "Tamam", Location = new Point(166, 86), Width = 80, DialogResult = DialogResult.OK };
                var btnIptal = new Button { Text = "Iptal", Location = new Point(256, 86), Width = 80, DialogResult = DialogResult.Cancel };
                f.Controls.Add(lbl); f.Controls.Add(txt); f.Controls.Add(btnTamam); f.Controls.Add(btnIptal);
                f.AcceptButton = btnTamam;
                f.CancelButton = btnIptal;
                if (f.ShowDialog(this) == DialogResult.OK) sonuc = txt.Text;
            }
            return sonuc;
        }

        // =====================================================================================
        // 5) YEREL AYARLAR (Ayarlar'dan yeniden adlandirildi; Acil Durum Filo Yonetimi'ne tasindi)
        // =====================================================================================
        private Panel SekmeYerelAyarlar()
        {
            var tp = YeniSekme("Yerel Ayarlar");
            var p = AkisPanel();
            var cbEkran = OnayKutu("Ekran/uyku hic kapanmasin", _ayar.EkranHepAcik, v => _ayar.EkranHepAcik = v);
            var cbCop = OnayKutu("Cop kutusu tamamen kapali (silinen dosya direkt silinir)", _ayar.CopKutusuKapat, v => _ayar.CopKutusuKapat = v);
            var cbGorsel = OnayKutu("Gorsel efektleri kapat, bilgisayari hizlandir (tum kullanicilar)", _ayar.GorselPerformans, v => _ayar.GorselPerformans = v);
            var cbPlan = OnayKutu("Yuksek performans guc plani", _ayar.YuksekPerformansPlan, v => _ayar.YuksekPerformansPlan = v);
            // v101 (Ali Bey'in acik talebi, 11 Kasim 2026: "oyun engeli ogrenci makinasinda filodan
            // kaldirilmamali, menu sadece ogretmen makinasinda acik, tek ogrenci yuklemesinde gomulu
            // olsun"): Oyun Engelleme kutusu artik SADECE MakineTuru="ogretmen" olan kurulumlarda
            // gosteriliyor. Ogrenci kurulumunda bu kutu hic YOK -- OyunEngelle degeri kurulumda
            // (TaskHelper.KurulumYap / Installer profil uygulamasi) varsayilan ACIK olarak sabitlenir
            // ve yerel ayarlar ekranindan da degistirilemez (gomulu). Ogretmen kendi makinesinde
            // (Yerel Ayarlar'dan, filodan DEGIL) bu kisitlamayi gerekirse kendisi kaldirabilir --
            // bu zaten onceden var olan "ogretmen oyun oynayabilir" karariyla birebir uyumlu.
            bool oyunKutusuGoster = string.Equals(_ayar.MakineTuru, "ogretmen", StringComparison.OrdinalIgnoreCase);
            var cbOyun = oyunKutusuGoster
                ? OnayKutu("Oyun ve zararli siteleri otomatik engelle (sessiz, arka planda)", _ayar.OyunEngelle, v => _ayar.OyunEngelle = v)
                : null;
            // v51: VPN ve YouTube de artik burada (digerleriyle AYNI "coklu secim" ekraninda) elle
            // ac/kapa edilebiliyor -- onceden sadece Koruma sekmesindeki ayri dugmelerle
            // degistirilebiliyordu. O dugmeler AYNEN kaliyor (hizli, tek-tiklik); buradaki kutular
            // "Ayarlari kaydet" ile BIRLIKTE, tum diger ayarlarla ayni anda da degistirilebilsin diye.
            var cbVpn = OnayKutu("VPN/Hotspot engelle (tum turler)", _ayar.VpnEngelle, v => _ayar.VpnEngelle = v);
            // v57: Ders saati/teneffus ayrimi kaldirildi -- YouTube artik Oyun/VPN/AI gibi
            // surekli acik/kapali, moladan bagimsiz (hoca ne secerse o kalir, otomatik gecis yok).
            var cbYoutube = OnayKutu("YouTube'u engelle (surekli, moladan bagimsiz)", _ayar.YoutubeKisitli, v => _ayar.YoutubeKisitli = v);
            // v55: AI web sitesi engeli -- BILEREK YoutubeKisitli'den AYRI kutucuk (ders saatine
            // gore degil, surekli acik/kapali). Varsayilan KAPALI (serbest).
            var cbAiSite = OnayKutu("AI web sitelerini (ChatGPT, Gemini, Claude.ai vb.) engelle (surekli, ders saatine bagli degil)", _ayar.AiSiteEngelle, v => _ayar.AiSiteEngelle = v);
            var cbAiHaftalik = OnayKutu("Yapay zeka ile haftalik otomatik kontrol (asagidaki gun/saatte)", _ayar.AiHaftalikAktif, v => _ayar.AiHaftalikAktif = v);
            var cbOtoGuncelleme = OnayKutu("Internetten otomatik guncelleme (saatlik kontrol, sessiz, menu gerekmez)", _ayar.OtoGuncellemeAktif, v => _ayar.OtoGuncellemeAktif = v);
            p.Controls.Add(cbEkran);
            p.Controls.Add(cbCop);
            p.Controls.Add(cbGorsel);
            p.Controls.Add(cbPlan);
            if (cbOyun != null) p.Controls.Add(cbOyun);
            else p.Controls.Add(new Label
            {
                Text = "Oyun engelleme: bu makinede gomulu olarak ACIK (ogrenci kurulumunda degistirilemez).",
                ForeColor = Color.Gray, AutoSize = true, Margin = new Padding(6, 4, 0, 4)
            });
            p.Controls.Add(cbVpn);
            p.Controls.Add(cbYoutube);
            p.Controls.Add(cbAiSite);
            p.Controls.Add(cbAiHaftalik);
            // v69 (Ali Bey'in istegi): AI haftalik kontrolunun gun/saati de Agir Bakim gibi
            // secilebilir -- "acik" KAPALI ise gun/saat kutulari gizlenir (gereksiz, kafa
            // karistirici gorunmesin diye); "acik" isaretlenince gosterilir.
            var aiZamanPaneli = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false, Margin = new Padding(24, 0, 0, 2), Visible = _ayar.AiHaftalikAktif };
            var cboAiGun = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130, BackColor = Color.White, ForeColor = RenkFg };
            cboAiGun.Items.AddRange(new object[] { "Pazartesi", "Sali", "Carsamba", "Persembe", "Cuma", "Cumartesi", "Pazar" });
            int aiGunIndex = cboAiGun.Items.IndexOf(_ayar.AiGun);
            cboAiGun.SelectedIndex = aiGunIndex >= 0 ? aiGunIndex : 0; // varsayilan Pazartesi
            var txtAiSaat = new TextBox { Width = 70, Text = string.IsNullOrWhiteSpace(_ayar.AiSaat) ? "09:00" : _ayar.AiSaat, BackColor = Color.White, ForeColor = RenkFg };
            aiZamanPaneli.Controls.Add(new Label { Text = "Gun/saat:", ForeColor = RenkFg, AutoSize = true, Margin = new Padding(0, 6, 2, 0) });
            aiZamanPaneli.Controls.Add(cboAiGun);
            aiZamanPaneli.Controls.Add(new Label { Text = "  Saat (SS:dd):", ForeColor = RenkFg, AutoSize = true, Margin = new Padding(6, 6, 2, 0) });
            aiZamanPaneli.Controls.Add(txtAiSaat);
            cbAiHaftalik.CheckedChanged += (s, e) => aiZamanPaneli.Visible = cbAiHaftalik.Checked;
            p.Controls.Add(aiZamanPaneli);
            p.Controls.Add(cbOtoGuncelleme);

            // v57: Agir Bakim gun/saati -- onceden sadece kodda sabit (Cuma 12:00), hic UI'si
            // yoktu. Artik burada degistirilebilir; Filo Yonetimi'nden zorlanmazsa bu yerel
            // deger gecerli olur.
            p.Controls.Add(new Label { Text = "Agir bakim (haftalik, bilgisayar BOSTAYKEN calisir, 6 saate kadar surebilir)", ForeColor = RenkVurgu, AutoSize = true, Margin = new Padding(6, 14, 0, 4) });
            var agirPaneli = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false, Margin = new Padding(6, 0, 0, 4) };
            var cboAgirGun = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130, BackColor = Color.White, ForeColor = RenkFg };
            cboAgirGun.Items.AddRange(new object[] { "Pazartesi", "Sali", "Carsamba", "Persembe", "Cuma", "Cumartesi", "Pazar" });
            int gunIndex = cboAgirGun.Items.IndexOf(_ayar.AgirGun);
            cboAgirGun.SelectedIndex = gunIndex >= 0 ? gunIndex : 4; // varsayilan Cuma
            var txtAgirSaat = new TextBox { Width = 70, Text = string.IsNullOrWhiteSpace(_ayar.AgirSaat) ? "12:00" : _ayar.AgirSaat, BackColor = Color.White, ForeColor = RenkFg };
            agirPaneli.Controls.Add(cboAgirGun);
            agirPaneli.Controls.Add(new Label { Text = "  Saat (SS:dd):", ForeColor = RenkFg, AutoSize = true, Margin = new Padding(6, 6, 2, 0) });
            agirPaneli.Controls.Add(txtAgirSaat);
            p.Controls.Add(agirPaneli);
            p.Controls.Add(new Label
            {
                Text = "Cuma zorunlu degildir, istediginiz gunu secebilirsiniz. Filo Yonetimi'nden\r\n" +
                       "zorunlu bir gun/saat gonderilmisse bu YEREL deger gecersiz kalir.",
                ForeColor = Color.Gray, AutoSize = true, Margin = new Padding(6, 0, 0, 4)
            });

            p.Controls.Add(Buton("Ayarlari kaydet ve uygula", async () =>
            {
                // v57 hata duzeltmesi: cboAgirGun/txtAgirSaat ONCEDEN Calistir(...) -- yani
                // Task.Run ile arka plan is parcacigi -- icinden okunuyordu. WinForms kontrollerini
                // UI is parcacigi disindan okumak tanimsiz/kararsiz davranisa yol acabilir; artik
                // degerler burada (UI is parcacigi) okunup yerel degiskenlere alinıyor, arka plan
                // islemine SADECE bu degiskenler gonderiliyor.
                string agirGunSecili = cboAgirGun.SelectedItem as string ?? "Cuma";
                string agirSaatSecili = string.IsNullOrWhiteSpace(txtAgirSaat.Text) ? "12:00" : txtAgirSaat.Text.Trim();
                string aiGunSecili = cboAiGun.SelectedItem as string ?? "Pazartesi";
                string aiSaatSecili = string.IsNullOrWhiteSpace(txtAiSaat.Text) ? "09:00" : txtAiSaat.Text.Trim();
                await Calistir("Ayarlar", () =>
            {
                _ayar.AgirGun = agirGunSecili;
                _ayar.AgirSaat = agirSaatSecili;
                _ayar.AiGun = aiGunSecili;
                _ayar.AiSaat = aiSaatSecili;
                _ayar.Save();
                // Gorevler her zaman YEREL kurulu kopyaya baglanmali (Installer.Kur).
                string yer = Installer.Kur(_ayar);
                // v51: VPN/YouTube/Oyun/AI'yi de buradan, kaydetme ile BIRLIKTE hemen uygula. v55:
                // her biri icin filo-geneli zorunlu politika varsa (Filo Yonetimi) o GECERLI olur,
                // yoksa az once kaydedilen yerel ayar uygulanir -- OyunDenetimDongusuCalistir ile
                // AYNI kural, kaydet sonrasi beklemeden gorunsun diye.
                // v92: "Askiya Al" (filo-genelinde TUM kisitlamalari gecici durdurma) ozelligi Ali
                // Bey'in acik istegiyle TAMAMEN KALDIRILDI -- asagidaki her blok artik KOSULSUZ
                // (eskiden "askida degilse" diye sarti vardi) efektif ayari uygular.
                var filo = FiloPolitika.Durum(false);
                try
                {
                    // v101: Program.cs'teki OyunDenetimDongusuCalistir ile AYNI kural -- Oyun
                    // Engelleme artik filo politikasindan ETKILENMIYOR, sadece yerel ayar gecerli.
                    bool efektifOyun = _ayar.OyunEngelle;
                    if (efektifOyun) { GameControl.SurecKapat(); GameControl.CalismaEngelle(); }
                    else GameControl.EngelKaldir();
                }
                catch { }
                try
                {
                    bool efektifVpn = filo.Vpn ?? _ayar.VpnEngelle;
                    if (efektifVpn) VpnKontrol.Engelle(); else VpnKontrol.KaldirEngel();
                }
                catch { }
                try
                {
                    YoutubeKisit.SaatiUygula(filo.Youtube ?? _ayar.YoutubeKisitli);
                }
                catch { }
                try
                {
                    bool efektifAi = filo.AiSite ?? _ayar.AiSiteEngelle;
                    if (efektifAi) AiSiteEngelle.Engelle(); else AiSiteEngelle.KaldirEngel();
                }
                catch { }
                Logger.UI("Ayarlar kaydedildi ve gizli kurulum guncellendi: " + yer);
                Logger.UI(Environment.NewLine + TaskHelper.GorevDurumu());
            });
            }));

            tp.Controls.Add(p);
            return tp;
        }

        // =====================================================================================
        // 6) FILO YONETIMI (yeni, v55) -- filo-geneli ZORUNLU ayarlar + Kapat/Yeniden Baslat/
        //    Envanter Iste/Sheet Temizle komutlari (v92: "Acil Durum / Askiya Al" bolumu
        //    kaldirildi -- bkz. SekmeFiloYonetimi icindeki v92 notu).
        // =====================================================================================
        private Panel SekmeFiloYonetimi()
        {
            var tp = YeniSekme("Filo Yonetimi");
            var p = AkisPanel();

            // v92: "Acil Durum / Askiya Al" bolumu (filo-genelinde TUM kisitlamalari gecici
            // durdurma) Ali Bey'in acik istegiyle ("Tamamen kaldir") BURADAN TAMAMEN KALDIRILDI.
            // Hizli bir "fiili durumu gor" ihtiyaci icin asagidaki "Mevcut filo durumu" bolumu
            // (filo-geneli zorunlu ayarlarin KENDISI) zaten yeterli bilgiyi veriyor.

            // v55 (genisletildi): filo-geneli ZORUNLU ayarlar -- Yerel Ayarlar sekmesindeki
            // kutucuklarin BIREBIR AYNISI burada da var. Bir ayar icin "ZORUNLU" olarak isaretlenip
            // "Bu ayarlari TUM filoya uygula" ile gonderilirse, o ayar TUM okulda yerel ayarin
            // ONUNE GECER; "Filo zorlamalarini TAMAMEN kaldir" ile her makine kendi yerel ayarina
            // doner. Bir kutucuk isaretlenmemisse ve hic gonderilmemisse (ilk acilista), o ayar
            // icin zorlama YOKTUR -- asagidaki "Mevcut filo durumu" ile her zaman kontrol edilebilir.
            p.Controls.Add(new Label { Text = "Filo-geneli ZORUNLU ayarlar (Yerel Ayarlar ile AYNI secenekler)", ForeColor = RenkVurgu, Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold), AutoSize = true, Margin = new Padding(6, 22, 0, 4) });
            p.Controls.Add(new Label
            {
                Text = "Normalde her bilgisayar kendi Yerel Ayarlar/Koruma sekmesindeki ayarlarini\r\n" +
                       "kullanir. Asagidaki kutucuklari isaretleyip 'Bu ayarlari TUM filoya uygula'\r\n" +
                       "dedigin ayarlar, hedefteki makine(ler)de yerel ayarin ONUNE GECER. Isaretlemedigin\r\n" +
                       "bir ayar icin onceki filo zorlamasi (varsa) AYNEN kalir -- degistirmek icin o\r\n" +
                       "kutuyu da isaretleyip tekrar gonder. Yayilma suresi ~5 dakika.",
                ForeColor = Color.Gray, AutoSize = true, Margin = new Padding(6, 0, 0, 6)
            });

            // v57: Hedef -- bu push'un HANGI bilgisayar(lar)a uygulanacagi. "TUMU" = tum okul.
            // Bir lab adi (orn. "BMTAL-LAB1") = o lab'daki TUM bilgisayarlar (adi o on-ekle
            // baslayan herkes). Tam bir bilgisayar adi (orn. "BMTAL-LAB1-PC1") = SADECE o makine.
            // Esleme bilgisayar adinin bu metinle BASLAMASI ile yapilir (Apps Script tarafinda).
            // v57 (ikinci revizyon): Hedef artik SERBEST METIN degil -- ELLE YAZILMAZ, listeden
            // SECILIR. Liste: TUMU + BMTAL-LAB1..BMTAL-LAB10 (sabit on-tanimli lab adlari).
            // ComboBoxStyle.DropDown (tam DropDownList degil) birakildi ki, gerekirse Ali Bey tek
            // bir makineyi (orn. "BMTAL-LAB1-PC1") hedeflemek icin listedeki bir degeri secip
            // ustune yazarak duzenleyebilsin -- ama GUNLUK KULLANIMDA secim listeden yapilir,
            // bos bir kutuya elle yazmaya gerek YOKTUR.
            // v105 (Ali Bey'in istegi: "karisikligi onlemek icin hoca sadece kendi labi icin filo
            // islemi yapabilmeli... SUPER KULLANICI dogru sifreyi giren tum filoya islem yapabilsin"):
            // labGruplari burada SADECE yerel onbellekten (agKontrolEt=false) okunur -- panel
            // INSASI SIRASINDA (UI is parcacigini BLOKE ETMEMEK icin) hicbir ag istegi atilmaz.
            // Asagidaki "Lab Gruplari" bolumundeki "Yenile (Drive'dan)" dugmesi Drive'dan taze
            // ceker. Bu degisken SONRADAN (Yaz/Sil/Yenile sonrasi) YENIDEN ATANIR -- tum Filo
            // dugmelerinin kapatma kontrolleri (cboHedef okuyan her yerde) hep GUNCEL degeri gorur.
            var labGruplari = FiloLabGruplari.Listele(false);
            // v105 duzeltme (Ali Bey'in bulgusu: "lab grubundayim ama combobox'ta hepsi cikiyor,
            // sadece BMTAL-LAB2 cikmali, super userda ne varsa ve TUMU cikmali"): Hedef kutusunun
            // ICERIGI artik KILITLI/ACIK durumuna gore degisiyor -- eskiden her zaman TUMU + sabit
            // 10 lab GORUNUYORDU (sadece GONDERME aninda engelleniyordu); artik KILITLIYKEN kutuda
            // SADECE bu bilgisayarin kendi grubu (ve varsa onun altindaki makineler) var -- TUMU ve
            // diger laplar GORUNMUYOR BILE. SUPER KULLANICI acikken TUMU + Lab Gruplari'ndaki TUM
            // gruplar (ve varsa altlarindaki makineler) gorunur. Liste, HedefListesiniYenile ile
            // -- lock durumu her degistiginde (kilitDurumGuncelle icinden) ve "Makine listesini
            // yenile" ile -- yeniden kurulur; hangi gruplar oldugu artik sabit "BMTAL-LAB1..10"
            // DEGIL, Lab Gruplari'nda GERCEKTEN tanimli olanlardir.
            p.Controls.Add(new Label { Text = "Hedef (listeden sec)", ForeColor = RenkFg, AutoSize = true, Margin = new Padding(6, 4, 0, 2) });
            var cboHedef = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown, Width = 220, BackColor = Color.White, ForeColor = RenkFg, Margin = new Padding(6, 0, 0, 2) };
            // v57 (ikinci revizyon): "LAB1'i sec, bir daha her islemde tekrar secme" -- bu
            // bilgisayarda son secilen Hedef kalici olarak hatirlanir (AppSettings.FiloHedef);
            // panel her acildiginda OTOMATIK geri yuklenir. Her degisiklik aninda kaydedilir.
            cboHedef.TextChanged += (s, e) =>
            {
                string yeni = string.IsNullOrWhiteSpace(cboHedef.Text) ? "TUMU" : cboHedef.Text.Trim();
                if (yeni != _ayar.FiloHedef) { _ayar.FiloHedef = yeni; _ayar.Save(); }
            };
            // v91/v105: bir gruba ait makineleri ALTINA (girintili) listelemek icin -- "Makine
            // listesini yenile"ye basilinca Drive'dan cekilen GERCEK makine adlari burada tutulur;
            // kilit/grup degisince (hedefListesiniYenile tekrar cagrildiginda) ayni liste tekrar
            // kullanilir, her seferinde yeniden ag istegi atilmaz.
            List<string> sonMakineListesi = null;
            Func<string, string, bool> onekEslesir = (uyelerStr, makineAdi) =>
            {
                foreach (var onekHam in (uyelerStr ?? "").Split(','))
                {
                    var onek = onekHam.Trim();
                    if (onek.Length > 0 && makineAdi.StartsWith(onek, StringComparison.OrdinalIgnoreCase)) return true;
                }
                return false;
            };
            Action<string> hedefListesiniYenile = (tercihEdilen) =>
            {
                string secim = tercihEdilen ?? cboHedef.Text;
                cboHedef.Items.Clear();
                if (FiloKisitlama.SuperAcik)
                {
                    cboHedef.Items.Add("TUMU");
                    var grupAdlari = new List<string>(labGruplari.Keys);
                    grupAdlari.Sort(StringComparer.OrdinalIgnoreCase);
                    foreach (var grupAdi in grupAdlari)
                    {
                        cboHedef.Items.Add(grupAdi);
                        if (sonMakineListesi != null)
                            foreach (var m in sonMakineListesi)
                                if (onekEslesir(labGruplari[grupAdi], m)) cboHedef.Items.Add("    " + m);
                    }
                }
                else
                {
                    string kendiGrup = FiloKisitlama.KendiGrubunuBul(labGruplari);
                    if (kendiGrup != null)
                    {
                        cboHedef.Items.Add(kendiGrup);
                        if (sonMakineListesi != null && labGruplari.TryGetValue(kendiGrup, out var kendiUyeler))
                            foreach (var m in sonMakineListesi)
                                if (onekEslesir(kendiUyeler, m)) cboHedef.Items.Add("    " + m);
                    }
                    else
                    {
                        cboHedef.Items.Add("(kilitli -- Super Kullanici gerekir)");
                    }
                }
                if (!string.IsNullOrEmpty(secim) && cboHedef.Items.Contains(secim)) cboHedef.Text = secim;
                else if (cboHedef.Items.Count > 0) cboHedef.SelectedIndex = 0;
            };
            // v68 (Ali Bey'in istegi, 5 Kasim 2026): v63..v68 arasinda eklenen "lab secilince
            // alt makineleri otomatik listele" ozelligi (FiloEnvanter.cs + DropDown/
            // SelectedIndexChanged yuklemesi) Ali Bey'in ACIK istegiyle TAMAMEN GERI ALINDI --
            // kutu v57 (ikinci revizyon) HALINE donduruldu.
            // v91 (Ali Bey'in istegi, 9 Kasim 2026: "lab secilince altinda [TUMU'nun altinda]
            // o lab'daki OGR'ler listelenmeli, kullanimi kolay olsun"): v68'in kaldirdigi
            // ozellik, DAHA BASIT bir tasarimla GERI GETIRILDI -- her secimde ayri bir ag
            // istegi atmak yerine, TEK bir "Yenile" dugmesiyle Drive'daki Envanter'den TUM
            // makine listesi bir kerede cekilir ve her lab'in ALTINA kendi makineleri eklenir
            // (FiloMakineListesi.cs). Panel acildiginda otomatik CEKILMEZ (gereksiz ag istegi
            // olmasin) -- Ali Bey istedikce "Yenile"ye basar.
            // v105: Hedef kutusunun yanina kucuk bir Super Kullanici kilidi (Ali Bey'in istegi:
            // "Hedef kutusunun yanina kucuk bir 'Super Kullanici' kilidi/dugmesi eksin"). Kilitliyken
            // (varsayilan, HER program acilisinda) bu bilgisayar SADECE kendi Lab Grubuna Filo islemi
            // yapabilir; dogru sifre girilince (program kapanana kadar) tum filoya acilir. Tekrar
            // tiklayinca sifresiz ELLE DE kilitlenebilir.
            var hedefSatiri = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false, Margin = new Padding(0) };
            hedefSatiri.Controls.Add(cboHedef);
            var btnSuperKilit = new Button { Text = "\U0001F512", Width = 34, Height = 23, Margin = new Padding(6, 0, 0, 2), BackColor = Color.White, ForeColor = RenkFg, FlatStyle = FlatStyle.Flat };
            btnSuperKilit.FlatAppearance.BorderColor = RenkKenar;
            hedefSatiri.Controls.Add(btnSuperKilit);
            p.Controls.Add(hedefSatiri);
            var lblKilitDurum = new Label { Text = "", AutoSize = true, Margin = new Padding(6, 2, 0, 6) };
            p.Controls.Add(lblKilitDurum);
            Action kilitDurumGuncelle = () =>
            {
                if (FiloKisitlama.SuperAcik)
                {
                    btnSuperKilit.Text = "\U0001F513";
                    lblKilitDurum.Text = "Super Kullanici ACIK -- bu program kapanana kadar TUM filoya islem yapabilirsiniz.";
                    lblKilitDurum.ForeColor = Color.FromArgb(197, 48, 48);
                }
                else
                {
                    string kendiGrup = FiloKisitlama.KendiGrubunuBul(labGruplari, out var celisenGruplar);
                    btnSuperKilit.Text = "\U0001F512";
                    if (kendiGrup != null)
                    {
                        lblKilitDurum.Text = "Kilitli -- bu bilgisayar (" + Environment.MachineName + ") sadece " + kendiGrup + " icin Filo islemi yapabilir.";
                        lblKilitDurum.ForeColor = Color.Gray;
                    }
                    else if (celisenGruplar != null && celisenGruplar.Count > 0)
                    {
                        // v105 duzeltme (Ali Bey'in bulgusu: ayni makine yanlislikla 2 ayri Lab
                        // Grubuna uye yazilinca hangisinin gecerli olacagi belirsizlesiyordu):
                        // artik rastgele biri SECILMIYOR -- acikca "cakisma" olarak gosterilip
                        // guvenlik amacli kilitli kaliniyor.
                        lblKilitDurum.Text = "Kilitli -- bu bilgisayar (" + Environment.MachineName + ") BIRDEN FAZLA grupta (" +
                                             string.Join(", ", celisenGruplar) + ") uye, hangisi gecerli BELIRSIZ -- Filo islemi YAPILAMAZ.";
                        lblKilitDurum.ForeColor = Color.FromArgb(214, 118, 28);
                    }
                    else
                    {
                        lblKilitDurum.Text = "Kilitli -- bu bilgisayar (" + Environment.MachineName + ") hicbir Lab Grubuna atanmamis, Filo islemi YAPILAMAZ.";
                        lblKilitDurum.ForeColor = Color.Gray;
                    }
                }
                // v105 duzeltme: kilit durumu her degistiginde Hedef kutusunun ICERIGI de
                // (sadece kendi grubu <-> TUMU+tum gruplar) otomatik yeniden kuruluyor.
                hedefListesiniYenile(null);
            };
            // Ilk acilista: once onceki oturumdan hatirlanan _ayar.FiloHedef tercih edilsin.
            hedefListesiniYenile(_ayar.FiloHedef);
            kilitDurumGuncelle();
            btnSuperKilit.Click += (s, e) =>
            {
                if (FiloKisitlama.SuperAcik)
                {
                    FiloKisitlama.SuperAcik = false;
                    kilitDurumGuncelle();
                    Logger.UI("Super Kullanici kilidi tekrar kapatildi.");
                    return;
                }
                string girilen = SuperSifreSor();
                if (girilen == null) return; // iptal edildi
                if (girilen == Musteri.SuperKullaniciSifre)
                {
                    FiloKisitlama.SuperAcik = true;
                    kilitDurumGuncelle();
                    Logger.UI("Super Kullanici kilidi acildi -- program kapanana kadar tum filoya islem yapilabilir.");
                }
                else
                {
                    Logger.UI("Super Kullanici sifresi hatali -- kilit acilmadi.");
                }
            };
            var btnHedefYenile = Buton("Makine listesini yenile (Drive'dan)", async () =>
            {
                List<string> makineler;
                try { makineler = await System.Threading.Tasks.Task.Run(() => FiloMakineListesi.Listele()); }
                catch (Exception ex) { Logger.UI("Makine listesi alinamadi: " + ex.Message); return; }

                // v105 duzeltme: artik sabit "BMTAL-LAB1..10" varsayimiyla DEGIL, GERCEK Lab
                // Gruplari verisine (ve kilit durumuna) gore yeniden kuruluyor.
                sonMakineListesi = makineler;
                hedefListesiniYenile(null);
                Logger.UI("Makine listesi guncellendi (" + makineler.Count + " kayitli makine). Hedef kutusundan kendi grubunuzun (veya Super Kullanici acikken herhangi bir grubun) altindaki bir makineyi secebilirsiniz.");
            });
            p.Controls.Add(btnHedefYenile);
            p.Controls.Add(new Label
            {
                Text = "TUMU = herkes | BMTAL-LAB1..LAB10 = o lab'daki herkes. 'Makine listesini yenile'ye\r\n" +
                       "bastiginizda her lab'in altina o lab'daki bilgisayarlar eklenir -- listeden TEK BIR\r\n" +
                       "makine secebilirsiniz. (Yenilemeden once de tam bilgisayar adini elle yazabilirsiniz.)\r\n" +
                       "Secim BU BILGISAYARDA kalici kalir -- panel her acildiginda son secileni hatirlar.",
                ForeColor = Color.Gray, AutoSize = true, Margin = new Padding(6, 0, 0, 6)
            });

            // v105 (Ali Bey'in istegi: "Menuden bu gruplari lazim oldukca silme, ekleme guncelleme
            // ve girildiginde var olanlar gorulebilir"): Lab Gruplari -- grup adi -> uye bilgisayar-
            // adi on-ekleri. TUM filoda paylasimli (Drive uzerinden); yukaridaki kilit/durum bu
            // listeye bakarak "bu bilgisayar hangi grupta" sorusunu cevaplar.
            p.Controls.Add(new Label { Text = "Lab Gruplari (tum filoda paylasimli)", ForeColor = RenkVurgu, Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold), AutoSize = true, Margin = new Padding(6, 18, 0, 4) });
            p.Controls.Add(new Label
            {
                Text = "Her ogretmen bilgisayari SADECE kendi grubuna Filo islemi yapabilir (yukaridaki kilit\r\n" +
                       "acik degilse). Buradaki ekleme/silme/guncelleme TUM filoda birkac dakika icinde gecerli\r\n" +
                       "olur. Uyeler: o gruba ait bilgisayar-adi ON-EKLERI, virgulle ayrilmis\r\n" +
                       "(orn. BMTAL-LAB1,BMTAL-LAB1-ORT1) -- bir bilgisayarin adi bu on-eklerden BIRIYLE\r\n" +
                       "BASLIYORSA o makine bu gruba ait sayilir. Uyeler BOS birakilirsa, Grup Adi'nin\r\n" +
                       "KENDISI on-ek sayilir (coğu zaman -- orn. tum BMTAL-LAB2-... bilgisayarlari icin --\r\n" +
                       "Grup Adi'ni yazip Uyeler'i bos birakmak yeterlidir, ayni seyi iki kere yazmaya gerek yok).",
                ForeColor = Color.Gray, AutoSize = true, Margin = new Padding(6, 0, 0, 6)
            });
            var lstGruplar = new ListBox { Width = 480, Height = 40, BackColor = Color.White, ForeColor = RenkFg, Font = new Font("Consolas", 8.5f), Margin = new Padding(6, 0, 0, 6) };
            p.Controls.Add(lstGruplar);

            var grupGirisSatiri = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false, Margin = new Padding(6, 0, 0, 2) };
            grupGirisSatiri.Controls.Add(new Label { Text = "Grup adi:", AutoSize = true, ForeColor = RenkFg, Margin = new Padding(0, 6, 2, 0) });
            var txtGrupAdi = new TextBox { Width = 140, BackColor = Color.White, ForeColor = RenkFg, Margin = new Padding(0, 2, 10, 2) };
            grupGirisSatiri.Controls.Add(txtGrupAdi);
            grupGirisSatiri.Controls.Add(new Label { Text = "Uyeler (virgulle):", AutoSize = true, ForeColor = RenkFg, Margin = new Padding(0, 6, 2, 0) });
            var txtUyeler = new TextBox { Width = 240, BackColor = Color.White, ForeColor = RenkFg, Margin = new Padding(0, 2, 0, 2) };
            grupGirisSatiri.Controls.Add(txtUyeler);
            p.Controls.Add(grupGirisSatiri);

            Action<bool> gruplariListeyeYansit = (yenilendi) =>
            {
                lstGruplar.Items.Clear();
                foreach (var kv in labGruplari) lstGruplar.Items.Add(kv.Key + "  =  " + kv.Value);
                if (lstGruplar.Items.Count == 0) lstGruplar.Items.Add("(henuz hic Lab Grubu tanimlanmamis)");
                // v105 duzeltme (Ali Bey'in geri bildirimi: liste kutusu sabit 110px oldugu icin
                // sadece 1-2 grup varken altinda bos alan kaliyordu): kutu artik GRUP SAYISINA
                // gore boyutlaniyor (1-7 grup arasi tam sigacak kadar; daha fazlasi icin kutu
                // 7 satirda sabitlenir, geri kalani ListBox'in kendi kaydirma cubuguyla gezilir).
                int satirAdedi = Math.Max(1, Math.Min(lstGruplar.Items.Count, 7));
                lstGruplar.Height = satirAdedi * lstGruplar.ItemHeight + 6;
                kilitDurumGuncelle();
            };
            gruplariListeyeYansit(false);
            lstGruplar.SelectedIndexChanged += (s, e) =>
            {
                if (lstGruplar.SelectedItem == null) return;
                string secim = lstGruplar.SelectedItem.ToString();
                int ix = secim.IndexOf("  =  ", StringComparison.Ordinal);
                if (ix < 0) return;
                txtGrupAdi.Text = secim.Substring(0, ix);
                txtUyeler.Text = secim.Substring(ix + 5);
            };

            var grupDugmeSatiri = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false, Margin = new Padding(6, 4, 0, 14) };
            grupDugmeSatiri.Controls.Add(Buton("Ekle / Guncelle", async () =>
            {
                string grupAdi = txtGrupAdi.Text.Trim();
                string uyeler = txtUyeler.Text.Trim();
                if (grupAdi.Length == 0) { Logger.UI("Grup adi bos olamaz."); return; }
                // v105 duzeltme (Ali Bey'in bulgusu: "Grup Adi BMTAL-LAB2, Uye Adi BMTAL-LAB2 --
                // iyi dusun", yani ayni degeri IKI KERE yazmak gereksiz/sacma): cogu zaman lab
                // adinin KENDISI zaten bilgisayarlarin ortak on-eki oluyor (orn. hepsi
                // "BMTAL-LAB2-..." diye adlandirilmis). Bu yuzden Uyeler BOS birakilirsa artik
                // otomatik olarak Grup Adi'nin KENDISI uye on-eki sayilir -- farkli/coklu bir
                // on-ek GEREKEN ozel durumlarda yine Uyeler alanina elle yazilabilir.
                if (uyeler.Length == 0) uyeler = grupAdi;

                // v105 duzeltme (Ali Bey'in bulgusu -- ekran goruntusunde isaretledi: ayni makine
                // adi yanlislikla 2 ayri gruba (BMTAL-LAB1 ve BMTAL-LAB2) uye yazilmisti, hangisinin
                // gecerli olacagi belirsizdi): kaydetmeden ONCE, girilen uyelerin BASKA bir grupla
                // (kendisi HARIC) CAKISIP cakismadigi kontrol edilir -- cakisma varsa acik bir uyari
                // verilir (yine de kaydedilir -- Ali Bey karar verir, ama artik SESSIZCE olmaz).
                var celisenGruplar = new List<string>();
                foreach (var kv in labGruplari)
                {
                    if (string.Equals(kv.Key, grupAdi, StringComparison.OrdinalIgnoreCase)) continue; // kendisi -- guncelleniyor
                    foreach (var mevcutOnekHam in (kv.Value ?? "").Split(','))
                    {
                        var mevcutOnek = mevcutOnekHam.Trim();
                        if (mevcutOnek.Length == 0) continue;
                        foreach (var yeniOnekHam in uyeler.Split(','))
                        {
                            var yeniOnek = yeniOnekHam.Trim();
                            if (yeniOnek.Length == 0) continue;
                            if (yeniOnek.Equals(mevcutOnek, StringComparison.OrdinalIgnoreCase) ||
                                yeniOnek.StartsWith(mevcutOnek, StringComparison.OrdinalIgnoreCase) ||
                                mevcutOnek.StartsWith(yeniOnek, StringComparison.OrdinalIgnoreCase))
                            {
                                if (!celisenGruplar.Contains(kv.Key)) celisenGruplar.Add(kv.Key);
                            }
                        }
                    }
                }

                string hataMesaji = null;
                await Calistir("Lab grubu yaz", () => { hataMesaji = FiloLabGruplari.Yaz(grupAdi, uyeler); });
                if (hataMesaji != null) { Logger.UI(hataMesaji); return; }
                labGruplari = FiloLabGruplari.Listele(false); // Yaz() onbellegi zaten tazeledi
                if (celisenGruplar.Count > 0)
                    Logger.UI("UYARI: '" + grupAdi + "' icindeki bazi uyeler, baska grup(lar)la (" + string.Join(", ", celisenGruplar) +
                               ") CAKISIYOR -- cakisan bilgisayar(lar) hangi gruba ait oldugu belirsiz olacagi icin guvenlik " +
                               "amacli KILITLI kalacak (Super Kullanici gerekir). Lutfen uye listelerini duzeltip tekrar kaydedin.");
                else
                    Logger.UI("Lab grubu kaydedildi: " + grupAdi + " = " + uyeler);
                gruplariListeyeYansit(false);
            }));
            grupDugmeSatiri.Controls.Add(Buton("Sil", async () =>
            {
                string grupAdi = txtGrupAdi.Text.Trim();
                if (grupAdi.Length == 0) { Logger.UI("Silmek icin once listeden bir grup secin veya grup adini yazin."); return; }
                string hataMesaji = null;
                await Calistir("Lab grubu sil", () => { hataMesaji = FiloLabGruplari.Sil(grupAdi); });
                if (hataMesaji != null) { Logger.UI(hataMesaji); return; }
                labGruplari = FiloLabGruplari.Listele(false); // Sil() onbellegi zaten tazeledi
                Logger.UI("Lab grubu silindi: " + grupAdi);
                gruplariListeyeYansit(false);
            }));
            grupDugmeSatiri.Controls.Add(Buton("Yenile (Drive'dan)", async () =>
            {
                await Calistir("Lab gruplari yenile", () => { labGruplari = FiloLabGruplari.Listele(true); });
                gruplariListeyeYansit(true);
                Logger.UI("Lab gruplari yenilendi (" + labGruplari.Count + " grup).");
            }));
            p.Controls.Add(grupDugmeSatiri);

            var filoBaslangic = FiloPolitika.Durum(false);
            // v55 duzeltme 2: Ali Bey'in acik istegi -- panel ilk acildiginda (veya zaten bir
            // filo kurali varsa onu gosterirken) HEM "filo kurali ayarla" kutusu HEM deger kutusu
            // ZATEN ISARETLI gelsin ki "Bu ayarlari TUM filoya uygula" dugmesine TEK TIKLA tum
            // kisitlamalar (Oyun/VPN/YouTube/AI) tum okulda ZORUNLU AKTIF olsun -- her kutuyu
            // tek tek isaretlemek GEREKMESIN. "Ayarla" kutusu HER ZAMAN varsayilan ISARETLI
            // (zaten bir kural varsa da, yoksa da); deger kutusu mevcut filo degeri varsa ONU,
            // yoksa GUVENLI/BEKLENEN varsayilani (Oyun/VPN/YouTube/AI hepsi ACIK/engelli) gosterir.
            // Istemeyen ogretmen ilgili kutuyu kendisi kaldirir.
            // v101 (Ali Bey'in acik talebi, 11 Kasim 2026): Oyun Engelleme artik filodan acik/kapali
            // YAPILAMIYOR -- her ogrenci makinesinde gomulu/sabit ACIK (bkz. Yerel Ayarlar ve
            // Program.cs/OyunDenetimDongusuCalistir). Eski "filo kurali ayarla" kutulari kaldirildi;
            // yanlislikla filodan kapatilma riski tamamen ortadan kalksin diye BILEREK kontrol yok.
            p.Controls.Add(new Label
            {
                Text = "Oyun engelleme: artik filodan ayarlanamaz -- tum ogrenci makinelerinde gomulu/sabit ACIK.",
                ForeColor = Color.Gray, AutoSize = true, Margin = new Padding(6, 10, 0, 4)
            });
            var cbFiloVpnZorla = new CheckBox { Text = "VPN/Hotspot engeli icin filo kurali ayarla", Checked = true, AutoSize = true, ForeColor = RenkFg, Margin = new Padding(6, 10, 4, 0) };
            var cbFiloVpn = OnayKutu("  -> ZORUNLU: VPN/Hotspot engelle (tum turler)", filoBaslangic.Vpn ?? true, v => { });
            // v57: Ders saati ayrimi kaldirildi -- YouTube de Oyun/VPN/AI gibi surekli acik/kapali.
            var cbFiloYoutubeZorla = new CheckBox { Text = "YouTube engeli icin filo kurali ayarla", Checked = true, AutoSize = true, ForeColor = RenkFg, Margin = new Padding(6, 10, 4, 0) };
            var cbFiloYoutube = OnayKutu("  -> ZORUNLU: YouTube'u engelle (surekli)", filoBaslangic.Youtube ?? true, v => { });
            var cbFiloAiZorla = new CheckBox { Text = "AI web sitesi engeli icin filo kurali ayarla", Checked = true, AutoSize = true, ForeColor = RenkFg, Margin = new Padding(6, 10, 4, 0) };
            var cbFiloAi = OnayKutu("  -> ZORUNLU: AI web sitelerini engelle", filoBaslangic.AiSite ?? true, v => { });

            p.Controls.Add(cbFiloVpnZorla); p.Controls.Add(cbFiloVpn);
            p.Controls.Add(cbFiloYoutubeZorla); p.Controls.Add(cbFiloYoutube);
            p.Controls.Add(cbFiloAiZorla); p.Controls.Add(cbFiloAi);

            // v57: Agir Bakim gun/saati de filo'dan zorlanabilir -- isaretlenmezse (varsayilan)
            // her makine kendi Yerel Ayarlar'daki gun/saatini kullanir.
            var cbFiloAgirZorla = new CheckBox { Text = "Agir bakim gun/saati icin filo kurali ayarla", Checked = false, AutoSize = true, ForeColor = RenkFg, Margin = new Padding(6, 10, 4, 0) };
            var agirFiloPaneli = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false, Margin = new Padding(24, 2, 0, 4) };
            var cboFiloAgirGun = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130, BackColor = Color.White, ForeColor = RenkFg };
            cboFiloAgirGun.Items.AddRange(new object[] { "Pazartesi", "Sali", "Carsamba", "Persembe", "Cuma", "Cumartesi", "Pazar" });
            // v57 hata duzeltmesi: filoBaslangic.AgirGun, filo kurali hic ayarlanmamissa (normal/
            // beklenen durum) NULL olur -- ComboBox.Items.IndexOf(null) bir ArgumentNullException
            // firlatiyordu ve MainForm hic acilmadan (hicbir mesaj gostermeden) programi kapatiyordu.
            // Boyle bir makinede panel ASLA acilamiyordu. Simdi null/bos guvenli: varsayilan (Cuma) kullanilir.
            int filoGunIndex = string.IsNullOrWhiteSpace(filoBaslangic.AgirGun)
                ? -1 : cboFiloAgirGun.Items.IndexOf(filoBaslangic.AgirGun);
            cboFiloAgirGun.SelectedIndex = filoGunIndex >= 0 ? filoGunIndex : 4;
            var txtFiloAgirSaat = new TextBox { Width = 70, Text = string.IsNullOrWhiteSpace(filoBaslangic.AgirSaat) ? "12:00" : filoBaslangic.AgirSaat, BackColor = Color.White, ForeColor = RenkFg };
            agirFiloPaneli.Controls.Add(cboFiloAgirGun);
            agirFiloPaneli.Controls.Add(new Label { Text = "  Saat (SS:dd):", ForeColor = RenkFg, AutoSize = true, Margin = new Padding(6, 6, 2, 0) });
            agirFiloPaneli.Controls.Add(txtFiloAgirSaat);
            p.Controls.Add(cbFiloAgirZorla);
            p.Controls.Add(agirFiloPaneli);

            // v69 (Ali Bey'in istegi, 5 Kasim 2026): "FILO AI API acik/kapali, guncelleme gun/
            // saati NERDE" sorusuna yanit -- Oyun/VPN/YouTube/AI-site ile AYNI "filo kurali ayarla"
            // deseni AI haftalik kontrolune de eklendi. Ali Bey'in acik istegiyle: gun/saat kutulari
            // SADECE "ZORUNLU AI kontrolu ACIK" secildiginde gorunur (Agir Bakim'da hep gorunur
            // kalirken burada farkli -- Ali Bey boyle istedi).
            var cbFiloAiHaftalikZorla = new CheckBox { Text = "AI haftalik kontrolu icin filo kurali ayarla", Checked = false, AutoSize = true, ForeColor = RenkFg, Margin = new Padding(6, 10, 4, 0) };
            var cbFiloAiHaftalik = OnayKutu("  -> ZORUNLU: AI haftalik kontrolu acik", filoBaslangic.AiKontrol ?? true, v => { });
            cbFiloAiHaftalik.Margin = new Padding(24, 2, 4, 0);
            var aiZamanFiloPaneli = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false, Margin = new Padding(24, 2, 0, 4), Visible = cbFiloAiHaftalik.Checked };
            var cboFiloAiGun = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130, BackColor = Color.White, ForeColor = RenkFg };
            cboFiloAiGun.Items.AddRange(new object[] { "Pazartesi", "Sali", "Carsamba", "Persembe", "Cuma", "Cumartesi", "Pazar" });
            int filoAiGunIndex = string.IsNullOrWhiteSpace(filoBaslangic.AiGun)
                ? -1 : cboFiloAiGun.Items.IndexOf(filoBaslangic.AiGun);
            cboFiloAiGun.SelectedIndex = filoAiGunIndex >= 0 ? filoAiGunIndex : 0; // varsayilan Pazartesi
            var txtFiloAiSaat = new TextBox { Width = 70, Text = string.IsNullOrWhiteSpace(filoBaslangic.AiSaat) ? "09:00" : filoBaslangic.AiSaat, BackColor = Color.White, ForeColor = RenkFg };
            aiZamanFiloPaneli.Controls.Add(new Label { Text = "Gun/saat:", ForeColor = RenkFg, AutoSize = true, Margin = new Padding(0, 6, 2, 0) });
            aiZamanFiloPaneli.Controls.Add(cboFiloAiGun);
            aiZamanFiloPaneli.Controls.Add(new Label { Text = "  Saat (SS:dd):", ForeColor = RenkFg, AutoSize = true, Margin = new Padding(6, 6, 2, 0) });
            aiZamanFiloPaneli.Controls.Add(txtFiloAiSaat);
            // Gun/saat kutulari SADECE "ayarla" VE "acik" ikisi de isaretliyken anlamli -- ikisi de
            // isaretliyse goster, degilse (kapali zorlanacaksa veya zorlama hic yoksa) gizle.
            Action aiZamanGorunurlukGuncelle = () => aiZamanFiloPaneli.Visible = cbFiloAiHaftalikZorla.Checked && cbFiloAiHaftalik.Checked;
            cbFiloAiHaftalikZorla.CheckedChanged += (s, e) => aiZamanGorunurlukGuncelle();
            cbFiloAiHaftalik.CheckedChanged += (s, e) => aiZamanGorunurlukGuncelle();
            aiZamanGorunurlukGuncelle();
            // v113 (Ali Bey'in istegi, 17 Kasim 2026): Oyun-AI (bilinmeyen oyun/program tespiti)
            // ozelligi hic calismadigi icin TAMAMEN devre disi birakildi (schtasks kaldirildi,
            // Koruma sekmesindeki butonlar gizlendi -- bkz. SekmeKoruma()). Bu panelde kalan
            // "AI haftalik kontrolu" filo-kurali kutulari artik KAFA KARISTIRIYOR: Ali Bey zaten
            // Politika sayfasinda AiHaftalik=KAPALI, Hedef=TUMU olarak ayarladi (fonksiyonel olarak
            // tamamen kapali). Degiskenler (cbFiloAiHaftalikZorla, cbFiloAiHaftalik,
            // aiZamanFiloPaneli) ve aşağıdaki "Uygula" blogundaki aiKontrol/aiGun/aiSaat hesabı
            // BILEREK SILINMEDI -- Checked hep false kaldigi icin o blok her zaman mevcut Sheet
            // degerini (filoBaslangic.AiKontrol/AiGun/AiSaat) oldugu gibi korur, hicbir sey
            // degistirmez. Ileride "AI Sorun Teshisi" (Apps Script uzerinden, Sheet tabanli arıza
            // yorumlama) ozelligi gelince bu alan o amacla yeniden kullanilabilir.
            // p.Controls.Add(cbFiloAiHaftalikZorla);
            // p.Controls.Add(cbFiloAiHaftalik);
            // p.Controls.Add(aiZamanFiloPaneli);

            p.Controls.Add(Buton("Bu ayarlari TUM filoya uygula", async () =>
            {
                // v57 hata duzeltmesi: asagidaki CheckBox/ComboBox/TextBox degerleri ONCEDEN
                // Calistir(...) -- yani arka plan is parcacigi -- icinden okunuyordu. WinForms
                // kontrollerini UI is parcacigi disindan okumak tanimsiz/kararsiz davranisa yol
                // acabilir; artik hepsi burada (UI is parcacigi) okunup yerel degiskenlere
                // alinıyor, arka plan islemine sadece bu degiskenler gonderiliyor.
                //
                // v55 duzeltme: "Ayarla" kutusu isaretli DEGILSE, o alan icin ONCEKI filo kurali
                // (panel acildiginda okunan filoBaslangic -- varsa deger, yoksa null/zorlama-yok)
                // AYNEN tekrar gonderilir. ONCEDEN bu durumda null gonderiliyordu; Apps Script HER
                // yazmada sutunlari komple yeniden yazdigi icin bu, isaretlenmemis alanin varsa
                // MEVCUT kuralini SESSIZCE SILIYORDU -- panelin ustundeki aciklama metniyle
                // ("isaretlenmedigin ayarin onceki kurali aynen kalir") CELISIYORDU. Simdi gercekten
                // oyle calisiyor: isaretli -> yeni secilen deger; isaretli degil -> degismeden kalir.
                // v101: Oyun artik filodan hic gonderilmiyor (her zaman null -- "zorlama yok",
                // zaten etkisi de yok, Sheet'teki sutun karisikligini da onlemek icin temiz null).
                bool? oyun = null;
                bool? vpn = cbFiloVpnZorla.Checked ? (bool?)cbFiloVpn.Checked : filoBaslangic.Vpn;
                bool? youtube = cbFiloYoutubeZorla.Checked ? (bool?)cbFiloYoutube.Checked : filoBaslangic.Youtube;
                bool? aiSite = cbFiloAiZorla.Checked ? (bool?)cbFiloAi.Checked : filoBaslangic.AiSite;
                string agirGun = cbFiloAgirZorla.Checked ? (cboFiloAgirGun.SelectedItem as string) : filoBaslangic.AgirGun;
                string agirSaat = cbFiloAgirZorla.Checked ? txtFiloAgirSaat.Text.Trim() : filoBaslangic.AgirSaat;
                bool? aiKontrol = cbFiloAiHaftalikZorla.Checked ? (bool?)cbFiloAiHaftalik.Checked : filoBaslangic.AiKontrol;
                // Gun/saat SADECE "ayarla" isaretli VE "acik" seciliyse gonderilir (kapali
                // zorlanacaksa gun/saatin bir anlami yok -- onceki deger aynen korunur).
                string aiGun = (cbFiloAiHaftalikZorla.Checked && cbFiloAiHaftalik.Checked) ? (cboFiloAiGun.SelectedItem as string) : filoBaslangic.AiGun;
                string aiSaat = (cbFiloAiHaftalikZorla.Checked && cbFiloAiHaftalik.Checked) ? txtFiloAiSaat.Text.Trim() : filoBaslangic.AiSaat;
                string hedef = string.IsNullOrWhiteSpace(cboHedef.Text) ? "TUMU" : cboHedef.Text.Trim();
                // v105: Lab Gruplari kisitlamasi -- Super Kullanici acik degilse, bu bilgisayar
                // SADECE kendi grubuna islem gonderebilir. Izinli() false donerse hicbir Filo
                // komutu GONDERILMEZ (asagidaki tum kod atlanir).
                if (!FiloKisitlama.Izinli(hedef, labGruplari, out var kisitHata)) { Logger.UI(kisitHata); return; }
                await Calistir("Filo ayarlari - uygula", () =>
                {
                    Logger.UI(FiloPolitika.Uygula(oyun, vpn, youtube, aiSite, agirGun, agirSaat, hedef, aiKontrol, aiGun, aiSaat));
                    Logger.UI("Hedef: " + hedef + (hedef.Equals("TUMU", StringComparison.OrdinalIgnoreCase) ? " (tum okul)" : " (sadece bu on-ekle baslayan bilgisayar(lar))"));
                });
            }));
            p.Controls.Add(Buton("Filo zorlamalarini TAMAMEN kaldir (hepsi yerel ayara donsun)", async () => await Calistir("Filo ayarlari - kaldir", () =>
                Logger.UI(FiloPolitika.TumunuKaldir()))));
            p.Controls.Add(Buton("Mevcut filo durumunu kontrol et (Drive'dan, simdi)", async () => await Calistir("Filo ayarlari - durum", () =>
            {
                var d = FiloPolitika.Durum(true);
                string S(bool? v) => v == null ? "yok (yerel ayar gecerli)" : ("ZORUNLU " + (v.Value ? "ACIK" : "KAPALI"));
                string agirS = (string.IsNullOrWhiteSpace(d.AgirGun) && string.IsNullOrWhiteSpace(d.AgirSaat)) ? "yok (yerel ayar gecerli)" : ("ZORUNLU " + d.AgirGun + " " + d.AgirSaat);
                string aiS = S(d.AiKontrol) + ((string.IsNullOrWhiteSpace(d.AiGun) && string.IsNullOrWhiteSpace(d.AiSaat)) ? "" : (" (" + d.AiGun + " " + d.AiSaat + ")"));
                Logger.UI("Filo durumu -- Oyun: " + S(d.Oyun) + " | VPN: " + S(d.Vpn) + " | YouTube: " + S(d.Youtube) + " | AI sitesi: " + S(d.AiSite) + " | Agir bakim: " + agirS + " | AI haftalik: " + aiS);
            })));

            // v57.2 (Ali Bey'in talebi, 3 Kasim 2026): "Sinav Modu" terminolojisi VE bu
            // bilgisayarda yerel/sureli baslatma tamamen KALDIRILDI -- hepsi FILODAN yapilsin
            // istendi. Asagidaki uc blok (Internet, AI, YouTube) hepsi SADECE Filo Yonetimi'nden,
            // yukaridaki "Hedef" kutusuyla (TUMU / lab / tek bilgisayar) calisir; hiçbiri sureyle
            // sinirli degildir -- hoca ELLE "Ac/Kaldir" demeden kapanmaz. Alttaki motor
            // (InternetEngeli.cs, dosya/log adlari "sinav_modu*") DEGISMEDI -- sadece panel
            // butonlari ve adlandirma sadelestirildi.
            p.Controls.Add(new Label { Text = "Internet Kapat / Ac -- FILODAN (yukaridaki Hedef kutusunu kullanir)", ForeColor = Color.OrangeRed, Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold), AutoSize = true, Margin = new Padding(6, 22, 0, 4) });
            p.Controls.Add(new Label
            {
                Text = "Hedefteki makine(ler)de TUM internet (kablolu+WiFi) kesilir/acilir. En gec ~5\r\n" +
                       "dakika icinde uygulanir. Guvenlik siniri: en fazla 24 saat sonra KENDILIGINDEN\r\n" +
                       "acilir (makine internetsiz kalinca komut da alamayacagi icin kalici kilitlenmesin\r\n" +
                       "diye); daha uzun surmesi gerekirse 'Internet Kapat'a tekrar basmaniz yeterli.",
                ForeColor = Color.Gray, AutoSize = true, Margin = new Padding(6, 0, 0, 6)
            });
            var btnInternetKapat = Buton("Internet Kapat", async () =>
            {
                string hedef = string.IsNullOrWhiteSpace(cboHedef.Text) ? "TUMU" : cboHedef.Text.Trim();
                // v105: Lab Gruplari kisitlamasi -- Super Kullanici acik degilse, bu bilgisayar
                // SADECE kendi grubuna islem gonderebilir. Izinli() false donerse hicbir Filo
                // komutu GONDERILMEZ (asagidaki tum kod atlanir).
                if (!FiloKisitlama.Izinli(hedef, labGruplari, out var kisitHata)) { Logger.UI(kisitHata); return; }
                await Calistir("Internet kapat", () => Logger.UI(InternetEngeli.FiloBaslatSuresiz(hedef)));
            });
            btnInternetKapat.BackColor = Color.FromArgb(214, 118, 28); btnInternetKapat.ForeColor = Color.White; // v104: acik temada okunakli turuncu
            p.Controls.Add(btnInternetKapat);
            p.Controls.Add(Buton("Internet Ac", async () =>
            {
                string hedef = string.IsNullOrWhiteSpace(cboHedef.Text) ? "TUMU" : cboHedef.Text.Trim();
                // v105: Lab Gruplari kisitlamasi -- Super Kullanici acik degilse, bu bilgisayar
                // SADECE kendi grubuna islem gonderebilir. Izinli() false donerse hicbir Filo
                // komutu GONDERILMEZ (asagidaki tum kod atlanir).
                if (!FiloKisitlama.Izinli(hedef, labGruplari, out var kisitHata)) { Logger.UI(kisitHata); return; }
                await Calistir("Internet ac", () => Logger.UI(InternetEngeli.FiloIptal(hedef)));
            }));

            // v57.2: AI ve YouTube icin de, genel "Bu ayarlari TUM filoya uygula" dugmesine
            // gerek kalmadan, TEK TIKLA Kapat/Ac -- diger ayarlarin (Oyun/VPN/karsi taraf) o
            // andaki filo durumuna DOKUNMAZ, sadece kendi alanini degistirir.
            p.Controls.Add(new Label { Text = "AI web sitesi Kapat / Ac -- FILODAN (yukaridaki Hedef kutusunu kullanir)", ForeColor = RenkFg, Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold), AutoSize = true, Margin = new Padding(6, 16, 0, 4) });
            p.Controls.Add(Buton("AI Kapat", async () =>
            {
                string hedef = string.IsNullOrWhiteSpace(cboHedef.Text) ? "TUMU" : cboHedef.Text.Trim();
                // v105: Lab Gruplari kisitlamasi -- Super Kullanici acik degilse, bu bilgisayar
                // SADECE kendi grubuna islem gonderebilir. Izinli() false donerse hicbir Filo
                // komutu GONDERILMEZ (asagidaki tum kod atlanir).
                if (!FiloKisitlama.Izinli(hedef, labGruplari, out var kisitHata)) { Logger.UI(kisitHata); return; }
                await Calistir("AI kapat", () =>
                {
                    var d = FiloPolitika.Durum(false);
                    Logger.UI(FiloPolitika.Uygula(d.Oyun, d.Vpn, d.Youtube, true, d.AgirGun, d.AgirSaat, hedef, d.AiKontrol, d.AiGun, d.AiSaat));
                });
            }));
            p.Controls.Add(Buton("AI Ac", async () =>
            {
                string hedef = string.IsNullOrWhiteSpace(cboHedef.Text) ? "TUMU" : cboHedef.Text.Trim();
                // v105: Lab Gruplari kisitlamasi -- Super Kullanici acik degilse, bu bilgisayar
                // SADECE kendi grubuna islem gonderebilir. Izinli() false donerse hicbir Filo
                // komutu GONDERILMEZ (asagidaki tum kod atlanir).
                if (!FiloKisitlama.Izinli(hedef, labGruplari, out var kisitHata)) { Logger.UI(kisitHata); return; }
                await Calistir("AI ac", () =>
                {
                    var d = FiloPolitika.Durum(false);
                    Logger.UI(FiloPolitika.Uygula(d.Oyun, d.Vpn, d.Youtube, false, d.AgirGun, d.AgirSaat, hedef, d.AiKontrol, d.AiGun, d.AiSaat));
                });
            }));

            p.Controls.Add(new Label { Text = "YouTube Kapat / Ac -- FILODAN (yukaridaki Hedef kutusunu kullanir)", ForeColor = RenkFg, Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold), AutoSize = true, Margin = new Padding(6, 16, 0, 4) });
            p.Controls.Add(new Label
            {
                Text = "Opera, YouTube ayarindan BAGIMSIZ olarak HER ZAMAN kapali tutulur (asagida ayrica belirtildi).",
                ForeColor = Color.Gray, AutoSize = true, Margin = new Padding(6, 0, 0, 6)
            });
            p.Controls.Add(Buton("YouTube Kapat", async () =>
            {
                string hedef = string.IsNullOrWhiteSpace(cboHedef.Text) ? "TUMU" : cboHedef.Text.Trim();
                // v105: Lab Gruplari kisitlamasi -- Super Kullanici acik degilse, bu bilgisayar
                // SADECE kendi grubuna islem gonderebilir. Izinli() false donerse hicbir Filo
                // komutu GONDERILMEZ (asagidaki tum kod atlanir).
                if (!FiloKisitlama.Izinli(hedef, labGruplari, out var kisitHata)) { Logger.UI(kisitHata); return; }
                await Calistir("YouTube kapat", () =>
                {
                    var d = FiloPolitika.Durum(false);
                    Logger.UI(FiloPolitika.Uygula(d.Oyun, d.Vpn, true, d.AiSite, d.AgirGun, d.AgirSaat, hedef, d.AiKontrol, d.AiGun, d.AiSaat));
                });
            }));
            p.Controls.Add(Buton("YouTube Ac", async () =>
            {
                string hedef = string.IsNullOrWhiteSpace(cboHedef.Text) ? "TUMU" : cboHedef.Text.Trim();
                // v105: Lab Gruplari kisitlamasi -- Super Kullanici acik degilse, bu bilgisayar
                // SADECE kendi grubuna islem gonderebilir. Izinli() false donerse hicbir Filo
                // komutu GONDERILMEZ (asagidaki tum kod atlanir).
                if (!FiloKisitlama.Izinli(hedef, labGruplari, out var kisitHata)) { Logger.UI(kisitHata); return; }
                await Calistir("YouTube ac", () =>
                {
                    var d = FiloPolitika.Durum(false);
                    Logger.UI(FiloPolitika.Uygula(d.Oyun, d.Vpn, false, d.AiSite, d.AgirGun, d.AgirSaat, hedef, d.AiKontrol, d.AiGun, d.AiSaat));
                });
            }));

            // v57 (ikinci revizyon): Filo'dan Kapat -- aksam ogrenciler bazi makineleri acik
            // unutmus olabilir; yukaridaki "Hedef" kutusundaki secimle (TUMU / bir lab / tek bir
            // makine) hedefteki makine(ler) en gec ~5 dk icinde (acikken/kullanimdaysa v96'dan
            // itibaren UYARISIZ/ANINDA) kapatilir. TEK SEFERLIK komuttur -- surekli bir "kapali
            // tut" kurali DEGILDIR; makine tekrar acilabilir, ayni komut icin bir daha kapanmaz.
            p.Controls.Add(new Label { Text = "Filo'dan Kapat -- hedefteki makine(ler)i uzaktan kapat (unutulan makineler icin)", ForeColor = Color.OrangeRed, Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold), AutoSize = true, Margin = new Padding(6, 22, 0, 4) });
            p.Controls.Add(new Label
            {
                Text = "Yukaridaki Hedef kutusundaki secimi kullanir. Hedefteki makine(ler) en gec ~5\r\n" +
                       "dakika icinde (o an acik ise) kapanir; kapali olan veya daha sonra acilan bir\r\n" +
                       "makine bu komuttan tekrar etkilenmez -- gerekirse yeniden 'Kapat' demeniz gerekir.",
                ForeColor = Color.Gray, AutoSize = true, Margin = new Padding(6, 0, 0, 6)
            });
            var btnFiloKapat = Buton("Hedefteki makine(ler)i FILODAN KAPAT", async () =>
            {
                string hedef = string.IsNullOrWhiteSpace(cboHedef.Text) ? "TUMU" : cboHedef.Text.Trim();
                // v105: Lab Gruplari kisitlamasi -- Super Kullanici acik degilse, bu bilgisayar
                // SADECE kendi grubuna islem gonderebilir. Izinli() false donerse hicbir Filo
                // komutu GONDERILMEZ (asagidaki tum kod atlanir).
                if (!FiloKisitlama.Izinli(hedef, labGruplari, out var kisitHata)) { Logger.UI(kisitHata); return; }
                var sonuc = MessageBox.Show(
                    "Hedef: " + hedef + "\r\n\r\nBu islem, hedefteki makine(ler)i en gec ~5 dakika icinde KAPATIR. Emin misiniz?",
                    "Filo'dan Kapat - Onay", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (sonuc != DialogResult.Yes) return;
                await Calistir("Filo'dan kapat", () => Logger.UI(FiloKapat.FiloBaslat(hedef)));
            });
            btnFiloKapat.BackColor = Color.FromArgb(197, 48, 48); btnFiloKapat.ForeColor = Color.White; // v104: acik temada okunakli kirmizi
            p.Controls.Add(btnFiloKapat);

            // v89 (Ali Bey'in istegi: "Filo'ya bir menu daha ekleyelim, hedefteki makineleri
            // filodan yeniden baslatalim"): Kapat ile BIREBIR AYNI desen/Hedef kutusu, sadece
            // kapatma yerine yeniden baslatma -- ayni ogretmen-muafiyeti kuralina tabi.
            p.Controls.Add(new Label { Text = "Filo'dan Yeniden Baslat -- hedefteki makine(ler)i uzaktan yeniden baslat", ForeColor = Color.OrangeRed, Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold), AutoSize = true, Margin = new Padding(6, 22, 0, 4) });
            p.Controls.Add(new Label
            {
                Text = "Yukaridaki Hedef kutusundaki secimi kullanir. Hedefteki makine(ler) en gec ~5\r\n" +
                       "dakika icinde (o an acik ise) yeniden baslar; kapali olan veya daha sonra acilan\r\n" +
                       "bir makine bu komuttan tekrar etkilenmez -- gerekirse yeniden 'Yeniden Baslat' demeniz gerekir.",
                ForeColor = Color.Gray, AutoSize = true, Margin = new Padding(6, 0, 0, 6)
            });
            var btnFiloYenidenBaslat = Buton("Hedefteki makine(ler)i FILODAN YENIDEN BASLAT", async () =>
            {
                string hedef = string.IsNullOrWhiteSpace(cboHedef.Text) ? "TUMU" : cboHedef.Text.Trim();
                // v105: Lab Gruplari kisitlamasi -- Super Kullanici acik degilse, bu bilgisayar
                // SADECE kendi grubuna islem gonderebilir. Izinli() false donerse hicbir Filo
                // komutu GONDERILMEZ (asagidaki tum kod atlanir).
                if (!FiloKisitlama.Izinli(hedef, labGruplari, out var kisitHata)) { Logger.UI(kisitHata); return; }
                var sonuc = MessageBox.Show(
                    "Hedef: " + hedef + "\r\n\r\nBu islem, hedefteki makine(ler)i en gec ~5 dakika icinde YENIDEN BASLATIR. Emin misiniz?",
                    "Filo'dan Yeniden Baslat - Onay", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (sonuc != DialogResult.Yes) return;
                await Calistir("Filo'dan yeniden baslat", () => Logger.UI(FiloYenidenBaslat.FiloBaslat(hedef)));
            });
            btnFiloYenidenBaslat.BackColor = Color.FromArgb(197, 48, 48); btnFiloYenidenBaslat.ForeColor = Color.White;
            p.Controls.Add(btnFiloYenidenBaslat);

            // v80 (Ali Bey'in istegi: "Filo menusune envanter gonder koyalim, calisan tum
            // makinelerin envanterini makinelerden istesin"): Kapat ile AYNI Hedef kutusunu
            // kullanir, AMA bir kisitlama DEGILDIR -- OGRETMEN makineleri de (filo zorlamasindan
            // muaf olsalar bile) bu komuta uyar, cunku sadece bilgi gonderiyor, hicbir ayari
            // degistirmiyor.
            p.Controls.Add(new Label { Text = "Envanter Gonder -- hedefteki makine(ler)in envanterini SIMDI iste", ForeColor = RenkVurgu, Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold), AutoSize = true, Margin = new Padding(6, 22, 0, 4) });
            p.Controls.Add(new Label
            {
                Text = "Yukaridaki Hedef kutusundaki secimi kullanir. Hedefteki makine(ler) en gec ~5\r\n" +
                       "dakika icinde (acik ve agdaysa) kendi envanterini (CPU/RAM/disk/Windows/lisans/\r\n" +
                       "SonGorulme) merkeze gonderir -- normal otomatik gonderimi beklemeden.",
                ForeColor = Color.Gray, AutoSize = true, Margin = new Padding(6, 0, 0, 6)
            });
            var btnEnvanterIste = Buton("Hedefteki makine(ler)in envanterini SIMDI iste", async () =>
            {
                string hedef = string.IsNullOrWhiteSpace(cboHedef.Text) ? "TUMU" : cboHedef.Text.Trim();
                // v105: Lab Gruplari kisitlamasi -- Super Kullanici acik degilse, bu bilgisayar
                // SADECE kendi grubuna islem gonderebilir. Izinli() false donerse hicbir Filo
                // komutu GONDERILMEZ (asagidaki tum kod atlanir).
                if (!FiloKisitlama.Izinli(hedef, labGruplari, out var kisitHata)) { Logger.UI(kisitHata); return; }
                await Calistir("Filo envanter iste", () => Logger.UI(EnvanterIste.FiloBaslat(hedef)));
            });
            p.Controls.Add(btnEnvanterIste);

            // v110: TESHIS -- sabit listeden SALT-OKUNUR sorgular; sonuc Sheet'teki 'Teshis' sayfasina.
            p.Controls.Add(new Label { Text = "Teshis -- hedefteki makine(ler)den bilgi topla (Wi-Fi, ag, aygit...)", ForeColor = RenkVurgu, Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold), AutoSize = true, Margin = new Padding(6, 22, 0, 4) });
            p.Controls.Add(new Label
            {
                Text = "Yukaridaki Hedef kutusunu kullanir. Sadece asagidaki SABIT, salt-okunur sorgular calisir\r\n" +
                       "(makinede hicbir sey degismez). Sonuc ~5-10 dk icinde Sheet'teki 'Teshis' sayfasina\r\n" +
                       "yazilir (makine basina tek satir). Kapali makineler acildiklarinda calistirir.",
                ForeColor = Color.Gray, AutoSize = true, Margin = new Padding(6, 0, 0, 6)
            });
            var teshisKutular = new System.Collections.Generic.List<CheckBox>();
            var teshisPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = new Padding(6, 0, 0, 2) };
            foreach (var kv in Teshis.Liste)
            {
                var cb = new CheckBox { Text = kv.Value, Tag = kv.Key, AutoSize = true, ForeColor = RenkFg, Checked = kv.Key == "wifi_ag" || kv.Key == "ag" };
                teshisKutular.Add(cb); teshisPanel.Controls.Add(cb);
            }
            p.Controls.Add(teshisPanel);
            p.Controls.Add(ButonVurgu("\U0001F50D  Hedefe TESHIS istegi gonder", async () =>
            {
                string hedef = string.IsNullOrWhiteSpace(cboHedef.Text) ? "TUMU" : cboHedef.Text.Trim();
                if (!FiloKisitlama.Izinli(hedef, labGruplari, out var kisitHata)) { Logger.UI(kisitHata); return; }
                var secili = teshisKutular.Where(c => c.Checked).Select(c => (string)c.Tag).ToList();
                await Calistir("Filo teshis", () => Logger.UI(Teshis.FiloBaslat(hedef, secili)));
            }));

            // v111: PILOT GUNCELLEME bolumu KALDIRILDI (Ali Bey: kafa karisikligi, GitHub duzeninde
            // gereksiz -- yeni exe'yi yayinlamadan once kendi makinenizde deneyebilirsiniz).

            // v110: ACIL KORUMA ANAHTARI -- uzaktan AppLocker + klasor kilitlerini geri al.
            p.Controls.Add(new Label { Text = "ACIL: Ogrenci korumasini uzaktan kapat / ac", ForeColor = Color.OrangeRed, Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold), AutoSize = true, Margin = new Padding(6, 22, 0, 4) });
            p.Controls.Add(new Label
            {
                Text = "Bir ders programi ogrenci makinesinde acilmiyorsa (AppLocker / klasor kilidi yuzunden)\r\n" +
                       "okula gitmeden hedefte bu korumalari geri alir. Oyun/VPN/site engelleri ve sinav modu\r\n" +
                       "ETKILENMEZ. Durum kalicidir: 'normale dondur' denene kadar acik kalir.",
                ForeColor = Color.Gray, AutoSize = true, Margin = new Padding(6, 0, 0, 6)
            });
            var korumaSatir = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false, Margin = new Padding(6, 0, 0, 2) };
            var btnKorumaKapat = ButonVurgu("⚠  Korumayi uzaktan KAPAT (hedef)", async () =>
            {
                string hedef = string.IsNullOrWhiteSpace(cboHedef.Text) ? "TUMU" : cboHedef.Text.Trim();
                if (!FiloKisitlama.Izinli("TUMU", labGruplari, out var kisitHata)) { Logger.UI("Bu anahtar tum filo kaydini degistirir; Super Kullanici gerekir.\r\n" + kisitHata); return; }
                if (MessageBox.Show("Hedef: " + hedef + "\r\n\r\nOgrenci makinelerinde AppLocker ve klasor kilitleri GERI ALINACAK.\r\nDevam edilsin mi?",
                        "Koruma acil kapatma", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                await Calistir("Koruma kapat", () => Logger.UI(FiloV110.KorumaAyarla(hedef, true)));
            }, Color.FromArgb(197, 48, 48));
            korumaSatir.Controls.Add(btnKorumaKapat);
            korumaSatir.Controls.Add(Buton("Korumayi normale dondur", async () =>
            {
                if (!FiloKisitlama.Izinli("TUMU", labGruplari, out var kisitHata)) { Logger.UI("Bu anahtar tum filo kaydini degistirir; Super Kullanici gerekir.\r\n" + kisitHata); return; }
                await Calistir("Koruma normal", () => Logger.UI(FiloV110.KorumaAyarla("TUMU", false)));
            }));
            p.Controls.Add(korumaSatir);

            // v94 (Ali Bey'in istegi: "Filo'dan dosya gonderebilir miyiz, FTP gibi"): gercek bir
            // FTP sunucusu YOK -- Ali Bey'in kendi onerisi: Drive'da 6 SABIT dosya (yukle.pdf/txt/
            // jpg/docx/exe/zip), gonderilecek dosya TURUNE uyan sabitin UZERINE "surum olarak"
            // yuklenir (guncelleme.zip ile AYNI mantik), panelden tur/hedef klasor/dosya adi secilir.
            p.Controls.Add(new Label { Text = "Dosya Gonder", ForeColor = Color.OrangeRed, Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold), AutoSize = true, Margin = new Padding(6, 22, 0, 4) });
            p.Controls.Add(new Label
            {
                Text = "Once Drive'da yukle.pdf/txt/jpg/docx/exe/zip adli SABIT dosyalardan turune uyanin\r\n" +
                       "UZERINE (silip yeniden DEGIL, 'surum olarak') gondermek istedigin dosyayi yukle.\r\n" +
                       "Sonra asagidan turu sec, hedef klasor ve kaydedilecek dosya adini yaz, Gonder'e bas.\r\n" +
                       "Hedefteki makine(ler) dosyayi Drive'dan dogrudan indirir (yukaridaki Hedef kutusu\r\n" +
                       "burada da gecerlidir). EXE dosyalari SADECE indirilir, OTOMATIK CALISTIRILMAZ.",
                ForeColor = Color.Gray, AutoSize = true, Margin = new Padding(6, 0, 0, 6)
            });
            var pDosya = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false, Margin = new Padding(6, 0, 0, 2) };
            var cboDosyaTur = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90, BackColor = Color.White, ForeColor = RenkFg };
            cboDosyaTur.Items.AddRange(new object[] { "pdf", "txt", "jpg", "docx", "exe", "zip" });
            cboDosyaTur.SelectedIndex = 0;
            pDosya.Controls.Add(new Label { Text = "Tur:", AutoSize = true, Margin = new Padding(0, 8, 4, 0) });
            pDosya.Controls.Add(cboDosyaTur);
            // v104 (Ali Bey'in istegi: "dosya gonderince makinalarin neresine gidiyor, masa ustu olmali"):
            // varsayilan hedef "C:\UNSAL_DOSYA" idi (gizli/ozel bir klasor, bulmasi zor). Simdi varsayilan
            // "Herkese Acik Masaustu" (C:\Users\Public\Desktop) -- bu klasore SYSTEM tarafindan (5 dk'lik
            // /oyun-engelle gorevinden) yazilan dosyalar, HANGI kullanici oturum acarsa acsin otomatik
            // olarak O KULLANICININ GORDUGU masaustunde de belirir. Dogrudan Environment.GetFolderPath(
            // SpecialFolder.Desktop) KULLANILMADI -- cunku indirme islemi SYSTEM/Oturum 0 baglaminda
            // calisiyor (bkz. duvar kagidi hatasi/duzeltmesi) ve bu, gercek kullanicinin DEGIL SYSTEM'in
            // kendi (gorunmez) masaustunu dondururdu. Ali Bey isterse klasoru burada elle degistirebilir.
            var txtHedefKlasor = new TextBox { Width = 220, Text = @"C:\Users\Public\Desktop", BackColor = Color.White, ForeColor = RenkFg };
            pDosya.Controls.Add(new Label { Text = "Hedef klasor:", AutoSize = true, Margin = new Padding(10, 8, 4, 0) });
            pDosya.Controls.Add(txtHedefKlasor);
            var txtDosyaAdi = new TextBox { Width = 140, Text = "yukle.pdf", BackColor = Color.White, ForeColor = RenkFg };
            pDosya.Controls.Add(new Label { Text = "Dosya adi:", AutoSize = true, Margin = new Padding(10, 8, 4, 0) });
            pDosya.Controls.Add(txtDosyaAdi);
            // v104 (Ali Bey'in istegi: "dosya secimini kolaylastir, cok secim var"): Tur'u elle
            // secmek + Dosya adini elle yazmak yerine, TEK bir "Goz at" ile bilgisayardan GERCEK
            // bir dosya secilir -- diyalogda SADECE desteklenen 6 uzanti (pdf/txt/jpg/docx/exe/zip)
            // gosterilir/secilebilir (baska uzanti secilemez), ve secilen dosyadan Tur + Dosya adi
            // OTOMATIK doldurulur. (Not: bu, SADECE isim/uzanti kolayligi icin -- dosyanin ICERIGI
            // hala Ali Bey tarafindan Drive'daki yukle.* sabitine elle yuklenmesi gerekir, FiloDosyaGonder
            // mimarisi degismedi.)
            var btnDosyaGozat = new Button
            {
                Text = "Goz at...",
                Width = 90,
                Height = 28,
                Margin = new Padding(10, 6, 0, 0),
                FlatStyle = FlatStyle.Flat,
                BackColor = RenkPanel,
                ForeColor = RenkFg
            };
            btnDosyaGozat.Click += (s, e) =>
            {
                using (var dlg = new OpenFileDialog
                {
                    Title = "Gonderilecek dosyayi sec (sadece desteklenen turler)",
                    Filter = "Desteklenen dosyalar (*.pdf;*.txt;*.jpg;*.docx;*.exe;*.zip)|*.pdf;*.txt;*.jpg;*.docx;*.exe;*.zip",
                    CheckFileExists = true,
                    Multiselect = false
                })
                {
                    if (dlg.ShowDialog() == DialogResult.OK)
                    {
                        string uzanti = Path.GetExtension(dlg.FileName).TrimStart('.').ToLowerInvariant();
                        int idx = cboDosyaTur.Items.IndexOf(uzanti);
                        if (idx < 0)
                        {
                            MessageBox.Show("Bu uzanti (" + uzanti + ") desteklenmiyor. Sadece pdf/txt/jpg/docx/exe/zip secilebilir.",
                                "Desteklenmeyen uzanti", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }
                        cboDosyaTur.SelectedIndex = idx;
                        txtDosyaAdi.Text = Path.GetFileName(dlg.FileName);
                    }
                }
            };
            pDosya.Controls.Add(btnDosyaGozat);
            p.Controls.Add(pDosya);
            cboDosyaTur.SelectedIndexChanged += (s, e) =>
            {
                // Kullaniciya kolaylik: tur degisince dosya adi onerisini de uzantiya gore guncelle
                // (kullanici kendi adini yazdiysa -- eski onerilerden biri degilse -- DOKUNULMAZ).
                var eskiOneriler = new[] { "yukle.pdf", "yukle.txt", "yukle.jpg", "yukle.docx", "yukle.exe", "yukle.zip" };
                if (Array.IndexOf(eskiOneriler, txtDosyaAdi.Text.Trim()) >= 0 || string.IsNullOrWhiteSpace(txtDosyaAdi.Text))
                    txtDosyaAdi.Text = "yukle." + cboDosyaTur.Text;
            };
            var btnDosyaGonder = Buton("Hedefteki makine(ler)e dosyayi GONDER", async () =>
            {
                string hedef = string.IsNullOrWhiteSpace(cboHedef.Text) ? "TUMU" : cboHedef.Text.Trim();
                // v105: Lab Gruplari kisitlamasi -- Super Kullanici acik degilse, bu bilgisayar
                // SADECE kendi grubuna islem gonderebilir. Izinli() false donerse hicbir Filo
                // komutu GONDERILMEZ (asagidaki tum kod atlanir).
                if (!FiloKisitlama.Izinli(hedef, labGruplari, out var kisitHata)) { Logger.UI(kisitHata); return; }
                string tur = cboDosyaTur.Text;
                string hedefKlasor = txtHedefKlasor.Text;
                string dosyaAdi = txtDosyaAdi.Text;
                var sonuc = MessageBox.Show(
                    "Hedef: " + hedef + "\r\nTur: " + tur + "\r\nKlasor: " + hedefKlasor + "\r\nDosya adi: " + dosyaAdi +
                    "\r\n\r\nGondermeden once Drive'daki yukle." + tur + " dosyasinin GUNCEL oldugundan emin ol. Devam edilsin mi?",
                    "Dosya Gonder - Onay", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (sonuc != DialogResult.Yes) return;
                await Calistir("Dosya gonder", () => Logger.UI(FiloDosyaGonder.FiloBaslat(hedef, tur, hedefKlasor, dosyaAdi)));
            });
            p.Controls.Add(btnDosyaGonder);

            // v94 (Ali Bey'in istegi: "buyuk ekran mesaji"): SYSTEM/Oturum 0 kisitlamasi yuzunden
            // ozel bir tam ekran pencere ACAMIYORUZ -- Ali Bey'in sectigi basit/guvenilir yontem:
            // Windows'un kendi WTSSendMessage API'si (bkz. FiloMesajGonder.cs) ile buyukce, her
            // zaman ustte bir sistem diyalog kutusu gosterilir, ogrenci TAMAM demeden kapanmaz.
            p.Controls.Add(new Label { Text = "Buyuk Ekran Mesaji", ForeColor = Color.OrangeRed, Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold), AutoSize = true, Margin = new Padding(6, 22, 0, 4) });
            p.Controls.Add(new Label
            {
                Text = "Hedefteki makine(ler)in ekraninda, ogrenci TAMAM demeden kapanmayan, her zaman\r\n" +
                       "en ustte bir sistem mesaj kutusu gosterir. Gercek tam ekran DEGILDIR ama goz ardi\r\n" +
                       "edilemez. Mesajda '|' karakteri kullanilmaz (otomatik '/' ile degistirilir).",
                ForeColor = Color.Gray, AutoSize = true, Margin = new Padding(6, 0, 0, 6)
            });
            var txtMesaj = new TextBox { Width = 460, Height = 60, Multiline = true, BackColor = Color.White, ForeColor = RenkFg, Margin = new Padding(6, 0, 0, 4) };
            p.Controls.Add(txtMesaj);
            var btnMesajGonder = Buton("Hedefteki makine(ler)in ekraninda GOSTER", async () =>
            {
                string hedef = string.IsNullOrWhiteSpace(cboHedef.Text) ? "TUMU" : cboHedef.Text.Trim();
                // v105: Lab Gruplari kisitlamasi -- Super Kullanici acik degilse, bu bilgisayar
                // SADECE kendi grubuna islem gonderebilir. Izinli() false donerse hicbir Filo
                // komutu GONDERILMEZ (asagidaki tum kod atlanir).
                if (!FiloKisitlama.Izinli(hedef, labGruplari, out var kisitHata)) { Logger.UI(kisitHata); return; }
                string mesaj = txtMesaj.Text;
                if (string.IsNullOrWhiteSpace(mesaj)) { MessageBox.Show("Mesaj bos olamaz."); return; }
                var sonuc = MessageBox.Show(
                    "Hedef: " + hedef + "\r\n\r\nMesaj hedefteki makine(ler)in ekraninda gosterilecek. Devam edilsin mi?",
                    "Mesaj Gonder - Onay", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (sonuc != DialogResult.Yes) return;
                await Calistir("Mesaj gonder", () => Logger.UI(FiloMesajGonder.FiloBaslat(hedef, mesaj)));
            });
            p.Controls.Add(btnMesajGonder);

            // v92 (Ali Bey'in istegi: "sheet teki verileri temizleme secenegi mumkunmu"): Google
            // Sheet'teki birikmis veriyi PANELDEN silme. Iki dugme, cunku sayfalar IKI FARKLI
            // sekilde tutuluyor -- Envanter MAKINE BASINA satir (lab bazinda silinebilir), komut
            // sayfalari (Kapat/YenidenBaslat/EnvanterIste/Sinav/Politika) OKUL BASINA TEK satir
            // (lab secimi burada ETKISIZ -- tum bekleyen komutlar/politika birlikte sifirlanir).
            p.Controls.Add(new Label { Text = "Sheet Verilerini Temizle", ForeColor = Color.OrangeRed, Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold), AutoSize = true, Margin = new Padding(6, 22, 0, 4) });
            p.Controls.Add(new Label
            {
                Text = "Envanter/Log: yukaridaki Hedef kutusundaki secime (TUMU/lab/tek makine) UYAN\r\n" +
                       "kayitlari Sheet'ten SILER (eski/kullanilmayan makineleri veya birikmis olay\r\n" +
                       "kayitlarini temizlemek icin). Komut sayfalari ise OKUL BASINA TEK satir tutar --\r\n" +
                       "orada lab secimi ETKISIZDIR, 'Bekleyen filo komutlarini sifirla' TUM bekleyen\r\n" +
                       "Kapat/Yeniden Baslat/Envanter Iste/Sinav/Politika komutlarini birden iptal eder.\r\n" +
                       "Bu islemler GERI ALINAMAZ.",
                ForeColor = Color.Gray, AutoSize = true, Margin = new Padding(6, 0, 0, 6)
            });
            var btnEnvanterTemizle = Buton("Hedefteki makine kayitlarini Envanter'den SIL", async () =>
            {
                string hedef = string.IsNullOrWhiteSpace(cboHedef.Text) ? "TUMU" : cboHedef.Text.Trim();
                // v105: Lab Gruplari kisitlamasi -- Super Kullanici acik degilse, bu bilgisayar
                // SADECE kendi grubuna islem gonderebilir. Izinli() false donerse hicbir Filo
                // komutu GONDERILMEZ (asagidaki tum kod atlanir).
                if (!FiloKisitlama.Izinli(hedef, labGruplari, out var kisitHata)) { Logger.UI(kisitHata); return; }
                var sonuc = MessageBox.Show(
                    "Hedef: " + hedef + "\r\n\r\nBu islem, Envanter sayfasindaki hedefe uyan makine kayitlarini KALICI OLARAK SILER. " +
                    "Geri alinamaz. Emin misiniz?",
                    "Envanter Temizle - Onay", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (sonuc != DialogResult.Yes) return;
                await Calistir("Envanter temizle", () => Logger.UI(FiloSheetTemizle.EnvanterTemizle(hedef)));
            });
            btnEnvanterTemizle.BackColor = Color.FromArgb(197, 48, 48); btnEnvanterTemizle.ForeColor = Color.White;
            p.Controls.Add(btnEnvanterTemizle);
            // v93 (Ali Bey'in geri bildirimi: "envanter tumu secince silindi, log silinmedi"):
            // Envanter ile BIREBIR AYNI Hedef/lab mantigiyla, ayrica Log sayfasini da temizleyen
            // bir dugme eklendi.
            var btnLogTemizle = Buton("Hedefteki makine kayitlarini Log'dan SIL", async () =>
            {
                string hedef = string.IsNullOrWhiteSpace(cboHedef.Text) ? "TUMU" : cboHedef.Text.Trim();
                // v105: Lab Gruplari kisitlamasi -- Super Kullanici acik degilse, bu bilgisayar
                // SADECE kendi grubuna islem gonderebilir. Izinli() false donerse hicbir Filo
                // komutu GONDERILMEZ (asagidaki tum kod atlanir).
                if (!FiloKisitlama.Izinli(hedef, labGruplari, out var kisitHata)) { Logger.UI(kisitHata); return; }
                var sonuc = MessageBox.Show(
                    "Hedef: " + hedef + "\r\n\r\nBu islem, Log sayfasindaki hedefe uyan olay kayitlarini KALICI OLARAK SILER. " +
                    "Geri alinamaz. Emin misiniz?",
                    "Log Temizle - Onay", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (sonuc != DialogResult.Yes) return;
                await Calistir("Log temizle", () => Logger.UI(FiloSheetTemizle.LogTemizle(hedef)));
            });
            btnLogTemizle.BackColor = Color.FromArgb(197, 48, 48); btnLogTemizle.ForeColor = Color.White;
            p.Controls.Add(btnLogTemizle);
            var btnKomutlariTemizle = Buton("Bekleyen TUM filo komutlarini/politikayi sifirla", async () =>
            {
                var sonuc = MessageBox.Show(
                    "Bu islem, bekleyen TUM Kapat/Yeniden Baslat/Envanter Iste/Sinav/Politika komutlarini " +
                    "(okul genelinde, lab ayrimi YOKTUR) sifirlar/iptal eder. Geri alinamaz. Emin misiniz?",
                    "Filo Komutlarini Sifirla - Onay", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (sonuc != DialogResult.Yes) return;
                await Calistir("Filo komutlarini sifirla", () => Logger.UI(FiloSheetTemizle.KomutlariTemizle()));
            });
            btnKomutlariTemizle.BackColor = Color.FromArgb(197, 48, 48); btnKomutlariTemizle.ForeColor = Color.White;
            p.Controls.Add(btnKomutlariTemizle);

            tp.Controls.Add(p);
            return tp;
        }

        // =====================================================================================
        // 7) RAPORLAR (yeni, v55) -- Merkezi kayit (3 dugme -> 1 dugme + secim), envanter listesi,
        //    bu bilgisayarin gunluk/haftalik oyun raporu (Koruma'dan tasindi).
        // =====================================================================================
        private Panel SekmeRaporlar()
        {
            var tp = YeniSekme("Raporlar");
            var p = AkisPanel();

            // v44: Bu TEK rapor grubu hem guncelleme/sifre hem de oyun-engelleme olaylarini ayni
            // tabloda gosterir (hepsi ayni merkezi Google Sheet'e dusuyor). v55: onceden 3 ayri
            // dugme (bugun/7gun/tumu) tek bir dugme + acilir listeye BIRLESTIRILDI (optimizasyon #1).
            p.Controls.Add(new Label { Text = "Merkezi kayit (tum bilgisayarlar: guncelleme + sifre + oyun + AI)", ForeColor = RenkVurgu, AutoSize = true, Margin = new Padding(6, 6, 0, 4) });
            var seciciPaneli = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false, Margin = new Padding(6, 0, 0, 4) };
            var cboAralik = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 220,
                BackColor = Color.White,
                ForeColor = RenkFg
            };
            cboAralik.Items.AddRange(new object[] { "Bugun", "Son 7 gun", "Tumu (son 200 kayit)" });
            cboAralik.SelectedIndex = 0;
            seciciPaneli.Controls.Add(cboAralik);
            p.Controls.Add(seciciPaneli);
            p.Controls.Add(Buton("Merkezi kaydi ac (secili aralik)", async () =>
            {
                // v57 hata duzeltmesi: cboAralik.SelectedIndex ONCEDEN Calistir(...) icinden
                // (arka plan is parcacigi) okunuyordu; artik UI is parcacigindan okunup
                // degiskene aliniyor.
                int secim = cboAralik.SelectedIndex;
                await Calistir("Merkezi kayit", () =>
                {
                    switch (secim)
                    {
                        case 0: Logger.UI(DriveLog.Ac(1)); break;
                        case 1: Logger.UI(DriveLog.Ac(7)); break;
                        default: Logger.UI(DriveLog.Ac()); break;
                    }
                });
            }));

            p.Controls.Add(new Label { Text = "Envanter", ForeColor = RenkVurgu, AutoSize = true, Margin = new Padding(6, 14, 0, 4) });
            p.Controls.Add(Buton("Tum makineler - envanter listesi (Sheet'i ac)", async () => await Calistir("Envanter listesi", () =>
                Logger.UI(DriveLog.AcEnvanter()))));
            // v70 (Ali Bey'in istegi): ayni envanter, yaziciya dokmeye hazir (yatay sayfa,
            // basliklar her sayfada tekrarlar) bir goruntude -- yukardakinden TAMAMEN AYRI secenek.
            p.Controls.Add(Buton("Tum makineler - envanter yazdirma gorunumu", async () => await Calistir("Envanter yazdirma gorunumu", () =>
                Logger.UI(DriveLog.AcEnvanterYazdirma()))));

            // v111 (Ali Bey'in istegi: "teshis raporlarda gozukmuyor"): Teshis sonuclari + istekleri.
            // v113e: AI Sorun Teshisi artik calisiyor (her teshis sonucu, Claude tarafindan Turkce +
            // madde madde yorumlanip ayni tabloda "AiYorum" sutununda gosteriliyor) -- aciklama ve
            // dugme metni bunu yansitacak sekilde guncellendi.
            p.Controls.Add(new Label { Text = "Teshis (uzaktan toplanan bilgi + AI yorumu)", ForeColor = RenkVurgu, AutoSize = true, Margin = new Padding(6, 14, 0, 4) });
            p.Controls.Add(new Label
            {
                Text = "Filo'dan gonderdiginiz teshis isteklerinin SONUCLARINI (Wi-Fi, ipconfig, proxy, disk,\r\n" +
                       "sistem, gorevler) ve Claude'un bu sonuclar uzerine yazdigi kisa Turkce AI yorumunu\r\n" +
                       "(gercek bir sorun varsa belirtir, yoksa \"Sorun tespit edilmedi\" yazar) tarayicida\r\n" +
                       "tablo olarak gosterir.",
                ForeColor = Color.Gray, AutoSize = true, Margin = new Padding(6, 0, 0, 4)
            });
            p.Controls.Add(ButonVurgu("\U0001F50D  Teshis sonuclarini + AI yorumunu goster (tarayicida ac)", async () =>
                await Calistir("Teshis raporu", () => Logger.UI(DriveLog.AcTeshis()))));

            p.Controls.Add(new Label { Text = "Filo bazinda engellenen oyunlar (TUM bilgisayarlar)", ForeColor = RenkVurgu, AutoSize = true, Margin = new Padding(6, 14, 0, 4) });
            p.Controls.Add(new Label
            {
                Text = "Oyun adi, hangi bilgisayar, tarih/saat -- okulun tum bilgisayarlarindan,\r\n" +
                       "merkezi kayittan derlenir (Drive Apps Script v55 gereklidir).",
                ForeColor = Color.Gray, AutoSize = true, Margin = new Padding(6, 0, 0, 4)
            });
            var filoOyunPaneli = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false, Margin = new Padding(6, 0, 0, 4) };
            var cboFiloOyunAralik = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 220,
                BackColor = Color.White,
                ForeColor = RenkFg
            };
            cboFiloOyunAralik.Items.AddRange(new object[] { "Gunluk (bugun)", "Haftalik (son 7 gun)", "Tumu (son 200 kayit)" });
            cboFiloOyunAralik.SelectedIndex = 0;
            filoOyunPaneli.Controls.Add(cboFiloOyunAralik);
            p.Controls.Add(filoOyunPaneli);
            p.Controls.Add(Buton("Filo oyun raporunu listele (secili aralik)", async () =>
            {
                // v57 hata duzeltmesi: ONCEDEN hem cboFiloOyunAralik okunuyor hem de ListeGoster()
                // (yeni bir Form acan) cagrisi, Calistir(...) ile baslatilan ARKA PLAN is
                // parcacigindan yapiliyordu. Artik secim degeri UI is parcacigindan okunuyor;
                // sadece agdan veri getirme islemi (DriveLog.OyunRaporuGetir) arka planda
                // calisiyor, sonuc degiskenlere yazildiktan sonra -- await Calistir(...) tamamlanip
                // UI is parcacigina GERI DONDUGUNDE -- ListeGoster() burada, UI is parcacigindan
                // cagriliyor.
                int gun = cboFiloOyunAralik.SelectedIndex == 0 ? 1 : (cboFiloOyunAralik.SelectedIndex == 1 ? 7 : 0);
                string aralikMetni = cboFiloOyunAralik.Text;
                string hataSonuc = null;
                System.Collections.Generic.List<string> listeSonuc = null;
                await Calistir("Filo oyun raporu", () =>
                {
                    listeSonuc = DriveLog.OyunRaporuGetir(gun, out hataSonuc);
                });
                if (hataSonuc != null) { Logger.UI(hataSonuc); return; }
                if (listeSonuc == null || listeSonuc.Count == 0) { Logger.UI("Bu aralikta filo genelinde engellenen oyun kaydi bulunamadi."); return; }
                ListeGoster("Filo bazinda engellenen oyunlar - " + aralikMetni, listeSonuc,
                    "TARIH/SAAT              |  OYUN                          |  BILGISAYAR");
            }));

            // v74 (Ali Bey'in istegi: "Ai tespitlerini raporlara ekleyelim") -- AI'nin kendi basina
            // bulup kalici listeye ekledigi oyun/vpn/youtube-ai-site tespitleri, TUM filodan,
            // makina + tespit + tarih/saat + kullanici olarak. Yukaridaki "Filo oyun raporu"
            // bloguyla AYNI desen (sadece Haftalik/Tumu -- Ali Bey'in istegi: "haftalık,tümü olsun").
            p.Controls.Add(new Label { Text = "AI tespitleri (TUM bilgisayarlar)", ForeColor = RenkVurgu, AutoSize = true, Margin = new Padding(6, 14, 0, 4) });
            p.Controls.Add(new Label
            {
                Text = "AI'nin kendiliginden bulup kalici listeye ekledigi oyun/vpn/youtube-ai\r\n" +
                       "siteleri -- makina, tespit, tarih/saat, kullanici (Drive Apps Script v57+\r\n" +
                       "yeniden dagitilmis olmali).",
                ForeColor = Color.Gray, AutoSize = true, Margin = new Padding(6, 0, 0, 4)
            });
            var filoAiPaneli = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false, Margin = new Padding(6, 0, 0, 4) };
            var cboFiloAiAralik = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 220,
                BackColor = Color.White,
                ForeColor = RenkFg
            };
            cboFiloAiAralik.Items.AddRange(new object[] { "Haftalik (son 7 gun)", "Tumu (son 200 kayit)" });
            cboFiloAiAralik.SelectedIndex = 0;
            filoAiPaneli.Controls.Add(cboFiloAiAralik);
            p.Controls.Add(filoAiPaneli);
            p.Controls.Add(Buton("AI tespitleri raporunu listele (secili aralik)", async () =>
            {
                int gun = cboFiloAiAralik.SelectedIndex == 0 ? 7 : 0;
                string aralikMetni = cboFiloAiAralik.Text;
                string hataSonuc = null;
                System.Collections.Generic.List<string> listeSonuc = null;
                await Calistir("AI tespitleri raporu", () =>
                {
                    listeSonuc = DriveLog.AiTespitRaporuGetir(gun, out hataSonuc);
                });
                if (hataSonuc != null) { Logger.UI(hataSonuc); return; }
                if (listeSonuc == null || listeSonuc.Count == 0) { Logger.UI("Bu aralikta filo genelinde AI tespit kaydi bulunamadi."); return; }
                ListeGoster("AI tespitleri - " + aralikMetni, listeSonuc,
                    "MAKINA              |  TESPIT                        |  TARIH/SAAT            |  KULLANICI");
            }));

            p.Controls.Add(new Label { Text = "Bu bilgisayar - oyun raporu", ForeColor = RenkVurgu, AutoSize = true, Margin = new Padding(6, 14, 0, 4) });
            p.Controls.Add(Buton("Bu bilgisayar - gunluk engellenen oyunlar (liste)", async () =>
            {
                await System.Threading.Tasks.Task.CompletedTask;
                ListeGoster("Bugun engellenen / kaldirilan oyunlar (bu bilgisayar)", EngelKayit.Listele(0));
            }));
            p.Controls.Add(Buton("Bu bilgisayar - haftalik engellenen oyunlar (liste)", async () =>
            {
                await System.Threading.Tasks.Task.CompletedTask;
                ListeGoster("Son 7 gun engellenen / kaldirilan oyunlar (bu bilgisayar)", EngelKayit.Listele(7));
            }));
            // v42/v43: Yukaridaki iki rapor SADECE o an panelin acik oldugu bilgisayarin yerel kaydini
            // gosterir. TUM bilgisayarlardaki oyun engellerini (+ guncelleme + sifre + AI olaylarini)
            // TEK tabloda gormek icin: yukaridaki "Merkezi kayit" bolumu.

            tp.Controls.Add(p);
            return tp;
        }

        private Panel SekmeKullanici()
        {
            var tp = YeniSekme("Kullanicilar");
            var p = AkisPanel();

            p.Controls.Add(new Label
            {
                Text = "Olusturulacak STANDART kullanicilar (her satira bir ad):\r\n" +
                       "ornek:  9-B   10-B   11-B   12-B",
                ForeColor = RenkVurgu, AutoSize = true, Margin = new Padding(6, 6, 0, 4)
            });

            string kayitliParola;
            var kayitli = Core.Kullanici.Yukle(out kayitliParola);

            var txtAdlar = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Width = 360,
                Height = 150,
                BackColor = Color.White,
                ForeColor = RenkFg,
                BorderStyle = BorderStyle.FixedSingle,
                Text = string.Join(Environment.NewLine, kayitli),
                Margin = new Padding(6, 2, 4, 8)
            };
            p.Controls.Add(txtAdlar);

            p.Controls.Add(new Label { Text = "Ortak parola (bos birakirsan parolasiz acilir):", ForeColor = RenkFg, AutoSize = true, Margin = new Padding(6, 6, 0, 2) });
            // v80 (paket denetimi bulgusu P1-07: "ortak parola kutusu maskelenmiyor -- omuz ustu
            // goruntuleme riski"): PasswordChar eklendi, ekranda artik yildizli gorunur. Davranis
            // (bos birakinca sifresiz acilmasi) BILEREK aynen korundu -- bu Ali Bey'in ogrenci
            // makinelerinde hizli/surtunmesiz giris icin kullandigi bilinen bir ozellik; sadece
            // asagidaki "OLUSTUR" butonunda bos parolayla devam etmeden once acik bir onay eklendi.
            var txtParola = new TextBox
            {
                Width = 360,
                BackColor = Color.White,
                ForeColor = RenkFg,
                BorderStyle = BorderStyle.FixedSingle,
                Text = kayitliParola,
                PasswordChar = '*',
                Margin = new Padding(6, 2, 4, 8)
            };
            p.Controls.Add(txtParola);

            // v109: Rol secimi -- hesaplar UB_Ogrenci veya UB_Ogretmen yerel grubuna konur (ikisi de
            // STANDART kullanici). Ogrenci rolundeki hesaplar Windows Bakim klasorunu goremez ve
            // (ogrenci makinesinde) AppLocker kurallarina tabidir. Yonetici olan bir hesap secilen
            // role atanirken standart kullaniciya DUSURULUR (yonetici/yerlesik/paneli calistiran haric).
            var rolPaneli = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(6, 2, 0, 8) };
            rolPaneli.Controls.Add(new Label { Text = "Rol:", ForeColor = RenkFg, AutoSize = true, Margin = new Padding(0, 4, 6, 0) });
            var rbOgrenci = new RadioButton { Text = "Ogrenci", AutoSize = true, Checked = true, ForeColor = RenkFg };
            var rbOgretmen = new RadioButton { Text = "Ogretmen", AutoSize = true, ForeColor = RenkFg, Margin = new Padding(12, 3, 0, 0) };
            rolPaneli.Controls.Add(rbOgrenci);
            rolPaneli.Controls.Add(rbOgretmen);
            p.Controls.Add(rolPaneli);

            string[] Adlar() => txtAdlar.Text.Replace("\r", "").Split('\n');

            p.Controls.Add(Buton("Listeyi USB'ye kaydet", async () =>
            {
                // v57 hata duzeltmesi: Adlar()/txtParola.Text ONCEDEN Calistir(...) icinden
                // (arka plan is parcacigi) okunuyordu; artik UI is parcacigindan okunup
                // degiskene aliniyor.
                string[] adlar = Adlar();
                string parola = txtParola.Text;
                await Calistir("Liste kaydet", () =>
                {
                    string yer = Core.Kullanici.Kaydet(adlar, parola);
                    Logger.UI("Kullanici listesi kaydedildi: " + yer);
                    Logger.UI("Bu USB'yi baska bilgisayarda acip 'Olustur' diyebilirsin.");
                });
            }));

            p.Controls.Add(Buton("Standart kullanicilari OLUSTUR", async () =>
            {
                if (MessageBox.Show(
                        "Listedeki adlar bu bilgisayarda STANDART kullanici olarak olusturulacak/guncellenecek\r\n" +
                        "ve secilen ROLE (" + (rbOgretmen.Checked ? "OGRETMEN" : "OGRENCI") + ") atanacak.\r\n" +
                        "(Yonetici degil; yonetici olan hesap standart kullaniciya dusurulur.)\r\n\r\nDevam?",
                        "Kullanici olustur", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    // v57 hata duzeltmesi: Adlar()/txtParola.Text ONCEDEN Calistir(...) icinden
                    // (arka plan is parcacigi) okunuyordu; artik UI is parcacigindan okunup
                    // degiskene aliniyor.
                    string[] adlar = Adlar();
                    string parola = txtParola.Text;
                    bool rolOgretmen = rbOgretmen.Checked;

                    // v80 (paket denetimi bulgusu P1-07: "bos parola acikca kabul edilebilir
                    // secenek olarak sunuluyor"): davranis (bos = sifresiz) BILEREK korunuyor,
                    // ama artik oyle devam etmeden once ayrica acik onay isteniyor.
                    if (string.IsNullOrEmpty(parola))
                    {
                        if (MessageBox.Show(
                                "Parola alani BOS -- bu hesaplar SIFRESIZ acilacak (herkes parolasiz girebilir).\r\n\r\nYine de devam edilsin mi?",
                                "Parola bos", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                            return;
                    }

                    // v80 (paket denetimi bulgusu P1-07: "mevcut hesaplarda kod parola guncelleme
                    // yoluna da giriyor; toplu islem beklenmeyen mevcut hesap parolasini
                    // degistirebilir"): OLUSTURMADAN ONCE, listedeki adlardan bu makinede ZATEN
                    // var olanlari sorup, varsa parolalarinin YENIDEN YAZILACAGINI acikca
                    // soruyoruz. Bu sorgu da (net user) sistem komutu calistirdigi icin arka
                    // planda yapiliyor (UI donmasin diye), ama ONAY penceresi gosterilmeden
                    // ASIL OLUSTURMA/GUNCELLEME baslamiyor.
                    System.Collections.Generic.List<string> zatenVar = null;
                    await Task.Run(() => { zatenVar = Core.Kullanici.ZatenVarOlanlar(adlar); });
                    if (zatenVar != null && zatenVar.Count > 0)
                    {
                        if (MessageBox.Show(
                                "Su hesaplar bu makinede ZATEN VAR ve parolalari YENIDEN YAZILACAK:\r\n\r\n" +
                                "  " + string.Join(", ", zatenVar) + "\r\n\r\nDevam edilsin mi?",
                                "Mevcut hesap parolasi degisecek", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                            return;
                    }

                    await Calistir("Kullanici olustur", () =>
                    {
                        string rapor;
                        int n = Core.Kullanici.Olustur(adlar, parola, rolOgretmen, out rapor);
                        Logger.UI("Rol: " + (rolOgretmen ? "OGRETMEN (UB_Ogretmen)" : "OGRENCI (UB_Ogrenci)"));
                        Logger.UI("Olusturulan yeni hesap: " + n);
                        Logger.UI(rapor);
                        // Bir dahaki sefere hazir dursun diye listeyi de kaydet
                        try { Core.Kullanici.Kaydet(adlar, parola); } catch { }
                    });
                }
            }));

            tp.Controls.Add(p);
            return tp;
        }

        // =====================================================================================
        // 9) YARDIM (yeni, v104 -- Ali Bey'in istegi: "uygun version ve help olmali")
        // =====================================================================================
        private Panel SekmeYardim()
        {
            var tp = YeniSekme("Yardim");
            var p = AkisPanel();

            void Baslik(string s) => p.Controls.Add(new Label { Text = s, ForeColor = RenkVurgu, Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 14, 0, 6) });
            void Satir(string s) => p.Controls.Add(new Label { Text = s, ForeColor = RenkFg, AutoSize = true, MaximumSize = new Size(720, 0), Margin = new Padding(0, 0, 0, 4) });

            p.Controls.Add(new Label { Text = "UNSAL BAKIM - " + License.Surum, ForeColor = RenkFg, Font = new Font("Segoe UI Semibold", 14f, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 0, 0, 2) });
            Satir("Lisans durumu: " + License.Durum);

            Baslik("Bu program ne yapar?");
            Satir("Okul bilgisayarlarinda Windows bakimini, hizlandirmayi, oyun/YouTube/VPN kisitlamalarini ve");
            Satir("filodan (merkezi) uzaktan yonetimi tek bir uygulamadan saglar. Hazirlayan: Ali Unsal.");

            Baslik("Sol menudeki bolumler");
            Satir("Genel Bakis       : Bu makinenin ozet durumu (surum, koruma ayarlari, zamanlanmis gorevler).");
            Satir("Bakim             : Windows temizligi/hizlandirma islemlerini calistirir.");
            Satir("Kurulum           : Programi bu makineye kurar / zamanlanmis gorevleri yonetir.");
            Satir("Koruma            : Oyun/VPN/YouTube/AI site/Kamera kisitlamalarini acar-kapatir.");
            Satir("Yerel Ayarlar     : Sadece bu makineye ozel haftalik bakim/AI tarama zamanlarini ayarlar.");
            Satir("Filo Yonetimi     : Okuldaki TUM makineleri (veya secilenleri) merkezden yonetir.");
            Satir("Raporlar          : Makinelerden toplanan envanter/durum bilgilerini listeler.");
            Satir("Kullanicilar      : Windows kullanici hesaplarini olusturur/yonetir.");

            Baslik("Sik sorulan sorular");
            Satir("- \"Degisiklik makineye ne zaman yansir?\"  Filo komutlari en gec ~5 dakika icinde uygulanir");
            Satir("  (bkz. 5 dakikalik denetim gorevi); yerel ayarlar hemen uygulanir.");
            Satir("- \"Sorun yasiyorum, nereye bakmaliyim?\"  Ana ekranin altindaki islem gunlugu kutusu ve");
            Satir("  Raporlar bolumu, en son neler oldugunu gosterir.");

            Baslik("v109 - ogrenciye kapali, ogretmene acik");
            Satir("- Hesaplar iki yerel gruptan birindedir: UB_Ogrenci veya UB_Ogretmen (ikisi de standart kullanici).");
            Satir("  Rol, Kullanicilar sekmesinden degistirilir; yeni hesaplar makine turune gore saatte bir otomatik atanir.");
            Satir("- Ogrenci makinesinde ogrenci, program klasorunu (Program Files\\Windows Bakim) ve ProgramData\\UnsalBakim'i");
            Satir("  goremez/calistiramaz. Ogrenci bilgisayarinda paneli USB'den acin.");
            Satir("- AppLocker ilk kurulumda DENETIM modundadir (sadece kaydeder). Koruma sekmesinden kayitlara bakip");
            Satir("  ders yazilimlari dogrulandiktan sonra ZORUNLU moda alin; sorun olursa DENETIM'e geri alin.");
            Satir("- Her makinenin hata/uyari ozeti Google Sheet'teki MakineLog sekmesindedir (makine basina tek satir).");
            Satir("- Ag sifirlama artik otomatik degil: Bakim sekmesindeki dugmeyle, onayla yapilir.");

            Baslik("v110 - uzaktan guvenli yonetim");
            Satir("- Guncelleme ONCE: makineler her 5 dk Drive'daki surumu kontrol eder; yeni surum varsa once guncellenir,");
            Satir("  Filo komutlarini yeni surum uygular (komutlar Sheet'te bekler).");
            
            Satir("- Teshis (Filo): Wi-Fi/BSSID, ipconfig, proxy, aygit, disk, sistem, gorev bilgisi -> Sheet 'Teshis' sayfasi.");
            Satir("  Sadece sabit, salt-okunur sorgular; Sheet'ten komut calistirilamaz.");
            Satir("- ACIL koruma anahtari (Filo): ogrenci AppLocker/klasor kilitlerini uzaktan geri alir.");
            Satir("- Ogrenci makinelerinde tarayici gizli DNS (DoH) kapali ve Google/Bing SafeSearch zorunlu.");
            Satir("- MakineLog'da son 7 gunun Wi-Fi baglantilari; 'fatih' / 'f@tih' disindaki aglar [!] ile isaretlenir.");
            Satir("- Envanter ID'si artik donanimdan (HW...) ve MAC fiziksel karttan alinir; eski satirlar otomatik tasinir.");

            Baslik("Destek");
            Satir("Teknik sorular ve yeni istekler icin: Ali Unsal (Bilisim Teknolojileri Ogretmeni, BMTAL).");

            tp.Controls.Add(p);
            return tp;
        }

        // ---- Yardimci UI ----
        // v104 (Ali Bey'in istegi: "2. sade kaliyor, albenisi yuksek, ciddi ticari olsun"):
        // koseleri yuvarlatilmis + hafif golgeli "kart" gorunumu icin kucuk bir GDI+ yardimcisi.
        private static GraphicsPath YuvarlakYol(Rectangle r, int yaricap)
        {
            int d = yaricap * 2;
            var yol = new GraphicsPath();
            if (r.Width <= 0 || r.Height <= 0) { yol.AddRectangle(r); return yol; }
            yol.AddArc(r.X, r.Y, d, d, 180, 90);
            yol.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            yol.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            yol.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            yol.CloseFigure();
            return yol;
        }

        // v104: TabPage yerine duz Panel -- disaridaki acik gri zeminde, Padding ile birakilan
        // bosluk sayesinde icerideki AkisPanel() (beyaz, yuvarlak koseli) bir "kart" gibi
        // gorunur; bu metot ayrica kartin arkasina hafif bir golge cizer. Varsayilan
        // Visible=false: gorunur olmak SekmeSec() tarafindan tek bir panel icin acilir.
        // v104 duzeltme (Ali Bey'in geri bildirimi: ekran goruntusunde kirmizi ok ile
        // isaretledi -- "Koruma" gibi az icerikli sekmelerde kart, eskiden Dock=Fill oldugu
        // icin ZORLA tum sekme yuksekligini dolduruyor, icerik bittikten sonra kocaman BOS
        // beyaz alan kaliyordu. Artik kart ICERIGE GORE boyutlanir (AkisPanel Dock=Top +
        // AutoSize); icerik kisa ise kart kisa kalir, altinda sadece gri zemin gorunur.
        // Icerik uzun ise (orn. Filo Yonetimi) kartin kendisi uzar ve bu DIS panel
        // (AutoScroll=true) tum sayfayi asagi kaydirir -- eskiden oldugu gibi kartin
        // ICINDE ayri bir kaydirma cubugu YERINE, butun sayfa birlikte kayar.
        private Panel YeniSekme(string ad)
        {
            var tp = new Panel { Dock = DockStyle.Fill, BackColor = RenkBg, ForeColor = RenkFg, Padding = new Padding(22), Visible = false, AutoScroll = true };
            // Kart AutoSize ile boyut degistirdikce (icerik kisa/uzun) golge de yeniden cizilsin.
            tp.Layout += (s, e) => tp.Invalidate();
            tp.Paint += (s, e) =>
            {
                if (tp.Controls.Count == 0) return;
                var kartAlani = tp.Controls[0].Bounds; // kartin GERCEK (icerige gore) sinirlari
                var golgeAlani = new Rectangle(kartAlani.X + 4, kartAlani.Y + 4, kartAlani.Width, kartAlani.Height);
                using (var yol = YuvarlakYol(golgeAlani, 10))
                using (var br = new SolidBrush(Color.FromArgb(26, 20, 40, 80)))
                    e.Graphics.FillPath(br, yol);
            };
            return tp;
        }

        private FlowLayoutPanel AkisPanel()
        {
            var p = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = RenkPanel,
                Padding = new Padding(18)
            };
            p.Resize += (s, e) =>
            {
                try { p.Region = new Region(YuvarlakYol(new Rectangle(Point.Empty, p.Size), 10)); } catch { }
            };
            return p;
        }

        // v104: Genel Bakis'taki renkli "KPI" kutulari icin kucuk bir yardimci -- ust kenarda
        // ince renkli bir vurgu cizgisi + ince kenarlik ile sade ama "panel" hissi veren kutu.
        private (Panel Kutu, Label Deger) KpiKutusu(string baslik, string ilkDeger, Color vurgu)
        {
            var kutu = new Panel { Width = 190, Height = 72, Margin = new Padding(0, 0, 14, 14), BackColor = Color.White };
            var ustCizgi = new Panel { Dock = DockStyle.Top, Height = 3, BackColor = vurgu };
            var k = new Label { Text = baslik.ToUpperInvariant(), ForeColor = Color.FromArgb(138, 151, 163), Font = new Font("Segoe UI", 8f, FontStyle.Bold), AutoSize = true, Location = new Point(14, 16) };
            var v = new Label { Text = ilkDeger, ForeColor = RenkFg, Font = new Font("Segoe UI Semibold", 13.5f, FontStyle.Bold), AutoSize = true, Location = new Point(14, 36) };
            kutu.Paint += (s, e) =>
            {
                using (var kenar = new Pen(RenkKenar)) e.Graphics.DrawRectangle(kenar, 0, 3, kutu.Width - 1, kutu.Height - 4);
            };
            kutu.Controls.Add(k);
            kutu.Controls.Add(v);
            kutu.Controls.Add(ustCizgi);
            return (kutu, v);
        }

        private Button Buton(string metin, Func<Task> isAsync)
        {
            var b = new Button
            {
                Text = metin,
                Width = 360,
                Height = 42,
                Margin = new Padding(4, 6, 4, 2),
                FlatStyle = FlatStyle.Flat,
                BackColor = RenkPanel,
                ForeColor = RenkFg,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0)
            };
            b.FlatAppearance.BorderColor = Color.FromArgb(45, 60, 72);
            b.Click += async (s, e) => { b.Enabled = false; try { await isAsync(); } finally { b.Enabled = true; } };
            return b;
        }

        // v110: Belirgin (vurgu) buton -- renkli zemin, beyaz kalin yazi, ortalanmis. Onemli
        // eylemler (Teshis gonder, acil koruma) icin; sade "Buton" cok silik kaliyordu.
        private Button ButonVurgu(string metin, Func<Task> isAsync, Color? arka = null)
        {
            var renk = arka ?? RenkVurgu;
            var b = new Button
            {
                Text = metin,
                Width = 360,
                Height = 50,
                Margin = new Padding(4, 8, 4, 4),
                FlatStyle = FlatStyle.Flat,
                BackColor = renk,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = ControlPaint.Light(renk, 0.2f);
            b.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(renk, 0.1f);
            b.Click += async (s, e) => { b.Enabled = false; try { await isAsync(); } finally { b.Enabled = true; } };
            return b;
        }

        private CheckBox OnayKutu(string metin, bool deger, Action<bool> set)
        {
            var c = new CheckBox { Text = metin, Checked = deger, AutoSize = true, Margin = new Padding(6, 8, 4, 4), ForeColor = RenkFg };
            c.CheckedChanged += (s, e) => set(c.Checked);
            return c;
        }

        /// <summary>v55: en yikici 2-3 islem icin -- sadece kirmizi renk degil, kullanicinin
        /// gercekten bilerek onayladigini garantileyen YAZILI onay (beklenen metni tam olarak
        /// yazmasini ister). Eslesmezse veya iptal ederse false doner, hicbir sey yapilmaz.</summary>
        private bool YaziliOnayAl(string beklenenMetin, string aciklama)
        {
            string girilen = Microsoft.VisualBasic.Interaction.InputBox(
                aciklama, "Onay gerekli: " + beklenenMetin, "");
            return string.Equals((girilen ?? "").Trim(), beklenenMetin, StringComparison.Ordinal);
        }

        /// <summary>v57 hata duzeltmesi: standart InputBox parola karakterlerini maskelemiyordu
        /// (ekranda acikca gorunuyordu). Bu kucuk ozel diyalog, PasswordChar ile yildizli bir
        /// TextBox kullanir. Iptal/kapat -> null; Tamam -> girilen metin (bos da olabilir).</summary>
        private string ParolaSor(string aciklama, string baslik)
        {
            using (var f = new Form
            {
                Text = baslik,
                Width = 420,
                Height = 170,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MinimizeBox = false,
                MaximizeBox = false,
                BackColor = RenkBg,
                ForeColor = RenkFg
            })
            {
                var lbl = new Label { Text = aciklama, AutoSize = true, Location = new Point(12, 14), ForeColor = RenkFg };
                var txt = new TextBox { Location = new Point(12, 40), Width = 380, PasswordChar = '*', ForeColor = RenkFg, BackColor = Color.White };
                var ok = new Button { Text = "Tamam", DialogResult = DialogResult.OK, Location = new Point(236, 80), Width = 75 };
                var iptal = new Button { Text = "Iptal", DialogResult = DialogResult.Cancel, Location = new Point(317, 80), Width = 75 };
                f.Controls.Add(lbl); f.Controls.Add(txt); f.Controls.Add(ok); f.Controls.Add(iptal);
                f.AcceptButton = ok; f.CancelButton = iptal;
                return f.ShowDialog(this) == DialogResult.OK ? txt.Text : null;
            }
        }

        private async Task Calistir(string ad, Action is1)
        {
            Durum(ad + " calisiyor...");
            UiLog("--- " + ad + " basladi ---");
            try { await Task.Run(is1); }
            catch (Exception ex) { UiLog("[HATA] " + ex.Message); }
            Durum("Hazir.");
        }

        /// <summary>v40: "Ogrenci"/"Ogretmen" profiliyle HER ZAMAN gizli kurulum yapar (ayri bir
        /// "gizli olmayan" secenek yok). Profili uygular (OyunEngelle/VpnEngelle/YoutubeKisitli),
        /// kaydeder, Installer.Kur ile kurar, kisitlama fonksiyonlarini hemen uygular/kaldirir.
        /// v55: AiSiteEngelle BILEREK profile BAGLI DEGIL -- kurulum profili ne olursa olsun,
        /// AI web sitesi engeli ayri ve varsayilan KAPALI kalir (Koruma/Yerel Ayarlar'dan elle
        /// acilabilir, ya da Filo Yonetimi'nden tum filoya zorunlu uygulanabilir).</summary>
        private async Task KurGizliProfil(string profil, string onayMesaji)
        {
            if (MessageBox.Show(onayMesaji, "Kurulum", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            await Calistir("Kurulum (" + profil + ")", () =>
            {
                _ayar.MakineTuru = profil;
                bool kisitlamaAcik = !string.Equals(profil, "ogretmen", StringComparison.OrdinalIgnoreCase);
                _ayar.OyunEngelle = kisitlamaAcik;
                _ayar.VpnEngelle = kisitlamaAcik;
                _ayar.YoutubeKisitli = kisitlamaAcik;
                // v55 duzeltme: AI web sitesi engeli ONCEDEN burada hic ayarlanmiyordu -- OGRENCI
                // kurulumunda bile hep varsayilan "kapali/serbest" kaliyordu (digerleri gibi
                // otomatik acilmiyordu). Ali Bey'in acik istegi: OGRENCI kurulumunda TUM
                // kisitlamalar (Oyun/VPN/YouTube/AI) hicbir ek islem yapmadan ZATEN aktif olsun;
                // OGRETMEN kurulumunda hicbiri aktif olmasin. Digerleriyle AYNI kisitlamaAcik
                // mantigina baglandi.
                _ayar.AiSiteEngelle = kisitlamaAcik;
                _ayar.Save();

                string yer = Installer.Kur(_ayar);
                try { if (_ayar.VpnEngelle) VpnKontrol.Engelle(); else VpnKontrol.KaldirEngel(); } catch { }
                try { YoutubeKisit.SaatiUygula(); } catch { }
                try { if (_ayar.AiSiteEngelle) AiSiteEngelle.Engelle(); else AiSiteEngelle.KaldirEngel(); } catch { }

                // v76 (Ali Bey'in duzeltmesi: "biz orayami kur dedik Kurulum sekmesi ogretmen kur
                // calistiginda hazirlasin dedik") -- kisayol artik asil istenen yerde: BU bilgisayar
                // "OGRETMEN bilgisayari olarak kur" ile kurulurken (ORTAK masaustu + ORTAK Baslat
                // menusune), Kullanicilar sekmesindeki ayri hesap olusturmaya bagli degil.
                if (string.Equals(profil, "ogretmen", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        Core.Kullanici.KisayolOlustur(out string kisayolMesaj);
                        Logger.UI(kisayolMesaj);
                    }
                    catch { }
                }

                Logger.UI("GIZLI kurulum tamam (" + profil.ToUpperInvariant() + " profili, onceki kurulum sonlandirildi). Konum: " + yer);
                Logger.UI(Environment.NewLine + TaskHelper.GorevDurumu());
            });
        }

        private void Durum(string s)
        {
            if (_durum.InvokeRequired) { _durum.BeginInvoke((Action)(() => _durum.Text = s)); return; }
            _durum.Text = s;
        }

        private void UiLog(string s)
        {
            if (_log == null) return;
            if (_log.InvokeRequired) { _log.BeginInvoke((Action)(() => UiLog(s))); return; }
            _log.AppendText(s + Environment.NewLine);
            _log.SelectionStart = _log.TextLength;
            _log.ScrollToCaret();
        }
    }
}
