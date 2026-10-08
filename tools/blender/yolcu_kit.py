"""
Yolcu kiti (bpy): duraklarda bekleyen ve otobüse binen low-poly insanlar. Ortak palet materyali.

Her yolcu: kök boş obje + "Govde" (gövde, baş, saç) + "Bacak_Sol", "Bacak_Sag" (pivot kalçada) +
"Kol_Sol", "Kol_Sag" (pivot omuzda). Yürüme animasyonu kodla yapılır (Passenger.cs): bacak ve kollar
pivotları etrafında X ekseninde sallanır. Unity'de ön +Z, pivot ayakların ortasında, zeminde.

Kullanım:
    python yolcu_kit.py --out <klasör> [--render önizleme.png]
"""
import argparse
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402

import apartman_kit as kit  # noqa: E402


def U(f, r, z):
    """Unity (ileri, sağ, yukarı) → Blender."""
    return Vector((-r, -f, z))


class PersonBuilder(kit.MeshBuilder):
    def ubox(self, f0, f1, r0, r1, z0, z1, color):
        x0, x1 = sorted((-r1, -r0))
        y0, y1 = sorted((-f1, -f0))
        self.box(x0, x1, y0, y1, z0, z1, color)


# (ad, boy, cilt, saç, üst, alt, tip)  tip: erkek, kadin, basortulu, yasli, cocuk, ogrenci
YOLCULAR = [
    ("Yolcu_01_Takimli", 1.78, "cilt_orta", "sac_siyah", "kiyafet_lacivert", "kiyafet_lacivert", "erkek"),
    ("Yolcu_02_Kotlu", 1.74, "cilt_acik", "sac_kahve", "kiyafet_beyaz", "kiyafet_kot", "erkek"),
    ("Yolcu_03_Kadin", 1.65, "cilt_acik", "sac_kahve", "kiyafet_bordo", "kiyafet_siyah", "kadin"),
    ("Yolcu_04_Basortulu", 1.62, "cilt_orta", "basortusu_mavi", "kiyafet_bej", "kiyafet_bej", "basortulu"),
    ("Yolcu_05_Yasli", 1.70, "cilt_orta", "sac_gri", "kiyafet_gri", "kiyafet_haki", "yasli"),
    ("Yolcu_06_Ogrenci", 1.68, "cilt_esmer", "sac_siyah", "kiyafet_haki", "kiyafet_kot", "ogrenci"),
    ("Yolcu_07_Kadin", 1.68, "cilt_esmer", "sac_siyah", "kiyafet_beyaz", "kiyafet_kot", "kadin"),
    ("Yolcu_08_Basortulu", 1.60, "cilt_acik", "basortusu_bej", "kiyafet_siyah", "kiyafet_siyah", "basortulu"),
    ("Yolcu_09_Mont", 1.80, "cilt_acik", "sac_siyah", "kiyafet_siyah", "kiyafet_kot", "erkek"),
    ("Yolcu_10_Cocuk", 1.30, "cilt_orta", "sac_kahve", "kiyafet_bordo", "kiyafet_kot", "cocuk"),
]


def build_person(spec, material):
    name, h, skin, hair, top, bottom, kind = spec
    k = h / 1.75
    hip = 0.92 * k
    shoulder = 1.44 * k
    w = 0.22 * k  # yarım omuz genişliği
    root = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(root)
    parts = []

    body = PersonBuilder()
    skirt = kind in ("kadin", "basortulu")
    # gövde (kalçadan omuza, hafif daralan) ve kalça
    body.ubox(-0.11 * k, 0.11 * k, -w + 0.02, w - 0.02, hip, shoulder, top)
    body.ubox(-0.10 * k, 0.10 * k, -w + 0.04, w - 0.04, hip - 0.08 * k, hip + 0.02, bottom)
    if skirt:
        # uzun etek / pardösü eteği: bacakların üstünü örter
        body.ubox(-0.14 * k, 0.14 * k, -w + 0.01, w - 0.01, hip - 0.55 * k, hip, top if kind == "basortulu" else bottom)
    if kind == "ogrenci":
        body.ubox(-0.26 * k, -0.11 * k, -w + 0.06, w - 0.06, hip + 0.12 * k, shoulder - 0.05 * k, "kiyafet_bordo")  # sırt çantası
    # boyun ve baş
    body.ubox(-0.04 * k, 0.04 * k, -0.04 * k, 0.04 * k, shoulder, shoulder + 0.06 * k, skin)
    hz = shoulder + 0.06 * k
    body.sphere = None
    import bmesh
    geom = bmesh.ops.create_icosphere(body.bm, subdivisions=1, radius=1.0)
    from mathutils import Matrix
    hc = U(0.01 * k, 0, hz + 0.12 * k)
    bmesh.ops.transform(body.bm, matrix=Matrix.Translation(hc) @ Matrix.Diagonal((0.095 * k, 0.11 * k, 0.125 * k, 1)),
                        verts=geom["verts"])
    body._paint(geom["verts"], skin)
    if kind == "basortulu":
        geom = bmesh.ops.create_icosphere(body.bm, subdivisions=1, radius=1.0)
        bmesh.ops.transform(body.bm, matrix=Matrix.Translation(U(-0.015 * k, 0, hz + 0.13 * k)) @
                            Matrix.Diagonal((0.115 * k, 0.125 * k, 0.15 * k, 1)), verts=geom["verts"])
        body._paint(geom["verts"], hair)
        body.ubox(-0.09 * k, 0.07 * k, -0.13 * k, 0.13 * k, shoulder - 0.06 * k, hz + 0.05 * k, hair)
        # yüz açıklığı
        body.ubox(0.07 * k, 0.11 * k, -0.06 * k, 0.06 * k, hz + 0.06 * k, hz + 0.2 * k, skin)
    else:
        # saç: başın üstü ve arkası
        long_hair = kind == "kadin"
        body.ubox(-0.11 * k, 0.06 * k, -0.10 * k, 0.10 * k, hz + 0.16 * k, hz + 0.26 * k, hair)
        body.ubox(-0.11 * k, -0.04 * k, -0.10 * k, 0.10 * k, hz + (-0.05 if long_hair else 0.08) * k, hz + 0.2 * k, hair)
        if kind == "yasli":
            body.ubox(-0.12 * k, 0.12 * k, -0.12 * k, 0.12 * k, hz + 0.22 * k, hz + 0.26 * k, "kiyafet_gri")  # kasket
            body.ubox(0.08 * k, 0.18 * k, -0.11 * k, 0.11 * k, hz + 0.22 * k, hz + 0.24 * k, "kiyafet_gri")
    obj = body.to_object("Govde", material)
    obj.parent = root
    parts.append(obj)

    # bacaklar: pivot kalçada (nesne konumu), mesh aşağı uzanır
    leg_len = hip
    for side, nm in ((-1, "Bacak_Sol"), (1, "Bacak_Sag")):
        b = PersonBuilder()
        r = side * 0.10 * k
        b.ubox(-0.065 * k, 0.065 * k, -0.065 * k, 0.065 * k, -leg_len + 0.07, 0.0, bottom)
        b.ubox(-0.07 * k, 0.13 * k, -0.06 * k, 0.06 * k, -leg_len, -leg_len + 0.07, "ayakkabi")
        o = b.to_object(nm, material)
        o.location = U(0, r, hip)
        o.parent = root
        parts.append(o)
    # kollar: pivot omuzda
    arm_len = 0.62 * k
    for side, nm in ((-1, "Kol_Sol"), (1, "Kol_Sag")):
        b = PersonBuilder()
        b.ubox(-0.05 * k, 0.05 * k, -0.05 * k, 0.05 * k, -arm_len + 0.09 * k, 0.0, top)
        b.ubox(-0.04 * k, 0.04 * k, -0.04 * k, 0.04 * k, -arm_len, -arm_len + 0.09 * k, skin)
        if kind == "yasli" and side > 0:
            b.ubox(0.03, 0.06, -0.015, 0.015, -arm_len - (hip - arm_len * 0) + 0.55 * k, -arm_len, "kapi")  # baston
        o = b.to_object(nm, material)
        o.location = U(0, side * (w + 0.03 * k), shoulder - 0.03 * k)
        o.parent = root
        parts.append(o)
    tris = sum(len(p.vertices) - 2 for o in parts for p in o.data.polygons)
    return root, tris


def export(root, path):
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for c in root.children:
        c.select_set(True)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_scale_options="FBX_SCALE_ALL",
                             axis_forward="-Z", axis_up="Y", bake_space_transform=True, path_mode="STRIP",
                             object_types={"MESH", "EMPTY"}, mesh_smooth_type="FACE")


def render(roots, path):
    scene = bpy.context.scene
    n = len(roots)
    for i, r in enumerate(roots):
        r.location = ((i - (n - 1) / 2) * 0.75, 0, 0)
        r.rotation_euler = (0, 0, math.radians(20))
    # bir yolcuyu yürüme pozunda göster
    walker = roots[1]
    for c in walker.children:
        if c.name.startswith("Bacak_Sol") or c.name.startswith("Kol_Sag"):
            c.rotation_euler = (math.radians(25), 0, 0)
        elif c.name.startswith("Bacak_Sag") or c.name.startswith("Kol_Sol"):
            c.rotation_euler = (math.radians(-25), 0, 0)
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 32
    scene.cycles.use_denoising = True
    scene.render.resolution_x, scene.render.resolution_y = 1600, 600
    scene.view_settings.view_transform = "AgX"
    world = bpy.data.worlds.new("Gok")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.6, 0.72, 0.9, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.8
    scene.world = world
    sun = bpy.data.lights.new("Gunes", "SUN")
    sun.energy = 3.0
    so = bpy.data.objects.new("Gunes", sun)
    so.rotation_euler = (math.radians(50), 0, math.radians(-30))
    scene.collection.objects.link(so)
    m = bpy.data.materials.new("Zemin")
    m.use_nodes = True
    m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.55, 0.53, 0.5, 1)
    bpy.ops.mesh.primitive_plane_add(size=40)
    bpy.context.object.data.materials.append(m)
    cam = bpy.data.cameras.new("Kamera")
    cam.lens = 50
    co = bpy.data.objects.new("Kamera", cam)
    scene.collection.objects.link(co)
    co.location = (0, -9.5, 1.6)
    co.rotation_euler = (Vector((0, 0, 0.9)) - co.location).to_track_quat("-Z", "Y").to_euler()
    scene.camera = co
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True)
    ap.add_argument("--render", default=None)
    args = ap.parse_args(argv)
    os.makedirs(args.out, exist_ok=True)
    kit.clear_scene()
    img = kit.build_palette_image(os.path.join(args.out, "_palet.png"))
    img.pack()
    os.remove(os.path.join(args.out, "_palet.png"))
    mat = kit.palette_material(img)
    roots = []
    for spec in YOLCULAR:
        root, tris = build_person(spec, mat)
        export(root, os.path.join(args.out, spec[0] + ".fbx"))
        print(f"| `{spec[0]}` | {spec[1]:.2f} m | {tris} |")
        roots.append(root)
    if args.render:
        render(roots, args.render)


if __name__ == "__main__":
    main()
