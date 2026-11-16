using System.Text.Json;

namespace YccSkin;

// FACEIT seviyesinden rozet (2026-11-16). Hiç rozet kaydı olmayan oyuncu için sorulur; FACEIT hesabı yoksa ya da cevap
// gelmezse hiçbir şey yapılmaz (bir sonraki girişte tekrar sorulur). Seçimi olanınkine (Yok dahil) dokunulmaz.
public partial class YccSkin
{
    private static readonly HttpClient FaceitHttp = new() { Timeout = TimeSpan.FromSeconds(6) };

    private async Task<int?> FaceitSeviye(ulong sid)
    {
        var a = Config;
        if (a.FaceitAdres == "") return null;
        try
        {
            using var istek = new HttpRequestMessage(HttpMethod.Get, $"{a.FaceitAdres}?game=cs2&game_player_id={sid}");
            if (a.FaceitAnahtari != "") istek.Headers.Add("Authorization", "Bearer " + a.FaceitAnahtari);
            if (a.FaceitAraciSifre != "") istek.Headers.Add("X-Ycc", a.FaceitAraciSifre);
            using var cevap = await FaceitHttp.SendAsync(istek);
            if (!cevap.IsSuccessStatusCode) return null;   // 404: FACEIT hesabı yok
            using var d = JsonDocument.Parse(await cevap.Content.ReadAsStringAsync());
            return d.RootElement.TryGetProperty("games", out var g) && g.TryGetProperty("cs2", out var c) && c.TryGetProperty("skill_level", out var s)
                ? s.GetInt32() : null;
        }
        catch (Exception e) { Console.WriteLine("[YccSkin] FACEIT sorulamadı: " + e.Message); return null; }
    }

    // Rozet kaydında -1: "FACEIT seviyem" seçildi (siteden ya da !pin'den), bir sonraki yüklemede seviye sorulup gerçek rozet yazılır.
    // FACEIT rozetleri elle seçilemez (sunucu sahibi 2026-11-16): herkes ancak kendi seviyesinin rozetini takabilir.
    public const int FaceitIstek = -1;

    // Oyuncunun seviye rozetini bulup iki takıma kaydeder; bulunamazsa rozet "Yok" (0) olur. Bulunan rozet ve seviye döner.
    private async Task<(int rozet, int? seviye)> FaceitRozetAta(ulong sid, Secimler s)
    {
        int? seviye = await FaceitSeviye(sid);
        var rozet = FaceitRozeti(Config.FaceitRozetleri, seviye);
        foreach (var t in new[] { 2, 3 }) { s.Rozet[t] = rozet; await _db.TakimDegeri("wp_player_pins", "id", sid, t, rozet); }
        return (rozet, seviye);
    }

    // Seviye 1..10 -> ayardaki rozet; geçersizse 0
    public static int FaceitRozeti(IReadOnlyList<int> rozetler, int? seviye) =>
        seviye is >= 1 and <= 10 && rozetler.Count >= seviye ? rozetler[seviye.Value - 1] : 0;
}
