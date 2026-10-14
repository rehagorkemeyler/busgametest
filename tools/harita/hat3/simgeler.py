"""
A4 — Simge yapılar.

1. Hazır modeller gerçek konumlarına (Unity'de prefab olarak; yerleşim JSON'unda "items"):
   SKP Emek İşhanı, Kızılay AVM, Güvenlik Anıtı (OSM ayak izine: ön yüz en yakın caddeye), Zafer Anıtı (Ulus Meydanı),
   Hitit Güneş Kursu (Sıhhiye), Opera binası (Opera alanındaki en büyük binanın yerine). Yerine konan OSM binaları
   üretilmez (simge_haric.json → binalar.py, ufuk.py).
2. Üretilen ağlar (SimgeYapilar grubu, parça başına):
   - su yüzeyleri (Gençlik Parkı, Altınpark gölleri…): H3_Su + taş kenar
   - Ankara Kalesi surları (OSM city_wall): 10 m taş duvar, 40 m'de bir burç
   - Mareşal Atatürk Anıtı (Kızılay, Zafer Meydanı): kaide + stilize figür
   - metro girişleri (güzergâhtan ≤ 80 m): cam saçak + merdiven boşluğu
   - yaya üst geçitleri (güzergâhı kesen köprülü yaya yolları): tabliye, korkuluk, iki uçta merdiven
Çıktı: <ISLEM>/parcalar/simge_<i>_<j>.pkl, <ISLEM>/simge_items.json, <ISLEM>/simge_haric.json
Kullanım: python -m hat3.simgeler
"""
import json
import math
import os
import pickle

import numpy as np
import shapely
from shapely.geometry import LineString, Point, Polygon

from . import kok
from .binalar import Atlas, cati_uv
from .mesh import Parcalar, anahtar, serit, ucgenle, yonlu_duvar
from .yuzeyler import SonArazi, Yukseklik

# (model, OSM kaynağı ("way", id) / ("node", id), yerleşim)
HAZIR = [
    ("SKP/Landmarks/EmekIshani", ("way", 473688392), "on"),
    ("SKP/Landmarks/KizilayAVM", ("way", 427335497), "on"),
    ("SKP/Landmarks/GuvenlikAniti", ("way", 638950300), "on"),
    ("Landmarks/ZaferAniti", ("node", 2726221369), "merkez"),
    ("Landmarks/HititGunesKursu", ("node", 2726234695), "merkez"),
    ("Landmarks/OperaBinasi", ("alan", 404878536), "on"),
]
ATATURK_ANITI = 9396947281


class Simgeler:
    def __init__(self):
        self.o = kok.osm()
        self.ar = SonArazi()
        self.pc = Parcalar()
        self.A = Atlas()
        self.items = []
        self.haric = set()
        geo = __import__("pickle").load(open(os.path.join(kok.ISLEM, "yollar_geo.pkl"), "rb"))
        self.geo = geo
        self.guz = geo["guzergah"]
        d = np.load(os.path.join(kok.ISLEM, "arazi.npz"))
        self.yh = Yukseklik(d["dugum_xz"], d["dugum_y"], d["dugum_fark"])

    def pg(self, wid):
        w = self.o["ways"][wid]
        return Polygon([self.o["xy"][n] for n in w["n"] if n in self.o["xy"]]).buffer(0)

    def dortgen(self, mal, a, b, ya, yb, uv, bak=None):
        k = anahtar((a[0] + b[0]) / 2, (a[1] + b[1]) / 2)
        if bak is None:
            V = [(a[0], ya, a[1]), (b[0], ya, b[1]), (b[0], yb, b[1]), (a[0], yb, a[1])]
            self.pc.ag(k, mal).ekle(V, uv, [(0, 1, 2), (0, 2, 3)])
        else:
            V, _, F = yonlu_duvar(a, b, ya, ya, yb, yb, bak)
            self.pc.ag(k, mal).ekle(V, uv, F)

    def kutu(self, mal, x, z, w, d, ya, yb, aci, uv_yan, uv_ust):
        ca, sa = math.cos(aci), math.sin(aci)
        P = [(x + ca * px - sa * pz, z + sa * px + ca * pz) for px, pz in
             ((-w / 2, -d / 2), (w / 2, -d / 2), (w / 2, d / 2), (-w / 2, d / 2))]
        pg = shapely.geometry.polygon.orient(Polygon(P), 1.0)
        c = list(pg.exterior.coords)
        for a, b in zip(c[:-1], c[1:]):
            self.dortgen(mal, a, b, ya, yb, uv_yan)
        xz, tri = ucgenle(pg)
        V = np.stack([xz[:, 0], np.full(len(xz), yb), xz[:, 1]], 1)
        self.pc.ag(anahtar(x, z), mal).ekle(V, [uv_ust] * len(xz), tri)

    # ------------------------------------------------------------ hazır modeller
    def hazir(self):
        bina_agac = []
        for model, (tur, kimlik), yer in HAZIR:
            if tur == "node":
                x, z = self.o["xy"][kimlik]
                y = float(self.ar.y(x, z)[0])
                # ön yüz en yakın güzergâh noktasına
                p = self.guz.interpolate(self.guz.project(Point(x, z)))
                rot = math.degrees(math.atan2(p.x - x, p.y - z))
                self.items.append({"model": model, "pos": [round(x, 2), round(y, 2), round(z, 2)], "rotY": round(rot % 360, 1),
                                   "group": "SimgeYapilar"})
                continue
            if tur == "alan":
                alan = self.pg(kimlik)
                adaylar = [(wid, self.pg(wid)) for wid, w in self.o["ways"].items() if "building" in w["t"]
                           and alan.contains(Polygon([self.o["xy"][n] for n in w["n"] if n in self.o["xy"]]).centroid)]
                kimlik, pg = max(adaylar, key=lambda a: a[1].area)
            else:
                pg = self.pg(kimlik)
            r = pg.minimum_rotated_rectangle
            c = np.asarray(r.exterior.coords)[:4]
            mer = c.mean(axis=0)
            hedef = self.guz.interpolate(self.guz.project(Point(*mer)))
            yon = np.array([hedef.x, hedef.y]) - mer
            en_iyi = None
            for i in range(4):
                a, b = c[i], c[(i + 1) % 4]
                orta = (a + b) / 2
                n = orta - mer
                ln = np.linalg.norm(n)
                n = n / ln
                puan = np.dot(n, yon / (np.linalg.norm(yon) or 1))
                if en_iyi is None or puan > en_iyi[0]:
                    en_iyi = (puan, orta, n)
            _, orta, n = en_iyi
            ys = self.ar.y(c[:, 0], c[:, 1])
            rot = math.degrees(math.atan2(n[0], n[1]))
            self.items.append({"model": model, "pos": [round(orta[0], 2), round(float(ys.min()), 2), round(orta[1], 2)],
                               "rotY": round(rot % 360, 1), "group": "SimgeYapilar"})
            self.haric.add(kimlik)
            bina_agac.append(pg.buffer(2))
        # yerine model konan binaların içindeki küçük OSM binaları da (bölümler) çizilmesin
        for wid, w in self.o["ways"].items():
            if "building" in w["t"] and wid not in self.haric:
                ns = [self.o["xy"][n] for n in w["n"] if n in self.o["xy"]]
                if len(ns) >= 3 and any(b.contains(Point(np.mean(ns, axis=0))) for b in bina_agac):
                    self.haric.add(wid)

    # ------------------------------------------------------------ su
    def su(self):
        kapsam = self.guz.buffer(2000)
        sayi = 0
        for wid, w in self.o["ways"].items():
            t = w["t"]
            if t.get("natural") != "water" and t.get("leisure") != "swimming_pool" and t.get("water") is None:
                continue
            if t.get("tunnel") or t.get("covered") == "yes" or t.get("location") == "underground":
                continue
            ns = [self.o["xy"][n] for n in w["n"] if n in self.o["xy"]]
            if len(ns) < 4 or ns[0] != ns[-1]:
                continue
            pg = shapely.geometry.polygon.orient(Polygon(ns).buffer(0), 1.0) if Polygon(ns).buffer(0).geom_type == "Polygon" else None
            if pg is None or pg.area < 15 or not kapsam.intersects(pg):
                continue
            c = np.asarray(pg.exterior.coords)
            y = float(self.ar.y(c[:, 0], c[:, 1]).min()) + 0.05
            xz, tri = ucgenle(pg)
            if len(tri) == 0:
                continue
            V = np.stack([xz[:, 0], np.full(len(xz), y), xz[:, 1]], 1)
            k = anahtar(*pg.representative_point().coords[0])
            self.pc.ag(k, "H3_Su@SimgeYapilar").ekle(V, xz * 0.1, tri)
            # taş kenar (dışa bakan 0,4 m)
            for a, b in zip(c[:-1], c[1:]):
                n = np.array([b[1] - a[1], -(b[0] - a[0])])
                self.dortgen("H3_Bordur@SimgeYapilar", a, b, y - 0.3, y + 0.35, [(0, 0), (1, 0), (1, 0.3), (0, 0.3)], tuple(n))
            sayi += 1
        print(f"  su yüzeyi {sayi}")

    # ------------------------------------------------------------ Ankara Kalesi
    def kale(self):
        tas = self.A.j["stiller"]["ulus_tas"][1]
        for wid, w in self.o["ways"].items():
            t = w["t"]
            if t.get("barrier") != "city_wall" and t.get("historic") not in ("citywalls", "castle"):
                continue
            ns = [self.o["xy"][n] for n in w["n"] if n in self.o["xy"]]
            if len(ns) < 2:
                continue
            L = LineString(ns)
            if L.distance(self.guz) > 2000:
                continue
            adim = 6.0
            pts = [L.interpolate(s) for s in np.arange(0, L.length + 0.01, adim)]
            xz = np.array([(p.x, p.y) for p in pts])
            y = self.ar.y(xz[:, 0], xz[:, 1])
            for i in range(len(xz) - 1):
                a, b = xz[i], xz[i + 1]
                t_ = (b - a) / (np.linalg.norm(b - a) or 1)
                n = np.array([t_[1], -t_[0]])
                for yon in (1, -1):
                    aa, bb = a + n * 1.3 * yon, b + n * 1.3 * yon
                    yb = min(y[i], y[i + 1])
                    V, _, F = yonlu_duvar(aa, bb, yb - 3, yb - 3, yb + 10, yb + 10, n * yon)
                    self.pc.ag(anahtar(*a), "H3_Cephe@SimgeYapilar").ekle(V, self.A.uv(tas["D"], 0, 2, 0, 3), F)
            # burçlar
            for s in np.arange(10, L.length, 40.0):
                p = L.interpolate(s)
                q = L.interpolate(min(s + 1, L.length))
                aci = math.atan2(q.y - p.y, q.x - p.x)
                y0 = float(self.ar.y(p.x, p.y)[0])
                self.kutu("H3_Cephe@SimgeYapilar", p.x, p.y, 7.5, 7.5, y0 - 3, y0 + 14, aci,
                          self.A.uv(tas["D"], 0, 2, 0, 4), cati_uv(1, 0.5, 0.5).tolist())
        print("  kale surları")

    # ------------------------------------------------------------ Atatürk anıtı (Kızılay)
    def ataturk(self):
        if ATATURK_ANITI not in self.o["xy"]:
            return
        x, z = self.o["xy"][ATATURK_ANITI]
        y = float(self.ar.y(x, z)[0])
        p = self.guz.interpolate(self.guz.project(Point(x, z)))
        aci = math.atan2(p.y - z, p.x - x)
        tas = self.A.uv(self.A.j["temel"])
        bronz = self.A.uv(self.A.j["cami"]["kubbe"])
        self.kutu("H3_Cephe@SimgeYapilar", x, z, 4.2, 4.2, y - 0.5, y + 0.6, aci, tas, tas[0])
        self.kutu("H3_Cephe@SimgeYapilar", x, z, 2.6, 2.6, y + 0.6, y + 4.2, aci, tas, tas[0])
        # figür: bacaklar, gövde, baş (stilize, 3,4 m)
        yb = y + 4.2
        self.kutu("H3_Cephe@SimgeYapilar", x, z, 0.9, 0.6, yb, yb + 1.6, aci, bronz, bronz[0])
        self.kutu("H3_Cephe@SimgeYapilar", x, z, 1.1, 0.7, yb + 1.6, yb + 2.9, aci, bronz, bronz[0])
        self.kutu("H3_Cephe@SimgeYapilar", x, z, 0.45, 0.45, yb + 2.9, yb + 3.4, aci, bronz, bronz[0])
        print("  Atatürk anıtı (Kızılay)")

    # ------------------------------------------------------------ metro girişleri
    def metro(self):
        cam = self.A.uv(self.A.j["stiller"]["cam_ofis"][0]["P"])
        sayi = 0
        for nid, t in self.o["tagged_nodes"].items():
            if t.get("railway") != "subway_entrance" or nid not in self.o["xy"]:
                continue
            x, z = self.o["xy"][nid]
            p = Point(x, z)
            if self.guz.distance(p) > 80:
                continue
            q = self.guz.interpolate(self.guz.project(p))
            aci = math.atan2(q.y - z, q.x - x) + math.pi / 2
            y = float(self.ar.y(x, z)[0]) + 0.15
            self.kutu("H3_Cephe@SimgeYapilar", x, z, 3.2, 6.5, y + 2.6, y + 2.9, aci, cam, cam[0])
            for dx in (-1.5, 1.5):
                ca, sa = math.cos(aci), math.sin(aci)
                self.kutu("H3_Cephe@SimgeYapilar", x + ca * dx, z + sa * dx, 0.12, 6.5, y, y + 2.6, aci, cam, cam[0])
            sayi += 1
        print(f"  metro girişi {sayi}")

    # ------------------------------------------------------------ yaya üst geçitleri
    def ust_gecit(self):
        beton = [(0, 0), (1, 0), (1, 1), (0, 1)]
        A = self.geo["A"]
        sayi = 0
        for wid, w in self.o["ways"].items():
            t = w["t"]
            if t.get("highway") not in ("footway", "steps", "path", "pedestrian") or t.get("bridge") is None:
                continue
            ns = [self.o["xy"][n] for n in w["n"] if n in self.o["xy"]]
            if len(ns) < 2:
                continue
            L = LineString(ns)
            if L.distance(self.guz) > 3 or L.length < 12:
                continue
            xz = np.array([L.interpolate(s).coords[0] for s in np.linspace(0, L.length, max(3, int(L.length / 4)))])
            # tabliye: altındaki yolun en yüksek noktası + 5,6 m
            yol = self.yh(xz[:, 0], xz[:, 1])
            dy = float(yol.max()) + 5.6
            yy = np.full(len(xz), dy)
            V, UV, F = serit(xz, yy, 1.5, 1.5)
            self.pc.ekle_dunya("H3_Bordur@SimgeYapilar", V, UV, F)
            V, UV, F = serit(xz, yy - 0.8, 1.6, 1.6)
            self.pc.ekle_dunya("H3_Bordur@SimgeYapilar", V, UV, F[:, ::-1])
            tt = np.gradient(xz, axis=0)
            tt /= np.linalg.norm(tt, axis=1, keepdims=True)
            nn = np.stack([tt[:, 1], -tt[:, 0]], 1)
            for yon in (1, -1):
                kx = xz + nn * 1.6 * yon
                for i in range(len(xz) - 1):
                    V, _, F = yonlu_duvar(kx[i], kx[i + 1], dy - 0.8, dy - 0.8, dy + 1.2, dy + 1.2, nn[i] * yon)
                    self.pc.ekle_dunya("H3_Bordur@SimgeYapilar", V, beton, F)
            # iki uçta merdiven/asansör kulesi (Ankara üst geçitlerinde olduğu gibi), asfaltın dışında
            for uc, ic in ((0, 1), (-1, -2)):
                a = xz[uc]
                yonv = a - xz[ic]
                yonv /= np.linalg.norm(yonv)
                for ek in np.arange(1.8, 12.0, 1.0):
                    c = a + yonv * ek
                    if not shapely.contains_xy(A, c[0], c[1]):
                        break
                yg = float(self.ar.y(c[0], c[1])[0])
                aci = math.atan2(yonv[1], yonv[0])
                self.kutu("H3_Bordur@SimgeYapilar", c[0], c[1], 3.4, 3.4, yg - 0.5, dy + 2.6, aci, beton, (0.5, 0.5))
                # kuleyle tabliye arası köprü
                V, UV, F = serit(np.array([a, c]), np.array([dy, dy]), 1.5, 1.5)
                self.pc.ekle_dunya("H3_Bordur@SimgeYapilar", V, UV, F)
            sayi += 1
        print(f"  yaya üst geçidi {sayi}")


def main():
    s = Simgeler()
    s.hazir()
    s.su()
    s.kale()
    s.ataturk()
    s.metro()
    s.ust_gecit()
    klasor = os.path.join(kok.ISLEM, "parcalar")
    for f in os.listdir(klasor):
        if f.startswith("simge_"):
            os.remove(os.path.join(klasor, f))
    for (i, j), mals in s.pc.p.items():
        pickle.dump({m: a.dizi() for m, a in mals.items()}, open(os.path.join(klasor, f"simge_{i}_{j}.pkl"), "wb"))
    json.dump(s.items, open(os.path.join(kok.ISLEM, "simge_items.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    json.dump(sorted(s.haric), open(os.path.join(kok.ISLEM, "simge_haric.json"), "w"))
    print(f"{len(s.items)} hazır model, {len(s.haric)} OSM binası yerine; {len(s.pc.p)} parça")


if __name__ == "__main__":
    main()
