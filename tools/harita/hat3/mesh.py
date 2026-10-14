"""
Parça (chunk) başına ağ biriktirme ve çokgen yüzey üçgenleme.

Koordinatlar Unity: (x doğu, y yukarı, z kuzey). Parçalar PARCA m'lik kareler: anahtar (i, j) = (⌊x/PARCA⌋, ⌊z/PARCA⌋).
Her parça, malzeme adı → Ag (köşeler, uv, üçgenler). Yön kuralı: üstten bakınca (x sağa, z yukarı çizilmiş) saat yönünün
tersi = yukarı bakan yüz. Blender'a (-x, -z, y) olarak aktarılınca da yukarı bakar (180° döndürme yönü korur).
"""
import math

import mapbox_earcut as earcut
import numpy as np
import shapely
from shapely.geometry import MultiPolygon, Polygon, box

PARCA = 250.0


def anahtar(x, z):
    return (int(math.floor(x / PARCA)), int(math.floor(z / PARCA)))


class Ag:
    def __init__(self):
        self.v, self.uv, self.f = [], [], []
        self.n = 0

    def ekle(self, V, UV, F):
        """V (k,3), UV (k,2), F (m,3) yerel indislerle."""
        V = np.asarray(V, dtype=np.float32)
        if len(V) == 0:
            return
        self.v.append(V)
        self.uv.append(np.asarray(UV, dtype=np.float32))
        self.f.append(np.asarray(F, dtype=np.int32) + self.n)
        self.n += len(V)

    def dizi(self):
        if not self.v:
            return None
        return np.concatenate(self.v), np.concatenate(self.uv), np.concatenate(self.f)

    def ucgen_sayisi(self):
        return sum(len(f) for f in self.f)


class Parcalar:
    """{(i, j): {malzeme: Ag}}"""

    def __init__(self):
        self.p = {}

    def ag(self, k, malzeme):
        return self.p.setdefault(k, {}).setdefault(malzeme, Ag())

    def ekle_dunya(self, malzeme, V, UV, F):
        """Ağı ilk üçgeninin ağırlık merkezine göre bir parçaya ekler (küçük nesneler için)."""
        V = np.asarray(V)
        if len(V) == 0:
            return
        c = V.mean(axis=0)
        self.ag(anahtar(c[0], c[2]), malzeme).ekle(V, UV, F)


def cokgenler(g):
    if g is None or g.is_empty:
        return []
    if isinstance(g, Polygon):
        return [g]
    if isinstance(g, MultiPolygon):
        return list(g.geoms)
    return [p for p in getattr(g, "geoms", []) if isinstance(p, (Polygon, MultiPolygon))
            for p in cokgenler(p)]


def ucgenle(pg):
    """Delikli çokgen → (xz (k,2), üçgenler (m,3)); üstten bakınca saat yönünün tersi (yukarı bakan)."""
    halkalar = [np.asarray(pg.exterior.coords)[:-1]] + [np.asarray(h.coords)[:-1] for h in pg.interiors]
    halkalar = [h for h in halkalar if len(h) >= 3]
    if not halkalar:
        return np.zeros((0, 2)), np.zeros((0, 3), dtype=np.int32)
    xz = np.concatenate(halkalar)
    uclar = np.cumsum([len(h) for h in halkalar]).astype(np.uint32)
    idx = earcut.triangulate_float64(xz, uclar).reshape(-1, 3)
    if len(idx) == 0:
        return xz, np.zeros((0, 3), dtype=np.int32)
    # yön: (b-a)×(c-a) > 0 → saat yönünün tersi (yukarı)
    a, b, c = xz[idx[:, 0]], xz[idx[:, 1]], xz[idx[:, 2]]
    cap = (b[:, 0] - a[:, 0]) * (c[:, 1] - a[:, 1]) - (b[:, 1] - a[:, 1]) * (c[:, 0] - a[:, 0])
    ters = cap < 0
    idx[ters] = idx[ters][:, [0, 2, 1]]
    return xz, idx.astype(np.int32)


def parcala(g):
    """Geometriyi parça karelerine böler: {(i, j): geometri}."""
    out = {}
    for pg in cokgenler(g):
        x0, z0, x1, z1 = pg.bounds
        for i in range(math.floor(x0 / PARCA), math.ceil(x1 / PARCA)):
            for j in range(math.floor(z0 / PARCA), math.ceil(z1 / PARCA)):
                k = pg.intersection(box(i * PARCA, j * PARCA, (i + 1) * PARCA, (j + 1) * PARCA))
                if not k.is_empty:
                    out.setdefault((i, j), []).append(k)
    return out


def yuzey(parcalar, malzeme, g, yukseklik, hucre=10.0, uv_olcek=0.125, y_ek=0.0):
    """Çokgeni önce parça karelerine, sonra hücre ızgarasına böler (yükseklik değişimi için ara köşeler).
    yukseklik(x (n,), z (n,)) → y (n,)."""
    for k, liste in parcala(g).items():
        ag = parcalar.ag(k, malzeme)
        for gg in liste:
            for pg in cokgenler(gg):
                x0, z0, x1, z1 = pg.bounds
                i0, i1 = math.floor(x0 / hucre), math.ceil(x1 / hucre)
                j0, j1 = math.floor(z0 / hucre), math.ceil(z1 / hucre)
                kutular = np.array([box(i * hucre, j * hucre, (i + 1) * hucre, (j + 1) * hucre)
                                    for i in range(i0, i1) for j in range(j0, j1)], dtype=object)
                if len(kutular) == 0:
                    continue
                shapely.prepare(pg)
                ic = shapely.intersects(kutular, pg)
                for parca_g in shapely.intersection(kutular[ic], pg):
                    for p in cokgenler(parca_g):
                        if p.area < 1e-3:
                            continue
                        xz, tri = ucgenle(p)
                        if len(tri) == 0:
                            continue
                        y = yukseklik(xz[:, 0], xz[:, 1]) + y_ek
                        ag.ekle(np.stack([xz[:, 0], y, xz[:, 1]], axis=1), xz * uv_olcek, tri)


def serit(xz, y, sol, sag, y_ek=0.0, uv_v=None, uv_u=(0.0, 1.0)):
    """Eksen boyunca şerit (sola sol m, sağa sag m ofset). Döner V, UV, F (yukarı bakan)."""
    xz = np.asarray(xz, dtype=np.float64)
    n = len(xz)
    t = np.gradient(xz, axis=0)
    t /= np.maximum(np.linalg.norm(t, axis=1, keepdims=True), 1e-9)
    nrm = np.stack([t[:, 1], -t[:, 0]], axis=1)          # sağ normal (x-z düzleminde)
    L = np.concatenate([[0], np.cumsum(np.linalg.norm(np.diff(xz, axis=0), axis=1))])
    pl = xz - nrm * sol
    pr = xz + nrm * sag
    V = np.concatenate([np.stack([pl[:, 0], y + y_ek, pl[:, 1]], 1), np.stack([pr[:, 0], y + y_ek, pr[:, 1]], 1)])
    vv = L if uv_v is None else L * uv_v
    UV = np.concatenate([np.stack([np.full(n, uv_u[0]), vv], 1), np.stack([np.full(n, uv_u[1]), vv], 1)])
    F = []
    for i in range(n - 1):
        a, b, c, d = i, i + 1, n + i, n + i + 1
        F += [(a, c, b), (b, c, d)]
    return V, UV, np.array(F, dtype=np.int32).reshape(-1, 3)


def duvar(p0, p1, y0a, y0b, y1a, y1b, uv_olcek=0.5):
    """p0→p1 (x,z) boyunca dikey dörtgen: alt y0a..y0b, üst y1a..y1b. Yüz p0→p1 yönünün sağına bakar."""
    L = math.hypot(p1[0] - p0[0], p1[1] - p0[1])
    V = [(p0[0], y0a, p0[1]), (p1[0], y0b, p1[1]), (p1[0], y1b, p1[1]), (p0[0], y1a, p0[1])]
    UV = [(0, y0a * uv_olcek), (L * uv_olcek, y0b * uv_olcek), (L * uv_olcek, y1b * uv_olcek), (0, y1a * uv_olcek)]
    return V, UV, [(0, 1, 2), (0, 2, 3)]


def yonlu_duvar(p0, p1, y0a, y0b, y1a, y1b, bak, uv_olcek=0.5):
    """duvar(), yüzü 'bak' (x, z) yönüne bakacak sırada."""
    sag = (p1[1] - p0[1], -(p1[0] - p0[0]))
    if sag[0] * bak[0] + sag[1] * bak[1] < 0:
        return duvar(p1, p0, y0b, y0a, y1b, y1a, uv_olcek)
    return duvar(p0, p1, y0a, y0b, y1a, y1b, uv_olcek)
