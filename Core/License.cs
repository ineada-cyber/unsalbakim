using System;

namespace UnsalBakim.Core
{
    /// <summary>Surum bilgisi + ileride ticari lisans (seri no) icin yer tutucu.
    /// Beta surumu sure sinirsiz calisir (son kullanim tarihi yoktur).</summary>
    public static class License
    {
        public const string Surum = "1.0-beta v112";
        /// <summary>Surum NUMARASI (sadece sayi) -- Drive'daki surum.txt ile karsilastirmak icin.
        /// Her yeni surumde Surum metniyle BIRLIKTE bunu da artirin.</summary>
        public const int SurumNo = 112;

        // Sure siniri KALDIRILDI: program her zaman gecerli.
        public static bool BetaGecerli => true;

        public static string Durum => "Beta surum - suresiz";

        /// <summary>v50: Ticari lisans icin ISKELET -- her derlemenin (Musteri.MusteriId'ye bagli)
        /// kendi seri numarasi olsun diye sekli hazirlandi. Su an HENUZ gercek bir dogrulama/
        /// aktivasyon YOK (asagidaki LisansGecerliMi hala pratikte hep true doner) -- amac, ileride
        /// cevrimici kontrol/aktivasyon eklendiginde sadece LisansGecerliMi'nin ICINI degistirmek,
        /// bu sabiti cagiran baska hicbir kodu (Program.cs vs.) DEGISTIRMEMEK.</summary>
        public const string SeriNo = "UNSAL-" + Musteri.MusteriId;

        // TODO (ticari): gercek dogrulama/aktivasyon (cevrimici kontrol, makine kimligine bagli
        // aktivasyon, lisans suresi/durumu) -- su an sadece SeriNo'nun bos olmadigina bakiyor.
        public static bool LisansGecerliMi(string seri) => !string.IsNullOrWhiteSpace(seri);
    }
}
