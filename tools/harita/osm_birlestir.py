"""
OSM API 0.6 /map karolarını (XML) tek bir sıkıştırılmış JSON'a birleştirir (Hat 3 — 408 Kızılay → Güneşevler).

Karolar `indir` ile alınır (api.openstreetmap.org, 0,01° karolar); her karo komşularıyla çakışan yolları ve
düğümleri de içerir, kimliğe göre tekilleştirilir.

Çıktı (osm.json.gz):
    {"nodes": {id: [lat, lon]}, "tagged_nodes": {id: {tags}}, "ways": {id: {"n": [node ids], "t": {tags}}},
     "relations": {id: {"m": [[type, ref, role]], "t": {tags}}}}

Kullanım:
    python osm_birlestir.py --karolar <klasör> --out <osm.json.gz>
Veri: © OpenStreetMap katkıcıları, ODbL.
"""
import argparse
import glob
import gzip
import json
import os
import xml.etree.ElementTree as ET


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--karolar", required=True)
    ap.add_argument("--out", required=True)
    args = ap.parse_args()
    nodes, tagged, ways, rels = {}, {}, {}, {}
    for f in sorted(glob.glob(os.path.join(args.karolar, "*.osm"))):
        for ev, el in ET.iterparse(f):
            if el.tag == "node":
                i = int(el.get("id"))
                nodes[i] = [float(el.get("lat")), float(el.get("lon"))]
                t = {c.get("k"): c.get("v") for c in el if c.tag == "tag"}
                if t:
                    tagged[i] = t
                el.clear()
            elif el.tag == "way":
                ways[int(el.get("id"))] = {"n": [int(c.get("ref")) for c in el if c.tag == "nd"],
                                           "t": {c.get("k"): c.get("v") for c in el if c.tag == "tag"}}
                el.clear()
            elif el.tag == "relation":
                rels[int(el.get("id"))] = {"m": [[c.get("type"), int(c.get("ref")), c.get("role")] for c in el if c.tag == "member"],
                                           "t": {c.get("k"): c.get("v") for c in el if c.tag == "tag"}}
                el.clear()
    with gzip.open(args.out, "wt", encoding="utf-8") as fh:
        json.dump({"nodes": nodes, "tagged_nodes": tagged, "ways": ways, "relations": rels}, fh, ensure_ascii=False)
    print(f"{len(nodes)} düğüm, {len(ways)} yol/alan, {len(rels)} ilişki → {args.out}")


if __name__ == "__main__":
    main()
