# FACEIT seviye rozetleri (2026-11-16, kendi çizimimiz): koyu parlak bir rozet, seviyeye göre renkli açık halka ve büyük numara.
# Çıktı: cikti/seviye<N>.png (512x384, oyunun pin resmi boyutu) ve cikti/seviye<N>_kucuk.png (64x48, skor tablosu).
# Kullanım: python ciz.py [seviye ...]   (verilmezse 1-10)
import math, os, sys
from PIL import Image, ImageDraw, ImageFilter, ImageFont

KOK = os.path.dirname(os.path.abspath(__file__))
CIKTI = os.path.join(KOK, "cikti")
# Seviye renkleri (FACEIT'teki sıralamaya benzer: gri, yeşil, sarı, turuncu, kırmızı)
RENK = {1: "#eeeeee", 2: "#1ce400", 3: "#1ce400", 4: "#ffc800", 5: "#ffc800", 6: "#ffc800", 7: "#ffc800", 8: "#ff6309", 9: "#ff6309", 10: "#fe1f00"}
YAZI = "C:/Windows/Fonts/bahnschrift.ttf"
K = 4   # kenarlar yumuşak olsun diye 4 kat büyük çizilip küçültülür


def ciz(seviye):
    W, H = 512 * K, 384 * K
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    cx, cy, r = W // 2, H // 2, 168 * K
    # gölge
    golge = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ImageDraw.Draw(golge).ellipse((cx - r, cy - r + 14 * K, cx + r, cy + r + 14 * K), fill=(0, 0, 0, 150))
    img.alpha_composite(golge.filter(ImageFilter.GaussianBlur(14 * K)))
    d = ImageDraw.Draw(img)
    # rozet gövdesi: dış metal kenar + koyu iç
    d.ellipse((cx - r, cy - r, cx + r, cy + r), fill="#3a3d42")
    d.ellipse((cx - r + 8 * K, cy - r + 8 * K, cx + r - 8 * K, cy + r - 8 * K), fill="#1b1d21")
    # seviye halkası: altta açık (225°'den 315°'ye boşluk)
    renk = RENK[seviye]
    ri, kal = r - 34 * K, 30 * K
    d.arc((cx - ri, cy - ri, cx + ri, cy + ri), start=135, end=405, fill=renk, width=kal)
    for a in (135, 405):   # halka uçları yuvarlak
        x, y = cx + (ri - kal / 2) * math.cos(math.radians(a)), cy + (ri - kal / 2) * math.sin(math.radians(a))
        d.ellipse((x - kal / 2, y - kal / 2, x + kal / 2, y + kal / 2), fill=renk)
    # numara
    font = ImageFont.truetype(YAZI, 150 * K if seviye < 10 else 128 * K)
    try: font.set_variation_by_name("Bold")
    except Exception: pass
    yazi = str(seviye)
    kutu = d.textbbox((0, 0), yazi, font=font)
    d.text((cx - (kutu[0] + kutu[2]) / 2, cy - (kutu[1] + kutu[3]) / 2 + 4 * K), yazi, font=font, fill=renk)
    # üstte cam parıltısı
    parilti = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ImageDraw.Draw(parilti).ellipse((cx - r * 0.82, cy - r * 0.95, cx + r * 0.82, cy - r * 0.05), fill=(255, 255, 255, 38))
    maske = Image.new("L", (W, H), 0)
    ImageDraw.Draw(maske).ellipse((cx - r + 8 * K, cy - r + 8 * K, cx + r - 8 * K, cy + r - 8 * K), fill=255)
    img.alpha_composite(Image.composite(parilti, Image.new("RGBA", (W, H)), maske))
    buyuk = img.resize((512, 384), Image.LANCZOS)
    return buyuk, buyuk.resize((64, 48), Image.LANCZOS)


if __name__ == "__main__":
    os.makedirs(CIKTI, exist_ok=True)
    for s in [int(x) for x in sys.argv[1:]] or range(1, 11):
        b, k = ciz(s)
        b.save(os.path.join(CIKTI, f"seviye{s}.png"), optimize=True)
        k.save(os.path.join(CIKTI, f"seviye{s}_kucuk.png"))
        print("seviye", s)
