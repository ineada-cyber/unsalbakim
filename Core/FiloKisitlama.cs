using System;
using System.Collections.Generic;

namespace UnsalBakim.Core
{
    /// <summary>
    /// v105 (Ali Bey'in istegi: "Karisikligi onlemek icin hoca ORNEGIN BMTAL-LAB1-ORT1 menuden
    /// ONA SADECE BMTAL-LAB1 ICIN FILO ISLEMI YAPABILMELI, DIGER BOLUMLE ILGISI OLMAMALI,
    /// RAPORLAR ISTEDIGINI YADA TUMUNU ALABILMELI"; istisna sorusuna net cevap: "Hayir, herkes
    /// (ben dahil) sadece kendi labiyla sinirli olsun"; sonra: "SUPER KULLANICI HEPSINI
    /// GOREBILSIN, BU KULLANICIYA DOGRU SIFREYI GIREN, TUM FILO MAKINALARINA ISLEM YAPABILSIN").
    ///
    /// ONEMLI MIMARI NOT: Filo Yonetimi'ndeki Hedef (cboHedef) metninin GERCEK bilgisayar
    /// adlarina eslenmesi SUNUCU TARAFINDA (Apps Script, hedefEslesir_) yapilir -- bu repoda
    /// yoktur ve buradan DEGISTIRILEMEZ/erisilemez. Bu yuzden bu sinif sunucu tarafina HICBIR
    /// SEKILDE DOKUNMAZ; sadece ISTEMCI (bu program) TARAFINDA, kullanicinin Hedef kutusuna ne
    /// YAZABILECEGINI/SECEBILECEGINI VE GONDEREBILECEGINI kisitlayan bir ON KAPI (client-side
    /// guard) saglar -- yani "bu bilgisayardan hangi Filo komutlari gonderilebilir" sorusunu
    /// cevaplar, "bu komut gercekte hangi makinelere ULASIR" sorusunu DEGIL (o zaten sunucuda,
    /// degismeden calismaya devam eder).
    ///
    /// RAPORLAR SEKMESI BU KISITLAMANIN DISINDADIR -- Raporlar kendi ayri kontrolleriyle calisir,
    /// cboHedef'i hic kullanmaz, bu yuzden buradaki kisitlama Raporlar'i hicbir sekilde etkilemez
    /// (Ali Bey'in acik istegi: "Raporlar istedigini yada tumunu alabilmeli").
    /// </summary>
    public static class FiloKisitlama
    {
        /// <summary>Bu OTURUMDA (program acik oldugu surece) Super Kullanici dogru sifreyi girdi
        /// mi? Ali Bey'in acik istegi ("program acik oldugu surece... tekrar sifre sorulmaz"):
        /// BILEREK kalici/diske KAYDEDILMEZ -- her program acilisinda (statik alan varsayilani)
        /// yeniden KILITLI baslar. Kilit dugmesinden tekrar tiklanarak elle de kilitlenebilir.</summary>
        public static bool SuperAcik = false;

        /// <summary>Bu bilgisayarin KENDI lab grubunu bulur -- gruplar sozlugundeki HER grubun
        /// Uyeler alanindaki (',' ile ayrilmis) on-eklerden EN AZ BIRIYLE Environment.MachineName
        /// (buyuk/kucuk harf duyarsiz) BASLIYORSA o grup secilir. Hicbir grup eslesmezse null
        /// doner (bu bilgisayar hicbir gruba atanmamis demektir -- GUVENLI varsayilan: kilitli).
        /// celisenGruplar: cagiran taraf bilgilendirilsin diye -- BIRDEN FAZLA grup eslesirse
        /// (orn. Ali Bey'in Lab Gruplari ekranina yanlislikla AYNI makineyi 2 ayri gruba uye
        /// yazmasi -- bkz. v105 duzeltme asagida) bu liste eslesen TUM grup adlarini icerir ve
        /// metot GUVENLI varsayilan olarak null doner (hangi grubun gecerli oldugu BELIRSIZ
        /// oldugu icin, rastgele birini secmek yerine TAMAMEN KILITLENIR).</summary>
        public static string KendiGrubunuBul(Dictionary<string, string> gruplar, out List<string> celisenGruplar)
        {
            celisenGruplar = null;
            if (gruplar == null || gruplar.Count == 0) return null;
            string makine = (Environment.MachineName ?? "").Trim().ToUpperInvariant();
            if (makine.Length == 0) return null;

            var eslesenler = new List<string>();
            foreach (var kv in gruplar)
            {
                foreach (var onEkHam in (kv.Value ?? "").Split(','))
                {
                    var onEk = onEkHam.Trim().ToUpperInvariant();
                    if (onEk.Length == 0) continue;
                    if (makine.StartsWith(onEk, StringComparison.Ordinal)) { eslesenler.Add(kv.Key); break; }
                }
            }
            if (eslesenler.Count == 0) return null;
            if (eslesenler.Count > 1) { celisenGruplar = eslesenler; return null; } // belirsiz -- guvenli: kilitli
            return eslesenler[0];
        }

        /// <summary>Tek-parametreli kisayol -- celisme bilgisiyle ilgilenmeyen cagiranlar icin.</summary>
        public static string KendiGrubunuBul(Dictionary<string, string> gruplar) => KendiGrubunuBul(gruplar, out _);

        /// <summary>Hedef kutusundan gelen metnin bu bilgisayardan GONDERILMESINE izin olup
        /// olmadigini kontrol eder. true donerse hedef OLDUGU GIBI kullanilabilir/gonderilebilir;
        /// false donerse 'hata' aciklayici bir mesaj icerir ve CAGIRAN TARAF ISLEMI DURDURMALI --
        /// hicbir Filo komutu (Kapat, Dosya Gonder, Politika, vb.) GONDERILMEMELIDIR.</summary>
        public static bool Izinli(string hedef, Dictionary<string, string> gruplar, out string hata)
        {
            hata = null;
            // Ali Bey'in istegi: Super Kullanici dogru sifreyi girdiyse SINIRSIZ (TUMU dahil).
            if (SuperAcik) return true;

            string kendiGrup = KendiGrubunuBul(gruplar, out var celisenGruplar);
            if (kendiGrup == null)
            {
                if (celisenGruplar != null && celisenGruplar.Count > 0)
                {
                    hata = "Bu bilgisayar BIRDEN FAZLA Lab Grubuna uye olarak eklenmis (" +
                           string.Join(", ", celisenGruplar) + ") -- hangisinin gecerli olacagi " +
                           "BELIRSIZ oldugu icin guvenlik amacli HICBIR Filo islemi gonderilemez.\r\n" +
                           "(Filo Yonetimi -> Lab Gruplari'ndan bu bilgisayarin SADECE BIR grupta " +
                           "uye olmasini saglayin, veya Hedef kutusunun yanindaki kilidi acin.)";
                }
                else
                {
                    hata = "Bu bilgisayar hicbir Lab Grubuna atanmamis -- guvenlik icin Super Kullanici " +
                           "sifresi girilmeden HICBIR Filo islemi gonderilemez.\r\n" +
                           "(Filo Yonetimi -> Lab Gruplari'ndan bu bilgisayari bir gruba ekleyin, " +
                           "veya Hedef kutusunun yanindaki kilidi acin.)";
                }
                return false;
            }

            string h = string.IsNullOrWhiteSpace(hedef) ? "TUMU" : hedef.Trim();
            // "TUMU" DAHIL -- Ali Bey'in acik istegi: "herkes (ben dahil) sadece kendi labiyla
            // sinirli olsun", istisnasiz. TUMU, kendi grubunun ADI OLMADIGI surece (ki olamaz,
            // bir grup adi asla "TUMU" olamayacagi varsayilir) burada otomatik REDDEDILIR.
            if (h.Equals(kendiGrup, StringComparison.OrdinalIgnoreCase)) return true;

            string uyeler = gruplar.TryGetValue(kendiGrup, out var u) ? (u ?? "") : "";
            foreach (var onEkHam in uyeler.Split(','))
            {
                var onEk = onEkHam.Trim();
                if (onEk.Length == 0) continue;
                // Hem "kendi grubunun bir uyesi on-ekiyle baslayan" (orn. tam bir ogrenci makinesi
                // adi) HEM "o on-ekin kendisi" (orn. ikinci bir takma ad) kabul edilir.
                if (h.StartsWith(onEk, StringComparison.OrdinalIgnoreCase) ||
                    onEk.StartsWith(h, StringComparison.OrdinalIgnoreCase)) return true;
            }

            hata = "Bu bilgisayar SADECE kendi lab grubuna (" + kendiGrup + ") Filo islemi yapabilir.\r\n" +
                   "'" + h + "' bu gruba ait degil -- gonderilmedi. Tum filoya veya baska bir laba " +
                   "islem yapmak icin Hedef kutusunun yanindaki Super Kullanici kilidini acin.";
            return false;
        }
    }
}
