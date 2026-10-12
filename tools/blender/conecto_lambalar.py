"""
Mercedes Conecto (MB_Conecto_G): gece yanan lamba parçalarını ekler. Kaynak modelde hiç lamba parçası yoktu (BMC'de
Lamba_* var); camlar ve lambalar yalnız kaplama dokusuna çizili olduğundan gece otobüs dışarıdan simsiyah/gri görünüyordu.

Eklenen parçalar (kökün çocuğu, pivot gövdenin pivotu; arka gövdedekiler oyunda KorukluOtobus.arkaGorseller ile izler):
  Lamba_Pencere_On / Lamba_Pencere_Arka   yan camlar (gece_yan.png maskesi)
  Lamba_Park / Lamba_Park_Arka            farlar (ön), park lambaları ve arka cam (arka) (gece_onarka.png)
  Lamba_Fren                              stop lambaları (gece_fren.png; frende yanar)
  Lamba_IcSerit / Lamba_IcSerit_Arka      tavanda iki sıra ışık şeridi (düz beyaz)
Yöntem: maskenin yandığı yere UV'si değen dış yüzler (M_caroserie, M_onarka) kopyalanır, yüz normali boyunca birkaç mm
dışarı itilir; kopyalar aynı UV'yi taşır, malzemesi maskeyi kullanır (Unity'de Unlit + alfa kesme: OtobusKurucu.LambaMalzemesi).
Maskeler tools/kaplama_conecto.py'den (kaplamadan bağımsız).

Kullanım (iki FBX için, conecto_kapilar.py'den sonra; FBX'i yerinde yazar, ikinci kez çalıştırılmaz):
    Blender -b --factory-startup --python conecto_lambalar.py -- --fbx MB_Conecto_G.fbx --doku <Textures klasörü>
"""
import argparse
import os
import statistics
import sys

import bpy  # noqa: E402
import bmesh  # noqa: E402
import numpy as np  # noqa: E402
from mathutils import Vector  # noqa: E402
from mathutils.bvhtree import BVHTree  # noqa: E402
from PIL import Image  # noqa: E402

# (parça, gövde, kaynak malzeme, maske, malzeme, dışarı itme m)
KAPLAMALAR = [
    ("Lamba_Pencere_On", "Govde_On", "M_caroserie", "gece_yan.png", "M_LambaPencere", 0.004),
    ("Lamba_Pencere_Arka", "Govde_Arka", "M_caroserie", "gece_yan.png", "M_LambaPencere", 0.004),
    ("Lamba_Park", "Govde_On", "M_onarka", "gece_onarka.png", "M_LambaPark", 0.004),
    ("Lamba_Park_Arka", "Govde_Arka", "M_onarka", "gece_onarka.png", "M_LambaPark", 0.004),
    ("Lamba_Fren", "Govde_Arka", "M_onarka", "gece_fren.png", "M_LambaFren", 0.007),
]
SERITLER = [("Lamba_IcSerit", "Govde_On"), ("Lamba_IcSerit_Arka", "Govde_Arka")]
SERIT_X, SERIT_EN = 0.48, 0.10


def malzeme(ad, doku=None, renk=(1, 1, 1)):
    m = bpy.data.materials.get(ad) or bpy.data.materials.new(ad)
    m.use_nodes = True
    bsdf = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Base Color"].default_value = (*renk, 1)
    if doku and not any(n.type == "TEX_IMAGE" for n in m.node_tree.nodes):
        t = m.node_tree.nodes.new("ShaderNodeTexImage")
        t.image = bpy.data.images.load(doku)
        m.node_tree.links.new(t.outputs["Color"], bsdf.inputs["Base Color"])
        m.node_tree.links.new(t.outputs["Alpha"], bsdf.inputs["Alpha"])
    return m


def maske_yanik(alfa, uvs):
    """UV kutusunun içinde maskenin yanan pikseli var mı (kutu: yüzün UV sınırları)."""
    h, w = alfa.shape
    us = [u for u, _ in uvs]
    vs = [v for _, v in uvs]
    x0, x1 = int(max(min(us), 0) * w), int(min(max(us), 1) * w) + 1
    y0, y1 = int((1 - min(max(vs), 1)) * h), int((1 - max(min(vs), 0)) * h) + 1
    if x1 <= x0 or y1 <= y0:
        return False
    return bool(alfa[y0:y1, x0:x1].any())


def yeni_obje(ad, bm, govde, mat):
    me = bpy.data.meshes.new(ad)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(mat)
    obj = bpy.data.objects.new(ad, me)
    bpy.context.scene.collection.objects.link(obj)
    # gövdeyle aynı ebeveyn (kök) ve aynı pivot: mobil ve tam kalite modelde aynı yer (BusKaliteModeli yalnız mesh'i değiştirir)
    obj.parent = govde.parent
    obj.matrix_world = govde.matrix_world.copy()
    return obj


def kaplama_kopyasi(ad, govde, kaynak, maske_yolu, mat, itme):
    alfa = np.asarray(Image.open(maske_yolu).convert("RGBA"))[:, :, 3] > 0
    me = govde.data
    adlar = [m.name for m in me.materials]
    if kaynak not in adlar:
        print(f"{ad}: {govde.name}'da {kaynak} yok, atlandı")
        return None
    ki = adlar.index(kaynak)
    bm = bmesh.new()
    bm.from_mesh(me)
    uv = bm.loops.layers.uv.active
    yeni = bmesh.new()
    yuv = yeni.loops.layers.uv.new("UVMap")
    esle = {}
    n = 0
    for f in bm.faces:
        if f.material_index != ki:
            continue
        uvs = [tuple(lo[uv].uv) for lo in f.loops]
        if not maske_yanik(alfa, uvs):
            continue
        vs = []
        for v in f.verts:
            anahtar = (v.index, f.normal.to_tuple(3))
            if anahtar not in esle:
                esle[anahtar] = yeni.verts.new(v.co + f.normal * itme)
            vs.append(esle[anahtar])
        try:
            nf = yeni.faces.new(vs)
        except ValueError:
            continue
        nf.smooth = False
        for lo, (u, v) in zip(nf.loops, uvs):
            lo[yuv].uv = (u, v)
        n += 1
    bm.free()
    if n == 0:
        yeni.free()
        print(f"{ad}: yüz bulunamadı")
        return None
    obj = yeni_obje(ad, yeni, govde, mat)
    print(f"{ad}: {n} yüz")
    return obj


def tavan_seridi(ad, govde, mat):
    """Gövdenin içinde, tavanın 2 cm altında iki boyuna ışık şeridi (aşağı bakar)."""
    mw = govde.matrix_world
    bvh = BVHTree.FromObject(govde, bpy.context.evaluated_depsgraph_get())
    ys = [(mw @ v.co).y for v in govde.data.vertices]
    y_bas, y_son = min(ys), max(ys)
    on = govde.name == "Govde_On"
    # ön gövdede sürücü yeri (ön, −y) ve körük ağzı (arka) hariç; arka gövdede körük ağzı (ön) ve motor bölmesi hariç
    a, b = (y_bas + 2.2, y_son - 0.8) if on else (y_bas + 2.4, y_son - 0.8)
    inv = mw.inverted()
    tavanlar = []
    for i in range(12):
        y = a + (b - a) * (i + 0.5) / 12
        for x in (-SERIT_X, SERIT_X):
            p = inv @ Vector((x, y, 1.6))
            d = (inv.to_3x3() @ Vector((0, 0, 1))).normalized()
            hit = bvh.ray_cast(p, d, 2.0)
            if hit[0] is not None:
                tavanlar.append((mw @ hit[0]).z)
    if not tavanlar:
        print(f"{ad}: tavan bulunamadı")
        return None
    z = statistics.median(tavanlar) - 0.02
    yeni = bmesh.new()
    yuv = yeni.loops.layers.uv.new("UVMap")
    for x in (-SERIT_X, SERIT_X):
        kose = [Vector((x - SERIT_EN / 2, a, z)), Vector((x + SERIT_EN / 2, a, z)),
                Vector((x + SERIT_EN / 2, b, z)), Vector((x - SERIT_EN / 2, b, z))]
        vs = [yeni.verts.new(inv @ k) for k in kose]
        f = yeni.faces.new(vs)
        f.normal_update()
        if (mw.to_3x3() @ f.normal).z > 0:
            f.normal_flip()
        for lo, t in zip(f.loops, ((0, 0), (1, 0), (1, 1), (0, 1))):
            lo[yuv].uv = t
    obj = yeni_obje(ad, yeni, govde, mat)
    print(f"{ad}: tavan z {z:.2f}, y {a:.2f} … {b:.2f}")
    return obj


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    ap = argparse.ArgumentParser()
    ap.add_argument("--fbx", required=True)
    ap.add_argument("--doku", required=True, help="gece_*.png maskelerinin klasörü (Textures)")
    ap.add_argument("--out", default=None)
    args = ap.parse_args(argv)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=args.fbx, axis_forward="-Z", axis_up="Y", bake_space_transform=True)
    if any(o.name.startswith("Lamba_") for o in bpy.context.scene.objects):
        sys.exit("FBX'te lamba parçaları zaten var: önce conecto_donustur.py + conecto_ic.py + conecto_kapilar.py ile yeniden üretin")
    for ad, govde, kaynak, maske, mad, itme in KAPLAMALAR:
        yol = os.path.join(args.doku, maske)
        kaplama_kopyasi(ad, bpy.data.objects[govde], kaynak, yol, malzeme(mad, yol), itme)
    ic = malzeme("M_LambaIc", renk=(1.0, 0.98, 0.94))
    for ad, govde in SERITLER:
        tavan_seridi(ad, bpy.data.objects[govde], ic)
    bpy.context.view_layer.update()
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.fbx(filepath=args.out or args.fbx, use_selection=True,
                             apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                             bake_space_transform=True, path_mode="STRIP", object_types={"MESH", "EMPTY"},
                             mesh_smooth_type="FACE")


if __name__ == "__main__":
    main()
