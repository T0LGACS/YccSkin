# YccSkin veri üretici (kendi yazdığımız): CS2'nin kendi dosyalarından skin listesini çıkarır.
# Girdi: items_game.txt + csgo_turkish.txt + csgo_english.txt (vpk.py ile pak01_dir.vpk'den çıkarılır)
# Çıktı: skinler.json (YccSkin eklentisinin data klasörüne). CS2'ye yeni skin gelince yeniden çalıştırılır.
# Kullanım: python veri_uret.py <klasör (3 dosya içinde)> <çıktı.json> <pak01_dir.vpk>
import json, re, sys
sys.path.insert(0, __file__.rsplit("\\", 1)[0].rsplit("/", 1)[0])
import kv

klasor, cikti = sys.argv[1], sys.argv[2]
ig = kv.dosya(klasor + "/items_game.txt").al("items_game")

def ceviri(dil):
    t = kv.dosya(f"{klasor}/csgo_{dil}.txt").al("lang").al("Tokens")
    return {k.lower(): v for k, v in t}
tr, en = ceviri("turkish"), ceviri("english")

def isim(jeton, yedek=""):
    j = (jeton or "").lstrip("#").lower()
    return tr.get(j) or en.get(j) or yedek

prefabs = ig.birlesik("prefabs").sozluk()
items = [(int(k), v) for k, v in ig.birlesik("items") if k.isdigit() and isinstance(v, kv.Blok)]

def prefab_zinciri(v):
    """Eşyanın prefab'ı ve onun prefab'ları (ilk bulunan alan için)."""
    yol, p = [v], v.al("prefab", "")
    for _ in range(10):
        for ad in p.split():
            if ad in prefabs: yol.append(prefabs[ad])
        p = prefabs[p.split()[0]].al("prefab", "") if p and p.split()[0] in prefabs else ""
        if not p: break
    return yol

def alan(v, ad):
    for b in prefab_zinciri(v):
        x = b.al(ad)
        if x is not None: return x
    return None

# Hangi skin hangi eşyaya çıkıyor: kasa/koleksiyon listelerindeki "[skin_adı]eşya_adı" girdileri
eslesme = {}
for bolum in ("client_loot_lists", "item_sets", "revolving_loot_lists"):
    for _, liste in ig.hepsi(bolum) and [(k, v) for b in ig.hepsi(bolum) for k, v in b]:
        kaynak = liste if isinstance(liste, kv.Blok) else []
        for k, v in kaynak:
            for metin in (k, v if isinstance(v, str) else ""):
                m = re.match(r"^\[([^\]]+)\](\S+)$", metin)
                if m: eslesme.setdefault(m.group(2), set()).add(m.group(1))
    for b in ig.hepsi(bolum):
        for k, v in b:
            for alt in ([k] + ([x for x, _ in v] if isinstance(v, kv.Blok) else [])):
                m = re.match(r"^\[([^\]]+)\](\S+)$", alt)
                if m: eslesme.setdefault(m.group(2), set()).add(m.group(1))

# Bıçak ve eldiven skinleri kasa listelerinde yok. Oyun her eşya+skin için hazır resim tutuyor:
# panorama/images/econ/default_generated/<eşya>_<skin>_light_png.vtex_c -> bu adlardan eşleşme çıkarılır
import vpk
esya_adlari = sorted({v.al("name", "") for _, v in items if v.al("name")}, key=len, reverse=True)
for yol in vpk.dizin(sys.argv[3])[0]:
    m = re.match(r"^panorama/images/econ/default_generated/(.+)_light_png\.vtex_c$", yol)
    if not m: continue
    for e in esya_adlari:
        if m.group(1).startswith(e + "_"):
            eslesme.setdefault(e, set()).add(m.group(1)[len(e) + 1:]); break

kitler = {}
for k, v in ig.birlesik("paint_kits"):
    if not k.isdigit() or not isinstance(v, kv.Blok) or int(k) == 0: continue
    kitler[v.al("name", "")] = {"id": int(k), "isim": isim(v.al("description_tag"), v.al("name", "")),
                                "aslinda": v.al("name", ""), "wear_min": float(v.al("wear_remap_min", "0") or 0),
                                "wear_max": float(v.al("wear_remap_max", "1") or 1),
                                "eski": v.al("use_legacy_model", "0") == "1"}

EK = [(r"phase(\d)", "Faz {}"), (r"ruby", "Yakut"), (r"sapphire", "Safir"), (r"blackpearl", "Kara İnci"), (r"emerald", "Zümrüt")]

def ayirt(kit):
    """Doppler gibi aynı adlı skinlerin farkı (iç addan): Faz 1, Yakut, Safir..."""
    for desen, yazi in EK:
        m = re.search(desen, kit["aslinda"])
        if m: return yazi.format(*m.groups())
    return str(kit["id"])

def skinler(esya_adi):
    l = [kitler[s] for s in eslesme.get(esya_adi, ()) if s in kitler]
    sayi = {}
    for k in l: sayi[k["isim"]] = sayi.get(k["isim"], 0) + 1
    # "eski": skin silahın eski (CS:GO) modelini kullanıyor; eklenti modelin gövde grubunu buna göre seçer
    return sorted(({"id": k["id"], "isim": k["isim"] if sayi[k["isim"]] == 1 else f'{k["isim"]} ({ayirt(k)})', **({"eski": True} if k["eski"] else {})}
                   for k in l), key=lambda x: x["isim"])

silahlar, bicaklar, eldivenler, ajanlar, rozetler = [], [], [], [], []
# YccRozet: kullanılmayan 2016/2017 hizmet madalyaları FACEIT seviye 1-10 rozeti olarak kullanılıyor (YccSkin FaceitRozetleri ile aynı sıra)
FACEIT_ROZET = {d: i + 1 for i, d in enumerate([1339, 1340, 1341, 1342, 1343, 1344, 1357, 1358, 1359, 1360])}
dogal = lambda x: [int(t) if t.isdigit() else t for t in re.split(r"(\d+)", x["isim"])]   # "Seviye 10", "Seviye 2"den sonra gelsin
for di, v in items:
    ad = v.al("name", "")
    pre = v.al("prefab", "")
    gorunen = isim(alan(v, "item_name"), ad)
    if ad.startswith("weapon_") and pre.endswith("_prefab") and pre.startswith("weapon_") and "knife" not in ad and di < 100:
        silahlar.append({"defindex": di, "ad": ad, "isim": gorunen, "skinler": skinler(ad)})
    elif pre == "melee_unusual" or ad in ("weapon_bayonet",) or (ad.startswith("weapon_knife_") and pre.startswith("melee")):
        if skinler(ad): bicaklar.append({"defindex": di, "ad": ad, "isim": gorunen, "skinler": skinler(ad)})   # varsayılan bıçaklar seçilmez
    elif pre == "hands_paintable":
        eldivenler.append({"defindex": di, "ad": ad, "isim": gorunen, "skinler": skinler(ad)})
    elif pre == "customplayertradable":
        model = alan(v, "model_player") or ""
        sinif = alan(v, "used_by_classes")
        takim = "ct" if isinstance(sinif, kv.Blok) and sinif.al("counter-terrorists") else "t"
        m = re.match(r"(?:characters|agents)/models/(.+)\.vmdl", model)
        if m: ajanlar.append({"defindex": di, "model": m.group(1), "yol": model, "takim": takim, "isim": gorunen})
    elif pre in ("commodity_pin", "attendance_pin", "collectible_untradable_coin", "prestige_coin") or re.match(r"season\d+_coin", pre):
        # Adı ve resmi olmayan kullanılmayan kayıtlar ("item 6046") alınmaz. Aynı rozetin "Genuine" kopyası (örn. 6006/6106) aynı ad ve
        # resimle tek kart olur; diğer numara "esler"e yazılır ki onu seçmiş oyuncunun seçimi tanınsın (sunucu sahibi 2026-11-16)
        resim_yolu = alan(v, "image_inventory")
        if not alan(v, "item_name") or not resim_yolu: continue
        if di in FACEIT_ROZET: gorunen = f"FACEIT Seviye {FACEIT_ROZET[di]}"   # resmi YccRozet Workshop eklentisiyle seviye ikonuna çevrili
        es = next((r for r in rozetler if r["isim"] == gorunen and r["_resim"] == resim_yolu), None)
        if es: es.setdefault("esler", []).append(di)
        else: rozetler.append({"defindex": di, "isim": gorunen, "_resim": resim_yolu})

muzikler = []
for k, v in ig.birlesik("music_definitions"):
    if k.isdigit() and isinstance(v, kv.Blok) and int(k) > 0:
        muzikler.append({"id": int(k), "isim": isim(v.al("loc_name"), v.al("name", ""))})

veri = {"silahlar": sorted(silahlar, key=lambda x: x["isim"]), "bicaklar": sorted(bicaklar, key=lambda x: x["isim"]),
        "eldivenler": sorted(eldivenler, key=lambda x: x["isim"]), "ajanlar": sorted(ajanlar, key=lambda x: (x["takim"], x["isim"])),
        "muzikler": sorted(muzikler, key=lambda x: x["isim"]), "rozetler": sorted(({k: v for k, v in r.items() if k != "_resim"} for r in rozetler), key=dogal)}
json.dump(veri, open(cikti, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print({k: len(v) for k, v in veri.items()}, "| skinli silah:", sum(1 for s in silahlar if s["skinler"]),
      "| toplam silah skini:", sum(len(s["skinler"]) for s in silahlar), "| bıçak skini:", sum(len(s["skinler"]) for s in bicaklar),
      "| eldiven skini:", sum(len(s["skinler"]) for s in eldivenler))
