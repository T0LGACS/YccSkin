using MySqlConnector;

namespace YccSkin;

// Oyuncunun tüm seçimleri. Takım: 2 = T, 3 = CT (eski WeaponPaints tablolarıyla aynı)
public class Secimler
{
    public Dictionary<(int takim, int def), SilahSkin> Skinler = new();
    public Dictionary<int, string> Bicak = new();
    public Dictionary<int, int> Eldiven = new();
    public string? AjanCT, AjanT;
    public Dictionary<int, int> Muzik = new();
    public Dictionary<int, int> Rozet = new();
}

// wp_player_* tablolarını aynen kullanır: 1.250 oyuncunun eski seçimleri korunur.
public class Veritabani(VeritabaniAyar a)
{
    private readonly string _baglanti = new MySqlConnectionStringBuilder
    {
        Server = a.Sunucu, Port = (uint)a.Port, UserID = a.Kullanici, Password = a.Sifre, Database = a.Ad,
        CharacterSet = "utf8mb4", ConnectionTimeout = 5, DefaultCommandTimeout = 10, Pooling = true,
    }.ConnectionString;

    private async Task<MySqlConnection> Ac() { var c = new MySqlConnection(_baglanti); await c.OpenAsync(); return c; }

    private static MySqlCommand Komut(MySqlConnection c, string sql, params (string, object?)[] p)
    {
        var k = new MySqlCommand(sql, c);
        foreach (var (ad, deger) in p) k.Parameters.AddWithValue(ad, deger ?? DBNull.Value);
        return k;
    }

    public async Task<Secimler> Oku(ulong sid)
    {
        var s = new Secimler();
        var id = sid.ToString();
        await using var c = await Ac();
        await using (var k = Komut(c, @"SELECT weapon_team, weapon_defindex, weapon_paint_id, weapon_wear, weapon_seed, weapon_nametag, weapon_stattrak, weapon_stattrak_count,
            weapon_sticker_0, weapon_sticker_1, weapon_sticker_2, weapon_sticker_3, weapon_sticker_4, weapon_keychain FROM wp_player_skins WHERE steamid = @s", ("@s", id)))
        await using (var r = await k.ExecuteReaderAsync())
            while (await r.ReadAsync())
                s.Skinler[(r.GetInt32(0), r.GetInt32(1))] = new SilahSkin
                {
                    Paint = r.GetInt32(2), Asinma = r.GetFloat(3), Seed = r.GetInt32(4), Isim = r.IsDBNull(5) ? null : r.GetString(5),
                    StatTrak = r.GetBoolean(6), StatTrakSayi = r.GetInt32(7),
                    Cikartma = new[] { r.GetString(8), r.GetString(9), r.GetString(10), r.GetString(11), r.GetString(12) }, Anahtarlik = r.GetString(13),
                };
        await using (var k = Komut(c, "SELECT weapon_team, knife FROM wp_player_knife WHERE steamid = @s", ("@s", id)))
        await using (var r = await k.ExecuteReaderAsync())
            while (await r.ReadAsync()) s.Bicak[r.GetInt32(0)] = r.GetString(1);
        await using (var k = Komut(c, "SELECT weapon_team, weapon_defindex FROM wp_player_gloves WHERE steamid = @s", ("@s", id)))
        await using (var r = await k.ExecuteReaderAsync())
            while (await r.ReadAsync()) s.Eldiven[r.GetInt32(0)] = r.GetInt32(1);
        await using (var k = Komut(c, "SELECT agent_ct, agent_t FROM wp_player_agents WHERE steamid = @s", ("@s", id)))
        await using (var r = await k.ExecuteReaderAsync())
            if (await r.ReadAsync()) { s.AjanCT = r.IsDBNull(0) ? null : r.GetString(0); s.AjanT = r.IsDBNull(1) ? null : r.GetString(1); }
        await using (var k = Komut(c, "SELECT weapon_team, music_id FROM wp_player_music WHERE steamid = @s", ("@s", id)))
        await using (var r = await k.ExecuteReaderAsync())
            while (await r.ReadAsync()) s.Muzik[r.GetInt32(0)] = r.GetInt32(1);
        await using (var k = Komut(c, "SELECT weapon_team, id FROM wp_player_pins WHERE steamid = @s", ("@s", id)))
        await using (var r = await k.ExecuteReaderAsync())
            while (await r.ReadAsync()) s.Rozet[r.GetInt32(0)] = r.GetInt32(1);
        return s;
    }

    public async Task SkinYaz(ulong sid, int takim, int def, SilahSkin k)
    {
        await using var c = await Ac();
        await using var q = Komut(c, @"INSERT INTO wp_player_skins (steamid, weapon_team, weapon_defindex, weapon_paint_id, weapon_wear, weapon_seed, weapon_stattrak, weapon_stattrak_count)
            VALUES (@s, @t, @d, @p, @w, @sd, @st, @sc)
            ON DUPLICATE KEY UPDATE weapon_paint_id = @p, weapon_wear = @w, weapon_seed = @sd, weapon_stattrak = @st, weapon_stattrak_count = @sc",
            ("@s", sid.ToString()), ("@t", takim), ("@d", def), ("@p", k.Paint), ("@w", k.Asinma), ("@sd", k.Seed), ("@st", k.StatTrak), ("@sc", k.StatTrakSayi));
        await q.ExecuteNonQueryAsync();
    }

    // Sadece StatTrak sayısı (her öldürmede; skin satırının geri kalanına dokunmaz)
    public async Task StatTrakYaz(ulong sid, int takim, int def, int sayi)
    {
        await using var c = await Ac();
        await using var q = Komut(c, "UPDATE wp_player_skins SET weapon_stattrak_count = @c WHERE steamid = @s AND weapon_team = @t AND weapon_defindex = @d",
            ("@c", sayi), ("@s", sid.ToString()), ("@t", takim), ("@d", def));
        await q.ExecuteNonQueryAsync();
    }

    public async Task SkinSil(ulong sid, int takim, int def)
    {
        await using var c = await Ac();
        await using var q = Komut(c, "DELETE FROM wp_player_skins WHERE steamid = @s AND weapon_team = @t AND weapon_defindex = @d", ("@s", sid.ToString()), ("@t", takim), ("@d", def));
        await q.ExecuteNonQueryAsync();
    }

    // Tek değerli tablolar (bıçak, eldiven, müzik, rozet): değer null ise satır silinir
    public async Task TakimDegeri(string tablo, string sutun, ulong sid, int takim, object? deger)
    {
        if (tablo is not ("wp_player_knife" or "wp_player_gloves" or "wp_player_music" or "wp_player_pins")) throw new ArgumentException(tablo);
        await using var c = await Ac();
        await using var q = deger == null
            ? Komut(c, $"DELETE FROM {tablo} WHERE steamid = @s AND weapon_team = @t", ("@s", sid.ToString()), ("@t", takim))
            : Komut(c, $"INSERT INTO {tablo} (steamid, weapon_team, {sutun}) VALUES (@s, @t, @v) ON DUPLICATE KEY UPDATE {sutun} = @v",
                ("@s", sid.ToString()), ("@t", takim), ("@v", deger));
        await q.ExecuteNonQueryAsync();
    }

    public async Task AjanYaz(ulong sid, string? ct, string? t)
    {
        await using var c = await Ac();
        await using var q = Komut(c, "INSERT INTO wp_player_agents (steamid, agent_ct, agent_t) VALUES (@s, @c, @t) ON DUPLICATE KEY UPDATE agent_ct = @c, agent_t = @t",
            ("@s", sid.ToString()), ("@c", ct), ("@t", t));
        await q.ExecuteNonQueryAsync();
    }
}
