using System;
using System.IO;

namespace UnsalBakim.Core
{
    /// <summary>Bakim islemleri: hafif / agir. .bat yerine native + harici arac cagrisi.</summary>
    public static class Maintenance
    {
        public static void HafifBakim(AppSettings ayar, bool trim = true)
        {
            const string log = "hafif.txt";
            Logger.Write(log, "================= HAFIF BAKIM BASLADI =================");

            Logger.UI("[1/5] Gecici dosyalar temizleniyor...");
            long mb = TempTemizle(3);
            Logger.Write(log, "TEMP temizligi: ~" + mb + " MB");

            Logger.UI("[2/5] Guvenlik duvari + virus programi + DNS...");
            PerformansAyar.FirewallVeAV();   // firewall ON + AV (Defender servisi) ON
            NativeRunner.Run("ipconfig.exe", "/flushdns", log, false);
            if (ayar.CopKutusuKapat) { PerformansAyar.CopKutusuKapat(); PerformansAyar.CopKutusuBosalt(); }

            // v82 (Ali Bey'in istegi, 6 Kasim 2026): Windows Guncelleme indirme onbellegi
            // (eski/kullanilmis guncelleme dosyalari) bosaltilir -- disk yer acar, Windows
            // gerektiginde yeniden indirir, kullanici dosyalarina dokunmaz.
            Logger.UI("[2d/5] Windows Guncelleme onbellegi temizleniyor...");
            try { PerformansAyar.GuncellemeOnbellekTemizle(); } catch (Exception ex) { Logger.Write(log, "Guncelleme onbellek temizligi hatasi: " + ex.Message); }

            // v30: kamera tercihini gunde iki kez (bu gorevle) yeniden uygula -- surucu/Windows
            // Update PnP durumunu sifirlarsa, kapali kalmasi gereken kamera sessizce geri acilmasin.
            try { if (ayar.KameraKapali) KameraKontrol.Kapat(); } catch { }

            // AGIR bakimdan HAFIF'e tasinan HIZLI ayarlar: bozuk bakim policy duzeltme + VERI KORUMA.
            // (Veri koruma = Sistem Korumasi acik tutulur -- bu SISTEM AYARLARININ geri alinmasi
            //  icindir, kullanici dosyasi SILMEZ. DUZELTME (6 Kasim 2026): Windows 10/11'de "sag tik >
            //  Onceki surumleri geri yukle" ile TEK DOSYA kurtarma artik YOK (Win8'de istemciden
            //  kaldirildi); gercek dosya kurtarma guvencesi Cop Kutusu'nun acik kalmasina baglidir.)
            Logger.UI("[2c/5] Hizli ayarlar + sistem korumasi aciliyor...");
            try { PerformansAyar.BakimPolicyDuzelt(); } catch { }
            try { PerformansAyar.VeriKoruma(); } catch { }

            Logger.UI("[2a/5] Windows Disk Temizleme (SilentCleanup)...");
            NativeRunner.Run("schtasks.exe", "/run /tn \"\\Microsoft\\Windows\\DiskCleanup\\SilentCleanup\"", log, false);

            if (ayar.GorselPerformans)
            {
                Logger.UI("[2b/5] Gorsel efektler performansa aliniyor...");
                PerformansAyar.GorselTumKullanicilar();
            }
            if (ayar.YuksekPerformansPlan)
                NativeRunner.Run("powercfg.exe", "/setactive SCHEME_MIN", log, false);

            Logger.UI("[3/5] SSD TRIM / disk optimizasyonu...");
            if (trim) NativeRunner.Run("defrag.exe", "C: /L", log); // SSD icin ReTrim (gunde 1 kez yeter)
            else Logger.Write(log, "TRIM bu calismada atlandi (ogle calismasinda yapilir).");

            if (ayar.EkranHepAcik)
            {
                Logger.UI("[4/5] Ekran/uyku ayari (hep acik)...");
                NativeRunner.Run("powercfg.exe", "-change -monitor-timeout-ac 0", log, false);
                NativeRunner.Run("powercfg.exe", "-change -standby-timeout-ac 0", log, false);
            }

            Logger.UI("[5/5] Disk durumu raporlaniyor...");
            DiskRaporu(log);

            // v109: AKILLI GUNLUK BAKIM -- hizli saglik kontrolu (bulgu varsa agir bakim onarir),
            // cokme dokumleri, kurulum artiklari, dosyasi olmayan baslangic girdileri, geri yukleme
            // noktasi sayisinin 1'e indirilmesi ve (gerekiyorsa) disk acil temizligi. Hicbiri
            // kisisel dosyaya veya Cop Kutusu'na dokunmaz.
            Logger.UI("[5b/5] Akilli gunluk bakim (saglik, dokum, artik, geri yukleme noktasi)...");
            try { AkilliBakim.HizliSaglikKontrolu(log); } catch (Exception ex) { Logger.Write(log, "Saglik kontrolu hatasi: " + ex.Message); }
            try { AkilliBakim.CokmeDokumleriTemizle(); } catch { }
            try { Installer.EskiArtiklariTemizle(); } catch { }
            try { AkilliBakim.YetimBaslangicGirdileriniTemizle(); } catch { }
            try { AkilliBakim.GeriYuklemeAlaniSinirla(); AkilliBakim.GeriYuklemeNoktalariniTekeIndir(); } catch { }
            try { AkilliBakim.DiskKritikDonguKontrol(); } catch { }

            // v49: Gunluk envanter gonderimi -- ayri bir zamanlanmis gorev ACILMADI, zaten gunluk
            // calisan bu goreve eklendi (DriveLog.EnvanterGonder sessiz-en-iyi-caba tasarimli:
            // WebAppUrl bos/Apps Script eski/ag yoksa sessizce atlar, bakimi ASLA bloklamaz/yavaslatmaz).
            // v54: bu envanter POST'u, Apps Script guncelse Log'a da gunluk hafif-bakim
            // tamamlandi olayi ekler. Ek HTTP istegi acilmaz; tek POST icinde yazilir.
            try { DriveLog.EnvanterGonder(ayar, true); } catch { }
            // v109: merkezi MakineLog (Sheet'te bu makinenin TEK satiri: hata/uyari ozeti).
            try { MakineLog.Gonder(ayar, false); } catch { }

            Logger.Write(log, "================= HAFIF BAKIM BITTI =================");
            Logger.UI("Hafif bakim tamamlandi.");
        }

        public static void AgirBakim(AppSettings ayar)
        {
            const string log = "agir.txt";
            Logger.Write(log, "================= AGIR BAKIM BASLADI =================");

            Logger.UI("[1/10] Veri koruma (Sistem Korumasi) + TEK geri yukleme noktasi...");
            // v109 (Ali Bey: "geri yukleme noktasi 1 tane yeter, eskileri silsek otomatik"): once
            // YENI nokta alinir; basariliysa eskiler silinir, tek nokta kalir. Alan %5 ile sinirli.
            try { AkilliBakim.GeriYuklemeNoktasiAl(log); } catch (Exception ex) { Logger.Write(log, "Geri yukleme noktasi hatasi: " + ex.Message); }

            Logger.UI("[2/10] Guvenlik duvari + virus programi + performans ayarlari...");
            // Gorsel efektler (tum kullanicilar), cop kutusu kapat/bosalt, yuksek performans plani,
            // firewall + AV ACIK, bozuk bakim policy temizligi + WinSAT
            try { PerformansAyar.Uygula(ayar); } catch { }
            // v109: Ag yigini sifirlama (winsock/int ip reset) OTOMATIK bakimdan CIKARILDI --
            // elle girilmis sabit IP/DNS ayarlarini silebiliyor ve yeniden baslatma istiyordu.
            // Artik sadece Bakim sekmesindeki "Ag ayarlarini sifirla" dugmesiyle, onayla yapilir
            // (bkz. AkilliBakim.AgAyarlariniSifirla).

            // v82 (Ali Bey'in istegi, 6 Kasim 2026): Windows Guncelleme indirme onbellegi
            // (eski/kullanilmis guncelleme dosyalari) bosaltilir.
            Logger.UI("[3/10] Windows Guncelleme onbellegi temizleniyor...");
            try { PerformansAyar.GuncellemeOnbellekTemizle(); } catch (Exception ex) { Logger.Write(log, "Guncelleme onbellek temizligi hatasi: " + ex.Message); }

            Logger.UI("[4/10] Gecici dosya ve onbellek temizligi...");
            TempTemizle(1);

            Logger.UI("[5/10] Windows Disk Temizleme...");
            NativeRunner.Run("schtasks.exe", "/run /tn \"\\Microsoft\\Windows\\DiskCleanup\\SilentCleanup\"", log, false);

            // v109: Agir ONARIM (RestoreHealth + SFC) artik SADECE bulgu varsa -- gunluk hafif bakimdaki
            // hizli saglik kontrolu bir sorun bulup isaret biraktiysa VEYA su anki kontrol saglikli
            // degilse. Saglikli bir sistemde her hafta 30-90 dk diski yormanin bir yarari yok.
            Logger.UI("[6/10] Windows bileseni deposu kontrolu (+ gerekiyorsa onarim)...");
            string depo = AkilliBakim.BilesenDeposuDurumu();
            bool onarim = AkilliBakim.OnarimIsaretiVar() || (depo != "Healthy" && depo != "?");
            Logger.Write(log, "Bilesen deposu: " + depo + (AkilliBakim.OnarimIsaretiVar() ? " (gunluk kontrol onarim isareti birakmis)" : ""));
            if (onarim)
            {
                NativeRunner.Run("DISM.exe", "/Online /Cleanup-Image /RestoreHealth", log, true, 1800000);
                Logger.UI("[7/10] SFC sistem dosyasi taramasi (uzun surebilir)...");
                NativeRunner.Run("sfc.exe", "/scannow", log, true, 1800000);
            }
            else Logger.Write(log, "Bilesen deposu saglikli -- RestoreHealth/SFC bu hafta ATLANDI (gereksiz disk yuku olmasin).");
            NativeRunner.Run("DISM.exe", "/Online /Cleanup-Image /StartComponentCleanup", log, true, 1800000);

            Logger.UI("[8/10] Disk optimizasyonu...");
            NativeRunner.Run("defrag.exe", "C: /O", log, true, 1800000);

            Logger.UI("[9/10] CHKDSK (sadece disk 'kirli' isaretliyse)...");
            if (AkilliBakim.DiskKirliMi() || onarim)
                NativeRunner.Run("chkdsk.exe", "C: /scan", log, true, 600000);
            else Logger.Write(log, "Disk kirli isareti yok -- CHKDSK atlandi.");
            AkilliBakim.OnarimIsaretiniSil();

            // v82 (Ali Bey'in istegi, 6 Kasim 2026): SADECE bilgi amacli -- hicbir ayari
            // degistirmez/kapatmaz, sadece log dosyasina (agir.txt) yazar.
            Logger.UI("[10/10] Sistem bilgisi + disk sagligi + baslangic programlari raporu...");
            try { SistemBilgisiRaporuYaz(log); } catch (Exception ex) { Logger.Write(log, "Sistem bilgisi raporu hatasi: " + ex.Message); }
            try { SmartDiskSagligiRaporu(log); } catch (Exception ex) { Logger.Write(log, "Disk sagligi raporu hatasi: " + ex.Message); }
            try { BaslangicProgramlariRaporu(log); } catch (Exception ex) { Logger.Write(log, "Baslangic programlari raporu hatasi: " + ex.Message); }

            DiskRaporu(log);
            try { AkilliBakim.DiskKritikDonguKontrol(); } catch { }
            try { MakineLog.Gonder(ayar, false); } catch { }
            Logger.Write(log, "================= AGIR BAKIM BITTI =================");
            Logger.UI("Agir bakim tamamlandi.");
        }

        /// <summary>v109: disk acil temizligi icin disaridan erisim.</summary>
        public static long TempTemizleHerkes(int gun) => TempTemizle(gun);

        // TEMP: Windows\Temp + her kullanici profilinin AppData\Local\Temp (gunden eski)
        private static long TempTemizle(int gun)
        {
            long bayt = 0;
            DateTime sinir = DateTime.Now.AddDays(-gun);
            bayt += KlasorTemizle(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"), sinir);
            try
            {
                string users = Path.GetPathRoot(Environment.SystemDirectory) + "Users";
                if (Directory.Exists(users))
                    foreach (var prof in Directory.GetDirectories(users))
                        bayt += KlasorTemizle(Path.Combine(prof, "AppData", "Local", "Temp"), sinir);
            }
            catch { }
            return bayt / 1024 / 1024;
        }

        private static long KlasorTemizle(string yol, DateTime sinir)
        {
            long bayt = 0;
            if (!Directory.Exists(yol)) return 0;
            try
            {
                foreach (var f in Directory.GetFiles(yol, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        var fi = new FileInfo(f);
                        if (fi.LastWriteTime < sinir)
                        {
                            long b = fi.Length;
                            fi.Delete();
                            bayt += b;
                        }
                    }
                    catch { }
                }
            }
            catch { }
            return bayt;
        }

        private static void DiskRaporu(string log)
        {
            try
            {
                foreach (var d in DriveInfo.GetDrives())
                {
                    if (d.DriveType != DriveType.Fixed || !d.IsReady) continue;
                    double yuzde = 100.0 * d.AvailableFreeSpace / d.TotalSize;
                    string s = "Surucu " + d.Name + " bos: " + Math.Round(d.AvailableFreeSpace / 1e9, 1) + " GB / " + Math.Round(d.TotalSize / 1e9, 1) + " GB (%" + Math.Round(yuzde, 0) + ")";
                    if (yuzde < 10) s = "[UYARI] " + s + " - disk dolmak uzere";
                    Logger.Write(log, s);
                }
            }
            catch { }
        }

        // ---------------------------------------------------------------
        // v82 (Ali Bey'in istegi, 6 Kasim 2026): SADECE BILGI amacli raporlar -- ucu de
        // hicbir ayari degistirmez/kapatmaz/silmez, sadece agir.txt log dosyasina yazar.
        // ---------------------------------------------------------------

        /// <summary>SystemInfo.Topla()'nin (CPU/RAM/disk/Windows/lisans) cikardigi hazir metni
        /// haftalik agir bakim log'una da yazar. SystemInfo.Topla() KENDISI DEGISTIRILMEDI --
        /// panelin "Sistem bilgisini goster" butonu bu fonksiyonu zaten kullaniyor, bozma riski
        /// alinmadi, burada sadece AYNI ciktinin log'a da dusmesi saglandi.</summary>
        private static void SistemBilgisiRaporuYaz(string log)
        {
            string metin = SystemInfo.Topla();
            foreach (var satir in metin.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
                Logger.Write(log, satir);
        }

        /// <summary>Disk SMART sagligi (arizaya gidip gitmedigi) -- erken uyari icin. SystemInfo.Topla()
        /// sadece disk modeli/boyutunu veriyordu, saglik durumunu HIC vermiyordu; bu yuzden ayri.</summary>
        private static void SmartDiskSagligiRaporu(string log)
        {
            Logger.Write(log, "--- Disk sagligi (SMART) ---");
            NativeRunner.Run("powershell.exe",
                "-NoProfile -ExecutionPolicy Bypass -Command \"Get-PhysicalDisk | Select-Object FriendlyName,HealthStatus,OperationalStatus | Format-Table -AutoSize | Out-String -Width 200\"",
                log, true, 30000);
        }

        /// <summary>Acilista otomatik baslayan programlarin listesi -- acilisi yavaslatan bir
        /// program varsa gorulebilsin diye. HICBIR PROGRAM KAPATILMAZ/KALDIRILMAZ, sadece listelenir.</summary>
        private static void BaslangicProgramlariRaporu(string log)
        {
            Logger.Write(log, "--- Baslangic programlari ---");
            NativeRunner.Run("powershell.exe",
                "-NoProfile -ExecutionPolicy Bypass -Command \"Get-CimInstance Win32_StartupCommand | Select-Object Name,Command,Location | Format-Table -AutoSize | Out-String -Width 200\"",
                log, true, 30000);
        }
    }
}
