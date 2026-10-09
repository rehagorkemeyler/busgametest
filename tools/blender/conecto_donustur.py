"""
Mercedes-Benz O530G Conecto (körüklü, 18 m) → Unity'ye hazır FBX.
Model: "Zort" (https://sketchfab.com/privatetrs1), "Mercedes Benz Conecto",
https://sketchfab.com/3d-models/mercedes-benz-conecto-ceb9e135aedc4b60a8d815fb54920129 — CC BY 4.0 (docs/KREDILER.md).

Kaynak GLB'de dokular yok ve camlar gövdeyle aynı (kayıp) dokuyu paylaşıyor. Bu yüzden:
  - Yan yüzlere (|normal.x| > 0.6, dış kabuk) düzlemsel UV verilir; "M_caroserie" şimdilik düz beyaz.
    Kaplama (cam bandı, sağdaki 4 kapının camları: KAPILAR, logolar) bulut oturumunda çizilecek. Ön ve arka cam koyu.
  - Diğer yüzler malzeme ve konuma göre düz renk alır (tavan açık gri, körük siyah, teker koyu).
Parçalar (bmc_donustur adlarıyla, döndürülmüş: Unity'de ön +Z, sağ +X):
  Govde_On, Govde_Arka (pivot mafsalda), Koruk (pivot mafsalda; oyunda mafsal açısının yarısı kadar döner),
  Teker_OnSol/OnSag, Teker_OrtaSol/OrtaSag (çeken aks değil, ön gövdenin arka aksı), Teker_ArkaSol/ArkaSag (çeken aks),
  Golge_On, Golge_Arka (--mobil).
Aks ve mafsal konumları kaynak modelden ölçüldü (ön y=3.28, orta y=-2.62, arka y=-8.75, teker yarıçapı 0.51 m,
mafsal körüğün ortası y=-4.45; Blender koordinatı, ön +Y).

Kullanım (Blender 5.x):
    Blender -b --factory-startup --python conecto_donustur.py -- --src mercedes_benz_conecto.glb --out <klasör> [--mobil]
Ardından iki FBX için de conecto_ic.py (iç mekân ve ön/arka yüz malzemeleri) çalıştırılır.
"""
import argparse
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
import bmesh  # noqa: E402
import numpy as np  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402

import bmc_donustur as bmc  # noqa: E402

AD = "MB_Conecto_G"
AKSLAR = {"On": 3.28, "Orta": -2.62, "Arka": -8.75}
TEKER_R = 0.51
TEKER_Z = 0.51
MAFSAL_Y = -4.45
KORUK_Y = (-5.36, -3.55)
# sağ taraftaki kapılar (Blender y aralıkları, ön +Y)
KAPILAR = [(4.28, 5.53), (-1.60, -0.36), (-7.71, -6.48), (-10.92, -9.67)]
CAM_Z = (1.22, 2.66)
KAPI_CAM_Z = (0.55, 2.66)
GOVDE_Y = (-12.15, 6.05)
GOVDE_Z = (0.0, 3.2)

RENKLER = {
    "M_Tavan": (0.88, 0.88, 0.87), "M_Govde": (0.80, 0.80, 0.80), "M_Koruk": (0.05, 0.05, 0.05),
    "M_Koyu": (0.04, 0.05, 0.06), "M_Siyah": (0.06, 0.06, 0.06), "M_Yazi": (0.9, 0.9, 0.9),
    "M_Teker": (0.09, 0.09, 0.09), "M_Jant": (0.62, 0.63, 0.65), "M_Ic": (0.45, 0.46, 0.48),
    "M_On_Cam": (0.05, 0.06, 0.07),
}
DOKU_W, DOKU_H = 2048, 512


def malzeme(ad, doku=None):
    m = bpy.data.materials.get(ad) or bpy.data.materials.new(ad)
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*RENKLER.get(ad, (1, 1, 1)), 1.0)
    if doku is not None and not any(n.type == "TEX_IMAGE" for n in m.node_tree.nodes):
        node = m.node_tree.nodes.new("ShaderNodeTexImage")
        node.image = bpy.data.images.load(doku)
        m.node_tree.links.new(node.outputs["Color"], bsdf.inputs["Base Color"])
    return m


def kaplama_ciz(yol):
    """Yan yüz kaplaması için düz beyaz şablon (Millennium'daki base.bmp gibi). UV: u = boy (arka→ön),
    v = yükseklik; üst yarı sağ yan, alt yarı sol yan (GOVDE_Y, GOVDE_Z aralıkları). Kaplama, cam bandı,
    kapı camları ve logolar bulut oturumunda bu yerleşime göre çizilecek."""
    img = np.ones((DOKU_H, DOKU_W, 4), dtype=np.float32)
    img[..., :3] = 0.95
    b = bpy.data.images.new("caroserie", DOKU_W, DOKU_H, alpha=False)
    b.pixels.foreach_set(img.ravel())
    b.filepath_raw = yol
    b.file_format = "PNG"
    b.save()
    bpy.data.images.remove(b)


def build(src, out, mobil):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=src)
    tex_out = os.path.join(out, "Textures")
    os.makedirs(tex_out, exist_ok=True)
    kaplama = os.path.join(tex_out, "caroserie.png")
    kaplama_ciz(kaplama)

    kaynak = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    hedef = {}  # etiket → (bmesh, [malzeme adları])

    def grup(etiket):
        if etiket not in hedef:
            bm = bmesh.new()
            bm.loops.layers.uv.new("UVMap")
            hedef[etiket] = (bm, [])
        return hedef[etiket]

    for o in kaynak:
        mats = [s.material.name if s.material else "" for s in o.material_slots]
        if any(k in (mats[0] if mats else "") for k in ("Path", "GSit")):
            continue  # yolcu yolu işaretleri
        bm = bmesh.new()
        bm.from_mesh(o.data)
        bm.transform(o.matrix_world)
        bm.normal_update()  # transform normalleri güncellemez; kaynak kökünde Y-yukarı → Z-yukarı dönüşü var
        uv = bm.loops.layers.uv.active
        for f in bm.faces:
            c = f.calc_center_median()
            n = f.normal
            mname = mats[f.material_index] if f.material_index < len(mats) else ""
            # teker: aks çizgisine yakın, gövde dışında
            etiket = None
            for aks, ay in AKSLAR.items():
                if all(math.hypot(v.co.y - ay, v.co.z - TEKER_Z) < TEKER_R + 0.03 for v in f.verts) and abs(c.x) > 0.55:
                    etiket = f"Teker_{aks}{'Sag' if c.x > 0 else 'Sol'}"
                    jant = math.hypot(c.y - ay, c.z - TEKER_Z) < 0.3 and abs(c.x) > 1.0
                    mat = "M_Jant" if jant else "M_Teker"
                    break
            if etiket is None:
                if "main_01" in mname or "trailer_01" in mname:
                    etiket, mat = "Koruk", "M_Koruk"
                else:
                    etiket = "Govde_On" if c.y > MAFSAL_Y else "Govde_Arka"
                    if "drks" in mname:
                        mat = "M_Koyu"
                    elif "413" in mname:
                        mat = "M_Siyah"
                    elif "AI_text" in mname:
                        mat = "M_Yazi"
                    elif abs(n.x) > 0.6 and abs(c.x) > 1.15 and 0.25 < c.z < 3.1:
                        mat = "M_caroserie"
                    elif abs(n.y) > 0.5 and c.y > 5.78:
                        # ön yüz (normalin yönüne bakmadan: kaynakta bazı ön yüzler içe bakıyor): cam bandı koyu,
                        # gerisi kaplama rengi (M_Govde; BusLivery kaplamayla birlikte boyar). Gösterge paneli y < 5.7
                        mat = "M_On_Cam" if 0.98 < c.z < 2.78 and abs(c.x) < 1.15 else "M_Govde"
                    elif abs(n.y) > 0.5 and c.y < -11.95:
                        mat = "M_On_Cam" if 1.35 < c.z < 2.6 and abs(c.x) < 1.05 else "M_Govde"  # arka yüz ve arka cam
                    elif n.z > 0.6 and c.z > 2.75:
                        mat = "M_Tavan"
                    elif abs(c.x) < 1.15 and 0.45 < c.z < 3.0 and abs(n.x) < 0.95:
                        mat = "M_Ic"
                    else:
                        mat = "M_Govde"
            hbm, hmats = grup(etiket)
            huv = hbm.loops.layers.uv["UVMap"]
            vs = [hbm.verts.new(v.co) for v in f.verts]
            try:
                nf = hbm.faces.new(vs)
            except ValueError:
                continue
            if mat not in hmats:
                hmats.append(mat)
            nf.material_index = hmats.index(mat)
            for lo, lsrc in zip(nf.loops, f.loops):
                if mat == "M_caroserie":
                    # düzlemsel UV: u boy, v yükseklik; sağ yan üst yarı, sol yan alt yarı
                    p = lsrc.vert.co
                    u = (p.y - GOVDE_Y[0]) / (GOVDE_Y[1] - GOVDE_Y[0])
                    v = (p.z - GOVDE_Z[0]) / (GOVDE_Z[1] - GOVDE_Z[0]) * 0.5 + (0.5 if c.x > 0 else 0.0)
                    lo[huv].uv = (min(max(u, 0.0), 1.0), min(max(v, 0.001), 0.999))
                elif uv is not None:
                    lo[huv].uv = lsrc[uv].uv
        bm.free()

    for o in kaynak:
        bpy.data.objects.remove(o, do_unlink=True)
    for o in list(bpy.context.scene.objects):
        bpy.data.objects.remove(o, do_unlink=True)

    flip = Matrix.Rotation(math.pi, 4, "Z")
    objs = {}
    for etiket, (bm, mats) in hedef.items():
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0005)
        bm.transform(flip)
        me = bpy.data.meshes.new(etiket)
        bm.to_mesh(me)
        bm.free()
        for m in mats:
            me.materials.append(malzeme(m, kaplama if m == "M_caroserie" else None))
        o = bpy.data.objects.new(etiket, me)
        bpy.context.scene.collection.objects.link(o)
        objs[etiket] = o

    def set_origin(obj, nokta):
        obj.data.transform(Matrix.Translation(-nokta))
        obj.location = nokta

    for etiket, o in objs.items():
        if etiket.startswith("Teker_"):
            bb = [Vector(c) for c in o.bound_box]
            set_origin(o, sum(bb, Vector()) / 8)
    mafsal = flip @ Vector((0.0, MAFSAL_Y, 1.0))
    set_origin(objs["Govde_Arka"], mafsal)
    set_origin(objs["Koruk"], mafsal)

    # sadeleştirme: tam ~160 bin, mobil ~80 bin üçgen (BMC: 158 / 124 bin)
    oran = 0.3 if mobil else 0.6
    for etiket, o in objs.items():
        if etiket.startswith(("Govde_", "Koruk")) or etiket.startswith("Teker_"):
            mod = o.modifiers.new("Decimate", "DECIMATE")
            mod.ratio = oran * (0.5 if etiket.startswith("Teker_") else 1.0)
            bpy.context.view_layer.objects.active = o
            bpy.ops.object.modifier_apply(modifier=mod.name)

    if mobil:
        objs["Golge_On"] = bmc.shadow_proxy([objs["Govde_On"]], "Golge_On")
        objs["Golge_Arka"] = bmc.shadow_proxy([objs["Govde_Arka"]], "Golge_Arka")
    bmc.iki_tarafli_yap(objs)

    bpy.context.view_layer.update()  # pivotlar taşındı: matrix_world güncel olsun
    pts = [o.matrix_world @ Vector(c) for o in objs.values() for c in o.bound_box]
    cx = (min(p.x for p in pts) + max(p.x for p in pts)) / 2
    cy = (min(p.y for p in pts) + max(p.y for p in pts)) / 2
    zmin = TEKER_Z - TEKER_R  # zemin: teker tabanı (kaynakta zeminin altına inen alt parçalar var)
    kok = bpy.data.objects.new(AD, None)
    bpy.context.scene.collection.objects.link(kok)
    for o in objs.values():
        o.location -= Vector((cx, cy, zmin))
        o.parent = kok

    rapor, toplam = [], 0
    for etiket, o in sorted(objs.items()):
        t = sum(len(p.vertices) - 2 for p in o.data.polygons)
        toplam += t
        loc = o.location
        rapor.append(f"| `{etiket}` | ({-loc.x:+.3f}, {loc.z:+.3f}, {-loc.y:+.3f}) | {t} | {len(o.data.materials)} |")
    print(f"toplam üçgen: {toplam}")
    print("\n".join(rapor))
    with open(os.path.join(out, "parcalar.md"), "w") as fh:
        fh.write(f"Toplam üçgen: {toplam}\n\n| Parça | Unity yerel konum (x, y, z) | Üçgen | Materyal |\n|---|---|---|---|\n")
        fh.write("\n".join(rapor) + "\n")

    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.fbx(filepath=os.path.join(out, AD + ".fbx"), use_selection=True,
                             apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                             bake_space_transform=True, path_mode="STRIP", object_types={"MESH", "EMPTY"},
                             mesh_smooth_type="FACE")
    return objs


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    ap = argparse.ArgumentParser()
    ap.add_argument("--src", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--mobil", action="store_true")
    ap.add_argument("--render", default=None)
    args = ap.parse_args(argv)
    os.makedirs(args.out, exist_ok=True)
    objs = build(args.src, args.out, args.mobil)
    if args.render:
        from tds_okuyucu import render
        shown = [o for t, o in objs.items() if not t.startswith("Golge_")]
        for t, o in objs.items():
            if t.startswith("Golge_"):
                o.hide_render = True
        render(shown, args.render)


if __name__ == "__main__":
    main()
