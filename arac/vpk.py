# VPK (Valve paket) okuyucu, kendi yazdığımız: CS2'nin pak01_dir.vpk dosyasından tek tek dosya çıkarır.
# Kullanım: python vpk.py <pak01_dir.vpk> <paketteki/yol.txt> <çıktı dosyası>
import os, struct, sys

def dizin(yol):
    """Paketteki her dosya için (yol -> (arşiv no, ön veri, başlangıç, uzunluk)) döner."""
    f = open(yol, "rb")
    imza, surum, agac = struct.unpack("<III", f.read(12))
    if imza != 0x55AA1234: raise ValueError("VPK değil")
    bas = 12 + (16 if surum == 2 else 0)
    f.seek(bas)
    def yazi():
        b = bytearray()
        while (c := f.read(1)) != b"\0": b += c
        return b.decode("utf-8", "replace")
    d = {}
    while (uz := yazi()):
        while (kl := yazi()):
            while (ad := yazi()):
                crc, on, arsiv, off, ln, son = struct.unpack("<IHHIIH", f.read(18))
                d[f"{kl}/{ad}.{uz}".lstrip(" /")] = (arsiv, f.read(on), off, ln)
    return d, bas + agac

def oku(vpk_yol, ic_yol):
    d, veri_bas = dizin(vpk_yol)
    arsiv, on, off, ln = d[ic_yol]
    if arsiv == 0x7FFF:   # veri dizin dosyasının içinde, ağaçtan sonra
        f = open(vpk_yol, "rb"); f.seek(veri_bas + off)
    else:
        f = open(vpk_yol.replace("_dir.vpk", f"_{arsiv:03d}.vpk"), "rb"); f.seek(off)
    return on + f.read(ln)

if __name__ == "__main__":
    open(sys.argv[3], "wb").write(oku(sys.argv[1], sys.argv[2]))
    print(sys.argv[2], "->", sys.argv[3], os.path.getsize(sys.argv[3]), "bayt")
