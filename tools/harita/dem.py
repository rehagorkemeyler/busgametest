"""
Yükseklik modeli: AWS "elevation-tiles-prod" terrarium karoları (SRTM/Copernicus karışımı, ~30 m) → enlem/boylamdan
rakım. Karolar bir klasörde: <z>_<x>_<y>.png.

    dem = Dem(klasor, z=15); dem.rakim(lat, lon)  # m, çift doğrusal
İndirme:
    python dem.py --indir <klasör> --kuzey 39.985 --guney 39.905 --bati 32.835 --dogu 32.920
"""
import glob
import math
import os

import numpy as np
from PIL import Image


class Dem:
    # terrarium'da "veri yok" siyah piksel (−32 768 m); z15 ve z14'te Ankara'nın kuzeyinde (39.985 K) bütün bir karo satırı boş,
    # bazı karolarda tek tük bozuk piksel var. Bu eşiğin altındaki değerler geçersiz: bir alt yakınlaştırmadan örneklenir.
    GECERSIZ = 300.0

    def __init__(self, klasor, z=15, yedek=(14, 13)):
        self.z = z
        self.karolar = {}
        for f in glob.glob(os.path.join(klasor, f"{z}_*_*.png")):
            _, x, y = os.path.basename(f)[:-4].split("_")
            a = np.asarray(Image.open(f).convert("RGB"), dtype=np.float64)
            self.karolar[(int(x), int(y))] = a[:, :, 0] * 256 + a[:, :, 1] + a[:, :, 2] / 256 - 32768
        self.yedek = None
        if yedek and glob.glob(os.path.join(klasor, f"{yedek[0]}_*_*.png")):
            self.yedek = Dem(klasor, yedek[0], yedek[1:])

    def _piksel(self, lat, lon):
        n = 2 ** self.z * 256
        px = (lon + 180) / 360 * n
        py = (1 - math.asinh(math.tan(math.radians(lat))) / math.pi) / 2 * n
        return px, py

    def _deger(self, px, py):
        k = self.karolar.get((int(px // 256), int(py // 256)))
        if k is None:
            raise KeyError(f"DEM karosu yok: {int(px // 256)}, {int(py // 256)}")
        return k[int(py % 256), int(px % 256)]

    def rakim(self, lat, lon):
        px, py = self._piksel(lat, lon)
        px -= 0.5
        py -= 0.5
        x0, y0 = math.floor(px), math.floor(py)
        fx, fy = px - x0, py - y0
        try:
            v00 = self._deger(x0, y0)
            v10 = self._deger(x0 + 1, y0)
            v01 = self._deger(x0, y0 + 1)
            v11 = self._deger(x0 + 1, y0 + 1)
        except KeyError:
            if self.yedek is None:
                raise
            return self.yedek.rakim(lat, lon)
        if min(v00, v10, v01, v11) < self.GECERSIZ and self.yedek is not None:
            return self.yedek.rakim(lat, lon)
        return (v00 * (1 - fx) + v10 * fx) * (1 - fy) + (v01 * (1 - fx) + v11 * fx) * fy


def karo(lat, lon, z):
    n = 2 ** z
    return int((lon + 180) / 360 * n), int((1 - math.asinh(math.tan(math.radians(lat))) / math.pi) / 2 * n)


if __name__ == "__main__":
    import argparse
    import urllib.request
    ap = argparse.ArgumentParser()
    ap.add_argument("--indir", required=True)
    ap.add_argument("--kuzey", type=float, required=True)
    ap.add_argument("--guney", type=float, required=True)
    ap.add_argument("--bati", type=float, required=True)
    ap.add_argument("--dogu", type=float, required=True)
    ap.add_argument("--z", type=int, default=15)
    a = ap.parse_args()
    os.makedirs(a.indir, exist_ok=True)
    x0, y0 = karo(a.kuzey, a.bati, a.z)
    x1, y1 = karo(a.guney, a.dogu, a.z)
    for x in range(x0, x1 + 1):
        for y in range(y0, y1 + 1):
            f = os.path.join(a.indir, f"{a.z}_{x}_{y}.png")
            if not os.path.exists(f):
                urllib.request.urlretrieve(f"https://s3.amazonaws.com/elevation-tiles-prod/terrarium/{a.z}/{x}/{y}.png", f)
    print((x1 - x0 + 1) * (y1 - y0 + 1), "karo")
