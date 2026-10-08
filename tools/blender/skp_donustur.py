"""
SketchUp modellerini (önce tools/skp/skp_to_glb.py ile GLB'ye çevrilmiş) oyuna hazır FBX'e dönüştürür.

Yaptıkları:
  - Google Earth zemin/fotoğraf parçalarını, insan figürlerini ve gereksiz 3D yazıları atar.
  - Milimetreyi metreye çevirir, bütün parçaları tek mesh'te birleştirir.
  - openskp boyanmamış yüzleri katman rengiyle (kırmızı) verir: ön/arka çiftinde kırmızı tarafı atar,
    tek kalan kırmızı yüzleri SketchUp varsayılanı gibi açık griye boyar.
  - Tek taraflı kalan her yüzü ters kopyalar: SketchUp yüzleri iki taraftan görünür, Unity arka yüzü çizmez.
  - Bütün malzeme ve dokuları tek bir atlas dokusuna bake eder: model başına tek materyal (mobilde tek draw call).
  - Ön cepheyi Blender'da -Y'ye (Unity'de +Z) çevirir; pivot ön cephe ortası, zemin seviyesi (bina kitiyle aynı kural).
  - Kızılay blokları: Google Earth'ten çizilmiş binaları tek tek ayırır, her birini eksene hizalar.

Çıktı (her model için):  <maps>/<klasör>/<Ad>.fbx  ve  <maps>/<klasör>/T_<Ad>.png (atlas)
Kızılay bloklarında atlas ortak: Buildings/KizilayBloklari/T_KizilayBloklari.png ve Bloklar.json (ölçüler).
Unity'de HaritaKurucu, yanında T_*.png olan modele palet yerine kendi materyalini (M_*.mat) verir.

Kullanım (Blender'ın kendi Python'u ile):
    Blender -b --factory-startup -P tools/blender/skp_donustur.py -- \
        --glb <glb klasörü> --maps AnkaraBusSimulator/Assets/_Project/Maps [--only EmekIshani ...] [--render-dir <klasör>]
"""
import argparse
import json
import math
import os
import re
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector
from mathutils.geometry import intersect_point_tri

# openskp'nin boyanmamış yüzlere verdiği katman rengi (Layer0: 255, 84, 84)
FALLBACK_RGB = (1.0, 84 / 255, 84 / 255)
DEFAULT_RGB = (0.86, 0.86, 0.84)  # SketchUp varsayılan ön yüz rengi (hafif kırık beyaz)

GOOGLE_EARTH = r"Google_Earth|GOOGLE_EARTH"
PEOPLE = r"__Bryce_|2D_Man"

# Kalite önce: geometri ve dokular olduğu gibi kalır, yalnızca modelin parçası olmayan şeyler atılır
# (Google Earth zemin fotoğrafı, 2D insan figürleri). atlas=None: orijinal malzeme ve dokular korunur.
# ad: kaynak GLB, klasör (Maps/SKP altında), atılacak obje adları (regex), ön cepheyi -Y'ye getiren Z dönüşü
MODELS = {
    "KizilayAVM": dict(glb="kizi", folder="Landmarks", drop=[GOOGLE_EARTH, PEOPLE], rot_z=0.0),
    "EmekIshani": dict(
        glb="gokdelen", folder="Landmarks", drop=[PEOPLE], rot_z=0.0,
        drop_ground=True,  # binanın altındaki asfalt plakası (oyunda yol ve kaldırım ayrı)
    ),
    "GuvenlikAniti": dict(glb="guvenpark", folder="Landmarks", drop=[GOOGLE_EARTH, PEOPLE], rot_z=0.0),
    "Lamba_Nostaljik": dict(
        glb="lamba", folder="Props", drop=[], rot_z=0.0, scale=1.4, pivot="center",
        # LOD0 tam detay; uzakta sadeleştirilmiş kopyalar (Unity _LOD0/_LOD1/_LOD2 adlarından LODGroup kurar)
        lods=[None, 4000, 900],
    ),
    "KizilayBloklari": dict(glb="adsiz", folder="Buildings/KizilayBloklari", drop=[GOOGLE_EARTH, PEOPLE], split=True),
}


# ---------------------------------------------------------------------------
# yükleme ve birleştirme
# ---------------------------------------------------------------------------
def load_glb(path, drop):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=path)
    pats = [re.compile(p) for p in drop]
    meshes = []
    for o in list(bpy.context.scene.objects):
        if o.type != "MESH":
            continue
        if any(p.search(o.name) for p in pats):
            continue
        meshes.append(o)
    # örnekler (instance) aynı mesh'i paylaşır: kopyala, dünya dönüşümünü mesh'e göm
    for o in meshes:
        mw = o.matrix_world.copy()
        o.data = o.data.copy()
        o.parent = None
        o.data.transform(mw)
        o.matrix_world = Matrix.Identity(4)
    for o in list(bpy.context.scene.objects):
        if o not in meshes:
            bpy.data.objects.remove(o, do_unlink=True)
    # SketchUp "Image" objeleri: duvarın üstüne yapıştırılmış fotoğraf düzlemleri; çakışmada en öncelikli
    for o in meshes:
        attr = o.data.attributes.new("oncelik", "INT", "FACE")
        prio = 2 if re.search(r"__Image(#\d+)?_", o.name) else 0
        attr.data.foreach_set("value", [prio] * len(o.data.polygons))
    bpy.ops.object.select_all(action="DESELECT")
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    obj.name = obj.data.name = "Model"
    obj.data.transform(Matrix.Scale(0.001, 4))  # mm → m
    return obj


def material_rgb(mat):
    """glTF malzemesinin taban rengi ve doku olup olmadığı."""
    bsdf = next((n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
    if bsdf is None:
        return None, False
    inp = bsdf.inputs["Base Color"]
    textured = any(n.type == "TEX_IMAGE" for n in mat.node_tree.nodes)
    return tuple(inp.default_value[:3]), textured


def is_fallback(mat):
    rgb, textured = material_rgb(mat)
    return rgb is not None and not textured and all(abs(a - b) < 0.01 for a, b in zip(rgb, FALLBACK_RGB))


def _inside(p, face):
    """p noktası (yüzün düzleminde) çokgen yüzün içinde mi (yelpaze üçgenleri üzerinden)."""
    vs = [v.co for v in face.verts]
    return any(intersect_point_tri(p, vs[0], vs[i], vs[i + 1]) for i in range(1, len(vs) - 1))


def clean_mesh(obj, drop_ground=False):
    """Üst üste binen yüzleri çözer, kırmızı varsayılanları boyar, tek taraflı yüzleri ters kopyalar.

    Öncelik: fotoğraf düzlemi (Image) > dokulu yüz > düz renk > boyanmamış (kırmızı).
      1. Ön/arka çiftinde boyanmamış taraf atılır; tek taraflı kalan yüzler ters kopyalanır
         (SketchUp'ta fotoğraf düzlemleri çoğu zaman binanın içine bakar, ters kopyası dışa bakar).
      2. Aynı köşelerde aynı yöne bakan kopyalardan en öncelikli olan kalır.
      3. Aynı düzlemde, merkezi daha öncelikli bir yüzün içinde kalan yüz silinir (duvar fotoğrafın altında kalır).
      4. Hâlâ aynı düzlemi paylaşan öncelikli yüzler 2 cm öne alınır (Unity'de z-fighting olmasın).
    """
    me = obj.data
    fallback = {i for i, m in enumerate(me.materials) if m and is_fallback(m)}
    textured = {i for i, m in enumerate(me.materials) if m and material_rgb(m)[1]}
    default_mat = bpy.data.materials.new("Varsayilan")
    default_mat.use_nodes = True
    bsdf = default_mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*DEFAULT_RGB, 1.0)
    me.materials.append(default_mat)
    default_idx = len(me.materials) - 1

    bm = bmesh.new()
    bm.from_mesh(me)
    # remove_doubles kullanılmaz: aynı köşelere oturan yüzleri (duvar + fotoğrafı) sessizce siler.
    # Yüzler köşe koordinatlarıyla (1 mm) eşleştirilir.
    if drop_ground:
        ground = [f for f in bm.faces if all(v.co.z < 0.05 for v in f.verts)]
        bmesh.ops.delete(bm, geom=ground, context="FACES")
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.calc_area() < 1e-7], context="FACES")
    bm.normal_update()

    def vkey(f):
        return frozenset((round(v.co.x, 3), round(v.co.y, 3), round(v.co.z, 3)) for v in f.verts)

    layer = bm.faces.layers.int.get("oncelik")
    prio = {}
    for f in bm.faces:
        if f.material_index in fallback:
            prio[f] = -1
        else:
            prio[f] = max(f[layer] if layer else 0, 1 if f.material_index in textured else 0)

    # 1. ön/arka çiftleri ve tek taraflı yüzler
    by_verts = {}
    for f in bm.faces:
        by_verts.setdefault(vkey(f), []).append(f)
    drop, singles = [], []
    for faces in by_verts.values():
        painted = [f for f in faces if prio[f] >= 0]
        if painted and len(painted) < len(faces):
            drop += [f for f in faces if prio[f] < 0]
            faces = painted
        for f in faces:
            if not any(f.normal.dot(g.normal) < -0.99 for g in faces):
                singles.append(f)
    bmesh.ops.delete(bm, geom=drop, context="FACES")
    singles = [f for f in singles if f.is_valid]
    dup = bmesh.ops.duplicate(bm, geom=singles)
    for f in singles:
        prio[dup["face_map"][f]] = prio[f]
    bmesh.ops.reverse_faces(bm, faces=[dup["face_map"][f] for f in singles], flip_multires=False)
    bm.normal_update()

    # 2. aynı köşeler, aynı yön: en öncelikli kalır
    by_verts = {}
    for f in bm.faces:
        by_verts.setdefault(vkey(f), []).append(f)
    drop = set()
    for faces in by_verts.values():
        kept = []
        for f in sorted(faces, key=lambda f: -prio[f]):
            if any(f.normal.dot(k.normal) > 0.99 for k in kept):
                drop.add(f)
            else:
                kept.append(f)

    # 3. aynı düzlem: daha öncelikli yüzün altında kalan yüz silinir
    def plane_key(f):
        n = f.normal
        return (round(n.x, 2), round(n.y, 2), round(n.z, 2), round(n.dot(f.verts[0].co) / 0.02))
    by_plane = {}
    for f in bm.faces:
        if f not in drop:
            by_plane.setdefault(plane_key(f), []).append(f)
    offset = {}
    for faces in by_plane.values():
        # aynı öncelikte üst üste binen farklı malzemeler (büyük fotoğrafın üstüne yamanmış küçük fotoğraf):
        # küçük olan üstte kalır
        for f in faces:
            if any(g.material_index != f.material_index and prio[g] == prio[f]
                   and g.calc_area() > 1.2 * f.calc_area() and _inside(f.calc_center_median(), g)
                   for g in faces):
                offset[f] = 0.01
        levels = sorted({prio[f] for f in faces})
        if len(levels) < 2:
            continue
        for f in faces:
            higher = [g for g in faces if prio[g] > prio[f]]
            if higher and any(_inside(f.calc_center_median(), g) for g in higher):
                drop.add(f)
        left = [f for f in faces if f not in drop]
        lowest = min((prio[f] for f in left), default=0)
        for f in left:
            if prio[f] > lowest:
                offset[f] = offset.get(f, 0.0) + 0.02 * (levels.index(prio[f]) - levels.index(lowest))
    bmesh.ops.delete(bm, geom=list(drop), context="FACES")

    # 4. öne alınacak yüzler komşularından ayrılıp normal yönünde kaydırılır
    for dist in sorted({d for f, d in offset.items() if f.is_valid}):
        faces = [f for f, d in offset.items() if d == dist and f.is_valid]
        dup = bmesh.ops.duplicate(bm, geom=faces)
        for f in faces:
            for v in dup["face_map"][f].verts:
                v.co += f.normal * dist
        bmesh.ops.delete(bm, geom=faces, context="FACES")

    for f in bm.faces:
        if f.material_index in fallback:
            f.material_index = default_idx
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    bm.to_mesh(me)
    bm.free()
    me.update()



def decimate(obj, target_tris):
    """Lamba gibi çok tekrarlanan objeler için: önce düz yüzleri eritir, sonra hedef üçgen sayısına indirir."""
    me = obj.data
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.002)
    bm.to_mesh(me)
    bm.free()
    mod = obj.modifiers.new("Planar", "DECIMATE")
    mod.decimate_type = "DISSOLVE"
    mod.angle_limit = math.radians(4)
    mod.delimit = {"MATERIAL"}
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=mod.name)
    tris = sum(len(p.vertices) - 2 for p in me.polygons)
    if tris > target_tris:
        mod = obj.modifiers.new("Collapse", "DECIMATE")
        mod.decimate_type = "COLLAPSE"
        mod.ratio = target_tris / tris
        mod.use_collapse_triangulate = True
        bpy.ops.object.modifier_apply(modifier=mod.name)


# ---------------------------------------------------------------------------
# malzemeler ve dokular
# ---------------------------------------------------------------------------
def material_key(mat):
    """Görünüşü belirleyen şeyler: renk, opaklık, doku. Aynı görünen malzemeler birleştirilir."""
    nt = mat.node_tree
    bsdf = next((n for n in nt.nodes if n.type == "BSDF_PRINCIPLED"), None)
    rgb = tuple(round(c, 3) for c in bsdf.inputs["Base Color"].default_value[:3]) if bsdf else ()
    alpha = round(bsdf.inputs["Alpha"].default_value, 3) if bsdf else 1.0
    imgs = tuple(sorted(n.image.name for n in nt.nodes if n.type == "TEX_IMAGE" and n.image))
    return rgb, alpha, imgs


def simplify_material(mat):
    """glTF içe aktarıcısı dokuyu renk çarpanıyla çarpan düğümler ekler; FBX dışa aktarıcısı bu durumda dokuyu
    hiç yazmaz. Dokuyu doğrudan Base Color'a bağlar (SketchUp da dokuyu çarpansız gösterir)."""
    nt = mat.node_tree
    bsdf = next((n for n in nt.nodes if n.type == "BSDF_PRINCIPLED"), None)
    tex = next((n for n in nt.nodes if n.type == "TEX_IMAGE" and n.image), None)
    if bsdf is None or tex is None:
        return
    for link in list(bsdf.inputs["Base Color"].links):
        nt.links.remove(link)
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Base Color"].default_value = (1, 1, 1, 1)
    for n in list(nt.nodes):
        if n.type in ("MIX", "MIX_RGB", "VECT_MATH", "MATH", "SEPARATE_COLOR") and not any(l.is_valid for o in n.outputs for l in o.links):
            nt.nodes.remove(n)


def merge_materials(obj, name):
    """Aynı görünen malzemeleri birleştirir (openskp tek/çift taraflı kopyalarını ayrı malzeme yapar),
    kalanlara okunur adlar verir. Görünüş değişmez; yalnızca draw call azalır."""
    me = obj.data
    for m in me.materials:
        if m is not None:
            simplify_material(m)
    keys, remap, mats = {}, {}, []
    for i, m in enumerate(me.materials):
        if m is None:
            continue
        k = material_key(m)
        if k not in keys:
            keys[k] = len(mats)
            mats.append(m)
        remap[i] = keys[k]
    used = {p.material_index for p in me.polygons}
    idx = [remap.get(p.material_index, 0) for p in me.polygons]
    me.materials.clear()
    for j, m in enumerate(mats):
        m.name = f"{name}_{j:02d}"
        m.use_backface_culling = True  # yüzler zaten iki taraflı kopyalandı
        if material_key(m)[1] < 0.999:
            m.surface_render_method = "BLENDED"
        me.materials.append(m)
    me.polygons.foreach_set("material_index", idx)
    # hiç kullanılmayan malzemeleri at
    in_use = sorted({remap[i] for i in used if i in remap})
    if len(in_use) < len(mats):
        order = {old: new for new, old in enumerate(in_use)}
        kept = [mats[i] for i in in_use]
        me.materials.clear()
        for m in kept:
            me.materials.append(m)
        me.polygons.foreach_set("material_index", [order[i] for i in idx])
    me.update()


def save_textures(objs, tex_dir, prefix):
    """GLB'ye gömülü dokuları PNG olarak yazar; FBX bunlara göreli yolla bağlanır (Unity otomatik bulur)."""
    os.makedirs(tex_dir, exist_ok=True)
    images = []
    for o in objs:
        for m in o.data.materials:
            for n in m.node_tree.nodes:
                if n.type == "TEX_IMAGE" and n.image and n.image not in images:
                    images.append(n.image)
    for i, img in enumerate(images):
        path = os.path.join(tex_dir, f"{prefix}_{i:02d}.png")
        img.filepath_raw = path
        img.file_format = "PNG"
        img.save()
        img.name = os.path.basename(path)
        img.filepath = path
    return images

# ---------------------------------------------------------------------------
# yön, pivot, ayırma
# ---------------------------------------------------------------------------
def bounds(obj):
    vs = [v.co for v in obj.data.vertices]
    mn = Vector((min(v.x for v in vs), min(v.y for v in vs), min(v.z for v in vs)))
    mx = Vector((max(v.x for v in vs), max(v.y for v in vs), max(v.z for v in vs)))
    return mn, mx


def place_pivot(obj, rot_z=0.0, mode="front", ground=None):
    """Ön cephe -Y'ye; pivot ön cephenin ortası (mode="front") ya da taban ortası (mode="center"), zemin = 0."""
    obj.data.transform(Matrix.Rotation(math.radians(rot_z), 4, "Z"))
    mn, mx = bounds(obj)
    cx = (mn.x + mx.x) / 2
    cy = mn.y if mode == "front" else (mn.y + mx.y) / 2
    cz = mn.z if ground is None else ground
    obj.data.transform(Matrix.Translation((-cx, -cy, -cz)))


def footprint_angle(obj):
    """Tabanın en küçük alanlı dikdörtgeninin açısı (dönen kumpas, dışbükey zarf üzerinde)."""
    pts = sorted({(round(v.co.x, 3), round(v.co.y, 3)) for v in obj.data.vertices})
    if len(pts) < 3:
        return 0.0

    def cross(o, a, b):
        return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])
    lower, upper = [], []
    for p in pts:
        while len(lower) >= 2 and cross(lower[-2], lower[-1], p) <= 0:
            lower.pop()
        lower.append(p)
    for p in reversed(pts):
        while len(upper) >= 2 and cross(upper[-2], upper[-1], p) <= 0:
            upper.pop()
        upper.append(p)
    hull = lower[:-1] + upper[:-1]
    best = (float("inf"), 0.0)
    for i in range(len(hull)):
        a, b = hull[i], hull[(i + 1) % len(hull)]
        ang = math.atan2(b[1] - a[1], b[0] - a[0])
        c, s = math.cos(-ang), math.sin(-ang)
        xs = [p[0] * c - p[1] * s for p in hull]
        ys = [p[0] * s + p[1] * c for p in hull]
        w, d = max(xs) - min(xs), max(ys) - min(ys)
        if w * d < best[0]:
            # uzun kenar X boyunca (yola paralel) olsun
            best = (w * d, -ang if w >= d else -ang + math.pi / 2)
    return best[1]


def split_buildings(obj):
    """Ayrık parçaları ayırır; tabanları üst üste binen parçaları (çatı, çıkıntı) aynı binada toplar."""
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.separate(type="LOOSE")
    bpy.ops.object.mode_set(mode="OBJECT")
    parts = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    boxes = []
    for o in parts:
        mn, mx = bounds(o)
        boxes.append((mn, mx))
    parent = list(range(len(parts)))

    def find(i):
        while parent[i] != i:
            parent[i] = parent[parent[i]]
            i = parent[i]
        return i
    for i in range(len(parts)):
        for j in range(i + 1, len(parts)):
            (a0, a1), (b0, b1) = boxes[i], boxes[j]
            ox = min(a1.x, b1.x) - max(a0.x, b0.x)
            oy = min(a1.y, b1.y) - max(a0.y, b0.y)
            # küçük parçanın tabanı neredeyse tamamen öbürünün içindeyse aynı bina (çatı katı, çıkıntı).
            # Yan yana duran eğik binaların sınır kutuları da kısmen çakışır; onlar birleşmemeli.
            small = min((a1.x - a0.x) * (a1.y - a0.y), (b1.x - b0.x) * (b1.y - b0.y))
            if ox > 0 and oy > 0 and ox * oy > 0.85 * max(small, 1e-6):
                parent[find(i)] = find(j)
    clusters = {}
    for i, o in enumerate(parts):
        clusters.setdefault(find(i), []).append(o)
    result = []
    for objs in clusters.values():
        bpy.ops.object.select_all(action="DESELECT")
        for o in objs:
            o.select_set(True)
        bpy.context.view_layer.objects.active = objs[0]
        if len(objs) > 1:
            bpy.ops.object.join()
        result.append(bpy.context.view_layer.objects.active)
    return result


# ---------------------------------------------------------------------------
# dışa aktarma
# ---------------------------------------------------------------------------
def export_fbx(objs, path):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_scale_options="FBX_SCALE_ALL",
                             axis_forward="-Z", axis_up="Y", bake_space_transform=True,
                             mesh_smooth_type="FACE", path_mode="RELATIVE", object_types={"MESH"})


def tri_count(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


def make_lods(obj, name, targets):
    """LOD0 = obj (tam detay); diğerleri kopyalanıp hedef üçgen sayısına sadeleştirilir."""
    lods = []
    for i, target in enumerate(targets):
        o = obj if i == 0 else obj.copy()
        if i:
            o.data = obj.data.copy()
            bpy.context.scene.collection.objects.link(o)
            decimate(o, target)
        o.name = o.data.name = f"{name}_LOD{i}"
        lods.append(o)
    return lods


def convert(name, spec, glb_dir, maps):
    path = os.path.join(glb_dir, spec["glb"] + ".glb")
    obj = load_glb(path, spec["drop"])
    src_tris = tri_count(obj)
    if spec.get("scale"):
        obj.data.transform(Matrix.Scale(spec["scale"], 4))
    clean_mesh(obj, drop_ground=spec.get("drop_ground", False))
    merge_materials(obj, name)
    out_dir = os.path.join(maps, "SKP", spec["folder"])
    os.makedirs(out_dir, exist_ok=True)
    tex_dir = os.path.join(out_dir, "Textures")
    save_textures([obj], tex_dir, name)

    if not spec.get("split"):
        place_pivot(obj, spec.get("rot_z", 0.0), spec.get("pivot", "front"))
        objs = make_lods(obj, name, spec["lods"]) if spec.get("lods") else [obj]
        if len(objs) == 1:
            obj.name = obj.data.name = name
        export_fbx(objs, os.path.join(out_dir, name + ".fbx"))
        mn, mx = bounds(obj)
        size = mx - mn
        lod_txt = " / ".join(str(tri_count(o)) for o in objs)
        print(f"[skp] {name}: kaynak {src_tris} → {lod_txt} üçgen, {len(obj.data.materials)} malzeme, "
              f"{size.x:.1f} × {size.y:.1f} × {size.z:.1f} m")
        return [obj]

    # Kızılay blokları: her bina ayrı FBX, dokular ortak klasörde
    blocks = split_buildings(obj)
    keep = []
    for o in blocks:
        mn, mx = bounds(o)
        if (mx.x - mn.x) < 4 or (mx.y - mn.y) < 4 or mx.z < 6:
            bpy.data.objects.remove(o, do_unlink=True)  # kırıntı
        else:
            keep.append(o)
    keep.sort(key=lambda o: (round(bounds(o)[0].x), round(bounds(o)[0].y)))
    info = {}
    for i, o in enumerate(keep, 1):
        bname = f"Blok_{i:02d}"
        o.data.transform(Matrix.Rotation(footprint_angle(o), 4, "Z"))
        # Google Earth zemini z = 0'da; binalar eğimli araziye oturmak için aşağı uzanıyor
        place_pivot(o, 0.0, "center", ground=0.0)
        o.name = o.data.name = bname
        # birleşik mesh'ten kalan, bu binada kullanılmayan malzeme yuvalarını at
        used = sorted({p.material_index for p in o.data.polygons})
        mats = [o.data.materials[j] for j in used]
        order = {old: new for new, old in enumerate(used)}
        idx = [order[p.material_index] for p in o.data.polygons]
        o.data.materials.clear()
        for m in mats:
            o.data.materials.append(m)
        o.data.polygons.foreach_set("material_index", idx)
        export_fbx([o], os.path.join(out_dir, bname + ".fbx"))
        mn, mx = bounds(o)
        info[bname] = {"w": round(mx.x - mn.x, 2), "d": round(mx.y - mn.y, 2), "h": round(mx.z, 2),
                       "tris": tri_count(o)}
    with open(os.path.join(out_dir, "Bloklar.json"), "w", encoding="utf-8") as fh:
        json.dump(info, fh, ensure_ascii=False, indent=1)
    print(f"[skp] {name}: {src_tris} üçgen → {len(keep)} bina, toplam {sum(v['tris'] for v in info.values())} üçgen")
    return keep

# ---------------------------------------------------------------------------
# önizleme (Unity gibi arka yüzler çizilmez)
# ---------------------------------------------------------------------------
def render_views(objs, png, views=((0, "ön"), (90, "sağ"), (180, "arka"), (270, "sol"))):
    sc = bpy.context.scene
    for o in objs:
        for m in o.data.materials:
            m.use_backface_culling = True
    mn = Vector((min(bounds(o)[0][i] for o in objs) for i in range(3)))
    mx = Vector((max(bounds(o)[1][i] for o in objs) for i in range(3)))
    ctr, size = (mn + mx) / 2, (mx - mn).length
    cam = bpy.data.objects.new("Kamera", bpy.data.cameras.new("Kamera"))
    sc.collection.objects.link(cam)
    cam.data.lens = 40
    cam.data.clip_end = size * 20
    sc.camera = cam
    sun = bpy.data.objects.new("Gunes", bpy.data.lights.new("Gunes", "SUN"))
    sun.data.energy = 3.5
    sun.rotation_euler = (math.radians(50), 0, math.radians(30))
    sc.collection.objects.link(sun)
    world = bpy.data.worlds.new("Gok")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.75, 0.82, 0.9, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.8
    sc.world = world
    sc.render.engine = "BLENDER_EEVEE"
    sc.render.resolution_x, sc.render.resolution_y = 640, 640
    sc.view_settings.view_transform = "Standard"
    paths = []
    base, ext = os.path.splitext(png)
    for deg, label in views:
        a = math.radians(deg)
        d = Vector((math.sin(a), -math.cos(a), 0.45)).normalized()  # 0° = önden (−Y)
        cam.location = ctr + d * size * 1.25
        cam.rotation_euler = (ctr - cam.location).to_track_quat("-Z", "Y").to_euler()
        sc.render.filepath = f"{base}_{label}{ext}"
        bpy.ops.render.render(write_still=True)
        paths.append(sc.render.filepath)
    return paths


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    ap = argparse.ArgumentParser()
    ap.add_argument("--glb", required=True, help="skp_to_glb.py çıktısı")
    ap.add_argument("--maps", required=True, help="Assets/_Project/Maps")
    ap.add_argument("--only", nargs="*", default=None)
    ap.add_argument("--render-dir", default=None)
    args = ap.parse_args(argv)
    for name, spec in MODELS.items():
        if args.only and name not in args.only:
            continue
        objs = convert(name, spec, args.glb, args.maps)
        if args.render_dir:
            os.makedirs(args.render_dir, exist_ok=True)
            render_views(objs, os.path.join(args.render_dir, name + ".png"))


if __name__ == "__main__":
    main()
