"""
BMC Procity 12LF (Proton Bus Simulator modu) → Unity'ye hazır FBX.

Yaptıkları:
  - .3ds'i okur (tds_okuyucu), yolcu yol işaretlerini, ıslak cam ve "açık" durum kopyalarını atar.
  - Parçaları ayırır: Govde, Teker_OS/OD/AS/AD, Kapi_1_1..Kapi_3_2, Direksiyon, Lamba_* .
  - Tekerlek, kapı ve direksiyonun pivotlarını dönme noktalarına taşır (kapı pivotları rightdoorN.txt'den).
  - Aynı dokuyu kullanan materyalleri birleştirir (≈150 → ≈40), saydam olanları "_Cam" ekiyle ayırır.
  - Arka tekerleklerin üçgen sayısını düşürür.
  - Modeli 180° döndürür: Unity'de ön +Z, sağ +X olur. Kök pivot: zeminde, otobüsün tam ortası.

Kullanım:
    python bmc_donustur.py --src "BMC Procity 12LF/BMC Procity 12LF" --out <klasör> [--render önizleme.png]
"""
import argparse
import math
import os
import re
import shutil
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
import bmesh  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402

import tds_okuyucu as tds  # noqa: E402

DROP = [
    r"^_sit_", r"^_stand_", r"^_r\d[a-z]\d+", r"_wet", r"wetorclear", r"^_key_", r"^_cockpit_",
    r"^_autogear_", r"^_bt.*_on_", r"^_anim00[1-4]_", r"^_anim006_", r"^_anim007_", r"^_anim010_on",
    r"^_sunshade_on_", r"^_driver_window_on", r"^_passenger_windows_back_on", r"^_parking_brake_on_",
    r"^_blinkers_button_[lr]_$", r"^_mirror\d_$",
]
WHEELS = {"Teker_OnSol": "fl", "Teker_OnSag": "fr", "Teker_ArkaSol": "rl", "Teker_ArkaSag": "rr"}
LIGHTS = {
    "_tex_brake_": "Lamba_Fren", "_tex_left_blinker_": "Lamba_SinyalSol", "_tex_right_blinker_": "Lamba_SinyalSag",
    "_tex_low_beam_": "Lamba_KisaFar", "_tex_high_beam_": "Lamba_UzunFar", "_tex_external_lights_": "Lamba_Park",
    "_tex_light_1_": "Lamba_Ic1", "_tex_light_2_": "Lamba_Ic2", "_tex_light_3_": "Lamba_Ic3",
    "_anim008_on__emissive_": "Lamba_IcSerit",
}


def read_door_pivots(src):
    """rightdoorN.txt → {(kapı, kanat): (Blender x, y, z), açı}. Proton: y yukarı, z ileri."""
    piv = {}
    for n in (1, 2, 3):
        txt = open(os.path.join(src, f"rightdoor{n}.txt"), encoding="latin-1").read().replace("\r", "")
        rot = {int(k): float(v) for k, v in re.findall(r"anim_rotate_(\d)=(-?[\d.]+)", txt)}
        for leaf in (1, 2):
            sec = re.search(rf"\[anim1_door{leaf}\]\s*posX=(-?[\d.]+)\s*posY=(-?[\d.]+)\s*posZ=(-?[\d.]+)", txt)
            x, y, z = map(float, sec.groups())
            piv[(n, leaf)] = (Vector((x, z, y)), rot.get(leaf, 90.0))
    return piv


def classify(name):
    if any(re.search(p, name) for p in DROP):
        return None
    for lname, tag in LIGHTS.items():
        if name == lname:
            return tag
    m = re.search(r"_wheel_(fl|fr|rl|rr)_", name)
    if m:
        return next(k for k, v in WHEELS.items() if v == m.group(1))
    m = re.search(r"rdoor_(\d)x(\d)_", name)
    if m:
        return f"Kapi_{m.group(1)}_{m.group(2)}"
    if name.startswith("_steering_wheel_"):
        return "Direksiyon"
    return "Govde"


def build(src, out, render_path):
    model = tds.TDS(os.path.join(src, "models", "BMC Procity 12LF.3ds"))
    tex_dirs = [os.path.join(src, "textures"), os.path.join(src, "skins")]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    tex_out = os.path.join(out, "Textures")
    os.makedirs(tex_out, exist_ok=True)

    # materyalleri dokuya göre birleştir
    mat_by_key = {}
    copied = set()

    def material_for(mat_name, transparent):
        info = model.materials.get(mat_name, {"diffuse": (0.8, 0.8, 0.8), "texture": ""})
        path = tds.find_texture(info["texture"], tex_dirs)
        base = os.path.splitext(os.path.basename(path))[0] if path else None
        key = (base or "Renk_%02x%02x%02x" % tuple(int(c * 255) for c in info["diffuse"]), transparent)
        if key in mat_by_key:
            return mat_by_key[key]
        mat = bpy.data.materials.new("M_" + key[0] + ("_Cam" if transparent else ""))
        mat.use_nodes = True
        bsdf = mat.node_tree.nodes["Principled BSDF"]
        bsdf.inputs["Base Color"].default_value = (*info["diffuse"], 1.0)
        if path:
            dst = os.path.join(tex_out, os.path.basename(path))
            if dst not in copied:
                shutil.copyfile(path, dst)
                copied.add(dst)
            node = mat.node_tree.nodes.new("ShaderNodeTexImage")
            node.image = bpy.data.images.load(dst)
            mat.node_tree.links.new(node.outputs["Color"], bsdf.inputs["Base Color"])
            if transparent:
                mat.node_tree.links.new(node.outputs["Alpha"], bsdf.inputs["Alpha"])
        mat_by_key[key] = mat
        return mat

    groups = {}
    dropped = 0
    for o in model.objects:
        tag = classify(o["name"])
        if tag is None:
            dropped += 1
            continue
        transparent = "transparent" in o["name"]
        groups.setdefault(tag, []).append((o, transparent))

    # 180° döndür: Blender -Y ön → Unity +Z ön
    flip = Matrix.Rotation(math.pi, 4, "Z")
    objs = {}
    for tag, items in groups.items():
        bm = bmesh.new()
        uv_layer = bm.loops.layers.uv.new("UVMap")
        mats = []
        for o, transparent in items:
            vmap = [bm.verts.new(flip @ Vector(v)) for v in o["verts"]]
            face_mat = {}
            for mname, idx in o["face_mats"].items():
                for fi in idx:
                    face_mat[fi] = mname
            for fi, (a, b, c) in enumerate(o["faces"]):
                if len({a, b, c}) < 3:
                    continue
                try:
                    f = bm.faces.new((vmap[a], vmap[b], vmap[c]))
                except ValueError:
                    continue  # çift yüz
                mat = material_for(face_mat.get(fi, ""), transparent)
                if mat not in mats:
                    mats.append(mat)
                f.material_index = mats.index(mat)
                if o["uvs"]:
                    for loop, vi in zip(f.loops, (a, b, c)):
                        if vi < len(o["uvs"]):
                            loop[uv_layer].uv = o["uvs"][vi]
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0001)
        mesh = bpy.data.meshes.new(tag)
        bm.to_mesh(mesh)
        bm.free()
        for mat in mats:
            mesh.materials.append(mat)
        obj = bpy.data.objects.new(tag, mesh)
        bpy.context.scene.collection.objects.link(obj)
        objs[tag] = obj

    def set_origin(obj, point):
        obj.data.transform(Matrix.Translation(-point))
        obj.location = point

    # tekerlek, direksiyon: sınır kutusu merkezi
    for tag in list(WHEELS) + ["Direksiyon"]:
        o = objs[tag]
        bb = [Vector(c) for c in o.bound_box]
        set_origin(o, sum(bb, Vector()) / 8)
    # kapılar: Proton dönme noktaları (180° döndürülmüş)
    pivots = read_door_pivots(src)
    door_angles = {}
    for (n, leaf), (p, ang) in pivots.items():
        tag = f"Kapi_{n}_{leaf}"
        if tag in objs:
            pp = flip @ p
            set_origin(objs[tag], Vector((pp.x, pp.y, objs[tag].bound_box[0][2] * 0 + pp.z)))
            door_angles[tag] = ang

    # arka tekerlekleri sadeleştir (13.7k → ~3.4k üçgen)
    for tag in ("Teker_ArkaSol", "Teker_ArkaSag"):
        o = objs[tag]
        mod = o.modifiers.new("Decimate", "DECIMATE")
        mod.ratio = 0.25
        bpy.context.view_layer.objects.active = o
        bpy.ops.object.modifier_apply(modifier=mod.name)

    # kök: zeminde, otobüs ortası
    allpts = [o.matrix_world @ Vector(c) for o in objs.values() for c in o.bound_box]
    cx = (min(p.x for p in allpts) + max(p.x for p in allpts)) / 2
    cy = (min(p.y for p in allpts) + max(p.y for p in allpts)) / 2
    root = bpy.data.objects.new("BMC_Procity_12LF", None)
    bpy.context.scene.collection.objects.link(root)
    for o in objs.values():
        o.location -= Vector((cx, cy, 0))
        o.parent = root

    report = []
    total = 0
    for tag, o in sorted(objs.items()):
        tris = sum(len(p.vertices) - 2 for p in o.data.polygons)
        total += tris
        loc = o.location
        # Unity koordinatı: x = -Blender x, y = Blender z, z = -Blender y
        report.append(f"| `{tag}` | ({-loc.x:+.3f}, {loc.z:+.3f}, {-loc.y:+.3f}) | {tris} | {len(o.data.materials)} |"
                      + (f" ±90° (Proton: {door_angles[tag]:+.0f}°)" if tag in door_angles else ""))
    print(f"atılan obje: {dropped}, materyal: {len(mat_by_key)}, toplam üçgen: {total}")
    print("\n".join(report))
    with open(os.path.join(out, "parcalar.md"), "w") as fh:
        fh.write(f"Toplam üçgen: {total}, materyal: {len(mat_by_key)}\n\n")
        fh.write("| Parça | Unity yerel konum (x, y, z) | Üçgen | Materyal |\n|---|---|---|---|\n")
        fh.write("\n".join(report) + "\n")

    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.fbx(filepath=os.path.join(out, "BMC_Procity_12LF.fbx"), use_selection=True,
                             apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                             bake_space_transform=True, path_mode="STRIP", object_types={"MESH", "EMPTY"},
                             mesh_smooth_type="FACE")
    if render_path:
        for tag, o in objs.items():
            if tag.startswith("Lamba_"):
                o.hide_render = True
        tds.render(list(objs.values()), render_path)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    ap = argparse.ArgumentParser()
    ap.add_argument("--src", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--render", default=None)
    args = ap.parse_args(argv)
    os.makedirs(args.out, exist_ok=True)
    build(args.src, args.out, args.render)


if __name__ == "__main__":
    main()
