"""
Ankara apartman kiti üreticisi (Blender 4.x / 5.x, bpy).

Her bina tek mesh + tek materyalden oluşur. Renkler 256x256'lık ortak bir palet
dokusundan (atlas) gelir; böylece mobilde bina başına tek draw call olur.

Kullanım (Blender olmadan, pip ile kurulan bpy modülüyle):
    python apartman_kit.py --out <klasör> [--render]
Blender içinden:
    blender -b -P apartman_kit.py -- --out <klasör> [--render]

Koordinatlar: 1 birim = 1 m. Pivot ön cephenin ortası, zemin seviyesi.
Ön cephe Blender'da -Y yönüne bakar.
"""
import argparse
import math
import os
import random
import sys

import bpy  # bpy önce yüklenmeli; bmesh/mathutils onunla gelir
import bmesh
from mathutils import Matrix, Vector

# ---------------------------------------------------------------------------
# Palet
# ---------------------------------------------------------------------------
GRID = 8
CELL = 32
PALETTE = {
    # sıvalar
    "krem": (0.93, 0.88, 0.76), "bej": (0.85, 0.76, 0.62), "somon": (0.91, 0.69, 0.58),
    "acik_sari": (0.95, 0.86, 0.58), "gri": (0.74, 0.74, 0.72), "beyaz_siva": (0.92, 0.92, 0.89),
    "acik_yesil": (0.78, 0.84, 0.70), "kiremit_siva": (0.80, 0.55, 0.45),
    # detay
    "silme": (0.96, 0.95, 0.92), "pvc": (0.97, 0.97, 0.97), "cam": (0.22, 0.30, 0.36), "perde": (0.78, 0.72, 0.62), "perde_koyu": (0.55, 0.45, 0.40),
    "dukkan_cam": (0.30, 0.40, 0.45), "beton": (0.58, 0.58, 0.56), "korkuluk": (0.18, 0.19, 0.20),
    "kapi": (0.35, 0.24, 0.17), "tas": (0.66, 0.62, 0.56), "kiremit": (0.65, 0.28, 0.20),
    "catı_beton": (0.50, 0.50, 0.49), "klima": (0.90, 0.90, 0.88), "klima_izgara": (0.45, 0.45, 0.45),
    "depo": (0.82, 0.83, 0.85), "anten": (0.88, 0.88, 0.88),
    # tente ve tabelalar
    "tente_kirmizi": (0.72, 0.12, 0.12), "tente_mavi": (0.12, 0.28, 0.60), "tente_yesil": (0.13, 0.45, 0.25),
    "tente_turuncu": (0.88, 0.47, 0.12), "tente_bordo": (0.45, 0.10, 0.15),
    "tabela_beyaz": (0.95, 0.95, 0.95), "tabela_kirmizi": (0.80, 0.10, 0.10), "tabela_mavi": (0.10, 0.35, 0.75),
    "tabela_sari": (0.98, 0.80, 0.10), "tabela_yesil": (0.05, 0.55, 0.35),
}
PALETTE_INDEX = {name: i for i, name in enumerate(PALETTE)}
assert len(PALETTE) <= GRID * GRID

SIVALAR = ["krem", "bej", "somon", "acik_sari", "gri", "beyaz_siva", "acik_yesil", "kiremit_siva"]
TENTELER = ["tente_kirmizi", "tente_mavi", "tente_yesil", "tente_turuncu", "tente_bordo"]
TABELALAR = ["tabela_beyaz", "tabela_kirmizi", "tabela_mavi", "tabela_sari", "tabela_yesil"]


def palette_uv(name):
    i = PALETTE_INDEX[name]
    col, row = i % GRID, i // GRID
    return Vector(((col + 0.5) / GRID, 1.0 - (row + 0.5) / GRID))


def build_palette_image(path):
    size = GRID * CELL
    img = bpy.data.images.new("AnkaraPalet", size, size, alpha=False)
    px = [0.0] * (size * size * 4)
    for name, (r, g, b) in PALETTE.items():
        i = PALETTE_INDEX[name]
        col, row = i % GRID, i // GRID
        y0 = size - (row + 1) * CELL
        for y in range(y0, y0 + CELL):
            for x in range(col * CELL, (col + 1) * CELL):
                o = (y * size + x) * 4
                px[o:o + 4] = (r, g, b, 1.0)
    img.pixels = px
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    return img


def palette_material(img):
    mat = bpy.data.materials.get("M_AnkaraPalet")
    if mat:
        return mat
    mat = bpy.data.materials.new("M_AnkaraPalet")
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = 0.8
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = img
    tex.interpolation = "Closest"
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    return mat


# ---------------------------------------------------------------------------
# Geometri yardımcıları
# ---------------------------------------------------------------------------
class MeshBuilder:
    def __init__(self):
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("UVMap")

    def box(self, x0, x1, y0, y1, z0, z1, color, matrix=None):
        geom = bmesh.ops.create_cube(self.bm, size=1.0)
        verts = geom["verts"]
        sx, sy, sz = x1 - x0, y1 - y0, z1 - z0
        m = Matrix.Translation(((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2)) @ Matrix.Diagonal((sx, sy, sz, 1))
        if matrix is not None:
            m = matrix @ m
        bmesh.ops.transform(self.bm, matrix=m, verts=verts)
        self._paint(verts, color)

    def cylinder(self, cx, cy, z0, z1, radius, color, segments=8, radius_top=None):
        geom = bmesh.ops.create_cone(self.bm, cap_ends=True, segments=segments,
                                     radius1=radius, radius2=radius if radius_top is None else radius_top,
                                     depth=z1 - z0)
        verts = geom["verts"]
        bmesh.ops.translate(self.bm, vec=(cx, cy, (z0 + z1) / 2), verts=verts)
        self._paint(verts, color)
        return verts

    def prism(self, points, z0, z1, color):
        """Yatay bir çokgeni (xy noktaları) z0..z1 arasında yükseltir."""
        bottom = [self.bm.verts.new((x, y, z0)) for x, y in points]
        top = [self.bm.verts.new((x, y, z1)) for x, y in points]
        faces = [self.bm.faces.new(list(reversed(bottom))), self.bm.faces.new(top)]
        n = len(points)
        for i in range(n):
            j = (i + 1) % n
            faces.append(self.bm.faces.new((bottom[i], bottom[j], top[j], top[i])))
        self._paint(bottom + top, color)

    def _paint(self, verts, color):
        uv = palette_uv(color)
        vs = set(verts)
        for f in {f for v in vs for f in v.link_faces}:
            for loop in f.loops:
                loop[self.uv].uv = uv

    def to_object(self, name, material):
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        mesh = bpy.data.meshes.new(name)
        self.bm.to_mesh(mesh)
        self.bm.free()
        mesh.materials.append(material)
        obj = bpy.data.objects.new(name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        return obj


# ---------------------------------------------------------------------------
# Apartman parçaları
# ---------------------------------------------------------------------------
def window(mb, cx, w, z0, h, frame="pvc", trim="silme", sill=True, glass="cam"):
    """Ön cepheye (y=0) pencere: cam, PVC kasa, orta kayıt, denizlik."""
    x0, x1 = cx - w / 2, cx + w / 2
    mb.box(x0, x1, -0.02, 0.0, z0, z0 + h, glass)
    t = 0.07
    mb.box(x0 - t, x0, -0.06, 0.0, z0, z0 + h, frame)
    mb.box(x1, x1 + t, -0.06, 0.0, z0, z0 + h, frame)
    mb.box(x0 - t, x1 + t, -0.06, 0.0, z0 + h, z0 + h + t, frame)
    mb.box(x0 - t, x1 + t, -0.06, 0.0, z0 - t, z0, frame)
    mb.box(cx - 0.03, cx + 0.03, -0.05, 0.0, z0, z0 + h, frame)
    if sill:
        mb.box(x0 - 0.12, x1 + 0.12, -0.16, 0.0, z0 - 0.12, z0 - 0.05, trim)


def ac_unit(mb, cx, z):
    mb.box(cx - 0.42, cx + 0.42, -0.36, -0.04, z, z + 0.55, "klima")
    mb.box(cx - 0.05, cx + 0.32, -0.38, -0.36, z + 0.1, z + 0.45, "klima_izgara")


def balcony(mb, x0, x1, z, depth, wall_color, rng):
    """Uzun balkon: döşeme, yarım dolu parapet + demir korkuluk, arkada cam kapı ve pencere."""
    slab = 0.18
    mb.box(x0, x1, -depth, 0.0, z - slab, z, "beton")
    # parapet: alt yarısı sıvalı panel, üstte demir korkuluk
    ph = 0.55
    mb.box(x0, x1, -depth, -depth + 0.1, z, z + ph, wall_color)
    mb.box(x0, x0 + 0.1, -depth, 0.0, z, z + ph, wall_color)
    mb.box(x1 - 0.1, x1, -depth, 0.0, z, z + ph, wall_color)
    top = z + 1.0
    mb.box(x0, x1, -depth - 0.02, -depth + 0.06, top - 0.05, top, "korkuluk")
    mb.box(x0, x0 + 0.06, -depth, 0.0, top - 0.05, top, "korkuluk")
    mb.box(x1 - 0.06, x1, -depth, 0.0, top - 0.05, top, "korkuluk")
    n = max(2, int((x1 - x0) / 0.5))
    for i in range(n + 1):
        x = x0 + 0.03 + (x1 - x0 - 0.06) * i / n
        mb.box(x - 0.015, x + 0.015, -depth + 0.02, -depth + 0.05, z + ph, top, "korkuluk")
    # arkadaki cephe: balkon kapısı + pencere
    w = x1 - x0
    door_w = 0.9
    mb.box(x0 + 0.4, x0 + 0.4 + door_w, -0.02, 0.0, z, z + 2.25, "cam")
    mb.box(x0 + 0.33, x0 + 0.4, -0.06, 0.0, z, z + 2.3, "pvc")
    mb.box(x0 + 0.4 + door_w, x0 + 0.47 + door_w, -0.06, 0.0, z, z + 2.3, "pvc")
    mb.box(x0 + 0.33, x0 + 0.47 + door_w, -0.06, 0.0, z + 2.25, z + 2.32, "pvc")
    if w > 2.6:
        window(mb, x0 + 0.47 + door_w + (w - 0.87 - door_w) / 2, min(1.6, w - door_w - 1.4), z + 0.9, 1.35, sill=False)
    # balkonda bazen çamaşır askısı / saksı hissi veren küçük kutu
    if rng.random() < 0.35:
        mb.box(x1 - 0.7, x1 - 0.3, -depth + 0.15, -depth + 0.45, z, z + 0.35, "kiremit")


def shop_front(mb, x0, x1, h, rng):
    """Zemin kat dükkânı: vitrin camı, kapı, tabela bandı, tente."""
    awning = rng.choice(TENTELER)
    sign = rng.choice(TABELALAR)
    glass_top = h - 1.0
    mb.box(x0 + 0.2, x1 - 0.2, -0.03, 0.0, 0.35, glass_top, "dukkan_cam")
    mb.box(x0 + 0.2, x1 - 0.2, -0.08, 0.0, 0.0, 0.35, "tas")
    # kapı kasası
    dx = x0 + 0.5
    mb.box(dx, dx + 0.06, -0.08, 0.0, 0.0, glass_top, "korkuluk")
    mb.box(dx + 1.0, dx + 1.06, -0.08, 0.0, 0.0, glass_top, "korkuluk")
    mb.box(x0 + 0.2, x1 - 0.2, -0.08, 0.0, glass_top - 0.06, glass_top, "korkuluk")
    # tabela bandı
    mb.box(x0 + 0.1, x1 - 0.1, -0.22, 0.0, glass_top + 0.1, h - 0.15, sign)
    # tente: öne doğru eğik ince plaka
    if rng.random() < 0.8:
        depth = 1.3
        ang = math.radians(22)
        z = glass_top + 0.05
        m = Matrix.Translation((0, 0, z)) @ Matrix.Rotation(ang, 4, "X") @ Matrix.Translation((0, 0, -z))
        mb.box(x0 + 0.15, x1 - 0.15, -depth, 0.0, z - 0.04, z, awning, matrix=m)
        drop = depth * math.sin(ang)
        mb.box(x0 + 0.15, x1 - 0.15, -depth * math.cos(ang) - 0.04, -depth * math.cos(ang),
               z - drop - 0.3, z - drop, awning)


def roof_flat(mb, w, d, z, color, rng):
    mb.box(-w / 2, w / 2, 0.0, d, z, z + 0.15, "catı_beton")
    p = 0.2
    for x0, x1, y0, y1 in ((-w / 2, w / 2, 0, p), (-w / 2, w / 2, d - p, d), (-w / 2, -w / 2 + p, 0, d), (w / 2 - p, w / 2, 0, d)):
        mb.box(x0, x1, y0, y1, z, z + 0.9, color)
    mb.box(-w / 2 - 0.1, w / 2 + 0.1, -0.25, p + 0.05, z + 0.85, z + 1.05, "silme")
    # asansör / merdiven kulesi
    ex = rng.uniform(-w / 4, w / 4)
    mb.box(ex - 1.4, ex + 1.4, d * 0.45, d * 0.45 + 3.2, z, z + 2.6, color)
    mb.box(ex - 1.5, ex + 1.5, d * 0.45 - 0.1, d * 0.45 + 3.3, z + 2.6, z + 2.75, "silme")
    # su depoları
    for i in range(rng.randint(2, 4)):
        tx = rng.uniform(-w / 2 + 1.2, w / 2 - 1.2)
        ty = rng.uniform(d * 0.15, d * 0.35) if i % 2 else rng.uniform(d * 0.7, d - 1.2)
        mb.cylinder(tx, ty, z + 0.15, z + 1.4, 0.55, "depo", segments=10)
    # çanak antenler
    for _ in range(rng.randint(2, 5)):
        ax = rng.uniform(-w / 2 + 0.6, w / 2 - 0.6)
        ay = rng.uniform(0.6, 2.0)
        mb.box(ax - 0.03, ax + 0.03, ay - 0.03, ay + 0.03, z + 0.15, z + 1.3, "korkuluk")
        verts = mb.cylinder(0, 0, -0.04, 0.04, 0.38, "anten", segments=10)
        rot = Matrix.Translation((ax, ay - 0.05, z + 1.3)) @ Matrix.Rotation(math.radians(70), 4, "X")
        bmesh.ops.transform(mb.bm, matrix=rot, verts=verts)


def roof_hip(mb, w, d, z, rng):
    over = 0.5
    h = min(w, d) * 0.28
    x0, x1, y0, y1 = -w / 2 - over, w / 2 + over, -over, d + over
    ridge = (x1 - x0) / 2 - (y1 - y0) / 2
    bm = mb.bm
    base = [bm.verts.new(v) for v in ((x0, y0, z), (x1, y0, z), (x1, y1, z), (x0, y1, z))]
    cy = (y0 + y1) / 2
    if ridge > 0:
        r = [bm.verts.new((-ridge, cy, z + h)), bm.verts.new((ridge, cy, z + h))]
        faces = [(base[0], base[1], r[1], r[0]), (base[1], base[2], r[1]),
                 (base[2], base[3], r[0], r[1]), (base[3], base[0], r[0])]
    else:
        r = [bm.verts.new((0, cy, z + h))]
        faces = [(base[0], base[1], r[0]), (base[1], base[2], r[0]), (base[2], base[3], r[0]), (base[3], base[0], r[0])]
    for f in faces:
        bm.faces.new(f)
    bm.faces.new(list(reversed(base)))
    mb._paint(base + r, "kiremit")
    # baca
    mb.box(w / 4, w / 4 + 0.6, d * 0.6, d * 0.6 + 0.6, z, z + h + 0.9, "tas")


# ---------------------------------------------------------------------------
# Bina tipleri
# ---------------------------------------------------------------------------
def build_apartment(name, *, bays=4, floors=6, depth=14.0, bay_w=3.6, wall=None, seed=1,
                    shops=True, balcony_rule="seritler", roof="duz", stone_base=False, material=None):
    rng = random.Random(seed)
    wall = wall or rng.choice(SIVALAR)
    w = bays * bay_w
    gh = 4.2 if shops else 3.2
    fh = 3.0
    top = gh + floors * fh
    mb = MeshBuilder()

    # gövde
    mb.box(-w / 2, w / 2, 0.0, depth, 0.0, top, wall)
    if not shops or stone_base:
        mb.box(-w / 2 - 0.05, w / 2 + 0.05, -0.08, 0.0, 0.0, gh if not shops else 0.9, "tas")

    # zemin kat
    for b in range(bays):
        x0 = -w / 2 + b * bay_w
        if shops:
            shop_front(mb, x0, x0 + bay_w, gh, rng)
        elif b == bays // 2:
            # apartman girişi
            cx = x0 + bay_w / 2
            mb.box(cx - 0.75, cx + 0.75, -0.04, 0.0, 0.0, 2.4, "kapi")
            mb.box(cx - 1.2, cx + 1.2, -1.2, 0.0, 2.6, 2.8, "beton")
            mb.box(cx - 1.4, cx + 1.4, -1.6, 0.0, 0.0, 0.3, "tas")
        else:
            window(mb, x0 + bay_w / 2, 1.5, 1.2, 1.4)
    # zemin kat ile 1. kat arası kalın silme
    mb.box(-w / 2 - 0.1, w / 2 + 0.1, -0.3, 0.0, gh - 0.15, gh + 0.15, "silme")

    # balkonlu bölmeleri seç
    if balcony_rule == "seritler":
        # iki bölmelik uzun balkon şeritleri, aralarında pencere bölmeleri
        balcony_bays = {b for b in range(bays) if (b // 2) % 2 == 0} if bays >= 4 else {0}
    elif balcony_rule == "orta":
        balcony_bays = {bays // 2} if bays % 2 else {bays // 2 - 1, bays // 2}
    else:
        balcony_bays = set()

    for f in range(floors):
        z = gh + f * fh
        if f > 0:
            mb.box(-w / 2 - 0.04, w / 2 + 0.04, -0.12, 0.0, z - 0.1, z + 0.12, "silme")
        b = 0
        while b < bays:
            x0 = -w / 2 + b * bay_w
            if b in balcony_bays:
                run = 1
                while b + run < bays and (b + run) in balcony_bays:
                    run += 1
                balcony(mb, x0 + 0.15, x0 + run * bay_w - 0.15, z + 0.05, 1.3, wall, rng)
                b += run
                continue
            if shops and f == 0 and rng.random() < 0.5:
                # Kızılay usulü: 1. kattaki büro/dershane tabelası pencereyi kaplar
                mb.box(x0 + 0.2, x0 + bay_w - 0.2, -0.18, 0.0, z + 0.5, z + 2.6, rng.choice(TABELALAR))
                b += 1
                continue
            r = rng.random()
            window(mb, x0 + bay_w / 2, 1.5, z + 0.9, 1.45,
                   glass="perde" if r < 0.25 else "perde_koyu" if r < 0.35 else "cam")
            if rng.random() < 0.3:
                ac_unit(mb, x0 + bay_w / 2 + rng.choice((-1, 1)) * 0.2, z + 0.15)
            b += 1

    # çatı
    if roof == "kirma":
        mb.box(-w / 2 - 0.15, w / 2 + 0.15, -0.15, depth + 0.15, top - 0.2, top, "silme")
        roof_hip(mb, w, depth, top, rng)
    else:
        roof_flat(mb, w, depth, top, wall, rng)

    # arka cephe: sade pencere sırası (genelde görünmez, ucuz tutuldu)
    for f in range(floors):
        z = gh + f * fh + 0.9
        for b in range(bays):
            cx = -w / 2 + b * bay_w + bay_w / 2
            mb.box(cx - 0.7, cx + 0.7, depth, depth + 0.02, z, z + 1.4, "cam")

    obj = mb.to_object(name, material)
    return obj


VARIANTS = {
    # A1 bulvar apartmanı: zemin katta dükkânlar, uzun balkon şeritleri, düz çatı
    "A1_Bulvar_6Kat_Krem": dict(bays=4, floors=6, wall="krem", seed=11),
    "A1_Bulvar_7Kat_Somon": dict(bays=5, floors=7, wall="somon", seed=23),
    "A1_Bulvar_8Kat_Bej": dict(bays=4, floors=8, wall="bej", seed=37),
    # A3 70'ler Çankaya apartmanı: dükkânsız, taş kaplı zemin, kırma çatı
    "A3_Cankaya_5Kat_SariKirma": dict(bays=3, floors=4, wall="acik_sari", seed=5, shops=False,
                                      balcony_rule="orta", roof="kirma", depth=12.0),
}


# ---------------------------------------------------------------------------
# Dışa aktarma ve önizleme
# ---------------------------------------------------------------------------
def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def export_fbx(obj, path):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_scale_options="FBX_SCALE_ALL",
                             axis_forward="-Z", axis_up="Y", bake_space_transform=True,
                             mesh_smooth_type="FACE", path_mode="STRIP", object_types={"MESH"})


def render_preview(objs, path):
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 48
    scene.cycles.use_denoising = True
    scene.render.resolution_x, scene.render.resolution_y = 1600, 900
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.view_settings.exposure = -0.4

    # binaları yan yana diz
    x = 0.0
    centers = []
    for o in objs:
        w = o.dimensions.x
        o.location.x = x + w / 2
        centers.append(x + w / 2)
        x += w + 0.0
    total = x
    for o in objs:
        o.location.x -= total / 2

    mat = bpy.data.materials.new("Zemin")
    mat.use_nodes = True
    mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.25, 0.25, 0.26, 1)
    bpy.ops.mesh.primitive_plane_add(size=1, location=(0, -15, 0))
    road = bpy.context.object
    road.scale = (total + 60, 30, 1)
    # şerit çizgisi
    lm = bpy.data.materials.new("Serit")
    lm.use_nodes = True
    lm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.9, 0.9, 0.85, 1)
    for i in range(int((total + 60) / 9)):
        bpy.ops.mesh.primitive_plane_add(size=1, location=(-total / 2 - 30 + i * 9, -10.5, 0.01))
        ln = bpy.context.object
        ln.scale = (4, 0.15, 1)
        ln.data.materials.append(lm)
    road.data.materials.append(mat)
    mat2 = bpy.data.materials.new("Kaldirim")
    mat2.use_nodes = True
    mat2.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.55, 0.53, 0.5, 1)
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, -2.5, 0.075))
    walk = bpy.context.object
    walk.scale = (total + 60, 5, 0.15)
    walk.data.materials.append(mat2)

    world = bpy.data.worlds.new("Gok")
    world.use_nodes = True
    sky = world.node_tree.nodes.new("ShaderNodeTexSky")
    try:
        sky.sky_type = "NISHITA"
        sky.sun_elevation = math.radians(38)
        sky.sun_rotation = math.radians(210)
    except (AttributeError, TypeError):
        pass
    bg = world.node_tree.nodes["Background"]
    world.node_tree.links.new(sky.outputs["Color"], bg.inputs["Color"])
    bg.inputs["Strength"].default_value = 0.18
    scene.world = world

    sun = bpy.data.lights.new("Gunes", "SUN")
    sun.energy = 3.2
    sun.angle = math.radians(1.5)
    so = bpy.data.objects.new("Gunes", sun)
    so.rotation_euler = (math.radians(52), 0, math.radians(-50))
    scene.collection.objects.link(so)

    cam = bpy.data.cameras.new("Kamera")
    cam.lens = 24
    co = bpy.data.objects.new("Kamera", cam)
    scene.collection.objects.link(co)
    co.location = (-total * 0.42, -14.0, 2.8)
    target = Vector((total * 0.08, 2, 12))
    co.rotation_euler = (target - co.location).to_track_quat("-Z", "Y").to_euler()
    scene.camera = co
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True)
    ap.add_argument("--render", default=None, help="Önizleme PNG yolu")
    ap.add_argument("--only", nargs="*", default=None)
    args = ap.parse_args(argv)
    os.makedirs(args.out, exist_ok=True)

    clear_scene()
    img = build_palette_image(os.path.join(args.out, "T_AnkaraPalet.png"))
    mat = palette_material(img)
    objs = []
    for name, params in VARIANTS.items():
        if args.only and name not in args.only:
            continue
        obj = build_apartment(name, material=mat, **params)
        export_fbx(obj, os.path.join(args.out, name + ".fbx"))
        tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)
        print(f"{name}: {obj.dimensions.x:.1f} x {obj.dimensions.y:.1f} x {obj.dimensions.z:.1f} m, {tris} üçgen")
        objs.append(obj)
    if args.render:
        render_preview(objs, args.render)


if __name__ == "__main__":
    main()
