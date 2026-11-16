using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;

namespace YccSkin;

public class Ayarlar : BasePluginConfig
{
    [JsonPropertyName("Veritabani")] public VeritabaniAyar Veritabani { get; set; } = new();
    // CAttributeList::SetOrAddAttributeValueByName imzası. arac/imza_bul.py ile libserver.so'dan bulunur;
    // CS2 güncellemesinden sonra skinler çalışmazsa araç tekrar çalıştırılıp buraya yazılır (eklenti yeniden derlenmez).
    [JsonPropertyName("Imza")] public string Imza { get; set; } = "55 48 89 E5 41 57 41 56 41 55 49 89 FD 41 54 53 48 89 F3 48 83 EC ? F3";
    [JsonPropertyName("On")] public string On { get; set; } = "{GREEN}[Skin]{DEFAULT}";
    [JsonPropertyName("Bekleme")] public float Bekleme { get; set; } = 2;   // iki seçim arası en az saniye

    // FACEIT rozeti (sunucu sahibi 2026-11-16): hiç rozet kaydı olmayan oyuncuya ilk girişte FACEIT seviyesine göre rozet takılır.
    // FACEIT sunucu firmalarının IP'lerini engellediği için istek Cloudflare'deki aracıdan (Worker) geçer: FaceitAdres aracının
    // adresi, FaceitAraciSifre aracının beklediği şifre. Aracı yoksa doğrudan FACEIT adresi ve FaceitAnahtari kullanılır.
    // FaceitRozetleri: seviye 1..10 için rozet numarası. Adres boşsa özellik kapalı.
    [JsonPropertyName("FaceitAdres")] public string FaceitAdres { get; set; } = "";
    [JsonPropertyName("FaceitAnahtari")] public string FaceitAnahtari { get; set; } = "";
    [JsonPropertyName("FaceitAraciSifre")] public string FaceitAraciSifre { get; set; } = "";
    [JsonPropertyName("FaceitRozetleri")] public List<int> FaceitRozetleri { get; set; } = new() { 1339, 1340, 1341, 1342, 1343, 1344, 1357, 1358, 1359, 1360 };   // YccRozet eklentisinde seviye resmine çevrilen madalyalar
}

public class VeritabaniAyar
{
    [JsonPropertyName("Sunucu")] public string Sunucu { get; set; } = "127.0.0.1";
    [JsonPropertyName("Port")] public int Port { get; set; } = 3306;
    [JsonPropertyName("Kullanici")] public string Kullanici { get; set; } = "cs2";
    [JsonPropertyName("Sifre")] public string Sifre { get; set; } = "";
    [JsonPropertyName("Ad")] public string Ad { get; set; } = "cs2";
}
