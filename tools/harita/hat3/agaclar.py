"""
A6 — Ağaçlar: kartlı (alfa kesmeli) düşük üçgenli ağaçlar, parça başına birleşik ağ.

Türler (Ankara): çınar (bulvar, park), akasya (sokak, bina arası), kavak (sıra), karaçam (park, yamaç).
Ağaç başına ~70 üçgen: 6 kenarlı gövde + çapraz yaprak kartları (iki yüzlü). Malzemeler:
    H3_Govde@Agaclar (agac_govde.png), H3_Yaprak@Agaclar (agac_yaprak.png, alfa kesme)
Yerleşim kaynakları:
    1. OSM natural=tree düğümleri ve natural=tree_row çizgileri (8 m'de bir)
    2. Park/yeşil alanlar (güzergâhtan PARK_MESAFE m içinde, yoğunluk alana göre)
    3. Refüjler (≥ 2 m geniş, 14 m'de bir)
    4. Kaldırım kenarı (≥ 3 m kaldırım, 12 m'de bir, kavşak/durak/yaya geçidi yakını hariç)
    5. Bina araları (güzergâhtan 150 m içinde boş zemin, seyrek)
Çıktı: <ISLEM>/parcalar/agac_<i>_<j>.pkl, dokular Maps/Hat3/Textures
Kullanım: python -m hat3.agaclar
"""
import math
import os
import pickle
import time

import numpy as np
import shapely
from PIL import Image, ImageDraw, ImageFilter
from shapely.geometry import LineString, Point, Polygon
from shapely.strtree import STRtree

from . import kok
from .binalar import TEXTURES, hash01
from .mesh import Parcalar, anahtar, cokgenler
from .yuzeyler import SonArazi

PARK_MESAFE = 260.0
BAHCE_MESAFE = 150.0

# yaprak atlası 2×2: 0 çınar, 1 akasya, 2 kavak, 3 çam
TURLER = {
    # tür: (gövde boyu, gövde yarıçapı, taç merkezi, taç yarıçapı (yatay, dikey), kart sayısı, yaprak hücresi)
    "cinar": (3.2, 0.22, 7.5, (4.5, 3.6), 7, 0),
    "akasya": (2.4, 0.15, 5.0, (3.0, 2.4), 6, 1),
    "kavak": (2.0, 0.18, 9.0, (1.6, 6.5), 6, 2),
    "cam": (2.2, 0.2, 7.0, (3.0, 4.6), 6, 3),
}


def doku_yaprak(out):
    rng = np.random.default_rng(11)
    n = 512
    im = Image.new("RGBA", (2 * n, 2 * n), (0, 0, 0, 0))
    renkler = {0: [(58, 92, 40), (74, 112, 48), (92, 128, 56)], 1: [(70, 104, 44), (88, 124, 52), (110, 138, 60)],
               2: [(62, 96, 44), (80, 116, 50), (100, 134, 58)], 3: [(32, 58, 36), (42, 72, 44), (54, 86, 50)]}
    for h in range(4):
        hc = Image.new("RGBA", (n, n), (0, 0, 0, 0))
        dr = ImageDraw.Draw(hc)
        if h == 3:      # çam: koni biçimli iğne kümeleri
            for k in range(900):
                y = rng.uniform(0.05, 0.95)
                gen = 0.45 * y + 0.05
                x = 0.5 + rng.uniform(-gen, gen)
                r = rng.uniform(6, 16)
                c = renkler[3][rng.integers(3)]
                dr.ellipse((x * n - r, y * n - r * 0.6, x * n + r, y * n + r * 0.6), fill=(*c, 255))
        else:
            for k in range(1400):
                a = rng.uniform(0, 2 * math.pi)
                rr = math.sqrt(rng.uniform(0, 1)) * 0.46
                if h == 2:
                    x, y = 0.5 + rr * math.cos(a) * 0.45, 0.5 + rr * math.sin(a) * 1.05
                else:
                    x, y = 0.5 + rr * math.cos(a), 0.5 + rr * math.sin(a) * 0.85
                if not (0.02 < x < 0.98 and 0.02 < y < 0.98):
                    continue
                r = rng.uniform(5, 13) if h != 1 else rng.uniform(4, 9)
                c = renkler[h][rng.integers(3)]
                # alt kısım gölgeli
                g = 0.75 + 0.25 * (1 - y)
                c = tuple(int(v * g) for v in c)
                dr.ellipse((x * n - r, y * n - r, x * n + r, y * n + r), fill=(*c, 255))
        im.paste(hc, ((h % 2) * n, (h // 2) * n))
    im.save(os.path.join(out, "agac_yaprak.png"))
    # gövde kabuğu
    a = np.zeros((256, 256, 3))
    a[:] = (86, 72, 58)
    a += rng.standard_normal((256, 256, 1)) * 8
    for x in range(0, 256, 9):
        a[:, x:x + 2] *= 0.7
    Image.fromarray(a.clip(0, 255).astype(np.uint8)).save(os.path.join(out, "agac_govde.png"))


def yaprak_uv(h, u, v):
    eps = 2 / 1024
    c, r = h % 2, h // 2
    return (c / 2 + eps + (0.5 - 2 * eps) * u, 1 - (r + 1) / 2 + eps + (0.5 - 2 * eps) * v)


def sablon(tur):
    """Ağaç şablonu: {'H3_Govde@Agaclar': (V, UV, F), 'H3_Yaprak@Agaclar': (V, UV, F)} (kökte, y=0)."""
    gb, gr, tm, (tr, tv), kart, h = TURLER[tur]
    # gövde: 6 kenar, taç merkezine kadar incelir
    n = 6
    V, UV, F = [], [], []
    for k, (y, r) in enumerate([(0.0, gr), (gb, gr * 0.75), (tm, gr * 0.35)]):
        for i in range(n + 1):
            a = 2 * math.pi * i / n
            V.append((r * math.cos(a), y, r * math.sin(a)))
            UV.append((i / n, y / 3))
    for k in range(2):
        for i in range(n):
            a = k * (n + 1) + i
            F += [(a, a + 1, a + n + 2), (a, a + n + 2, a + n + 1)]
    govde = (np.array(V), np.array(UV), np.array(F))
    # yaprak kartları: dikey çapraz kartlar + yatay kart; iki yüzlü
    V, UV, F = [], [], []
    rng = np.random.default_rng(hash(tur) % 2 ** 31)
    for k in range(kart):
        if k == kart - 1 and tur != "kavak":
            # yatay (üstten görünüm)
            cy = tm + tv * 0.25
            P = [(-tr, cy, -tr), (tr, cy, -tr), (tr, cy, tr), (-tr, cy, tr)]
        else:
            a = math.pi * k / (kart - 1) + rng.uniform(-0.2, 0.2)
            dx, dz = math.cos(a) * tr, math.sin(a) * tr
            ox, oz = rng.uniform(-0.4, 0.4), rng.uniform(-0.4, 0.4)
            y0, y1 = tm - tv, tm + tv
            P = [(-dx + ox, y0, -dz + oz), (dx + ox, y0, dz + oz), (dx + ox, y1, dz + oz), (-dx + ox, y1, -dz + oz)]
        b = len(V)
        V += P
        UV += [yaprak_uv(h, 0, 0), yaprak_uv(h, 1, 0), yaprak_uv(h, 1, 1), yaprak_uv(h, 0, 1)]
        F += [(b, b + 1, b + 2), (b, b + 2, b + 3), (b, b + 2, b + 1), (b, b + 3, b + 2)]
    yaprak = (np.array(V), np.array(UV), np.array(F))
    return {"H3_Govde@Agaclar": govde, "H3_Yaprak@Agaclar": yaprak}


def main():
    t0 = time.time()
    doku_yaprak(TEXTURES)
    o = kok.osm()
    geo = pickle.load(open(os.path.join(kok.ISLEM, "yollar_geo.pkl"), "rb"))
    A, S, refuj, koridor, guz = geo["A"], geo["S"], geo["refuj"], geo["koridor"], geo["guzergah"]
    binalar = pickle.load(open(os.path.join(kok.ISLEM, "binalar.pkl"), "rb"))
    bina_agac = STRtree([b["taban"].buffer(1.5) for b in binalar])
    ar = SonArazi()
    shapely.prepare(A)
    shapely.prepare(S)
    durak = [Point(kok.yerel(dz["lat"], dz["lon"])) for dz in kok.guzergah()["duraklar"]]
    kav = [Point(x, z).buffer(r) for x, z, r in geo["kavsaklar"]]
    yasak = shapely.union_all(durak_b := [p.buffer(28) for p in durak] + kav)
    shapely.prepare(yasak)
    rng = np.random.default_rng(408)
    noktalar = []      # (x, z, tür, ölçek)

    def bos_mu(x, z, yol_payi=True):
        p = Point(x, z)
        if yol_payi and (shapely.contains_xy(A, x, z) or shapely.contains_xy(S, x, z)):
            return False
        return len(bina_agac.query(p, predicate="intersects")) == 0

    # 1. OSM ağaçları
    for nid, t in o["tagged_nodes"].items():
        if t.get("natural") == "tree" and nid in o["xy"]:
            x, z = o["xy"][nid]
            if guz.distance(Point(x, z)) < 350 and not shapely.contains_xy(A, x, z):
                noktalar.append((x, z, "cinar" if hash01(nid) < 0.5 else "akasya"))
    for w in o["ways"].values():
        if w["t"].get("natural") == "tree_row":
            ns = [o["xy"][n] for n in w["n"] if n in o["xy"]]
            if len(ns) < 2:
                continue
            L = LineString(ns)
            if L.distance(guz) > 350:
                continue
            for s in np.arange(4, L.length, 8.0):
                p = L.interpolate(s)
                if not shapely.contains_xy(A, p.x, p.y):
                    noktalar.append((p.x, p.y, "kavak" if hash01(p.x, p.y) < 0.3 else "cinar"))
    n1 = len(noktalar)
    # 2. parklar ve yeşil alanlar
    for wid, w in o["ways"].items():
        t = w["t"]
        yog = None
        if t.get("leisure") in ("park", "garden"):
            yog = 140.0
        elif t.get("landuse") == "cemetery":
            yog = 180.0
        elif t.get("natural") in ("wood",) or t.get("landuse") == "forest":
            yog = 60.0
        elif t.get("landuse") in ("grass", "recreation_ground", "village_green") or t.get("natural") == "scrub":
            yog = 400.0
        if yog is None:
            continue
        ns = [o["xy"][n] for n in w["n"] if n in o["xy"]]
        if len(ns) < 4:
            continue
        pg = Polygon(ns).buffer(0)
        bolge = pg.intersection(guz.buffer(PARK_MESAFE))
        if bolge.is_empty:
            continue
        adet = int(bolge.area / yog)
        x0, z0, x1, z1 = bolge.bounds
        shapely.prepare(bolge)
        k = 0
        deneme = 0
        while k < adet and deneme < adet * 4:
            deneme += 1
            x, z = rng.uniform(x0, x1), rng.uniform(z0, z1)
            if shapely.contains_xy(bolge, x, z) and bos_mu(x, z):
                u = rng.random()
                noktalar.append((x, z, "cam" if u < 0.4 else ("cinar" if u < 0.75 else "akasya")))
                k += 1
    n2 = len(noktalar)
    # 3. refüjler
    for rf in refuj:
        en = 2 * rf.area / max(rf.length, 1)
        if en < 2.0:
            continue
        # refüj ekseni ≈ en uzun köşegen boyunca: dikdörtgen ekseni
        r = rf.minimum_rotated_rectangle
        c = np.asarray(r.exterior.coords)[:4]
        e0, e1 = np.linalg.norm(c[1] - c[0]), np.linalg.norm(c[2] - c[1])
        a, b = ((c[0] + c[3]) / 2, (c[1] + c[2]) / 2) if e0 > e1 else ((c[0] + c[1]) / 2, (c[3] + c[2]) / 2)
        L = LineString([a, b])
        tur = "cinar" if en >= 3 else "akasya"
        for s in np.arange(7, L.length - 3, 14.0):
            p = L.interpolate(s)
            if shapely.contains_xy(rf, p.x, p.y) and not shapely.contains_xy(yasak, p.x, p.y):
                noktalar.append((p.x, p.y, tur))
    n3 = len(noktalar)
    # 4. kaldırım kenarı: asfalt sınırından 1,1 m dışarı, kaldırım ≥ 3 m ise
    for pg in cokgenler(A.intersection(guz.buffer(300))):
        for halka in [pg.exterior] + list(pg.interiors):
            L = LineString(halka.coords)
            for s in np.arange(6, L.length, 12.0):
                p, q = L.interpolate(s), L.interpolate(min(s + 1, L.length))
                tx, tz = q.x - p.x, q.y - p.y
                ln = math.hypot(tx, tz) or 1
                nx, nz = tz / ln, -tx / ln
                for yon in (1, -1):
                    x, z = p.x + nx * 1.1 * yon, p.y + nz * 1.1 * yon
                    x3, z3 = p.x + nx * 3.0 * yon, p.y + nz * 3.0 * yon
                    if shapely.contains_xy(S, x, z) and shapely.contains_xy(S, x3, z3) and \
                            not shapely.contains_xy(yasak, x, z) and hash01(int(x), int(z), "kal") < 0.55:
                        noktalar.append((x, z, "akasya" if hash01(int(x), int(z)) < 0.7 else "cinar"))
                        break
    n4 = len(noktalar)
    # 5. bina araları
    bolge = guz.buffer(BAHCE_MESAFE)
    x0, z0, x1, z1 = bolge.bounds
    shapely.prepare(bolge)
    adet = int(bolge.area / 350)
    for _ in range(adet):
        x, z = rng.uniform(x0, x1), rng.uniform(z0, z1)
        if shapely.contains_xy(bolge, x, z) and bos_mu(x, z) and \
                ar.yol_mesafesi(x, z)[0] > 4.0:
            u = rng.random()
            noktalar.append((x, z, "akasya" if u < 0.5 else ("kavak" if u < 0.7 else ("cam" if u < 0.85 else "cinar"))))
    print(f"  ağaç: OSM {n1}, park {n2 - n1}, refüj {n3 - n2}, kaldırım {n4 - n3}, bina arası {len(noktalar) - n4}"
          f" ({time.time() - t0:.0f} sn)")

    sablonlar = {t: sablon(t) for t in TURLER}
    pc = Parcalar()
    P = np.array([(x, z) for x, z, _ in noktalar])
    Y = ar.y(P[:, 0], P[:, 1])
    for (x, z, tur), y in zip(noktalar, Y):
        u = hash01(round(x, 1), round(z, 1))
        olc = 0.75 + 0.5 * u
        aci = u * 2 * math.pi * 7
        ca, sa = math.cos(aci), math.sin(aci)
        k = anahtar(x, z)
        for mal, (V, UV, F) in sablonlar[tur].items():
            Vr = np.stack([V[:, 0] * ca - V[:, 2] * sa, V[:, 1], V[:, 0] * sa + V[:, 2] * ca], 1) * olc
            Vr += (x, y - 0.1, z)
            pc.ag(k, mal).ekle(Vr, UV, F)
    klasor = os.path.join(kok.ISLEM, "parcalar")
    for f in os.listdir(klasor):
        if f.startswith("agac_"):
            os.remove(os.path.join(klasor, f))
    toplam = 0
    for (i, j), mals in pc.p.items():
        cikti = {m: a.dizi() for m, a in mals.items()}
        toplam += sum(len(v[2]) for v in cikti.values())
        pickle.dump(cikti, open(os.path.join(klasor, f"agac_{i}_{j}.pkl"), "wb"))
    pickle.dump(noktalar, open(os.path.join(kok.ISLEM, "agaclar.pkl"), "wb"))
    print(f"{len(noktalar)} ağaç, {len(pc.p)} parça, {toplam} üçgen ({time.time() - t0:.0f} sn)")


if __name__ == "__main__":
    main()
