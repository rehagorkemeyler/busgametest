"""
A1 — Arazi ve yol yükseklikleri.

1. DEM (terrarium z15, ~30 m kaynak) → 10 m ızgara, Gauss yumuşatma (σ 20 m; SRTM yüzey modeli, bina/ağaç gürültüsü).
2. Yol ağı yükseklikleri: araç yolları ve demiryolları ≤ 8 m aralıkla sıklaştırılır; her düğüm için hedef = arazi + katman
   farkı, komşular arası yay (graf Laplace) ile birlikte en küçük kareler çözümü → kavşaklarda yollar aynı yükseklikte,
   köprü/alt geçit rampaları kendiliğinden (~%6–9).
   Katman kuralı: oyuncu güzergâhı her yerde arazide kalır (OSM'de köprü/tünel de olsa). Güzergâhı kesen başka bir yol/hat
   aradaki katman farkı kadar (6,5 m/katman) yukarıda ya da aşağıda: Sıhhiye'de demiryolu ve Celal Bayar Bulvarı köprüleri,
   Aydınlıkevler'de güzergâhın üstünden geçen yol, Dışkapı köprüsünün altındaki yol çukurda. Güzergâhtan uzak köprü ve
   tüneller kendi katmanlarıyla (×6,5 m). Tünel içindeki (yer altı) yollar ve raylar çizilmez.

Çıktı: <ISLEM>/arazi.npz
    H (ny, nx) yumuşatılmış arazi, x0, z0, hucre
    dugum_xz (n, 2), dugum_y (n,), dugum_fark (n,) — sıklaştırılmış yol ağı düğümleri (fark: katman hedefi, 0 = zemin)
    ve <ISLEM>/yollar.json: [{id, sinif, t (etiketler), d (düğüm indisleri), guzergah (bool), katman_farki}]
Rakımlar Kızılay durağının yumuşatılmış arazi rakımına göre (Y0).

Kullanım: python -m hat3.arazi   (tools/harita içinden)
"""
import glob
import math
import os

import numpy as np
from PIL import Image
from scipy import ndimage, sparse
from scipy.sparse.linalg import spsolve
from shapely.geometry import LineString, Point
from shapely.strtree import STRtree

from . import kok

HUCRE = 10.0
SIGMA = 20.0
KATMAN = 6.5          # m / katman (köprü altı açıklığı + tabliye)
SIKLIK = 8.0          # m, yol düğümleri arası en çok
LAMBDA = 300.0        # yay katsayısı (graf yumuşatma uzunluğu ~40 m)
DEM_Z = 15
RAYLAR = ("rail", "light_rail", "tram", "narrow_gauge")


def dem_mozaik(klasor=None):
    """Terrarium karolarını tek diziye birleştirir: (dizi, ilk karo x, ilk karo y)."""
    klasor = klasor or os.path.join(kok.VERI, "dem")
    karolar = {}
    for f in glob.glob(os.path.join(klasor, f"{DEM_Z}_*_*.png")):
        _, x, y = os.path.basename(f)[:-4].split("_")
        a = np.asarray(Image.open(f).convert("RGB"), dtype=np.float64)
        karolar[(int(x), int(y))] = a[:, :, 0] * 256 + a[:, :, 1] + a[:, :, 2] / 256 - 32768
    xs = sorted({k[0] for k in karolar})
    ys = sorted({k[1] for k in karolar})
    m = np.zeros((len(ys) * 256, len(xs) * 256))
    for (x, y), a in karolar.items():
        m[(y - ys[0]) * 256:(y - ys[0] + 1) * 256, (x - xs[0]) * 256:(x - xs[0] + 1) * 256] = a
    return m, xs[0], ys[0]


def dem_ornekle(m, kx0, ky0, lat, lon):
    n = 2 ** DEM_Z * 256
    px = (lon + 180) / 360 * n - kx0 * 256 - 0.5
    py = (1 - np.arcsinh(np.tan(np.radians(lat))) / np.pi) / 2 * n - ky0 * 256 - 0.5
    return ndimage.map_coordinates(m, [py, px], order=1, mode="nearest")


class Arazi:
    """Yumuşatılmış arazi ızgarası; y(x, z) çift doğrusal."""

    def __init__(self, H, x0, z0, hucre):
        self.H, self.x0, self.z0, self.hucre = H, x0, z0, hucre

    def y(self, x, z):
        tek = np.ndim(x) == 0
        x = np.atleast_1d(np.asarray(x, dtype=np.float64))
        z = np.atleast_1d(np.asarray(z, dtype=np.float64))
        v = ndimage.map_coordinates(self.H, [(z - self.z0) / self.hucre, (x - self.x0) / self.hucre],
                                    order=1, mode="nearest")
        return float(v[0]) if tek else v

    @staticmethod
    def yukle():
        d = np.load(os.path.join(kok.ISLEM, "arazi.npz"))
        return Arazi(d["H"], float(d["x0"]), float(d["z0"]), float(d["hucre"]))


def arazi_izgarasi():
    m, kx0, ky0 = dem_mozaik()
    # DEM'in kapsadığı alan (kenarda yarım hücre pay)
    n = 2 ** DEM_Z
    lon0 = kx0 / n * 360 - 180
    lon1 = (kx0 + m.shape[1] / 256) / n * 360 - 180
    lat0 = math.degrees(math.atan(math.sinh(math.pi * (1 - 2 * (ky0 + m.shape[0] / 256) / n))))
    lat1 = math.degrees(math.atan(math.sinh(math.pi * (1 - 2 * ky0 / n))))
    xa, za = kok.yerel(lat0, lon0)
    xb, zb = kok.yerel(lat1, lon1)
    x0 = math.ceil(xa / HUCRE) * HUCRE + HUCRE
    z0 = math.ceil(za / HUCRE) * HUCRE + HUCRE
    nx = int((xb - x0) / HUCRE) - 1
    nz = int((zb - z0) / HUCRE) - 1
    gx, gz = np.meshgrid(x0 + np.arange(nx) * HUCRE, z0 + np.arange(nz) * HUCRE)
    lat, lon = kok.cografi(gx, gz)
    H = dem_ornekle(m, kx0, ky0, lat.ravel(), lon.ravel()).reshape(gz.shape)
    H = ndimage.gaussian_filter(H, SIGMA / HUCRE, mode="nearest")
    return Arazi(H, x0, z0, HUCRE)


def katman(t):
    try:
        return int(float(t.get("layer", "0")))
    except ValueError:
        return 0


def yol_agi(arazi):
    o = kok.osm()
    xy = o["xy"]
    g = kok.guzergah()
    guz_id = set(g["osm_yollar"])
    guz_hat = LineString([kok.yerel(*p) for p in g["yol"]])
    secili = []
    for wid, w in o["ways"].items():
        t = w["t"]
        h = t.get("highway")
        r = t.get("railway")
        if h in kok.ARAC_YOLU and t.get("area") != "yes":
            sinif = h
        elif r in RAYLAR:
            sinif = "ray"
        else:
            continue
        if wid not in guz_id and (t.get("tunnel") in ("yes", "building_passage", "culvert") or t.get("location") == "underground"):
            if katman(t) < 0 or t.get("tunnel") == "yes":
                continue                        # yer altında: çizilmez
        ns = [n for n in w["n"] if n in xy]
        if len(ns) < 2:
            continue
        secili.append((wid, sinif, t, ns))
    # güzergâh yollarının katmanları (kesişen yolun farkı için)
    guz_geo = [(LineString([xy[n] for n in ns]), katman(t)) for wid, s, t, ns in secili if wid in guz_id]
    agac = STRtree([gg for gg, _ in guz_geo])

    # güzergâhın köprü/tünel parçalarını (paylaşılan düğüm olmadan) kesen yollar: kesişim noktası ve katman farkı.
    # OSM'de alttaki/üstteki yol çoğu zaman katmansız (Etlik Cd. Dışkapı köprüsünün altında, Turgut Özal 2 Blv.
    # Aydınlıkevler'de güzergâhın üstünde): kesişimde fark tam, 25 m'den sonra 120 m'de sıfıra iner (rampa ~%5).
    kesisim = {}
    for wid, sinif, t, ns in secili:
        if wid not in guz_id or not (t.get("bridge") or t.get("tunnel")):
            continue
        Lr = katman(t)
        geo_r = LineString([xy[n] for n in ns])
        for wid2, s2, t2, ns2 in secili:
            if wid2 in guz_id or set(ns2) & set(ns):
                continue
            g2 = LineString([xy[n] for n in ns2])
            if g2.intersects(geo_r):
                x = g2.intersection(geo_r)
                pt = x if x.geom_type == "Point" else x.representative_point()
                kesisim.setdefault(wid2, []).append((np.array(pt.coords[0]), (katman(t2) - Lr) * KATMAN))

    def incelen(wid2, p):
        f, w = 0.0, 1.0
        for q, fk in kesisim.get(wid2, ()):
            d = float(np.linalg.norm(np.asarray(p) - q))
            k = 1.0 if d < 25 else max(0.0, 1 - (d - 25) / 120)
            if k > 0 and abs(fk * k) > abs(f):
                f, w = fk * k, 400.0 if d < 25 else 50.0
        return f, w

    # sıklaştırma: OSM düğümleri paylaşılır, aralara ara düğüm
    idx = {}
    pts = []
    hedef_fark = []          # düğüm başına katman farkı (m)
    agirlik = []
    kenarlar = []
    yollar = []

    def dugum(osm_id, p, fark, w):
        if osm_id is not None and osm_id in idx:
            i = idx[osm_id]
            # paylaşılan düğüm: en "zemin" olanı tercih (köprü ucu zemindeki yola bağlanır; iki köprü parçası arası yukarıda)
            if abs(fark) < abs(hedef_fark[i]):
                hedef_fark[i] = fark
                agirlik[i] = w
            return i
        pts.append(p)
        hedef_fark.append(fark)
        agirlik.append(w)
        i = len(pts) - 1
        if osm_id is not None:
            idx[osm_id] = i
        return i

    for wid, sinif, t, ns in secili:
        L = katman(t)
        guz = wid in guz_id
        if guz:
            fark = 0.0
        elif L != 0 or t.get("bridge") or t.get("tunnel"):
            geo = LineString([xy[n] for n in ns])
            fark = L * KATMAN
            if geo.distance(guz_hat) < 60:
                yakin = [guz_geo[i] for i in agac.query(geo.buffer(60))]
                if yakin:
                    Lr = min(yakin, key=lambda gl: gl[0].distance(geo))[1]
                    fark = (L - Lr) * KATMAN
        else:
            fark = 0.0
        # köprü/tünel etiketli kısım yerinde durur; yalnız katmanlı (rampa) yollar yumuşakça bağlanır
        w_kopru = 1.0 if fark == 0 else (400.0 if (t.get("bridge") or t.get("tunnel")) else 5.0)
        dlist = []
        for a, b in zip(ns, ns[1:]):
            pa, pb = np.array(xy[a]), np.array(xy[b])
            ia = dugum(a, tuple(pa), *((fark, w_kopru) if wid not in kesisim else incelen(wid, pa)))
            if not dlist:
                dlist.append(ia)
            L_ab = float(np.linalg.norm(pb - pa))
            k = max(1, int(math.ceil(L_ab / SIKLIK)))
            onceki = ia
            for j in range(1, k):
                p = pa + (pb - pa) * j / k
                ij = dugum(None, tuple(p), *((fark, w_kopru) if wid not in kesisim else incelen(wid, p)))
                kenarlar.append((onceki, ij, L_ab / k, guz))
                dlist.append(ij)
                onceki = ij
            ib = dugum(b, tuple(pb), *((fark, w_kopru) if wid not in kesisim else incelen(wid, pb)))
            kenarlar.append((onceki, ib, L_ab / k, guz))
            dlist.append(ib)
        if wid in kesisim:
            fark = max((fk for _, fk in kesisim[wid]), key=abs)
        yollar.append({"id": wid, "sinif": sinif, "t": t, "d": dlist, "guzergah": guz, "katman_farki": fark})

    P = np.array(pts)
    zemin = arazi.y(P[:, 0], P[:, 1])
    fark = np.array(hedef_fark)
    W = np.array(agirlik)
    n = len(P)
    rows, cols, vals = [], [], []
    for i, j, l, guz in kenarlar:
        c = LAMBDA * (3.0 if guz else 1.0) / max(l, 0.5)     # güzergâh daha yumuşak (sürüş)
        rows += [i, j, i, j]
        cols += [i, j, j, i]
        vals += [c, c, -c, -c]
    A = (sparse.coo_matrix((vals, (rows, cols)), shape=(n, n)).tocsr() + sparse.diags(W)).tocsc()
    y = spsolve(A, W * (zemin + fark))
    # 2. geçiş: güzergâhın yanındaki (≤ 40 m) katmanlı düğümlerin hedefi = en yakın güzergâh düğümü + fark
    guz_d = sorted({i for w in yollar if w["guzergah"] for i in w["d"]})
    agac_d = STRtree([Point(P[i]) for i in guz_d])
    duzelt = zemin.copy()
    for i in np.nonzero(fark)[0]:
        j = agac_d.nearest(Point(P[i]))
        if np.linalg.norm(P[i] - P[guz_d[j]]) < 40:
            duzelt[i] = y[guz_d[j]]
    y = spsolve(A, W * (duzelt + fark))
    return P, y, yollar, fark


def main():
    ar = arazi_izgarasi()
    P, y, yollar, fark = yol_agi(ar)
    Y0 = float(ar.y(0.0, 0.0))
    np.savez_compressed(os.path.join(kok.ISLEM, "arazi.npz"), H=ar.H - Y0, x0=ar.x0, z0=ar.z0, hucre=ar.hucre,
                        dugum_xz=P, dugum_y=y - Y0, dugum_fark=fark, Y0=Y0)
    kok.kaydet_json(yollar, os.path.join(kok.ISLEM, "yollar.json"))
    print(f"arazi {ar.H.shape} ({ar.H.shape[1] * HUCRE / 1000:.1f} × {ar.H.shape[0] * HUCRE / 1000:.1f} km), Kızılay {Y0:.1f} m;"
          f" yol ağı {len(yollar)} yol, {len(P)} düğüm")


if __name__ == "__main__":
    main()
