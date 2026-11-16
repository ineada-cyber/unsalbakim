using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace UnsalBakim.Core
{
    /// <summary>
    /// Standart (yonetici OLMAYAN) yerel kullanici hesaplari olusturur.
    /// Adlar bir kez USB'ye (exe yaninda kullanicilar.txt) kaydedilir; her bilgisayarda
    /// "Olustur" denince bu hesaplar acilir. "net user ... /add" hesabi otomatik olarak
    /// yerel Kullanicilar (Users) grubuna ekler -> standart kullanici olur (yonetici degil).
    /// </summary>
    public static class Kullanici
    {
        private const string Log = "kullanici.txt";

        private static string DosyaYolu =>
            Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "kullanicilar.txt");

        /// <summary>USB'deki listeyi yukler. out parola = ortak parola (gizli saklanir).</summary>
        public static List<string> Yukle(out string parola)
        {
            parola = "";
            var liste = new List<string>();
            try
            {
                if (!File.Exists(DosyaYolu)) return liste;
                foreach (var ln in File.ReadAllLines(DosyaYolu))
                {
                    var s = ln.Trim();
                    if (s.Length == 0) continue;
                    if (s.StartsWith("#parola_sifreli="))
                    {
                        parola = Yetki.SifreCoz(s.Substring("#parola_sifreli=".Length)) ?? "";
                        continue;
                    }
                    if (s.StartsWith("#")) continue;
                    liste.Add(s);
                }
            }
            catch { }
            return liste;
        }

        /// <summary>Listeyi + ortak parolayi USB'ye (exe yanina) kaydeder. Parola gizli yazilir.</summary>
        public static string Kaydet(IEnumerable<string> adlar, string parola)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# UNSAL BAKIM - olusturulacak standart kullanicilar (her satir bir ad)");
            sb.AppendLine("#parola_sifreli=" + Yetki.SifreGizle(parola ?? ""));
            foreach (var a in adlar)
            {
                var ad = (a ?? "").Trim();
                if (ad.Length > 0) sb.AppendLine(ad);
            }
            File.WriteAllText(DosyaYolu, sb.ToString());
            return DosyaYolu;
        }

        /// <summary>v80 (paket denetimi bulgusu P1-07'nin bir parcasi: "mevcut hesaplarda kod
        /// parola guncelleme yoluna da giriyor; toplu islem beklenmeyen mevcut hesap parolasini
        /// degistirebilir"): "OLUSTUR" basilmadan ONCE, listedeki adlardan HANGILERININ bu
        /// makinede ZATEN var oldugunu doner -- caller (MainForm) bunlari gosterip parolalarinin
        /// YENIDEN YAZILACAGINI acikca sorabilsin diye. Hicbir sey DEGISTIRMEZ, sadece sorgular
        /// ("net user <ad>" -- hesap varsa cikis kodu 0, yoksa 2).</summary>
        public static List<string> ZatenVarOlanlar(IEnumerable<string> adlar)
        {
            var sonuc = new List<string>();
            foreach (var raw in adlar)
            {
                string ad = (raw ?? "").Trim();
                if (ad.Length == 0 || ad.Length > 20) continue;
                try
                {
                    int rc = NativeRunner.Run("net.exe", "user \"" + ad + "\"", Log, false);
                    if (rc == 0) sonuc.Add(ad);
                }
                catch { }
            }
            return sonuc;
        }

        /// <summary>Verilen adlari standart kullanici olarak olusturur. Donus: olusturulan sayi.</summary>
        public static int Olustur(IEnumerable<string> adlar, string parola, out string rapor) => Olustur(adlar, parola, false, out rapor);

        /// <summary>v109: rol (ogretmen/ogrenci) ile. Hesap olusturulur/guncellenir, ardindan
        /// RolGuvenlik.RolAta ile UB_Ogretmen veya UB_Ogrenci grubuna tasinir ve yonetici ise
        /// standart kullaniciya dusurulur ("yonetici", yerlesik Administrator ve paneli
        /// calistiran hesap HARIC).</summary>
        public static int Olustur(IEnumerable<string> adlar, string parola, bool ogretmen, out string rapor)
        {
            var sb = new StringBuilder();
            int n = 0;
            int islenen = 0; // v75: hem YENI hem de ZATEN VAR olan hesaplari sayar (kisayol kosulu icin)
            string p = parola ?? "";
            foreach (var raw in adlar)
            {
                string ad = (raw ?? "").Trim();
                if (ad.Length == 0) continue;
                if (ad.Length > 20) { sb.AppendLine("  [ATLANDI] " + ad + " (ad 20 karakterden uzun)"); continue; }
                islenen++;

                // Hesabi olustur (net user varsayilan olarak Kullanicilar/Users grubuna ekler = standart)
                int rc = NativeRunner.Run("net.exe",
                    "user \"" + ad + "\" \"" + p + "\" /add /expires:never /passwordchg:no", Log, false);

                if (rc == 0)
                {
                    // Parola suresiz + hesap aktif (dil bagimsiz PowerShell)
                    NativeRunner.Run("powershell.exe",
                        "-NoProfile -ExecutionPolicy Bypass -Command \"Set-LocalUser -Name '" + ad +
                        "' -PasswordNeverExpires $true -ErrorAction SilentlyContinue; " +
                        "Enable-LocalUser -Name '" + ad + "' -ErrorAction SilentlyContinue\"", Log, false);
                    sb.AppendLine("  [OLUSTURULDU] " + ad + " -- " + RolGuvenlik.RolAta(ad, ogretmen));
                    n++;
                }
                else
                {
                    // Zaten varsa: parolayi guncelle + aktif et, yine standart birak
                    NativeRunner.Run("net.exe", "user \"" + ad + "\" \"" + p + "\" /expires:never", Log, false);
                    NativeRunner.Run("powershell.exe",
                        "-NoProfile -ExecutionPolicy Bypass -Command \"Set-LocalUser -Name '" + ad +
                        "' -PasswordNeverExpires $true -ErrorAction SilentlyContinue; " +
                        "Enable-LocalUser -Name '" + ad + "' -ErrorAction SilentlyContinue\"", Log, false);
                    sb.AppendLine("  [ZATEN VAR - guncellendi] " + ad + " -- " + RolGuvenlik.RolAta(ad, ogretmen));
                }
            }
            rapor = sb.ToString();
            Logger.Write(Log, "Kullanici olusturma: " + n + " yeni hesap.");

            // v75 (Ali Bey'in istegi: "Menu kurulumdan ogretmen kullanici yaratilirken ilgili
            // makinanin masa ustu, kontrol panelinde menu kisa yolu olusturulsun, pakete uygun
            // kisa yol ikonu koy"). Yeni olusturulan STANDART (ogretmen) hesaplarin Windows
            // profil klasoru (C:\Users\<ad>) ilk GIRISTE olusur -- hesap net user /add ile
            // olusturulur olusturulmaz henuz YOKTUR. Bu yuzden kisa yolu kullaniciya ozel
            // Desktop/Start Menu klasorune degil, TUM kullanicilarin gordugu ORTAK (Public
            // Desktop + Common Start Menu Programs) klasorlerine -- bu makinada bir KERE --
            // koyuyoruz; yeni hesap ilk girdiginde kisa yol zaten orada olur.
            // v75 DUZELTME: ilk surumde SADECE n>0 (yani YENI olusturulan) iken kisayol
            // kuruluyordu -- hesap zaten varsa (ZATEN VAR - guncellendi) atlaniyordu. "OLUSTUR"
            // butonuna hesap zaten varken basildiginda da kisayol kurulsun diye artik "islenen"
            // (listedeki GECERLI her ad, yeni ya da var) kosulu kullaniliyor.
            // v109: kisayol SADECE ogretmen makinesinde (MakineTuru=ogretmen) -- v108'de bu satir
            // ogrenci makinesinde hesap olusturulunca da ortak masaustune UNSAL BAKIM kisayolu
            // koyuyordu (ogrenci de goruyordu).
            if (islenen > 0)
            {
                try
                {
                    var ay = new AppSettings(); ay.Load();
                    if (RolGuvenlik.OgretmenMakinesi(ay)) { KisayolOlustur(out string kisayolMesaj); sb.AppendLine(kisayolMesaj); }
                    else sb.AppendLine("  [KISAYOL] Ogrenci makinesi -- UNSAL BAKIM kisayolu BILEREK konmadi.");
                }
                catch { }
            }
            rapor = sb.ToString();
            return n;
        }

        /// <summary>v75: UNSAL BAKIM panelinin kisa yolunu bu makinanin ORTAK masaustune
        /// (Public Desktop) ve ORTAK Baslat menusune (Common Start Menu Programs) koyar --
        /// boylece ogretmen hesaplari dahil TUM kullanicilar gorur, hesap ilk kez girse de
        /// kisa yol zaten oradadir. Ikon: exe'nin kendi ikonu (pakete gomulu ne ise o -- ileride
        /// exe'nin ikonu degisirse kisa yol otomatik guncel ikonu gosterir). Zaten varsa
        /// sessizce UZERINE YAZAR (tekrar tekrar calistirmak zararsiz).</summary>
        public static bool KisayolOlustur(out string mesaj)
        {
            try
            {
                string exeYolu = Assembly.GetExecutingAssembly().Location;
                string publicMasaustu = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
                string ortakBaslat = Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);
                string hedef1 = Path.Combine(publicMasaustu, "UNSAL BAKIM.lnk");
                string hedef2 = Path.Combine(ortakBaslat, "UNSAL BAKIM.lnk");

                string psKomut =
                    "$ws = New-Object -ComObject WScript.Shell; " +
                    "foreach ($h in @('" + hedef1.Replace("'", "''") + "','" + hedef2.Replace("'", "''") + "')) { " +
                    "  $sc = $ws.CreateShortcut($h); " +
                    "  $sc.TargetPath = '" + exeYolu.Replace("'", "''") + "'; " +
                    "  $sc.WorkingDirectory = '" + Path.GetDirectoryName(exeYolu).Replace("'", "''") + "'; " +
                    "  $sc.IconLocation = '" + exeYolu.Replace("'", "''") + ",0'; " +
                    "  $sc.Description = 'UNSAL BAKIM'; " +
                    "  $sc.Save() " +
                    "}";
                int rc = NativeRunner.Run("powershell.exe",
                    "-NoProfile -ExecutionPolicy Bypass -Command \"" + psKomut.Replace("\"", "\\\"") + "\"",
                    Log, false);
                if (rc == 0)
                {
                    mesaj = "  [KISAYOL] Masaustu + Baslat menusu kisa yolu olusturuldu/guncellendi (tum kullanicilar icin ortak).";
                    return true;
                }
                mesaj = "  [KISAYOL] Olusturulamadi (cikis kodu: " + rc + ").";
                return false;
            }
            catch (Exception ex)
            {
                mesaj = "  [KISAYOL] Olusturulamadi: " + ex.Message;
                return false;
            }
        }
    }
}
