using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace YccSkin;

// data/skinler.json (arac/veri_uret.py'nin oyun dosyalarından ürettiği liste)
public class SkinVeri
{
    [JsonPropertyName("silahlar")] public List<Esya> Silahlar { get; set; } = new();
    [JsonPropertyName("bicaklar")] public List<Esya> Bicaklar { get; set; } = new();
    [JsonPropertyName("eldivenler")] public List<Esya> Eldivenler { get; set; } = new();
    [JsonPropertyName("ajanlar")] public List<Ajan> Ajanlar { get; set; } = new();
    [JsonPropertyName("muzikler")] public List<Basit> Muzikler { get; set; } = new();
    [JsonPropertyName("rozetler")] public List<Rozet> Rozetler { get; set; } = new();

    public Dictionary<int, Esya> Defindex = new();     // silah + bıçak + eldiven
    public Dictionary<string, Esya> BicakAd = new();
    public Dictionary<int, Skin> SkinId = new();        // eski modeli kullananlar için

    public static SkinVeri Oku(string yol)
    {
        var v = JsonSerializer.Deserialize<SkinVeri>(File.ReadAllText(yol)) ?? new();
        foreach (var e in v.Silahlar.Concat(v.Bicaklar).Concat(v.Eldivenler))
        {
            v.Defindex[e.Defindex] = e;
            foreach (var s in e.Skinler) v.SkinId.TryAdd(s.Id, s);
        }
        foreach (var b in v.Bicaklar) v.BicakAd[b.Ad] = b;
        return v;
    }
}

public class Esya
{
    [JsonPropertyName("defindex")] public int Defindex { get; set; }
    [JsonPropertyName("ad")] public string Ad { get; set; } = "";
    [JsonPropertyName("isim")] public string Isim { get; set; } = "";
    [JsonPropertyName("skinler")] public List<Skin> Skinler { get; set; } = new();
}

public class Skin
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("isim")] public string Isim { get; set; } = "";
    [JsonPropertyName("eski")] public bool Eski { get; set; }
}

public class Ajan
{
    [JsonPropertyName("defindex")] public int Defindex { get; set; }
    [JsonPropertyName("model")] public string Model { get; set; } = "";   // veritabanındaki biçim: ctm_sas/ctm_sas_variantg
    [JsonPropertyName("yol")] public string Yol { get; set; } = "";       // agents/models/....vmdl
    [JsonPropertyName("takim")] public string Takim { get; set; } = "";
    [JsonPropertyName("isim")] public string Isim { get; set; } = "";
}

public class Basit
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("isim")] public string Isim { get; set; } = "";
}

public class Rozet
{
    [JsonPropertyName("defindex")] public int Defindex { get; set; }
    [JsonPropertyName("isim")] public string Isim { get; set; } = "";
}

// Bir silahın kayıtlı seçimi (wp_player_skins satırı)
public class SilahSkin
{
    public int Paint, Seed, StatTrakSayi;
    public float Asinma = 0.000001f;
    public bool StatTrak;
    public string? Isim;
    public string[] Cikartma = { "0;0;0;0;0;0;0", "0;0;0;0;0;0;0", "0;0;0;0;0;0;0", "0;0;0;0;0;0;0", "0;0;0;0;0;0;0" };
    public string Anahtarlik = "0;0;0;0;0";
}

public static class Metin
{
    // "id;schema;x;y;wear;scale;rotation" → (id, schema, x, y, wear, scale, rotation); id 0 = boş
    public static (int id, int sema, float x, float y, float asinma, float olcek, float donus)? Cikartma(string s)
    {
        var p = s.Split(';');
        if (p.Length < 7 || !int.TryParse(p[0], out var id) || id <= 0) return null;
        float F(string x) => float.TryParse(x, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : 0;
        int.TryParse(p[1], out var sema);
        return (id, sema, F(p[2]), F(p[3]), F(p[4]), F(p[5]), F(p[6]));
    }

    // "id;x;y;z;seed" → anahtarlık; id 0 = yok
    public static (int id, float x, float y, float z, int tohum)? Anahtarlik(string s)
    {
        var p = s.Split(';');
        if (p.Length < 5 || !int.TryParse(p[0], out var id) || id <= 0) return null;
        float F(string x) => float.TryParse(x, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : 0;
        int.TryParse(p[4], out var tohum);
        return (id, F(p[1]), F(p[2]), F(p[3]), tohum);
    }

    public static bool BicakMi(string sinif) => sinif.Contains("knife") || sinif.Contains("bayonet");

    // Aşınma menüsündeki seçenekler (oyunun kalite aralıklarının alt sınırları)
    public static readonly (string ad, float deger)[] Asinmalar =
    {
        ("Fabrikadan Yeni Çıkmış", 0.000001f), ("Az Aşınmış", 0.07f), ("Görevde Kullanılmış", 0.15f), ("Eskimiş", 0.38f), ("Savaş Görmüş", 0.45f),
    };

    private static readonly Dictionary<string, string> Renkler = BuildRenkler();
    private static Dictionary<string, string> BuildRenkler()
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in typeof(CounterStrikeSharp.API.Modules.Utils.ChatColors).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
            if (f.GetValue(null) is char c) d["{" + f.Name + "}"] = c.ToString();
        return d;
    }
    public static string Renk(string s)
    {
        if (string.IsNullOrEmpty(s) || !s.Contains('{')) return s;
        foreach (var (k, v) in Renkler) s = s.Replace(k, v, StringComparison.OrdinalIgnoreCase);
        return s;
    }
}
