using System;
using System.Collections.Generic;
using System.IO;

namespace UnsalBakim.Core
{
    /// <summary>
    /// Engellenen/yakalanan oyunlari yapilandirilmis (sutunlu) olarak kaydeder:
    ///   tarih  saat  oyun_adi  kategori(online/kurulu)  yontem
    /// Menudeki "Gunluk / Haftalik engellenen oyunlar" raporlari bu dosyadan beslenir.
    /// </summary>
    public static class EngelKayit
    {
        private static readonly object _k = new object();
        private static string Dosya => Path.Combine(Logger.LogDir, "engellenen.tsv");

        // Kategori ayrimi: internetten/çevrim içi oynanan tipik oyunlar "online", digerleri "kurulu".
        private static readonly string[] OnlineAnahtar =
        {
            "roblox","valorant","fortnite","league","pubg","warzone","apex","among us",
            "fall guys","genshin","brawl","tlauncher","point blank","zula","wolfteam",
            "knight online","metin2","now.gg","poki","crazygames","friv","y8","miniclip",
            "kizi","coolmath","oyunskor","kraloyun","oyunlar1","oyunoyna","oyunkolu",
            "minecraft","gameloop","bluestacks"
        };

        public static string Kategori(string ad)
        {
            string s = (ad ?? "").ToLowerInvariant();
            foreach (var k in OnlineAnahtar) if (s.Contains(k)) return "online";
            return "kurulu";
        }

        /// <summary>Bir engelleme/yakalama olayini kaydeder. yontem: surec-kapatma / usb / kaldirma / gecmis-izi.</summary>
        public static void Ekle(string ad, string yontem)
        {
            string temizAd = (ad ?? "").Replace("\t", " ").Replace("\r", " ").Replace("\n", " ").Trim();
            string kategori = Kategori(temizAd);
            try
            {
                Directory.CreateDirectory(Logger.LogDir);
                string sat = DateTime.Now.ToString("yyyy-MM-dd") + "\t" + DateTime.Now.ToString("HH:mm:ss")
                           + "\t" + temizAd + "\t" + kategori + "\t" + (yontem ?? "");
                lock (_k) { File.AppendAllText(Dosya, sat + Environment.NewLine); }
            }
            catch { }

            // v42: Bu makineye ozel (yerel diskteki) kayda ek olarak, ayni olayi DriveLog uzerinden
            // merkezi Google Sheet'e de gonder -- boylece "Guncelleme gecmisini goster" sayfasi artik
            // SADECE guncelleme/sifre olaylarini degil, TUM makinelerdeki engellenen oyunlari da TEK
            // tabloda gosterir (Ali Bey'in istedigi "tum kullanicilarin merkezi raporu"). DriveLog.Gonder
            // zaten WebAppUrl bos ise veya ag hatasi olursa sessizce no-op/try-catch icinde; burada ayrica
            // arka plan threadinde (ThreadPool) cagriliyor ki ag yavassa 10 dakikalik oyun denetim
            // dongusunu (GameControl.SurecKapat vb.) BEKLETMESIN.
            try
            {
                string temizAdYakin = temizAd; string yontemYakin = yontem; string kategoriYakin = kategori;
                System.Threading.ThreadPool.QueueUserWorkItem(_ =>
                {
                    try { DriveLog.Gonder("oyun_engellendi", temizAdYakin + " | " + kategoriYakin + " | " + (yontemYakin ?? "")); }
                    catch { }
                });
            }
            catch { }
        }

        /// <summary>Son gunGeriye gun icindeki kayitlari okunur satirlar halinde dondurur (en yeni ustte).</summary>
        public static List<string> Listele(int gunGeriye)
        {
            var res = new List<string>();
            try
            {
                if (!File.Exists(Dosya)) return res;
                DateTime sinir = DateTime.Today.AddDays(-gunGeriye);
                foreach (var ln in File.ReadAllLines(Dosya))
                {
                    var p = ln.Split('\t');
                    if (p.Length < 5) continue;
                    DateTime t;
                    if (!DateTime.TryParse(p[0] + " " + p[1], out t)) continue;
                    if (t.Date < sinir) continue;
                    res.Add(p[0] + "  " + p[1] + "   |  " + p[2] + "   |  " + p[3] + "   |  " + p[4]);
                }
            }
            catch { }
            res.Reverse();
            return res;
        }
    }
}
