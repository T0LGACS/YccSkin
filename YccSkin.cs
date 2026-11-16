using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

namespace YccSkin;

// YCC skin sistemi (kullanıcı 2026-11-15: 3. aşama, WeaponPaints + T3 menüsü yerine kendi eklentimiz).
// Skin listesi oyunun kendi dosyalarından (arac/veri_uret.py), skin fonksiyonunun adresi oyunun sunucu dosyasından
// (arac/imza_bul.py) kendi araçlarımızla çıkarıldı. Seçimler eski wp_player_* tablolarında.
public partial class YccSkin : BasePlugin, IPluginConfig<Ayarlar>
{
    public override string ModuleName => "YccSkin";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleAuthor => "Claude";
    public override string ModuleDescription => "YCC skin: silah, bıçak, eldiven, ajan, müzik kiti, rozet";

    public Ayarlar Config { get; set; } = new();
    public void OnConfigParsed(Ayarlar config) => Config = config;

    private Veritabani _db = null!;
    private SkinVeri _veri = new();
    private readonly Dictionary<ulong, Secimler> _secim = new();   // sadece ana iş parçacığı

    public override void Load(bool hotReload)
    {
        _db = new Veritabani(Config.Veritabani);
        _veri = SkinVeri.Oku(Path.Combine(ModuleDirectory, "data", "skinler.json"));
        Console.WriteLine($"[YccSkin] {_veri.Silahlar.Count} silah, {_veri.Bicaklar.Count} bıçak, {_veri.Eldivenler.Count} eldiven, {_veri.Ajanlar.Count} ajan yüklendi");
        UygulamaKur();

        RegisterListener<Listeners.OnServerPrecacheResources>(m => { foreach (var a in _veri.Ajanlar) if (a.Yol != "") m.AddResource(a.Yol); });
        RegisterListener<Listeners.OnClientAuthorized>((slot, steam) => Yukle(slot, steam.SteamId64));
        RegisterListener<Listeners.OnClientDisconnect>(slot => { var p = Utilities.GetPlayerFromSlot(slot); if (p != null) _secim.Remove(p.SteamID); });
        if (hotReload) foreach (var p in Utilities.GetPlayers().Where(Gercek)) Yukle(p.Slot, p.SteamID);

        // Silah oluşunca sahibi bir sonraki karede belli olur, o zaman uygulanır
        // Bıçak modelleri harita yüklenirken indirilsin: oyunda ilk kez seçilen bıçağın modeli yüklenirken oyuncunun oyunu 4-5 sn takılıyor,
        // karakter kilitlenmiş gibi oluyordu (sunucu sahibi 2026-11-16; sunucu kaydında bıçak doğru verilmiş, oyuncu bağlantısı takılmış)
        RegisterListener<Listeners.OnServerPrecacheResources>(m => { foreach (var b in BicakModelleri) m.AddResource($"weapons/models/knife/{b}/weapon_knife_{b[6..]}.vmdl"); });
        RegisterListener<Listeners.OnEntitySpawned>(e =>
        {
            if (!e.DesignerName.StartsWith("weapon_")) return;
            var w = e.As<CBasePlayerWeapon>();
            Server.NextFrame(() => { try { SilahUygula(w); } catch (Exception x) { Console.WriteLine("[YccSkin] silah: " + x.Message); } });
        });
        RegisterEventHandler<EventPlayerSpawn>((e, _) =>
        {
            var p = e.Userid;
            if (Gercek(p)) AddTimer(0.1f, () => { if (Gercek(p)) try { OyuncuUygula(p!); } catch (Exception x) { Console.WriteLine("[YccSkin] oyuncu: " + x.Message); } });
            return HookResult.Continue;
        });

        RegisterEventHandler<EventPlayerDeath>((e, _) =>
        {
            try { StatTrakSay(e); } catch (Exception x) { Console.WriteLine("[YccSkin] stattrak: " + x.Message); }
            return HookResult.Continue;
        });

        TusMenuKur();
        MenuKur();
    }

    private static bool Gercek(CCSPlayerController? p) => p != null && p.IsValid && !p.IsBot && !p.IsHLTV;

    private void Yaz(CCSPlayerController p, string s) => p.PrintToChat(" " + Metin.Renk(Config.On + " " + s));

    private static readonly string[] BicakModelleri = { "knife_bayonet", "knife_bowie", "knife_butterfly", "knife_canis", "knife_cord", "knife_css",
        "knife_falchion", "knife_flip", "knife_gut", "knife_karambit", "knife_kukri", "knife_m9", "knife_navaja", "knife_outdoor", "knife_push",
        "knife_skeleton", "knife_stiletto", "knife_tactical", "knife_talon", "knife_ursus" };

    private static void Hareket(CCSPlayerController h, MoveType_t t)
    {
        var pawn = h.PlayerPawn.Value;
        if (pawn == null || !pawn.IsValid) return;
        pawn.MoveType = t;
        pawn.ActualMoveType = t;
        Utilities.SetStateChanged(pawn, "CBaseEntity", "m_MoveType");
    }

    private void Yukle(int slot, ulong sid)
    {
        Task.Run(async () =>
        {
            try
            {
                var s = await _db.Oku(sid);
                // Hiç rozet kaydı yoksa FACEIT seviyesine göre rozet takılır (bir kez; sonra oyuncu ne seçerse o kalır).
                // "FACEIT seviyem" seçilmişse (-1) seviye her durumda sorulur; hesap yoksa rozet "Yok" olur.
                var faceit = 0;
                int? seviye = null;
                var istendi = s.Rozet.Values.Contains(FaceitIstek);
                if (istendi) (faceit, seviye) = await FaceitRozetAta(sid, s);
                else if (s.Rozet.Count == 0 && (faceit = FaceitRozeti(Config.FaceitRozetleri, seviye = await FaceitSeviye(sid))) > 0)
                    foreach (var t in new[] { 2, 3 }) { s.Rozet[t] = faceit; await _db.TakimDegeri("wp_player_pins", "id", sid, t, faceit); }
                Server.NextFrame(() =>
                {
                    var p = Utilities.GetPlayerFromSlot(slot);
                    if (!Gercek(p) || p!.SteamID != sid) return;
                    _secim[sid] = s;
                    if (faceit > 0) Yaz(p, $"FACEIT seviyene göre {{GREEN}}Lvl {seviye}{{DEFAULT}} rozetin takıldı. Değiştirmek için !pin ya da yccteam.com.tr");
                    else if (istendi) Yaz(p, "{RED}FACEIT hesabın bulunamadı{DEFAULT}, rozet takılmadı. Steam hesabın FACEIT'e bağlı olmalı.");
                    // İlk girişte oyuncu çoğu zaman seçimler yüklenmeden doğuyor: yüklenince hemen uygula
                    if (p.PawnIsAlive) { OyuncuUygula(p); SilahYenile(p, _ => true); }
                });
            }
            catch (Exception e) { Console.WriteLine("[YccSkin] seçimler okunamadı: " + e.Message); }
        });
    }

    private static void Arka(string ne, Func<Task> f) => Task.Run(async () =>
    {
        try { await f(); }
        catch (Exception e) { Console.WriteLine($"[YccSkin] {ne} kaydedilemedi: {e.Message}"); }
    });
}
