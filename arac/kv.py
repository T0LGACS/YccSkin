# Valve KeyValues (items_game.txt, csgo_turkish.txt) okuyucu, kendi yazdığımız.
# Aynı anahtar birden çok kez geçebildiği için her blok (anahtar, değer) listesi olarak döner; Blok.al() birleştirir.
import re

_JETON = re.compile(r'"((?:[^"\\]|\\.)*)"|([{}])|//[^\n]*|([^\s"{}]+)')


class Blok(list):
    def al(self, anahtar, varsayilan=None):
        for k, v in self:
            if k.lower() == anahtar.lower(): return v
        return varsayilan

    def hepsi(self, anahtar):
        return [v for k, v in self if k.lower() == anahtar.lower()]

    def birlesik(self, anahtar):
        """Aynı adlı tüm alt blokları tek blokta toplar (items_game'de 'items' gibi bölümler tekrar edebiliyor)."""
        b = Blok()
        for v in self.hepsi(anahtar):
            if isinstance(v, Blok): b.extend(v)
        return b

    def sozluk(self):
        return {k: v for k, v in self}


def oku(metin):
    jetonlar = []
    for m in _JETON.finditer(metin):
        if m.group(1) is not None: jetonlar.append(("y", m.group(1).replace('\\"', '"').replace("\\n", "\n")))
        elif m.group(2): jetonlar.append((m.group(2), None))
        elif m.group(3): jetonlar.append(("y", m.group(3)))
    i = 0

    def blok():
        nonlocal i
        b = Blok()
        while i < len(jetonlar):
            tur, deger = jetonlar[i]
            if tur == "}": i += 1; return b
            if tur != "y": i += 1; continue
            anahtar = deger; i += 1
            if i >= len(jetonlar): break
            tur, deger = jetonlar[i]
            if tur == "{": i += 1; b.append((anahtar, blok()))
            elif tur == "y":
                i += 1
                # [$WIN32] gibi koşul etiketleri değerden sonra gelebilir; atlanır
                if i < len(jetonlar) and jetonlar[i][0] == "y" and jetonlar[i][1].startswith("[$"): i += 1
                b.append((anahtar, deger))
        return b

    return blok()


def dosya(yol):
    return oku(open(yol, encoding="utf-8-sig", errors="replace").read())
