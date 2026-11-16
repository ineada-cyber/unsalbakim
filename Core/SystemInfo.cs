using System;
using System.Management;
using System.Text;
using Microsoft.Win32;

namespace UnsalBakim.Core
{
    /// <summary>Islemci, RAM, disk, Windows ve lisans bilgisi (WMI).</summary>
    public static class SystemInfo
    {
        public static string Topla()
        {
            var sb = new StringBuilder();
            sb.AppendLine("==================== SISTEM BILGISI ====================");
            try
            {
                foreach (ManagementObject mo in new ManagementObjectSearcher("select Manufacturer,Model from Win32_ComputerSystem").Get())
                    sb.AppendLine("Bilgisayar       : " + (mo["Manufacturer"] + " " + mo["Model"]).Trim());
            }
            catch { }

            // v106 (Ali Bey'in istegi): seri numarasi da yerel ekrana eklendi (marka/model zaten
            // yukarida vardi, seri no eksikti).
            try
            {
                foreach (ManagementObject mo in new ManagementObjectSearcher("select SerialNumber from Win32_BIOS").Get())
                {
                    string sn = (mo["SerialNumber"] ?? "").ToString().Trim();
                    if (sn.Length > 0) sb.AppendLine("Seri No          : " + sn);
                    break;
                }
            }
            catch { }

            try
            {
                foreach (ManagementObject mo in new ManagementObjectSearcher("select Name,NumberOfCores,NumberOfLogicalProcessors from Win32_Processor").Get())
                {
                    string ad = (mo["Name"] ?? "").ToString().Trim();
                    sb.AppendLine("Islemci          : " + ad);
                    sb.AppendLine("Cekirdek/Is parc.: " + mo["NumberOfCores"] + " / " + mo["NumberOfLogicalProcessors"]);
                    sb.AppendLine("Nesil/Yil        : " + IslemciNesli(ad));
                }
            }
            catch { }

            try
            {
                ulong toplam = 0; int modul = 0; uint hiz = 0; string tur = "";
                foreach (ManagementObject mo in new ManagementObjectSearcher("select Capacity,Speed,SMBIOSMemoryType from Win32_PhysicalMemory").Get())
                {
                    toplam += Convert.ToUInt64(mo["Capacity"] ?? (ulong)0);
                    modul++;
                    if (mo["Speed"] != null) hiz = Convert.ToUInt32(mo["Speed"]);
                    if (mo["SMBIOSMemoryType"] != null)
                    {
                        int t = Convert.ToInt32(mo["SMBIOSMemoryType"]);
                        tur = t == 34 ? "DDR5" : t == 26 ? "DDR4" : t == 24 ? "DDR3" : "";
                    }
                }
                double gb = Math.Round(toplam / 1024.0 / 1024.0 / 1024.0, 1);
                sb.AppendLine("RAM              : " + gb + " GB " + tur + (hiz > 0 ? " " + hiz + " MHz" : "") + " (" + modul + " modul)");
            }
            catch { }

            try
            {
                foreach (ManagementObject mo in new ManagementObjectSearcher("select Model,Size,MediaType from Win32_DiskDrive").Get())
                {
                    double gb = 0; try { gb = Math.Round(Convert.ToDouble(mo["Size"]) / 1024 / 1024 / 1024, 0); } catch { }
                    sb.AppendLine("Disk             : " + (mo["Model"] ?? "").ToString().Trim() + " | " + gb + " GB");
                }
                foreach (ManagementObject mo in new ManagementObjectSearcher("select DeviceID,Size,FreeSpace from Win32_LogicalDisk where DriveType=3").Get())
                {
                    double size = 0, free = 0;
                    try { size = Convert.ToDouble(mo["Size"]); free = Convert.ToDouble(mo["FreeSpace"]); } catch { }
                    if (size > 0)
                        sb.AppendLine("  " + mo["DeviceID"] + " bos: " + Math.Round(free / 1e9, 0) + " GB / " + Math.Round(size / 1e9, 0) + " GB (%" + Math.Round(100 * free / size, 0) + ")");
                }
            }
            catch { }

            try
            {
                foreach (ManagementObject mo in new ManagementObjectSearcher("select Caption,Version,OSArchitecture from Win32_OperatingSystem").Get())
                {
                    sb.AppendLine("Windows          : " + (mo["Caption"] ?? "").ToString().Replace("Microsoft ", "").Trim());
                    sb.AppendLine("Surum / Mimari   : " + mo["Version"] + " / " + mo["OSArchitecture"]);
                }
            }
            catch { }

            sb.AppendLine("Windows lisansi  : " + LisansDurumu());
            sb.AppendLine("========================================================");
            return sb.ToString();
        }

        private static string IslemciNesli(string ad)
        {
            try
            {
                if (ad.IndexOf("Intel", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var m = System.Text.RegularExpressions.Regex.Match(ad, @"i[3579][- ]([0-9]{3,5})");
                    if (m.Success)
                    {
                        string num = m.Groups[1].Value;
                        int g = num.Length >= 4 ? int.Parse(num.Substring(0, num.Length - 3)) : 1;
                        string[] yil = { "", "2010", "2011", "2013", "2014", "2015", "2016", "2017", "2018", "2019", "2020", "2021", "2022", "2023", "2024" };
                        return g + ". nesil Intel Core" + (g < yil.Length ? " (~" + yil[g] + ")" : "");
                    }
                    if (ad.IndexOf("Ultra", StringComparison.OrdinalIgnoreCase) >= 0) return "Intel Core Ultra (2024+)";
                }
                else if (ad.IndexOf("Ryzen", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var m = System.Text.RegularExpressions.Regex.Match(ad, @"Ryzen \d+ ([0-9]{4,5})");
                    if (m.Success) return "AMD Ryzen " + m.Groups[1].Value.Substring(0, 1) + "000 serisi";
                }
            }
            catch { }
            return "bilinmiyor";
        }

        /// <summary>v49: Envanter sistemi icin HAM/yapilandirilmis alanlar (Topla()'nin insan-okur
        /// metin bicimine karsilik, burada her deger ayri bir sozluk girdisi olarak doner -- Google
        /// Sheet'e ayri sutunlar halinde gitmesi icin). Topla() KASITLI OLARAK degistirilmedi (ekranda
        /// zaten calisan "Sistem bilgisini goster" butonunu bozma riski almamak icin); ayni WMI
        /// sorgularinin HAFIF bir tekrari.</summary>
        public static System.Collections.Generic.Dictionary<string, string> EnvanterAlanlari()
        {
            var d = new System.Collections.Generic.Dictionary<string, string>();
            // v106 (Ali Bey'in istegi: "bilgisayarlarin marka,model,seri numaralarini menuden
            // alabiliyor muyuz" -- merkezi Filo Envanterinde gorunsun istedi): Win32_ComputerSystem
            // (Manufacturer/Model -- Topla()'da zaten kullanilan AYNI WMI sorgusu) ve Win32_BIOS
            // (SerialNumber -- anakart/BIOS'a gomulu, donanim etiketiyle AYNI, degistirilemez
            // seri no) okunur. Sanal makinelerde veya bazi beyaz-kutu/clone sistemlerde
            // SerialNumber "To be filled by O.E.M." veya bos donebilir -- bu bir hata degil,
            // uretici o bilgiyi hic yazmamis demektir.
            try
            {
                foreach (ManagementObject mo in new ManagementObjectSearcher("select Manufacturer,Model from Win32_ComputerSystem").Get())
                {
                    d["marka"] = (mo["Manufacturer"] ?? "").ToString().Trim();
                    d["model"] = (mo["Model"] ?? "").ToString().Trim();
                    break;
                }
            }
            catch { }
            try
            {
                foreach (ManagementObject mo in new ManagementObjectSearcher("select SerialNumber from Win32_BIOS").Get())
                { d["seri_no"] = (mo["SerialNumber"] ?? "").ToString().Trim(); break; }
            }
            catch { }
            try
            {
                foreach (ManagementObject mo in new ManagementObjectSearcher("select Name from Win32_Processor").Get())
                { d["cpu"] = (mo["Name"] ?? "").ToString().Trim(); break; }
            }
            catch { }
            try
            {
                ulong toplam = 0;
                foreach (ManagementObject mo in new ManagementObjectSearcher("select Capacity from Win32_PhysicalMemory").Get())
                    toplam += Convert.ToUInt64(mo["Capacity"] ?? (ulong)0);
                d["ram_gb"] = Math.Round(toplam / 1024.0 / 1024.0 / 1024.0, 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            catch { }
            try
            {
                foreach (ManagementObject mo in new ManagementObjectSearcher("select DeviceID,Size,FreeSpace from Win32_LogicalDisk where DeviceID='C:'").Get())
                {
                    double size = Convert.ToDouble(mo["Size"] ?? 0.0), free = Convert.ToDouble(mo["FreeSpace"] ?? 0.0);
                    d["disk_toplam_gb"] = Math.Round(size / 1e9, 0).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    d["disk_bos_gb"] = Math.Round(free / 1e9, 0).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    break;
                }
            }
            catch { }
            try
            {
                foreach (ManagementObject mo in new ManagementObjectSearcher("select Caption,Version from Win32_OperatingSystem").Get())
                {
                    string tamAd = (mo["Caption"] ?? "").ToString().Replace("Microsoft ", "").Trim();
                    // v72 (Ali Bey'in istegi: "Win11 Pro gibi" -- Sheet'te daha kisa gorunsun):
                    // "Windows 11 Pro" -> "Win11 Pro", "Windows 10 Pro" -> "Win10 Pro". Kernel
                    // surum numarasi (10.0.26300) artik merkezi envantere hic gonderilmiyor --
                    // tek basina pek bir sey ifade etmiyordu, sutunu gereksiz uzatiyordu.
                    d["windows"] = System.Text.RegularExpressions.Regex.Replace(tamAd, @"^Windows\s+", "Win");
                    break;
                }
            }
            catch { }
            d["lisans"] = LisansDurumu();
            return d;
        }

        private static string LisansDurumu()
        {
            try
            {
                // v65 (Ali Bey'in istegi): "anahtar TAMAMI gorunsun". Windows 8+'ta OEM makinelerde
                // (BIOS/ACPI MSDM tablosuna gomulu) TAM urun anahtarini okumak icin resmi bir yol
                // VAR: SoftwareLicensingService.OA3xOriginalProductKey. Bu SADECE OEM/firmware
                // anahtari olan makinelerde dolu doner (Retail/Volume/GVLK lisanslarda bos gelir --
                // bu bir hata degil, boyle bir anahtar zaten yok). Bos gelirse asagidaki kayit
                // decode yontemine, o da bosa cikarsa PartialProductKey (son 5 karakter) ile devam edilir.
                string tamAnahtar = "";
                try
                {
                    foreach (ManagementObject mo0 in new ManagementObjectSearcher("select OA3xOriginalProductKey from SoftwareLicensingService").Get())
                    {
                        tamAnahtar = (mo0["OA3xOriginalProductKey"] ?? "").ToString().Trim();
                        if (tamAnahtar.Length > 0) break;
                    }
                }
                catch { }

                string q = "select LicenseStatus,ProductKeyChannel,PartialProductKey,Name from SoftwareLicensingProduct where ApplicationID='55c92734-d682-4d71-983e-d6ec3f16059f' and PartialProductKey is not null";
                foreach (ManagementObject mo in new ManagementObjectSearcher(q).Get())
                {
                    string ad = (mo["Name"] ?? "").ToString();
                    if (ad.IndexOf("Windows", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    int st = Convert.ToInt32(mo["LicenseStatus"]);
                    string d;
                    switch (st)
                    {
                        case 1: d = "Etkin (lisansli)"; break;
                        case 0: d = "Lisanssiz"; break;
                        case 2: d = "Deneme suresi"; break;
                        case 5: d = "Etkinlestirilmemis"; break;
                        default: d = "Durum " + st; break;
                    }
                    string parcaliAnahtar = (mo["PartialProductKey"] ?? "").ToString().Trim();
                    // v80 (Ali Bey'in istegi: kanal/"OEM:DM" kismi kaldirilsin, sade "Etkin (lisansli)"
                    // kalsin) -- ProductKeyChannel artik durum metnine eklenmiyor.
                    string durumMetin = d;

                    // v69 (Ali Bey'in istegi: "Lisans Durumu:, Urun Anahtari:" etiketleriyle,
                    // 5 harf degil TUMU lazim): OA3x bossa, kayittaki (DigitalProductId) kodlu
                    // anahtari decode edip TAM 25 karakteri bulmaya calis -- USB_MAKINA_BILGI
                    // aracindaki AYNI yontem (Windows 7'den beri bilinen, yaygin kullanilan
                    // standart decode). Dijital lisansli (kagitsiz) makinelerde bu genel/yer
                    // tutucu bir anahtar donebilir -- Windows'un tasarim siniri, hata degil.
                    string anahtarMetin;
                    if (tamAnahtar.Length > 0)
                    {
                        anahtarMetin = tamAnahtar;
                    }
                    else
                    {
                        string kayitAnahtari = KayittanAnahtarDecode();
                        if (kayitAnahtari.Length > 0) anahtarMetin = kayitAnahtari;
                        else if (parcaliAnahtar.Length > 0) anahtarMetin = "(son 5 hane) " + parcaliAnahtar;
                        else anahtarMetin = "yok";
                    }
                    // v80 (Ali Bey'in istegi: "Lisans Durumu:" -> "Lisans:" + virgulden once bosluk)
                    return "Lisans:" + durumMetin + " ,Urun Anahtari: " + anahtarMetin;
                }
            }
            catch { }
            return "bilinmiyor";
        }

        /// <summary>v69: HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion!DigitalProductId icindeki
        /// kodlu anahtari decode eder (ShowKeyPlus/Magical Jelly Bean gibi araclarin da kullandigi
        /// standart yontem -- lisans kirma/bypass DEGIL, makinede ZATEN kurulu anahtari okuma).
        /// Basarisiz olursa veya kayit yoksa bos dondurur (cagiran taraf baska bir yedek dener).</summary>
        private static string KayittanAnahtarDecode()
        {
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                {
                    var veri = k?.GetValue("DigitalProductId") as byte[];
                    if (veri == null || veri.Length < 67) return "";

                    byte[] anahtarBayt = new byte[15];
                    Array.Copy(veri, 52, anahtarBayt, 0, 15);
                    int isWin8 = (veri[66] / 6) & 1;
                    anahtarBayt[14] = (byte)((anahtarBayt[14] & 0xf7) | ((isWin8 & 2) * 4));

                    const string karakterler = "BCDFGHJKMPQRTVWXY2346789";
                    var sb = new StringBuilder();
                    for (int i = 24; i >= 0; i--)
                    {
                        int guncel = 0;
                        for (int j = 14; j >= 0; j--)
                        {
                            guncel = guncel * 256 ^ anahtarBayt[j];
                            anahtarBayt[j] = (byte)(guncel / 24);
                            guncel %= 24;
                        }
                        sb.Insert(0, karakterler[guncel]);
                        if (i % 5 == 0 && i != 0) sb.Insert(0, "-");
                    }
                    return sb.ToString();
                }
            }
            catch { return ""; }
        }
    }
}
