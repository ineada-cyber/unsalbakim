using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace UnsalBakim.Core
{
    /// <summary>
    /// v109: Windows AppLocker -- SADECE ogrenci makinesinde, kurallar SADECE UB_Ogrenci grubunu
    /// kisitlar (ogretmen/yonetici/SYSTEM etkilenmez). Windows 10 2004+ ve tum Windows 11
    /// surumlerinde Pro dahil uygulanabilir (Microsoft KB5024351, surum kontrolu kaldirildi).
    ///
    /// Kurallar (EXE ve MSI):
    ///  - Herkes: %WINDIR% (ogrencinin yazabildigi alt klasorler HARIC) ve %PROGRAMFILES% (x86 dahil)
    ///  - Administrators ve UB_Ogretmen: her yer
    ///  - UB_Ogrenci: Program Files\Windows Bakim (bizim program) ACIKCA YASAK
    ///  - Kural olmayan her yer (AppData, Indirilenler, Masaustu, Temp, USB, ProgramData...) ogrenci
    ///    icin izinsiz -> Denetim modunda sadece KAYDEDILIR, Zorunlu modda ENGELLENIR.
    ///  - Script/Appx/DLL koleksiyonlari BILEREK yapilandirilmadi (duvar kagidi wscript betigi ve
    ///    Store uygulamalari etkilenmesin).
    ///
    /// Varsayilan: DENETIM (AuditOnly). Zorunlu moda gecis SADECE panelden, yonetici onayiyla
    /// (Koruma sekmesi). Ogretmen makinesinde politika kaldirilir.
    /// </summary>
    public static class AppLockerYonet
    {
        private const string Log = "applocker.txt";

        private static string Kok =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "UnsalBakim");
        private static string XmlYolu => Path.Combine(Kok, "applocker_politika.xml");
        private static string DamgaYolu => Path.Combine(Kok, "applocker_uygulanan.txt");

        // Ogrencinin (standart kullanicinin) yazabildigi bilinen Windows alt klasorleri -- bunlar
        // "Windows klasoru serbest" kuralindan HARIC tutulur, yoksa ogrenci oraya exe kopyalayip
        // calistirabilirdi.
        private static readonly string[] WindowsIstisnalari =
        {
            @"%WINDIR%\Temp\*", @"%WINDIR%\Tasks\*", @"%WINDIR%\tracing\*",
            @"%WINDIR%\Registration\CRMLog\*", @"%WINDIR%\PLA\Reports\*", @"%WINDIR%\PLA\Rules\*",
            @"%WINDIR%\PLA\Templates\*", @"%WINDIR%\SysWOW64\Tasks\*", @"%WINDIR%\SysWOW64\com\dmp\*",
            @"%WINDIR%\SysWOW64\FxsTmp\*", @"%SYSTEM32%\Tasks\*", @"%SYSTEM32%\Tasks_Migrated\*",
            @"%SYSTEM32%\com\dmp\*", @"%SYSTEM32%\FxsTmp\*", @"%SYSTEM32%\spool\drivers\color\*",
            @"%SYSTEM32%\spool\PRINTERS\*", @"%SYSTEM32%\spool\SERVERS\*",
            @"%SYSTEM32%\Microsoft\Crypto\RSA\MachineKeys\*"
        };

        public static string ModMetni(AppSettings a) => a.AppLockerZorunlu ? "ZORUNLU (engelliyor)" : "DENETIM (sadece kaydediyor)";

        /// <summary>Politika XML'ini uretir (SID'ler bu makinedeki gruplardan).</summary>
        public static string PolitikaXml(bool zorunlu, string ogrenciSid, string ogretmenSid)
        {
            string mod = zorunlu ? "Enabled" : "AuditOnly";
            string programKlasoru = @"%PROGRAMFILES%\" + Path.GetFileName(Installer.HedefKlasor) + @"\*";
            var sb = new StringBuilder();
            sb.Append("<AppLockerPolicy Version=\"1\">");

            // ---- EXE ----
            sb.Append("<RuleCollection Type=\"Exe\" EnforcementMode=\"" + mod + "\">");
            sb.Append(Kural("2185f05d-8950-4b05-bfb0-1c6e18534cce", "UB: Windows klasoru (herkes)", "S-1-1-0", "Allow", @"%WINDIR%\*", WindowsIstisnalari));
            sb.Append(Kural("bb2325a6-33bb-4f3e-b9b9-7f081a17bef1", "UB: Program Files (herkes)", "S-1-1-0", "Allow", @"%PROGRAMFILES%\*", null));
            sb.Append(Kural("bfb45863-ac1f-4b6d-bdbd-7472545d6266", "UB: Yoneticiler her yer", "S-1-5-32-544", "Allow", "*", null));
            if (!string.IsNullOrEmpty(ogretmenSid))
                sb.Append(Kural("5f156b9c-a08d-4e3f-959a-c1363558fe9f", "UB: Ogretmenler her yer", ogretmenSid, "Allow", "*", null));
            if (!string.IsNullOrEmpty(ogrenciSid))
                sb.Append(Kural("f9e2c6fe-b6f8-4739-b005-b2e9b8c3ddd2", "UB: Ogrenci UNSAL BAKIM klasoru YASAK", ogrenciSid, "Deny", programKlasoru, null));
            sb.Append("</RuleCollection>");

            // ---- MSI ----
            sb.Append("<RuleCollection Type=\"Msi\" EnforcementMode=\"" + mod + "\">");
            sb.Append(Kural("a42c75a7-ed0f-4a1d-92e1-b73e944aac96", "UB: Windows Installer onbellegi (herkes)", "S-1-1-0", "Allow", @"%WINDIR%\Installer\*", null));
            sb.Append(Kural("c7b0a7cf-0887-4a4f-875d-334c7474a18b", "UB: Yoneticiler tum MSI", "S-1-5-32-544", "Allow", "*", null));
            if (!string.IsNullOrEmpty(ogretmenSid))
                sb.Append(Kural("68d1f790-c917-4347-b702-fd31a879a2e4", "UB: Ogretmenler tum MSI", ogretmenSid, "Allow", "*", null));
            sb.Append("</RuleCollection>");

            sb.Append("</AppLockerPolicy>");
            return sb.ToString();
        }

        private static string Kural(string id, string ad, string sid, string eylem, string yol, string[] istisnalar)
        {
            var sb = new StringBuilder();
            sb.Append("<FilePathRule Id=\"" + id + "\" Name=\"" + Xml(ad) + "\" Description=\"UNSAL BAKIM v109\" UserOrGroupSid=\"" + sid + "\" Action=\"" + eylem + "\">");
            sb.Append("<Conditions><FilePathCondition Path=\"" + Xml(yol) + "\" /></Conditions>");
            if (istisnalar != null && istisnalar.Length > 0)
            {
                sb.Append("<Exceptions>");
                foreach (var i in istisnalar) sb.Append("<FilePathCondition Path=\"" + Xml(i) + "\" />");
                sb.Append("</Exceptions>");
            }
            sb.Append("</FilePathRule>");
            return sb.ToString();
        }

        private static string Xml(string s) => (s ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

        private static string Ozet(string s)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(s))).Replace("-", "");
        }

        /// <summary>Saatte bir (RolGuvenlik.DonguKontrol) ve moddan degisikliginde cagrilir.
        /// Politika degismediyse ve AppIDSvc calisiyorsa hicbir sey yapmaz.</summary>
        public static void Uygula(AppSettings a, bool zorla = false)
        {
            if (!RolGuvenlik.YetkiliBaglam()) return;
            if (RolGuvenlik.OgretmenMakinesi(a)) { Kaldir(); return; }

            string ogr = RolGuvenlik.GrupSid(RolGuvenlik.GrupOgrenci);
            string ogt = RolGuvenlik.GrupSid(RolGuvenlik.GrupOgretmen);
            if (ogr == null) { Logger.WriteFileOnly(Log, "[!] UB_Ogrenci grubu yok -- AppLocker uygulanmadi (bir sonraki turda tekrar denenecek)."); return; }

            string xml = PolitikaXml(a.AppLockerZorunlu, ogr, ogt);
            string ozet = Ozet(xml);
            string onceki = "";
            try { if (File.Exists(DamgaYolu)) onceki = File.ReadAllText(DamgaYolu).Trim(); } catch { }

            int kod;
            if (!zorla && onceki == ozet)
            {
                // Politika ayni -- sadece servis calisiyor mu bak (calismiyorsa baslat).
                string d = NativeRunner.PowerShell("(Get-Service AppIDSvc -ErrorAction SilentlyContinue).Status", 30000, out kod);
                if (d.IndexOf("Running", StringComparison.OrdinalIgnoreCase) >= 0) return;
            }

            Directory.CreateDirectory(Kok);
            File.WriteAllText(XmlYolu, xml, new UTF8Encoding(false));
            string betik =
                "try { Set-AppLockerPolicy -XmlPolicy '" + XmlYolu.Replace("'", "''") + "' -ErrorAction Stop; 'POLITIKA_OK' } catch { 'POLITIKA_HATA|' + $_.Exception.Message } " +
                "& sc.exe config AppIDSvc start= auto | Out-Null; " +
                "try { Start-Service AppIDSvc -ErrorAction Stop } catch { } " +
                "'SERVIS|' + (Get-Service AppIDSvc -ErrorAction SilentlyContinue).Status";
            string cikti = NativeRunner.PowerShell(betik, 120000, out kod);
            bool ok = cikti.Contains("POLITIKA_OK");
            Logger.WriteFileOnly(Log, "AppLocker politikasi uygulandi (" + ModMetni(a) + "): " + cikti.Replace("\r", " ").Replace("\n", " ").Trim());
            if (ok)
            {
                try { File.WriteAllText(DamgaYolu, ozet); } catch { }
                if (onceki != ozet)
                    try { DriveLog.Gonder("applocker_politika", "Mod: " + ModMetni(a) + " | " + cikti.Replace("\r", " ").Replace("\n", " ").Trim()); } catch { }
            }
        }

        /// <summary>Ogretmen makinesi veya kurulum kaldirma: bizim uyguladigimiz politikayi bosaltir.</summary>
        public static void Kaldir()
        {
            if (!File.Exists(DamgaYolu)) return;
            if (!RolGuvenlik.YetkiliBaglam()) return;
            Directory.CreateDirectory(Kok);
            File.WriteAllText(XmlYolu, "<AppLockerPolicy Version=\"1\"></AppLockerPolicy>", new UTF8Encoding(false));
            int kod;
            string cikti = NativeRunner.PowerShell(
                "try { Set-AppLockerPolicy -XmlPolicy '" + XmlYolu.Replace("'", "''") + "' -ErrorAction Stop; 'KALDIRILDI' } catch { 'HATA|' + $_.Exception.Message }", 120000, out kod);
            Logger.WriteFileOnly(Log, "AppLocker politikasi kaldirildi: " + cikti.Trim());
            if (cikti.Contains("KALDIRILDI")) try { File.Delete(DamgaYolu); } catch { }
        }

        /// <summary>Son 24 saatin AppLocker olaylari (8003/8006 = denetim "engellenecekti",
        /// 8004/8007 = engellendi). Satir: "gg.aa SS:dd|DENETIM/ENGEL|kullanici|dosya".</summary>
        public static string SonOlaylar(int saat = 24, int adet = 60)
        {
            string betik =
                "$s=(Get-Date).AddHours(-" + saat + "); $ev=@(); " +
                "foreach($l in @('Microsoft-Windows-AppLocker/EXE and DLL','Microsoft-Windows-AppLocker/MSI and Script')) { " +
                "  try { $ev += Get-WinEvent -FilterHashtable @{LogName=$l; Id=8003,8004,8006,8007; StartTime=$s} -MaxEvents 300 -ErrorAction Stop } catch { } } " +
                "$ev | Sort-Object TimeCreated -Descending | Select-Object -First " + adet + " | ForEach-Object { " +
                "  $x=[xml]$_.ToXml(); $d=$x.Event.UserData.RuleAndFileData; $f=$d.FullFilePath; if (-not $f) { $f=$d.FilePath }; " +
                "  $u=$x.Event.System.Security.UserID; try { $u=(New-Object Security.Principal.SecurityIdentifier($u)).Translate([Security.Principal.NTAccount]).Value } catch { }; " +
                "  $t= if ($_.Id -eq 8003 -or $_.Id -eq 8006) { 'DENETIM' } else { 'ENGEL' }; " +
                "  $_.TimeCreated.ToString('dd.MM HH:mm') + '|' + $t + '|' + $u + '|' + $f }";
            int kod;
            string cikti = NativeRunner.PowerShell(betik, 60000, out kod);
            var sb = new StringBuilder();
            foreach (var satir in cikti.Replace("\r", "").Split('\n'))
            {
                var s = satir.Trim();
                if (s.Length > 0 && s.IndexOf('|') > 0 && (s.Contains("|DENETIM|") || s.Contains("|ENGEL|"))) sb.AppendLine(s);
            }
            return sb.ToString();
        }
    }
}
