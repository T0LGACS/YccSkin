using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;

namespace YccSkin;

// Skinin oyuna uygulanması. Silah/bıçak/eldivenin "eşya görünümüne" (CEconItemView) skin öznitelikleri yazılır:
// "set item texture prefab" (skin no), "... seed", "... wear", StatTrak, çıkartma ve anahtarlık öznitelikleri.
// Öznitelikleri oyunun kendi fonksiyonu (SetOrAddAttributeValueByName) yazar; adresi Ayarlar.Imza'dan.
public partial class YccSkin
{
    private MemoryFunctionVoid<nint, string, float>? _ozellikYaz;
    private ulong _esyaNo = 68000;   // her uygulamada yeni eşya numarası: istemci görünümü yeniler

    private void UygulamaKur()
    {
        try { _ozellikYaz = new MemoryFunctionVoid<nint, string, float>(Config.Imza); }
        catch (Exception e) { Console.WriteLine($"[YccSkin] skin fonksiyonu bulunamadı, imza eski olabilir (arac/imza_bul.py): {e.Message}"); }
    }

    // Tamsayı olarak saklanan öznitelikler (kimlik, StatTrak sayısı): bitleri float olarak geçilir
    private static float Bit(int x) => BitConverter.Int32BitsToSingle(x);

    private void Ozellik(CEconItemView item, string ad, float deger)
    {
        if (_ozellikYaz == null) return;
        _ozellikYaz.Invoke(item.NetworkedDynamicAttributes.Handle, ad, deger);
        _ozellikYaz.Invoke(item.AttributeList.Handle, ad, deger);
    }

    private void YeniNumara(CEconItemView item, ulong steamId)
    {
        var no = _esyaNo++;
        item.ItemID = no;
        item.ItemIDLow = (uint)(no & 0xFFFFFFFF);
        item.ItemIDHigh = (uint)(no >> 32);
        item.AccountID = (uint)(steamId & 0xFFFFFFFF);
    }

    // Yeni oluşan silah: sahibinin seçimi uygulanır. Yerden alınan başkasının silahına dokunulmaz.
    private void SilahUygula(CBasePlayerWeapon w)
    {
        if (!w.IsValid) return;
        var sahip = SilahSahibi(w);
        if (sahip == null || !_secim.TryGetValue(sahip.SteamID, out var s)) return;
        var item = w.AttributeManager.Item;
        var takim = sahip.TeamNum;
        int def = item.ItemDefinitionIndex;

        var bicakDegisti = false;
        if (Metin.BicakMi(w.DesignerName) && s.Bicak.TryGetValue(takim, out var bicakAd) && _veri.BicakAd.TryGetValue(bicakAd, out var bicak))
        {
            if (def != bicak.Defindex)
            {
                item.ItemDefinitionIndex = (ushort)bicak.Defindex;
                w.AcceptInput("ChangeSubclass", w, w, bicak.Defindex.ToString());
                def = bicak.Defindex;
            }
            item.EntityQuality = 3;   // ★ (bıçaklar "olağandışı" kalitede)
            bicakDegisti = true;
        }
        else if (Metin.BicakMi(w.DesignerName) && def != 42 && def != 59 && _veri.Defindex.ContainsKey(def))
        {
            // Bıçak seçimi kaldırıldı ("Varsayılan bıçak"): takımın varsayılan bıçağına dön (CT 42, T 59)
            var varsayilan = takim == 2 ? 59 : 42;
            item.ItemDefinitionIndex = (ushort)varsayilan;
            w.AcceptInput("ChangeSubclass", w, w, varsayilan.ToString());
            item.EntityQuality = 0;
            def = varsayilan;
            bicakDegisti = true;
        }

        // Skinsiz silaha da çıkartma / anahtarlık konabilir (sunucu sahibi 2026-11-16): kayıt varsa ve içinde ya skin ya süs varsa uygulanır
        if (!s.Skinler.TryGetValue((takim, def), out var k) || (k.Paint <= 0 && !SusVar(k)))
        {
            if (bicakDegisti) YeniNumara(item, sahip.SteamID);
            return;
        }

        YeniNumara(item, sahip.SteamID);
        if (k.Paint > 0)
        {
            if (k.StatTrak && !bicakDegisti) item.EntityQuality = 9;
            w.FallbackPaintKit = k.Paint;
            w.FallbackSeed = k.Seed;
            w.FallbackWear = k.Asinma;
            w.FallbackStatTrak = k.StatTrak ? k.StatTrakSayi : -1;
            Ozellik(item, "set item texture prefab", k.Paint);
            Ozellik(item, "set item texture seed", k.Seed);
            Ozellik(item, "set item texture wear", k.Asinma);
            if (k.StatTrak)
            {
                Ozellik(item, "kill eater", Bit(k.StatTrakSayi));
                Ozellik(item, "kill eater score type", Bit(0));
            }
        }
        if (!string.IsNullOrEmpty(k.Isim)) item.CustomName = k.Isim;

        for (var i = 0; i < 5; i++)
        {
            if (Metin.Cikartma(k.Cikartma[i]) is not { } c) continue;
            Ozellik(item, $"sticker slot {i} id", Bit(c.id));
            if (c.sema != 0) Ozellik(item, $"sticker slot {i} schema", Bit(c.sema));
            // Site konum/boyut seçtirmiyor (0 gelir): 0 olanlar yazılmaz, oyun çıkartmayı o yuvanın kendi yerine kendi boyutunda koyar
            if (c.x != 0) Ozellik(item, $"sticker slot {i} offset x", c.x);
            if (c.y != 0) Ozellik(item, $"sticker slot {i} offset y", c.y);
            Ozellik(item, $"sticker slot {i} wear", c.asinma);
            if (c.olcek > 0) Ozellik(item, $"sticker slot {i} scale", c.olcek);
            if (c.donus != 0) Ozellik(item, $"sticker slot {i} rotation", c.donus);
        }
        if (Metin.Anahtarlik(k.Anahtarlik) is { } a)
        {
            Ozellik(item, "keychain slot 0 id", Bit(a.id));
            Ozellik(item, "keychain slot 0 offset x", a.x);
            Ozellik(item, "keychain slot 0 offset y", a.y);
            Ozellik(item, "keychain slot 0 offset z", a.z);
            Ozellik(item, "keychain slot 0 seed", Bit(a.tohum));
        }

        // Eski (CS:GO) modelli skinler silahın "body" gövde grubunun 1. seçeneğinde görünür. Sadece sunucudaki model
        // maskesini değiştirmek istemciye gitmedi: Yabani Lotus CS2 modelinde kaldı, doku görünmedi (sunucu sahibi 2026-11-15).
        // Gövde grubu oyuna giriş komutuyla (SetBodygroup) değiştirilir, bu istemciye ulaşır.
        var eski = _veri.SkinId.TryGetValue(k.Paint, out var sk) && sk.Eski;
        w.AcceptInput("SetBodygroup", w, w, $"body,{(eski ? 1 : 0)}");
        var iskelet = w.CBodyComponent?.SceneNode?.GetSkeletonInstance();
        if (iskelet != null) iskelet.ModelState.MeshGroupMask = eski ? 2UL : 1UL;
    }

    private static bool SusVar(SilahSkin k) => k.Cikartma.Any(c => Metin.Cikartma(c) != null) || Metin.Anahtarlik(k.Anahtarlik) != null;

    private static CCSPlayerController? SilahSahibi(CBasePlayerWeapon w)
    {
        var pawn = w.OwnerEntity.Value?.As<CCSPlayerPawn>();
        var p = pawn != null && pawn.IsValid ? pawn.OriginalController.Value : null;
        if (p == null || !p.IsValid || p.IsBot) return null;
        // Silahı ilk alan bu oyuncu değilse (yerden aldıysa) dokunma
        if (w.OriginalOwnerXuidLow != 0 && w.OriginalOwnerXuidLow != (uint)(p.SteamID & 0xFFFFFFFF)) return null;
        return p;
    }

    // Doğuşta: ajan modeli, eldiven, müzik kiti, rozet
    private void OyuncuUygula(CCSPlayerController p)
    {
        if (!_secim.TryGetValue(p.SteamID, out var s) || !p.PawnIsAlive) return;
        var takim = p.TeamNum;
        var pawn = p.PlayerPawn.Value;
        if (pawn == null || !pawn.IsValid) return;

        var ajan = takim == 3 ? s.AjanCT : takim == 2 ? s.AjanT : null;
        if (ajan != null)
        {
            var yol = _veri.Ajanlar.FirstOrDefault(x => x.Model == ajan)?.Yol ?? $"characters/models/{ajan}.vmdl";
            pawn.SetModel(yol);
        }

        if (s.Eldiven.TryGetValue(takim, out var eldiven) && eldiven > 0)
            AddTimer(0.08f, () => EldivenTak(p, pawn, s, takim, eldiven));

        var envanter = p.InventoryServices;
        if (envanter != null)
        {
            if (s.Muzik.TryGetValue(takim, out var muzik) && muzik > 0) envanter.MusicID = (ushort)muzik;
            if (s.Rozet.TryGetValue(takim, out var rozet) && rozet > 0) envanter.Rank[5] = (CounterStrikeSharp.API.Core.MedalRank_t)rozet;
            Utilities.SetStateChanged(p, "CCSPlayerController", "m_pInventoryServices");
        }
    }

    // Eldiven: eski öznitelikler temizlenip skin yazılır. İstemci eldiveni, oyuncu modelinin "first_or_third_person"
    // gövde grubu değişince yeniden kuruyor: 0 yapılıp kısa süre sonra 1'e döndürülür. Model değiştirme, sayaç ve
    // "varsayılan eldiveni gizle" denemeleri işe yaramamıştı (2026-11-15, sunucu sahibinin denemeleri).
    private void EldivenTak(CCSPlayerController p, CCSPlayerPawn pawn, Secimler s, int takim, int eldiven)
    {
        if (!p.IsValid || !p.PawnIsAlive || !pawn.IsValid) return;
        var g = pawn.EconGloves;
        g.NetworkedDynamicAttributes.Attributes.RemoveAll();
        g.AttributeList.Attributes.RemoveAll();
        g.ItemDefinitionIndex = (ushort)eldiven;
        YeniNumara(g, p.SteamID);
        if (s.Skinler.TryGetValue((takim, eldiven), out var k) && k.Paint > 0)
        {
            Ozellik(g, "set item texture prefab", k.Paint);
            Ozellik(g, "set item texture seed", k.Seed);
            Ozellik(g, "set item texture wear", k.Asinma);
        }
        g.Initialized = true;
        p.ExecuteClientCommand("lastinv");
        pawn.AcceptInput("SetBodygroup", null, null, "first_or_third_person,0");
        AddTimer(0.2f, () =>
        {
            if (pawn.IsValid) pawn.AcceptInput("SetBodygroup", null, null, "first_or_third_person,1");
            if (p.IsValid && p.PawnIsAlive) p.ExecuteClientCommand("lastinv");
        });
    }

    private readonly HashSet<int> _yenileniyor = new();

    // Seçim değişince yeni skin hemen görünsün diye silahlar yerinde güncellenir (yeniden verilmez).
    // Silahı kaldırıp yeniden vermek önce bıçakta el boşluğu yaptı, sonra silahlarda silah hiç geri gelmedi
    // (sunucu sahibi 2026-11-16: "silah kayboluyor", skin ancak ölünce değişiyordu). Bıçakta işe yarayan yol hepsine uygulanır:
    // - elde olmayan silah: öznitelikleri temizlenip seçim yeniden yazılır, çekilince yeni haliyle gelir;
    // - eldeki silah: önce başka silaha geçilir, sonra güncellenip geri dönülür (elde bıçağın tipi değişince oyun bıçağı siliyor).
    private void SilahYenile(CCSPlayerController p, Func<CBasePlayerWeapon, bool> hangisi)
    {
        var pawn = p.PlayerPawn.Value;
        var slot = p.Slot;
        if (pawn == null || !pawn.IsValid || !p.PawnIsAlive || pawn.WeaponServices == null || !_yenileniyor.Add(slot)) return;
        AddTimer(0.4f, () => _yenileniyor.Remove(slot));
        var aktif = pawn.WeaponServices.ActiveWeapon.Value;
        var hepsi = pawn.WeaponServices.MyWeapons.Select(h => h.Value).Where(w => w != null && w.IsValid).Select(w => w!).ToList();

        foreach (var w in hepsi.Where(hangisi).Where(w => SilahSahibi(w)?.Slot == p.Slot))
        {
            var bicakMi = Metin.BicakMi(w.DesignerName);
            // Bıçağın tipi canlı değişmez, bir sonraki doğuşta gelir (sunucu sahibi 2026-11-16): yerinde değişince adı ve kaplaması eski
            // bıçakta kaldı, kaldırıp yeniden vermek de oyuncunun oyununu kilitledi (elindekiler görünmüyor, yürüyemiyor; sunucuda her şey doğruydu).
            if (bicakMi && BicakTipiDegisiyor(p, w)) { Yaz(p, "Yeni bıçağın {GREEN}bir sonraki doğuşta{DEFAULT} gelecek."); continue; }
            if (w != aktif) { Temizle(w, p); SilahUygula(w); continue; }
            var baska = hepsi.FirstOrDefault(x => x != w);
            if (baska == null)
            {
                Temizle(w, p); SilahUygula(w);   // tip değişmediği için bıçak da yerinde güncellenebilir
                continue;
            }
            var geri = bicakMi ? "slot3" : "use " + SinifAdi(w);
            p.ExecuteClientCommand("use " + SinifAdi(baska));
            AddTimer(0.15f, () =>
            {
                if (!p.IsValid || !p.PawnIsAlive || !w.IsValid) return;
                Temizle(w, p);
                SilahUygula(w);
                AddTimer(0.1f, () => { if (p.IsValid && p.PawnIsAlive) p.ExecuteClientCommand(geri); });
            });
        }
    }

    // Menüden seçim E tuşuyla yapılıyor ve o karede menü kapanıp karakter çözülüyor; silah aynı karede değiştirilince karakter kilitli
    // kalıp bıçak görünmüyordu (sunucu sahibi 2026-11-16, siteden seçince sorun yoktu). Menü kapandıktan biraz sonra yenilenir.
    private void MenudenSonraYenile(CCSPlayerController p, Func<CBasePlayerWeapon, bool> hangisi) =>
        AddTimer(0.3f, () => { if (Gercek(p)) SilahYenile(p, hangisi); });   // sadece değişen silah

    // Eski skinin izleri silinir (skin kaldırma ve StatTrak kapatma da yerinde çalışsın), yeni eşya numarası istemciyi yeniler
    private void Temizle(CBasePlayerWeapon w, CCSPlayerController p)
    {
        var item = w.AttributeManager.Item;
        item.NetworkedDynamicAttributes.Attributes.RemoveAll();
        item.AttributeList.Attributes.RemoveAll();
        w.FallbackPaintKit = 0;
        w.FallbackStatTrak = -1;
        item.CustomName = "";
        if (!Metin.BicakMi(w.DesignerName)) item.EntityQuality = 0;
        YeniNumara(item, p.SteamID);
    }

    private bool BicakTipiDegisiyor(CCSPlayerController p, CBasePlayerWeapon w)
    {
        int simdiki = w.AttributeManager.Item.ItemDefinitionIndex;
        if (!_secim.TryGetValue(p.SteamID, out var s)) return false;
        int hedef = s.Bicak.TryGetValue(p.TeamNum, out var ad) && _veri.BicakAd.TryGetValue(ad, out var b) ? b.Defindex : (p.TeamNum == 2 ? 59 : 42);
        return simdiki != hedef;
    }

    // StatTrak sayacı (sunucu sahibi 2026-11-16: "!st açılıyor ama sayı artmıyor"). Rakip oyuncuyu, kendi skinli ve StatTrak'ı açık
    // silahıyla öldürünce sayı bir artar; seçim iki takıma kayıtlı olduğu için iki kayıt birlikte artar, sayı hep aynı görünür.
    private void StatTrakSay(EventPlayerDeath e)
    {
        var p = e.Attacker;
        var olen = e.Userid;
        // FFA sunucusunda (mp_teammates_are_enemies 1) aynı takımdan biri de rakiptir
        if (!Gercek(p) || olen == null || !olen.IsValid || olen == p || (olen.TeamNum == p!.TeamNum && !TakimArkadasiDusman())) return;
        if (!_secim.TryGetValue(p.SteamID, out var s)) return;
        var w = p.PlayerPawn.Value?.WeaponServices?.ActiveWeapon.Value;
        if (w == null || !w.IsValid || SilahSahibi(w)?.Slot != p.Slot) return;
        var def = w.AttributeManager.Item.ItemDefinitionIndex;
        if (!s.Skinler.TryGetValue((p.TeamNum, def), out var k) || !k.StatTrak || k.Paint <= 0) return;

        var sayi = k.StatTrakSayi + 1;
        var sid = p.SteamID;
        foreach (var t in Takimlar)
            if (s.Skinler.TryGetValue((t, def), out var x) && x.StatTrak)
            {
                x.StatTrakSayi = sayi;
                Arka("stattrak sayısı", () => _db.StatTrakYaz(sid, t, def, sayi));
            }
        w.FallbackStatTrak = sayi;
        Ozellik(w.AttributeManager.Item, "kill eater", Bit(sayi));
    }

    private static bool TakimArkadasiDusman() => ConVar.Find("mp_teammates_are_enemies")?.GetPrimitiveValue<bool>() == true;

    // M4A1-S, USP-S vb. tasarım adı başka sınıf adı taşıyabildiği için tanımdan alınır
    private string SinifAdi(CBasePlayerWeapon w) =>
        _veri.Defindex.TryGetValue(w.AttributeManager.Item.ItemDefinitionIndex, out var e) ? e.Ad : w.DesignerName;
}
