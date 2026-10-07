"""
OMSI 2 .o3d model okuyucu (sürüm 1-3, şifresiz). Blender'a mesh olarak aktarır.

Kullanım:
    python o3d_okuyucu.py <o3d klasörü veya dosyaları> --textures <doku klasörü> --out model.fbx [--render önizleme.png]

Dosya yapısı (bölüm etiketiyle başlar, sıra serbest):
    0x84 0x19 <sürüm>              başlık (sürüm >= 3 ise ek bayrak baytı ve 4 baytlık anahtar)
    0x17 vertex listesi            sayı + (konum 3f, normal 3f, uv 2f)
    0x49 üçgen listesi             sayı + (3 indeks + materyal indeksi u16)
    0x26 materyal listesi          sayı + (diffuse 4f, specular 3f, emissive 3f, power f, doku adı)
    0x54 kemik listesi             (okunur, atlanır)
    0x79 dönüşüm matrisi           16f
Not: OMSI'de y ve z eksenleri Blender'a göre yer değiştirmiştir; okuyucu bunu düzeltir.
"""
import argparse
import math
import os
import struct
import sys


class O3D:
    def __init__(self, path):
        self.path = path
        self.verts, self.tris, self.mats, self.matrix = [], [], [], None
        data = open(path, "rb").read()
        if data[:2] != b"\x84\x19":
            raise ValueError("o3d değil")
        self.version = data[2]
        pos = 3
        long_idx = False
        if self.version >= 3:
            flags = data[3]
            long_idx = bool(flags & 1)
            key = struct.unpack_from("<I", data, 4)[0]
            if key != 0xFFFFFFFF and key != 0:
                raise ValueError(f"şifreli o3d (anahtar {key:#x})")
            pos = 8
        self.long_idx = long_idx
        cnt = lambda p: (struct.unpack_from("<I", data, p)[0], p + 4) if long_idx else (struct.unpack_from("<H", data, p)[0], p + 2)
        idx_fmt, idx_sz = ("<3I", 12) if long_idx else ("<3H", 6)
        while pos < len(data):
            tag = data[pos]
            pos += 1
            if tag == 0x17:
                n, pos = cnt(pos)
                for _ in range(n):
                    self.verts.append(struct.unpack_from("<8f", data, pos))
                    pos += 32
            elif tag == 0x49:
                n, pos = cnt(pos)
                for _ in range(n):
                    a, b, c = struct.unpack_from(idx_fmt, data, pos)
                    m = struct.unpack_from("<H", data, pos + idx_sz)[0]
                    self.tris.append((a, b, c, m))
                    pos += idx_sz + 2
            elif tag == 0x26:
                n = struct.unpack_from("<H", data, pos)[0]
                pos += 2
                for _ in range(n):
                    f = struct.unpack_from("<11f", data, pos)
                    pos += 44
                    ln = data[pos]
                    pos += 1
                    name = data[pos:pos + ln].decode("latin-1")
                    pos += ln
                    self.mats.append({"diffuse": f[0:4], "texture": name})
            elif tag == 0x54:
                n = struct.unpack_from("<H", data, pos)[0]
                pos += 2
                for _ in range(n):
                    ln = data[pos]
                    pos += 1 + ln
                    w, pos = cnt(pos)
                    pos += w * ((4 if long_idx else 2) + 4)
            elif tag == 0x79:
                self.matrix = struct.unpack_from("<16f", data, pos)
                pos += 64
            else:
                raise ValueError(f"bilinmeyen bölüm {tag:#x} @ {pos - 1}")


def bounds(models):
    xs, ys, zs = [], [], []
    for m in models:
        for v in m.verts:
            xs.append(v[0]); ys.append(v[1]); zs.append(v[2])
    return (min(xs), max(xs)), (min(ys), max(ys)), (min(zs), max(zs))


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    ap = argparse.ArgumentParser()
    ap.add_argument("inputs", nargs="+")
    ap.add_argument("--info", action="store_true", help="Yalnızca özet yazdır (Blender gerekmez)")
    ap.add_argument("--textures", default=None)
    ap.add_argument("--out", default=None)
    ap.add_argument("--render", default=None)
    args = ap.parse_args(argv)

    files = []
    for p in args.inputs:
        if os.path.isdir(p):
            files += sorted(os.path.join(p, f) for f in os.listdir(p) if f.lower().endswith(".o3d"))
        else:
            files.append(p)
    models = []
    for f in files:
        try:
            m = O3D(f)
        except Exception as e:  # noqa: BLE001
            print(f"ATLANDI {os.path.basename(f)}: {e}")
            continue
        models.append(m)
        if args.info:
            (x0, x1), (y0, y1), (z0, z1) = bounds([m]) if m.verts else ((0, 0), (0, 0), (0, 0))
            tex = sorted({mt["texture"] for mt in m.mats})
            print(f"{os.path.basename(f):22s} v{m.version} {len(m.verts):6d} vtx {len(m.tris):6d} tri  "
                  f"x[{x0:6.2f},{x1:6.2f}] y[{y0:6.2f},{y1:6.2f}] z[{z0:5.2f},{z1:5.2f}]  {', '.join(tex)[:70]}")
    if args.info:
        (x0, x1), (y0, y1), (z0, z1) = bounds(models)
        print(f"TOPLAM: {sum(len(m.tris) for m in models)} üçgen, boyut x {x1 - x0:.2f} y {y1 - y0:.2f} z {z1 - z0:.2f}")
        return
    to_blender(models, args)


def to_blender(models, args):
    import bpy
    bpy.ops.wm.read_factory_settings(use_empty=True)
    mat_cache = {}

    def material(tex, diffuse):
        key = tex or f"renk_{diffuse}"
        if key in mat_cache:
            return mat_cache[key]
        mat = bpy.data.materials.new(os.path.splitext(tex)[0] if tex else "Renk")
        mat.use_nodes = True
        bsdf = mat.node_tree.nodes["Principled BSDF"]
        bsdf.inputs["Base Color"].default_value = diffuse
        path = os.path.join(args.textures, tex) if (tex and args.textures) else None
        if path and not os.path.exists(path):
            # OMSI dokuları bazen farklı uzantıyla durur
            base = os.path.splitext(path)[0]
            for ext in (".dds", ".png", ".bmp", ".tga", ".jpg"):
                if os.path.exists(base + ext):
                    path = base + ext
                    break
        if path and os.path.exists(path):
            try:
                img = bpy.data.images.load(path)
                node = mat.node_tree.nodes.new("ShaderNodeTexImage")
                node.image = img
                mat.node_tree.links.new(node.outputs["Color"], bsdf.inputs["Base Color"])
            except RuntimeError as e:
                print("doku yüklenemedi", path, e)
        mat_cache[key] = mat
        return mat

    objs = []
    for m in models:
        if not m.tris:
            continue
        name = os.path.splitext(os.path.basename(m.path))[0]
        mesh = bpy.data.meshes.new(name)
        # OMSI: x sağ, y yukarı, z ileri (sol el) -> Blender: x sağ, y ileri, z yukarı
        co = [(v[0], v[2], v[1]) for v in m.verts]
        faces = [(a, c, b) for a, b, c, _ in m.tris]
        mesh.from_pydata(co, [], faces)
        uv = mesh.uv_layers.new(name="UVMap")
        for poly, (a, b, c, mi) in zip(mesh.polygons, m.tris):
            for li, vi in zip(poly.loop_indices, (a, c, b)):
                u, v = m.verts[vi][6], m.verts[vi][7]
                uv.data[li].uv = (u, 1.0 - v)
        used = sorted({t[3] for t in m.tris})
        remap = {}
        for mi in used:
            info = m.mats[mi] if mi < len(m.mats) else {"diffuse": (0.8, 0.8, 0.8, 1), "texture": ""}
            mesh.materials.append(material(info["texture"], tuple(info["diffuse"])))
            remap[mi] = len(mesh.materials) - 1
        for poly, t in zip(mesh.polygons, m.tris):
            poly.material_index = remap[t[3]]
        mesh.validate()
        obj = bpy.data.objects.new(name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        objs.append(obj)

    if args.out:
        bpy.ops.export_scene.fbx(filepath=args.out, apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z",
                                 axis_up="Y", bake_space_transform=True, path_mode="RELATIVE",
                                 object_types={"MESH"})
    if args.render:
        render(objs, args.render)


def render(objs, path):
    import bpy
    from mathutils import Vector
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 24
    scene.cycles.use_denoising = True
    scene.render.resolution_x, scene.render.resolution_y = 1400, 800
    scene.view_settings.view_transform = "AgX"
    world = bpy.data.worlds.new("Gok")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.6, 0.7, 0.85, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 1.0
    scene.world = world
    sun = bpy.data.lights.new("Gunes", "SUN")
    sun.energy = 3.0
    so = bpy.data.objects.new("Gunes", sun)
    so.rotation_euler = (math.radians(45), 0, math.radians(30))
    scene.collection.objects.link(so)
    pts = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    center = (lo + hi) / 2
    size = (hi - lo).length
    cam = bpy.data.cameras.new("Kamera")
    cam.lens = 35
    co = bpy.data.objects.new("Kamera", cam)
    scene.collection.objects.link(co)
    co.location = center + Vector((size * 0.75, -size * 0.55, size * 0.3))
    co.rotation_euler = (center - co.location).to_track_quat("-Z", "Y").to_euler()
    scene.camera = co
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    main()
