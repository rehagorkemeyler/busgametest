"""
Karlı palet dokusu: haritanın ortak paletinin (Materials/T_AnkaraPalet.png, tools/blender/apartman_kit.py PALETTE sırası,
16x16 hücre, hücre 16 px) karla kaplı kopyası. Çim, toprak, kaldırım, bordür, düz çatı ve kiremit kar beyazı; ağaç yaprakları
karlı; asfalt ıslak ve biraz açık (sulu kar). Duvarlar, camlar, araçlar, tabelalar değişmez.
Çıktı: Resources/Hava/T_AnkaraPalet_Karli.png (HavaZamanKurucu M_AnkaraPalet_Karli ve _GeceKarli'yi bundan kurar).

Kullanım: python tools/hava/karli_palet.py
"""
import ast
import os
import re

from PIL import Image

KOK = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
KIT = os.path.join(KOK, "tools/blender/apartman_kit.py")
GIRDI = os.path.join(KOK, "AnkaraBusSimulator/Assets/_Project/Materials/T_AnkaraPalet.png")
CIKTI = os.path.join(KOK, "AnkaraBusSimulator/Assets/_Project/Resources/Hava/T_AnkaraPalet_Karli.png")
GRID, HUCRE = 16, 16

KAR = (0.92, 0.93, 0.96)
KARLI = {
    "cim": KAR, "toprak": (0.86, 0.87, 0.9), "kaldirim": (0.88, 0.89, 0.92), "bordur": (0.9, 0.91, 0.93),
    "catı_beton": KAR, "kiremit": (0.86, 0.84, 0.85), "beton": (0.8, 0.81, 0.83),
    "yaprak": (0.62, 0.68, 0.64), "yaprak_acik": (0.74, 0.78, 0.74),
    "asfalt": (0.2, 0.2, 0.22), "asfalt_acik": (0.29, 0.29, 0.31),
}


def palet_sirasi():
    kaynak = open(KIT, encoding="utf8").read()
    govde = re.search(r"PALETTE = (\{.*?\n\})", kaynak, re.S).group(1)
    govde = re.sub(r"#[^\n]*", "", govde)
    return list(ast.literal_eval(govde).keys())


def main():
    sira = palet_sirasi()
    img = Image.open(GIRDI).convert("RGB")
    boy = GRID * HUCRE
    assert img.size == (boy, boy), img.size
    for ad, renk in KARLI.items():
        i = sira.index(ad)
        col, row = i % GRID, i // GRID
        x0, y0 = col * HUCRE, row * HUCRE   # PNG'de satır 0 üstte (Blender'da y0 = boy - (row + 1) * HUCRE alttan)
        img.paste(tuple(int(c * 255 + 0.5) for c in renk), (x0, y0, x0 + HUCRE, y0 + HUCRE))
    os.makedirs(os.path.dirname(CIKTI), exist_ok=True)
    img.save(CIKTI)
    print("yazıldı:", CIKTI)


if __name__ == "__main__":
    main()
