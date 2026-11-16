using System;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;

namespace UnsalBakim.Core
{
    /// <summary>
    /// v94 (Ali Bey'in istegi: "buyuk ekran mesaji"): "Filo'dan Kapat" ile BIREBIR AYNI TEK-
    /// SEFERLIK komut deseni (bkz. FiloKapat.cs'in basindaki tasarim notu).
    ///
    /// TEKNIK KISIT (Ali Bey'e panelde/kilavuzda acikca anlatilir): arka plan gorevleri SYSTEM
    /// hesabiyla "Oturum 0" denen GORUNMEZ bir oturumda calisir -- bu yuzden sıradan bir WinForms
    /// penceresi ACAMAYIZ (ogrencinin ekraninda hic gorunmez). Bunun yerine Windows'un KENDI
    /// "WTSSendMessage" API'si kullanilir (eski "net send"/msg.exe'nin dayandigi AYNI mekanizma)
    /// -- SYSTEM surecinden cagrilsa bile, dogrudan AKTIF oturumun (fiziksel konsol) ekraninda
    /// buyukce, her zaman ustte bir sistem diyalog kutusu olarak belirir ve ogrenci TAMAM demeden
    /// kapanmaz. Gercek "tam ekran ozel pencere" DEGILDIR (Ali Bey'in bilerek sectigi basit/
    /// guvenilir secenek) -- ama goz ardi edilemez, buyukce ve kalici bir uyaridir.
    /// </summary>
    public static class FiloMesajGonder
    {
        private const string Log = "filo_mesaj_gonder.txt";

        private static string OnbellekYolu =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "UnsalBakim", "mesaj_son_uygulanan.txt");

        [DllImport("kernel32.dll")]
        private static extern int WTSGetActiveConsoleSessionId();

        [DllImport("wtsapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool WTSSendMessage(IntPtr hServer, int sessionId, string pTitle, int titleLength,
            string pMessage, int messageLength, int style, int timeoutSaniye, out int pResponse, bool bekle);

        private const int MB_OK = 0x00000000;
        private const int MB_ICONINFORMATION = 0x00000040;
        private const int MB_TOPMOST = 0x00040000;
        private const int MB_SETFOREGROUND = 0x00010000;

        /// <summary>PANELDEN cagrilir: hedefteki makine(ler)e yeni bir "mesaj goster" komutu
        /// gonderir. Gercek satir sonlari '\n' (iki karakter, ters taksim+n) olarak, '|' karakteri
        /// ise '/' ile DEGISTIRILEREK gonderilir (Sheet'teki alan ayiraci '|' oldugu icin).</summary>
        public static string FiloBaslat(string hedef, string mesaj)
        {
            if (string.IsNullOrWhiteSpace(Musteri.WebAppUrl))
                return "Drive log ozelligi kurulmadi (WebAppUrl bos); yazilamadi.";
            if (string.IsNullOrWhiteSpace(mesaj))
                return "Mesaj metni bos birakilamaz.";
            try
            {
                string zaman = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                string mesajKacisli = Kacis(mesaj);
                string json = "{\"tip\":\"mesaj\",\"token\":\"" + Kacir(Musteri.ApiToken) + "\",\"musteriId\":\"" +
                              Kacir(Musteri.MusteriId) + "\",\"hedef\":\"" + Kacir(string.IsNullOrWhiteSpace(hedef) ? "TUMU" : hedef.Trim()) +
                              "\",\"mesaj\":\"" + Kacir(mesajKacisli) + "\",\"zaman\":\"" + zaman +
                              "\",\"guncelleyen\":\"" + Kacir(Environment.UserName + "@" + Environment.MachineName) + "\"}";
                try { ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; } catch { }
                string yanit;
                using (var wc = new System.Net.WebClient())
                {
                    wc.Headers["Content-Type"] = "application/json";
                    yanit = wc.UploadString(Musteri.WebAppUrl, "POST", json);
                }
                if (yanit != null && yanit.Trim() == "OK")
                    return "Mesaj gonderildi (hedef: " + hedef + "). Hedefteki makine(ler) en gec ~5 dakika icinde ekranda gosterecek.";
                return "Yazilamadi, Apps Script yaniti: '" + yanit + "' (script guncel olmayabilir).";
            }
            catch (Exception ex) { return "Yazilamadi: " + ex.Message; }
        }

        /// <summary>v57 (3. revizyon) deseniyle AYNI: FiloDurum.Al'in birlesik istegiyle ONCEDEN
        /// alinmis degerler uzerinde calisir -- kendi ag istegini atmaz.</summary>
        public static void FiloKontrolEtVeUygulaDeger(FiloDurumToplu toplu)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(toplu.MesajZaman)) return;

                string yerelZaman = OnbellekOku();
                if (string.Equals(toplu.MesajZaman, yerelZaman, StringComparison.Ordinal)) return; // zaten gosterildi

                OnbellekYaz(toplu.MesajZaman); // once damgayi yaz, sonra goster (FiloKapat.cs ile AYNI sira)

                string mesaj = KacisGeriAl(toplu.MesajMetni ?? "");
                Logger.Write(Log, "Filo mesaji alindi (" + toplu.MesajZaman + ") -- ekranda gosteriliyor: " +
                                  (mesaj.Length > 80 ? mesaj.Substring(0, 80) + "..." : mesaj));

                int oturumId = WTSGetActiveConsoleSessionId();
                if (oturumId < 0)
                {
                    Logger.Write(Log, "Aktif konsol oturumu bulunamadi (kimse giris yapmamis olabilir) -- mesaj gosterilemedi.");
                    return;
                }
                int yanitKodu;
                bool basarili = WTSSendMessage(IntPtr.Zero, oturumId, "UNSAL BAKIM - Duyuru", "UNSAL BAKIM - Duyuru".Length,
                    mesaj, mesaj.Length, MB_OK | MB_ICONINFORMATION | MB_TOPMOST | MB_SETFOREGROUND,
                    0, out yanitKodu, false);
                if (!basarili)
                    Logger.WriteFileOnly(Log, "WTSSendMessage basarisiz (hata kodu: " + Marshal.GetLastWin32Error() + ").");
            }
            catch (Exception ex) { Logger.WriteFileOnly(Log, "Filo mesaj kontrolu hatasi: " + ex.Message); }
        }

        /// <summary>Gercek satir sonlarini (\r\n / \n) iki karakterlik '\n' isaretine, '|' karakterini
        /// '/' ile degistirir (Sheet alan ayiraci '|' oldugu icin kayip olmadan tasinamaz).</summary>
        private static string Kacis(string s) => (s ?? "").Replace("\r\n", "\n").Replace("\n", "\\n").Replace("|", "/");

        private static string KacisGeriAl(string s) => (s ?? "").Replace("\\n", Environment.NewLine);

        private static string OnbellekOku()
        {
            try { return File.Exists(OnbellekYolu) ? File.ReadAllText(OnbellekYolu).Trim() : null; }
            catch { return null; }
        }

        private static void OnbellekYaz(string zaman)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(OnbellekYolu));
                File.WriteAllText(OnbellekYolu, zaman ?? "");
            }
            catch { }
        }

        private static string Kacir(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
