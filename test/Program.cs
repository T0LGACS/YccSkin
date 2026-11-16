using YccSkin;

// YccSkin veri ve ayrıştırma denemesi: dotnet run (bu klasörde)
var hata = 0;
void Esit(object? beklenen, object? gelen, string ne)
{
    if (Equals(beklenen, gelen)) return;
    hata++; Console.WriteLine($"HATA {ne}: beklenen [{beklenen}] gelen [{gelen}]");
}

var v = SkinVeri.Oku("../data/skinler.json");
Esit(true, v.Silahlar.Count >= 35, "silah sayısı");
Esit(true, v.Defindex.ContainsKey(7) && v.Defindex[7].Ad == "weapon_ak47", "AK-47 defindex 7");
Esit(true, v.BicakAd.ContainsKey("weapon_knife_karambit") && v.BicakAd["weapon_knife_karambit"].Defindex == 507, "karambit 507");
Esit(true, v.Defindex.ContainsKey(5030), "spor eldiven 5030");
Esit(true, v.Ajanlar.Any(a => a.Model == "ctm_sas/ctm_sas_variantg" && a.Takim == "ct"), "ajan biçimi veritabanıyla aynı");
Esit(true, v.SkinId.ContainsKey(568) && v.SkinId.ContainsKey(10070), "veritabanındaki örnek skinler");
Esit(true, v.Muzikler.Any(m => m.Id == 1), "varsayılan müzik kiti");

Esit((1234, 0, 0.1f, -0.2f, 0f, 1f, 90f), Metin.Cikartma("1234;0;0.1;-0.2;0;1;90"), "çıkartma");
Esit(null, Metin.Cikartma("0;0;0;0;0;0;0"), "boş çıkartma");
Esit((55, 1.5f, 0f, 2f, 7), Metin.Anahtarlik("55;1.5;0;2;7"), "anahtarlık");
Esit(null, Metin.Anahtarlik("0;0;0;0;0"), "boş anahtarlık");
Esit(true, Metin.BicakMi("weapon_knife_t") && Metin.BicakMi("weapon_bayonet") && !Metin.BicakMi("weapon_ak47"), "bıçak tanıma");
Esit(5, Metin.Asinmalar.Length, "aşınma seçenekleri");

Console.WriteLine(hata == 0 ? "TAMAM: tüm denemeler geçti" : $"{hata} deneme başarısız");
return hata == 0 ? 0 : 1;
