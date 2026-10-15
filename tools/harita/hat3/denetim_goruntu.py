"""
Denetim kümelerinin görüntüleri (docs/PLAN_HARITA_V2.md, M1): denetim.json'daki her türün en büyük kümeleri için Blender'da
iki kamera — havadan eğik (35 m) ve sürücü gözü (2,6 m, kümeye 25 m'den). Blender'ı çağırır (harita_hat3.py --render, EEVEE).

Kullanım: python -m hat3.denetim_goruntu --json docs/gorseller/harita_v2/denetim_m1.json --out docs/gorseller/harita_v2/m1 [--tur-basina 6]
"""
import argparse
import json
import math
import os
import subprocess

from . import kok

BLENDER = os.environ.get("BLENDER", "/Applications/Blender.app/Contents/MacOS/Blender")


def kamera(x, y, z, kx, ky, kz):
    """(x,y,z)'den (kx,ky,kz)'ye bakan Unity yaw/pitch kamera dizgesi."""
    dx, dy, dz = kx - x, ky - y, kz - z
    yaw = math.degrees(math.atan2(dx, dz))
    pitch = math.degrees(math.atan2(dy, math.hypot(dx, dz)))
    return f"{x:.1f},{y:.1f},{z:.1f},{yaw:.1f},{pitch:.1f}"


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--json", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--tur-basina", type=int, default=6)
    ap.add_argument("--turler", default="aciklik,basamak,egim,serit,kesik,kose,nesne,dal")
    a = ap.parse_args()
    d = json.load(open(a.json, encoding="utf-8"))
    secim = []
    for tur in a.turler.split(","):
        ks = sorted([k for k in d["kumeler"] if k["tur"] == tur], key=lambda k: -k["n"])[:a.tur_basina]
        secim += ks
    kameralar, adlar, liste = [], [], []
    for i, k in enumerate(secim):
        x, z, y = k["x"], k["z"], k["y"] if k.get("y") is not None else 0.0
        if y < -150:
            y = 0.0
        ad = f"{k['tur']}_{i:02d}"
        kameralar.append(kamera(x - 25, y + 35, z - 25, x, y, z))
        adlar.append(ad + "_hava")
        kameralar.append(kamera(x - 18, y + 2.6, z - 18, x, y + 1.0, z))
        adlar.append(ad + "_yer")
        liste.append(f"{ad}: {k['tur']} n={k['n']} değer={k['deger']} ({x:.0f}, {y:.1f}, {z:.0f}) {k['sokak']} / {k['durak']} {k.get('ek') or ''}")
    os.makedirs(a.out, exist_ok=True)
    with open(os.path.join(a.out, "liste.txt"), "w", encoding="utf-8") as fh:
        fh.write("\n".join(liste) + "\n")
    kok_ = os.path.abspath(os.path.join(kok.KLASOR, "..", ".."))
    env = dict(os.environ, PYTHONPATH=os.path.join(kok_, "tools", "harita", ".venv", "blender_site"))
    # tek Blender çağrısında çok kamera sessizce düşüyor: 10'arlık gruplar
    for g in range(0, len(kameralar), 10):
        arg = [BLENDER, "--background", "--factory-startup", "--python-use-system-env", "--python",
               os.path.join(kok_, "tools", "blender", "harita_hat3.py"), "--", "--render", os.path.abspath(a.out),
               "--motor", "eevee", "--yaricap", "450", "--genislik", "960",
               "--dokular", os.environ.get("HARITA_DOKULAR", os.path.join(kok_, "AnkaraBusSimulator", "Assets", "_Project", "Maps", "Hat3", "Textures")),
               "--adlar", ",".join(adlar[g:g + 10])]
        for k in kameralar[g:g + 10]:
            arg.append(f"--kamera={k}")   # eksi değer seçenek sanılmasın
        r = subprocess.run(arg, env=env, capture_output=True, text=True)
        hata = [l for l in (r.stdout + r.stderr).splitlines() if "Error" in l or "Traceback" in l]
        if r.returncode or hata:
            print(f"grup {g}: çıkış {r.returncode}", "\n".join(hata[-10:]))
    print(f"{len(adlar)} görüntü → {a.out}")


if __name__ == "__main__":
    main()
