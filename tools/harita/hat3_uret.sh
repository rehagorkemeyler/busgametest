#!/bin/bash
# Hat 3 (EGO 408 Kızılay → Güneşevler) haritasını baştan üretir. docs/PLAN_HAT3_408.md
# Gerekenler: Blender python'u (bpy) + numpy, scipy, shapely, mapbox_earcut, pillow.
#   PY=<bpy'li python> tools/harita/hat3_uret.sh
# Veri tools/harita/veri altında (OSM, DEM, güzergâh); yeniden indirmek için osm_indir.sh, dem.py --indir.
set -e
PY=${PY:-python3}
KOK=$(cd "$(dirname "$0")/../.." && pwd)
MAPS="$KOK/AnkaraBusSimulator/Assets/_Project/Maps"
cd "$KOK/tools/harita"
$PY -m hat3.arazi
$PY -m hat3.yollar
$PY -m hat3.yuzeyler
$PY -m hat3.dokular --out "$MAPS/Hat3/Textures"
$PY -m hat3.simgeler          # binalardan önce: yerine model konan binalar (simge_haric.json)
$PY -m hat3.binalar
$PY -m hat3.agaclar
$PY -m hat3.ufuk
$PY -m hat3.yerlesim --maps "$MAPS"
rm -f "$MAPS/Hat3/Parcalar/"*.fbx
$PY "$KOK/tools/blender/harita_hat3.py" -- --fbx "$MAPS/Hat3"
