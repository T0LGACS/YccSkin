using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

namespace YccSkin;

// !ws, !knife, !eldiven, !ajan, !music, !pin, !st, !wp. Menüler W/S/E/R ile gezilir (TusMenu.cs).
// Silah, bıçak, eldiven, müzik ve rozet seçimi iki takıma birden kaydedilir (eski menü de oyuncunun her iki tarafta
// aynı skini görmesini sağlıyordu); ajan takıma özel.
public partial class YccSkin
{
    private static readonly int[] Takimlar = { 2, 3 };
    private readonly Dictionary<ulong, DateTime> _son = new();

    private void MenuKur()
    {
        foreach (var k in new[] { "ws", "skin" }) AddCommand("css_" + k, "Silah skini seç", (p, _) => { if (Hazir(p)) SilahMenu(p!); });
        AddCommand("css_knife", "Bıçak seç", (p, _) => { if (Hazir(p)) BicakMenu(p!); });
        foreach (var k in new[] { "eldiven", "glove", "gloves" }) AddCommand("css_" + k, "Eldiven seç", (p, _) => { if (Hazir(p)) EldivenMenu(p!); });
        foreach (var k in new[] { "ajan", "agent" }) AddCommand("css_" + k, "Ajan seç", (p, _) => { if (Hazir(p)) AjanMenu(p!); });
        AddCommand("css_music", "Müzik kiti seç", (p, _) => { if (Hazir(p)) ListeMenu(p!, "Müzik kiti", _veri.Muzikler.Select(m => (m.Id, m.Isim)), s => s.Muzik, "wp_player_music", "music_id"); });
        foreach (var k in new[] { "pin", "pins", "coin" }) AddCommand("css_" + k, "Rozet seç", (p, _) => { if (Hazir(p)) RozetMenu(p!); });
        foreach (var k in new[] { "st", "stattrak" }) AddCommand("css_" + k, "Elindeki silahta StatTrak aç/kapa", (p, _) => { if (Hazir(p)) StatTrak(p!); });
        // Site skin panelinden seçim yapılınca site bunu RCON ile çağırır: seçimler veritabanından yeniden okunur, oyundaysa
        // hemen uygulanır (2026-11-16). Sadece sunucu konsolu/RCON; oyuncu başkasının seçimini yeniletemesin.
        AddCommand("css_skinyenile", "Site: bir oyuncunun skin seçimlerini yeniden yükler", (p, info) =>
        {
            if (p != null || !ulong.TryParse(info.GetArg(1), out var sid)) return;
            var o = Utilities.GetPlayerFromSteamId(sid);
            if (Gercek(o)) Yukle(o!.Slot, sid);
        });
        foreach (var k in new[] { "wp", "wp_refresh" }) AddCommand("css_" + k, "Skinleri yeniler", (p, _) =>
        {
            if (!Hazir(p)) return;
            SilahYenile(p!, _ => true);
            OyuncuUygula(p!);
            Yaz(p!, "Skinlerin yenilendi.");
        });
    }

    private bool Hazir(CCSPlayerController? p)
    {
        if (!Gercek(p)) return false;
        if (!_secim.ContainsKey(p!.SteamID)) { Yaz(p, "{RED}Seçimlerin daha yükleniyor, birkaç saniye sonra tekrar dene."); return false; }
        if (_son.TryGetValue(p.SteamID, out var t) && (DateTime.UtcNow - t).TotalSeconds < Config.Bekleme) return false;
        _son[p.SteamID] = DateTime.UtcNow;
        return true;
    }

    private static string Isaret(bool secili, string ad) => secili ? "✓ " + ad : ad;

    // ---- silah skinleri

    private void SilahMenu(CCSPlayerController p)
    {
        var s = _secim[p.SteamID];
        var m = new TusMenu("Silah seç");
        var elde = p.PlayerPawn.Value?.WeaponServices?.ActiveWeapon.Value;
        var eldeDef = elde != null && elde.IsValid ? elde.AttributeManager.Item.ItemDefinitionIndex : -1;
        var liste = _veri.Silahlar.Where(e => e.Skinler.Count > 0).ToList();
        if (s.Bicak.TryGetValue(p.TeamNum, out var bAd) && _veri.BicakAd.TryGetValue(bAd, out var bicak)) liste.Insert(0, bicak);
        var ilk = liste.FirstOrDefault(e => e.Defindex == eldeDef);
        if (ilk != null) { liste.Remove(ilk); liste.Insert(0, ilk); }
        foreach (var e in liste)
        {
            var esya = e;
            var skin = s.Skinler.TryGetValue((p.TeamNum, e.Defindex), out var k) && k.Paint > 0
                ? esya.Skinler.FirstOrDefault(x => x.Id == k.Paint)?.Isim : null;
            m.Ekle($"{(esya == ilk ? "▶ " : "")}{esya.Isim}{(skin != null ? $" ({skin})" : "")}", a => SkinMenu(a, esya));
        }
        Ac(p, m);
    }

    private void SkinMenu(CCSPlayerController p, Esya e)
    {
        var s = _secim[p.SteamID];
        s.Skinler.TryGetValue((p.TeamNum, e.Defindex), out var simdiki);
        var m = new TusMenu($"{e.Isim} skini");
        m.Ekle(Isaret(simdiki == null || simdiki.Paint == 0, "Skinsiz (varsayılan)"), a => SkinKaydet(a, e, 0, 0));
        foreach (var sk in e.Skinler)
        {
            var skin = sk;
            m.Ekle(Isaret(simdiki?.Paint == skin.Id, skin.Isim), a => AsinmaMenu(a, e, skin));
        }
        Ac(p, m);
    }

    private void AsinmaMenu(CCSPlayerController p, Esya e, Skin skin)
    {
        var m = new TusMenu($"{skin.Isim}: aşınma");
        foreach (var (ad, deger) in Metin.Asinmalar)
            m.Ekle(ad, a => SkinKaydet(a, e, skin.Id, deger));
        Ac(p, m);
    }

    private void SkinKaydet(CCSPlayerController p, Esya e, int paint, float asinma)
    {
        var s = _secim[p.SteamID];
        var sid = p.SteamID;
        foreach (var t in Takimlar)
        {
            // Skin kalkınca çıkartmalar kalsın: süsü olan kayıt silinmez, sadece skini sıfırlanır
            if (paint == 0 && !(s.Skinler.TryGetValue((t, e.Defindex), out var eski) && SusVar(eski))) { s.Skinler.Remove((t, e.Defindex)); Arka("skin", () => _db.SkinSil(sid, t, e.Defindex)); continue; }
            if (!s.Skinler.TryGetValue((t, e.Defindex), out var k)) s.Skinler[(t, e.Defindex)] = k = new SilahSkin();
            k.Paint = paint; k.Asinma = asinma; k.Seed = Random.Shared.Next(0, 1000);
            var kopya = new SilahSkin { Paint = k.Paint, Asinma = k.Asinma, Seed = k.Seed, StatTrak = k.StatTrak, StatTrakSayi = k.StatTrakSayi };
            Arka("skin", () => _db.SkinYaz(sid, t, e.Defindex, kopya));
        }
        // Oyundaki menüden seçilen skin canlı uygulanmaz, bir sonraki doğuşta gelir (sunucu sahibi 2026-11-16: canlı yenilemede
        // envanter gidip geliyor, bıçak bazen kilitliyordu). Siteden seçim canlı uygulanmaya devam ediyor.
        Yaz(p, (paint == 0 ? $"{e.Isim}: skin kaldırıldı" : $"{e.Isim}: {{GREEN}}{e.Skinler.First(x => x.Id == paint).Isim}{{DEFAULT}} seçildi")
            + ", {GREEN}bir sonraki doğuşta{DEFAULT} gelecek.");
    }

    // ---- bıçak

    private void BicakMenu(CCSPlayerController p)
    {
        var s = _secim[p.SteamID];
        s.Bicak.TryGetValue(p.TeamNum, out var simdiki);
        var m = new TusMenu("Bıçak seç");
        m.Ekle(Isaret(simdiki == null, "Varsayılan bıçak"), a => BicakKaydet(a, null));
        foreach (var b in _veri.Bicaklar)
        {
            var bicak = b;
            m.Ekle(Isaret(simdiki == bicak.Ad, bicak.Isim), a => BicakKaydet(a, bicak));
        }
        Ac(p, m);
    }

    private void BicakKaydet(CCSPlayerController p, Esya? b)
    {
        var s = _secim[p.SteamID];
        var sid = p.SteamID;
        foreach (var t in Takimlar)
        {
            if (b == null) s.Bicak.Remove(t); else s.Bicak[t] = b.Ad;
            Arka("bıçak", () => _db.TakimDegeri("wp_player_knife", "knife", sid, t, b?.Ad));
        }
        // Bıçak tipi canlı değişmiyor, silahlara hiç dokunulmaz (sunucu sahibi 2026-11-16: "envanter bi yenileniyor ama bıçak gelmiyor")
        Yaz(p, (b == null ? "Varsayılan bıçağa dönülecek" : $"Bıçak: {{GREEN}}{b.Isim}{{DEFAULT}}") + ", bir sonraki doğuşta gelecek. Skinini !ws menüsünden seçebilirsin.");
    }

    // ---- eldiven

    private void EldivenMenu(CCSPlayerController p)
    {
        var s = _secim[p.SteamID];
        s.Eldiven.TryGetValue(p.TeamNum, out var simdiki);
        var m = new TusMenu("Eldiven seç");
        m.Ekle(Isaret(simdiki == 0, "Varsayılan eldiven"), a => EldivenKaydet(a, null, 0, 0));
        foreach (var g in _veri.Eldivenler)
        {
            var eldiven = g;
            m.Ekle(Isaret(simdiki == eldiven.Defindex, eldiven.Isim), a =>
            {
                var sm = new TusMenu($"{eldiven.Isim} skini");
                foreach (var sk in eldiven.Skinler)
                {
                    var skin = sk;
                    sm.Ekle(skin.Isim, b =>
                    {
                        var am = new TusMenu($"{skin.Isim}: aşınma");
                        foreach (var (ad, deger) in Metin.Asinmalar) am.Ekle(ad, c => EldivenKaydet(c, eldiven, skin.Id, deger));
                        Ac(b, am);
                    });
                }
                Ac(a, sm);
            });
        }
        Ac(p, m);
    }

    private void EldivenKaydet(CCSPlayerController p, Esya? g, int paint, float asinma)
    {
        var s = _secim[p.SteamID];
        var sid = p.SteamID;
        foreach (var t in Takimlar)
        {
            if (g == null) { s.Eldiven.Remove(t); Arka("eldiven", () => _db.TakimDegeri("wp_player_gloves", "weapon_defindex", sid, t, null)); continue; }
            s.Eldiven[t] = g.Defindex;
            Arka("eldiven", () => _db.TakimDegeri("wp_player_gloves", "weapon_defindex", sid, t, g.Defindex));
            var k = new SilahSkin { Paint = paint, Asinma = asinma, Seed = Random.Shared.Next(0, 1000) };
            s.Skinler[(t, g.Defindex)] = k;
            Arka("eldiven skini", () => _db.SkinYaz(sid, t, g.Defindex, k));
        }
        if (g != null) OyuncuUygula(p);
        Yaz(p, g == null ? "Varsayılan eldivene dönüldü (bir sonraki doğuşta)." : $"Eldiven: {{GREEN}}{g.Isim}{{DEFAULT}}.");
    }

    // ---- ajan (takıma özel)

    private void AjanMenu(CCSPlayerController p)
    {
        var takim = p.TeamNum == 3 ? "ct" : p.TeamNum == 2 ? "t" : "";
        if (takim == "") { Yaz(p, "{RED}Ajan seçmek için bir takımda olmalısın."); return; }
        var s = _secim[p.SteamID];
        var simdiki = takim == "ct" ? s.AjanCT : s.AjanT;
        var m = new TusMenu($"Ajan seç ({takim.ToUpperInvariant()})");
        m.Ekle(Isaret(simdiki == null, "Varsayılan ajan"), a => AjanKaydet(a, takim, null));
        foreach (var ag in _veri.Ajanlar.Where(x => x.Takim == takim))
        {
            var ajan = ag;
            m.Ekle(Isaret(simdiki == ajan.Model, ajan.Isim), a => AjanKaydet(a, takim, ajan));
        }
        Ac(p, m);
    }

    private void AjanKaydet(CCSPlayerController p, string takim, Ajan? ajan)
    {
        var s = _secim[p.SteamID];
        if (takim == "ct") s.AjanCT = ajan?.Model; else s.AjanT = ajan?.Model;
        var (ct, t, sid) = (s.AjanCT, s.AjanT, p.SteamID);
        Arka("ajan", () => _db.AjanYaz(sid, ct, t));
        if (ajan != null) OyuncuUygula(p);
        Yaz(p, ajan == null ? "Varsayılan ajana dönüldü (bir sonraki doğuşta)." : $"Ajan: {{GREEN}}{ajan.Isim}");
    }

    // ---- müzik kiti / rozet

    private void ListeMenu(CCSPlayerController p, string baslik, IEnumerable<(int id, string isim)> liste,
        Func<Secimler, Dictionary<int, int>> alan, string tablo, string sutun)
    {
        var s = _secim[p.SteamID];
        alan(s).TryGetValue(p.TeamNum, out var simdiki);
        var m = new TusMenu(baslik);
        m.Ekle(Isaret(simdiki == 0, "Yok"), a => ListeKaydet(a, baslik, alan, tablo, sutun, 0, "Yok"));
        foreach (var (id, isim) in liste)
        {
            var (i, ad) = (id, isim);
            m.Ekle(Isaret(simdiki == i, ad), a => ListeKaydet(a, baslik, alan, tablo, sutun, i, ad));
        }
        Ac(p, m);
    }

    private void ListeKaydet(CCSPlayerController p, string baslik, Func<Secimler, Dictionary<int, int>> alan, string tablo, string sutun, int id, string ad)
    {
        var d = alan(_secim[p.SteamID]);
        var sid = p.SteamID;
        // Rozette "Yok" da kaydedilir (0): kayıt silinirse bir sonraki girişte FACEIT rozeti yeniden takılırdı
        var yokKalsin = tablo == "wp_player_pins";
        foreach (var t in Takimlar)
        {
            if (id == 0 && !yokKalsin) d.Remove(t); else d[t] = id;
            Arka(baslik, () => _db.TakimDegeri(tablo, sutun, sid, t, id == 0 && !yokKalsin ? null : id));
        }
        OyuncuUygula(p);
        Yaz(p, $"{baslik}: {{GREEN}}{ad}");
    }

    // ---- rozet: FACEIT rozetleri listede yok, yerine "FACEIT seviyem" var (seviye sorulup kendi rozeti takılır)

    private void RozetMenu(CCSPlayerController p)
    {
        var s = _secim[p.SteamID];
        s.Rozet.TryGetValue(p.TeamNum, out var simdiki);
        var faceitMi = Config.FaceitRozetleri.Contains(simdiki) || simdiki == FaceitIstek;
        var m = new TusMenu("Rozet");
        m.Ekle(Isaret(simdiki == 0, "Yok"), a => ListeKaydet(a, "Rozet", x => x.Rozet, "wp_player_pins", "id", 0, "Yok"));
        if (Config.FaceitAdres != "") m.Ekle(Isaret(faceitMi, "FACEIT seviyem (otomatik)"), FaceitSec);
        foreach (var r in _veri.Rozetler.Where(r => !Config.FaceitRozetleri.Contains(r.Defindex)))
        {
            var (i, ad) = (r.Defindex, r.Isim);
            m.Ekle(Isaret(simdiki == i, ad), a => ListeKaydet(a, "Rozet", x => x.Rozet, "wp_player_pins", "id", i, ad));
        }
        Ac(p, m);
    }

    private void FaceitSec(CCSPlayerController p)
    {
        var (sid, slot) = (p.SteamID, p.Slot);
        Yaz(p, "FACEIT seviyen soruluyor...");
        Task.Run(async () =>
        {
            try
            {
                var seviye = await FaceitSeviye(sid);
                var rozet = FaceitRozeti(Config.FaceitRozetleri, seviye);
                // Seçimler oyun iş parçacığında değişir (oyun aynı sözlüğü okuyor)
                Server.NextFrame(() =>
                {
                    if (_secim.TryGetValue(sid, out var s))
                        foreach (var t in Takimlar) { s.Rozet[t] = rozet; Arka("rozet", () => _db.TakimDegeri("wp_player_pins", "id", sid, t, rozet)); }
                    var o = Utilities.GetPlayerFromSlot(slot);
                    if (!Gercek(o) || o!.SteamID != sid) return;
                    OyuncuUygula(o);
                    Yaz(o, rozet > 0 ? $"FACEIT seviyen {{GREEN}}Lvl {seviye}{{DEFAULT}}, rozetin takıldı." : "{RED}FACEIT hesabın bulunamadı{DEFAULT}, rozet takılmadı. Steam hesabın FACEIT'e bağlı olmalı.");
                });
            }
            catch (Exception e) { Console.WriteLine("[YccSkin] FACEIT rozeti: " + e.Message); }
        });
    }

    // ---- StatTrak

    private void StatTrak(CCSPlayerController p)
    {
        var w = p.PlayerPawn.Value?.WeaponServices?.ActiveWeapon.Value;
        if (w == null || !w.IsValid) return;
        var def = w.AttributeManager.Item.ItemDefinitionIndex;
        var s = _secim[p.SteamID];
        if (!s.Skinler.TryGetValue((p.TeamNum, def), out var k) || k.Paint == 0) { Yaz(p, "{RED}Önce bu silaha !ws ile skin seç."); return; }
        var ac = !k.StatTrak;
        var sid = p.SteamID;
        foreach (var t in Takimlar)
            if (s.Skinler.TryGetValue((t, def), out var x))
            {
                x.StatTrak = ac;
                var kopya = new SilahSkin { Paint = x.Paint, Asinma = x.Asinma, Seed = x.Seed, StatTrak = ac, StatTrakSayi = x.StatTrakSayi };
                Arka("stattrak", () => _db.SkinYaz(sid, t, def, kopya));
            }
        MenudenSonraYenile(p, ww => ww.AttributeManager.Item.ItemDefinitionIndex == def);
        Yaz(p, $"StatTrak {(ac ? "{GREEN}açıldı" : "{RED}kapatıldı")}{{DEFAULT}}.");
    }
}
