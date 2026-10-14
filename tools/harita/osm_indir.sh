#!/bin/bash
# Hat 3 OSM karoları: api.openstreetmap.org /api/0.6/map, 0,01° karolar.
# Kullanım: tools/harita/osm_indir.sh <karo klasörü>   (sonra osm_birlestir.py)
# Enlem 39.915–39.985, boylam 32.840–32.910 (Kızılay → Güneşevler koridoru).
set -e
out=${1:?karo klasörü}; mkdir -p "$out"; cd "$out"
for la in $(seq 39.915 0.01 39.975); do for lo in $(seq 32.840 0.01 32.900); do
  f="t_${la}_${lo}.osm"; [ -s "$f" ] && continue
  la2=$(python3 -c "print(round($la+0.01,3))"); lo2=$(python3 -c "print(round($lo+0.01,3))")
  for i in 1 2 3 4; do
    code=$(curl -s -m 180 -o "$f.tmp" -w '%{http_code}' -A 'AnkaraBusSim/1.0 (egitsel oyun)' \
      "https://api.openstreetmap.org/api/0.6/map?bbox=$lo,$la,$lo2,$la2")
    [ "$code" = 200 ] && { mv "$f.tmp" "$f"; break; }
    echo "tekrar $f $code"; sleep $((i*5))
  done
  echo "$f $(stat -c%s "$f" 2>/dev/null)"
done; done
