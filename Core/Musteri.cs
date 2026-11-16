namespace UnsalBakim.Core
{
    /// <summary>
    /// v50: TEK MERKEZI MUSTERI YAPILANDIRMASI. Bu paketin hangi musteriye/okula ait oldugunu ve o
    /// musterinin Drive/Apps Script baglantisini TEK yerde tutar.
    ///
    /// NEDEN: Su an (2026-10) tek musteri var (Bodrum Mesleki ve Teknik Anadolu Lisesi). Ileride
    /// baska okullara/kurumlara da satilirsa, her musteri icin AYRI bir derleme + ayri bir Drive/
    /// Apps Script baglantisi gerekecek (ayni "UNSAL BAKIM" kaynagindan, farkli musteri bilgileriyle
    /// derlenmis farkli bir .exe). Bu dosya olmadan onceki (v49 ve oncesi) surumlerde bu degerler
    /// DriveLog.cs ve GuncellemeKontrol.cs icinde DAGINIK sabitlerdi -- yeni bir musteri icin kod
    /// icinde arama yapip tek tek degistirmek gerekiyordu. v50'den itibaren: YENI BIR MUSTERIYE
    /// DERLEME YAPARKEN SADECE BU DOSYA DEGISTIRILIR, baska hicbir dosyaya dokunmaya gerek YOK.
    ///
    /// Bu bir refactor'dur -- degerler (WebAppUrl, ApiToken, SurumDosyaId, ZipDosyaId) v49'dakiyle
    /// BIREBIR AYNI, sadece tasindi. Hicbir davranis degismedi, Apps Script'i yeniden dagitmaya
    /// GEREK YOK.
    /// </summary>
    public static class Musteri
    {
        /// <summary>Bu derlemenin ait oldugu musteri/okul kodu. Log ve Envanter kayitlarina da
        /// eklenir (DriveLog.cs) ki ileride ortak/merkezi bir sisteme gecilirse (birden fazla
        /// musterinin verisi ayni yerde tutulursa) her kayit hangi musteriye ait oldugunu zaten
        /// tasisin -- geriye donuk veri tasima/duzeltme derdi olmaz.</summary>
        public const string MusteriId = "BODRUM-BMTAL";

        /// <summary>Bu musterinin kurdugu Apps Script Web App URL'si (musterinin KENDI Google
        /// hesabinda). v49'daki degerle AYNI.</summary>
        public const string WebAppUrl = "https://script.google.com/macros/s/AKfycbw3jCbUaZE2Xw63DmQYfGUku3p7-y4gAEHsZRXGRnqYz1ruOda29JCBtMZs-IXa4Pba/exec";

        /// <summary>Bu musterinin Apps Script'iyle paylasilan gizli anahtar -- Apps Script'teki
        /// API_TOKEN ile AYNI olmali. v49'daki degerle AYNI.</summary>
        public const string ApiToken = "0e8a4ea0aa270227519ad29bd20b1cee";

        /// <summary>Drive'daki surum.txt dosyasinin sabit ID'si (GuncellemeKontrol.cs). v49'daki
        /// degerle AYNI.</summary>
        public const string SurumDosyaId = "1NlA0JDjyxBhBQUF0j4XWa5YxpVAQk8Xd";

        /// <summary>Drive'daki guncelleme.zip dosyasinin sabit ID'si (GuncellemeKontrol.cs).
        /// DUZELTME (v88, 9 Kasim 2026): eski ID (11Gneh0vEtHCER3TQbEt4-HGwojN6ZAnI) artik GECERSIZ --
        /// dosya Drive'da "Cop kutusu"na dusup geri yuklenince (veya yeniden yuklenince) Drive YENI
        /// bir ID atadi; eski ID'ye yapilan indirme istekleri bu yuzden 404 donuyordu (Ali Bey'in
        /// kendi makinesinde canli yakaladigi hata). ONEMLI: Drive'da BUNDAN SONRA guncelleme.zip'i
        /// GUNCELLERKEN, dosyayi SILIP YENIDEN YUKLEMEK yerine ayni dosyanin uzerine "surum olarak"
        /// yukleyin (dosyaya sag tik -> Yonetim -> surum guncelle, veya suruklenen dosyayi AYNI
        /// dosyanin ustune birakin) -- boylece ID bir daha degismez.</summary>
        public const string ZipDosyaId = "1q3zMXLTgz6bsZU_DHzjDL-didb5_gvKs";

        // ============================ v110: GUVENLI GUNCELLEME (GitHub + imza) ============================
        // 13 Kasim 2026 incelemesi: Google Drive guncelleme.zip'i "viruslu" isaretleyip engelledi
        // (sezgisel/ML yanlis alarm). Ayrica guncelleme kriptografik olarak dogrulanmiyordu -- Drive/token
        // ele gecse SYSTEM yetkisiyle sahte exe dagitilabilirdi. v110 cozumu:
        //   1) Guncelleme dosyalari Google Drive yerine GitHub Releases'te tutulur (virus taramasi YOK,
        //      sabit "latest" adresi). Asagidaki URL, Ali Bey'in GitHub kullanici adi ve depo adiyla
        //      DOLDURULACAK (su an KULLANICI/DEPO placeholder -- doldurulana kadar otomatik guncelleme
        //      PASIFTIR, hicbir sey indirilmez; bu BILEREK boyle, dogrulama hazir olmadan uzaktan
        //      guncelleme acilmasin).
        //   2) Her guncelleme, Ali Bey'in OZEL anahtariyla imzalanmis bir manifest tasir; istemci
        //      asagidaki ACIK anahtarla imzayi ve zip'in SHA-256'sini dogrulamadan HICBIR dosyayi
        //      degistirmez. Ozel anahtar Ali Bey'in makinesinde kalir, buraya KONULMAZ.
        // GitHub Releases "latest" taban adresi. Ornek: https://github.com/aliunsal/unsalbakim/releases/latest/download
        public const string GuncellemeTabanUrl = "https://github.com/ineada-cyber/unsalbakim/releases/latest/download";

        // Guncelleme imzasini dogrulayan ACIK anahtar (RSA, .NET XML bicimi). anahtar_uret.ps1 ciktisindaki
        // <RSAKeyValue>...</RSAKeyValue> buraya tek satir olarak yapistirilir. BOS kaldigi surece otomatik
        // guncelleme PASIFTIR (guvenli varsayilan).
        public const string GuncellemeAcikAnahtarXml = "<RSAKeyValue><Modulus>xT1WyTE/hSOu7jQxjkdOoBcZLkeJmR6SIm9lCBe7VXpRhCx1P42g3COmfX3Ng/SY1ZvhD6FS66XMzSIy9Ih+I7MMgl981lv/LlYOaqEHzlL4iE2ggzuTlZQgTZ1wDHQ//Om07MNaDMbCfVpmwaRDe2SwYVF9Pnk2KITzjQoSnIOMfwMOvInDjVWQFPC65Dr/R+t38c3RRslxpsnOFyZTMC6nq0/KqbJCJOlXK+EZRJIoAnhXdWZ7ilVvWBPOURJpBP2eowfc7hv42vFETuoBaOe+6zmMu4/jZQv4L0g5bnceiWAdPDpnF4EUSY9SPXvdt67d2nym02f/j4UpGaMahQ==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";

        /// <summary>GitHub+imza tabanli guvenli guncelleme yapilandirildi mi? (URL dolu + ACIK anahtar dolu.)</summary>
        public static bool GuvenliGuncellemeHazir =>
            !string.IsNullOrWhiteSpace(GuncellemeTabanUrl) && GuncellemeTabanUrl.IndexOf("KULLANICI", System.StringComparison.Ordinal) < 0 &&
            !string.IsNullOrWhiteSpace(GuncellemeAcikAnahtarXml);

        /// <summary>v94 (Ali Bey'in istegi: "Filo'dan dosya gonderebilir miyiz"): "Filo Yonetimi"
        /// panelinden hedefteki makinelere dosya gondermek icin KULLANILAN 6 SABIT Drive dosyasinin
        /// ID'leri -- ZipDosyaId ile AYNI mantik (sabit ID, Ali Bey her gonderecegi dosyayi bu
        /// sabit dosyalardan TURUNE uyanin UZERINE "surum olarak" yukler; SILIP YENIDEN YUKLEMEZ --
        /// aksi halde ZipDosyaId'de yasanan 404 hatasi (bkz. yukarisi) burada da tekrarlanir).
        /// Ali Bey Drive'da (guncelleme klasorunun yaninda) yukle.pdf / yukle.txt / yukle.jpg /
        /// yukle.docx / yukle.exe / yukle.zip adinda 6 bos/placeholder dosya olusturup "baglantiya
        /// sahip herkes" ile paylastiktan sonra buradaki ID'ler GUNCELLENMELI (su an BOS -- dolu
        /// olmayan bir tur panelde secilirse "henuz kurulmadi" hatasi doner, baska bir sey bozulmaz).</summary>
        public const string DosyaPdfId = "1wpNsJsQTnbwacQ2J4f82sj6EenKpCCuv";
        public const string DosyaTxtId = "1xDRchMEh5aR8i7NS5L-KsmO5PV1a98V2";
        public const string DosyaJpgId = "1dvlk18j1VIP8fmLirZ5iygYOxn6RtZmw";
        public const string DosyaDocxId = "10VAvLjW4PACWc2AbiZgH1pRSGDKvCLUB";
        public const string DosyaExeId = "1QBo3KEWeXCxTG590Y9X6wRPqAa-XqfUK";
        public const string DosyaZipId = "1gQKSDdNEFB0PAl2ShVAXDqmddWsE5cci";

        /// <summary>v105 (Ali Bey'in istegi: "SÜPER yeni şifre bir yerine koysak mi sabit,
        /// programa gömsek"): Filo Yonetimi'ndeki Hedef kutusunun yanindaki kilit dugmesine
        /// girilince, bu bilgisayarin (program acik oldugu surece) TUM filoya (TUMU + her lab +
        /// her makine) islem yapabilmesini saglayan SABIT/GOMULU sifre -- bkz. Core/FiloKisitlama.cs.
        /// Her kurulumda AYNI (musteri-basina ozellestirilmez); degistirmek icin sadece bu satir
        /// guncellenip program yeniden derlenir.</summary>
        public const string SuperKullaniciSifre = "BTan106120";
    }
}
