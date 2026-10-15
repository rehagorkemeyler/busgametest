"""
A3 — Binalar: OSM ayak izleri → Ankara cepheli bina ağları (parça başına).

- Koridordaki (güzergâhtan KORIDOR m) tüm bina ayak izleri; ≤ YAKIN m olanlar ayrıntılı (bölme başına dörtgen, zemin katta
  dükkân/giriş) + LOD1; daha uzaktakiler yalnız basit (BinalarUzak).
- Kat sayısı: OSM building:levels / height; yoksa güzergâh boyunca bölge kuralı (BOLGELER), bina türü ve taban alanı.
- Stil: bölgenin stil ağırlıkları + tür (okul/hastane → kurum, ofis → cam), bina kimliğinden belirlenimci.
- Çatı: düz (çakıl/beton, korkuluk, su deposu) ya da küçük evlerde kırma kiremit.
- Cami: taş duvar, kasnaklı kubbe, minare(ler) (OSM'deki minare konumları; yoksa köşeye).
- Taban: arazinin (SonArazi) ayak izi altındaki en düşük kotu; 1,5 m temel aşağı iner (yokuşta bina gömülü değil).

Malzeme adları "taban@grup": H3_Cephe, H3_Cati (Binalar), …@BinalarLOD1, …@BinalarUzak, H3_Carpisma@BinalarCarpisma.
Çıktı: <ISLEM>/parcalar/bina_<i>_<j>.pkl, <ISLEM>/binalar.pkl (önizleme için taban listesi)
Kullanım: python -m hat3.binalar
"""
import hashlib
import json
import math
import os
import pickle
import time

import numpy as np
import shapely
from shapely.geometry import LineString, MultiPolygon, Point, Polygon
from shapely.ops import polylabel

from . import kok
from .mesh import Parcalar, anahtar, cokgenler, ucgenle
from .yollar import KORIDOR
from .yuzeyler import SonArazi

YAKIN = 260.0
BOLME = 3.0             # bölme genişliği (m)
KAT = 2.9
ZEMIN_KAT_DUKKAN = 4.0
ZEMIN_KAT = 3.0
TEXTURES = os.environ.get("HARITA_DOKULAR", os.path.join(kok.KLASOR, "..", "..", "AnkaraBusSimulator", "Assets", "_Project", "Maps", "Hat3", "Textures"))

# (güzergâh s sonu, ad, kat aralığı, stil ağırlıkları, cadde cephesinde dükkân olasılığı)
# bölgeler hat başına, güzergâh boyunca s (m) üst sınırıyla: (s_son, ad, kat aralığı, stil ağırlıkları, dolgu oranı)
BOLGELER_ATAKULE = [
    (700, "Kızılay–Güvenpark", (6, 10), {"bulvar60": 5, "cam_ofis": 2, "kamu": 2, "mantolama": 1}, 0.9),
    (1700, "Bakanlıklar–Meclis", (5, 9), {"kamu": 5, "kurum": 2, "bulvar60": 2}, 0.5),
    (2650, "Kavaklıdere", (4, 8), {"bulvar60": 3, "mantolama": 3, "cam_ofis": 1, "tugla": 1}, 0.7),
    (1e9, "Cinnah–Çankaya", (4, 7), {"mantolama": 4, "bulvar60": 2, "tugla": 2, "kamu": 1}, 0.6),
]
BOLGELER = [
    (720, "Kızılay–Sıhhiye", (5, 10), {"bulvar60": 6, "mantolama": 2, "cam_ofis": 1, "kamu": 1, "tugla": 1}, 0.9),
    (1450, "Sıhhiye–Opera", (5, 9), {"kamu": 4, "bulvar60": 3, "kurum": 2, "mantolama": 1}, 0.6),
    (2350, "Opera–Ulus", (4, 7), {"ulus_tas": 4, "bulvar60": 3, "kamu": 2}, 0.85),
    (3300, "Çankırı Caddesi", (5, 8), {"bulvar60": 4, "mantolama": 2, "cam_ofis": 1, "tugla": 1}, 0.8),
    (4300, "Dışkapı", (4, 8), {"kurum": 2, "bulvar60": 3, "mantolama": 2, "tugla": 1}, 0.5),
    (5500, "Aydınlıkevler–Altınpark", (5, 9), {"bulvar60": 3, "mantolama": 3, "tugla": 2}, 0.6),
    (6450, "Subayevleri–Hasköy", (4, 8), {"mantolama": 3, "bulvar60": 2, "tugla": 2, "gecekondu": 1}, 0.55),
    (7650, "Güneşevler", (3, 6), {"mantolama": 3, "tugla": 2, "gecekondu": 2, "bulvar60": 1}, 0.6),
    (1e9, "Ali Ersoy", (4, 8), {"mantolama": 5, "tugla": 1, "gecekondu": 1}, 0.4),
]
def bolge_sec(hat, s):
    """(hat, s) → bölge satırı. Uretici'ye s < 0 gelirse Atakule hattında −1 − s."""
    if hat == "408_atakule":
        return next(b for b in BOLGELER_ATAKULE if s < b[0])
    if s < 0:
        return next(b for b in BOLGELER_ATAKULE if -1.0 - s < b[0])
    return next(b for b in BOLGELER if s < b[0])


KUCUK = ("garage", "garages", "shed", "kiosk", "hut", "container", "service", "toilets", "carport", "transformer_tower",
         "greenhouse", "cabin")
EV = ("house", "detached", "bungalow", "semidetached_house", "terrace")
KURUM = ("school", "kindergarten", "hospital", "university", "college", "public", "government", "civic", "dormitory",
         "train_station", "transportation", "stadium", "sports_hall", "sports_centre")
ATLA = ("ruins", "construction", "no", "bridge", "tent", "ship")


def hash01(*a):
    h = hashlib.md5(("/".join(map(str, a))).encode()).digest()
    return int.from_bytes(h[:4], "little") / 2 ** 32


def secim(agirlik, u):
    top = sum(agirlik.values())
    s = 0
    for k, v in agirlik.items():
        s += v / top
        if u <= s:
            return k
    return k


class Atlas:
    def __init__(self):
        self.j = json.load(open(os.path.join(TEXTURES, "cephe_atlas.json")))
        self.n = self.j["n"]
        self.eps = 1.5 / (self.n * self.j["hucre"])

    def uv(self, k, u0=0.0, u1=1.0, v0=0.0, v1=1.0):
        """Hücre k'nin içindeki (u, v) ∈ [0,1] aralığını atlas UV'sine çevirir: [(u0,v0),(u1,v0),(u1,v1),(u0,v1)]"""
        c, r = k % self.n, k // self.n
        a = c / self.n + self.eps
        b = (c + 1) / self.n - self.eps
        alt = 1 - (r + 1) / self.n + self.eps
        ust = 1 - r / self.n - self.eps

        def m(u, v):
            return (a + (b - a) * u, alt + (ust - alt) * v)
        return [m(u0, v0), m(u1, v0), m(u1, v1), m(u0, v1)]


# çatı atlası 2×2: 0 çakıl, 1 beton, 2 kiremit, 3 kurşun
def cati_uv(hucre, u, v):
    c, r = hucre % 2, hucre // 2
    eps = 2 / 1024
    return np.stack([c / 2 + eps + (0.5 - 2 * eps) * u, 1 - (r + 1) / 2 + eps + (0.5 - 2 * eps) * v], -1)


def _haric():
    yol = os.path.join(kok.ISLEM, "simge_haric.json")
    return set(json.load(open(yol))) if os.path.exists(yol) else set()


HARIC = _haric()      # simgeler.py: yerine hazır model konan OSM binaları
# adı bilinen tarihî/kamu binaları (Ulus): kesme taş ya da kamu cephesi
AD_STIL = {"Bankası": "ulus_tas", "Müzesi": "ulus_tas", "Han": "ulus_tas", "Palas": "ulus_tas", "Valiliği": "ulus_tas",
           "Çarşısı": "kamu", "Postane": "ulus_tas", "PTT": "ulus_tas", "Hastanesi": "kurum", "Fakültesi": "kurum",
           "Meclis": "ulus_tas", "Bakanlığı": "kamu", "Büyükelçiliği": "kamu", "Elçiliği": "kamu"}
# OSM'de kat bilgisi olmayan simge binalar (gerçek kat sayısı): TBMM ana bina (Holzmeister, ~6 kat), Atakule AVM (3 kat podyum)
OZEL_KAT = {67624843: 6, 681425145: 3}


def bina_listesi(o, koridor):
    out = []
    xy = o["xy"]
    for wid, w in o["ways"].items():
        t = w["t"]
        if "building" not in t or t["building"] in ATLA or t.get("location") == "underground" or \
                t.get("layer", "0").lstrip("+").lstrip("-").isdigit() and t.get("layer", "0").startswith("-"):
            continue
        if wid in HARIC:
            continue
        ns = [xy[n] for n in w["n"] if n in xy]
        if len(ns) < 4:
            continue
        out.append((wid, Polygon(ns), t))
    for rid, r in o["relations"].items():
        t = r["t"]
        if "building" not in t or t.get("type") != "multipolygon" or t["building"] in ATLA:
            continue
        dis, ic = [], []
        for typ, ref, rol in r["m"]:
            if typ != "way" or ref not in o["ways"]:
                continue
            ns = [xy[n] for n in o["ways"][ref]["n"] if n in xy]
            if len(ns) >= 4 and ns[0] == ns[-1]:
                (dis if rol != "inner" else ic).append(ns)
        for d in dis:
            pg = Polygon(d, [h for h in ic if Polygon(d).contains(Point(h[0]))])
            out.append((-rid, pg, t))
    sec = []
    shapely.prepare(koridor)
    for wid, pg, t in out:
        pg = pg.buffer(0)
        if pg.is_empty:
            continue
        pg = pg.simplify(0.35, preserve_topology=True)
        for p in cokgenler(pg):
            if p.area >= 12 and koridor.intersects(p):
                sec.append((wid, shapely.geometry.polygon.orient(p, 1.0), t))
    return sec


def kat_sayisi(wid, t, pg, bolge, tur):
    if wid in OZEL_KAT:
        return OZEL_KAT[wid]
    for k in ("building:levels", "levels"):
        if k in t:
            try:
                return max(1, int(round(float(t[k].split(";")[0].split("-")[-1].replace(",", ".")))))
            except ValueError:
                pass
    if "height" in t:
        try:
            return max(1, int(round(float(t["height"].replace("m", "").strip()) / 3.0)))
        except ValueError:
            pass
    alan = pg.area
    u = hash01(wid, "kat")
    if tur in KUCUK or alan < 40:
        return 1
    if tur in EV:
        return 1 + int(u * 2)
    if tur in ("industrial", "warehouse", "garage"):
        return 1 + int(u * 2)
    if tur in ("school", "kindergarten"):
        return 2 + int(u * 3)
    if tur in ("hospital",):
        return 5 + int(u * 4)
    if tur in ("university", "college", "public", "government", "civic"):
        return 3 + int(u * 4)
    lo, hi = bolge[2]
    k = lo + int(u * (hi - lo + 1))
    if alan < 90:
        k = min(k, 2 + int(u * 2))
    elif alan < 160:
        k = min(k, 4)
    if alan > 3000:
        k = min(k, 5)
    return k


def stil_sec(wid, t, bolge, tur):
    u = hash01(wid, "stil")
    ad = t.get("name", "")
    for anahtar_, st in AD_STIL.items():
        if anahtar_ in ad:
            return st
    if tur in ("school", "kindergarten", "hospital", "university", "college", "dormitory"):
        return "kurum"
    if tur in ("public", "government", "civic", "train_station"):
        return "kamu" if u < 0.7 else "ulus_tas"
    if tur in ("office", "commercial", "retail") and u < 0.25:
        return "cam_ofis"
    if tur in EV or tur in KUCUK:
        return "gecekondu" if u < 0.6 else "tugla"
    if tur in ("industrial", "warehouse"):
        return "kamu"
    return secim(bolge[3], u)


class Uretici:
    def __init__(self):
        self.atlas = Atlas()
        self.pc = Parcalar()
        self.arazi = SonArazi()
        self.sayac = {"ayrintili": 0, "uzak": 0, "cami": 0, "minare": 0, "dukkan": 0}

    # ----- düşük seviye: dörtgen ekle
    def dortgen(self, mal, k, a, b, ya, yb, uv):
        """a→b duvar (x,z), alt ya, üst yb; yüz a→b'nin sağına (dışa) bakar."""
        V = [(a[0], ya, a[1]), (b[0], ya, b[1]), (b[0], yb, b[1]), (a[0], yb, a[1])]
        self.pc.ag(k, mal).ekle(V, uv, [(0, 1, 2), (0, 2, 3)])

    def taban_kotu(self, pg):
        c = np.asarray(pg.exterior.coords)
        if len(c) > 40:
            c = c[:: len(c) // 40 + 1]
        icn = pg.representative_point()
        y = self.arazi.y(np.append(c[:, 0], icn.x), np.append(c[:, 1], icn.y))
        return float(y.min()), float(y.max())

    # ----- çatı
    def duz_cati(self, mal, k, pg, y, hucre):
        xz, tri = ucgenle(pg)
        if len(tri) == 0:
            return
        x0, z0, x1, z1 = pg.bounds
        s = max(x1 - x0, z1 - z0, 1.0)
        uv = cati_uv(hucre, (xz[:, 0] - x0) / s, (xz[:, 1] - z0) / s)
        V = np.stack([xz[:, 0], np.full(len(xz), y), xz[:, 1]], 1)
        self.pc.ag(k, mal).ekle(V, uv, tri)

    def kirma_cati(self, mal, k, pg, y):
        """En küçük döndürülmüş dikdörtgen üzerine kırma çatı (0,35 m saçak)."""
        r = pg.minimum_rotated_rectangle.buffer(0.35, join_style="mitre")
        c = np.asarray(shapely.geometry.polygon.orient(r, 1.0).exterior.coords)[:4]
        e0, e1 = np.linalg.norm(c[1] - c[0]), np.linalg.norm(c[2] - c[1])
        if e0 < e1:
            c = np.roll(c, -1, axis=0)
            e0, e1 = e1, e0
        # c0→c1 uzun kenar
        h = 0.32 * e1
        ux = (c[1] - c[0]) / e0
        mid0 = (c[0] + c[3]) / 2 + ux * (e1 / 2)
        mid1 = (c[1] + c[2]) / 2 - ux * (e1 / 2)
        if np.dot(mid1 - mid0, ux) < 0:
            mid0 = mid1 = (c[0] + c[2]) / 2
        P = [c[0], c[1], c[2], c[3], mid0, mid1]
        Y = [y, y, y, y, y + h, y + h]
        V = np.array([(p[0], yy, p[1]) for p, yy in zip(P, Y)])
        # yüzler (dışa/yukarı): ön (0,1,5,4), sağ uç (1,2,5), arka (2,3,4,5), sol uç (3,0,4)
        F = [(0, 1, 5), (0, 5, 4), (1, 2, 5), (2, 3, 4), (2, 4, 5), (3, 0, 4)]
        uv = cati_uv(2, (V[:, 0] - V[:, 0].min()) / max(e0, 1), (V[:, 2] - V[:, 2].min()) / max(e0, 1))
        self.pc.ag(k, mal).ekle(V, uv, F)

    # ----- cephe
    def cephe(self, pg, k, stil, renk, katlar, y0, ayrintili, dukkan_olas, wid, tur):
        A = self.atlas
        st = A.j["stiller"][stil][renk]
        temel = A.j["temel"]
        zh = ZEMIN_KAT_DUKKAN if dukkan_olas > 0 else ZEMIN_KAT
        ust = y0 + zh + (katlar - 1) * KAT
        mal = "H3_Cephe" if ayrintili else "H3_Cephe@BinalarUzak"
        desen = ["PBPD", "PPBP", "PDPB", "BPPB", "PBBP"][int(hash01(wid, "desen") * 5)]
        if stil in ("cam_ofis", "kurum"):
            desen = "PPPP"
        if stil == "gecekondu":
            desen = "PDPD"
        giris_konuldu = False
        halkalar = [pg.exterior] + list(pg.interiors)
        en_uzun = max(((np.linalg.norm(np.subtract(b, a)), i) for i, (a, b) in
                       enumerate(zip(pg.exterior.coords[:-1], pg.exterior.coords[1:]))), default=(0, 0))[1]
        for hi, halka in enumerate(halkalar):
            c = np.asarray(halka.coords)
            for ei, (a, b) in enumerate(zip(c[:-1], c[1:])):
                L = float(np.linalg.norm(b - a))
                if L < 0.3:
                    continue
                t = (b - a) / L
                n = np.array([t[1], -t[0]])
                m = (a + b) / 2
                # temel (arazinin altına)
                self.dortgen(mal, k, a, b, y0 - 1.5, y0, A.uv(temel, 0, L / 3, 0, 0.5))
                if not ayrintili:
                    self.uzak_duvar(mal, k, a, b, L, y0, ust, katlar, st["U"], zh)
                    continue
                # cadde cephesi mi (6–14 m önünde yol/kaldırım)?
                dm = self.arazi.yol_mesafesi(np.array([m[0] + n[0] * 4, m[0] + n[0] * 10]),
                                             np.array([m[1] + n[1] * 4, m[1] + n[1] * 10]))
                cadde = hi == 0 and float(dm.min()) < 1.0
                nb = max(1, int(round(L / BOLME)))
                bw = L / nb
                for j in range(nb):
                    p0, p1 = a + t * bw * j, a + t * bw * (j + 1)
                    # zemin kat
                    if cadde and dukkan_olas > 0 and hash01(wid, ei, j, "dk") < dukkan_olas and L >= 3:
                        hucre = A.j["dukkan"][int(hash01(wid, ei, j) * len(A.j["dukkan"]))]
                        self.sayac["dukkan"] += 1
                    elif cadde and not giris_konuldu and ei == en_uzun and j == nb // 2:
                        hucre = A.j["giris"][int(hash01(wid, "g") * len(A.j["giris"]))]
                        giris_konuldu = True
                    elif cadde and dukkan_olas > 0 and hash01(wid, ei, j, "kp") < 0.15:
                        hucre = A.j["kepenk"][int(hash01(wid, ei, j, "k") * len(A.j["kepenk"]))]
                    else:
                        hucre = st["P" if hash01(wid, ei, j, "z") < 0.6 else "D"]
                    self.dortgen(mal, k, p0, p1, y0, y0 + zh, A.uv(hucre))
                    for f in range(1, katlar):
                        tip = desen[(j + f) % len(desen)] if L >= 2.4 else "D"
                        if not cadde and tip == "B" and hash01(wid, ei, j, f) < 0.5:
                            tip = "P"
                        yb = y0 + zh + (f - 1) * KAT
                        self.dortgen(mal, k, p0, p1, yb, yb + KAT, A.uv(st[tip]))
                # korkuluk (çatı parapeti)
                self.dortgen(mal, k, a, b, ust, ust + 0.6, A.uv(st["D"], 0, 1, 0.0, 0.2))
        return ust

    def uzak_duvar(self, mal, k, a, b, L, y0, ust, katlar, hucre, zh):
        A = self.atlas
        t = (b - a) / L
        nc = max(1, int(round(L / 12.0)))
        cw = L / nc
        yuk = ust - y0
        nr = max(1, int(math.ceil(katlar / 4)))
        for i in range(nc):
            p0, p1 = a + t * cw * i, a + t * cw * (i + 1)
            for r in range(nr):
                ya = y0 + r * 4 * KAT * (yuk / (katlar * KAT))
                kat_bu = min(4, katlar - r * 4)
                yb = ya + kat_bu * KAT * (yuk / (katlar * KAT))
                self.dortgen(mal, k, p0, p1, ya, yb, A.uv(hucre, 0, 1, 1 - kat_bu / 4, 1))

    # ----- bina
    def bina(self, wid, pg, t, s, d_guz, minareler):
        tur = t.get("building", "yes")
        if t.get("amenity") == "place_of_worship" and (t.get("religion") == "muslim" or tur == "mosque") or tur == "mosque":
            return self.cami(wid, pg, t, minareler)
        bolge = bolge_sec("", s)
        katlar = kat_sayisi(wid, t, pg, bolge, tur)
        stil = stil_sec(wid, t, bolge, tur)
        renk = int(hash01(wid, "renk") * len(self.atlas.j["stiller"][stil]))
        y0, y1 = self.taban_kotu(pg)
        c = pg.representative_point()
        k = anahtar(c.x, c.y)
        ayrintili = d_guz <= YAKIN
        if tur == "roof":                           # sundurma/akaryakıt istasyonu kanopisi
            for mal in (("H3_Cati",) if ayrintili else ("H3_Cati@BinalarUzak",)):
                self.duz_cati(mal, k, pg, y0 + 4.8, 1)
            return
        dukkan = bolge[4] if tur in ("yes", "apartments", "residential", "commercial", "retail", "office") and katlar >= 3 \
            else 0.0
        ust = self.cephe(pg, k, stil, renk, katlar, y0, ayrintili, dukkan, wid, tur)
        kirma = katlar <= 3 and pg.area < 260 and pg.area / pg.minimum_rotated_rectangle.area > 0.8 and \
            (tur in EV or stil in ("gecekondu",) or hash01(wid, "cati") < 0.25)
        cati_mal = "H3_Cati" if ayrintili else "H3_Cati@BinalarUzak"
        if kirma:
            self.kirma_cati(cati_mal, k, pg, ust + (0.6 if ayrintili else 0))
        else:
            self.duz_cati(cati_mal, k, pg, ust + 0.05, 0 if hash01(wid, "ct") < 0.5 else 1)
            if ayrintili and tur not in KUCUK and pg.area > 80 and hash01(wid, "depo") < 0.7:
                self.su_deposu(cati_mal, k, pg, ust + 0.05, wid)
        if ayrintili:
            self.lod1(wid, pg, k, stil, renk, katlar, y0, ust, kirma)
            self.carpisma(pg, k, y0, ust)
            self.sayac["ayrintili"] += 1
        else:
            self.sayac["uzak"] += 1

    def lod1(self, wid, pg, k, stil, renk, katlar, y0, ust, kirma):
        st = self.atlas.j["stiller"][stil][renk]
        for halka in [pg.exterior]:
            c = np.asarray(halka.coords)
            for a, b in zip(c[:-1], c[1:]):
                L = float(np.linalg.norm(b - a))
                if L >= 0.3:
                    self.uzak_duvar("H3_Cephe@BinalarLOD1", k, a, b, L, y0, ust, katlar, st["U"], ZEMIN_KAT)
        if kirma:
            self.kirma_cati("H3_Cati@BinalarLOD1", k, pg, ust)
        else:
            self.duz_cati("H3_Cati@BinalarLOD1", k, pg, ust + 0.05, 0)

    def carpisma(self, pg, k, y0, ust):
        c = np.asarray(pg.simplify(1.0).exterior.coords)
        for a, b in zip(c[:-1], c[1:]):
            self.dortgen("H3_Carpisma@BinalarCarpisma", k, a, b, y0 - 1, min(ust, y0 + 10), [(0, 0)] * 4)

    def su_deposu(self, mal, k, pg, y, wid):
        for i in range(1 + int(hash01(wid, "dn") * 2)):
            p = pg.representative_point() if i == 0 else pg.centroid
            ox = (hash01(wid, i, "x") - 0.5) * 4
            oz = (hash01(wid, i, "z") - 0.5) * 4
            q = Point(p.x + ox, p.y + oz)
            if not pg.buffer(-1.2).contains(q):
                continue
            self.kutu(mal, k, q.x, q.y, 0.6, y, y + 1.1, cati_hucre=1)

    def kutu(self, mal, k, x, z, r, ya, yb, cati_hucre=1, n=4, r_ust=None):
        r_ust = r if r_ust is None else r_ust
        ac = [2 * math.pi * i / n + math.pi / n for i in range(n)]
        alt = [(x + r * math.cos(a_), z + r * math.sin(a_)) for a_ in ac]
        ustp = [(x + r_ust * math.cos(a_), z + r_ust * math.sin(a_)) for a_ in ac]
        V, UV, F = [], [], []
        for i in range(n):
            j = (i + 1) % n
            b0 = len(V)
            # çevre saat yönünün tersine: dış normal a→b'nin sağı
            a_, b_ = alt[i], alt[j]
            au, bu = ustp[i], ustp[j]
            V += [(a_[0], ya, a_[1]), (b_[0], ya, b_[1]), (bu[0], yb, bu[1]), (au[0], yb, au[1])]
            UV += list(cati_uv(cati_hucre, np.array([0, 1, 1, 0.0]), np.array([0, 0, 1, 1.0])))
            F += [(b0, b0 + 1, b0 + 2), (b0, b0 + 2, b0 + 3)]
        b0 = len(V)
        for p in ustp:
            V.append((p[0], yb, p[1]))
            UV.append(cati_uv(cati_hucre, 0.5, 0.5))
        for i in range(1, n - 1):
            F.append((b0, b0 + i, b0 + i + 1))
        self.pc.ag(k, mal).ekle(np.array(V), np.array(UV), F)

    # ----- cami
    def cami(self, wid, pg, t, minareler):
        A = self.atlas
        y0, _ = self.taban_kotu(pg)
        c = pg.representative_point()
        k = anahtar(c.x, c.y)
        alan = pg.area
        h = 6.0 if alan < 250 else (8.5 if alan < 800 else 11.0)
        cami = A.j["cami"]
        for halka in [pg.exterior] + list(pg.interiors):
            cc = np.asarray(halka.coords)
            for a, b in zip(cc[:-1], cc[1:]):
                L = float(np.linalg.norm(b - a))
                if L < 0.3:
                    continue
                tt = (b - a) / L
                nb = max(1, int(round(L / 3.5)))
                bw = L / nb
                self.dortgen("H3_Cephe", k, a, b, y0 - 1.5, y0, A.uv(A.j["temel"]))
                for j in range(nb):
                    p0, p1 = a + tt * bw * j, a + tt * bw * (j + 1)
                    self.dortgen("H3_Cephe", k, p0, p1, y0, y0 + h, A.uv(cami["P"] if j % 2 == 0 else cami["D"]))
                self.dortgen("H3_Cephe@BinalarLOD1", k, a, b, y0, y0 + h, A.uv(cami["D"]))
        self.duz_cati("H3_Cati", k, pg, y0 + h, 1)
        self.duz_cati("H3_Cati@BinalarLOD1", k, pg, y0 + h, 1)
        # kubbe: en büyük iç daire (polylabel)
        merkez = polylabel(pg, tolerance=0.5)
        r = min(pg.exterior.distance(merkez) * 0.92, 14.0)
        if r >= 2.0:
            for mal in ("H3_Cephe", "H3_Cephe@BinalarLOD1"):
                self.kubbe(mal, k, merkez.x, merkez.y, r, y0 + h, mal == "H3_Cephe")
        # minareler
        yakin = [m for m in minareler if pg.buffer(25).contains(Point(m))]
        if not yakin and alan >= 60:
            dis = np.asarray(pg.exterior.coords)[:-1]
            uz = np.linalg.norm(dis - np.array([merkez.x, merkez.y]), axis=1)
            sira = np.argsort(-uz)
            yakin = [tuple(dis[sira[0]])]
            if alan > 800 and len(sira) > 1:
                ikinci = max(dis, key=lambda p: np.linalg.norm(p - dis[sira[0]]))
                yakin.append(tuple(ikinci))
        H_m = 15.0 if alan < 150 else (25.0 if alan < 800 else 35.0)
        for m in yakin[:4]:
            my, _ = self.taban_kotu(Point(m).buffer(1.5))
            self.minare(k, m[0], m[1], my, H_m)
            self.sayac["minare"] += 1
        self.carpisma(pg, k, y0, y0 + h)
        self.sayac["cami"] += 1

    def kubbe(self, mal, k, x, z, r, y, ayrintili):
        A = self.atlas
        kasnak = 0.25 * r
        n = 16 if ayrintili else 8
        halkalar = 5 if ayrintili else 3
        # kasnak (silindir)
        ac = [2 * math.pi * i / n for i in range(n + 1)]
        for i in range(n):
            a = (x + r * math.cos(ac[i]), z + r * math.sin(ac[i]))
            b = (x + r * math.cos(ac[i + 1]), z + r * math.sin(ac[i + 1]))
            self.dortgen(mal, k, a, b, y, y + kasnak, A.uv(A.j["cami"]["D"]))
        # yarım küre (kurşun)
        V, UV, F = [], [], []
        hucre = A.j["cami"]["kubbe"]
        for h_ in range(halkalar + 1):
            fi = (math.pi / 2) * h_ / halkalar
            rr, yy = r * math.cos(fi), y + kasnak + r * 0.85 * math.sin(fi)
            for i in range(n + 1):
                V.append((x + rr * math.cos(ac[i]), yy, z + rr * math.sin(ac[i])))
                u = A.uv(hucre, i / n, i / n, h_ / halkalar, h_ / halkalar)[0]
                UV.append(u)
        for h_ in range(halkalar):
            for i in range(n):
                a = h_ * (n + 1) + i
                b, c, d = a + 1, a + n + 1, a + n + 2
                F += [(a, b, d), (a, d, c)]
        self.pc.ag(k, mal).ekle(np.array(V), np.array(UV), F)

    def minare(self, k, x, z, y, H_m):
        A = self.atlas
        h = A.j["cami"]["minare"]
        govde = y + H_m * 0.78

        def silindir(mal, r0, r1, ya, yb, n=8):
            ac = [2 * math.pi * i / n for i in range(n + 1)]
            for i in range(n):
                a = (x + r0 * math.cos(ac[i]), z + r0 * math.sin(ac[i]))
                b = (x + r0 * math.cos(ac[i + 1]), z + r0 * math.sin(ac[i + 1]))
                if r1 == r0:
                    self.dortgen(mal, k, a, b, ya, yb, A.uv(h))
                else:
                    a2 = (x + r1 * math.cos(ac[i]), z + r1 * math.sin(ac[i]))
                    b2 = (x + r1 * math.cos(ac[i + 1]), z + r1 * math.sin(ac[i + 1]))
                    V = [(a[0], ya, a[1]), (b[0], ya, b[1]), (b2[0], yb, b2[1]), (a2[0], yb, a2[1])]
                    self.pc.ag(k, mal).ekle(V, A.uv(h), [(0, 1, 2), (0, 2, 3)])

        for mal, n in (("H3_Cephe", 8), ("H3_Cephe@BinalarLOD1", 6)):
            silindir(mal, 1.15, 1.15, y - 1, govde, n)
            silindir(mal, 1.15, 1.75, govde, govde + 0.4, n)         # şerefe altı
            silindir(mal, 1.75, 1.75, govde + 0.4, govde + 1.0, n)   # şerefe korkuluğu
            silindir(mal, 0.95, 0.95, govde + 1.0, y + H_m * 0.93, n)
            # külah
            ac = [2 * math.pi * i / n for i in range(n + 1)]
            tepe = y + H_m * 0.93 + 4.5
            hk = A.j["cami"]["kubbe"]
            for i in range(n):
                a = (x + 1.05 * math.cos(ac[i]), z + 1.05 * math.sin(ac[i]))
                b = (x + 1.05 * math.cos(ac[i + 1]), z + 1.05 * math.sin(ac[i + 1]))
                yb = y + H_m * 0.93
                V = [(a[0], yb, a[1]), (b[0], yb, b[1]), (x, tepe, z)]
                uv = A.uv(hk)
                self.pc.ag(k, mal).ekle(V, [uv[0], uv[1], uv[2]], [(0, 1, 2)])


def main():
    t0 = time.time()
    o = kok.osm()
    geo = pickle.load(open(os.path.join(kok.ISLEM, "yollar_geo.pkl"), "rb"))
    koridor, guz = geo["koridor"], geo["guzergah"]
    binalar = bina_listesi(o, koridor)
    print(f"  {len(binalar)} bina ayak izi ({time.time() - t0:.0f} sn)")
    minareler = []
    for w in o["ways"].values():
        if w["t"].get("tower:type") == "minaret":
            ns = [o["xy"][n] for n in w["n"] if n in o["xy"]]
            if len(ns) >= 3:
                p = Polygon(ns).centroid
                minareler.append((p.x, p.y))
    for nid, t in o["tagged_nodes"].items():
        if t.get("tower:type") == "minaret" and nid in o["xy"]:
            minareler.append(o["xy"][nid])
    u = Uretici()
    kayit = []
    minare_binalari = set()
    for wid, pg, t in binalar:
        if t.get("tower:type") == "minaret":
            continue                         # minare ayrı bina olarak çizilmez (camiyle birlikte)
        c = pg.representative_point()
        hat, s = kok.hat_s(c.x, c.y)
        if hat == "408_atakule":
            s = -1.0 - s            # Uretici.bina: s < 0 → Atakule bölgeleri (bolge_sec)
        dg = guz.distance(pg)
        try:
            u.bina(wid, pg, t, s, dg, minareler)
        except Exception as e:      # bozuk geometri: atla, say
            print("   atlandı", wid, e)
            continue
        kayit.append({"id": wid, "taban": pg, "tur": t.get("building")})
    print(f"  binalar: {u.sayac} ({time.time() - t0:.0f} sn)")
    klasor = os.path.join(kok.ISLEM, "parcalar")
    os.makedirs(klasor, exist_ok=True)
    for f in os.listdir(klasor):
        if f.startswith("bina_"):
            os.remove(os.path.join(klasor, f))
    toplam = {}
    for (i, j), mals in u.pc.p.items():
        cikti = {}
        for mal, ag in mals.items():
            dz = ag.dizi()
            if dz is not None:
                cikti[mal] = dz
                toplam[mal] = toplam.get(mal, 0) + len(dz[2])
        pickle.dump(cikti, open(os.path.join(klasor, f"bina_{i}_{j}.pkl"), "wb"))
    pickle.dump(kayit, open(os.path.join(kok.ISLEM, "binalar.pkl"), "wb"))
    print(f"{len(u.pc.p)} parça; üçgen: " + ", ".join(f"{k} {v}" for k, v in sorted(toplam.items())) +
          f" ({time.time() - t0:.0f} sn)")


if __name__ == "__main__":
    main()
