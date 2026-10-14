"""
Otobüs FBX'inden yolcu koltuklarının ve ayakta durma yerlerinin çıkarılması (Y20). Çıktı JSON (Unity yerel koordinatı, kök
zeminde, ön +Z, sağ +X), OtobusKurucu prefabda "Koltuklar" noktalarını buradan kurar:
  koltuklar: [{p: [x,y,z], yon: [x,0,z], on: [x,y,z], koridor: [x,y,z]}]
    p        oturan yolcunun kökü (Quaternius Otur pozu: kalça kökün 10 cm gerisinde, uyluk altı kökten 0,45 m yukarıda)
    yon      yolcunun baktığı yön (sırtlıktan mindere)
    on       koltuğun önünde, zeminde (yolcu buradan oturur / kalkar)
    koridor  koridorda (x = 0), koltuğun hizasında, zeminde
  ayakta: [{p: [x,y,z]}]  koridorda, tavan borusunun altında boş yerler (koltuklar dolunca tutunarak durulur)
Yöntem: koltuk kumaşı malzemesinin (BMC: tapiterie, Millennium: bancos) yukarı bakan yüzleri kümelenir (minder); genişse
(çift koltuk) 0,45 m'lik oturaklara bölünür; minderin üstünde kalan dikey kumaş yüzleri sırtlıktır, yön sırtlıktan mindere
(en yakın eksene oturtulur). Zemin yükseklikleri yukarıdan aşağı ışınla.

Kullanım:
    Blender -b --factory-startup --python koltuk_cikar.py -- --fbx BMC_Procity_12LF.fbx --kumas tapiterie --out koltuklar.json
"""
import argparse
import json
import math
import sys

import bpy  # noqa: E402
import bmesh  # noqa: E402
from mathutils import Vector  # noqa: E402
from mathutils.bvhtree import BVHTree  # noqa: E402

OTURAK_EN = 0.45
OTUR_Y = 0.45          # minder üstü − kök
ONE = 0.13             # kök minder ortasının bu kadar önünde
KUME = 0.10            # minder yüzlerini birleştirme mesafesi (m)
AYAKTA_ARALIK = 0.75
MINDER_UST = 2.3
HATA = False


def U(v):
    """Blender dünya → Unity (x, y, z)."""
    return Vector((-v.x, v.z, -v.y))


def B(u):
    return Vector((-u.x, -u.z, u.y))


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    ap = argparse.ArgumentParser()
    ap.add_argument("--fbx", required=True)
    ap.add_argument("--kumas", required=True, help="koltuk kumaşı malzeme adı parçası")
    ap.add_argument("--out", required=True)
    ap.add_argument("--ayakta_z", default=None, help="ayakta yerlerin z aralığı (ör. -5,4)")
    ap.add_argument("--hata", action="store_true")
    ap.add_argument("--sirttan", action="store_true", help="minderi kumaş olmayan koltukları sırtlıktan bul (deneysel)")
    args = ap.parse_args(argv)
    global HATA
    HATA = args.hata

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=args.fbx, axis_forward="-Z", axis_up="Y", bake_space_transform=True)
    govdeler = [o for o in bpy.context.scene.objects if o.type == "MESH" and o.name.startswith("Govde")]
    # bütün gövde mesh'leri dünya koordinatında tek BVH (zemin ve boşluk ışınları) ve kumaş yüzleri
    tum_v, tum_f, kumas = [], [], []
    for o in govdeler:
        mw = o.matrix_world
        r = mw.to_3x3()
        bm = bmesh.new()
        bm.from_mesh(o.data)
        adlar = [m.name if m else "" for m in o.data.materials]
        bas = len(tum_v)
        tum_v.extend(mw @ v.co for v in bm.verts)
        for f in bm.faces:
            tum_f.append([bas + v.index for v in f.verts])
            if args.kumas in adlar[f.material_index]:
                n = (r @ f.normal).normalized()
                c = mw @ f.calc_center_median()
                kumas.append((U(c), Vector((-n.x, n.z, -n.y)), f.calc_area(), [U(mw @ v.co) for v in f.verts]))
        bm.free()
    bvh = BVHTree.FromPolygons(tum_v, tum_f)

    def zemin(u, ust=None, en_cok=None):
        """Unity noktasının altındaki ilk yukarı bakan yüzey (ust'ten aşağı ışın); en_cok'tan yüksek olanlar (önündeki
        koltuğun minderi) geçilir."""
        bas = B(Vector((u.x, ust if ust is not None else u.y + 1.2, u.z)))
        for _ in range(10):
            hit = bvh.ray_cast(bas, Vector((0, 0, -1)), 3.0)
            if hit[0] is None:
                return None
            if hit[1].z > 0.7 and (en_cok is None or hit[0].z <= en_cok):
                return hit[0].z
            bas = hit[0] + Vector((0, 0, -0.01))
        return None

    # minderler: yukarı bakan kumaş yüzleri, birbirine KUME'den yakın olanlar tek küme
    # Millennium'da arka sıralar yüksek platformda: minder 2,0 m'ye kadar (Y20: 1,6 sınırı üç sırayı kaçırıyordu)
    ust = [k for k in kumas if k[1].y > 0.7 and 0.3 < k[0].y < MINDER_UST]
    kumeler = []
    for c, n, a, vs in ust:
        hedef = None
        for km in kumeler:
            if any(abs(c.x - d.x) < KUME + 0.15 and abs(c.z - d.z) < KUME + 0.15 and abs(c.y - d.y) < 0.08 for d in km["c"]):
                hedef = km
                break
        if hedef is None:
            hedef = {"c": [], "v": [], "a": 0.0}
            kumeler.append(hedef)
        hedef["c"].append(c)
        hedef["v"].extend(vs)
        hedef["a"] += a
    koltuklar = []
    for km in kumeler:
        xs = [v.x for v in km["v"]]
        zs = [v.z for v in km["v"]]
        ys = [v.y for v in km["v"]]
        w, d = max(xs) - min(xs), max(zs) - min(zs)
        if km["a"] < 0.04 or max(w, d) < 0.25:
            if HATA:
                print(f"  elendi (küçük) x {sum(xs)/len(xs):.2f} z {sum(zs)/len(zs):.2f} alan {km['a']:.3f} w {w:.2f} d {d:.2f}")
            continue
        merkez = Vector(((max(xs) + min(xs)) / 2, max(ys), (max(zs) + min(zs)) / 2))
        # sırtlık: minderin yakınında, minderden en az 15 cm yukarıda, dikey kumaş yüzleri
        sirt = [k[0] for k in kumas if abs(k[1].y) < 0.45 and k[0].y > merkez.y + 0.15 and
                abs(k[0].x - merkez.x) < w / 2 + 0.2 and abs(k[0].z - merkez.z) < d / 2 + 0.2]
        if not sirt:
            if HATA:
                print(f"  elendi (sırtlık yok) x {merkez.x:.2f} z {merkez.z:.2f} y {merkez.y:.2f} w {w:.2f} d {d:.2f}")
            continue
        sc = sum(sirt, Vector()) / len(sirt)
        yon = Vector((merkez.x - sc.x, 0, merkez.z - sc.z))
        yon = Vector((math.copysign(1, yon.x), 0, 0)) if abs(yon.x) > abs(yon.z) else Vector((0, 0, math.copysign(1, yon.z)))
        # oturak sayısı: yöne dik genişlik
        en = w if yon.z != 0 else d
        adet = max(1, int(round(en / OTURAK_EN)))
        yan = Vector((yon.z, 0, -yon.x))
        for i in range(adet):
            k = (i + 0.5) / adet - 0.5
            orta = merkez + yan * (k * en)
            p = orta + yon * ONE
            p.y = merkez.y - OTUR_Y
            on = orta + yon * 0.55
            zy = zemin(on, ust=merkez.y + 0.3, en_cok=p.y + 0.1)
            on.y = zy if zy is not None else p.y
            koridor = Vector((0.0, 0, orta.z if yon.z != 0 else on.z))
            ky = zemin(koridor, ust=merkez.y + 0.6, en_cok=p.y + 0.1)
            koridor.y = ky if ky is not None else on.y
            koltuklar.append({"p": [round(p.x, 3), round(p.y, 3), round(p.z, 3)],
                              "yon": [yon.x, 0.0, yon.z],
                              "on": [round(on.x, 3), round(on.y, 3), round(on.z, 3)],
                              "koridor": [round(koridor.x, 3), round(koridor.y, 3), round(koridor.z, 3)]})
    # sırtlıktan koltuk (minderi kumaş olmayan koltuklar için, --sirttan ile): öne bakan dikey kumaş yüzleri kümelenir; minder
    # sırtlığın alt kenarının hizasında, 22 cm önünde. Y20: Millennium'da yalnız sahte koltuk üretti (sırtlığın arka yüzü, kapı
    # ve sürücü bölmesi panelleri) — minder sınırı düzelince gerek kalmadı, varsayılan kapalı.
    dik = [] if not args.sirttan else [k for k in kumas if abs(k[1].y) < 0.35 and 0.5 < k[0].y < 1.9 and k[2] > 0.0005]
    sirtlar = []
    for c, n, a, vs in dik:
        yatay = Vector((n.x, 0, n.z))
        if yatay.length < 0.5:
            continue
        yatay.normalize()
        yon = Vector((math.copysign(1, yatay.x), 0, 0)) if abs(yatay.x) > abs(yatay.z) else Vector((0, 0, math.copysign(1, yatay.z)))
        hedef = None
        for sr in sirtlar:
            if sr["yon"] == yon and any((c - d).length < 0.12 for d in sr["c"]):
                hedef = sr
                break
        if hedef is None:
            hedef = {"c": [], "v": [], "a": 0.0, "yon": yon}
            sirtlar.append(hedef)
        hedef["c"].append(c)
        hedef["v"].extend(vs)
        hedef["a"] += a
    for sr in sirtlar:
        yon = sr["yon"]
        xs = [v.x for v in sr["v"]]
        ys = [v.y for v in sr["v"]]
        zs_ = [v.z for v in sr["v"]]
        en = (max(xs) - min(xs)) if yon.z != 0 else (max(zs_) - min(zs_))
        boy = max(ys) - min(ys)
        if sr["a"] < 0.05 or en < 0.3 or boy < 0.25:
            continue
        alt = min(ys)
        taban = Vector(((max(xs) + min(xs)) / 2, alt, (max(zs_) + min(zs_)) / 2))
        adet = max(1, int(round(en / OTURAK_EN)))
        yan = Vector((yon.z, 0, -yon.x))
        for i in range(adet):
            k = (i + 0.5) / adet - 0.5
            orta = taban + yan * (k * en) + yon * 0.22
            if any(abs(orta.x - t["p"][0] + yon.x * ONE) < 0.3 and abs(orta.z - t["p"][2] + yon.z * ONE) < 0.3
                   for t in koltuklar):
                continue
            # kök (zemin) yüksekliği: koltuğun ön kenarının altındaki zemin, ayakların bastığı yer (minder kumaş değil, yüksekliği
            # güvenilir ölçülemiyor; minder zeminden 0,45 m yukarıda varsayılır). Sırtlığın alt kenarından 25 cm aşağısı en çok.
            fy = zemin(orta + yon * 0.30, ust=alt - 0.05, en_cok=alt - 0.25)
            if fy is None or fy < 0.2 or fy > 1.3:
                continue
            p = orta + yon * ONE
            # yüksek kaideli koltuk (teker yuvası üstü): minder sırtlığın alt kenarında, kök ondan 0,45 m aşağıda (ayak boşta)
            p.y = max(fy, alt - OTUR_Y - 0.02)
            on = orta + yon * 0.55
            zy = zemin(on, ust=p.y + 0.75, en_cok=p.y + 0.1)
            on.y = zy if zy is not None else p.y
            koridor = Vector((0.0, 0, orta.z if yon.z != 0 else on.z))
            ky = zemin(koridor, ust=p.y + 1.05, en_cok=p.y + 0.1)
            koridor.y = ky if ky is not None else on.y
            koltuklar.append({"p": [round(p.x, 3), round(p.y, 3), round(p.z, 3)], "yon": [yon.x, 0.0, yon.z],
                              "on": [round(on.x, 3), round(on.y, 3), round(on.z, 3)],
                              "koridor": [round(koridor.x, 3), round(koridor.y, 3), round(koridor.z, 3)]})

    # aynı oturağı iki küme bulduysa (minder iki parça) birini at
    tekil = []
    for k in koltuklar:
        if not any(abs(k["p"][0] - t["p"][0]) < 0.25 and abs(k["p"][2] - t["p"][2]) < 0.25 for t in tekil):
            tekil.append(k)
    koltuklar = sorted(tekil, key=lambda k: -k["p"][2])

    # ayakta yerler: koridorda (x 0 ve ±0,25), zemin bulunan, 1,9 m boş yerler; koltuk önlerinden uzak
    ayakta = []
    zs = [k["p"][2] for k in koltuklar]
    z0, z1 = (min(zs) - 0.5, max(zs) + 0.5) if zs else (-5, 5)
    if args.ayakta_z:
        z0, z1 = (float(x) for x in args.ayakta_z.split(","))
    z = z1
    while z > z0:
        for x in (0.0,):
            y = zemin(Vector((x, 0, z)), ust=2.2)
            if y is None or y > 1.3:
                continue
            bas = B(Vector((x, y + 0.15, z)))
            hit = bvh.ray_cast(bas, Vector((0, 0, 1)), 1.7)
            if hit[0] is not None:
                continue
            if any(abs(Vector(k["on"]).x - x) < 0.3 and abs(Vector(k["on"]).z - z) < 0.35 for k in koltuklar):
                continue
            ayakta.append({"p": [round(x, 3), round(y, 3), round(z, 3)]})
        z -= AYAKTA_ARALIK
    json.dump({"kaynak": args.fbx.split("/")[-1], "koltuklar": koltuklar, "ayakta": ayakta}, open(args.out, "w"), indent=1)
    print(f"koltuk {len(koltuklar)}, ayakta {len(ayakta)} → {args.out}")


if __name__ == "__main__":
    main()
