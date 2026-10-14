"""
Üstten önizleme (PNG): yol çokgenleri, katmanlı parçalar, binalar, duraklar. Doğrulama içindir.
    python -m hat3.onizleme <x> <z> <yarıçap m> <çıktı.png> [piksel]
"""
import os
import pickle
import sys

import numpy as np
from PIL import Image, ImageDraw
from shapely.geometry import MultiPolygon, Polygon, box

from . import kok


def cokgenler(g):
    if g.is_empty:
        return []
    if isinstance(g, Polygon):
        return [g]
    if isinstance(g, MultiPolygon):
        return list(g.geoms)
    return [p for p in getattr(g, "geoms", []) if isinstance(p, Polygon)]


def ciz(x, z, r, out, px=1400, ekstra=None):
    geo = pickle.load(open(os.path.join(kok.ISLEM, "yollar_geo.pkl"), "rb"))
    k = px / (2 * r)
    im = Image.new("RGB", (px, px), (222, 214, 196))
    dr = ImageDraw.Draw(im)
    pen = box(x - r, z - r, x + r, z + r)

    def P(pts):
        return [((a - (x - r)) * k, ((z + r) - b) * k) for a, b in pts]

    def doldur(g, renk, cizgi=None):
        for pg in cokgenler(g.intersection(pen)):
            dr.polygon(P(pg.exterior.coords), fill=renk, outline=cizgi)
            for h in pg.interiors:
                dr.polygon(P(h.coords), fill=(222, 214, 196))

    doldur(geo["S"], (200, 200, 195), (150, 150, 150))
    for rf in geo["refuj"]:
        doldur(rf, (140, 185, 120))
    doldur(geo["A"], (85, 85, 90))
    for kp in geo["katmanli"]:
        renk = (230, 140, 40) if kp["fark"].max() > 0.3 else (60, 120, 220)
        dr.line(P(kp["xz"]), fill=renk, width=max(2, int(kp["genislik"] * k)))
    bpath = os.path.join(kok.ISLEM, "binalar.pkl")
    if os.path.exists(bpath):
        for b in pickle.load(open(bpath, "rb")):
            pg = b["taban"]
            if pg.intersects(pen):
                dr.polygon(P(pg.exterior.coords), fill=(170, 120, 100), outline=(110, 70, 60))
    g = kok.guzergah()
    dr.line(P([kok.yerel(*p) for p in g["yol"]]), fill=(255, 0, 255), width=2)
    for dz in g["duraklar"]:
        a, b = kok.yerel(dz["lat"], dz["lon"])
        (u, v), = P([(a, b)])
        dr.rectangle((u - 5, v - 5, u + 5, v + 5), fill=(255, 0, 0))
        dr.text((u + 7, v - 6), dz["ad"], fill=(150, 0, 0))
    if ekstra:
        ekstra(dr, P)
    im.save(out)


if __name__ == "__main__" and sys.argv[1] != "kamera":
    a = sys.argv[1:]
    ciz(float(a[0]), float(a[1]), float(a[2]), a[3], int(a[4]) if len(a) > 4 else 1400)


def kameralar(s_liste, yan=1.5, boy=2.4, ileri=12.0):
    """Güzergâhta s (m) noktalarından sürücü gözü kameraları: "x,y,z,yaw,pitch" (Unity; yaw kuzeyden saat yönü)."""
    import math
    from shapely.geometry import LineString
    from .yuzeyler import Yukseklik
    d = np.load(os.path.join(kok.ISLEM, "arazi.npz"))
    yh = Yukseklik(d["dugum_xz"], d["dugum_y"], d["dugum_fark"])
    g = kok.guzergah()
    G = LineString([kok.yerel(*p) for p in g["yol"]])
    out = []
    for s in s_liste:
        p, q = G.interpolate(s), G.interpolate(s + ileri)
        tx, tz = q.x - p.x, q.y - p.y
        L = math.hypot(tx, tz) or 1
        x, z = p.x + tz / L * yan, p.y - tx / L * yan        # sağa yan m (sağ şeritte)
        y = float(yh(x, z)[0]) + boy
        yaw = math.degrees(math.atan2(tx, tz))
        out.append(f"{x:.1f},{y:.1f},{z:.1f},{yaw:.1f},-2")
    return out


if __name__ == "__main__" and len(sys.argv) > 1 and sys.argv[1] == "kamera":
    print(" ".join(f'--kamera="{k}"' for k in kameralar([float(v) for v in sys.argv[2:]])))
