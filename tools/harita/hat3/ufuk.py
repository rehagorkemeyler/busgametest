"""
A5 — Harita sınırlarını gizleme: koridorun dışında UFUK m'ye kadar "uzak şehir" halkası.

- Arazi: doğal DEM (yumuşatılmış), 30 m ızgara; koridor sınırına 10 m bindirme (çatlak kalmasın).
- Binalar: koridora girmeyen OSM ayak izleri basit kutular (kenar başına tek dörtgen, U hücresi; düz çatı); OSM
  karolarının dışı (veri yok) prosedürel apartman blokları (Ankara yamaçlarında olduğu gibi sık, 4–8 kat).
- Çarpışma yok, gölge yok (Unity'de Ufuk grubu). Gündüz sisi 250–1500 m: halkanın dışı sise karışır, ufuk çizgisi
  binalardan oluşur, harita kenarı görünmez.
Malzemeler: H3_Zemin@Ufuk, H3_Cephe@Ufuk, H3_Cati@Ufuk.
Çıktı: <ISLEM>/parcalar/ufuk_<i>_<j>.pkl
Kullanım: python -m hat3.ufuk
"""
import math
import os
import pickle
import time

import numpy as np
import shapely
from shapely.geometry import Point, Polygon, box
from shapely.strtree import STRtree

from . import kok
from .arazi import Arazi
from .binalar import BOLGELER, KAT, bolge_sec, Atlas, bina_listesi, cati_uv, hash01, kat_sayisi, stil_sec
from .mesh import Parcalar, ucgenle

UFUK = 2000.0
PARCA_UFUK = 1000.0      # uzak halka büyük parçalarda (az nesne)


def anahtar(x, z):
    return (int(math.floor(x / PARCA_UFUK)), int(math.floor(z / PARCA_UFUK)))
HUCRE = 30.0
OSM_KAPSAM = (39.915, 39.985, 32.840, 32.910)      # osm_indir.sh karoları


def main():
    t0 = time.time()
    d = np.load(os.path.join(kok.ISLEM, "arazi.npz"))
    ar = Arazi(d["H"], float(d["x0"]), float(d["z0"]), float(d["hucre"]))
    geo = pickle.load(open(os.path.join(kok.ISLEM, "yollar_geo.pkl"), "rb"))
    koridor, guz = geo["koridor"], geo["guzergah"]
    # DEM kapsamı (kenarda 50 m pay)
    H = ar.H
    dem_kutu = box(ar.x0 + 50, ar.z0 + 50, ar.x0 + (H.shape[1] - 1) * ar.hucre - 50, ar.z0 + (H.shape[0] - 1) * ar.hucre - 50)
    halka = guz.buffer(UFUK, quad_segs=8).intersection(dem_kutu).difference(koridor.buffer(-10))
    shapely.prepare(halka)
    la0, la1, lo0, lo1 = OSM_KAPSAM
    xa, za = kok.yerel(la0, lo0)
    xb, zb = kok.yerel(la1, lo1)
    osm_kutu = box(xa + 30, za + 30, xb - 30, zb - 30)
    pc = Parcalar()
    A = Atlas()

    # --- arazi
    x0, z0, x1, z1 = halka.bounds
    nx, nz = int((x1 - x0) / HUCRE) + 2, int((z1 - z0) / HUCRE) + 2
    gx, gz = np.meshgrid(x0 + np.arange(nx) * HUCRE, z0 + np.arange(nz) * HUCRE)
    gy = ar.y(gx.ravel(), gz.ravel()).reshape(gx.shape) - 0.4
    kor_ic = koridor.buffer(-12)
    shapely.prepare(kor_ic)
    for j in range(nz - 1):
        for i in range(nx - 1):
            cx, cz = gx[j, i] + HUCRE / 2, gz[j, i] + HUCRE / 2
            if not shapely.intersects(halka, box(gx[j, i], gz[j, i], gx[j, i] + HUCRE, gz[j, i] + HUCRE)):
                continue
            if shapely.contains_xy(kor_ic, cx, cz) and shapely.contains_xy(kor_ic, gx[j, i], gz[j, i]) and \
                    shapely.contains_xy(kor_ic, gx[j, i] + HUCRE, gz[j, i] + HUCRE):
                continue
            V = [(gx[j, i], gy[j, i], gz[j, i]), (gx[j, i + 1], gy[j, i + 1], gz[j, i + 1]),
                 (gx[j + 1, i + 1], gy[j + 1, i + 1], gz[j + 1, i + 1]), (gx[j + 1, i], gy[j + 1, i], gz[j + 1, i])]
            UV = [(v[0] * 0.03, v[2] * 0.03) for v in V]
            pc.ag(anahtar(cx, cz), "H3_Zemin@Ufuk").ekle(V, UV, [(0, 1, 2), (0, 2, 3)])
    print(f"  arazi ({time.time() - t0:.0f} sn)")

    def kutu_bina(pg, katlar, stil, renk, k):
        y0 = float(ar.y(*np.asarray(pg.exterior.coords).T).min()) - 0.3
        ust = y0 + 1.2 + katlar * KAT
        u = A.j["stiller"][stil][renk]["U"]
        c = np.asarray(pg.exterior.coords)
        for a, b in zip(c[:-1], c[1:]):
            L = float(np.linalg.norm(b - a))
            if L < 0.5:
                continue
            # uzakta tek dörtgen: U hücresi (4 bölme × 4 kat) kenar boyunca gerilir
            V = [(a[0], y0, a[1]), (b[0], y0, b[1]), (b[0], ust, b[1]), (a[0], ust, a[1])]
            pc.ag(k, "H3_Cephe@Ufuk").ekle(V, A.uv(u, 0, 1, max(0.0, 1 - katlar / 4), 1), [(0, 1, 2), (0, 2, 3)])
        if len(c) >= 4:
            xz2, tri = ucgenle(pg)
            if len(tri):
                Vt = np.stack([xz2[:, 0], np.full(len(xz2), ust), xz2[:, 1]], 1)
                pc.ag(k, "H3_Cati@Ufuk").ekle(Vt, cati_uv(1, np.full(len(xz2), 0.5), np.full(len(xz2), 0.5)), tri)

    # --- OSM binaları (koridor dışı)
    o = kok.osm()
    sayi = 0
    for wid, pg, t in bina_listesi(o, halka):
        if koridor.contains(pg.representative_point()):
            continue
        if pg.area < 30:
            continue
        pg = pg.minimum_rotated_rectangle if pg.area < 400 else pg.simplify(2.0)
        pg = shapely.geometry.polygon.orient(pg, 1.0) if isinstance(pg, Polygon) else None
        if pg is None or pg.is_empty:
            continue
        c = pg.representative_point()
        bolge = bolge_sec(*kok.hat_s(c.x, c.y))
        tur = t.get("building", "yes")
        katlar = kat_sayisi(wid, t, pg, bolge, tur)
        stil = stil_sec(wid, t, bolge, tur)
        renk = int(hash01(wid, "renk") * len(A.j["stiller"][stil]))
        kutu_bina(pg, katlar, stil, renk, anahtar(c.x, c.y))
        sayi += 1
    print(f"  OSM uzak binalar {sayi} ({time.time() - t0:.0f} sn)")

    # --- prosedürel bloklar (40 m ızgara): OSM kapsamı dışı ve OSM'de bina işaretlenmemiş yerleşim (yamaçlar);
    # park/mezarlık/ana yol üstüne konmaz
    engel = [Polygon(np.asarray(pg.exterior.coords)).buffer(25) for _, pg, _ in bina_listesi(o, halka)]
    for w in o["ways"].values():
        t = w["t"]
        ns = [o["xy"][n] for n in w["n"] if n in o["xy"]]
        if len(ns) < 2:
            continue
        if t.get("highway") in ("motorway", "trunk", "primary", "secondary", "trunk_link", "primary_link") or "railway" in t:
            engel.append(shapely.LineString(ns).buffer(18))
        elif len(ns) >= 4 and (t.get("leisure") in ("park", "garden", "pitch", "stadium") or t.get("landuse") in
                               ("cemetery", "grass", "forest", "recreation_ground", "military", "railway") or
                               t.get("natural") in ("wood", "water", "scrub") or t.get("amenity") in ("school", "hospital", "university")):
            engel.append(Polygon(ns).buffer(0))
    engel_u = shapely.union_all(engel)
    dis = halka.difference(engel_u)
    shapely.prepare(dis)
    rng = np.random.default_rng(1923)
    x0, z0, x1, z1 = dis.bounds
    ek = 0
    for zz in np.arange(z0, z1, 40.0):
        for xx in np.arange(x0, x1, 40.0):
            if rng.random() > (0.6 if not shapely.contains_xy(osm_kutu, xx, zz) else 0.45):
                continue
            cx, cz = xx + rng.uniform(5, 35), zz + rng.uniform(5, 35)
            if not shapely.contains_xy(dis, cx, cz):
                continue
            w, dd = rng.uniform(12, 24), rng.uniform(10, 18)
            a = rng.uniform(0, math.pi)
            ca, sa = math.cos(a), math.sin(a)
            pts = [(cx + ca * px - sa * pz, cz + sa * px + ca * pz) for px, pz in
                   ((-w / 2, -dd / 2), (w / 2, -dd / 2), (w / 2, dd / 2), (-w / 2, dd / 2))]
            pg = shapely.geometry.polygon.orient(Polygon(pts), 1.0)
            stil = ["mantolama", "bulvar60", "tugla", "mantolama"][int(rng.integers(4))]
            renk = int(rng.integers(len(A.j["stiller"][stil])))
            kutu_bina(pg, int(rng.integers(4, 9)), stil, renk, anahtar(cx, cz))
            ek += 1
    print(f"  prosedürel bloklar {ek} ({time.time() - t0:.0f} sn)")

    klasor = os.path.join(kok.ISLEM, "parcalar")
    for f in os.listdir(klasor):
        if f.startswith("ufuk_"):
            os.remove(os.path.join(klasor, f))
    toplam = {}
    for (i, j), mals in pc.p.items():
        cikti = {m: a.dizi() for m, a in mals.items()}
        for m, v in cikti.items():
            toplam[m] = toplam.get(m, 0) + len(v[2])
        pickle.dump(cikti, open(os.path.join(klasor, f"ufuk_{i}_{j}.pkl"), "wb"))
    print(f"{len(pc.p)} parça; üçgen: {toplam} ({time.time() - t0:.0f} sn)")


if __name__ == "__main__":
    main()
