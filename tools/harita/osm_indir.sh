#!/bin/bash
# OSM karoları: api.openstreetmap.org /api/0.6/map, 0,01° karolar.
# Kullanım: tools/harita/osm_indir.sh <karo klasörü> [güney kuzey batı doğu]   (sonra osm_birlestir.py)
# Varsayılan: enlem 39.865–39.985, boylam 32.820–32.910 (Kızılay → Güneşevler ve Kızılay → Atakule koridorları).
# Karo adı sol alt köşe: t_<enlem>_<boylam>.osm; olan karo yeniden indirilmez.
set -e
out=${1:?karo klasörü}; G=${2:-39.865}; K=${3:-39.985}; B=${4:-32.820}; D=${5:-32.910}
mkdir -p "$out"; cd "$out"
for la in $(python3 -c "print(' '.join(f'{x/1000:.3f}' for x in range(round($G*1000), round($K*1000), 10)))"); do
for lo in $(python3 -c "print(' '.join(f'{x/1000:.3f}' for x in range(round($B*1000), round($D*1000), 10)))"); do
  f="t_${la}_${lo}.osm"; [ -s "$f" ] && continue
  la2=$(python3 -c "print(round($la+0.01,3))"); lo2=$(python3 -c "print(round($lo+0.01,3))")
  for i in 1 2 3 4; do
    code=$(curl -s -m 180 -o "$f.tmp" -w '%{http_code}' -A 'AnkaraBusSim/1.0 (egitsel oyun)' \
      "https://api.openstreetmap.org/api/0.6/map?bbox=$lo,$la,$lo2,$la2")
    [ "$code" = 200 ] && { mv "$f.tmp" "$f"; break; }
    echo "tekrar $f $code"; sleep $((i*5))
  done
  echo "$f $(stat -f%z "$f" 2>/dev/null || stat -c%s "$f")"
done; done
