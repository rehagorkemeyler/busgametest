"""
A2 — Yol ve arazi yüzey ağları (parça başına).

Malzemeler:
    H3_Asfalt     zemin seviyesindeki asfalt (yollar_geo A) + köprü/alt geçit şeritleri
    H3_Kaldirim   kaldırım ve yaya alanları (+BORDUR)
    H3_Refuj      dar refüjler (+BORDUR; çim)
    H3_Bordur     asfalt–kaldırım arası dikey yüz, köprü korkulukları, alt geçit duvarları, ayaklar
    H3_Cizgi      şerit çizgileri, yaya geçitleri (asfaltın 2 cm üstü)
    H3_Zemin      arazi (yerleşim: toprak/beton karışık)
    H3_Cim        park, yeşil alan araziyi
Yükseklik: zemin seviyesindeki yol düğümlerinden ters uzaklık (k=4). Arazi yollara 35 m'de yumuşakça oturur.

Çıktı: <ISLEM>/parcalar/yol_<i>_<j>.pkl → {malzeme: (V, UV, F)}   (Unity koordinatı, CCW = yukarı)
Kullanım: python -m hat3.yuzeyler
"""
import json
import math
import os
import pickle
import time

import numpy as np
import shapely
from PIL import Image, ImageDraw
from scipy import ndimage
from scipy.spatial import cKDTree
from shapely.geometry import LineString, Point, box

from . import kok
from .arazi import Arazi
from .mesh import PARCA, Parcalar, anahtar, cokgenler, duvar, serit, yonlu_duvar, yuzey
from .yollar import BORDUR

CIZGI_SINIF = ("trunk", "primary", "secondary", "tertiary", "trunk_link", "primary_link", "secondary_link")
YESIL_LANDUSE = ("grass", "recreation_ground", "meadow", "village_green", "cemetery", "forest", "orchard")
YESIL_LEISURE = ("park", "garden", "playground", "pitch")
RASTER = 2.5           # arazi yumuşatma rasterı (m)
YUMUSAK = 35.0         # arazinin yola oturduğu mesafe


def arazi_y(dogal, yol_y, mesafe):
    """Son arazi kotu: doğal arazi yollara YUMUSAK m içinde yumuşakça oturur; yolun altında yol − 0,3."""
    w = np.clip(1 - mesafe / YUMUSAK, 0, 1)
    w = w * w * (3 - 2 * w)
    yv = dogal + (yol_y + 0.08 - dogal) * w
    return np.where(mesafe <= 0.1, yol_y - 0.3, yv)


class SonArazi:
    """Binalar ve donatı için arazi kotu (yuzeyler.py'nin kaydettiği mesafe rasterı ile)."""

    def __init__(self):
        d = np.load(os.path.join(kok.ISLEM, "arazi.npz"))
        self.ar = Arazi(d["H"], float(d["x0"]), float(d["z0"]), float(d["hucre"]))
        self.yh = Yukseklik(d["dugum_xz"], d["dugum_y"], d["dugum_fark"])
        r = np.load(os.path.join(kok.ISLEM, "mesafe.npz"))
        self.mesafe, self.mx0, self.mz0 = r["mesafe"], float(r["x0"]), float(r["z0"])

    def y(self, x, z):
        x, z = np.atleast_1d(x), np.atleast_1d(z)
        ri = np.clip(((z - self.mz0) / RASTER).astype(int), 0, self.mesafe.shape[0] - 1)
        rj = np.clip(((x - self.mx0) / RASTER).astype(int), 0, self.mesafe.shape[1] - 1)
        return arazi_y(self.ar.y(x, z), self.yh(x, z), self.mesafe[ri, rj])

    def yol_mesafesi(self, x, z):
        x, z = np.atleast_1d(x), np.atleast_1d(z)
        ri = np.clip(((z - self.mz0) / RASTER).astype(int), 0, self.mesafe.shape[0] - 1)
        rj = np.clip(((x - self.mx0) / RASTER).astype(int), 0, self.mesafe.shape[1] - 1)
        return self.mesafe[ri, rj]


class Yukseklik:
    """Zemin seviyesindeki yol düğümlerinden yükseklik (ters uzaklık ağırlıklı)."""

    """Zemin seviyesindeki yol yüzeyinin yüksekliği: noktanın en yakın yol ekseni parçasına izdüşümü, eksen boyunca doğrusal
    (yollar enine düz). En yakın iki eksen 3 m'den yakınsa uzaklıkla karışır (kavşak). Eski yöntem (en yakın 4 düğümden ters
    uzaklık) kavşakta ve paralel/ farklı kottaki yollarda (çift yönlü yolun iki yanı, rampa) tümsek ve kırık üretiyordu."""

    def __init__(self, P, Y, F):
        m = np.abs(F) < 0.3
        yollar = json.load(open(os.path.join(kok.ISLEM, "yollar.json"), encoding="utf-8"))
        a, b = [], []
        for w in yollar:
            d_ = w["d"]
            for i, j in zip(d_, d_[1:]):
                if m[i] and m[j]:
                    a.append(i)
                    b.append(j)
        self.a, self.b = np.array(a), np.array(b)
        self.A, self.B = P[self.a], P[self.b]
        self.YA, self.YB = Y[self.a], Y[self.b]
        self.agac = shapely.STRtree(shapely.linestrings(np.stack([self.A, self.B], axis=1)))
        self.P, self.Y = P[m], Y[m]

    def _seg(self, k, q):
        ab = self.B[k] - self.A[k]
        L2 = np.maximum((ab ** 2).sum(1), 1e-9)
        t = np.clip(((q - self.A[k]) * ab).sum(1) / L2, 0, 1)
        y = self.YA[k] + (self.YB[k] - self.YA[k]) * t
        d = np.linalg.norm(q - (self.A[k] + ab * t[:, None]), axis=1)
        return y, d

    def __call__(self, x, z):
        q = np.stack([np.atleast_1d(np.asarray(x, float)), np.atleast_1d(np.asarray(z, float))], 1)
        noktalar = shapely.points(q)
        k1 = self.agac.query_nearest(noktalar, all_matches=False)[1]
        y1, d1 = self._seg(k1, q)
        # ikinci en yakın: 3 m daha uzağa kadar (kavşak karışımı)
        pi, ki = self.agac.query(noktalar, predicate="dwithin", distance=min(float(d1.max()), 25.0) + 3.0)
        y = y1.copy()
        if len(pi):
            yy, dd = self._seg(ki, q[pi])
            ok = (ki != k1[pi]) & (dd < d1[pi] + 3.0)
            pi, yy, dd = pi[ok], yy[ok], dd[ok]
            if len(pi):
                w = 1.0 / np.maximum(dd, 0.3) ** 2
                ws = np.bincount(pi, weights=w, minlength=len(q)) + 1.0 / np.maximum(d1, 0.3) ** 2
                ys = np.bincount(pi, weights=w * yy, minlength=len(q)) + y1 / np.maximum(d1, 0.3) ** 2
                y = ys / ws
        return y


KOPRU_KALDIRIM = 2.0


def kesikli(xz, y, cizgi=3.0, bosluk=6.0):
    """Polilinyeyi kesikli parçalara böler: [(xz, y), ...]."""
    L = np.concatenate([[0], np.cumsum(np.linalg.norm(np.diff(xz, axis=0), axis=1))])
    out = []
    s = bosluk / 2
    while s + cizgi <= L[-1]:
        ss = np.linspace(s, s + cizgi, 3)
        out.append((np.stack([np.interp(ss, L, xz[:, 0]), np.interp(ss, L, xz[:, 1])], 1), np.interp(ss, L, y)))
        s += cizgi + bosluk
    return out


def kavsak_disi(xz, kavsaklar_agac, kavsaklar_r):
    """Kavşak dairelerinin dışında kalan ardışık nokta grupları (indis aralıkları)."""
    d, i = kavsaklar_agac.query(xz)
    disarida = d > kavsaklar_r[i]
    gruplar, bas = [], None
    for k, v in enumerate(disarida):
        if v and bas is None:
            bas = k
        if (not v or k == len(xz) - 1) and bas is not None:
            son = k if v else k - 1
            if son > bas:
                gruplar.append((bas, son + 1))
            bas = None
    return gruplar


def yogunlastir(xz, y, adim=3.0):
    L = np.concatenate([[0], np.cumsum(np.linalg.norm(np.diff(xz, axis=0), axis=1))])
    n = max(2, int(math.ceil(L[-1] / adim)) + 1)
    ss = np.linspace(0, L[-1], n)
    return np.stack([np.interp(ss, L, xz[:, 0]), np.interp(ss, L, xz[:, 1])], 1), np.interp(ss, L, y)


def main():
    t0 = time.time()
    d = np.load(os.path.join(kok.ISLEM, "arazi.npz"))
    P, Y, F = d["dugum_xz"], d["dugum_y"], d["dugum_fark"]
    ar = Arazi(d["H"], float(d["x0"]), float(d["z0"]), float(d["hucre"]))
    geo = pickle.load(open(os.path.join(kok.ISLEM, "yollar_geo.pkl"), "rb"))
    A, S, refuj, koridor = geo["A"], geo["S"], geo["refuj"], geo["koridor"]
    yh = Yukseklik(P, Y, F)
    pc = Parcalar()

    # --- zemin seviyesi yüzeyler
    yuzey(pc, "H3_Asfalt", A, yh, uv_olcek=0.125)
    R = shapely.union_all(refuj) if refuj else None
    S_kal = S.difference(R) if R is not None else S
    yuzey(pc, "H3_Kaldirim", S_kal, yh, uv_olcek=0.5, y_ek=BORDUR)
    if R is not None:
        yuzey(pc, "H3_Refuj", R, yh, uv_olcek=0.25, y_ek=BORDUR)
    print(f"  yüzeyler ({time.time() - t0:.0f} sn)")

    # --- bordür: asfalt sınırı, dışında kaldırım varsa 15 cm dikey yüz; kaldırımın dış kenarı 40 cm aşağı iner
    shapely.prepare(S)
    shapely.prepare(A)
    for g_kenar, ic, ust_ek, alt_ek in ((A, A, BORDUR, 0.0), (S, S, BORDUR, -0.4)):
        for pg in cokgenler(g_kenar):
            for halka in [pg.exterior] + list(pg.interiors):
                c = np.asarray(halka.coords)
                xz, _ = yogunlastir(c, np.zeros(len(c)), 4.0)
                if len(xz) < 2:
                    continue
                a, b = xz[:-1], xz[1:]
                m = (a + b) / 2
                t = b - a
                n = np.stack([t[:, 1], -t[:, 0]], 1) / np.maximum(np.linalg.norm(t, axis=1, keepdims=True), 1e-9)
                # dış normal: m + n içeride değilse dışarı
                iceride = shapely.contains_xy(ic, m[:, 0] + n[:, 0] * 0.3, m[:, 1] + n[:, 1] * 0.3)
                n[iceride] *= -1
                dis_x, dis_z = m[:, 0] + n[:, 0] * 0.5, m[:, 1] + n[:, 1] * 0.5
                if g_kenar is A:
                    sec = shapely.contains_xy(S, dis_x, dis_z)        # asfaltın yanında kaldırım var
                else:
                    sec = ~shapely.contains_xy(A, dis_x, dis_z)       # kaldırımın dış kenarı (asfalta değmeyen)
                ya, yb = yh(a[:, 0], a[:, 1]), yh(b[:, 0], b[:, 1])
                # düz ve aynı türden ardışık parçalar birleştirilir (≤ 24 m, yön farkı < 4°, kot farkı küçük)
                tt = (b - a) / np.maximum(np.linalg.norm(b - a, axis=1, keepdims=True), 1e-9)
                k = 0
                idx = np.nonzero(sec)[0]
                while k < len(idx):
                    bas = son = idx[k]
                    while k + 1 < len(idx) and idx[k + 1] == son + 1 and np.dot(tt[bas], tt[son + 1]) > 0.9976 and \
                            np.linalg.norm(b[son + 1] - a[bas]) < 24 and \
                            abs((yb[son + 1] - ya[bas]) - (yb[son] - ya[bas]) * np.linalg.norm(b[son + 1] - a[bas]) /
                                max(np.linalg.norm(b[son] - a[bas]), 1e-6)) < 0.04:
                        k += 1
                        son = idx[k]
                    k += 1
                    p0, p1, y0, y1 = a[bas], b[son], ya[bas], yb[son]
                    if g_kenar is A:     # asfalttan kaldırıma yükselen yüz: asfalta (içeri) bakar
                        V, UV, Fc = yonlu_duvar(p0, p1, y0, y1, y0 + ust_ek, y1 + ust_ek, -n[bas], 1.0)
                    else:                # kaldırımın dış kenarı: dışarı bakar
                        V, UV, Fc = yonlu_duvar(p0, p1, y0 + alt_ek, y1 + alt_ek, y0 + ust_ek, y1 + ust_ek, n[bas], 1.0)
                    pc.ekle_dunya("H3_Bordur", V, UV, Fc)
    print(f"  bordürler ({time.time() - t0:.0f} sn)")

    # --- şerit çizgileri
    kav = np.array([(x, z) for x, z, r in geo["kavsaklar"]]) if geo["kavsaklar"] else np.zeros((1, 2)) + 1e9
    kav_r = np.array([r for x, z, r in geo["kavsaklar"]] or [0.0])
    kav_agac = cKDTree(kav)
    for sr in geo["seritler"]:
        if sr["sinif"] not in CIZGI_SINIF:
            continue
        idx = np.array(sr["d"])
        zemin = np.abs(F[idx]) < 0.3
        if zemin.sum() < 2:
            continue
        xz0, y0 = P[idx], Y[idx]
        if sr["ters"]:
            xz0, y0 = xz0[::-1], y0[::-1]
        W, n = sr["genislik"], sr["n"]
        cizgiler = []      # (ofset, kesikli mi)
        if sr["tek"]:
            for k in range(1, n):
                cizgiler.append((-W / 2 + k * W / n, True))
            if sr["sinif"] in ("trunk", "primary") and n >= 2:
                cizgiler += [(-W / 2 + 0.35, False), (W / 2 - 0.35, False)]
        else:
            if sr["sinif"] in ("trunk", "primary"):
                cizgiler += [(-0.12, False), (0.12, False)]
            else:
                cizgiler.append((0.0, True))
            yarim = max(1, n // 2)
            for k in range(1, yarim):
                o = k * (W / 2) / yarim
                cizgiler += [(o, True), (-o, True)]
        for bas, son in kavsak_disi(xz0, kav_agac, kav_r):
            parca = slice(bas, son)
            if not np.all(zemin[parca]):
                continue
            xz, yy = yogunlastir(xz0[parca], y0[parca], 3.0)
            if len(xz) < 2:
                continue
            for o, kes in cizgiler:
                parcalar_ = kesikli(xz, yy) if kes else [(xz, yy)]
                for pxz, py in parcalar_:
                    yz = yh(pxz[:, 0], pxz[:, 1]) + 0.02
                    V, UV, Fc = serit(pxz, yz, -(o - 0.075), o + 0.075)
                    pc.ekle_dunya("H3_Cizgi", V, UV * 0.0, Fc)
    print(f"  çizgiler ({time.time() - t0:.0f} sn)")

    # --- yaya geçitleri (OSM crossing düğümleri, araç yolu üzerinde)
    o = kok.osm()
    gecit = 0
    seritler_geo = [(LineString(P[sr["d"]]), sr) for sr in geo["seritler"] if sr["sinif"] in kok.ARAC_YOLU]
    agac = shapely.STRtree([g for g, _ in seritler_geo])
    for nid, t in o["tagged_nodes"].items():
        if t.get("highway") != "crossing" and t.get("crossing") is None:
            continue
        if nid not in o["xy"]:
            continue
        p = Point(o["xy"][nid])
        if not koridor.contains(p):
            continue
        i = agac.nearest(p)
        g, sr = seritler_geo[i]
        if g.distance(p) > 1.0 or sr["sinif"] in ("service",):
            continue
        s = g.project(p)
        a, b = g.interpolate(max(0, s - 2)), g.interpolate(min(g.length, s + 2))
        tx, tz = b.x - a.x, b.y - a.y
        Lt = math.hypot(tx, tz) or 1
        tx, tz = tx / Lt, tz / Lt
        W = sr["genislik"]
        # zebra: yol yönünde 3 m'lik, 0,5 m genişlikte bantlar, aralık 0,5 m
        k = -W / 2 + 0.5
        while k + 0.5 <= W / 2 - 0.3:
            cx, cz = p.x + tz * (k + 0.25), p.y - tx * (k + 0.25)
            xz = np.array([[cx - tx * 1.5, cz - tz * 1.5], [cx + tx * 1.5, cz + tz * 1.5]])
            yy = yh(xz[:, 0], xz[:, 1]) + 0.025
            V, UV, Fc = serit(xz, yy, 0.25, 0.25)
            pc.ekle_dunya("H3_Cizgi", V, UV * 0.0, Fc)
            k += 1.0
        gecit += 1
    print(f"  {gecit} yaya geçidi ({time.time() - t0:.0f} sn)")

    # --- köprüler ve alt geçitler
    # duvar/korkuluk başka bir yolun asfaltına düşmesin (birleşen ya da yan yana giren şeritler): her şeridin asfalt izi
    iz_listesi = [LineString(kp["xz"]).buffer(kp["genislik"] / 2 - 0.2, cap_style="flat") for kp in geo["katmanli"]]
    iz_agac = shapely.STRtree(iz_listesi)

    def baska_yolda(k_kendi, x, z):
        if shapely.contains_xy(A, x, z):
            return True
        for j in iz_agac.query(Point(x, z)):
            if j != k_kendi and iz_listesi[j].contains(Point(x, z)):
                return True
        return False

    tunel_id = {w["id"]: (w["t"].get("tunnel") not in (None, "no") or w["t"].get("location") == "underground")
                for w in json.load(open(os.path.join(kok.ISLEM, "yollar.json"), encoding="utf-8"))}
    tavan_say = [0]
    for k_kp, kp in enumerate(geo["katmanli"]):
        xz, y = yogunlastir(kp["xz"], kp["y"], 4.0)
        W = kp["genislik"]
        kal = min(kp["kaldirim"], KOPRU_KALDIRIM)       # köprü/yarmada dar yürüme yolu (7 m'lik bulvar kaldırımı komşu şeritlere biniyordu)
        V, UV, Fc = serit(xz, y, W / 2, W / 2, y_ek=0.0, uv_v=0.125, uv_u=(0, W * 0.125))
        pc.ekle_dunya("H3_Asfalt", V, UV, Fc)
        zemin_y = ar.y(xz[:, 0], xz[:, 1])
        yukari = kp["fark"].max() > 0.3
        kenar = W / 2 + max(kal, 0.6)
        if kal > 0 or yukari:
            for yon in (-1, 1):
                sol, sag = (kenar, -W / 2) if yon < 0 else (-W / 2, kenar)
                V, UV, Fc = serit(xz, y + BORDUR, sol, sag, uv_v=0.5)
                pc.ekle_dunya("H3_Kaldirim", V, UV, Fc)
        t = np.gradient(xz, axis=0)
        t /= np.maximum(np.linalg.norm(t, axis=1, keepdims=True), 1e-9)
        nrm = np.stack([t[:, 1], -t[:, 0]], 1)
        if yukari:
            # korkuluk (1 m) + tabliye yanı, araziden 1,2 m'den yüksekte
            for yon in (-1, 1):
                kx = xz + nrm * kenar * yon
                for i in range(len(xz) - 1):
                    if max(y[i], y[i + 1]) - max(zemin_y[i], zemin_y[i + 1]) < 1.2:
                        continue
                    mx, mz = (kx[i] + kx[i + 1]) / 2
                    if baska_yolda(k_kp, mx, mz):
                        continue
                    bak = nrm[i] * yon
                    V, UV, Fc = yonlu_duvar(kx[i], kx[i + 1], y[i] - 1.2, y[i + 1] - 1.2, y[i] + 1.1, y[i + 1] + 1.1, bak)
                    pc.ekle_dunya("H3_Bordur", V, UV, Fc)
                    V, UV, Fc = yonlu_duvar(kx[i], kx[i + 1], y[i] + BORDUR, y[i + 1] + BORDUR, y[i] + 1.1,
                                            y[i + 1] + 1.1, -bak)
                    pc.ekle_dunya("H3_Bordur", V, UV, Fc)
            # alt yüz
            V, UV, Fc = serit(xz, y - 1.2, kenar, kenar, uv_v=0.5)
            pc.ekle_dunya("H3_Bordur", V, UV, Fc[:, ::-1])
            # ayaklar: 25 m'de bir, araziden 3 m'den yüksekse ve zemin seviyesi asfaltın dışında
            L = np.concatenate([[0], np.cumsum(np.linalg.norm(np.diff(xz, axis=0), axis=1))])
            for s in np.arange(12.5, L[-1], 25.0):
                px, pz = np.interp(s, L, xz[:, 0]), np.interp(s, L, xz[:, 1])
                py, pg = np.interp(s, L, y), float(ar.y(px, pz))
                if py - pg < 3.0 or shapely.contains_xy(A, px, pz):
                    continue
                for ax, az in ((-0.6, -0.6), (0.6, -0.6), (0.6, 0.6), (-0.6, 0.6)):
                    bx, bz = -az, ax           # köşeden bir sonraki köşe (saat yönünün tersi)
                    V, UV, Fc = yonlu_duvar((px + ax, pz + az), (px + bx, pz + bz), pg - 0.5, pg - 0.5, py - 1.2,
                                            py - 1.2, ((ax + bx) / 2, (az + bz) / 2))
                    pc.ekle_dunya("H3_Bordur", V, UV, Fc)
        else:
            # kapalı tünel (OSM tunnel=yes, zeminden > 4 m aşağıda): beton tavan döşemesi (yol + 5,9 m; alt ve üst yüz).
            # Üstü açık kalınca tünel içinden gökyüzü ve üstteki yolların alt yüzü görünüyordu.
            if tunel_id.get(kp["id"]):
                dd_ = np.asarray(kp["fark"], float)
                derin_ = np.interp(np.linspace(0, len(dd_) - 1, len(xz)), np.arange(len(dd_)), dd_) < -4.0
                bas_ = None
                for i in range(len(xz) + 1):
                    ic = i < len(xz) and derin_[i]
                    if ic and bas_ is None:
                        bas_ = i
                    elif not ic and bas_ is not None:
                        if i - bas_ >= 2:
                            q, qy = xz[bas_:i], y[bas_:i] + 5.9
                            V, UV, Fc = serit(q, qy, kenar + 0.4, kenar + 0.4, uv_v=0.5)
                            pc.ekle_dunya("H3_Bordur", V, UV, Fc[:, ::-1])
                            V, UV, Fc = serit(q, qy + 0.4, kenar + 0.4, kenar + 0.4, uv_v=0.5)
                            pc.ekle_dunya("H3_Bordur", V, UV, Fc)
                            tavan_say[0] += 1
                        bas_ = None
            # alt geçit: iki yanda istinat duvarı (yol kotundan doğal araziye)
            for yon in (-1, 1):
                kx = xz + nrm * kenar * yon
                dogal = ar.y(kx[:, 0], kx[:, 1])
                for i in range(len(xz) - 1):
                    if max(dogal[i] - y[i], dogal[i + 1] - y[i + 1]) < 0.6:
                        continue
                    mx, mz = (kx[i] + kx[i + 1]) / 2
                    if baska_yolda(k_kp, mx, mz):
                        continue
                    V, UV, Fc = yonlu_duvar(kx[i], kx[i + 1], y[i] - 0.2, y[i + 1] - 0.2, max(dogal[i], y[i]) + 0.6,
                                            max(dogal[i + 1], y[i + 1]) + 0.6, -nrm[i] * yon)
                    pc.ekle_dunya("H3_Bordur", V, UV, Fc)
    print(f"  köprü/alt geçit {len(geo['katmanli'])}, tünel tavanı {tavan_say[0]} ({time.time() - t0:.0f} sn)")

    # --- kesik yol uçları: koridor sınırında kesilen asfaltın ucuna beton bariyer (0,9 m; Yol grubunda, çarpışmalı)
    sinir = koridor.boundary
    bariyer = 0
    for gg in cokgenler(A):
        x_ = gg.boundary.intersection(sinir.buffer(0.3))
        for ln in getattr(x_, "geoms", [x_]):
            if ln.is_empty or ln.geom_type not in ("LineString", "MultiLineString") or ln.length < 2.0:
                continue
            c = np.asarray(ln.coords)
            a_, b_ = c[0], c[-1]
            u = b_ - a_
            L_ = np.linalg.norm(u)
            if L_ < 2.0:
                continue
            u /= L_
            n_ = np.array([u[1], -u[0]])
            # koridorun içine doğru 0,6 m çek (asfaltın üstünde dursun)
            if not shapely.contains_xy(koridor, *((a_ + b_) / 2 + n_ * 1.0)):
                n_ = -n_
            a_, b_ = a_ + n_ * 0.6, b_ + n_ * 0.6
            ya, yb = yh(np.array([a_[0], b_[0]]), np.array([a_[1], b_[1]]))
            for kay, yon in ((0.25, 1), (-0.25, -1)):
                pa, pb = a_ + n_ * kay, b_ + n_ * kay
                V, UV, Fc = yonlu_duvar(pa, pb, ya - 0.1, yb - 0.1, ya + 0.9, yb + 0.9, n_ * yon)
                pc.ekle_dunya("H3_Bordur", V, UV, Fc)
            V, UV, Fc = serit(np.array([a_, b_]), np.array([ya + 0.9, yb + 0.9]), 0.25, 0.25)
            pc.ekle_dunya("H3_Bordur", V, UV, Fc)
            bariyer += 1
    print(f"  kesik yol ucuna bariyer {bariyer} ({time.time() - t0:.0f} sn)")

    # --- arazi: raster (yol+kaldırım maskesi, mesafe dönüşümü, alt geçit çukurları, yeşil alanlar)
    x0, z0, x1, z1 = koridor.bounds
    x0, z0 = math.floor(x0 / PARCA) * PARCA, math.floor(z0 / PARCA) * PARCA
    x1, z1 = math.ceil(x1 / PARCA) * PARCA, math.ceil(z1 / PARCA) * PARCA
    nx, nz = int((x1 - x0) / RASTER), int((z1 - z0) / RASTER)

    def raster(geoms, deger=255, im=None):
        im = im or Image.new("L", (nx, nz), 0)
        dr = ImageDraw.Draw(im)
        for g in geoms:
            for pg in cokgenler(g):
                dr.polygon([((a - x0) / RASTER, (b - z0) / RASTER) for a, b in pg.exterior.coords], fill=deger)
                for h in pg.interiors:
                    dr.polygon([((a - x0) / RASTER, (b - z0) / RASTER) for a, b in h.coords], fill=0)
        return im

    yol_m = np.asarray(raster([A, S])) > 0
    mesafe = ndimage.distance_transform_edt(~yol_m) * RASTER
    cukur_g, cukur_y = [], []
    for kp in geo["katmanli"]:
        if kp["fark"].min() < -0.3:
            cukur_g.append(LineString(kp["xz"]).buffer(kp["genislik"] / 2 + max(kp["kaldirim"], 0.6) + 1.5))
    cukur_m = np.asarray(raster(cukur_g)) > 0 if cukur_g else np.zeros((nz, nx), bool)
    yesil = []
    for wid, w in o["ways"].items():
        t = w["t"]
        if t.get("landuse") in YESIL_LANDUSE or t.get("leisure") in YESIL_LEISURE or t.get("natural") in ("wood", "scrub", "grassland"):
            ns = [o["xy"][n] for n in w["n"] if n in o["xy"]]
            if len(ns) >= 4:
                yesil.append(shapely.Polygon(ns).buffer(0))
    yesil_m = np.asarray(raster(yesil)) > 0
    koridor_m = np.asarray(raster([koridor.buffer(15)])) > 0
    np.savez_compressed(os.path.join(kok.ISLEM, "mesafe.npz"), mesafe=mesafe.astype(np.float32), x0=x0, z0=z0)
    print(f"  raster {nx}×{nz} ({time.time() - t0:.0f} sn)")

    H_HUCRE = 10.0
    for i in range(int(x0 // PARCA), int(x1 // PARCA)):
        for j in range(int(z0 // PARCA), int(z1 // PARCA)):
            kb = box(i * PARCA, j * PARCA, (i + 1) * PARCA, (j + 1) * PARCA)
            if not kb.intersects(koridor):
                continue
            n = int(PARCA / H_HUCRE) + 1
            gx, gz = np.meshgrid(i * PARCA + np.arange(n) * H_HUCRE, j * PARCA + np.arange(n) * H_HUCRE)
            gx, gz = gx.ravel(), gz.ravel()
            ri = np.clip(((gz - z0) / RASTER).astype(int), 0, nz - 1)
            rj = np.clip(((gx - x0) / RASTER).astype(int), 0, nx - 1)
            dogal = ar.y(gx, gz)
            yol_y = yh(gx, gz)
            dd = mesafe[ri, rj]
            yv = arazi_y(dogal, yol_y, dd)
            cm = cukur_m[ri, rj]
            if cm.any():
                # alt geçit çukuru: en yakın katmanlı (aşağıdaki) yol düğümünün kotu
                tum = np.abs(F) > 0.3
                ag = cKDTree(P[tum & (F < 0)]) if (tum & (F < 0)).any() else None
                if ag is not None:
                    _, k = ag.query(np.stack([gx[cm], gz[cm]], 1))
                    yv[cm] = np.minimum(yv[cm], Y[tum & (F < 0)][k] - 0.3)
            V = np.stack([gx, yv, gz], 1)
            F_ = []
            for a in range(n - 1):
                for b in range(n - 1):
                    v0 = a * n + b
                    q = (v0, v0 + 1, v0 + n + 1, v0 + n)
                    cx, cz = gx[v0] + H_HUCRE / 2, gz[v0] + H_HUCRE / 2
                    ci = min(int((cz - z0) / RASTER), nz - 1)
                    cj = min(int((cx - x0) / RASTER), nx - 1)
                    if not koridor_m[ci, cj]:
                        continue
                    # tümüyle yolun altında kalan hücreyi atla (görünmez): hücredeki bütün raster pikselleri yol olmalı
                    r0 = int((gz[v0] - z0) / RASTER)
                    c0 = int((gx[v0] - x0) / RASTER)
                    blok = yol_m[max(r0, 0):r0 + int(H_HUCRE / RASTER) + 1, max(c0, 0):c0 + int(H_HUCRE / RASTER) + 1]
                    if blok.size and blok.all():
                        continue
                    F_.append((q[0], q[1], q[2], yesil_m[ci, cj]))
                    F_.append((q[0], q[2], q[3], yesil_m[ci, cj]))
            if not F_:
                continue
            F_ = np.array(F_, dtype=np.int64)
            for mal, sec in (("H3_Zemin", F_[:, 3] == 0), ("H3_Cim", F_[:, 3] == 1)):
                if sec.any():
                    # yön: (gx artar, gz artar) ızgarada (v0, v0+1, v0+n+1) saat yönünün tersi
                    pc.ag((i, j), mal).ekle(V, np.stack([gx, gz], 1) * (0.1 if mal == "H3_Zemin" else 0.15),
                                            F_[sec][:, :3])
    print(f"  arazi ({time.time() - t0:.0f} sn)")

    klasor = os.path.join(kok.ISLEM, "parcalar")
    os.makedirs(klasor, exist_ok=True)
    for f in os.listdir(klasor):
        if f.startswith("yol_"):
            os.remove(os.path.join(klasor, f))
    toplam = {}
    ciktilar = {}
    for (i, j), mals in pc.p.items():
        cikti = {}
        for mal, ag in mals.items():
            dz = ag.dizi()
            if dz is not None:
                cikti[mal] = dz
                toplam[mal] = toplam.get(mal, 0) + len(dz[2])
        ciktilar[(i, j)] = cikti
    # şerit çizgileri ve yaya geçitleri altlarındaki asfalta oturur (zemin düğümlerinden ara değerleme köprü/rampa/yarma
    # asfaltından 0,5 m'ye kadar sapıyordu): en yakın asfalt yüzeyi + 2,5 cm
    from .denetim import agac, sorgu
    TA = np.concatenate([c["H3_Asfalt"][0][c["H3_Asfalt"][2]] for c in ciktilar.values() if "H3_Asfalt" in c])
    trA = agac(TA)
    oturan = 0
    for c in ciktilar.values():
        if "H3_Cizgi" not in c:
            continue
        V = c["H3_Cizgi"][0]
        pi, ti, yy = sorgu(trA, TA, V[:, [0, 2]])
        en = np.full(len(V), np.nan)
        for k_, y_ in zip(pi, yy):
            if np.isnan(en[k_]) or abs(y_ - V[k_, 1]) < abs(en[k_] - V[k_, 1]):
                en[k_] = y_
        m = ~np.isnan(en) & (np.abs(en - V[:, 1]) < 1.5)
        V[m, 1] = en[m] + 0.025
        oturan += int(m.sum())
        # altında asfalt olmayan çizgi üçgenleri atılır (koridor sınırında kesilen yolun çizgisi toprağa uzanıyordu)
        Vv, UVv, Fv = c["H3_Cizgi"]
        tut = m[Fv].all(axis=1)
        c["H3_Cizgi"] = (Vv, UVv, Fv[tut])
    print(f"  asfalta oturan çizgi köşesi {oturan}")
    for (i, j), cikti in ciktilar.items():
        pickle.dump(cikti, open(os.path.join(klasor, f"yol_{i}_{j}.pkl"), "wb"))
    print(f"{len(pc.p)} parça; üçgen: " + ", ".join(f"{k} {v}" for k, v in sorted(toplam.items())) +
          f" ({time.time() - t0:.0f} sn)")


if __name__ == "__main__":
    main()
