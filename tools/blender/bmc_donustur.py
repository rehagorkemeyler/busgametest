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
    python bmc_donustur.py --src "BMC Procity 12LF/BMC Procity 12LF" --out <klasör> [--render önizleme.png] [--mobil]

İki çıktı repoda:
    BMC_Procity_12LF.fbx            --mobil ile (düşük/orta grafik ayarı): iç mekân sadeleştirilmiş, doku atlası, gölge kabuğu
    BMC_Procity_12LF_TamKalite.fbx  seçeneksiz (yüksek grafik ayarı); üretildikten sonra bu adla kaydedilir
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
    r"^_wipers1_\.0(0[2-9]|1\d|20)$",  # silecek animasyonunun 19 ara karesi (yalnızca .001 kalır)
]
WHEELS = {"Teker_OnSol": "fl", "Teker_OnSag": "fr", "Teker_ArkaSol": "rl", "Teker_ArkaSag": "rr"}
LIGHTS = {
    "_tex_brake_": "Lamba_Fren", "_tex_left_blinker_": "Lamba_SinyalSol", "_tex_right_blinker_": "Lamba_SinyalSag",
    "_tex_low_beam_": "Lamba_KisaFar", "_tex_high_beam_": "Lamba_UzunFar", "_tex_external_lights_": "Lamba_Park",
    "_tex_light_1_": "Lamba_Ic1", "_tex_light_2_": "Lamba_Ic2", "_tex_light_3_": "Lamba_Ic3",
    "_anim008_on__emissive_": "Lamba_IcSerit",
}


def is_interior(o):
    """Gövde kabuğunun tamamen içinde kalan obje mi (tutamaklar, tavan, kokpit iç parçaları)?"""
    n = o["name"]
    if any(k in n for k in ("skin", "transparent", "window", "wiper", "mirror", "_tex_", "wheel", "rdoor", "steering")):
        return False
    xs = [abs(v[0]) for v in o["verts"]]
    ys = [v[1] for v in o["verts"]]
    zs = [v[2] for v in o["verts"]]
    return max(xs) < 1.17 and min(zs) > 0.30 and max(zs) < 2.92 and min(ys) > -5.55 and max(ys) < 5.80


# Atlasa girmeyen materyaller: kaplama değişen (gövde, tavan modülü), lambalar (ışık açılınca ayrı ele alınacak),
# plaka ve saydam camlar.
ATLAS_HARIC = ("M_caroserie", "M_cngtank", "M_lumini", "M_registration_plates")
ATLAS_BOYUT = 4096


def build_atlas(objs, tex_out, name="Atlas_BMC"):
    """Küçük ve orta dokuları tek bir atlasa toplar; yüz bazında UV'leri atlas hücresine taşır.
    Bir yüzün UV'si tek bir doku karesini aşıyorsa (gerçek tekrar) o yüz eski materyalinde kalır."""
    import numpy as np
    from PIL import Image

    tris = {}
    for o in objs:
        for p in o.data.polygons:
            m = o.data.materials[p.material_index]
            tris[m.name] = tris.get(m.name, 0) + 1
    cands = {}
    for mname in tris:
        m = bpy.data.materials[mname]
        if mname.startswith(ATLAS_HARIC) or mname.endswith("_Cam"):
            continue
        imgs = [n.image for n in m.node_tree.nodes if n.type == "TEX_IMAGE" and n.image]
        if imgs:
            cands[mname] = imgs[0]

    def plan(scale):
        cells = {}
        for mname, img in cands.items():
            w, h = img.size
            side = (1024 if tris[mname] >= 3000 else 512) * scale
            k = min(1.0, side / max(w, h))
            cells[mname] = (max(16, int(w * k)), max(16, int(h * k)))
        order = sorted(cells, key=lambda n: -cells[n][1])
        x = y = shelf = 0
        pos = {}
        pad = 8
        for n in order:
            w, h = cells[n]
            if x + w + pad > ATLAS_BOYUT:
                x, y = 0, y + shelf + pad
                shelf = 0
            if y + h + pad > ATLAS_BOYUT:
                return None
            pos[n] = (x + pad // 2, y + pad // 2, w, h)
            x += w + pad
            shelf = max(shelf, h)
        return pos

    scale = 1.0
    pos = plan(scale)
    while pos is None:
        scale *= 0.85
        pos = plan(scale)

    atlas = Image.new("RGB", (ATLAS_BOYUT, ATLAS_BOYUT), (128, 128, 128))
    for mname, (px, py, w, h) in pos.items():
        img = cands[mname]
        src = Image.open(bpy.path.abspath(img.filepath)).convert("RGB").resize((w, h), Image.LANCZOS)
        # kenar taşması için hücreyi 4 px büyütülmüş kopyasıyla çevrele
        atlas.paste(src.resize((w + 8, h + 8)), (px - 4, py - 4))
        atlas.paste(src, (px, py))
    path = os.path.join(tex_out, name + ".png")
    atlas.save(path, optimize=True)

    amat = bpy.data.materials.new("M_" + name)
    amat.use_nodes = True
    node = amat.node_tree.nodes.new("ShaderNodeTexImage")
    node.image = bpy.data.images.load(path)
    amat.node_tree.links.new(node.outputs["Color"], amat.node_tree.nodes["Principled BSDF"].inputs["Base Color"])

    A = float(ATLAS_BOYUT)
    moved = kept = 0
    for o in objs:
        me = o.data
        if not any(m.name in pos for m in me.materials):
            continue
        if amat.name not in me.materials:
            me.materials.append(amat)
        ai = list(me.materials).index(amat)
        uv = me.uv_layers.active.data
        for p in me.polygons:
            mname = me.materials[p.material_index].name
            if mname not in pos:
                continue
            us = [uv[li].uv[0] for li in p.loop_indices]
            vs = [uv[li].uv[1] for li in p.loop_indices]
            fu, fv = math.floor(min(us) + 1e-4), math.floor(min(vs) + 1e-4)
            if max(us) - fu > 1.002 or max(vs) - fv > 1.002:
                kept += 1
                continue
            px, py, w, h = pos[mname]
            u0, v0 = (px + 0.5) / A, 1.0 - (py + h - 0.5) / A
            su, sv = (w - 1.0) / A, (h - 1.0) / A
            for li in p.loop_indices:
                u, v = uv[li].uv
                uv[li].uv = (u0 + min(max(u - fu, 0.0), 1.0) * su, v0 + min(max(v - fv, 0.0), 1.0) * sv)
            p.material_index = ai
            moved += 1
        # kullanılmayan materyal yuvalarını at
        used = sorted({p.material_index for p in me.polygons})
        remap = {old: new for new, old in enumerate(used)}
        keep = [me.materials[i] for i in used]
        idx = [remap[p.material_index] for p in me.polygons]
        me.materials.clear()
        for m in keep:
            me.materials.append(m)
        for p, i in zip(me.polygons, idx):
            p.material_index = i
    print(f"atlas: {len(pos)} doku, ölçek {scale:.2f}, {moved} yüz atlasa taşındı, {kept} yüz tekrar ettiği için kaldı")
    return pos


def shadow_proxy(objs, name="Golge_Govde"):
    """Otobüsün dış kabuğundan (gövde + tekerlekler) dışbükey gölge objesi."""
    bm = bmesh.new()
    for o in objs:
        for v in o.data.vertices:
            bm.verts.new(o.matrix_world @ v.co)
    bmesh.ops.convex_hull(bm, input=bm.verts)
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context="VERTS")
    bmesh.ops.dissolve_limit(bm, angle_limit=math.radians(8), verts=bm.verts, edges=bm.edges)
    bmesh.ops.triangulate(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(bpy.data.materials.get("M_Golge") or bpy.data.materials.new("M_Golge"))
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    return obj


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


def build(src, out, render_path, mobil=False):
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
        if mobil and tag == "Govde" and is_interior(o):
            tag = "Ic"
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

    # iç mekânı sadeleştirip gövdeye kat (dışarıdan camdan görünür, ama bu kadar detay gerekmez)
    ic = objs.pop("Ic", None)
    if ic is not None:
        mod = ic.modifiers.new("Decimate", "DECIMATE")
        mod.ratio = 0.45
        bpy.context.view_layer.objects.active = ic
        bpy.ops.object.modifier_apply(modifier=mod.name)
        bpy.ops.object.select_all(action="DESELECT")
        ic.select_set(True)
        objs["Govde"].select_set(True)
        bpy.context.view_layer.objects.active = objs["Govde"]
        bpy.ops.object.join()

    if mobil:
        build_atlas([o for t, o in objs.items() if not t.startswith("Lamba_")], tex_out)
        objs["Golge_Govde"] = shadow_proxy([objs["Govde"]] + [objs[t] for t in WHEELS])

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
        shown = [o for tag, o in objs.items() if not tag.startswith(("Lamba_", "Golge_"))]
        for tag, o in objs.items():
            if tag.startswith(("Lamba_", "Golge_")):
                o.hide_render = True
        tds.render(shown, render_path)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    ap = argparse.ArgumentParser()
    ap.add_argument("--src", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--render", default=None)
    ap.add_argument("--mobil", action="store_true",
                    help="Düşük seviye cihazlar için: iç mekânı sadeleştir, dokuları atlasa topla (küçülterek), "
                         "gölgeyi basit kabuğa ver. Görsel kaliteyi düşürür; varsayılan kapalı.")
    args = ap.parse_args(argv)
    os.makedirs(args.out, exist_ok=True)
    build(args.src, args.out, args.render, args.mobil)


if __name__ == "__main__":
    main()
