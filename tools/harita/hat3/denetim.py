"""
Harita denetimi (docs/PLAN_HARITA_V2.md, M1): FBX'e giden ara çıktılar (<ISLEM>/parcalar/*.pkl, yollar_geo.pkl) ve yerleşim
JSON'u üzerinde, haritadaki **bütün araç yollarını** tarar. Unity'siz, ~1 dk.

Örnekleme: asfalt alanı (yollar_geo A + katmanlı yolların şeritleri) 2 m ızgara. Her noktada o noktayı içeren bütün yol üçgenlerinin
yükseklikleri (köprü üstü/altı ayrı katman). Ölçütler:
  delik       asfalt alanında yol yüzeyi yok
  basamak     ızgara boyunca ikinci fark > 0,10 m (2 m'de ani kırılma; bordür hariç: yalnız asfalt/çizgi yüzeyi)
  egim        2 m'de > %12
  aciklik     yüzeyin 0,05–5,0 m üstünde yol/bina/simge/ağaç gövdesi üçgeni (köprü tabliyesi, rampa, üst geçit)
  dal         yüzeyin 0,05–4,2 m üstünde ağaç yaprağı (otobüs çatısına değer)
  serit       trafik şeridinin ekseni ve ±1,3 m'si asfaltta değil
  kesik       araç yolu koridor sınırında kesiliyor (asfalt düz çizgiyle bitiyor)
  kose        −200 m altında / +400 m üstünde köşe
  nesne       yerleşim öğesi (lamba, sundurma, ışık, simge) tabanı zeminden > 0,10 m uzak
Sorunlu noktalar 20 m'lik hücrelerle kümelenir; her küme: tür, nokta sayısı, merkez, en yakın sokak ve durak.

Kullanım: python -m hat3.denetim [--maps <Maps klasörü>] [--cikti docs/gorseller/harita_v2/denetim_m1]
Çıktı: <cikti>.txt (özet + kümeler), <cikti>.json (görüntü aracı için kümeler).
"""
import argparse
import glob
import json
import math
import os
import pickle
import time

import numpy as np
import shapely
from shapely.geometry import LineString, MultiPolygon, Point, Polygon
from shapely.strtree import STRtree

from . import kok

ADIM = 2.0
YOL_YUZEY = ("H3_Asfalt", "H3_Cizgi")
YOL_DIGER = ("H3_Kaldirim", "H3_Refuj", "H3_Bordur")
ACIKLIK = 5.0
DAL = 4.2
EGIM = 0.12
KIRILMA = 0.10
SERIT_PAY = 1.3


def ucgenler(desen, malzemeler=None, haric=()):
    """Parça pkl'lerinden (V, F) birleşimi: [(tri_xyz (n,3,3), malzeme adı)]."""
    tris, adlar = [], []
    for f in sorted(glob.glob(os.path.join(kok.ISLEM, "parcalar", desen))):
        p = pickle.load(open(f, "rb"))
        for m, (V, UV, F) in p.items():
            ad = m.split("@")[0]
            if malzemeler is not None and ad not in malzemeler:
                continue
            if ad in haric:
                continue
            if len(F) == 0:
                continue
            tris.append(V[F])
            adlar += [ad] * len(F)
    if not tris:
        return np.zeros((0, 3, 3)), np.array([])
    return np.concatenate(tris), np.array(adlar)


def yukseklik(T, idx, xz):
    """Noktaların (xz, (n,2)) T[idx] üçgenleri üzerindeki yüksekliği; üçgen dışındaysa NaN."""
    a, b, c = T[idx, 0], T[idx, 1], T[idx, 2]
    d = (b[:, 2] - c[:, 2]) * (a[:, 0] - c[:, 0]) + (c[:, 0] - b[:, 0]) * (a[:, 2] - c[:, 2])
    ok = np.abs(d) > 1e-9
    d = np.where(ok, d, 1.0)
    l1 = ((b[:, 2] - c[:, 2]) * (xz[:, 0] - c[:, 0]) + (c[:, 0] - b[:, 0]) * (xz[:, 1] - c[:, 2])) / d
    l2 = ((c[:, 2] - a[:, 2]) * (xz[:, 0] - c[:, 0]) + (a[:, 0] - c[:, 0]) * (xz[:, 1] - c[:, 2])) / d
    l3 = 1 - l1 - l2
    e = -1e-6
    ic = ok & (l1 >= e) & (l2 >= e) & (l3 >= e)
    y = l1 * a[:, 1] + l2 * b[:, 1] + l3 * c[:, 1]
    return np.where(ic, y, np.nan)


def agac(T):
    """Üçgenlerin 2B çokgenleri için STRtree."""
    pg = shapely.polygons(T[:, :, [0, 2]])
    return STRtree(pg)


def sorgu(tree, T, P):
    """P (n,2) noktaları × T üçgenleri: (nokta idx, üçgen idx, y) — yalnız gerçekten içeren çiftler."""
    if len(T) == 0 or len(P) == 0:
        return np.zeros(0, int), np.zeros(0, int), np.zeros(0)
    pi, ti = tree.query(shapely.points(P), predicate="intersects")
    y = yukseklik(T, ti, P[pi])
    k = ~np.isnan(y)
    return pi[k], ti[k], y[k]


class Kume:
    def __init__(self):
        self.h = {}

    def ekle(self, tur, x, z, deger=None, ek=None, y=None):
        x, z = float(x), float(z)          # numpy float32 JSON'a yazılamıyor
        deger = None if deger is None else float(deger)
        k = (tur, int(math.floor(x / 20)), int(math.floor(z / 20)))
        e = self.h.setdefault(k, {"tur": tur, "n": 0, "x": 0.0, "z": 0.0, "y": 0.0, "ny": 0, "deger": None, "ek": ek})
        e["n"] += 1
        e["x"] += x
        e["z"] += z
        if y is not None:
            e["y"] += float(y)
            e["ny"] += 1
        if deger is not None:
            e["deger"] = deger if e["deger"] is None else (min(e["deger"], deger) if tur in ("aciklik", "dal") else max(e["deger"], deger))

    def kumeler(self):
        """Komşu hücreleri birleştir."""
        hucre = {k: v for k, v in self.h.items()}
        gorulen, sonuc = set(), []
        for k in hucre:
            if k in gorulen:
                continue
            yigin, grup = [k], []
            gorulen.add(k)
            while yigin:
                c = yigin.pop()
                grup.append(hucre[c])
                for di in (-1, 0, 1):
                    for dj in (-1, 0, 1):
                        nk = (c[0], c[1] + di, c[2] + dj)
                        if nk in hucre and nk not in gorulen:
                            gorulen.add(nk)
                            yigin.append(nk)
            n = sum(g["n"] for g in grup)
            x = sum(g["x"] for g in grup) / n
            z = sum(g["z"] for g in grup) / n
            ds = [g["deger"] for g in grup if g["deger"] is not None]
            d = (min(ds) if k[0] in ("aciklik", "dal") else max(ds)) if ds else None
            ny = sum(g["ny"] for g in grup)
            yy = sum(g["y"] for g in grup) / ny if ny else None
            sonuc.append({"tur": k[0], "n": n, "x": round(x, 1), "z": round(z, 1), "y": None if yy is None else round(yy, 1),
                          "deger": None if d is None else round(float(d), 2),
                          "ek": grup[0]["ek"]})
        return sonuc


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--maps", default=os.path.join(kok.KLASOR, "..", "..", "AnkaraBusSimulator", "Assets", "_Project", "Maps"))
    ap.add_argument("--cikti", default=os.path.join(kok.KLASOR, "..", "..", "docs", "gorseller", "harita_v2", "denetim"))
    a = ap.parse_args()
    t0 = time.time()
    geo = pickle.load(open(os.path.join(kok.ISLEM, "yollar_geo.pkl"), "rb"))
    yer = json.load(open(os.path.join(a.maps, "Hat3", "Hat3_Yerlesim.json"), encoding="utf-8"))

    # yol yüzeyi (asfalt + çizgi; köprüler dahil) ve diğer yol malzemeleri
    TY, AY = ucgenler("yol_*.pkl", YOL_YUZEY)
    TD, AD = ucgenler("yol_*.pkl", YOL_DIGER)
    TZ, _ = ucgenler("yol_*.pkl", ("H3_Zemin", "H3_Cim"))
    TB, _ = ucgenler("bina_*.pkl", ("H3_Carpisma",))
    TS, AS = ucgenler("simge_*.pkl", None, haric=("H3_Su",))
    TG, _ = ucgenler("agac_*.pkl", ("H3_Govde",))
    TL, _ = ucgenler("agac_*.pkl", ("H3_Yaprak",))
    print(f"  üçgen: yol {len(TY)}, yol diğer {len(TD)}, zemin {len(TZ)}, bina {len(TB)}, simge {len(TS)}, ağaç {len(TG)}/{len(TL)} "
          f"({time.time() - t0:.0f} sn)")
    agY, agD, agZ = agac(TY), agac(TD), agac(TZ)

    # örnek noktalar: asfalt alanı + katmanlı yolların şeritleri
    alan = [geo["A"]]
    for kp in geo["katmanli"]:
        alan.append(LineString(kp["xz"]).buffer(float(kp["genislik"]) / 2, cap_style="flat"))
    A = shapely.union_all(alan)
    shapely.prepare(A)
    # 250 m'lik karelerde ızgara (tek dev çokgende içe tamponlama ve 10 milyonluk ızgara çok yavaş)
    x0, z0, x1, z1 = A.bounds
    gx, gz = np.meshgrid(np.arange(math.floor(x0 / ADIM) * ADIM, x1, ADIM), np.arange(math.floor(z0 / ADIM) * ADIM, z1, ADIM))
    ic = np.zeros(gx.shape, bool)
    for i in range(int(math.floor(x0 / 250)), int(math.ceil(x1 / 250))):
        for j in range(int(math.floor(z0 / 250)), int(math.ceil(z1 / 250))):
            kare = shapely.box(i * 250, j * 250, (i + 1) * 250, (j + 1) * 250)
            Ak = A.intersection(kare)
            if Ak.is_empty:
                continue
            Ak = Ak.buffer(-0.3)
            m = (gx >= i * 250) & (gx < (i + 1) * 250) & (gz >= j * 250) & (gz < (j + 1) * 250)
            r = np.where(m)
            if len(r[0]) == 0:
                continue
            ic[r] = shapely.contains_xy(Ak, gx[r], gz[r])
    ic = ic.ravel()
    G = np.stack([gx.ravel(), gz.ravel()], axis=1)
    P = G[ic]
    print(f"  örnek nokta {len(P)} ({time.time() - t0:.0f} sn)")

    K = Kume()
    # yüzey yükseklikleri: her nokta için katmanlar
    pi, ti, y = sorgu(agY, TY, P)
    katman = {}
    for p, yy in zip(pi, y):
        katman.setdefault(int(p), []).append(float(yy))
    for p in katman:
        ys = sorted(katman[p])
        tek = [ys[0]]
        for v in ys[1:]:
            if v - tek[-1] > 0.3:
                tek.append(v)
        katman[p] = tek
    delik = [i for i in range(len(P)) if i not in katman]
    for i in delik:
        # yol yok: altında başka yol malzemesi ya da arazi var mı (bilgi)
        K.ekle("delik", P[i, 0], P[i, 1])
    print(f"  delik {len(delik)} ({time.time() - t0:.0f} sn)")

    # ızgara komşuluğu: basamak / eğim (aynı katman: en yakın yükseklik)
    nx = gx.shape[1]
    gidx = -np.ones(len(G), int)
    gidx[np.where(ic)[0]] = np.arange(len(P))
    nokta_g = np.where(ic)[0]          # P indeksi → ızgara indeksi (bir kez; her çağrıda np.where 11 milyon hücreyi tarıyordu)
    def komsu(i, di, dj):
        r, c = divmod(int(nokta_g[i]), nx)
        r2, c2 = r + dj, c + di
        if r2 < 0 or c2 < 0 or r2 >= gx.shape[0] or c2 >= nx:
            return -1
        return gidx[r2 * nx + c2]
    n_basamak = n_egim = 0
    # yalnız tek katmanlı noktalar: köprü altı/üstü olan noktada komşu yanlış katmana eşlenebiliyor (katman çakışması
    # zaten "açıklık" denetiminde). Aynı yüzey: komşu farkı < 0,6 m.
    for i, ys in katman.items():
        if len(ys) != 1:
            continue
        yv = ys[0]
        for (di, dj) in ((1, 0), (0, 1)):
            j = komsu(i, di, dj)
            k2 = komsu(i, -di, -dj)
            if j < 0 or len(katman.get(j, [])) != 1:
                continue
            yj = katman[j][0]
            if abs(yj - yv) >= 0.6:
                continue
            e = abs(yj - yv) / ADIM
            if e > EGIM:
                n_egim += 1
                K.ekle("egim", P[i, 0], P[i, 1], e * 100, y=yv)
            if k2 >= 0 and len(katman.get(k2, [])) == 1 and abs(katman[k2][0] - yv) < 0.6:
                kr = abs(yj - 2 * yv + katman[k2][0])
                if kr > KIRILMA:
                    n_basamak += 1
                    K.ekle("basamak", P[i, 0], P[i, 1], kr, y=yv)
    print(f"  basamak {n_basamak}, eğim {n_egim} ({time.time() - t0:.0f} sn)")

    # açıklık: yüzeyin üstündeki her şey (yolun diğer katmanı, bina, simge, ağaç gövdesi), ağaç dalı ayrı
    engeller = [(TY, "yol"), (TD, "yol"), (TB, "bina"), (TS, "simge"), (TG, "agac")]
    n_ac = 0
    for T, ad in engeller:
        if len(T) == 0:
            continue
        tr = agac(T) if T is not TY else agY
        if T is TD:
            tr = agD
        qi, qt, qy = sorgu(tr, T, P)
        for p, t, yy in zip(qi, qt, qy):
            for yv in katman.get(int(p), []):
                d = yy - yv
                if 0.05 < d < ACIKLIK:
                    # yolun kendi yüzeyi: şerit çizgileri asfaltın ~5 cm üstünde, kaldırım/bordür kenarda ~15–30 cm; bunlar
                    # "üstten geçen yapı" değil (basamak denetimi ölçer). Üst katman ancak 0,5 m'den yüksekse sayılır.
                    if ad == "yol" and d < 0.5:
                        continue
                    n_ac += 1
                    K.ekle("aciklik", P[p, 0], P[p, 1], d, ad, y=yv)
    qi, qt, qy = sorgu(agac(TL), TL, P) if len(TL) else ([], [], [])
    n_dal = 0
    for p, t, yy in zip(qi, qt, qy):
        for yv in katman.get(int(p), []):
            if 0.05 < yy - yv < DAL:
                n_dal += 1
                K.ekle("dal", P[p, 0], P[p, 1], yy - yv, y=yv)
    print(f"  açıklık {n_ac}, dal {n_dal} ({time.time() - t0:.0f} sn)")

    # şeritler asfaltta
    Ab = A.buffer(0.05)
    shapely.prepare(Ab)
    n_serit = n_serit_kotu = 0
    for ln in yer["lanes"]:
        p3 = np.array(ln["points"], float).reshape(-1, 3)
        pts = p3[:, [0, 2]]
        for si_, (a_, b_) in enumerate(zip(pts[:-1], pts[1:])):
            pa = (p3[si_], p3[si_ + 1])
            L = np.linalg.norm(b_ - a_)
            if L < 1e-6:
                continue
            u = (b_ - a_) / L
            sg = np.array([u[1], -u[0]])
            for s in np.arange(0, L, 1.0):
                q = a_ + u * s
                n_serit += 1
                ok = all(shapely.contains_xy(Ab, *(q + sg * o)) for o in (0.0, SERIT_PAY, -SERIT_PAY))
                if not ok:
                    n_serit_kotu += 1
                    K.ekle("serit", q[0], q[1], None, ln["name"], y=float(np.interp(s, [0, L], [pa[0][1], pa[1][1]])))
    print(f"  şerit {n_serit_kotu}/{n_serit} ({time.time() - t0:.0f} sn)")

    # kesik: araç yolu koridor sınırını kesiyor
    kor = geo["koridor"].boundary
    n_kesik = 0
    Akes = geo["A"].intersection(kor.buffer(1.0))
    for g in (Akes.geoms if hasattr(Akes, "geoms") else [Akes]):
        if g.is_empty or g.area < 4:
            continue
        c = g.centroid
        n_kesik += 1
        K.ekle("kesik", c.x, c.y, None)
    print(f"  kesik {n_kesik} ({time.time() - t0:.0f} sn)")

    # köşe yükseklikleri
    n_kose = 0
    for f in glob.glob(os.path.join(kok.ISLEM, "parcalar", "*.pkl")):
        p = pickle.load(open(f, "rb"))
        for m, (V, UV, F) in p.items():
            kotu = (V[:, 1] < -200) | (V[:, 1] > 400)
            if kotu.any():
                n_kose += int(kotu.sum())
                c = V[kotu].mean(axis=0)
                K.ekle("kose", c[0], c[2], float(V[kotu, 1].min()), f"{os.path.basename(f)} {m} {int(kotu.sum())} köşe")
    print(f"  köşe {n_kose} ({time.time() - t0:.0f} sn)")

    # yerleşim öğeleri: tabanın altındaki yüzey (yol, yol diğer, zemin)
    n_nesne = 0
    IP = np.array([[it["pos"][0], it["pos"][2]] for it in yer["items"]], float)
    if len(IP):
        yuz = {}
        for T, tr in ((TY, agY), (TD, agD), (TZ, agZ)):
            qi, qt, qy = sorgu(tr, T, IP)
            for p, yy in zip(qi, qy):
                yuz.setdefault(int(p), []).append(float(yy))
        for i, it in enumerate(yer["items"]):
            ys = yuz.get(i)
            if not ys:
                continue
            yv = it["pos"][1]
            d = min(ys, key=lambda v: abs(v - yv)) - yv
            if abs(d) > 0.10:
                n_nesne += 1
                K.ekle("nesne", it["pos"][0], it["pos"][2], abs(d), f"{it['model']} ({'havada' if d < 0 else 'gömülü'} {abs(d):.2f} m)", y=yv)
    print(f"  nesne {n_nesne} ({time.time() - t0:.0f} sn)")

    # kümeler: en yakın sokak ve durak
    kumeler = K.kumeler()
    seritler = geo["seritler"]
    d = np.load(os.path.join(kok.ISLEM, "arazi.npz"))
    PX = d["dugum_xz"]
    hatlar = [LineString(PX[s["d"]]) for s in seritler]
    tr_yol = STRtree(hatlar)
    duraklar = []
    for h in kok.HATLAR.values():
        f = os.path.join(a.maps, h["klasor"], h["dosya"])
        if os.path.exists(f):
            duraklar += [(s["name"], s["pos"][0], s["pos"][2]) for s in json.load(open(f, encoding="utf-8"))["stops"]]
    from scipy.spatial import cKDTree
    kat_i = np.array(sorted(katman.keys()))
    kd = cKDTree(P[kat_i])
    for k in kumeler:
        if k["y"] is None:          # kesik, köşe: en yakın yol yüzeyi
            _, j = kd.query([k["x"], k["z"]])
            k["y"] = round(float(katman[int(kat_i[j])][0]), 1)
        pt = Point(k["x"], k["z"])
        j = tr_yol.nearest(pt)
        k["sokak"] = seritler[j]["ad"] or seritler[j]["sinif"]
        dn = min(duraklar, key=lambda s: (s[1] - k["x"]) ** 2 + (s[2] - k["z"]) ** 2)
        k["durak"] = f"{dn[0]}+{math.hypot(dn[1] - k['x'], dn[2] - k['z']):.0f}m"
        k["guzergah"] = round(float(geo["guzergah"].distance(pt)), 1)
    sira = {"aciklik": 0, "delik": 1, "basamak": 2, "kesik": 3, "serit": 4, "egim": 5, "nesne": 6, "dal": 7, "kose": 8}
    kumeler.sort(key=lambda k: (sira.get(k["tur"], 9), k["guzergah"]))

    os.makedirs(os.path.dirname(a.cikti), exist_ok=True)
    say = {}
    for k in kumeler:
        say[k["tur"]] = say.get(k["tur"], 0) + 1
    ozet = (f"Harita denetimi ({time.strftime('%Y-%m-%d %H:%M')}): örnek {len(P)} nokta (2 m), şerit örneği {n_serit}\n"
            f"nokta sayıları: delik {len(delik)}, basamak {n_basamak}, eğim>%{EGIM * 100:.0f} {n_egim}, açıklık<{ACIKLIK} m {n_ac}, "
            f"dal<{DAL} m {n_dal}, şerit asfalt dışı {n_serit_kotu}/{n_serit}, kesik yol ucu {n_kesik}, uç köşe {n_kose}, nesne {n_nesne}\n"
            f"kümeler: " + ", ".join(f"{t} {n}" for t, n in sorted(say.items(), key=lambda x: sira.get(x[0], 9))) + "\n")
    with open(a.cikti + ".txt", "w", encoding="utf-8") as fh:
        fh.write(ozet + "\n")
        fh.write("tür | nokta | değer | konum (x, z) | güzergâha m | sokak | en yakın durak | ek\n")
        for k in kumeler:
            fh.write(f"{k['tur']} | {k['n']} | {k['deger']} | ({k['x']:.0f}, {k['z']:.0f}) | {k['guzergah']:.0f} | {k['sokak']} | "
                     f"{k['durak']} | {k['ek'] or ''}\n")
    json.dump({"ozet": ozet, "kumeler": kumeler}, open(a.cikti + ".json", "w", encoding="utf-8"), ensure_ascii=False, indent=0)
    print(ozet)
    print(f"  → {a.cikti}.txt ({time.time() - t0:.0f} sn)")


if __name__ == "__main__":
    main()
