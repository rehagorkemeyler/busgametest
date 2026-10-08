"""
Hat 2 (Kızılay → Sıhhiye → Opera → Ulus) harita yerleşimi. Hat 1 ile aynı altyapı (harita_hat1.py).

Gerçek güzergâh (referans): Atatürk Bulvarı boyunca kuzeye ~2 km. Kızılay 868 m → Sıhhiye 855 m (çukur,
demiryolu köprüsü tümseği) → Opera ~860 m → Ulus 877 m. Oyun haritası stilize, ~1,4 km.

Üretir: <maps>/Hat2/Hat2_Yerlesim.json, <maps>/Hat2/Zemin_Hat2.fbx ve isteğe bağlı önizlemeler.
Unity'de: Ankara Bus > Hat 2 Haritasını Kur (ayrı bir sahnede).

Kullanım:
    python harita_hat2.py --maps AnkaraBusSimulator/Assets/_Project/Maps [--render-dir <klasör>]
"""
import argparse
import json
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402

import apartman_kit as kit  # noqa: E402
import harita_hat1 as h1  # noqa: E402
import yapilar_kit as yapi  # noqa: E402
import yol_kit as yol  # noqa: E402

B = yol.BULVAR
h1.SPEC.update({
    "Bulvar_Inis2_20m": (20, -0.02, 0), "Bulvar_Inis4_20m": (20, -0.04, 0), "Ulus_DonusHalkasi": (14, 0.0, 0),
})

HAT2_CHAIN = (
    ["Bulvar_Duz_20m"] * 2                       # Kızılay meydanı (başlangıcın gerisi)
    + ["Bulvar_DurakCebi_40m"]                   # Durak 1: Kızılay
    + ["Bulvar_Inis2_20m"] * 20                  # Sıhhiye'ye iniş
    + ["Bulvar_DurakCebi_40m"]                   # Durak 2: Sıhhiye
    + ["Bulvar_Egim4_20m"] * 4 + ["Bulvar_Inis4_20m"] * 4   # Sıhhiye köprüsü tümseği
    + ["Bulvar_Duz_20m"] * 15
    + ["Bulvar_DurakCebi_40m"]                   # Durak 3: Opera
    + ["Bulvar_Egim2_20m"] * 5 + ["Bulvar_Egim4_20m"] * 12  # Ulus yokuşu
    + ["Bulvar_Duz_20m", "Ulus_DonusHalkasi"]    # Durak 4: Ulus, Zafer Anıtı halkası
)
RING_RADIUS = 14.0 + (2 * B["lane"] + 1.0) / 2   # ada + halka yarısı (yol_kit.piece_roundabout)


def build_layout(widths, seed=2025, blocks=()):
    rng = random.Random(seed)
    lay = h1.Layout()
    bul = h1.Chain(HAT2_CHAIN, start=(-40.0, 0.0, 0.0, 0.0), s0=-40.0)
    h1.place_roads(lay, bul)
    HB, WB = B["half_road"], B["sidewalk"]
    side_out = HB + WB

    bays = [p for p in bul.parts if p[0] == "Bulvar_DurakCebi_40m"]
    names = ["Kızılay", "Sıhhiye", "Opera"]
    bay_ranges = []
    for (n, s, pose, path), name in zip(bays, names):
        sc = s + path.length / 2
        f, r, z, th = bul.frame(sc, HB + 1.5)
        lay.stop(name, f, r, z, math.degrees(th))
        f, r, z, th = bul.frame(sc, HB + 3.3)
        lay.add("Props/EGO_Durak", f, r, z + 0.15, math.degrees(th) - 90, "Duraklar")
        bay_ranges.append((s, s + path.length))
    n, s_ring, pose, path = bul.index_of("Ulus_DonusHalkasi")
    f, r, z, th = bul.frame(s_ring + 7.0, HB - 1.75)
    lay.stop("Ulus", f, r, z, math.degrees(th))
    f, r, z, th = bul.frame(s_ring + 7.0, HB + 0.3)
    lay.add("Props/EGO_Durak", f, r, z + 0.15, math.degrees(th) - 90, "Duraklar")

    s_kiz = bays[0][1]
    f, r, z, th = bul.frame(s_kiz + 10.0, HB + 1.5)
    spawn = {"pos": h1.unity(f, r, z + 0.3), "rotY": math.degrees(th)}

    # --- simge yapılar (kuzeye giderken batı = sol) ---
    def left(s, extra, model, group="SimgeYapilar"):
        f, r, z, th = bul.frame(s, -(side_out + extra))
        lay.add(model, f, r, z, math.degrees(th) + 90, group)

    # Kızılay meydanı: solda Kızılay AVM, durak cebinden sonra sağda Emek İşhanı
    avm_s = -22.0
    left(avm_s, 0.5, h1.SKP_AVM[0])
    avm_range = (avm_s - h1.SKP_AVM[1] / 2 - 2, avm_s + h1.SKP_AVM[1] / 2 + 2)
    emek_s = bays[0][1] + 40.0 + 4.0 + h1.SKP_EMEK[1] / 2
    f, r, z, th = bul.frame(emek_s, side_out + 0.5)
    lay.add(h1.SKP_EMEK[0], f, r, z, math.degrees(th) - 90, "SimgeYapilar")
    emek_range = (emek_s - h1.SKP_EMEK[1] / 2 - 2, emek_s + h1.SKP_EMEK[1] / 2 + 2)
    s_sih = bays[1][1]
    left(s_sih + 20.0, 7.0, "Landmarks/HititGunesKursu")
    s_opera = bays[2][1]
    left(s_opera + 20.0, 3.0, "Landmarks/OperaBinasi")
    s_park = s_opera + 20.0 + 32.0 + 6.0 + 45.0
    left(s_park, 0.5, "Landmarks/GenclikParki")
    ef, er, ez, eth = bul.frame(bul.s_end)
    cf, cr = ef + RING_RADIUS * math.cos(eth), er + RING_RADIUS * math.sin(eth)
    lay.add("Landmarks/ZaferAniti", cf, cr, ez + 0.2, math.degrees(eth), "SimgeYapilar")
    for k in range(12):
        a = 2 * math.pi * k / 12
        if abs(math.atan2(math.sin(a - (eth + math.pi)), math.cos(a - (eth + math.pi)))) < 0.7:
            continue
        lay.add("Props/Agac_Cinar_1", cf + 36.0 * math.cos(a), cr + 36.0 * math.sin(a), ez, rng.uniform(0, 360), "Agaclar")
    # Hitit anıtının çevresi: küçük meydan ağaçları
    for dk in (-14.0, 14.0):
        f, r, z, th = bul.frame(s_sih + 20.0 + dk, -(side_out + 5.0))
        lay.add("Props/Agac_Cinar_2", f, r, z, rng.uniform(0, 360), "Agaclar")

    # --- binalar ---
    pool_kiz = [("A1_Bulvar_6Kat_Krem", 3), ("A1_Bulvar_7Kat_Somon", 3), ("A1_Bulvar_8Kat_Bej", 2),
                ("A5_Ofis_10Kat_Mavi", 1), ("A2_Kose_7Kat_Gri", 1)]
    pool_sih = [("A5_Ofis_10Kat_Mavi", 3), ("A5_Ofis_8Kat_Yesil", 3), ("A1_Bulvar_6Kat_Krem", 2),
                ("A2_Kose_6Kat_Somon", 1)]
    pool_ulus = [("A3_Cankaya_5Kat_SariKirma", 3), ("A1_Bulvar_6Kat_Krem", 2), ("A1_Bulvar_7Kat_Somon", 2),
                 ("A2_Kose_6Kat_Somon", 1)]
    excl_r = bay_ranges + [emek_range, (s_ring - 30.0, bul.s_end + 60.0)]
    excl_l = [avm_range, (s_sih + 2.0, s_sih + 38.0), (s_opera - 20.0, s_park + 50.0), (s_ring - 30.0, bul.s_end + 60.0)]
    segs = [(-40.0, s_sih, pool_kiz, 0.0), (s_sih, s_opera + 40.0, pool_sih, 2.0), (s_opera + 40.0, bul.s_end, pool_ulus, 1.0)]
    for s0, s1, pool, gap in segs:
        h1.line_buildings(lay, bul, +1, s0, s1, pool, widths, rng, offset=side_out, gap=gap, exclude=excl_r)
        h1.line_buildings(lay, bul, -1, s0, s1, pool, widths, rng, offset=side_out, gap=gap, exclude=excl_l)
    # Kızılay–Sıhhiye arası ikinci sıra: Google Earth'ten Kızılay blokları
    block_rng = random.Random(seed + 12)
    h1.back_row(lay, bul, +1, -40.0, s_sih, blocks, block_rng, offset=side_out, exclude=[emek_range])
    h1.back_row(lay, bul, -1, -40.0, s_sih, blocks, block_rng, offset=side_out, exclude=[avm_range])

    # --- refüj: lamba ve ağaç ---
    s = -36.0
    k = 0
    while s < s_ring - 4:
        f, r, z, th = bul.frame(s)
        if k % 2 == 0:
            lay.add(h1.SKP_LAMBA, f, r, z + 0.2, math.degrees(th), "Lambalar")
        else:
            lay.add(f"Props/{rng.choice(['Agac_Cinar_1', 'Agac_Cinar_2'])}", f, r, z + 0.2, rng.uniform(0, 360), "Agaclar")
        s += 20.0
        k += 1

    lay.lanes = build_lanes(bul, s_ring)
    return lay, bul, spawn


def build_lanes(bul, s_ring, step=5.0):
    """Bulvarın iki yönünde 3'er şerit (halka girişine kadar). Dönüş yok, ışık yok."""
    lanes = []
    m, w = B["median"], B["lane"]
    for k in range(3):
        for sign, nm in ((1, "Gidis"), (-1, "Donus")):
            off = sign * (m + w * (k + 0.5))
            pts = []
            s = bul.s_start
            while s < s_ring:
                f, r, z, _ = bul.frame(s, off)
                pts += h1.unity(f, r, z)
                s += step
            f, r, z, _ = bul.frame(s_ring, off)
            pts += h1.unity(f, r, z)
            if sign < 0:
                pts = [c for i in range(len(pts) // 3 - 1, -1, -1) for c in pts[i * 3:i * 3 + 3]]
            lanes.append({"name": f"Bulvar_{nm}_{k + 1}", "limitKmh": 50, "points": pts, "exits": [],
                          "stopLine": -1.0, "group": 0, "signal": ""})
    return lanes


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    ap = argparse.ArgumentParser()
    ap.add_argument("--maps", required=True)
    ap.add_argument("--render-dir", default=None)
    args = ap.parse_args(argv)
    out = os.path.join(args.maps, "Hat2")
    os.makedirs(out, exist_ok=True)

    kit.clear_scene()
    img = kit.build_palette_image(os.path.join(out, "_palet_gecici.png"))
    img.pack()
    os.remove(os.path.join(out, "_palet_gecici.png"))
    mat = kit.palette_material(img)

    models, widths = {}, {}
    for name, (kind, kw) in kit.VARIANTS.items():
        o = kit.BUILDERS[kind](name, material=mat, **kw)
        models[f"Buildings/Apartmanlar/{name}"] = o
        widths[name] = {"w": o.dimensions.x, "setback": 3.2 if kw.get("garden") else 0.0}
    if args.render_dir:
        for name, make in yol.PIECES.items():
            rb, _ = make()
            models[f"Roads/{name}"] = rb.to_object(name, mat, recalc_normals=False)
        for name, (folder, make) in yapi.ITEMS.items():
            models[f"{folder}/{name}"] = make(name, mat)
    for o in models.values():
        o.hide_render = True
        o.hide_viewport = True

    lay, bul, spawn = build_layout(widths, blocks=h1.load_blocks(args.maps))
    verts, faces = h1.build_ground(bul, bul)
    mesh = bpy.data.meshes.new("Zemin_Hat2")
    mesh.from_pydata(verts, [], faces)
    uv = mesh.uv_layers.new(name="UVMap")
    for loop in uv.data:
        loop.uv = kit.palette_uv("cim")
    mesh.materials.append(mat)
    ground = bpy.data.objects.new("Zemin_Hat2", mesh)
    bpy.context.scene.collection.objects.link(ground)
    kit.export_fbx(ground, os.path.join(out, "Zemin_Hat2.fbx"))
    lay.add("Hat2/Zemin_Hat2", 0.0, 0.0, 0.0, 0.0, "Zemin")

    data = {"lineNumber": "2", "lineName": "Kızılay - Ulus", "spawnPos": spawn["pos"],
            "spawnRotY": round(spawn["rotY"], 2), "items": lay.items, "stops": lay.stops,
            "lanes": lay.lanes, "signals": []}
    with open(os.path.join(out, "Hat2_Yerlesim.json"), "w", encoding="utf-8") as fh:
        json.dump(data, fh, ensure_ascii=False, indent=1)
    counts = {}
    for it in lay.items:
        counts[it["group"]] = counts.get(it["group"], 0) + 1
    print("yerleşim:", counts, "durak:", [s["name"] for s in lay.stops], "şerit:", len(lay.lanes))
    print("uzunluk", round(bul.s_end), "m, Ulus rakımı", round(bul.end[2], 1), "m")

    if args.render_dir:
        h1.render(models, lay, ground, spawn, args.render_dir, prefix="hat2")


if __name__ == "__main__":
    main()
