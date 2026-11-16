using System;
using System.IO;

namespace UnsalBakim.Core
{
    /// <summary>
    /// v110 -- UZAKTAN ACIL DURUM ANAHTARI. Ali Bey okuldan uzaktayken v109'un ogrenci korumalari
    /// (AppLocker, Program Files / ProgramData klasor kilitleri) bir ders yazilimini bozarsa,
    /// panelin Filo sekmesinden "Korumayi uzaktan KAPAT" ile hedef makinelerde bu korumalar
    /// en gec ~5 dk icinde GERI ALINIR. "Normale dondur" ile tekrar uygulanir.
    ///
    /// Durum Sheet'teki "KorumaAcil" sayfasinda TUTULUR (tek seferlik degil): makine kapaliyken
    /// verilse bile acildiginda uygular. Internet/Apps Script yanit vermezse HICBIR SEY
    /// degismez (son bilinen durum korunur) -- bir ag sorunu korumayi ne acar ne kapatir.
    ///
    /// Etkilemedikleri: kullanici gruplari, oyun/VPN/site engelleri, sinav modu (onlar ayri).
    /// </summary>
    public static class KorumaAcil
    {
        private const string Log = "rol_guvenlik.txt";

        private static string IsaretYolu =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "UnsalBakim", "koruma_acil_kapali.txt");

        public static bool Aktif
        {
            get { try { return File.Exists(IsaretYolu); } catch { return false; } }
        }

        /// <summary>kapali: sunucudan gelen deger (true = KAPALI istendi). agBasarili=false ise hicbir sey yapilmaz.</summary>
        public static void Isle(bool kapali, bool agBasarili, AppSettings ayar)
        {
            if (!agBasarili) return;
            if (!RolGuvenlik.YetkiliBaglam()) return;
            try
            {
                if (kapali && !Aktif)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(IsaretYolu));
                    File.WriteAllText(IsaretYolu, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    Logger.WriteFileOnly(Log, "KORUMA ACIL KAPATMA alindi (Filo): AppLocker ve klasor kilitleri geri aliniyor.");
                    KorumalariGeriAl();
                    try { DriveLog.Gonder("koruma_acil_kapali", "Filo'dan: AppLocker + klasor kilitleri geri alindi."); } catch { }
                }
                else if (!kapali && Aktif)
                {
                    try { File.Delete(IsaretYolu); } catch { }
                    Logger.WriteFileOnly(Log, "KORUMA normale donduruldu (Filo): korumalar yeniden uygulaniyor.");
                    if (ayar != null) RolGuvenlik.DonguKontrol(ayar, true);
                    try { DriveLog.Gonder("koruma_normal", "Filo'dan: korumalar yeniden uygulandi."); } catch { }
                }
            }
            catch (Exception ex) { Logger.WriteFileOnly(Log, "[!] Koruma acil anahtari hatasi: " + ex.Message); }
        }

        private static void KorumalariGeriAl()
        {
            try { AppLockerYonet.Kaldir(); } catch (Exception ex) { Logger.WriteFileOnly(Log, "[!] AppLocker kaldirma hatasi: " + ex.Message); }
            try { RolGuvenlik.ProgramDataKilidiKaldir(); } catch (Exception ex) { Logger.WriteFileOnly(Log, "[!] ProgramData geri alma hatasi: " + ex.Message); }
            try
            {
                string klasor = Installer.HedefKlasor;
                if (Directory.Exists(klasor))
                    NativeRunner.Run("icacls.exe", "\"" + klasor + "\" /reset /T /C /Q", Log, false);
            }
            catch (Exception ex) { Logger.WriteFileOnly(Log, "[!] Program Files geri alma hatasi: " + ex.Message); }
        }
    }
}
