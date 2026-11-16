using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace UnsalBakim.Core
{
    /// <summary>
    /// TAHMINE DAYALI, GEÇICI ilk uygulama -- "yeni bulunan acikilar ve istekler" listesindeki
    /// madde #9: "degisimi engelleyen programlar". Ali Bey bu maddenin tam ne anlama geldigini
    /// ("baska bir sey, yazarim" dedi) henuz yazmadigi icin, en yaygin/bilinen "sistemi dondurup
    /// her yeniden baslatmada eski haline geri dondurerek YAPILAN HER DEGISIKLIGI (bizim
    /// bakim/engelleme ayarlarimiz dahil) SILEN" yazilim kategorisi hedeflendi: Deep Freeze,
    /// Reboot Restore Rx, Shadow Defender, Toolwiz Time Freeze, Comodo Time Machine, Returnil,
    /// WenPcFreeze, EAZFreeze.
    ///
    /// Bilerek yapilmayanlar: otomatik KALDIRMA (uninstall) -- bu tur yazilimlar genelde tum
    /// diski/surucuyu etkileyen, yanlis kaldirildiginda sistemi bozabilen ozel surucular icerir.
    /// Bunun yerine: calisan surecini kapat + ogretmene LOGLA (elle kontrol/kaldirma icin).
    ///
    /// Ali Bey donup tam ne kastettigini yazinca (ornegin "Program Kaldirma/Degistirme" menusunu
    /// engelleyen bir policy mi, yoksa baska bir yazilim turu mu) bu dosya ona gore guncellenecek;
    /// simdilik bu en olasi/zararsiz yorumla "devam et" talimatiyla eklendi.
    /// </summary>
    public static class DegisimEngelKontrol
    {
        private const string Log = "degisim_engel.txt";

        public static readonly List<string> Surecler = new List<string>
        {
            "PFCd","PFSvc","DFServ","DFWinLogon",          // Faronics Deep Freeze
            "RollbackRxClientService","RollbackRxAgent",    // Reboot Restore Rx / Rollback Rx
            "ShadowDefender","SbieSvc",                     // Shadow Defender (SbieSvc da Sandboxie ile paylasilir, dikkatli)
            "TimeFreeze","TzSentry",                         // Toolwiz Time Freeze
            "CTMService","ctmsvc",                           // Comodo Time Machine
            "RvlNsSrv","Returnil",                           // Returnil
            "WenPcFreeze","eazfreeze"
        };

        private static string EkDosya =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "UnsalBakim", "ek_degisim_engel.txt");

        static DegisimEngelKontrol()
        {
            try { if (File.Exists(EkDosya)) foreach (var l in File.ReadAllLines(EkDosya)) { var a = l.Trim(); if (a.Length > 0 && !a.StartsWith("#") && !Surecler.Contains(a)) Surecler.Add(a); } } catch { }
        }

        /// <summary>Calisan "donma/geri alma" yazilimlarini tespit edip kapatir; kaldirmaz.
        /// Bulunan her olay teacher log'una yazilir (elle inceleme icin).</summary>
        public static int TaraVeKapat()
        {
            int n = 0;
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in Surecler) set.Add(s);
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    if (set.Contains(p.ProcessName))
                    {
                        Logger.WriteFileOnly(Log, "[UYARI] 'Degisimi engelleyen' surec bulundu: " + p.ProcessName +
                            " -- otomatik KALDIRILMADI (sadece kapatildi); elle kontrol onerilir.");
                        try { p.Kill(); n++; } catch { }
                        EngelKayit.Ekle(p.ProcessName, "degisim-engel-surec-kapatma");
                    }
                }
                catch { }
            }
            if (n > 0) Logger.WriteFileOnly(Log, "Toplam " + n + " 'degisimi engelleyen' surec kapatildi.");
            return n;
        }
    }
}
