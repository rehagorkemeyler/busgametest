"""
SketchUp (.skp) → GLB. SketchUp 2021 ve sonrası (VFF) ile eski (2013–2020) dosyaları okur.

SketchUp ya da SketchUp SDK gerekmez; açık kaynak `openskp` paketini kullanır (MIT, tersine mühendislik).
Çıkan GLB'ler milimetre ölçeğinde, Y yukarıdır. Oyuna hazırlama işini `tools/blender/skp_donustur.py` yapar.

Kurulum (bir kere, ayrı bir sanal ortamda):
    python3 -m venv .venv-skp
    .venv-skp/bin/pip install openskp pillow

Kullanım:
    .venv-skp/bin/python tools/skp/skp_to_glb.py --out <glb klasörü> dosya1.skp [dosya2.skp ...]
    .venv-skp/bin/python tools/skp/skp_to_glb.py --out <glb klasörü> --as kizi.skp=KizilayAVM ...

`--as kaynak=ad` verilmezse GLB adı SKP dosyasının adı olur.
"""
import argparse
import io
import os
import sys

from PIL import Image
from openskp import SkpFile, _core
from openskp.export import glb

_extract_texture = _core._extract_texture


def _extract_texture_any_format(*args, **kwargs):
    """openskp yalnızca PNG/JPEG dokuları taşır; SketchUp'a sürüklenmiş .psd/.bmp/.tif/.tga fotoğraflar
    (ör. Emek İşhanı'nın cephe ve tabela fotoğrafları) sessizce düşer. Bunları PNG'ye çevirir."""
    tex = _extract_texture(*args, **kwargs)
    data = tex.get("data") if tex else None
    if data and not (data[:3] == b"\xff\xd8\xff" or data[:8] == b"\x89PNG\r\n\x1a\n"):
        try:
            im = Image.open(io.BytesIO(data))
            im.load()
            buf = io.BytesIO()
            im.convert("RGBA" if "A" in im.getbands() else "RGB").save(buf, "PNG")
            tex["data"] = buf.getvalue()
            print(f"  doku PNG'ye çevrildi: {tex.get('filename', '?')} ({im.format}, {im.size[0]}×{im.size[1]})")
        except Exception as exc:  # okunamayan format: openskp'nin davranışı (yalnızca renk) kalır
            print(f"  doku okunamadı: {tex.get('filename', '?')}: {exc}")
    return tex


_core._extract_texture = _extract_texture_any_format


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True)
    ap.add_argument("--as", dest="names", action="append", default=[],
                    help="kaynak.skp=çıkış_adı (birden çok verilebilir)")
    ap.add_argument("skp", nargs="+")
    args = ap.parse_args()
    os.makedirs(args.out, exist_ok=True)
    rename = dict(n.split("=", 1) for n in args.names)

    for path in args.skp:
        name = rename.get(os.path.basename(path), os.path.splitext(os.path.basename(path))[0])
        skp = SkpFile.open(path)
        model = skp.parse()
        dst = os.path.join(args.out, name + ".glb")
        glb.export(skp, dst, textures=True)
        # openskp GLB'nin yanına metadata ve küçük resim de yazar; yalnızca GLB lazım
        for extra in ("_metadata.json", "_thumbnail.png"):
            p = os.path.join(args.out, name + extra)
            if os.path.exists(p):
                os.remove(p)
        print(f"{path} → {dst}  (SketchUp {model.version}, {len(model.definitions)} bileşen, "
              f"{len(model.materials)} malzeme)")


if __name__ == "__main__":
    sys.exit(main())
