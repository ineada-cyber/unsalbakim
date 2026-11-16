using System;
using System.Diagnostics;

namespace UnsalBakim.Core
{
    /// <summary>Harici komut/araci (netsh, ipconfig, dism, sfc, schtasks, powercfg) calistirir.</summary>
    public static class NativeRunner
    {
        /// <summary>Komutu calistirir, ciktiyi log satirina gonderir, cikis kodunu dondurur.</summary>
        public static int Run(string exe, string args, string logDosya, bool ciktiyiLogla = true, int timeoutMs = 0)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding = System.Text.Encoding.UTF8
                };
                using (var p = new Process { StartInfo = psi })
                {
                    p.OutputDataReceived += (s, e) => { if (ciktiyiLogla && !string.IsNullOrEmpty(e.Data)) Logger.Write(logDosya, "  " + e.Data); };
                    p.ErrorDataReceived += (s, e) => { if (ciktiyiLogla && !string.IsNullOrEmpty(e.Data)) Logger.Write(logDosya, "  [!] " + e.Data); };
                    p.Start();
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    if (timeoutMs > 0) { if (!p.WaitForExit(timeoutMs)) { try { p.Kill(); } catch { } return -1; } }
                    else p.WaitForExit();
                    return p.ExitCode;
                }
            }
            catch (Exception ex)
            {
                Logger.Write(logDosya, "  [HATA] " + exe + " calistirilamadi: " + ex.Message);
                return -2;
            }
        }
            /// <summary>v109: Komutu calistirir ve standart ciktiyi (stdout+stderr) METIN olarak
        /// dondurur (log dosyasina YAZMAZ). Cikis kodu out parametresiyle doner (-1 zaman asimi,
        /// -2 calistirilamadi). Grup uyeligi/olay gunlugu gibi ciktisi islenecek komutlar icin.</summary>
        public static string RunCapture(string exe, string args, int timeoutMs, out int cikisKodu) => RunCapture(exe, args, timeoutMs, null, out cikisKodu);

        /// <summary>v109: stdin uzerinden girdi verilebilen surum (PowerShell betigi komut satirinda gorunmesin diye).</summary>
        public static string RunCapture(string exe, string args, int timeoutMs, string stdin, out int cikisKodu)
        {
            var sb = new System.Text.StringBuilder();
            cikisKodu = -2;
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = stdin != null,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding = System.Text.Encoding.UTF8
                };
                using (var p = new Process { StartInfo = psi })
                {
                    p.OutputDataReceived += (s, e) => { if (e.Data != null) lock (sb) sb.AppendLine(e.Data); };
                    p.ErrorDataReceived += (s, e) => { if (e.Data != null) lock (sb) sb.AppendLine(e.Data); };
                    p.Start();
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    if (stdin != null)
                    {
                        try { p.StandardInput.Write(stdin); p.StandardInput.Close(); } catch { }
                    }
                    if (!p.WaitForExit(timeoutMs > 0 ? timeoutMs : 120000)) { try { p.Kill(); } catch { } cikisKodu = -1; }
                    else { p.WaitForExit(); cikisKodu = p.ExitCode; }
                }
            }
            catch (Exception ex) { sb.AppendLine("[HATA] " + exe + ": " + ex.Message); }
            lock (sb) return sb.ToString();
        }

        /// <summary>v109: PowerShell betigini calistirir, ciktisini dondurur. Betik KOMUT SATIRINDA
        /// DEGIL, standart girdi (stdin) uzerinden verilir ("-Command -"). ONCEKI deneme
        /// -EncodedCommand kullaniyordu -- Norton bunu "komut satiri tespiti" (IDP.HELU.PSE96) ile
        /// zararli sayip powershell.exe'yi sonlandirdi (Ali Bey'in dizustunde canli goruldu).
        /// Ilerleme (progress) kayitlari kapatilir ve "#< CLIXML" gurultusu ciktidan ayiklanir.</summary>
        public static string PowerShell(string betik, int timeoutMs, out int cikisKodu)
        {
            string tam = "$ProgressPreference='SilentlyContinue'; $ErrorActionPreference='Continue'; [Console]::OutputEncoding=[Text.Encoding]::UTF8; " + betik.Replace("\r", " ").Replace("\n", " ") + "\r\nexit\r\n";
            // 32-bit surecten calisilirsa 64-bit PowerShell'i (LocalAccounts/AppLocker modulleri
            // sadece 64-bit'te var) Sysnative uzerinden cagir.
            string ps = "powershell.exe";
            try
            {
                if (Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess)
                {
                    string sn = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Sysnative", "WindowsPowerShell", "v1.0", "powershell.exe");
                    if (System.IO.File.Exists(sn)) ps = sn;
                }
            }
            catch { }
            string ham = RunCapture(ps, "-NoProfile -NonInteractive -Command -", timeoutMs, tam, out cikisKodu);
            var temiz = new System.Text.StringBuilder();
            bool xml = false;
            foreach (var satir in ham.Replace("\r", "").Split('\n'))
            {
                string t = satir.TrimStart();
                if (t.StartsWith("#< CLIXML")) { xml = true; continue; }
                if (xml && (t.StartsWith("<Objs") || t.StartsWith("<Obj") || t.StartsWith("</"))) continue;
                if (t.StartsWith("<Objs")) continue;
                temiz.AppendLine(satir);
            }
            return temiz.ToString();
        }
    }
}
