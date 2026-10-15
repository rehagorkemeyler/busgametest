"""
Hat 3 dokuları (PIL, prosedürel; fotoğraf yok):
    asfalt.png, kaldirim.png (gri kilit taşı), refuj.png, bordur.png, zemin.png, cim.png  — 512 px, döşenebilir
    cephe_atlas.png (2048 px, 16×16 hücre) + cephe_atlas.json — bina cepheleri: her hücre bir "bölme" (3 m × kat)
    cati_atlas.png (1024 px, 2×2) — çakıllı düz çatı, beton çatı, kiremit, kurşun (kubbe)

Cephe stilleri (Ankara): bulvar60 (60–70'ler sıvalı bulvar apartmanı), ulus_tas (Ulus kesme taş), mantolama (2000 sonrası
pastel), cam_ofis, kamu (brüt beton/tuğla kamu binası), gecekondu, tugla (açık tuğla apartman), kurum (hastane/okul).
Her stilin renk çeşitleri × {P: pencere, B: balkon, D: dar pencere/boş, U: uzak (4 bölme × 4 kat, LOD1)}. Zemin kat: dükkânlar (Türkçe tabela), giriş kapısı,
kepenk. Cami: taş duvar, minare, kubbe hücreleri.

Kullanım: python -m hat3.dokular --out AnkaraBusSimulator/Assets/_Project/Maps/Hat3/Textures
"""
import argparse
import json
import os
import random

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

FONT = os.environ.get("HARITA_FONT", "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf")  # macOS: matplotlib'in DejaVuSans-Bold.ttf'i
H = 128          # hücre pikseli
N = 16           # atlas 16 × 16

STILLER = {
    # ad: (renkler, pencere çerçevesi, cam, balkon korkuluğu)
    "bulvar60": ([(232, 220, 190), (214, 196, 160), (222, 203, 150), (196, 192, 182)], (70, 70, 72), (70, 88, 100), "demir"),
    "ulus_tas": ([(208, 188, 148), (188, 172, 138)], (60, 50, 40), (55, 65, 70), "tas"),
    "mantolama": ([(232, 172, 142), (240, 222, 156), (192, 212, 172), (236, 234, 228), (182, 202, 216), (230, 196, 170)],
                  (245, 245, 245), (80, 100, 115), "cam"),
    "cam_ofis": ([(60, 95, 110), (70, 100, 90), (110, 95, 75)], (40, 44, 48), None, None),
    "kamu": ([(170, 168, 160), (176, 120, 96)], (50, 50, 50), (60, 72, 80), "beton"),
    "gecekondu": ([(206, 196, 186), (222, 200, 196), (184, 150, 120)], (90, 70, 50), (60, 70, 75), None),
    "tugla": ([(160, 82, 60), (176, 100, 72)], (235, 235, 230), (70, 85, 95), "demir"),
    "kurum": ([(238, 234, 222), (226, 222, 205)], (90, 110, 130), (80, 110, 130), None),
}
DUKKANLAR = ["ECZANE", "BÜFE", "KUAFÖR", "EMLAK", "FIRIN", "LOKANTA", "MARKET", "TERZİ", "KIRTASİYE", "OPTİK",
             "ÇİÇEKÇİ", "BAKKAL", "KASAP", "TATLICI", "KUYUMCU", "BERBER", "NALBUR", "ÇAY OCAĞI", "SÜTÇÜ",
             "MOBİLYA", "TEKSTİL", "AYAKKABI", "ELEKTRİK", "KEBAP"]
TABELA_RENK = [(200, 30, 35), (20, 70, 150), (230, 180, 20), (20, 120, 60), (240, 240, 240), (30, 30, 30),
               (150, 30, 120), (230, 110, 20)]


def gurultu(n, olcek, rng, genlik):
    """Döşenebilir değer gürültüsü (n×n), Gauss yumuşatma (sarmal)."""
    from scipy import ndimage
    a = rng.standard_normal((n, n))
    a = ndimage.gaussian_filter(a, olcek, mode="wrap")
    a = (a - a.mean()) / (a.std() + 1e-9)
    return a * genlik


def kaydet(a, yol):
    Image.fromarray(np.clip(a, 0, 255).astype(np.uint8)).save(yol)


def doku_asfalt(rng, n=512):
    g = gurultu(n, 1.0, rng, 10) + gurultu(n, 6, rng, 6) + gurultu(n, 40, rng, 5)
    taban = np.array([62, 62, 65], float)
    a = taban + g[..., None]
    # ince taş parlamaları
    m = rng.random((n, n)) > 0.985
    a[m] += 30
    return a


def doku_kaldirim(rng, n=512):
    """Gri kilit taşı (Ankara): 2 m'lik karede 10×20 cm taşlar → 512 px'te taş 25×51 px, yarım kaydırmalı."""
    a = np.zeros((n, n, 3))
    w, h = 51, 25
    for j in range(0, n, h):
        kay = (j // h % 2) * w // 2
        for i in range(-w, n + w, w):
            r = 150 + rng.integers(-14, 14)
            renk = (r, r - 2, r - 6) if rng.random() > 0.08 else (r + 10, r - 20, r - 30)
            x0 = i + kay
            a[j:j + h, max(x0, 0):max(min(x0 + w, n), 0)] = renk
            a[j:j + h, max(x0, 0):max(min(x0 + 1, n), 0)] = (95, 95, 95)
        a[j:j + 1, :] = (95, 95, 95)
    a += gurultu(n, 1.0, rng, 6)[..., None]
    return a


def doku_duz(rng, renk, n=512, ince=6, kaba=10):
    return np.array(renk, float) + gurultu(n, 1.0, rng, ince)[..., None] + gurultu(n, 30, rng, kaba)[..., None]


def doku_zemin(rng, n=512):
    """Kent içi boş zemin (10 m'lik kare): eski beton plaklar, arada toprak ve kuru ot lekeleri (Ankara bina araları)."""
    a = doku_duz(rng, (150, 148, 142), n, 6, 8)
    p = 64                                              # 1,25 m plak
    for j in range(0, n, p):
        for i in range(0, n, p):
            a[j:j + p, i:i + p] += rng.integers(-10, 10)
        a[j:j + 2, :] *= 0.75
    a[:, ::p] *= 0.75
    a[:, 1::p] *= 0.85
    m = gurultu(n, 22, rng, 1) > 0.9
    a[m] = a[m] * 0.35 + np.array([128, 112, 86]) * 0.65   # toprak
    m2 = gurultu(n, 10, rng, 1) > 1.4
    a[m2] = a[m2] * 0.4 + np.array([110, 118, 70]) * 0.6   # ot
    return a


def doku_cim(rng, n=512):
    a = doku_duz(rng, (78, 104, 46), n, 14, 12)
    kuru = gurultu(n, 25, rng, 1) > 1.0
    a[kuru] = a[kuru] * 0.6 + np.array([150, 130, 80]) * 0.4
    return a


# ---------------------------------------------------------------- cephe hücreleri

def hucre_bos(renk):
    im = Image.new("RGB", (H, H), renk)
    return im, ImageDraw.Draw(im)


def leke(im, rng, guc=10):
    a = np.asarray(im, float)
    n = rng.standard_normal((H // 8, H // 8))
    n = np.asarray(Image.fromarray(((n + 3) * 40).clip(0, 255).astype(np.uint8)).resize((H, H), Image.BILINEAR), float)
    a = a + ((n - 120) / 120 * guc)[..., None]
    # alttan kirlenme
    a[int(H * 0.85):] *= 0.93
    return Image.fromarray(a.clip(0, 255).astype(np.uint8))


def pencere(dr, x0, y0, x1, y1, cerceve, cam, rng, perde=True, kepenk=False):
    dr.rectangle((x0, y0, x1, y1), fill=cerceve)
    k = 4
    ic = (x0 + k, y0 + k, x1 - k, y1 - k)
    c = tuple(int(v + rng.integers(-10, 10)) for v in cam)
    dr.rectangle(ic, fill=c)
    # yansıma
    dr.polygon([(ic[0], ic[3]), (ic[0] + (ic[2] - ic[0]) * 0.5, ic[1]), (ic[0] + (ic[2] - ic[0]) * 0.7, ic[1]),
                (ic[0] + (ic[2] - ic[0]) * 0.2, ic[3])], fill=tuple(min(255, v + 25) for v in c))
    # orta kayıt
    xm = (ic[0] + ic[2]) // 2
    dr.line((xm, ic[1], xm, ic[3]), fill=cerceve, width=3)
    if perde and rng.random() < 0.6:
        pr = rng.choice([(230, 225, 210), (200, 190, 170), (240, 240, 235), (190, 170, 150)])
        dr.rectangle((ic[0], ic[1], ic[0] + (ic[2] - ic[0]) * rng.uniform(0.15, 0.35), ic[3]), fill=tuple(pr))
    if kepenk and rng.random() < 0.3:
        dr.rectangle((ic[0], ic[1], ic[2], ic[1] + (ic[3] - ic[1]) * rng.uniform(0.3, 0.9)), fill=(200, 200, 195))


def korkuluk(dr, x0, y0, x1, y1, tur, renk_duvar):
    if tur == "demir":
        dr.rectangle((x0, y0, x1, y0 + 3), fill=(50, 50, 52))
        for x in range(x0, x1, 6):
            dr.line((x, y0, x, y1), fill=(50, 50, 52), width=2)
    elif tur == "cam":
        dr.rectangle((x0, y0, x1, y1), fill=(150, 175, 185))
        dr.rectangle((x0, y0, x1, y0 + 3), fill=(220, 220, 220))
    elif tur in ("tas", "beton"):
        c = tuple(int(v * 0.9) for v in renk_duvar)
        dr.rectangle((x0, y0, x1, y1), fill=c)
        dr.line((x0, y0, x1, y0), fill=tuple(int(v * 0.75) for v in renk_duvar), width=3)


def hucre_cephe(stil, renk, tip, rng):
    renkler, cerceve, cam, kork = STILLER[stil]
    im, dr = hucre_bos(renk)
    if stil == "cam_ofis":
        dr.rectangle((0, 0, H, H), fill=renk)
        for x in range(0, H, 32):
            dr.line((x, 0, x, H), fill=cerceve, width=4)
        dr.line((0, 0, H, 0), fill=cerceve, width=8)
        dr.line((0, int(H * 0.75), H, int(H * 0.75)), fill=cerceve, width=3)
        a = np.asarray(im, float)
        a += np.linspace(25, -15, H)[:, None, None]
        return leke(Image.fromarray(a.clip(0, 255).astype(np.uint8)), rng, 4)
    if stil == "ulus_tas":
        for y in range(0, H, 16):
            dr.line((0, y, H, y), fill=tuple(int(v * 0.86) for v in renk), width=1)
            off = (y // 16 % 2) * 20
            for x in range(off, H, 40):
                dr.line((x, y, x, y + 16), fill=tuple(int(v * 0.86) for v in renk), width=1)
        dr.rectangle((0, 0, H, 8), fill=tuple(int(v * 0.8) for v in renk))     # kat silmesi
    if stil == "tugla":
        for y in range(0, H, 6):
            dr.line((0, y, H, y), fill=(200, 190, 175), width=1)
            off = (y // 6 % 2) * 8
            for x in range(off, H, 16):
                dr.line((x, y, x, y + 6), fill=(200, 190, 175), width=1)
    if stil == "bulvar60":
        dr.rectangle((0, 0, H, 6), fill=tuple(int(v * 0.85) for v in renk))     # döşeme bandı
    if stil == "kamu":
        dr.rectangle((0, 0, H, 10), fill=(150, 150, 145))
        dr.rectangle((0, 0, 8, H), fill=(150, 150, 145))
    if tip == "P":
        if stil == "ulus_tas":
            pencere(dr, 34, 26, 94, 112, cerceve, cam, rng)
            dr.pieslice((34, 8, 94, 46), 180, 360, fill=tuple(int(v * 0.8) for v in renk))
        elif stil == "kurum":
            pencere(dr, 8, 34, 120, 92, cerceve, cam, rng, perde=False)
        elif stil == "gecekondu":
            pencere(dr, 40, 40, 88, 92, cerceve, cam, rng)
        else:
            pencere(dr, 22, 30, 106, 98, cerceve, cam, rng, kepenk=stil == "bulvar60")
    elif tip == "B":
        pencere(dr, 14, 18, 62, 112, cerceve, cam, rng)       # balkon kapısı
        pencere(dr, 70, 30, 114, 90, cerceve, cam, rng)
        if kork:
            korkuluk(dr, 0, 78, H, 118, kork, renk)
            dr.rectangle((0, 116, H, 124), fill=tuple(int(v * 0.8) for v in renk))   # balkon döşemesi
    else:   # D
        if rng.random() < 0.5 and stil not in ("kamu",):
            pencere(dr, 48, 44, 80, 84, cerceve, cam, rng, perde=False)
        if stil in ("mantolama",) and rng.random() < 0.5:
            dr.rectangle((0, 0, 20, H), fill=(110, 110, 112))     # vurgu şeridi
    # klima dış ünitesi / çanak anten
    if stil in ("bulvar60", "mantolama", "tugla", "gecekondu") and rng.random() < 0.18:
        x = int(rng.integers(8, 90))
        dr.rectangle((x, 100, x + 26, 118), fill=(225, 225, 222), outline=(150, 150, 150))
        dr.ellipse((x + 4, 103, x + 16, 115), outline=(120, 120, 120), width=2)
    return leke(im, rng, 9)


def hucre_dukkan(ad, rng):
    duvar = tuple(int(v) for v in rng.choice([(205, 200, 190), (180, 175, 165), (60, 60, 62), (215, 205, 180)]))
    im, dr = hucre_bos(duvar)
    tab = tuple(int(v) for v in TABELA_RENK[rng.integers(len(TABELA_RENK))])
    dr.rectangle((0, 6, H, 36), fill=tab)
    yazi = (255, 255, 255) if sum(tab) < 450 else (30, 30, 30)
    boy = 22 if len(ad) <= 7 else (17 if len(ad) <= 10 else 13)
    f = ImageFont.truetype(FONT, boy)
    w = dr.textlength(ad, font=f)
    dr.text(((H - w) / 2, 21 - boy * 0.62), ad, fill=yazi, font=f)
    # vitrin
    dr.rectangle((4, 42, H - 4, H - 2), fill=(45, 45, 48))
    dr.rectangle((8, 46, H - 8, H - 6), fill=(110, 130, 140))
    for i in range(int(rng.integers(3, 7))):
        x = int(rng.integers(12, H - 30))
        y = int(rng.integers(70, H - 26))
        dr.rectangle((x, y, x + int(rng.integers(8, 20)), y + int(rng.integers(8, 20))),
                     fill=tuple(int(v) for v in rng.integers(60, 230, 3)))
    dr.line((H // 2, 46, H // 2, H - 6), fill=(45, 45, 48), width=4)
    dr.polygon([(8, H - 6), (40, 46), (60, 46), (28, H - 6)], fill=(150, 170, 178))
    # tente
    if rng.random() < 0.35:
        tr = tuple(int(v) for v in rng.choice([(180, 40, 40), (40, 90, 50), (200, 150, 40), (40, 70, 140)]))
        dr.polygon([(0, 38), (H, 38), (H - 6, 58), (6, 58)], fill=tr)
        for x in range(0, H, 16):
            dr.line((x, 38, x + 3, 58), fill=(240, 240, 235), width=5)
    return leke(im, rng, 5)


def hucre_giris(renk, rng):
    im, dr = hucre_bos(renk)
    dr.rectangle((30, 20, 98, H), fill=(80, 60, 40))
    dr.rectangle((36, 26, 92, H - 4), fill=(120, 110, 95))
    dr.rectangle((40, 30, 88, 80), fill=(90, 110, 120))
    dr.rectangle((24, 12, 104, 20), fill=tuple(int(v * 0.8) for v in renk))   # saçak
    f = ImageFont.truetype(FONT, 11)
    dr.text((45, 2), "No:" + str(int(rng.integers(1, 120))), fill=(30, 30, 120), font=f)
    return leke(im, rng, 6)


def hucre_kepenk(rng):
    im, dr = hucre_bos((150, 150, 148))
    dr.rectangle((4, 30, H - 4, H), fill=(185, 185, 182))
    for y in range(30, H, 5):
        dr.line((4, y, H - 4, y), fill=(150, 150, 148), width=1)
    if rng.random() < 0.6:   # duvar yazısı
        f = ImageFont.truetype(FONT, 14)
        dr.text((14, 70), rng.choice(["KİRALIK", "SATILIK", "ANKARA", "06"]), fill=tuple(int(v) for v in rng.integers(30, 200, 3)), font=f)
    return leke(im, rng, 8)


def hucre_cami(tip, rng):
    tas = (214, 204, 182)
    im, dr = hucre_bos(tas)
    for y in range(0, H, 14):
        dr.line((0, y, H, y), fill=(196, 186, 165), width=1)
    if tip == "P":
        dr.rectangle((38, 30, 90, 104), fill=(70, 80, 88))
        dr.pieslice((38, 8, 90, 52), 180, 360, fill=(70, 80, 88))
        for x in (50, 64, 78):
            dr.line((x, 30, x, 104), fill=(200, 190, 170), width=2)
    elif tip == "minare":
        im, dr = hucre_bos((236, 232, 222))
        for y in (20, 70, 110):
            dr.rectangle((0, y, H, y + 5), fill=(190, 180, 160))
    elif tip == "kubbe":
        im, dr = hucre_bos((120, 128, 132))
        for x in range(0, H, 16):
            dr.line((x, 0, x, H), fill=(100, 108, 112), width=2)
    return leke(im, rng, 5)


def atlas(out):
    rng = np.random.default_rng(408)
    im = Image.new("RGB", (H * N, H * N), (128, 128, 128))
    harita = {"hucre": H, "n": N, "stiller": {}, "dukkan": [], "giris": [], "kepenk": [], "cami": {}}
    k = 0

    def koy(h):
        nonlocal k
        im.paste(h, ((k % N) * H, (k // N) * H))
        k += 1
        return k - 1

    for stil, (renkler, *_r) in STILLER.items():
        harita["stiller"][stil] = []
        for renk in renkler:
            h = {t: hucre_cephe(stil, renk, t, rng) for t in ("P", "B", "D")}
            hk = {t: koy(c) for t, c in h.items()}
            # U (uzak): 4 bölme × 4 kat küçültülmüş cephe — LOD1 duvarları için
            u = Image.new("RGB", (H, H))
            desen = "PBPD" if stil not in ("cam_ofis", "kurum") else "PPPP"
            for r in range(4):
                for c in range(4):
                    u.paste(h[desen[(c + r) % 4] if stil != "ulus_tas" else "P"].resize((H // 4, H // 4), Image.LANCZOS),
                            (c * H // 4, r * H // 4))
            hk["U"] = koy(u)
            harita["stiller"][stil].append(hk)
    for ad in DUKKANLAR:
        harita["dukkan"].append(koy(hucre_dukkan(ad, rng)))
    for renk in [(214, 196, 160), (232, 172, 142), (236, 234, 228), (188, 172, 138)]:
        harita["giris"].append(koy(hucre_giris(renk, rng)))
    for _ in range(3):
        harita["kepenk"].append(koy(hucre_kepenk(rng)))
    for t in ("P", "D", "minare", "kubbe"):
        harita["cami"][t] = koy(hucre_cami(t, rng))
    temel = Image.fromarray(doku_duz(rng, (146, 144, 138), H, 6, 4).clip(0, 255).astype(np.uint8))
    harita["temel"] = koy(temel)
    im.save(os.path.join(out, "cephe_atlas.png"))
    json.dump(harita, open(os.path.join(out, "cephe_atlas.json"), "w"), indent=1)
    print(f"cephe atlası: {k} hücre")


def cati_atlas(out):
    rng = np.random.default_rng(7)
    n = 512
    hucreler = []
    a = doku_duz(rng, (150, 146, 140), n, 18, 8)                 # çakıl
    hucreler.append(a)
    a = doku_duz(rng, (172, 170, 165), n, 5, 10)                 # beton (kırık su yalıtımı lekeleri)
    m = gurultu(n, 20, rng, 1) > 1.3
    a[m] = a[m] * 0.5 + np.array([60, 60, 62]) * 0.5
    hucreler.append(a)
    a = np.zeros((n, n, 3))                                      # kiremit (Marsilya tipi, sıra sıra)
    for y in range(0, n, 32):
        for x in range(-32, n, 40):
            off = (y // 32 % 2) * 20
            r = rng.integers(-15, 15)
            a[y:y + 32, max(x + off, 0):max(min(x + off + 40, n), 0)] = (170 + r, 78 + r // 2, 52 + r // 3)
        a[y + 26:y + 32] *= 0.65
    a += gurultu(n, 1, rng, 6)[..., None]
    hucreler.append(a)
    hucreler.append(doku_duz(rng, (118, 126, 130), n, 4, 6))    # kurşun kaplama (kubbe)
    b = np.zeros((2 * n, 2 * n, 3))
    for i, h in enumerate(hucreler):
        b[(i // 2) * n:(i // 2 + 1) * n, (i % 2) * n:(i % 2 + 1) * n] = h
    kaydet(b, os.path.join(out, "cati_atlas.png"))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True)
    a = ap.parse_args()
    os.makedirs(a.out, exist_ok=True)
    rng = np.random.default_rng(2024)
    kaydet(doku_asfalt(rng), os.path.join(a.out, "asfalt.png"))
    kaydet(doku_kaldirim(rng), os.path.join(a.out, "kaldirim.png"))
    kaydet(doku_cim(rng), os.path.join(a.out, "refuj.png"))
    kaydet(doku_duz(rng, (168, 166, 160), 256, 6, 6), os.path.join(a.out, "bordur.png"))
    kaydet(doku_zemin(rng), os.path.join(a.out, "zemin.png"))
    kaydet(doku_cim(rng), os.path.join(a.out, "cim.png"))
    su = doku_duz(rng, (52, 92, 104), 256, 3, 6)
    su += np.sin(np.arange(256) / 256 * 2 * np.pi * 12)[None, :, None] * 5
    kaydet(su, os.path.join(a.out, "su.png"))
    atlas(a.out)
    cati_atlas(a.out)


if __name__ == "__main__":
    main()
