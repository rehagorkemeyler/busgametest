"""
Güzergâh önizlemesi: hattın sürücü gözünden (ve isteğe bağlı havadan) görüntüleri, Blender EEVEE (harita_hat3.py --render).
Kamera güzergâh şeridinin (yerleşim JSON'undaki Gidis_1 şeritleri) üstünde 2,6 m, 40 m ileriye bakar.

Kullanım:
    python -m hat3.guzergah_goruntu --maps <Maps> --hat 408_gunesevler --out <klasör> [--her 250] [--s 560,1250,...] [--hava]
"""
import argparse
import json
import math
import os
import subprocess

import numpy as np

from . import kok

BLENDER = os.environ.get("BLENDER", "/Applications/Blender.app/Contents/MacOS/Blender")


def kamera(a, b):
    dx, dy, dz = b - a
    return f"{a[0]:.1f},{a[1]:.1f},{a[2]:.1f},{math.degrees(math.atan2(dx, dz)):.1f},{math.degrees(math.atan2(dy, math.hypot(dx, dz))):.1f}"


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--maps", required=True)
    ap.add_argument("--hat", required=True, choices=sorted(kok.HATLAR))
    ap.add_argument("--out", required=True)
    ap.add_argument("--her", type=float, default=0.0, help="her N m'de bir")
    ap.add_argument("--s", default="", help="virgülle s değerleri (m)")
    ap.add_argument("--hava", action="store_true")
    ap.add_argument("--genislik", type=int, default=960)
    a = ap.parse_args()
    h = kok.HATLAR[a.hat]
    yer = json.load(open(os.path.join(a.maps, h["klasor"], h["dosya"]), encoding="utf-8"))
    on = "" if a.hat == "408_gunesevler" else "A_"
    seritler = sorted([l for l in yer["lanes"] if l["name"].startswith(on + "Gidis_1_")], key=lambda l: l["name"])
    P = np.concatenate([np.array(l["points"], float).reshape(-1, 3) for l in seritler])
    d = np.concatenate([[0], np.cumsum(np.linalg.norm(np.diff(P[:, [0, 2]], axis=0), axis=1))])
    ss = [float(v) for v in a.s.split(",") if v] or list(np.arange(20, d[-1], a.her or 250))
    adlar, kameralar = [], []
    for s in ss:
        i = int(np.searchsorted(d, s))
        j = min(int(np.searchsorted(d, s + 40)), len(P) - 1)
        i = min(i, len(P) - 2)
        kameralar.append(kamera(P[i] + [0, 2.6, 0], P[j] + [0, 1.5, 0]))
        adlar.append(f"{a.hat}_s{int(s):05d}_yer")
        if a.hava:
            f = P[j] - P[i]
            f /= np.linalg.norm(f) or 1
            kameralar.append(kamera(P[i] - f * 50 + [0, 45, 0], P[i] + f * 60))
            adlar.append(f"{a.hat}_s{int(s):05d}_hava")
    os.makedirs(a.out, exist_ok=True)
    kok_ = os.path.abspath(os.path.join(kok.KLASOR, "..", ".."))
    env = dict(os.environ, PYTHONPATH=os.path.join(kok_, "tools", "harita", ".venv", "blender_site"))
    for g in range(0, len(kameralar), 10):
        arg = [BLENDER, "--background", "--factory-startup", "--python-use-system-env", "--python",
               os.path.join(kok_, "tools", "blender", "harita_hat3.py"), "--", "--render", os.path.abspath(a.out),
               "--motor", "eevee", "--yaricap", "450", "--genislik", str(a.genislik),
               "--dokular", os.environ.get("HARITA_DOKULAR", os.path.join(a.maps, "Hat3", "Textures")),
               "--adlar", ",".join(adlar[g:g + 10])] + [f"--kamera={k}" for k in kameralar[g:g + 10]]
        r = subprocess.run(arg, env=env, capture_output=True, text=True)
        if r.returncode:
            print(f"grup {g}: çıkış {r.returncode}", (r.stdout + r.stderr)[-800:])
    print(f"{len(adlar)} görüntü → {a.out} (güzergâh {d[-1]:.0f} m)")


if __name__ == "__main__":
    main()
