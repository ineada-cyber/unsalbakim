using System;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace UnsalBakim.Core
{
    /// <summary>
    /// v110 (13 Kasim 2026 incelemesi): GUVENLI guncelleme kaynagi.
    /// Google Drive guncelleme.zip'i "viruslu" isaretleyip engelledi; ayrica eski guncelleme
    /// kriptografik olarak dogrulanmiyordu. Bu sinif guncellemeyi GitHub Releases'ten alir ve
    /// HER iki guvenligi birden saglar:
    ///   1) Imza: indirilen 'guncelleme.manifest' (surum + zip'in SHA-256'si), Ali Bey'in OZEL
    ///      anahtariyla imzalanmistir; istemci Musteri.GuncellemeAcikAnahtarXml ile dogrular.
    ///      Imza gecersizse HICBIR dosyaya dokunulmaz (Drive/token ele gecse bile sahte exe
    ///      dagitilamaz).
    ///   2) Butunluk: guncelleme.zip indirildikten sonra SHA-256'si manifesttekiyle karsilastirilir;
    ///      uymazsa kurulum yapilmaz.
    /// GitHub virus taramasi yapmadigi icin "viruslu dosya" engeli de ortadan kalkar.
    ///
    /// Dosyalar (GitHub Releases 'latest' altinda, Musteri.GuncellemeTabanUrl):
    ///   guncelleme.manifest        -> metin: "surum=NNN\n sha256=HEX"
    ///   guncelleme.manifest.imza   -> Base64(RSA-SHA256/PKCS1 imza, manifest baytlari uzerinde)
    ///   guncelleme.zip             -> UnsalBakim.exe + UnsalBakim.exe.config (karistirilMAMIS)
    /// </summary>
    public static class GuncellemeGuvenli
    {
        private const string Log = "kurulum.txt";

        public static bool Hazir => Musteri.GuvenliGuncellemeHazir;

        private sealed class ZamanAsimliWebClient : WebClient
        {
            protected override WebRequest GetWebRequest(Uri address)
            {
                var req = base.GetWebRequest(address);
                if (req != null) req.Timeout = 30000;
                var hr = req as HttpWebRequest;
                if (hr != null) hr.AllowAutoRedirect = true; // GitHub asset -> nesne deposuna yonlendirir
                return req;
            }
        }

        private static void TlsHazirla()
        {
            try
            {
                var p = SecurityProtocolType.Tls12;
                try { p |= (SecurityProtocolType)768; } catch { }
                try { p |= (SecurityProtocolType)12288; } catch { }
                ServicePointManager.SecurityProtocol = p;
            }
            catch { try { ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; } catch { } }
        }

        private static byte[] Indir(string dosyaAdi)
        {
            TlsHazirla();
            using (var wc = new ZamanAsimliWebClient())
            {
                wc.Headers["User-Agent"] = "UnsalBakim/1.0";
                return wc.DownloadData(Musteri.GuncellemeTabanUrl.TrimEnd('/') + "/" + dosyaAdi);
            }
        }

        /// <summary>manifest + imzayi indirir, ACIK anahtarla imzayi dogrular, surum ve sha256'yi cikarir.
        /// Imza/format hatasinda Exception firlatir (cagiran onbellege/duruma duser, kurulum YAPILMAZ).</summary>
        public static void ManifestDogrula(out int surum, out string sha256Hex)
        {
            if (!Hazir)
                throw new InvalidOperationException("Guvenli guncelleme yapilandirilmadi (GitHub adresi/ACIK anahtar bos).");

            byte[] manifest = Indir("guncelleme.manifest");
            byte[] imza = Convert.FromBase64String(Encoding.ASCII.GetString(Indir("guncelleme.manifest.imza")).Trim());

            using (var rsa = new RSACryptoServiceProvider())
            {
                rsa.FromXmlString(Musteri.GuncellemeAcikAnahtarXml);
                if (!rsa.VerifyData(manifest, imza, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
                {
                    Logger.WriteFileOnly(Log, "[GUVENLIK] Guncelleme manifest imzasi GECERSIZ -- guncelleme reddedildi.");
                    throw new CryptographicException("Guncelleme imzasi dogrulanamadi.");
                }
            }

            surum = 0; sha256Hex = null;
            foreach (var ln in Encoding.UTF8.GetString(manifest).Replace("\r", "").Split('\n'))
            {
                var s = ln.Trim();
                int i = s.IndexOf('=');
                if (i <= 0) continue;
                string k = s.Substring(0, i).Trim().ToLowerInvariant();
                string v = s.Substring(i + 1).Trim();
                if (k == "surum") int.TryParse(v, out surum);
                else if (k == "sha256") sha256Hex = v.ToLowerInvariant();
            }
            if (surum <= 0 || string.IsNullOrWhiteSpace(sha256Hex) || sha256Hex.Length != 64)
                throw new FormatException("Manifest icerigi gecersiz (surum/sha256 okunamadi).");
        }

        /// <summary>Sadece surumu verir (imza dogrulanir). Yeni surum yoksa ag israfi olmadan karar verilir.</summary>
        public static int SurumAl()
        {
            int surum; string sha;
            ManifestDogrula(out surum, out sha);
            return surum;
        }

        /// <summary>guncelleme.zip'i indirir ve SHA-256'sini beklenen degerle karsilastirir; uymazsa Exception.</summary>
        public static byte[] ZipIndirVeDogrula(string beklenenSha256Hex)
        {
            byte[] zip = Indir("guncelleme.zip");
            string gercek;
            using (var sha = SHA256.Create())
                gercek = BitConverter.ToString(sha.ComputeHash(zip)).Replace("-", "").ToLowerInvariant();
            if (!string.Equals(gercek, (beklenenSha256Hex ?? "").ToLowerInvariant(), StringComparison.Ordinal))
            {
                Logger.WriteFileOnly(Log, "[GUVENLIK] Guncelleme zip SHA-256 UYUSMUYOR (beklenen " + beklenenSha256Hex + ", gelen " + gercek + ") -- reddedildi.");
                throw new CryptographicException("Guncelleme zip butunlugu dogrulanamadi.");
            }
            return zip;
        }
    }
}
