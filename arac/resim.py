# CS2 doku dosyasını (.vtex_c) PNG'ye çeviren kendi aracımız (2026-11-16, site skin paneli için).
# Biçim (dosyayı inceleyerek çıkardık): kaynak başlığı + blok tablosu; "DATA" bloğunda doku başlığı (genişlik, yükseklik,
# biçim, mip sayısı) ve ek veriler. Ek veri 4 = sıkıştırılmış mip boyutları. Piksel verisi başlıktan sonra, küçük mipten
# büyüğe sıralı, her mip ayrı LZ4 bloğu. Skin resimleri biçim 28 = BGRA8888 (piksel başına 4 bayt); biçim 2 = DXT5 de çözülür.
# Kullanım: python resim.py <dosya.vtex_c> <çıktı.png> [genişlik]   (genişlik verilirse küçültülür)
import struct, sys, zlib


def lz4_ac(veri, boyut):
    """LZ4 blok çözücü: [jeton][değişmez uzunluk ek][değişmezler][2 bayt geri uzaklık][eşleşme uzunluk ek] tekrarı."""
    cikti = bytearray()
    i = 0
    while i < len(veri):
        jeton = veri[i]; i += 1
        n = jeton >> 4
        if n == 15:
            while True:
                b = veri[i]; i += 1; n += b
                if b != 255: break
        cikti += veri[i:i + n]; i += n
        if i >= len(veri): break
        uzak = veri[i] | veri[i + 1] << 8; i += 2
        m = jeton & 15
        if m == 15:
            while True:
                b = veri[i]; i += 1; m += b
                if b != 255: break
        m += 4
        bas = len(cikti) - uzak
        if uzak >= m: cikti += cikti[bas:bas + m]
        else:
            for k in range(m): cikti.append(cikti[bas + k])
    if len(cikti) != boyut: raise ValueError(f"LZ4: {len(cikti)} != {boyut}")
    return bytes(cikti)


def renk565(c):
    r, g, b = c >> 11 & 31, c >> 5 & 63, c & 31
    return (r << 3 | r >> 2, g << 2 | g >> 4, b << 3 | b >> 2)


def dxt5_ac(veri, w, h):
    """DXT5: blok başına 8 bayt alfa (2 uç + 16x3 bit sıra), 8 bayt renk (2 uç 565 + 16x2 bit sıra). Çıktı RGBA."""
    px = bytearray(w * h * 4)
    bw, bh = (w + 3) // 4, (h + 3) // 4
    for by in range(bh):
        for bx in range(bw):
            o = (by * bw + bx) * 16
            a0, a1 = veri[o], veri[o + 1]
            ai = int.from_bytes(veri[o + 2:o + 8], "little")
            if a0 > a1: alfa = [a0, a1] + [((7 - k) * a0 + k * a1) // 7 for k in range(1, 7)]
            else: alfa = [a0, a1] + [((5 - k) * a0 + k * a1) // 5 for k in range(1, 5)] + [0, 255]
            c0, c1, ci = struct.unpack_from("<HHI", veri, o + 8)
            r0, r1 = renk565(c0), renk565(c1)
            renk = [r0, r1, tuple((2 * x + y) // 3 for x, y in zip(r0, r1)), tuple((x + 2 * y) // 3 for x, y in zip(r0, r1))]
            for p in range(16):
                x, y = bx * 4 + (p & 3), by * 4 + (p >> 2)
                if x >= w or y >= h: continue
                r, g, b = renk[ci >> (2 * p) & 3]
                k = (y * w + x) * 4
                px[k:k + 4] = bytes((r, g, b, alfa[ai >> (3 * p) & 7]))
    return px


def oku(dosya_baytlari, en_fazla=0):
    """(genişlik, yükseklik, RGBA baytları). en_fazla verilirse genişliği ondan büyük olmayan ilk mip (küçültmeye gerek kalmaz)."""
    b = dosya_baytlari
    bas, = struct.unpack_from("<I", b, 0)
    boff, bsay = struct.unpack_from("<II", b, 8)
    d = None
    for i in range(bsay):
        p = 8 + boff + i * 12
        if b[p:p + 4] == b"DATA":
            off, _ = struct.unpack_from("<II", b, p + 4); d = p + 4 + off
    if d is None: raise ValueError("DATA bloğu yok")
    w, h, _, bicim, mip = struct.unpack_from("<HHHBB", b, d + 20)
    eoff, esay = struct.unpack_from("<II", b, d + 32)
    e = d + 32 + eoff
    boyutlar = None
    for j in range(esay):
        tip, o, s = struct.unpack_from("<III", b, e + j * 12)
        if tip == 4:
            ed = e + j * 12 + 4 + o
            sikisik, aoff, n = struct.unpack_from("<III", b, ed)
            boyutlar = list(struct.unpack_from(f"<{n}I", b, ed + 4 + aoff)) if sikisik else None
    if bicim not in (2, 28): raise ValueError(f"desteklenmeyen biçim {bicim}")
    if bicim == 28: ham = lambda mw, mh: mw * mh * 4
    else: ham = lambda mw, mh: max(1, (mw + 3) // 4) * max(1, (mh + 3) // 4) * 16
    sec = 0
    while en_fazla and sec < mip - 1 and (w >> sec) > en_fazla: sec += 1
    # Mipler küçükten büyüğe; en büyük (0) en sonda
    konum = bas
    for m in range(mip - 1, sec, -1):
        konum += boyutlar[m] if boyutlar else ham(max(1, w >> m), max(1, h >> m))
    w, h = max(1, w >> sec), max(1, h >> sec)
    uz = boyutlar[sec] if boyutlar else ham(w, h)
    veri = b[konum:konum + uz]
    if boyutlar: veri = lz4_ac(veri, ham(w, h))
    if bicim == 2: return w, h, dxt5_ac(veri, w, h)
    px = bytearray(veri)
    px[0::4], px[2::4] = veri[2::4], veri[0::4]   # BGRA -> RGBA
    return w, h, px


IMZA = bytes([0x89]) + b"PNG"   # PNG dosya imzası


def gomulu_png(b, en_fazla=256):
    """Biçim 16 (PNG): dosyanın içinde her mip hazır bir PNG. Genişliği en_fazla'yı geçmeyen en büyüğü, yoksa en küçüğü döner."""
    bas, = struct.unpack_from("<I", b, 0)
    pngler, i = [], b.find(IMZA, bas)
    while i >= 0:
        son = b.find(b"IEND", i)
        if son < 0: break
        w, = struct.unpack_from(">I", b, i + 16)
        pngler.append((w, b[i:son + 8]))
        i = b.find(IMZA, son)
    if not pngler: return None
    uygun = [p for p in pngler if p[0] <= en_fazla]
    return max(uygun)[1] if uygun else min(pngler)[1]


def kucult(w, h, px, yeni_w):
    """Kutu ortalamasıyla küçültme (tam kat oranlarında kenar yumuşak kalır)."""
    if yeni_w >= w: return w, h, px
    k = w / yeni_w
    yeni_h = max(1, round(h / k))
    cikti = bytearray(yeni_w * yeni_h * 4)
    for y in range(yeni_h):
        y0, y1 = int(y * k), max(int(y * k) + 1, int((y + 1) * k))
        for x in range(yeni_w):
            x0, x1 = int(x * k), max(int(x * k) + 1, int((x + 1) * k))
            t = [0, 0, 0, 0]; n = 0
            for yy in range(y0, min(y1, h)):
                s = (yy * w + x0) * 4
                for xx in range(x1 - x0):
                    a = px[s + xx * 4 + 3]
                    t[0] += px[s + xx * 4] * a; t[1] += px[s + xx * 4 + 1] * a; t[2] += px[s + xx * 4 + 2] * a; t[3] += a; n += 1
            o = (y * yeni_w + x) * 4
            if t[3]: cikti[o:o + 4] = bytes((t[0] // t[3], t[1] // t[3], t[2] // t[3], t[3] // n))
    return yeni_w, yeni_h, cikti


def png(w, h, px):
    def parca(tur, veri): return struct.pack(">I", len(veri)) + tur + veri + struct.pack(">I", zlib.crc32(tur + veri))
    satirlar = b"".join(b"\0" + bytes(px[y * w * 4:(y + 1) * w * 4]) for y in range(h))
    return b"\x89PNG\r\n\x1a\n" + parca(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0)) + parca(b"IDAT", zlib.compress(satirlar, 9)) + parca(b"IEND", b"")


if __name__ == "__main__":
    w, h, px = oku(open(sys.argv[1], "rb").read(), int(sys.argv[3]) if len(sys.argv) > 3 else 0)
    if len(sys.argv) > 3: w, h, px = kucult(w, h, px, int(sys.argv[3]))
    open(sys.argv[2], "wb").write(png(w, h, px))
    print(sys.argv[2], w, h)
