#!/bin/bash
# Haritayı bu makinede (macOS, Blender.app) baştan üretir: hat3_uret.sh'nin yerel sarmalayıcısı.
# İlk kurulum (bir kez):
#   python3 -m venv tools/harita/.venv
#   tools/harita/.venv/bin/pip install numpy scipy shapely mapbox_earcut pillow matplotlib
#   /Applications/Blender.app/Contents/Resources/5.2/python/bin/python3.13 -m pip install \
#       --target tools/harita/.venv/blender_site shapely mapbox_earcut scipy pillow --no-deps
# Kullanım: tools/harita/uret_yerel.sh [MAPS klasörü]   (varsayılan: Unity projesindeki Maps)
set -e
KOK=$(cd "$(dirname "$0")/../.." && pwd)
VENV="$KOK/tools/harita/.venv"
MAPS=${1:-"$KOK/AnkaraBusSimulator/Assets/_Project/Maps"}
BLENDER=${BLENDER:-/Applications/Blender.app/Contents/MacOS/Blender}
export HARITA_FONT="$VENV/lib/python3.9/site-packages/matplotlib/mpl-data/fonts/ttf/DejaVuSans-Bold.ttf"
PY="$VENV/bin/python"
cd "$KOK/tools/harita"
for m in arazi yollar yuzeyler; do $PY -m hat3.$m; done
$PY -m hat3.dokular --out "$MAPS/Hat3/Textures"
for m in simgeler binalar agaclar ufuk; do $PY -m hat3.$m; done
$PY -m hat3.yerlesim --maps "$MAPS"
rm -f "$MAPS/Hat3/Parcalar/"*.fbx
PYTHONPATH="$VENV/blender_site" "$BLENDER" --background --factory-startup --python-use-system-env \
    --python "$KOK/tools/blender/harita_hat3.py" -- --fbx "$MAPS/Hat3"
