# Skin fonksiyonunun (CAttributeList::SetOrAddAttributeValueByName) adresini libserver.so'da bulur ve imza üretir.
# Kendi yöntemimiz: oyunun kendi kodu bu fonksiyonu "set item texture prefab" gibi öznitelik adlarıyla çağırıyor.
# 1) Bu adların dosyadaki adresini bul, 2) bu adresi "lea reg, [rip+x]" ile yükleyen komutları bul,
# 3) hemen ardından gelen "call" komutlarının hedeflerini say: en çok çağrılan hedef aranan fonksiyon,
# 4) fonksiyonun başındaki baytlardan, adresleri "?" yapılmış ve dosyada TEK yere uyan bir imza üret.
# Kullanım: python imza_bul.py <libserver.so>   (CS2 güncellemesinden sonra tekrar çalıştırılır)
import collections, re, struct, sys

d = open(sys.argv[1], "rb").read()
if d[:4] != b"\x7fELF": sys.exit("ELF değil")

# Program başlıkları: dosya konumu <-> sanal adres
phoff, = struct.unpack_from("<Q", d, 0x20)
phentsize, phnum = struct.unpack_from("<HH", d, 0x36)
yukler = []
for i in range(phnum):
    tip, bayrak, off, vaddr, _, filesz, memsz, _ = struct.unpack_from("<IIQQQQQQ", d, phoff + i * phentsize)
    if tip == 1: yukler.append((off, vaddr, filesz, bayrak))

def adres(off):
    for o, v, n, _ in yukler:
        if o <= off < o + n: return v + off - o
def konum(va):
    for o, v, n, _ in yukler:
        if v <= va < v + n: return o + va - v

kod = [(o, v, n) for o, v, n, b in yukler if b & 1]   # çalıştırılabilir bölüm

ADLAR = [b"set item texture prefab", b"set item texture seed", b"set item texture wear", b"kill eater"]
hedefler = collections.Counter()
for ad in ADLAR:
    for m in re.finditer(re.escape(ad) + b"\x00", d):
        dize = adres(m.start())
        for o, v, n in kod:
            parca = d[o:o + n]
            # lea r64, [rip+disp32]: REX.W(48/4C) 8D modrm(mod=00, rm=101)
            for l in re.finditer(rb"[\x48\x4c]\x8d[\x05\x0d\x15\x1d\x25\x2d\x35\x3d]", parca):
                i = l.start()
                disp, = struct.unpack_from("<i", parca, i + 3)
                if v + i + 7 + disp != dize: continue
                # sonraki 48 bayt içindeki ilk call rel32
                for j in range(i + 7, min(i + 55, n - 5)):
                    if parca[j] == 0xE8:
                        rel, = struct.unpack_from("<i", parca, j + 1)
                        hedefler[v + j + 5 + rel] += 1
                        break

if not hedefler: sys.exit("çağrı bulunamadı")
for h, n in hedefler.most_common(5): print(f"hedef 0x{h:x}: {n} çağrı")
fonk = hedefler.most_common(1)[0][0]
bas = konum(fonk)
# En çok çağrılan hedef bir köprü olabilir (örn. "add rdi, 0x70; jmp asıl"): ilk 16 baytta jmp varsa asıl fonksiyona geç.
for k in range(16):
    if d[bas + k] == 0xE9:
        rel, = struct.unpack_from("<i", d, bas + k + 1)
        print(f"köprü 0x{fonk:x} ({d[bas:bas + k].hex(' ')}) -> asıl fonksiyon")
        fonk = fonk + k + 5 + rel; bas = konum(fonk)
        break
# Değer parametresi float (xmm0): fonksiyonun başında xmm0'ı yığına yazan movss (F3 0F 11) olmalı, yoksa yanlış yer
if bytes.fromhex("f30f11") not in d[bas:bas + 64]: print("UYARI: başta float parametre izi yok, sonucu kontrol et")

# İmza: fonksiyonun ilk baytları; tekil olana kadar uzatılır. rip-göreli adres/çağrı alanları "?" yapılır.
def desen(n):
    b = list(d[bas:bas + n]); maske = [True] * n
    if 0xCC in b[1:]: raise ValueError("fonksiyon sonuna ulaşıldı")
    for k in range(n - 4):
        if b[k] == 0xE8 or b[k] == 0xE9: maske[k + 1:k + 5] = [False] * 4
        if k + 3 < n and b[k:k + 3] == [0x48, 0x83, 0xEC]: maske[k + 3] = False   # sub rsp, N: yığın boyu güncellemede değişebilir
        if k + 2 < n and b[k] in (0x48, 0x4C) and b[k + 1] in (0x8B, 0x8D) and (b[k + 2] & 0xC7) == 0x05: maske[k + 3:k + 7] = [False] * 4
    return b, maske[:n]

def uyanlar(b, maske):
    ifade = b"".join(re.escape(bytes([x])) if m else b"." for x, m in zip(b, maske))
    return [x.start() for x in re.finditer(ifade, d, re.S)]

for n in range(12, 64):
    b, maske = desen(n)
    if len(uyanlar(b, maske)) == 1:
        imza = " ".join(f"{x:02X}" if m else "?" for x, m in zip(b, maske))
        print(f"fonksiyon dosya konumu 0x{bas:x}, imza ({n} bayt, tekil):")
        print(imza)
        break
else:
    sys.exit("tekil imza üretilemedi")
