"""
Caio Millennium II: teker davlumbazlarını iki yüzlü yapar. Kaynak modelde davlumbazın iç duvarı (koridora bakan) tek yüzlü
ve normali tekere bakıyor; Unity arka yüzü çizmediği için içeriden teker ve zemin boşluğu görünüyordu (docs/RAPOR_Y13.md S4).
Her tekerin çevresinde tekere bakan yüzlerin ters yönlü bir kopyası eklenir (koridora bakar, malzemesi M_Gri).

Kullanım (her iki FBX için, millennium_donustur.py'den sonra; FBX'i yerinde yazar, ikinci kez çalıştırılmaz):
    Blender -b --factory-startup --python millennium_davlumbaz.py -- --fbx Caio_Millennium_II.fbx
"""
import argparse
import sys

import bpy  # noqa: E402
import bmesh  # noqa: E402
from mathutils import Vector  # noqa: E402

ISARET = "davlumbaz_iki_yuzlu"


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    ap = argparse.ArgumentParser()
    ap.add_argument("--fbx", required=True)
    ap.add_argument("--out", default=None)
    args = ap.parse_args(argv)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=args.fbx, axis_forward="-Z", axis_up="Y", bake_space_transform=True)
    govde = bpy.data.objects["Govde"]
    if govde.get(ISARET):
        sys.exit("bu FBX zaten işlenmiş")
    tekerler = [o for o in bpy.context.scene.objects if o.name.startswith("Teker_")]
    kutular = []
    for t in tekerler:
        pts = [t.matrix_world @ Vector(c) for c in t.bound_box]
        mn = Vector([min(p[i] for p in pts) for i in range(3)])
        mx = Vector([max(p[i] for p in pts) for i in range(3)])
        # tekerin çevresi: içe doğru 0,45 m, boyuna 0,3 m, üstüne 0,3 m
        ic = 0.45
        kutular.append((Vector((mn.x - ic, mn.y - 0.3, 0.25)), Vector((mx.x + ic, mx.y + 0.3, mx.z + 0.3)),
                        (mn + mx) * 0.5))

    mw = govde.matrix_world
    bm = bmesh.new()
    bm.from_mesh(govde.data)
    bm.faces.ensure_lookup_table()
    uv = bm.loops.layers.uv.active
    adlar = [m.name for m in govde.data.materials]
    gri = adlar.index("M_Gri") if "M_Gri" in adlar else -1  # iç yüz: düz gri (dış yüzün dokusu tekerlik içindi)
    secilen = []
    for f in bm.faces:
        c = mw @ f.calc_center_median()
        if abs(c.x) > 1.25:
            continue  # dış yan duvar
        n = (mw.to_3x3() @ f.normal).normalized()
        for a, b, merkez in kutular:
            # yalnızca tekere bakan yüzler (davlumbazın iç yüzü); kopyaları koridora bakar
            if a.x <= c.x <= b.x and a.y <= c.y <= b.y and a.z <= c.z <= b.z and n.dot(merkez - c) > 0.2:
                secilen.append(f)
                break
    for f in secilen:
        vs = [bm.verts.new(v.co) for v in reversed(f.verts)]
        nf = bm.faces.new(vs)
        nf.material_index = gri if gri >= 0 else f.material_index
        nf.smooth = f.smooth
        if uv is not None:
            for lo, lsrc in zip(nf.loops, reversed(list(f.loops))):
                lo[uv].uv = lsrc[uv].uv
    bm.to_mesh(govde.data)
    bm.free()
    govde[ISARET] = True
    print(f"iki yüzlü yapılan yüz: {len(secilen)}")
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.fbx(filepath=args.out or args.fbx, use_selection=True,
                             apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                             bake_space_transform=True, path_mode="STRIP", object_types={"MESH", "EMPTY"},
                             mesh_smooth_type="FACE", use_custom_props=True)


if __name__ == "__main__":
    main()
