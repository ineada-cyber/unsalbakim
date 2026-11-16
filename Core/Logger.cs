using System;
using System.IO;

namespace UnsalBakim.Core
{
    /// <summary>Basit dosya + arayuz logu. ProgramData altina yazar.</summary>
    public static class Logger
    {
        public static string LogDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "UnsalBakim", "LOG");

        private static readonly object _lock = new object();

        /// <summary>Arayuze satir gondermek icin UI tarafindan atanir.</summary>
        public static Action<string> OnLine;

        public static void Init()
        {
            try { Directory.CreateDirectory(LogDir); } catch { }
        }

        public static void Write(string dosya, string mesaj) => WriteCore(dosya, mesaj, true);

        /// <summary>Yalnizca dosyaya yazar; arayuz akisina gondermez. Arka plan oyun denetiminde kullanilir.</summary>
        public static void WriteFileOnly(string dosya, string mesaj) => WriteCore(dosya, mesaj, false);

        private static void WriteCore(string dosya, string mesaj, bool arayuzeGonder)
        {
            // Panelde gosterilen satir: kisa, makine adi gerekmez (zaten o makinenin panelindesin).
            string satirUi = "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + mesaj;
            // v101 (Ali Bey'in istegi, 11 Kasim 2026: "makina loglarini bilgisayar adi ve log kaydi
            // ayni formatta olsun, sonucta bu loglardan sorunu ben cozecegim"): DOSYAYA yazilan
            // satir ayrica makine adini da icerir. Boylece Ali Bey birden fazla makineden topladigi
            // LOG klasorlerini (ayni dosya adlari: oyun_log.txt, baslangic_temizle.txt vs.) birlestirip
            // tek seferde paylastiginda, her satirin HANGI makineye ait oldugu satirin KENDISINDEN
            // anlasilir -- klasor/dosya adina bakmaya gerek kalmaz. Mevcut okuyuculardan hicbiri
            // (EngelKayit, YapayZeka) satiri POZISYONEL parse etmiyor, sadece Contains() ile iceri
            // baktigi icin bu ekleme onlari BOZMAZ.
            string satirDosya = "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] [" + Environment.MachineName + "] " + mesaj;
            try
            {
                lock (_lock)
                {
                    Directory.CreateDirectory(LogDir);
                    File.AppendAllText(Path.Combine(LogDir, dosya), satirDosya + Environment.NewLine);
                }
            }
            catch { }
            if (arayuzeGonder) OnLine?.Invoke(satirUi);
        }

        public static void UI(string mesaj)
        {
            OnLine?.Invoke(mesaj);
        }
    }
}
