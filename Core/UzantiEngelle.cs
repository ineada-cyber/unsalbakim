using Microsoft.Win32;

namespace UnsalBakim.Core
{
    /// <summary>
    /// Chrome/Edge TARAYICI UZANTILARINI (extension) tamamen engeller. Bazi oyunlar artik
    /// web sitesi degil, dogrudan tarayici uzantisi olarak dagitiliyor (SiteEngelle/YoutubeKisit
    /// bunu kapsamaz, cunku bunlar URL degil, chrome-extension:// ad alaninda calisir).
    ///
    /// "ExtensionInstallBlocklist" = ["*"] Chromium politikasinda SADECE yeni kurulumu
    /// engellemekle kalmaz -- Google'in kendi belgelerine gore ZATEN KURULU olup bu listeye
    /// giren (joker karakterle TUMU) uzantilari da OTOMATIK DEVRE DISI birakir/kaldirir.
    /// Yani "kurulmusu da kaldır" istegi bu TEK politika ile karsilanir, ayrica uninstall
    /// komutu calistirmaya gerek yok.
    /// </summary>
    public static class UzantiEngelle
    {
        private const string Log = "oyun.txt";

        public static void Engelle()
        {
            try { Yaz(@"SOFTWARE\Policies\Google\Chrome"); } catch { }
            try { Yaz(@"SOFTWARE\Policies\Microsoft\Edge"); } catch { }
            Logger.WriteFileOnly(Log, "Tarayici uzantisi (extension) kurulumu tamamen engellendi (mevcutlar da devre disi).");
        }

        public static void KaldirEngel()
        {
            try { Sil(@"SOFTWARE\Policies\Google\Chrome"); } catch { }
            try { Sil(@"SOFTWARE\Policies\Microsoft\Edge"); } catch { }
            Logger.WriteFileOnly(Log, "Tarayici uzantisi engeli kaldirildi.");
        }

        private static void Yaz(string polYol)
        {
            using (var k = Registry.LocalMachine.CreateSubKey(polYol + @"\ExtensionInstallBlocklist"))
                k?.SetValue("1", "*", RegistryValueKind.String);
        }

        private static void Sil(string polYol)
        {
            using (var k = Registry.LocalMachine.OpenSubKey(polYol + @"\ExtensionInstallBlocklist", true))
                k?.DeleteValue("1", false);
        }
    }
}
