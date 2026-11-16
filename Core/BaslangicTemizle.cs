using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;

namespace UnsalBakim.Core
{
    /// <summary>
    /// v90 (Ali Bey'in istegi, 9 Kasim 2026: "sistem ilk acilista operada aciliyor olabilir,
    /// startup'ta gereksiz calistirmalardan arindirilmali"): Windows baslangicindaki (Run/RunOnce
    /// kayitlari + Baslangic klasorleri) bilinen istenmeyen (Opera/VPN/oyun/emulator) girdilerini
    /// AKTIF olarak SILER -- Maintenance.cs'teki BaslangicProgramlariRaporu salt-okunur bir
    /// RAPORDUR (sadece listeler), bu sinif ise GERCEKTEN TEMIZLER.
    /// NOT: Opera zaten YoutubeKisit.OperaIfeoEngelle() ile IFEO uzerinden kokten engelleniyor
    /// (startup'tan calissa bile acilamaz) -- bu sinif EK bir savunma katmani + hijyen: startup
    /// girdisinin kendisini de kaldirip gorunumu temiz tutar, ayrica IFEO listesinde olmayan
    /// (VPN, oyun/emulator) baslangic girdilerini de temizler.
    /// </summary>
    public static class BaslangicTemizle
    {
        private const string Log = "baslangic_temizle.txt";

        private static readonly string[] RunYollari =
        {
            @"Software\Microsoft\Windows\CurrentVersion\Run",
            @"Software\Microsoft\Windows\CurrentVersion\RunOnce"
        };

        /// <summary>Eslesme icin anahtar kelimeler: Opera + VpnKontrol.Surecler + bilinen
        /// oyun/emulator imzalari (OyunLogKontrol.BilinenOyunIzleri ile es-anlamli, ayri kod
        /// tabanlari arasinda dogrudan referans olmadan sadelik icin kucuk, odakli bir liste).</summary>
        private static List<string> AnahtarKelimeler()
        {
            var list = new List<string>
            {
                "opera",
                "tlauncher", "sklauncher", "gameloop", "bluestacks", "ldplayer", "noxplayer",
                "mumuplayer", "memuplayer", "ryujinx", "yuzu", "rpcs3", "pcsx2", "ppsspp",
                "retroarch", "moonlight", "parsec"
            };
            foreach (var s in VpnKontrol.Surecler) list.Add(s.ToLowerInvariant());
            return list;
        }

        /// <summary>Panelden/periyodik dongude kosulsuz cagrilir (v92: "Askiya Al" kaldirildi,
        /// dolayisiyla bu kuralin istisnasi da artik yok).</summary>
        public static int Temizle()
        {
            int silinen = 0;
            var anahtarlar = AnahtarKelimeler();

            // 1) Registry Run/RunOnce -- HKLM (tum kullanicilar icin gecerli) + HKCU (su an
            // oturum acmis kullanici). Yuklu olmayan diger kullanici profillerinin NTUSER.DAT'i
            // buradan acilmaz -- onlar her profil ilk defa oturum actiginda kendi HKCU'su
            // uzerinden bir sonraki turda temizlenir.
            foreach (var kok in new[] { Registry.LocalMachine, Registry.CurrentUser })
                foreach (var yol in RunYollari)
                    silinen += RegistryTemizle(kok, yol, anahtarlar);

            // 2) Baslangic klasorleri -- ortak (tum kullanicilar) + su anki kullanici.
            silinen += KlasorTemizle(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), anahtarlar);
            silinen += KlasorTemizle(Environment.GetFolderPath(Environment.SpecialFolder.Startup), anahtarlar);

            if (silinen > 0) Logger.WriteFileOnly(Log, "Baslangictan temizlenen girdi sayisi: " + silinen + ".");
            return silinen;
        }

        private static int RegistryTemizle(RegistryKey kok, string yol, List<string> anahtarlar)
        {
            int n = 0;
            try
            {
                using (var k = kok.OpenSubKey(yol, true))
                {
                    if (k == null) return 0;
                    foreach (var ad in k.GetValueNames())
                    {
                        try
                        {
                            string deger = (k.GetValue(ad) as string ?? "").ToLowerInvariant();
                            if (Eslesir(deger, anahtarlar) || Eslesir(ad.ToLowerInvariant(), anahtarlar))
                            {
                                k.DeleteValue(ad, false);
                                Logger.WriteFileOnly(Log, "Baslangic kaydi silindi (" + kok.Name + "\\" + yol + "): " + ad + " = " + deger);
                                n++;
                            }
                        }
                        catch (Exception ex) { Logger.WriteFileOnly(Log, "Kayit silinemedi (" + ad + "): " + ex.Message); }
                    }
                }
            }
            catch (Exception ex) { Logger.WriteFileOnly(Log, yol + " okunamadi: " + ex.Message); }
            return n;
        }

        private static int KlasorTemizle(string klasor, List<string> anahtarlar)
        {
            int n = 0;
            try
            {
                if (string.IsNullOrEmpty(klasor) || !Directory.Exists(klasor)) return 0;
                foreach (var dosya in Directory.GetFiles(klasor))
                {
                    try
                    {
                        string ad = Path.GetFileName(dosya).ToLowerInvariant();
                        if (Eslesir(ad, anahtarlar))
                        {
                            File.Delete(dosya);
                            Logger.WriteFileOnly(Log, "Baslangic klasorunden silindi: " + dosya);
                            n++;
                        }
                    }
                    catch (Exception ex) { Logger.WriteFileOnly(Log, "Dosya silinemedi (" + dosya + "): " + ex.Message); }
                }
            }
            catch (Exception ex) { Logger.WriteFileOnly(Log, klasor + " okunamadi: " + ex.Message); }
            return n;
        }

        private static bool Eslesir(string metin, List<string> anahtarlar)
        {
            if (string.IsNullOrEmpty(metin)) return false;
            foreach (var a in anahtarlar)
                if (!string.IsNullOrEmpty(a) && metin.Contains(a)) return true;
            return false;
        }
    }
}
