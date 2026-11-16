using System.Net;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

namespace YccSkin;

// Tuşla gezilen ekran ortası menüsü (YccAdmin ile aynı menü (kullanıcı 2026-11-15: "!admin menüsü w s r e tuşlarıyla yönlendirilebilir olsun,
// eskisi öyleydi"). Eski menü kütüphanesinin (MenuManagerCore) tuşlarıyla aynı:
//   W yukarı, S aşağı, E seç, R kapat, Ctrl (eğil) bir önceki menü. Menü açıkken oyuncu yürüyemez (eskisinde de öyleydi).
public class TusMenu(string baslik)
{
    public string Baslik { get; } = baslik;
    public List<(string ad, Action<CCSPlayerController> f, bool kapali)> Secenekler { get; } = new();
    public void Ekle(string ad, Action<CCSPlayerController> f, bool kapali = false) => Secenekler.Add((ad, f, kapali));
}

public partial class YccSkin
{
    private class AcikMenu
    {
        public required TusMenu Menu;
        public int Secili;
        public AcikMenu? Onceki;
        public PlayerButtons SonTus;
        public MoveType_t EskiHareket;
    }

    private readonly Dictionary<int, AcikMenu> _menuler = new();   // slot → açık menü
    private const int MenuSatir = 6;

    private void TusMenuKur()
    {
        RegisterListener<Listeners.OnTick>(() =>
        {
            if (_menuler.Count == 0) return;
            foreach (var (slot, a) in _menuler.ToList())
            {
                var p = Utilities.GetPlayerFromSlot(slot);
                if (!Gercek(p)) { _menuler.Remove(slot); continue; }
                // Menü açıkken karakter yerinden oynamasın (kullanıcı 2026-11-15): hareket türü bir kez değiştirmek yetmedi,
                // her karede yeniden donduruluyor ve hız çarpanı sıfırlanıyor
                var pawn = p!.PlayerPawn.Value;
                if (pawn != null && pawn.IsValid && p.PawnIsAlive)
                {
                    if (pawn.MoveType != MoveType_t.MOVETYPE_OBSOLETE) Hareket(p, MoveType_t.MOVETYPE_OBSOLETE);
                    pawn.VelocityModifier = 0;
                }
                var tus = p.Buttons;
                var yeni = tus & ~a.SonTus;   // bu karede yeni basılan tuşlar
                a.SonTus = tus;
                var n = a.Menu.Secenekler.Count;
                if ((yeni & PlayerButtons.Forward) != 0 && n > 0) a.Secili = (a.Secili - 1 + n) % n;
                else if ((yeni & PlayerButtons.Back) != 0 && n > 0) a.Secili = (a.Secili + 1) % n;
                else if ((yeni & PlayerButtons.Reload) != 0) { MenuKapat(p); continue; }
                else if ((yeni & PlayerButtons.Duck) != 0) { if (a.Onceki != null) { a.Onceki.SonTus = tus; _menuler[slot] = a.Onceki; } else MenuKapat(p); continue; }
                else if ((yeni & PlayerButtons.Use) != 0 && n > 0)
                {
                    var (_, f, kapali) = a.Menu.Secenekler[a.Secili];
                    if (kapali) continue;
                    f(p);
                    // Seçenek yeni bir menü açmadıysa iş bitti, menü kapanır
                    if (_menuler.TryGetValue(slot, out var simdi) && simdi == a) MenuKapat(p);
                    continue;
                }
                p.PrintToCenterHtml(MenuHtml(a));
            }
        });
        RegisterListener<Listeners.OnClientDisconnect>(slot => _menuler.Remove(slot));
        RegisterEventHandler<EventPlayerDeath>((e, i) => { if (e.Userid != null) MenuKapat(e.Userid); return HookResult.Continue; });
    }

    // Yeni menü açar; zaten açık bir menü varsa onun üstüne (Ctrl ile geri dönülür)
    private void Ac(CCSPlayerController p, TusMenu m)
    {
        _menuler.TryGetValue(p.Slot, out var onceki);
        var a = new AcikMenu { Menu = m, Onceki = onceki, SonTus = p.Buttons, EskiHareket = onceki?.EskiHareket ?? MoveType_t.MOVETYPE_WALK };
        if (onceki == null)
        {
            var pawn = p.PlayerPawn.Value;
            if (pawn != null && pawn.IsValid && p.PawnIsAlive) { a.EskiHareket = pawn.MoveType; Hareket(p, MoveType_t.MOVETYPE_OBSOLETE); }
        }
        _menuler[p.Slot] = a;
    }

    private void MenuKapat(CCSPlayerController p)
    {
        if (!_menuler.Remove(p.Slot, out var a)) return;
        while (a.Onceki != null) a = a.Onceki;
        if (p.PawnIsAlive)
        {
            Hareket(p, a.EskiHareket == MoveType_t.MOVETYPE_OBSOLETE ? MoveType_t.MOVETYPE_WALK : a.EskiHareket);
            if (p.PlayerPawn.Value is { IsValid: true } pawn) pawn.VelocityModifier = 1;   // menüde 0'a çekilmişti
        }
        p.PrintToCenterHtml(" ");
    }

    private static string MenuHtml(AcikMenu a)
    {
        var s = a.Menu.Secenekler;
        var bas = Math.Clamp(a.Secili - MenuSatir / 2, 0, Math.Max(0, s.Count - MenuSatir));
        var html = $"<font color='#ff5555' class='fontSize-m'>{WebUtility.HtmlEncode(a.Menu.Baslik)}</font><br>";
        for (var i = bas; i < Math.Min(s.Count, bas + MenuSatir); i++)
        {
            var ad = WebUtility.HtmlEncode(s[i].ad);
            html += i == a.Secili
                ? $"<font color='#ffd24d'>▶ {ad}</font><br>"
                : s[i].kapali ? $"<font color='#777777'>{ad}</font><br>" : $"<font color='#ffffff'>{ad}</font><br>";
        }
        if (s.Count == 0) html += "<font color='#777777'>(boş)</font><br>";
        return html + "<font color='#aaaaaa' class='fontSize-s'>W/S: gez · E: seç · Ctrl: geri · R: kapat</font>";
    }
}
