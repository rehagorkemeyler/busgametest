"""
.3ds model okuyucu (Proton Bus Simulator modları için). Blender'a mesh + materyal olarak aktarır,
isteğe bağlı FBX'e çevirir ve önizleme render'ı alır.

Kullanım:
    python tds_okuyucu.py model.3ds --info
    python tds_okuyucu.py model.3ds --textures <doku klasörleri...> --out model.fbx [--render önizleme.png]

Proton modlarında Y ve Z eksenleri Blender'a göre ters çevrilmiştir (ini dosyasındaki uyarı);
3ds dosyasının kendisi Blender'dan Z-yukarı olarak çıkarıldığından okuyucu ekstra dönüş yapmaz.
"""
import argparse
import math
import os
import struct
import sys
from collections import defaultdict

MAIN, EDIT, OBJECT, TRIMESH = 0x4D4D, 0x3D3D, 0x4000, 0x4100
VERTS, FACES, FACE_MAT, UVS, LOCAL = 0x4110, 0x4120, 0x4130, 0x4140, 0x4160
MATERIAL, MAT_NAME, MAT_DIFFUSE, MAT_TEXMAP, MAT_TEXFILE = 0xAFFF, 0xA000, 0xA020, 0xA200, 0xA300
COLOR_24, LIN_COLOR_24, COLOR_F = 0x0011, 0x0012, 0x0010


def cstr(data, pos):
    end = data.index(b"\x00", pos)
    return data[pos:end].decode("latin-1"), end + 1


class TDS:
    def __init__(self, path):
        self.objects = []      # {name, verts, faces, uvs, face_mats: {mat: [face idx]}, matrix}
        self.materials = {}    # name -> {diffuse, texture}
        data = open(path, "rb").read()
        cid, ln = struct.unpack_from("<HI", data, 0)
        if cid != MAIN:
            raise ValueError("3ds değil")
        self._walk(data, 6, ln, None)

    def _children(self, data, start, end):
        pos = start
        while pos + 6 <= end:
            cid, ln = struct.unpack_from("<HI", data, pos)
            if ln < 6:
                break
            yield cid, pos + 6, min(pos + ln, end)
            pos += ln

    def _walk(self, data, start, end, ctx):
        for cid, s, e in self._children(data, start, end):
            if cid == EDIT:
                self._walk(data, s, e, None)
            elif cid == OBJECT:
                name, p = cstr(data, s)
                obj = {"name": name, "verts": [], "faces": [], "uvs": [], "face_mats": defaultdict(list), "matrix": None}
                self._walk(data, p, e, obj)
                if obj["faces"]:
                    self.objects.append(obj)
            elif cid == TRIMESH:
                self._walk(data, s, e, ctx)
            elif cid == VERTS:
                n = struct.unpack_from("<H", data, s)[0]
                ctx["verts"] = list(struct.iter_unpack("<3f", data[s + 2:s + 2 + 12 * n]))
            elif cid == UVS:
                n = struct.unpack_from("<H", data, s)[0]
                ctx["uvs"] = list(struct.iter_unpack("<2f", data[s + 2:s + 2 + 8 * n]))
            elif cid == FACES:
                n = struct.unpack_from("<H", data, s)[0]
                ctx["faces"] = [f[:3] for f in struct.iter_unpack("<4H", data[s + 2:s + 2 + 8 * n])]
                self._walk(data, s + 2 + 8 * n, e, ctx)
            elif cid == FACE_MAT:
                mname, p = cstr(data, s)
                n = struct.unpack_from("<H", data, p)[0]
                ctx["face_mats"][mname] += list(struct.unpack_from(f"<{n}H", data, p + 2))
            elif cid == LOCAL:
                ctx["matrix"] = struct.unpack_from("<12f", data, s)
            elif cid == MATERIAL:
                mat = {"name": "", "diffuse": (0.8, 0.8, 0.8), "texture": ""}
                for c2, s2, e2 in self._children(data, s, e):
                    if c2 == MAT_NAME:
                        mat["name"] = cstr(data, s2)[0]
                    elif c2 == MAT_DIFFUSE:
                        for c3, s3, e3 in self._children(data, s2, e2):
                            if c3 in (COLOR_24, LIN_COLOR_24):
                                mat["diffuse"] = tuple(b / 255 for b in data[s3:s3 + 3])
                            elif c3 == COLOR_F:
                                mat["diffuse"] = struct.unpack_from("<3f", data, s3)
                    elif c2 == MAT_TEXMAP:
                        for c3, s3, e3 in self._children(data, s2, e2):
                            if c3 == MAT_TEXFILE:
                                mat["texture"] = cstr(data, s3)[0]
                self.materials[mat["name"]] = mat


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    ap = argparse.ArgumentParser()
    ap.add_argument("input")
    ap.add_argument("--info", action="store_true")
    ap.add_argument("--textures", nargs="*", default=[])
    ap.add_argument("--out", default=None)
    ap.add_argument("--render", default=None)
    args = ap.parse_args(argv)
    m = TDS(args.input)
    if args.info:
        allv = [v for o in m.objects for v in o["verts"]]
        for o in m.objects:
            xs = [v[0] for v in o["verts"]]; ys = [v[1] for v in o["verts"]]; zs = [v[2] for v in o["verts"]]
            mats = ",".join(o["face_mats"].keys())
            print(f"{o['name']:28s} {len(o['faces']):6d} tri  x[{min(xs):6.2f},{max(xs):6.2f}] "
                  f"y[{min(ys):6.2f},{max(ys):6.2f}] z[{min(zs):5.2f},{max(zs):5.2f}]  {mats[:60]}")
        print(f"TOPLAM {len(m.objects)} obje, {sum(len(o['faces']) for o in m.objects)} üçgen, {len(m.materials)} materyal")
        for k, v in m.materials.items():
            print(f"  materyal {k:24s} doku={v['texture']}")
        print("sınırlar x", min(v[0] for v in allv), max(v[0] for v in allv),
              "y", min(v[1] for v in allv), max(v[1] for v in allv), "z", min(v[2] for v in allv), max(v[2] for v in allv))
        return
    to_blender(m, args)


def find_texture(name, dirs):
    if not name:
        return None
    base = os.path.splitext(os.path.basename(name))[0].lower()
    for d in dirs:
        for f in os.listdir(d):
            if os.path.splitext(f)[0].lower() == base:
                return os.path.join(d, f)
    return None


def to_blender(m, args):
    import bpy
    bpy.ops.wm.read_factory_settings(use_empty=True)
    mats = {}
    for name, info in m.materials.items():
        mat = bpy.data.materials.new(name or "Materyal")
        mat.use_nodes = True
        bsdf = mat.node_tree.nodes["Principled BSDF"]
        bsdf.inputs["Base Color"].default_value = (*info["diffuse"], 1.0)
        path = find_texture(info["texture"], args.textures)
        if path:
            try:
                node = mat.node_tree.nodes.new("ShaderNodeTexImage")
                node.image = bpy.data.images.load(path)
                mat.node_tree.links.new(node.outputs["Color"], bsdf.inputs["Base Color"])
                if path.lower().endswith(".png"):
                    mat.node_tree.links.new(node.outputs["Alpha"], bsdf.inputs["Alpha"])
            except RuntimeError as e:
                print("doku yüklenemedi", path, e)
        elif info["texture"]:
            print("doku bulunamadı:", info["texture"])
        mats[name] = mat

    objs = []
    for o in m.objects:
        mesh = bpy.data.meshes.new(o["name"])
        mesh.from_pydata(o["verts"], [], o["faces"])
        if o["uvs"]:
            uv = mesh.uv_layers.new(name="UVMap")
            for poly in mesh.polygons:
                for li in poly.loop_indices:
                    vi = mesh.loops[li].vertex_index
                    if vi < len(o["uvs"]):
                        uv.data[li].uv = o["uvs"][vi]
        order = list(o["face_mats"].keys())
        for name in order:
            mesh.materials.append(mats.get(name) or bpy.data.materials.new(name))
        for mi, name in enumerate(order):
            for fi in o["face_mats"][name]:
                if fi < len(mesh.polygons):
                    mesh.polygons[fi].material_index = mi
        mesh.validate()
        obj = bpy.data.objects.new(o["name"], mesh)
        bpy.context.scene.collection.objects.link(obj)
        objs.append(obj)

    if args.out:
        bpy.ops.export_scene.fbx(filepath=args.out, apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z",
                                 axis_up="Y", bake_space_transform=True, path_mode="COPY", embed_textures=False,
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
    co.location = center + Vector((size * 0.7, size * 0.6, size * 0.25))
    co.rotation_euler = (center - co.location).to_track_quat("-Z", "Y").to_euler()
    scene.camera = co
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    main()
