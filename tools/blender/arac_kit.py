"""
Trafik araçları kiti (bpy): sarı Hyundai Accent Blue taksi, klasik Türkiye arabaları (Tofaş Şahin, Renault 12
Toros, Murat 131, Renault Broadway), Fiat Linea ve Ford Transit dolmuş. Low-poly, ortak palet materyali.

Her araç: kök boş obje + "Govde" + 4 tekerlek ("Teker_OnSol", "Teker_OnSag", "Teker_ArkaSol", "Teker_ArkaSag",
pivotları tekerlek merkezinde). Unity'de ön +Z, sağ +X, kök pivot zeminde aracın ortasında.

Kullanım:
    python arac_kit.py --out <klasör> [--render önizleme.png]
"""
import argparse
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402

import apartman_kit as kit  # noqa: E402


def B(fwd, right, up):
    """Unity çerçevesi (ileri, sağ, yukarı) → Blender koordinatı."""
    return Vector((-right, -fwd, up))


class CarBuilder(kit.MeshBuilder):
    def loft(self, stations, kinds, body, glass="arac_cam"):
        """stations: (ileri, z_alt, z_bel, z_tavan, yarı_genişlik_gövde, yarı_genişlik_tavan)
        kinds: istasyonlar arası bölüm tipi: body, ws (ön cam), rw (arka cam), cabin (yan cam), pillar."""
        rings = []
        for f, zb, zl, zt, wb, wt in stations:
            pts = [(-wb, zb), (-wb, zl), (-wt, zt), (wt, zt), (wb, zl), (wb, zb)]
            rings.append([self.bm.verts.new(B(f, r, z)) for r, z in pts])
        # kenar sırası: 0 sol alt yan, 1 sol üst yan, 2 tavan, 3 sağ üst yan, 4 sağ alt yan, 5 taban
        for i, kind in enumerate(kinds):
            a, b = rings[i], rings[i + 1]
            for e in range(6):
                j = (e + 1) % 6
                if e in (1, 3):
                    color = glass if kind in ("cabin", "ws", "rw") else body
                elif e == 2:
                    color = glass if kind in ("ws", "rw") else body
                elif e == 5:
                    color = "lastik"
                else:
                    color = body
                self._face([a[e], b[e], b[j], a[j]], color)
        self._face(list(reversed(rings[0])), body)
        self._face(rings[-1], body)

    def _face(self, verts, color):
        try:
            f = self.bm.faces.new(verts)
        except ValueError:
            return
        uv = kit.palette_uv(color)
        for loop in f.loops:
            loop[self.uv].uv = uv

    def ubox(self, f0, f1, r0, r1, z0, z1, color):
        """Unity çerçevesinde kutu (ileri f, sağ r, yukarı z)."""
        x0, x1 = sorted((-r1, -r0))
        y0, y1 = sorted((-f1, -f0))
        self.box(x0, x1, y0, y1, z0, z1, color)


def sedan_stations(L, W, H, *, belt, hood, trunk, ws, rw, nose_drop=0.12, tail_drop=0.08, tumble=0.17):
    """Üç hacimli sedan için istasyonlar ve bölüm tipleri."""
    hw = W / 2
    tw = hw - tumble
    zb = 0.28
    r_end, f_end = -L / 2, L / 2
    s = []
    s.append((r_end, zb + 0.06, belt - tail_drop, belt - tail_drop, hw - 0.06, hw - 0.1))
    s.append((r_end + 0.1, zb, belt, belt, hw, hw - 0.04))
    s.append((r_end + trunk, zb, belt, belt + 0.02, hw, hw - 0.06))
    roof_r = r_end + trunk + rw
    roof_f = f_end - hood - ws
    mid = (roof_r + roof_f) / 2
    s.append((roof_r, zb, belt, H, hw, tw))
    s.append((mid - 0.06, zb, belt, H, hw, tw))
    s.append((mid + 0.06, zb, belt, H, hw, tw))
    s.append((roof_f, zb, belt, H - 0.02, hw, tw))
    s.append((f_end - hood, zb, belt, belt + 0.04, hw, hw - 0.08))
    s.append((f_end - 0.1, zb, belt - nose_drop, belt - nose_drop, hw, hw - 0.04))
    s.append((f_end, zb + 0.06, belt - nose_drop - 0.06, belt - nose_drop - 0.06, hw - 0.06, hw - 0.1))
    kinds = ["body", "body", "rw", "cabin", "pillar", "cabin", "ws", "body", "body"]
    return s, kinds


def van_stations(L, W, H, *, belt=1.05, hood=0.55, ws=0.7):
    hw = W / 2
    zb = 0.32
    r_end, f_end = -L / 2, L / 2
    s = [
        (r_end, zb, belt, H - 0.05, hw - 0.02, hw - 0.08),
        (r_end + 0.08, zb, belt, H, hw, hw - 0.06),
        (r_end + 1.3, zb, belt, H, hw, hw - 0.06),
        (r_end + 1.36, zb, belt, H, hw, hw - 0.06),
        (f_end - hood - ws - 0.6, zb, belt, H, hw, hw - 0.06),
        (f_end - hood - ws, zb, belt, H - 0.04, hw, hw - 0.08),
        (f_end - hood, zb, belt, belt + 0.12, hw, hw - 0.12),
        (f_end, zb + 0.05, belt - 0.12, belt - 0.12, hw - 0.05, hw - 0.12),
    ]
    kinds = ["rw", "cabin", "pillar", "cabin", "cabin", "ws", "body"]
    return s, kinds


def details(cb, L, W, belt, *, body, classic, taxi=False, dolmus=False, H=1.4):
    hw = W / 2
    f_end, r_end = L / 2, -L / 2
    bumper = "krom" if classic else body
    # tamponlar
    cb.ubox(f_end - 0.08, f_end + 0.06, -hw + 0.05, hw - 0.05, 0.30, 0.48, bumper)
    cb.ubox(r_end - 0.06, r_end + 0.08, -hw + 0.05, hw - 0.05, 0.30, 0.48, bumper)
    # farlar, ızgara, stoplar, plakalar
    zl = belt - 0.32 if not dolmus else belt - 0.35
    for side in (-1, 1):
        if classic:
            cb.ubox(f_end - 0.02, f_end + 0.03, side * (hw - 0.42), side * (hw - 0.12), zl, zl + 0.16, "far")
        else:
            cb.ubox(f_end - 0.06, f_end + 0.02, side * (hw - 0.5), side * (hw - 0.1), zl + 0.04, zl + 0.16, "far")
        cb.ubox(r_end - 0.03, r_end + 0.02, side * (hw - 0.45), side * (hw - 0.1), zl + 0.02, zl + 0.18, "stop")
        # aynalar
        mf = f_end - (1.35 if not dolmus else 0.95)
        cb.ubox(mf - 0.06, mf + 0.06, side * hw, side * (hw + 0.14), belt + 0.08, belt + 0.2, body)
    cb.ubox(f_end - 0.01, f_end + 0.04, -hw + 0.45, hw - 0.45, zl, zl + 0.16, "korkuluk")
    cb.ubox(f_end + 0.05, f_end + 0.07, -0.26, 0.26, 0.32, 0.43, "plaka")
    cb.ubox(r_end - 0.07, r_end - 0.05, -0.26, 0.26, zl - 0.12, zl - 0.01, "plaka")
    if taxi:
        # tavan "TAKSİ" lambası ve kapılarda damalı şerit
        roof = H
        cb.ubox(-0.35, 0.05, -0.32, 0.32, roof, roof + 0.05, "lastik")
        cb.ubox(-0.32, 0.02, -0.3, 0.3, roof + 0.05, roof + 0.2, "tabela_beyaz")
        cb.ubox(-0.33, 0.03, -0.31, 0.31, roof + 0.2, roof + 0.23, "taksi_sari")
        f = -1.0
        k = 0
        while f < 1.0:
            for side in (-1, 1):
                cb.ubox(f, f + 0.12, side * hw, side * (hw + 0.006), belt - 0.18, belt - 0.1,
                        "lastik" if k % 2 == 0 else "araba_beyaz")
                cb.ubox(f, f + 0.12, side * hw, side * (hw + 0.006), belt - 0.26, belt - 0.18,
                        "araba_beyaz" if k % 2 == 0 else "lastik")
            f += 0.12
            k += 1
    if dolmus:
        cb.ubox(f_end - 0.95, f_end - 0.55, -0.45, 0.45, H, H + 0.22, "tabela_sari")
        # kayar kapı çizgisi
        cb.ubox(-0.2, -0.17, hw, hw + 0.01, 0.4, belt + 0.85, "korkuluk")
        cb.ubox(0.75, 0.78, hw, hw + 0.01, 0.4, belt + 0.85, "korkuluk")


def wheel(name, f, r, radius, width, material):
    cb = CarBuilder()
    # silindir z ekseninde üretilir; yana yatır (ekseni Unity sağ yönüne = Blender X)
    cb.cylinder(0, 0, -width / 2, width / 2, radius, "lastik", segments=12)
    side = 1 if r > 0 else -1
    cb.cylinder(0, 0, width / 2 - 0.01, width / 2 + 0.01, radius * 0.62, "jant", segments=10)
    rot = Matrix.Rotation(math.radians(90 * side), 4, "Y")
    import bmesh
    bmesh.ops.transform(cb.bm, matrix=rot, verts=list(cb.bm.verts))
    obj = cb.to_object(name, material)
    obj.location = B(f, r, radius)
    return obj


CARS = {
    # ad: (tip, boyutlar ve kesit ayarları, renk, klasik mi, taksi/dolmuş)
    "Taksi_AccentBlue": ("sedan", dict(L=4.37, W=1.70, H=1.46, wb=2.57, belt=0.95, hood=1.05, trunk=0.85, ws=0.95,
                                       rw=0.70, nose_drop=0.16, tumble=0.2), "taksi_sari", False, "taksi"),
    "Klasik_Sahin_Beyaz": ("sedan", dict(L=4.24, W=1.64, H=1.40, wb=2.45, belt=0.88, hood=1.15, trunk=0.95, ws=0.55,
                                         rw=0.30, nose_drop=0.06, tumble=0.14), "araba_beyaz", True, None),
    "Klasik_Sahin_Kirmizi": ("sedan", dict(L=4.24, W=1.64, H=1.40, wb=2.45, belt=0.88, hood=1.15, trunk=0.95, ws=0.55,
                                           rw=0.30, nose_drop=0.06, tumble=0.14), "araba_kirmizi", True, None),
    "Klasik_Sahin_Lacivert": ("sedan", dict(L=4.24, W=1.64, H=1.40, wb=2.45, belt=0.88, hood=1.15, trunk=0.95,
                                            ws=0.55, rw=0.30, nose_drop=0.06, tumble=0.14), "araba_lacivert", True, None),
    "Klasik_Toros_Mavi": ("sedan", dict(L=4.35, W=1.64, H=1.44, wb=2.44, belt=0.86, hood=1.10, trunk=0.70, ws=0.65,
                                        rw=0.80, nose_drop=0.10, tumble=0.15), "araba_mavi", True, None),
    "Klasik_Toros_Bej": ("sedan", dict(L=4.35, W=1.64, H=1.44, wb=2.44, belt=0.86, hood=1.10, trunk=0.70, ws=0.65,
                                       rw=0.80, nose_drop=0.10, tumble=0.15), "araba_bej", True, None),
    "Klasik_Murat131_Turuncu": ("sedan", dict(L=4.26, W=1.65, H=1.40, wb=2.49, belt=0.87, hood=1.05, trunk=0.90,
                                              ws=0.55, rw=0.35, nose_drop=0.04, tumble=0.13), "araba_turuncu", True, None),
    "Klasik_Murat131_Yesil": ("sedan", dict(L=4.26, W=1.65, H=1.40, wb=2.49, belt=0.87, hood=1.05, trunk=0.90,
                                            ws=0.55, rw=0.35, nose_drop=0.04, tumble=0.13), "araba_yesil", True, None),
    "Klasik_Broadway_Gri": ("sedan", dict(L=4.06, W=1.63, H=1.41, wb=2.48, belt=0.88, hood=0.95, trunk=0.80, ws=0.70,
                                          rw=0.45, nose_drop=0.08, tumble=0.15), "araba_gri", True, None),
    "Klasik_Broadway_Kirmizi": ("sedan", dict(L=4.06, W=1.63, H=1.41, wb=2.48, belt=0.88, hood=0.95, trunk=0.80,
                                              ws=0.70, rw=0.45, nose_drop=0.08, tumble=0.15), "araba_kirmizi", True, None),
    "Modern_Linea_Gri": ("sedan", dict(L=4.56, W=1.73, H=1.49, wb=2.60, belt=0.97, hood=1.10, trunk=0.85, ws=1.00,
                                       rw=0.75, nose_drop=0.18, tumble=0.21), "araba_gri", False, None),
    "Modern_Linea_Beyaz": ("sedan", dict(L=4.56, W=1.73, H=1.49, wb=2.60, belt=0.97, hood=1.10, trunk=0.85, ws=1.00,
                                         rw=0.75, nose_drop=0.18, tumble=0.21), "araba_beyaz", False, None),
    "Dolmus_Transit": ("van", dict(L=5.50, W=1.98, H=2.40, wb=3.30), "araba_beyaz", False, "dolmus"),
}


def build_car(name, spec, material):
    kind, p, color, classic, special = spec
    L, W, H, wb = p["L"], p["W"], p["H"], p["wb"]
    cb = CarBuilder()
    if kind == "sedan":
        st, kinds = sedan_stations(L, W, H, belt=p["belt"], hood=p["hood"], trunk=p["trunk"], ws=p["ws"], rw=p["rw"],
                                   nose_drop=p["nose_drop"], tumble=p["tumble"])
        belt = p["belt"]
        radius = 0.30
    else:
        st, kinds = van_stations(L, W, H)
        belt = 1.05
        radius = 0.36
    cb.loft(st, kinds, color)
    details(cb, L, W, belt, body=color, classic=classic, taxi=special == "taksi", dolmus=special == "dolmus", H=H)
    body = cb.to_object("Govde", material, recalc_normals=True)
    root = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(root)
    body.parent = root
    track = W / 2 - 0.12
    front_axle = wb / 2 + (0.05 if kind == "sedan" else 0.35)
    rear_axle = front_axle - wb
    for wname, f, r in (("Teker_OnSol", front_axle, -track), ("Teker_OnSag", front_axle, track),
                        ("Teker_ArkaSol", rear_axle, -track), ("Teker_ArkaSag", rear_axle, track)):
        w = wheel(wname, f, r, radius, 0.2, material)
        w.parent = root
    tris = sum(len(pp.vertices) - 2 for o in [body] + list(root.children) if o.type == "MESH"
               for pp in o.data.polygons)
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
    x = 0.0
    for i, r in enumerate(roots):
        r.location = (x, (i % 2) * 2.5, 0)
        x += 2.6
    for r in roots:
        r.location.x -= x / 2
        r.rotation_euler = (0, 0, math.radians(35))
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 32
    scene.cycles.use_denoising = True
    scene.render.resolution_x, scene.render.resolution_y = 1600, 700
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Medium High Contrast"
    world = bpy.data.worlds.new("Gok")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.6, 0.72, 0.9, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.7
    scene.world = world
    sun = bpy.data.lights.new("Gunes", "SUN")
    sun.energy = 3.5
    so = bpy.data.objects.new("Gunes", sun)
    so.rotation_euler = (math.radians(50), 0, math.radians(-30))
    scene.collection.objects.link(so)
    mat = bpy.data.materials.new("Zemin")
    mat.use_nodes = True
    mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.3, 0.3, 0.31, 1)
    bpy.ops.mesh.primitive_plane_add(size=200)
    bpy.context.object.data.materials.append(mat)
    cam = bpy.data.cameras.new("Kamera")
    cam.lens = 40
    co = bpy.data.objects.new("Kamera", cam)
    scene.collection.objects.link(co)
    co.location = (0, -22, 7)
    co.rotation_euler = (Vector((0, 1.5, 0.8)) - co.location).to_track_quat("-Z", "Y").to_euler()
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
    for name, spec in CARS.items():
        root, tris = build_car(name, spec, mat)
        export(root, os.path.join(args.out, name + ".fbx"))
        print(f"| `{name}` | {spec[1]['L']:.2f} × {spec[1]['W']:.2f} × {spec[1]['H']:.2f} | {tris} |")
        roots.append(root)
    if args.render:
        # her modelden bir tane göster
        seen, show = set(), []
        for r in roots:
            key = r.name.split("_")[1]
            if key not in seen:
                seen.add(key)
                show.append(r)
            else:
                for c in [r] + list(r.children):
                    c.hide_render = True
        render(show, args.render)


if __name__ == "__main__":
    main()
