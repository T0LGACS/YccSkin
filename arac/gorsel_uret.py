# Site skin paneli için resimler ve katalog (2026-11-16, kendi aracımız). Oyun dosyalarından:
#   resim/silah/<def>.png, resim/skin/<def>_<paint>.png, resim/ajan/<def>.png, resim/muzik/<id>.png, resim/rozet/<def>.png
#   katalog.json: skinler.json + nadirlik renkleri + hangi resmin var olduğu
#   cikartma.json + resim/cikartma/<id>.png, resim/anahtarlik/<id>.png: site atölyesindeki çıkartma ve anahtarlık seçimi
# Kullanım: python3 gorsel_uret.py <items_game.txt klasörü> <pak01_dir.vpk> <skinler.json> <çıktı klasörü>
# Var olan resim yeniden üretilmez (CS2 güncellemesinden sonra sadece yeniler eklenir).
import json, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import kv, resim, vpk

klasor, pak, skin_json, cikti = sys.argv[1:5]
ig = kv.dosya(os.path.join(klasor, "items_game.txt")).al("items_game")
veri = json.load(open(skin_json, encoding="utf-8"))
dizin, veri_bas = vpk.dizin(pak)

# vpk.oku her seferinde dizini yeniden okuyor; burada bir kez okunan dizinden çıkarılır
_dosyalar = {}
def ham(ic):
    arsiv, on, off, ln = dizin[ic]
    if arsiv == 0x7FFF:
        f = open(pak, "rb"); f.seek(veri_bas + off)
    else:
        yol = pak.replace("_dir.vpk", f"_{arsiv:03d}.vpk")
        f = _dosyalar.get(yol) or _dosyalar.setdefault(yol, open(yol, "rb")); f.seek(off)
    return on + f.read(ln)

uretilen = atlanan = hatali = 0
def yap(ic, hedef):
    """Oyun içindeki .vtex_c resmini 256 piksellik PNG'ye çevirir; resim yoksa False."""
    global uretilen, atlanan, hatali
    if ic not in dizin: return False
    y = os.path.join(cikti, "resim", hedef)
    if os.path.exists(y): atlanan += 1; return True
    try:
        b = ham(ic)
        try: icerik = resim.png(*resim.oku(b, 256))
        except ValueError as e:
            if "biçim 16" not in str(e): raise
            icerik = resim.gomulu_png(b)   # biçim 16: içinde hazır PNG var
            if icerik is None: raise
        os.makedirs(os.path.dirname(y), exist_ok=True)
        open(y + ".tmp", "wb").write(icerik); os.replace(y + ".tmp", y)
        uretilen += 1; return True
    except Exception as e:
        hatali += 1; print("resim çözülemedi:", ic, e); return False

# Nadirlik: paint_kits_rarity (kit adı -> nadirlik) -> rarities (renk adı) -> colors (hex)
renkler = {k: v.al("hex_color") for k, v in ig.birlesik("colors") if isinstance(v, kv.Blok)}
nadirlik_renk = {k: renkler.get(v.al("color", ""), "#b0c3d9") for k, v in ig.birlesik("rarities") if isinstance(v, kv.Blok)}
nadirlik_ad = {k: v.al("loc_key_weapon", k) for k, v in ig.birlesik("rarities") if isinstance(v, kv.Blok)}
kit_nadirlik = dict(ig.birlesik("paint_kits_rarity"))
kit_ad = {int(k): v.al("name", "") for k, v in ig.birlesik("paint_kits") if k.isdigit() and isinstance(v, kv.Blok)}
items = {int(k): v for k, v in ig.birlesik("items") if k.isdigit() and isinstance(v, kv.Blok)}
prefabs = ig.birlesik("prefabs").sozluk()
ALTIN = "#e4ae39"   # bıçak ve eldivenler (★)

def gorsel_yolu(di):
    """Eşyanın envanter resmi (image_inventory), prefab zincirinde aranır."""
    v = items.get(di)
    for _ in range(6):
        if v is None: return None
        y = v.al("image_inventory")
        if y: return f"panorama/images/{y}_png.vtex_c"
        p = (v.al("prefab", "") or "").split()
        v = prefabs.get(p[0]) if p else None
    return None

def esyalar(liste, ozel=False):
    for e in liste:
        e["resim"] = yap(f"panorama/images/econ/weapons/base_weapons/{e['ad']}_png.vtex_c", f"silah/{e['defindex']}.png") \
            or bool((g := gorsel_yolu(e["defindex"])) and yap(g, f"silah/{e['defindex']}.png"))
        for s in e["skinler"]:
            ad = kit_ad.get(s["id"], "")
            s["resim"] = yap(f"panorama/images/econ/default_generated/{e['ad']}_{ad}_light_png.vtex_c", f"skin/{e['defindex']}_{s['id']}.png")
            n = kit_nadirlik.get(ad, "common")
            s["renk"] = ALTIN if ozel else nadirlik_renk.get(n, "#b0c3d9")
            s["nadirlik"] = 7 if ozel else int(ig.birlesik("rarities").al(n, kv.Blok()).al("value", "1") or 1)
        e["skinler"].sort(key=lambda s: (-s["nadirlik"], s["isim"]))

esyalar(veri["silahlar"]); esyalar(veri["bicaklar"], True); esyalar(veri["eldivenler"], True)
for a in veri["ajanlar"]:
    g = gorsel_yolu(a["defindex"]); a["resim"] = bool(g and yap(g, f"ajan/{a['defindex']}.png"))
    r = (items.get(a["defindex"]) or kv.Blok()).al("item_rarity", "")
    a["renk"] = nadirlik_renk.get(r, "#b0c3d9")
muzik_tanim = {int(k): v for k, v in ig.birlesik("music_definitions") if k.isdigit() and isinstance(v, kv.Blok)}
for m in veri["muzikler"]:
    y = (muzik_tanim.get(m["id"]) or kv.Blok()).al("image_inventory")
    m["resim"] = bool(y and yap(f"panorama/images/{y}_png.vtex_c", f"muzik/{m['id']}.png"))
for r in veri["rozetler"]:
    g = gorsel_yolu(r["defindex"]); r["resim"] = bool(g and yap(g, f"rozet/{r['defindex']}.png"))

# Çıkartmalar ve anahtarlıklar (site atölyesi): ayrı dosyada, sadece çıkartma bölümü açılınca yüklenir. Küçük resim yeter (128 px).
def ceviri(dil):
    t = kv.oku(ham(f"resource/csgo_{dil}.txt").decode("utf-8-sig", "replace")).al("lang").al("Tokens")
    return {k.lower(): v for k, v in t}
tr, en = ceviri("turkish"), ceviri("english")
isim = lambda j, y: tr.get((j or "").lstrip("#").lower()) or en.get((j or "").lstrip("#").lower()) or y
def kucuk(ic, hedef):
    global uretilen, atlanan
    if ic not in dizin: return False
    y = os.path.join(cikti, "resim", hedef)
    if os.path.exists(y): atlanan += 1; return True
    try:
        b = ham(ic)
        try:
            w, h, px = resim.oku(b, 128)
            icerik = resim.png(*resim.kucult(w, h, px, 128))   # bazı çıkartmalarda küçük mip yok
        # shortcut: biçim 16 (hazır PNG) çıkartmalarda küçük mip yok, 512 px kalıyor (~640 MB); yer ya da hız sorun olursa PNG çözüp küçült
        except ValueError: icerik = resim.gomulu_png(b, 128)
        if not icerik: return False
        os.makedirs(os.path.dirname(y), exist_ok=True)
        open(y + ".tmp", "wb").write(icerik); os.replace(y + ".tmp", y); uretilen += 1; return True
    except Exception as e:
        print("çıkartma resmi çözülemedi:", ic, e); return False
cikartmalar = []
for k, v in ig.birlesik("sticker_kits"):
    m = isinstance(v, kv.Blok) and v.al("sticker_material")
    if not k.isdigit() or not m or int(k) == 0: continue
    if kucuk(f"panorama/images/econ/stickers/{m}_png.vtex_c", f"cikartma/{k}.png"):
        cikartmalar.append({"id": int(k), "isim": isim(v.al("item_name"), v.al("name", "")), "renk": nadirlik_renk.get(v.al("item_rarity", "rare"), "#4b69ff")})
anahtarliklar = []
for k, v in ig.birlesik("keychain_definitions"):
    if not k.isdigit() or not isinstance(v, kv.Blok) or not v.al("image_inventory"): continue
    if kucuk(f"panorama/images/{v.al('image_inventory')}_png.vtex_c", f"anahtarlik/{k}.png"):
        anahtarliklar.append({"id": int(k), "isim": isim(v.al("loc_name"), v.al("name", "")), "renk": nadirlik_renk.get(v.al("item_rarity", "rare"), "#4b69ff")})
cikartmalar.sort(key=lambda x: -x["id"]); anahtarliklar.sort(key=lambda x: x["isim"])

os.makedirs(cikti, exist_ok=True)
json.dump(veri, open(os.path.join(cikti, "katalog.json"), "w", encoding="utf-8"), ensure_ascii=False, separators=(",", ":"))
json.dump({"cikartmalar": cikartmalar, "anahtarliklar": anahtarliklar}, open(os.path.join(cikti, "cikartma.json"), "w", encoding="utf-8"), ensure_ascii=False, separators=(",", ":"))
print(f"çıkartma {len(cikartmalar)}, anahtarlık {len(anahtarliklar)}")
eksik = sum(1 for b in ("silahlar", "bicaklar", "eldivenler") for e in veri[b] for s in e["skinler"] if not s["resim"])
print(f"üretilen {uretilen}, zaten vardı {atlanan}, çözülemeyen {hatali}, resmi olmayan skin {eksik}")
