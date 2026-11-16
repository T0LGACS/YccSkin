# CS2 güncellemesinden sonra YccSkin'i kendiliğinden onarır (2026-11-16). /home/cs2/cs2-guncelle.sh oyunu güncelledikten
# sonra, sunucu kapalıyken çalıştırır. Elle: python3 /root/skin-arac/skin_kontrol.py [--dene]  (--dene: hiçbir şey yazmaz)
# 1) YccSkin.json'daki imza yeni libserver.so'da tek yere uyuyor mu? Uymuyorsa imza_bul.py ile yenisini bulup yazar.
# 2) Skin listesini oyun dosyalarından yeniden üretir; değiştiyse data/skinler.json'u yeniler.
# 3) Sitedeki skin panelinin resimlerini ve kataloğunu günceller (yeni skinler sitede de görünsün).
# Değişiklik ya da sorun olursa YccTemel'in Discord webhook'una yazar. Her yazmadan önce .yedek-<tarih> alır.
import json, os, re, shutil, subprocess, sys, tempfile, time, urllib.request

ARAC = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, ARAC)
import vpk

OYUN = "/home/cs2/cs2/game/csgo"
LIB = OYUN + "/bin/linuxsteamrt64/libserver.so"
PAK = OYUN + "/pak01_dir.vpk"
CSS = OYUN + "/addons/counterstrikesharp"
AYAR = CSS + "/configs/plugins/YccSkin/YccSkin.json"
VERI = CSS + "/plugins/YccSkin/data/skinler.json"
TEMEL = CSS + "/configs/plugins/YccTemel/YccTemel.json"
DOSYALAR = ["scripts/items/items_game.txt", "resource/csgo_turkish.txt", "resource/csgo_english.txt"]
DENE = "--dene" in sys.argv
TARIH = time.strftime("%Y%m%d")


def discord(mesaj):
    print("discord:", mesaj)
    if DENE: return
    try:
        url = json.load(open(TEMEL, encoding="utf-8"))["Discord"]["Webhook"]
        istek = urllib.request.Request(url, json.dumps({"content": "🔧 **Skin kontrolü:** " + mesaj}).encode(),
                                       {"Content-Type": "application/json", "User-Agent": "YccSkinKontrol"})
        urllib.request.urlopen(istek, timeout=15)
    except Exception as e:
        print("discord gönderilemedi:", e)


def yaz(yol, icerik):
    """Yedek alıp yazar, dosya sahibini (cs2) korur."""
    if DENE: print("[dene] yazılmadı:", yol); return
    st = os.stat(yol)
    shutil.copy2(yol, f"{yol}.yedek-{TARIH}")
    open(yol, "w", encoding="utf-8").write(icerik)
    os.chown(yol, st.st_uid, st.st_gid)


def imza_uyan(imza, d):
    ifade = b"".join(b"." if x == "?" else re.escape(bytes([int(x, 16)])) for x in imza.split())
    return len(re.findall(ifade, d, re.S))


def imza_kontrol():
    ayar = json.load(open(AYAR, encoding="utf-8"))
    eski = ayar["Imza"]
    n = imza_uyan(eski, open(LIB, "rb").read())
    if n == 1: print("imza geçerli"); return
    print(f"imza {n} yere uyuyor, yenisi aranıyor")
    c = subprocess.run([sys.executable, "-I", os.path.join(ARAC, "imza_bul.py"), LIB], capture_output=True, text=True)
    print(c.stdout, c.stderr)
    satirlar = c.stdout.strip().splitlines()   # başarıda son satır imzadır
    if c.returncode != 0 or "UYARI" in c.stdout or not satirlar:
        discord("CS2 güncellemesinden sonra skin imzası bozuldu ve yenisi güvenle bulunamadı. Skinler görünmeyebilir, elle bakılmalı (KILAVUZ.txt, imza_bul.py).")
        return
    ayar["Imza"] = satirlar[-1].strip()
    yaz(AYAR, json.dumps(ayar, ensure_ascii=False, indent=2))
    discord(f"CS2 güncellemesi skin imzasını değiştirdi, yenisi bulunup yazıldı. Oyunda bir skin seçip dene; sorun olursa eski ayar {AYAR}.yedek-{TARIH}")


def veri_kontrol():
    with tempfile.TemporaryDirectory() as k:
        for ic in DOSYALAR:
            open(os.path.join(k, os.path.basename(ic)), "wb").write(vpk.oku(PAK, ic))
        cikti = os.path.join(k, "skinler.json")
        c = subprocess.run([sys.executable, "-I", os.path.join(ARAC, "veri_uret.py"), k, cikti, PAK], capture_output=True, text=True)
        print(c.stdout, c.stderr)
        if c.returncode != 0:
            discord("Skin listesi oyun dosyalarından üretilemedi (dosya biçimi değişmiş olabilir). Eski liste kullanılmaya devam ediyor.")
            return
        yeni, eski = json.load(open(cikti, encoding="utf-8")), json.load(open(VERI, encoding="utf-8"))
    if yeni == eski: print("skin listesi aynı"); return
    say = lambda v: sum(len(s["skinler"]) for b in ("silahlar", "bicaklar", "eldivenler") for s in v[b])
    # Yeni liste belirgin şekilde küçükse üretimde bir şey ters gitmiştir: dokunma
    if say(yeni) < say(eski) * 0.9 or len(yeni["silahlar"]) < len(eski["silahlar"]):
        discord(f"Yeni skin listesi şüpheli derecede küçük ({say(eski)} → {say(yeni)} skin), uygulanmadı. Elle bakılmalı.")
        return
    yaz(VERI, json.dumps(yeni, ensure_ascii=False, indent=1))
    discord(f"Skin listesi oyunun yeni dosyalarından güncellendi: {say(eski)} → {say(yeni)} skin, {len(eski['ajanlar'])} → {len(yeni['ajanlar'])} ajan.")


def gorsel_kontrol():
    """Site skin paneli: yeni skinlerin resimleri ve katalog (var olan resimler yeniden üretilmez)."""
    site = "/var/www/yccteam/skin"
    if DENE or not os.path.isdir(site): return
    with tempfile.TemporaryDirectory() as k:
        open(os.path.join(k, "items_game.txt"), "wb").write(vpk.oku(PAK, DOSYALAR[0]))
        c = subprocess.run([sys.executable, "-I", os.path.join(ARAC, "gorsel_uret.py"), k, PAK, VERI, "/root/skin-gorsel"], capture_output=True, text=True)
    print(c.stdout[-500:], c.stderr[-500:])
    if c.returncode == 0: subprocess.run(["rsync", "-a", "/root/skin-gorsel/", site + "/"], check=True)


for adim in (imza_kontrol, veri_kontrol, gorsel_kontrol):
    try: adim()
    except Exception as e:
        print(adim.__name__, "hata:", e)
        discord(f"{adim.__name__} çalışırken hata: {e}")
