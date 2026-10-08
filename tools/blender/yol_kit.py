"""
Ankara yol kiti üreticisi (bpy). Bulvar ve Cinnah parçaları, eğimler, virajlar, durak cepleri,
T kavşak ve Atakule dönüş halkası.

Kullanım:
    python yol_kit.py --out <klasör> [--render onizleme.png]

Parça koordinatları (Unity'de, FBX içe aktarma sonrası):
    - Başlangıç noktası (0,0,0), yolun ortası.
    - Yol +Z yönünde ilerler, sağ taraf +X. Türkiye'de sağdan trafik.
    - Parçanın bitiş konumu ve açısı dosya adında ve docs/YOL_KITI.md'de yazılıdır.
Blender'da bu, yolun -Y yönünde ilerlemesi ve sağın -X olması demektir.
"""
import argparse
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402

import apartman_kit as kit  # noqa: E402

# ---------------------------------------------------------------------------
# Kesitler: (sağa doğru) şeritler. Her şerit: (r0, r1, yükseklik, renk)
# r ekseni yolun ortasından sağa (+) ve sola (-) metre.
# ---------------------------------------------------------------------------
CURB = 0.15
LANE_BULVAR = 3.5
LANE_CINNAH = 3.25

# Atatürk Bulvarı: 2x3 şerit, ortada 3 m ağaçlı/çimli refüj, 4.5 m kaldırımlar
BULVAR = dict(
    half_road=1.5 + 3 * LANE_BULVAR,  # refüj yarısı + 3 şerit
    median=1.5,
    sidewalk=4.5,
    lanes=3,
    lane=LANE_BULVAR,
)
# Cinnah Caddesi: 2x2 şerit, refüj yok, 3 m kaldırımlar
CINNAH = dict(
    half_road=2 * LANE_CINNAH,
    median=0.0,
    sidewalk=3.0,
    lanes=2,
    lane=LANE_CINNAH,
)


class RoadPath:
    """Sabit eğrilikli ve sabit eğimli yol ekseni. turn_deg > 0 sağa dönüş."""

    def __init__(self, length, slope=0.0, turn_deg=0.0, start=(0.0, 0.0, 0.0), heading=0.0):
        self.length = length
        self.slope = slope
        self.kappa = math.radians(turn_deg) / length if length else 0.0
        self.start = start
        self.heading = heading

    def frame(self, s):
        """s mesafesindeki eksen noktası (f, r, z) ve yön açısı."""
        f0, r0, z0 = self.start
        th0 = self.heading
        if abs(self.kappa) < 1e-9:
            f = f0 + s * math.cos(th0)
            r = r0 + s * math.sin(th0)
        else:
            R = 1.0 / self.kappa
            f = f0 + R * (math.sin(th0 + self.kappa * s) - math.sin(th0))
            r = r0 - R * (math.cos(th0 + self.kappa * s) - math.cos(th0))
        return f, r, z0 + self.slope * s, th0 + self.kappa * s

    def point(self, s, offset, dz=0.0):
        f, r, z, th = self.frame(s)
        # sağ vektör: (-sin th, cos th) (f, r düzleminde)
        pf = f - offset * math.sin(th)
        pr = r + offset * math.cos(th)
        # Unity çerçevesi (ileri f, sağ r) -> Blender (x = -r, y = -f)
        return Vector((-pr, -pf, z + dz))

    def samples(self, step=2.0):
        n = max(1, math.ceil(self.length / step))
        if abs(self.kappa) < 1e-9 and self.slope == 0.0:
            n = max(1, math.ceil(self.length / 10.0))
        return [self.length * i / n for i in range(n + 1)]


class RoadBuilder(kit.MeshBuilder):
    def quad(self, a, b, c, d, color, up_hint=None):
        verts = [self.bm.verts.new(v) for v in (a, b, c, d)]
        face = self.bm.faces.new(verts)
        face.normal_update()
        if up_hint is not None and face.normal.dot(up_hint) < 0:
            face.normal_flip()
        uv = kit.palette_uv(color)
        for loop in face.loops:
            loop[self.uv].uv = uv
        return face

    def strip(self, path, r0, r1, color, dz=0.0, step=2.0, active=None):
        """Yol boyunca r0..r1 arası yatay şerit. r0/r1/dz sabit ya da s'nin fonksiyonu olabilir.
        active(s) False döndüğü yerlerde şerit çizilmez."""
        fn = lambda v: v if callable(v) else (lambda s, v=v: v)
        r0, r1, dz = fn(r0), fn(r1), fn(dz)
        ss = path.samples(step)
        for s0, s1 in zip(ss, ss[1:]):
            sm = (s0 + s1) / 2
            if active is not None and not active(sm):
                continue
            a = path.point(s0, r0(s0), dz(s0))
            b = path.point(s0, r1(s0), dz(s0))
            c = path.point(s1, r1(s1), dz(s1))
            d = path.point(s1, r0(s1), dz(s1))
            self.quad(a, b, c, d, color, up_hint=Vector((0, 0, 1)))

    def wall(self, path, r, z_top, z_bot, color, outward, step=2.0, active=None):
        """Yol boyunca r ofsetinde dikey yüz (bordür kenarı). outward: +1 sağa, -1 sola bakar."""
        fn = lambda v: v if callable(v) else (lambda s, v=v: v)
        r, z_top, z_bot = fn(r), fn(z_top), fn(z_bot)
        ss = path.samples(step)
        for s0, s1 in zip(ss, ss[1:]):
            sm = (s0 + s1) / 2
            if active is not None and not active(sm):
                continue
            a = path.point(s0, r(s0), z_bot(s0))
            b = path.point(s1, r(s1), z_bot(s1))
            c = path.point(s1, r(s1), z_top(s1))
            d = path.point(s0, r(s0), z_top(s0))
            f, rr, z, th = path.frame(sm)
            # dışa doğru vektör (Blender)
            out = Vector((-math.cos(th), math.sin(th), 0)) * outward
            self.quad(a, b, c, d, color, up_hint=out)

    def dashes(self, path, r, width=0.15, dash=3.0, gap=5.0, color="serit_beyaz", active=None):
        s = 1.0
        while s + dash <= path.length:
            a = path.point(s, r - width / 2, 0.012)
            b = path.point(s, r + width / 2, 0.012)
            c = path.point(s + dash, r + width / 2, 0.012)
            d = path.point(s + dash, r - width / 2, 0.012)
            if active is None or active(s + dash / 2):
                self.quad(a, b, c, d, color, up_hint=Vector((0, 0, 1)))
            s += dash + gap

    def solid_line(self, path, r, width=0.15, color="serit_beyaz", active=None):
        self.strip(path, r - width / 2, r + width / 2, color, dz=0.012, active=active)


# ---------------------------------------------------------------------------
# Yol yüzeyi ve kenarlar
# ---------------------------------------------------------------------------
def road_surface(rb, path, prof, *, right_sidewalk=None, left_sidewalk=None, bay=None):
    """Kesit çizer. right_sidewalk/left_sidewalk(s) False ise o s'de kaldırım yok (kavşak ağzı).
    bay: (s0, s1, derinlik) sağ kaldırımda otobüs cebi."""
    H = prof["half_road"]
    W = prof["sidewalk"]
    rs = right_sidewalk or (lambda s: True)
    ls = left_sidewalk or (lambda s: True)

    def right_edge(s):
        if not bay:
            return H
        s0, s1, depth = bay
        taper = 8.0
        if s < s0 or s > s1:
            return H
        if s < s0 + taper:
            return H + depth * (s - s0) / taper
        if s > s1 - taper:
            return H + depth * (s1 - s) / taper
        return H + depth

    # asfalt (sağ ve sol, kaldırım boşluklarında kaldırım genişliği kadar uzar)
    rb.strip(path, -H, lambda s: right_edge(s), "asfalt", step=1.0 if bay else 2.0)
    rb.strip(path, lambda s: right_edge(s), H + W, "asfalt", active=lambda s: not rs(s))
    rb.strip(path, -H - W, -H, "asfalt", active=lambda s: not ls(s))

    # sağ kaldırım
    step = 1.0 if bay else 2.0
    rb.strip(path, right_edge, H + W, "kaldirim", dz=CURB, active=rs, step=step)
    rb.strip(path, right_edge, lambda s: right_edge(s) + 0.25, "bordur", dz=CURB + 0.003, active=rs, step=step)
    rb.wall(path, right_edge, CURB, 0.0, "bordur", -1, active=rs, step=step)
    rb.wall(path, H + W, CURB, -0.6, "kaldirim", +1, active=rs)
    # sol kaldırım
    rb.strip(path, -H - W, -H, "kaldirim", dz=CURB, active=ls)
    rb.strip(path, -H - 0.25, -H, "bordur", dz=CURB + 0.003, active=ls)
    rb.wall(path, -H, CURB, 0.0, "bordur", +1, active=ls)
    rb.wall(path, -H - W, CURB, -0.6, "kaldirim", -1, active=ls)
    # asfalt dış kenarı aşağı etek (boşluk görünmesin)
    rb.wall(path, H + W, 0.0, -0.6, "asfalt", +1, active=lambda s: not rs(s))
    rb.wall(path, -H - W, 0.0, -0.6, "asfalt", -1, active=lambda s: not ls(s))


def median(rb, path, prof, gaps=()):
    m = prof["median"]
    if m <= 0:
        return
    active = lambda s: not any(a <= s <= b for a, b in gaps)
    rb.strip(path, -m, m, "cim", dz=CURB + 0.05, active=active)
    rb.strip(path, -m, -m + 0.25, "bordur", dz=CURB + 0.06, active=active)
    rb.strip(path, m - 0.25, m, "bordur", dz=CURB + 0.06, active=active)
    rb.wall(path, -m, CURB + 0.05, 0.0, "bordur", -1, active=active)
    rb.wall(path, m, CURB + 0.05, 0.0, "bordur", +1, active=active)


def markings(rb, path, prof, active=None, edge_active=None):
    H, m, n, lane = prof["half_road"], prof["median"], prof["lanes"], prof["lane"]
    inner = m if m > 0 else 0.0
    for side in (1, -1):
        for k in range(1, n):
            rb.dashes(path, side * (inner + k * lane), active=active)
        # şerit kenar çizgisi (kaldırım tarafı) ve iç kenar
        rb.solid_line(path, side * (H - 0.35), active=edge_active)
        if m > 0:
            rb.solid_line(path, side * (m + 0.35), active=active)
    if m <= 0:
        # refüjsüz yolda ortada çift düz çizgi
        rb.solid_line(path, -0.12, active=active)
        rb.solid_line(path, 0.12, active=active)


def zebra(rb, path, s, prof, length=4.0):
    """s mesafesinde yolu enine kesen yaya geçidi."""
    H = prof["half_road"]
    r = -H + 0.5
    while r < H - 0.5:
        a = path.point(s, r, 0.013)
        b = path.point(s, r + 0.5, 0.013)
        c = path.point(s + length, r + 0.5, 0.013)
        d = path.point(s + length, r, 0.013)
        rb.quad(a, b, c, d, "serit_beyaz", up_hint=Vector((0, 0, 1)))
        r += 1.0


# ---------------------------------------------------------------------------
# Parçalar
# ---------------------------------------------------------------------------
def piece_straight(prof, length, slope=0.0, turn=0.0):
    rb = RoadBuilder()
    path = RoadPath(length, slope, turn)
    road_surface(rb, path, prof)
    median(rb, path, prof)
    markings(rb, path, prof)
    return rb, path


def piece_bus_bay(prof, length=40.0, slope=0.0, depth=3.0):
    rb = RoadBuilder()
    path = RoadPath(length, slope)
    bay = (4.0, length - 4.0, depth)
    road_surface(rb, path, prof, bay=bay)
    median(rb, path, prof)
    markings(rb, path, prof, edge_active=lambda s: not (bay[0] <= s <= bay[1]))
    # cep içi "OTOBÜS" alanı sarı kenar çizgisi
    H = prof["half_road"]
    rb.strip(path, H + depth - 0.4, H + depth - 0.25, "serit_sari", dz=0.012,
             active=lambda s: bay[0] + 8 <= s <= bay[1] - 8)
    return rb, path


def piece_t_junction(main, side, length=40.0):
    """Bulvar düz gider, sağa yan yol (Cinnah) ayrılır. Yan yolun ağzı parçanın ortasında."""
    rb = RoadBuilder()
    path = RoadPath(length)
    mouth = side["half_road"] + 2.0
    mid = length / 2
    in_mouth = lambda s: abs(s - mid) < mouth
    road_surface(rb, path, main, right_sidewalk=lambda s: not in_mouth(s))
    median(rb, path, main)
    markings(rb, path, main, edge_active=lambda s: not in_mouth(s))
    zebra(rb, path, mid - mouth - 5.0, main)
    zebra(rb, path, mid + mouth + 1.0, main)
    # yan yol: ağızdan sağa doğru 12 m
    H = main["half_road"] + main["sidewalk"]
    stub = RoadPath(12.0, 0.0, 0.0, start=(mid, H, 0.0), heading=math.radians(90))
    road_surface(rb, stub, side)
    markings(rb, stub, side)
    return rb, path, stub


def piece_roundabout(prof, island=12.0):
    """Atakule dönüş halkası. Girişi parçanın başında (0,0,0), halka +Z yönünde.
    Ortadaki ada Atakule için boş bırakılır (çim + bordür)."""
    rb = RoadBuilder()
    ring_w = 2 * prof["lane"] + 1.0
    Rc = island + ring_w / 2          # halkanın eksen yarıçapı
    entry = 14.0
    # giriş yolu
    ent = RoadPath(entry)
    road_surface(rb, ent, prof)
    markings(rb, ent, prof)
    # halka: giriş bitişinden sağa dönerek tam tur (merkez, girişin ilerisinde)
    ring = RoadPath(2 * math.pi * Rc, 0.0, 360.0, start=(entry, 0.0, 0.0), heading=math.radians(-90))
    # heading -90: sola bakarak başlar, sağa (saat yönü) dönerek... Türkiye'de halka saat yönünün tersine
    # dönülür; geometri simetrik olduğu için yüzey aynıdır, yalnızca okları Unity tarafında koyarız.
    half = ring_w / 2
    W = prof["sidewalk"]
    gap = lambda s: s < prof["half_road"] + 1.0 or s > ring.length - prof["half_road"] - 1.0
    rb.strip(ring, -half, half, "asfalt", step=1.5)
    # sağa dönen halkada sağ taraf (+) adaya, sol taraf (-) dış kaldırıma bakar
    out = lambda s: not gap(s)
    rb.strip(ring, -half - W, -half, "kaldirim", dz=CURB, step=1.5, active=out)
    rb.wall(ring, -half, CURB, 0.0, "bordur", +1, step=1.5, active=out)
    rb.wall(ring, -half - W, CURB, -0.6, "kaldirim", -1, step=1.5, active=out)
    rb.strip(ring, -half - W, -half, "asfalt", step=1.5, active=gap)
    # ada (Atakule buraya oturur)
    rb.strip(ring, half, half + 0.4, "bordur", dz=CURB + 0.05, step=1.5)
    rb.wall(ring, half, CURB + 0.05, 0.0, "bordur", -1, step=1.5)
    rb.strip(ring, half + 0.4, half + island - 0.01, "cim", dz=CURB + 0.05, step=1.5)
    rb.dashes(ring, 0.0)
    return rb, ent


PIECES = {
    # ad: (üretici, açıklama)
    "Bulvar_Duz_20m": lambda: piece_straight(BULVAR, 20.0),
    "Bulvar_Egim2_20m": lambda: piece_straight(BULVAR, 20.0, 0.02),
    "Bulvar_Egim4_20m": lambda: piece_straight(BULVAR, 20.0, 0.04),
    "Bulvar_DurakCebi_40m": lambda: piece_bus_bay(BULVAR, 40.0),
    "Bulvar_DurakCebi_Egim4_40m": lambda: piece_bus_bay(BULVAR, 40.0, 0.04),
    "Kavsak_Bulvar_Cinnah_T": lambda: piece_t_junction(BULVAR, CINNAH)[:2],
    "Cinnah_Duz_20m": lambda: piece_straight(CINNAH, 20.0),
    "Cinnah_Egim8_20m": lambda: piece_straight(CINNAH, 20.0, 0.08),
    "Cinnah_Egim10_20m": lambda: piece_straight(CINNAH, 20.0, 0.10),
    "Cinnah_Egim12_20m": lambda: piece_straight(CINNAH, 20.0, 0.12),
    "Cinnah_Viraj_Sag15_Egim10": lambda: piece_straight(CINNAH, 20.0, 0.10, 15.0),
    "Cinnah_Viraj_Sol15_Egim10": lambda: piece_straight(CINNAH, 20.0, 0.10, -15.0),
    "Cinnah_Viraj_Sag30_Egim8": lambda: piece_straight(CINNAH, 30.0, 0.08, 30.0),
    "Cinnah_Viraj_Sol30_Egim8": lambda: piece_straight(CINNAH, 30.0, 0.08, -30.0),
    "Cinnah_DurakCebi_Egim8_30m": lambda: piece_bus_bay(CINNAH, 30.0, 0.08, 3.0),
    "Atakule_DonusHalkasi": lambda: piece_roundabout(CINNAH),
    # Hat 2 (Kızılay → Ulus): inişler ve bulvar genişliğinde dönüş halkası
    "Bulvar_Inis2_20m": lambda: piece_straight(BULVAR, 20.0, -0.02),
    "Bulvar_Inis4_20m": lambda: piece_straight(BULVAR, 20.0, -0.04),
    "Ulus_DonusHalkasi": lambda: piece_roundabout(BULVAR, island=14.0),
}


def piece_end(path):
    f, r, z, th = path.frame(path.length)
    return f, r, z, math.degrees(th)


def build_all(out, material):
    objs = {}
    lines = []
    for name, make in PIECES.items():
        rb, path = make()
        obj = rb.to_object(name, material, recalc_normals=False)
        kit.export_fbx(obj, os.path.join(out, name + ".fbx"))
        f, r, z, th = piece_end(path)
        tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)
        line = f"| `{name}` | ({r:+.2f}, {z:+.2f}, {f:+.2f}) | {th:+.1f}° | {tris} |"
        lines.append(line)
        print(line)
        objs[name] = (obj, path)
    return objs, lines


# ---------------------------------------------------------------------------
# Önizleme: parçaları uç uca ekleyerek küçük bir güzergâh kurar
# ---------------------------------------------------------------------------
def place_chain(objs, chain):
    """Unity çerçevesinde uç uca ekler: her parçanın başlangıcını bir öncekinin bitişine koyar."""
    f, r, z, th = 0.0, 0.0, 0.0, 0.0
    placed = []
    for name in chain:
        src, path = objs[name]
        obj = src.copy()
        bpy.context.scene.collection.objects.link(obj)
        # Unity (f, r) -> Blender (x=-r, y=-f); yön açısı sağa dönüş pozitif -> Blender Z ekseninde pozitif
        obj.location = (-r, -f, z)
        obj.rotation_euler = (0, 0, -th)
        placed.append(obj)
        ef, er, ez, eth = path.frame(path.length)
        # parçanın yerel bitişini dünya çerçevesine çevir
        c, s_ = math.cos(th), math.sin(th)
        f, r = f + ef * c - er * s_, r + ef * s_ + er * c
        z += ez
        th += eth
    return placed


def render_chain(objs, path_png):
    for o, _ in objs.values():
        o.hide_render = True
    chain = (["Bulvar_Duz_20m", "Bulvar_DurakCebi_40m", "Bulvar_Egim2_20m", "Kavsak_Bulvar_Cinnah_T",
              "Bulvar_Egim4_20m", "Bulvar_Egim4_20m"])
    placed = place_chain(objs, chain)
    # Cinnah kolu: kavşak ağzından başlar
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 32
    scene.cycles.use_denoising = True
    scene.render.resolution_x, scene.render.resolution_y = 1600, 900
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.exposure = -0.3
    world = bpy.data.worlds.new("Gok")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.55, 0.7, 0.9, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.6
    scene.world = world
    sun = bpy.data.lights.new("Gunes", "SUN")
    sun.energy = 3.5
    so = bpy.data.objects.new("Gunes", sun)
    so.rotation_euler = (math.radians(50), 0, math.radians(-40))
    scene.collection.objects.link(so)

    # Cinnah kolunu kavşak parçasının yan yol ucundan başlat
    _, _, stub = piece_t_junction(BULVAR, CINNAH)
    jf = 20.0 + 40.0 + 20.0  # kavşağın başlangıcı (ilk üç parçanın uzunlukları)
    jz = 0.0 + 0.0 + 0.4            # Bulvar_Egim2_20m yükselişi
    ef, er, ez, eth = stub.frame(stub.length)
    f, r, z, th = jf + ef, er, jz + ez, eth
    for name in ["Cinnah_Duz_20m", "Cinnah_Egim8_20m", "Cinnah_DurakCebi_Egim8_30m", "Cinnah_Viraj_Sol30_Egim8",
                 "Cinnah_Egim10_20m", "Cinnah_Viraj_Sag15_Egim10", "Cinnah_Egim12_20m", "Atakule_DonusHalkasi"]:
        src, path = objs[name]
        obj = src.copy()
        scene.collection.objects.link(obj)
        obj.location = (-r, -f, z)
        obj.rotation_euler = (0, 0, -th)
        obj.hide_render = False
        lf, lr, lz, lth = path.frame(path.length)
        c, s_ = math.cos(th), math.sin(th)
        f, r = f + lf * c - lr * s_, r + lf * s_ + lr * c
        z += lz
        th += lth
    for o in placed:
        o.hide_render = False

    # zemin
    mat = bpy.data.materials.new("Zemin")
    mat.use_nodes = True
    mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.42, 0.40, 0.33, 1)
    bpy.ops.mesh.primitive_plane_add(size=600, location=(-150, -120, -0.7))
    bpy.context.object.data.materials.append(mat)

    cam = bpy.data.cameras.new("Kamera")
    cam.lens = 24
    co = bpy.data.objects.new("Kamera", cam)
    scene.collection.objects.link(co)
    co.location = (90, 30, 120)
    target = Vector((-60, -120, 0))
    co.rotation_euler = (target - co.location).to_track_quat("-Z", "Y").to_euler()
    scene.camera = co
    scene.render.filepath = path_png
    bpy.ops.render.render(write_still=True)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True)
    ap.add_argument("--render", default=None)
    args = ap.parse_args(argv)
    os.makedirs(args.out, exist_ok=True)
    kit.clear_scene()
    img = kit.build_palette_image(os.path.join(args.out, "T_AnkaraPalet.png"))
    mat = kit.palette_material(img)
    objs, lines = build_all(args.out, mat)
    with open(os.path.join(args.out, "parcalar.md"), "w") as fh:
        fh.write("| Parça | Bitiş konumu (x, y, z) | Bitiş yönü (Y ekseni) | Üçgen |\n|---|---|---|---|\n")
        fh.write("\n".join(lines) + "\n")
    if args.render:
        render_chain(objs, args.render)


if __name__ == "__main__":
    main()
