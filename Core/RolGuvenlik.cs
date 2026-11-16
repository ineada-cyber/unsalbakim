using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Principal;
using System.Text;

namespace UnsalBakim.Core
{
    /// <summary>
    /// v109 (Ali Bey'in istegi: "c:\ unsalbakim dosyalari gorunuyor, ogrenci okumasin, exeleri
    /// calistirmasin"): ogrenciye KAPALI, ogretmene ACIK yapinin cekirdegi.
    ///
    ///  1) Iki yerel Windows grubu: UB_Ogrenci ve UB_Ogretmen. Ikisi de STANDART kullanicidir
    ///     (hicbiri yonetici yetkisi vermez) -- sadece "kim ogrenci, kim ogretmen" etiketidir;
    ///     AppLocker ve klasor izinleri bu etikete gore uygulanir.
    ///  2) OTOMATIK ROL ATAMASI (guvenli gecis): v109 otomatik guncellemeyle TUM makinelere ayni
    ///     anda gelir. Mevcut hesaplar henuz hicbir gruba konmamis olacagi icin, saatte bir:
    ///       - OGRENCI makinesinde: hicbir UB grubunda olmayan standart (yonetici olmayan) hesaplar
    ///         UB_Ogrenci'ye eklenir;
    ///       - OGRETMEN makinesinde: hicbir UB grubunda olmayan standart hesaplar UB_Ogretmen'e
    ///         eklenir.
    ///     Yoneticiler, "yonetici" hesabi ve yerlesik hesaplar (RID 500-504) HIC ellenmez.
    ///     Bir hesabi elle baska role tasimak icin Kullanicilar sekmesindeki rol secimi kullanilir;
    ///     otomatik atama, zaten bir UB grubunda olan hesaba DOKUNMAZ.
    ///  3) ProgramData\UnsalBakim ACL: SADECE SYSTEM + Administrators. Kilit YALNIZCA SYSTEM veya
    ///     yukseltilmis yonetici baglaminda uygulanir (v57.1'deki "panel kendi kendini kilitledi"
    ///     hatasi tekrarlanmasin diye) -- pratikte 5 dk'lik SYSTEM gorevinden.
    ///  4) Program Files\Windows Bakim ACL: OGRENCI makinesinde SYSTEM + Administrators +
    ///     UB_Ogretmen (Oku/Calistir); ogrenci klasoru listeleyemez, exe'yi calistiramaz. OGRETMEN
    ///     makinesinde Windows varsayilanina (miras) dondurulur.
    ///  5) Ogrenci makinesinde eski surumlerden kalan ortak masaustu/Baslat kisayollari silinir.
    /// </summary>
    public static class RolGuvenlik
    {
        public const string GrupOgrenci = "UB_Ogrenci";
        public const string GrupOgretmen = "UB_Ogretmen";
        private const string Log = "rol_guvenlik.txt";

        private static string Kok =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "UnsalBakim");

        public static bool OgretmenMakinesi(AppSettings a) =>
            a != null && string.Equals(a.MakineTuru, "ogretmen", StringComparison.OrdinalIgnoreCase);

        /// <summary>Bu surec SYSTEM mi veya yukseltilmis yonetici mi? ACL/AppLocker/grup islemleri
        /// SADECE bu durumda yapilir.</summary>
        public static bool YetkiliBaglam()
        {
            try
            {
                using (var id = WindowsIdentity.GetCurrent())
                {
                    if (id.IsSystem) return true;
                    return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch { return false; }
        }

        /// <summary>Yerel grubun SID'ini dondurur (yoksa null).</summary>
        public static string GrupSid(string grup)
        {
            try { return ((SecurityIdentifier)new NTAccount(grup).Translate(typeof(SecurityIdentifier))).Value; }
            catch { return null; }
        }

        /// <summary>Yerel Administrators grubunun (dile gore degisen) adi -- ornek: "Administrators"
        /// veya "Yöneticiler".</summary>
        public static string YoneticiGrupAdi()
        {
            try
            {
                string tam = ((NTAccount)new SecurityIdentifier("S-1-5-32-544").Translate(typeof(NTAccount))).Value;
                int i = tam.IndexOf('\\');
                return i >= 0 ? tam.Substring(i + 1) : tam;
            }
            catch { return "Administrators"; }
        }

        public static void GruplariOlustur()
        {
            if (GrupSid(GrupOgrenci) == null)
                NativeRunner.Run("net.exe", "localgroup " + GrupOgrenci + " /add /comment:\"UNSAL BAKIM - ogrenci hesaplari (standart kullanici)\"", Log, false);
            if (GrupSid(GrupOgretmen) == null)
                NativeRunner.Run("net.exe", "localgroup " + GrupOgretmen + " /add /comment:\"UNSAL BAKIM - ogretmen hesaplari (standart kullanici)\"", Log, false);
        }

        /// <summary>Saatte bir (5 dk'lik SYSTEM dongusunden) cagrilir. Gruplari olusturur, rolleri
        /// otomatik atar, klasor izinlerini ve AppLocker'i uygular. Her adim ayri try/catch.</summary>
        public static void DonguKontrol(AppSettings a, bool zorla = false)
        {
            if (!YetkiliBaglam()) return;
            string damga = Path.Combine(Kok, "rol_guvenlik_son.txt");
            try
            {
                if (!zorla && File.Exists(damga) && (DateTime.Now - File.GetLastWriteTime(damga)).TotalMinutes < 55) return;
            }
            catch { }

            try { GruplariOlustur(); } catch (Exception ex) { Logger.WriteFileOnly(Log, "[!] Grup olusturma hatasi: " + ex.Message); }
            try { RolleriOtomatikAta(a); } catch (Exception ex) { Logger.WriteFileOnly(Log, "[!] Otomatik rol atama hatasi: " + ex.Message); }
            try { OrtakKisayollariTemizle(a); } catch (Exception ex) { Logger.WriteFileOnly(Log, "[!] Kisayol temizleme hatasi: " + ex.Message); }
            // v110: tarayici guvenligi (DoH kapali + SafeSearch) -- sadece ogrenci makinesinde.
            try { TarayiciGuvenlik.Uygula(!OgretmenMakinesi(a)); } catch (Exception ex) { Logger.WriteFileOnly(Log, "[!] Tarayici guvenligi hatasi: " + ex.Message); }
            // v110: Filo'dan "Korumayi uzaktan KAPAT" aktifse kilitleyici adimlar ATLANIR
            // (bkz. KorumaAcil.cs). Normale donunce KorumaAcil.Isle bu metodu zorla=true ile cagirir.
            if (KorumaAcil.Aktif)
            {
                Logger.WriteFileOnly(Log, "Koruma acil KAPALI: AppLocker/klasor kilitleri bu turda uygulanmadi.");
                try { File.WriteAllText(damga, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")); } catch { }
                return;
            }
            try { ProgramFilesIzinUygula(a); } catch (Exception ex) { Logger.WriteFileOnly(Log, "[!] Program Files izin hatasi: " + ex.Message); }
            try { AppLockerYonet.Uygula(a); } catch (Exception ex) { Logger.WriteFileOnly(Log, "[!] AppLocker hatasi: " + ex.Message); }
            // ProgramData kilidi EN SONA: yukaridaki adimlarin log/damga yazimi bittikten sonra.
            // v109 duzeltme (canli test): kilit SADECE OGRENCI makinesinde. Ogretmen makinesinde
            // (paneli ogretmen kullanir; Norton gibi guvenlik yazilimlari da orada) Windows
            // varsayilani geri yuklenir -- panelin ayar/gorev yazmasi hicbir kosulda engellenmesin.
            if (OgretmenMakinesi(a))
            {
                try { if (!File.Exists(Path.Combine(Kok, "programdata_varsayilan.txt"))) { ProgramDataKilidiKaldir(); File.WriteAllText(Path.Combine(Kok, "programdata_varsayilan.txt"), DateTime.Now.ToString("s")); } }
                catch (Exception ex) { Logger.WriteFileOnly(Log, "[!] ProgramData izin geri alma hatasi: " + ex.Message); }
            }
            else
            {
                try { File.Delete(Path.Combine(Kok, "programdata_varsayilan.txt")); } catch { }
                try { ProgramDataKilitle(); } catch (Exception ex) { Logger.WriteFileOnly(Log, "[!] ProgramData kilit hatasi: " + ex.Message); }
            }
            try { File.WriteAllText(damga, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")); } catch { }
        }

        /// <summary>Hicbir UB grubunda olmayan standart hesaplari makine turune gore gruba ekler.</summary>
        public static void RolleriOtomatikAta(AppSettings a)
        {
            string hedef = OgretmenMakinesi(a) ? GrupOgretmen : GrupOgrenci;
            string betik =
                "$hedef='" + hedef + "'; " +
                "function Uyeler($g){ try { $x=[ADSI](\"WinNT://./\"+$g+\",group\"); @($x.psbase.Invoke('Members') | ForEach-Object { ([ADSI]$_).InvokeGet('Name') }) } catch { @() } } " +
                "$adm=''; try { $adm=(New-Object Security.Principal.SecurityIdentifier('S-1-5-32-544')).Translate([Security.Principal.NTAccount]).Value.Split('\\')[-1] } catch { $adm='Administrators' } " +
                "$yon=Uyeler $adm; $ogr=Uyeler '" + GrupOgrenci + "'; $ogt=Uyeler '" + GrupOgretmen + "'; " +
                "Get-LocalUser -ErrorAction SilentlyContinue | Where-Object { $_.Enabled } | ForEach-Object { " +
                "  $n=$_.Name; $rid=[int]($_.SID.Value.Split('-')[-1]); " +
                "  if ($rid -lt 1000) { return } " +
                "  if ($n -ieq 'yonetici') { return } " +
                "  if ($yon -contains $n) { return } " +
                "  if (($ogr -contains $n) -or ($ogt -contains $n)) { return } " +
                "  & net.exe localgroup $hedef $n /add *> $null; " +
                "  if ($LASTEXITCODE -eq 0) { 'EKLENDI|' + $n + '|' + $hedef } else { 'HATA|' + $n + '|' + $hedef + '|' + $LASTEXITCODE } " +
                "}";
            int kod;
            string cikti = NativeRunner.PowerShell(betik, 60000, out kod);
            foreach (var satir in cikti.Replace("\r", "").Split('\n'))
            {
                var s = satir.Trim();
                if (s.StartsWith("EKLENDI|"))
                {
                    var p = s.Split('|');
                    Logger.WriteFileOnly(Log, "Otomatik rol: '" + p[1] + "' -> " + p[2]);
                }
                else if (s.StartsWith("HATA|")) Logger.WriteFileOnly(Log, "[!] Otomatik rol eklenemedi: " + s);
            }
        }

        /// <summary>Kullanicilar sekmesinden: hesabi secilen role tasir (diger UB grubundan cikarir)
        /// ve yonetici ise standart kullaniciya dusurur. "yonetici", yerlesik Administrator (RID 500)
        /// ve su anda bu paneli calistiran hesap KORUNUR.</summary>
        public static string RolAta(string ad, bool ogretmen)
        {
            var sb = new StringBuilder();
            try { GruplariOlustur(); } catch { }
            string hedef = ogretmen ? GrupOgretmen : GrupOgrenci;
            string diger = ogretmen ? GrupOgrenci : GrupOgretmen;
            int r1 = NativeRunner.Run("net.exe", "localgroup " + hedef + " \"" + ad + "\" /add", Log, false);
            // 0 = eklendi, 1378 (zaten uye) da basari sayilir -- net.exe bunu 2 ile dondurur; o yuzden uyeligi ayrica dogrulamiyoruz, sadece raporluyoruz.
            sb.Append("rol=" + (ogretmen ? "OGRETMEN" : "OGRENCI") + (r1 == 0 ? "" : " (zaten uye veya eklenemedi, kod " + r1 + ")"));
            NativeRunner.Run("net.exe", "localgroup " + diger + " \"" + ad + "\" /delete", Log, false);

            bool korunan = string.Equals(ad, "yonetici", StringComparison.OrdinalIgnoreCase)
                           || string.Equals(ad, Environment.UserName, StringComparison.OrdinalIgnoreCase)
                           || YerlesikYoneticiMi(ad);
            if (!korunan)
            {
                int r2 = NativeRunner.Run("net.exe", "localgroup \"" + YoneticiGrupAdi() + "\" \"" + ad + "\" /delete", Log, false);
                if (r2 == 0) sb.Append(", YONETICILIKTEN DUSURULDU (artik standart kullanici)");
            }
            else sb.Append(", yonetici grubuna dokunulmadi (korunan hesap)");
            return sb.ToString();
        }

        private static bool YerlesikYoneticiMi(string ad)
        {
            try
            {
                var sid = (SecurityIdentifier)new NTAccount(ad).Translate(typeof(SecurityIdentifier));
                return sid.Value.EndsWith("-500");
            }
            catch { return false; }
        }

        /// <summary>ProgramData\UnsalBakim -> sadece SYSTEM + Administrators (miras kapali).</summary>
        public static void ProgramDataKilitle()
        {
            if (!YetkiliBaglam()) return;
            if (!Directory.Exists(Kok)) return;
            NativeRunner.Run("icacls.exe", "\"" + Kok + "\" /setowner *S-1-5-32-544 /T /C /Q", Log, false);
            int kod = NativeRunner.Run("icacls.exe",
                "\"" + Kok + "\" /inheritance:r /grant:r *S-1-5-18:(OI)(CI)F *S-1-5-32-544:(OI)(CI)F /T /C /Q", Log, false);
            if (kod != 0) Logger.WriteFileOnly(Log, "[!] ProgramData ACL uygulanamadi (icacls kod " + kod + ").");
        }

        /// <summary>Kurulum kaldirilirken: ProgramData izinlerini Windows varsayilanina dondurur.</summary>
        public static void ProgramDataKilidiKaldir()
        {
            if (!Directory.Exists(Kok)) return;
            NativeRunner.Run("icacls.exe", "\"" + Kok + "\" /reset /T /C /Q", Log, false);
        }

        /// <summary>Program Files\Windows Bakim: OGRENCI makinesinde ogrenciye kapali, OGRETMEN
        /// makinesinde Windows varsayilani.</summary>
        public static void ProgramFilesIzinUygula(AppSettings a)
        {
            if (!YetkiliBaglam()) return;
            string klasor = Installer.HedefKlasor;
            if (!Directory.Exists(klasor)) return;
            if (OgretmenMakinesi(a))
            {
                NativeRunner.Run("icacls.exe", "\"" + klasor + "\" /reset /T /C /Q", Log, false);
                return;
            }
            string ogt = GrupSid(GrupOgretmen);
            string ek = ogt != null ? " *" + ogt + ":(OI)(CI)RX" : "";
            int kod = NativeRunner.Run("icacls.exe",
                "\"" + klasor + "\" /inheritance:r /grant:r *S-1-5-18:(OI)(CI)F *S-1-5-32-544:(OI)(CI)F" + ek + " /T /C /Q", Log, false);
            if (kod != 0) Logger.WriteFileOnly(Log, "[!] Program Files ACL uygulanamadi (icacls kod " + kod + ").");
        }

        /// <summary>Ogrenci makinesinde eski surumlerin (v75-v108) koydugu ORTAK kisayollari siler.</summary>
        public static void OrtakKisayollariTemizle(AppSettings a)
        {
            if (OgretmenMakinesi(a)) return;
            foreach (var kok in new[] { Environment.SpecialFolder.CommonDesktopDirectory, Environment.SpecialFolder.CommonPrograms })
            {
                try
                {
                    string f = Path.Combine(Environment.GetFolderPath(kok), "UNSAL BAKIM.lnk");
                    if (File.Exists(f)) { File.Delete(f); Logger.WriteFileOnly(Log, "Ogrenci makinesi: ortak kisayol silindi: " + f); }
                }
                catch { }
            }
        }
    }
}
