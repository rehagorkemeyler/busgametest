"""
Simge yapılar ve sokak objeleri (bpy): Atakule, Kızılay AVM, TBMM duvarı/kapısı, Kuğulu Park göleti,
EGO otobüs durağı, ağaçlar, sokak lambası.

Kullanım:
    python yapilar_kit.py --out <klasör> [--render onizleme.png]

Pivotlar zemin seviyesinde. Ön yüzü olan objelerde ön yüz (yol tarafı) Unity'de +Z'ye bakar
(Blender'da -Y), bina kitiyle aynı kural.
"""
import argparse
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
import bmesh  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402

import apartman_kit as kit  # noqa: E402


class PropBuilder(kit.MeshBuilder):
    def sphere(self, cx, cy, cz, rx, ry, rz, color, subdiv=1):
        geom = bmesh.ops.create_icosphere(self.bm, subdivisions=subdiv, radius=1.0)
        verts = geom["verts"]
        m = Matrix.Translation((cx, cy, cz)) @ Matrix.Diagonal((rx, ry, rz, 1))
        bmesh.ops.transform(self.bm, matrix=self.xform @ m, verts=verts)
        self._paint(verts, color)
        return verts

    def ring_band(self, r0, r1, z0, z1, color, segments=24):
        """İçi boş olmayan, alt yarıçapı r0 üst yarıçapı r1 olan kesik koni (yan yüzler + kapaklar)."""
        return self.cylinder(0, 0, z0, z1, r0, color, segments=segments, radius_top=r1)


# ---------------------------------------------------------------------------
# Atakule
# ---------------------------------------------------------------------------
def build_atakule(name, material):
    """Atakule: 125 m. Yivli beton gövde, kadeh biçimli başlık, camlı restoran bandı, kubbe ve anten.
    Dönüş halkası adasına (yarıçap 12 m) sığacak şekilde kaide yarıçapı 10 m."""
    pb = PropBuilder()
    seg = 24
    # kaide: alçak dairesel podyum ve giriş saçağı
    pb.cylinder(0, 0, 0.0, 1.2, 10.0, "granit", segments=seg)
    pb.cylinder(0, 0, 1.2, 6.0, 8.5, "dukkan_cam", segments=seg)
    pb.cylinder(0, 0, 6.0, 7.0, 9.2, "kule_beton", segments=seg)
    # gövde: hafif daralan silindir + 12 dikey yiv
    pb.cylinder(0, 0, 7.0, 92.0, 5.6, "kule_beton", segments=seg, radius_top=4.8)
    for i in range(12):
        a = 2 * math.pi * i / 12
        m = Matrix.Rotation(a, 4, "Z")
        verts_before = set(pb.bm.verts)
        pb.box(5.0, 6.2, -0.35, 0.35, 7.0, 90.0, "kule_beton")
        new = [v for v in pb.bm.verts if v not in verts_before]
        # yivi içe doğru eğ (gövde daraldığı için üstte içe yaklaşır)
        for v in new:
            t = (v.co.z - 7.0) / 83.0
            v.co.x -= 0.8 * t
        bmesh.ops.transform(pb.bm, matrix=m, verts=new)
    # asansör camı: gövdenin ön yüzünde dikey koyu şerit
    pb.box(-1.2, 1.2, -6.2, -5.0, 8.0, 90.0, "cam")
    # başlık: kadeh biçimi alt koni
    pb.ring_band(4.8, 13.5, 90.0, 99.0, "kule_beton", segments=seg)
    # cam restoran bandı (iki kat)
    pb.cylinder(0, 0, 99.0, 99.6, 14.0, "silme", segments=seg)
    pb.cylinder(0, 0, 99.6, 105.0, 13.6, "cam_ofis", segments=seg)
    pb.cylinder(0, 0, 105.0, 105.6, 14.2, "silme", segments=seg)
    for i in range(24):
        a = 2 * math.pi * i / 24
        m = Matrix.Rotation(a, 4, "Z")
        vb = set(pb.bm.verts)
        pb.box(13.5, 13.75, -0.12, 0.12, 99.6, 105.0, "alu")
        bmesh.ops.transform(pb.bm, matrix=m, verts=[v for v in pb.bm.verts if v not in vb])
    # üst kubbe (yarım küre) ve küçük gözlem katı
    pb.ring_band(14.0, 11.5, 105.6, 109.5, "kule_beton", segments=seg)
    pb.ring_band(11.5, 6.0, 109.5, 113.0, "kule_beton", segments=seg)
    pb.cylinder(0, 0, 113.0, 115.0, 4.0, "cam_ofis", segments=16)
    pb.ring_band(4.4, 1.0, 115.0, 117.0, "kule_beton", segments=16)
    # anten
    pb.cylinder(0, 0, 117.0, 125.0, 0.35, "metal", segments=8, radius_top=0.12)
    pb.cylinder(0, 0, 124.6, 125.2, 0.25, "kirmizi_bayrak", segments=8)
    return pb.to_object(name, material)


# ---------------------------------------------------------------------------
# Kızılay AVM
# ---------------------------------------------------------------------------
def build_kizilay_avm(name, material):
    """Kızılay AVM: bej taş kaplı büyük kutu, köşede geri çekilmiş cam atrium ve koyu silindir,
    üst kısımda dar dikey pencere sırası, zeminde vitrinler. Köşe sol önde (bulvar köşesi)."""
    pb = PropBuilder()
    W, D, H = 48.0, 36.0, 34.0
    c = 9.0  # köşe kesiği
    fp = [(-W / 2 + c, 0.0), (W / 2, 0.0), (W / 2, D), (-W / 2, D), (-W / 2, c)]
    pb.prism(fp, 0.0, H, "tas")
    pb.prism(fp, H, H + 0.3, "beton")
    # köşede cam atrium: kesik yüzün önüne yerleştirilmiş, hafif çıkıntılı cam kutu
    pb.xform = Matrix.Translation((-W / 2 + c / 2, c / 2, 0)) @ Matrix.Rotation(math.radians(-45), 4, "Z")
    cw = c * math.sqrt(2)
    pb.box(-cw / 2 + 1.0, cw / 2 - 1.0, -1.2, 0.0, 6.0, H - 4.0, "cam_ofis")
    for i in range(1, 6):
        x = -cw / 2 + 1.0 + (cw - 2.0) * i / 6
        pb.box(x - 0.06, x + 0.06, -1.3, -1.1, 6.0, H - 4.0, "alu")
    for z in range(8, int(H - 4), 4):
        pb.box(-cw / 2 + 1.0, cw / 2 - 1.0, -1.3, -1.1, z - 0.1, z + 0.1, "alu")
    pb.xform = Matrix.Identity(4)
    # koyu silindir (köşenin yanında, ön cephede)
    pb.cylinder(-W / 2 + c + 5.0, -2.0, 6.0, H - 6.0, 3.6, "granit", segments=16)
    # ön cephe: geniş cam şerit + logo paneli
    pb.box(4.0, 12.0, -0.4, 0.0, 6.0, H - 3.0, "cam_ofis")
    pb.box(13.0, 21.0, -0.3, 0.0, 14.0, 22.0, "beton")
    pb.box(15.0, 19.0, -0.4, -0.3, 18.0, 21.0, "tente_turuncu")
    pb.box(14.5, 19.5, -0.4, -0.3, 15.5, 16.8, "tabela_mavi")
    # üst bantta dar dikey pencereler (ön ve yan)
    for x in [x * 2.6 for x in range(-3, 9)]:
        if -W / 2 + c + 1 < x < W / 2 - 1 and not (3.0 < x < 13.0):
            pb.box(x - 0.3, x + 0.3, -0.05, 0.0, H - 9.0, H - 3.0, "cam")
    pb.xform = Matrix.Translation((-W / 2, c + (D - c) / 2, 0)) @ Matrix.Rotation(math.radians(-90), 4, "Z")
    side = D - c
    for i in range(1, 9):
        x = -side / 2 + side * i / 9
        pb.box(x - 0.3, x + 0.3, -0.05, 0.0, H - 9.0, H - 3.0, "cam")
    pb.box(-side / 2 + 2, side / 2 - 2, -0.3, 0.0, 0.4, 5.5, "dukkan_cam")
    pb.box(-side / 2 + 2, side / 2 - 2, -1.8, 0.0, 5.5, 6.0, "beton")
    pb.xform = Matrix.Identity(4)
    # zemin kat vitrinleri ve saçak (ön)
    pb.box(-W / 2 + c + 1, W / 2 - 1, -0.3, 0.0, 0.4, 5.5, "dukkan_cam")
    pb.box(-W / 2 + c, W / 2, -2.5, 0.0, 5.5, 6.0, "beton")
    # mağaza tabelaları (zemin üstü)
    rng = random.Random(7)
    x = -W / 2 + c + 8
    while x < W / 2 - 4:
        pb.box(x, x + 3.5, -2.6, -2.5, 5.6, 6.4, rng.choice(kit.TABELALAR))
        x += 5.0
    # çatıda bayrak direği
    pb.cylinder(-W / 2 + c + 2, 3.0, H, H + 8.0, 0.1, "metal", segments=6)
    pb.box(-W / 2 + c + 2.1, -W / 2 + c + 5.1, 2.95, 3.05, H + 6.0, H + 8.0, "kirmizi_bayrak")
    return pb.to_object(name, material)


# ---------------------------------------------------------------------------
# TBMM duvarı ve kapısı
# ---------------------------------------------------------------------------
def build_tbmm_wall(name, material, length=20.0):
    """Taş kaideli, demir parmaklıklı TBMM çevre duvarı. Uç uca eklenebilir; +Z boyunca değil,
    X ekseni boyunca uzanır (yola paralel koyun), ön yüz yola (+Z) bakar."""
    pb = PropBuilder()
    pb.box(-length / 2, length / 2, 0.0, 0.8, 0.0, 1.4, "tas")
    pb.box(-length / 2, length / 2, -0.05, 0.85, 1.4, 1.55, "kule_beton")
    n = int(length / 0.25)
    for i in range(n):
        x = -length / 2 + 0.125 + i * 0.25
        pb.box(x - 0.025, x + 0.025, 0.35, 0.45, 1.55, 3.1, "korkuluk")
    pb.box(-length / 2, length / 2, 0.33, 0.47, 2.95, 3.05, "korkuluk")
    for x in (-length / 2, 0.0, length / 2):
        pb.box(x - 0.45, x + 0.45, -0.05, 0.85, 0.0, 3.4, "tas")
        pb.box(x - 0.55, x + 0.55, -0.15, 0.95, 3.4, 3.6, "kule_beton")
    return pb.to_object(name, material)


def build_tbmm_gate(name, material):
    """TBMM ana kapısı: iki büyük taş ayak, demir kapı kanatları, iki yanında nöbet kulübesi."""
    pb = PropBuilder()
    gw = 12.0
    for sx in (-1, 1):
        x = sx * (gw / 2 + 1.2)
        pb.box(x - 1.2, x + 1.2, -0.6, 1.4, 0.0, 6.5, "tas")
        pb.box(x - 1.4, x + 1.4, -0.8, 1.6, 6.5, 7.0, "kule_beton")
        pb.box(x - 0.8, x + 0.8, -0.4, 1.2, 7.0, 7.8, "tas")
        # kapı kanadı
        x0, x1 = (0.0, gw / 2) if sx > 0 else (-gw / 2, 0.0)
        pb.box(x0, x1, 0.35, 0.45, 0.2, 0.35, "korkuluk")
        pb.box(x0, x1, 0.35, 0.45, 3.6, 3.75, "korkuluk")
        for i in range(int((x1 - x0) / 0.3)):
            xx = x0 + 0.15 + i * 0.3
            pb.box(xx - 0.03, xx + 0.03, 0.37, 0.43, 0.2, 3.9, "korkuluk")
        # nöbet kulübesi
        kx = sx * (gw / 2 + 4.5)
        pb.box(kx - 0.8, kx + 0.8, -0.2, 1.4, 0.0, 2.5, "kule_beton")
        pb.box(kx - 0.6, kx + 0.6, -0.25, -0.2, 1.0, 2.0, "cam")
        pb.box(kx - 1.0, kx + 1.0, -0.4, 1.6, 2.5, 2.7, "granit")
    # bayrak direkleri
    for sx in (-1, 1):
        x = sx * (gw / 2 + 7.5)
        pb.cylinder(x, 0.5, 0.0, 12.0, 0.12, "metal", segments=6)
        pb.box(x + 0.1, x + 3.1, 0.45, 0.55, 10.0, 12.0, "kirmizi_bayrak")
    return pb.to_object(name, material)


# ---------------------------------------------------------------------------
# Kuğulu Park
# ---------------------------------------------------------------------------
def build_kugulu_park(name, material):
    """Kuğulu Park parçası: oval gölet, taş kenar, yürüyüş yolu, söğütler ve kuğular. 44 × 30 m.
    Pivot ön kenarın ortası (yola bakan taraf)."""
    pb = PropBuilder()
    rng = random.Random(3)
    W, D = 44.0, 30.0
    pb.box(-W / 2, W / 2, 0.0, D, -0.3, 0.12, "cim")
    # yürüyüş yolu çevresi
    pb.box(-W / 2, W / 2, 0.0, 2.5, 0.12, 0.16, "kaldirim")
    # gölet: elips, taş kenar
    cx, cy, rx, ry = 0.0, D * 0.55, 14.0, 8.0
    seg = 28
    ring_out = [(cx + (rx + 0.8) * math.cos(2 * math.pi * i / seg), cy + (ry + 0.8) * math.sin(2 * math.pi * i / seg))
                for i in range(seg)]
    pb.prism(ring_out, 0.12, 0.45, "tas")
    water = [(cx + rx * math.cos(2 * math.pi * i / seg), cy + ry * math.sin(2 * math.pi * i / seg)) for i in range(seg)]
    pb.prism(water, 0.12, 0.47, "su")
    # kuğular
    for i in range(5):
        a = rng.uniform(0, 2 * math.pi)
        r = rng.uniform(0.2, 0.7)
        sx, sy = cx + rx * r * math.cos(a), cy + ry * r * math.sin(a)
        pb.sphere(sx, sy, 0.62, 0.45, 0.28, 0.2, "kuğu")
        pb.box(sx - 0.05, sx + 0.05, sy - 0.33, sy - 0.25, 0.62, 1.15, "kuğu")
        pb.box(sx - 0.04, sx + 0.04, sy - 0.48, sy - 0.33, 1.05, 1.12, "gaga")
    # söğütler (sarkık, geniş taç) ve banklar
    for i in range(6):
        a = 2 * math.pi * (i + 0.5) / 6
        tx, ty = cx + (rx + 3.5) * math.cos(a), cy + (ry + 2.8) * math.sin(a)
        if ty < 6.0:
            continue  # yol tarafını açık bırak, gölet görünsün
        willow(pb, tx, ty, rng)
    for x in (-12.0, -4.0, 4.0, 12.0):
        pb.box(x - 0.9, x + 0.9, 2.7, 3.1, 0.45, 0.5, "kapi")
        pb.box(x - 0.9, x + 0.9, 3.05, 3.15, 0.5, 0.9, "kapi")
        for dx in (-0.7, 0.7):
            pb.box(x + dx - 0.05, x + dx + 0.05, 2.75, 3.1, 0.12, 0.45, "korkuluk")
    return pb.to_object(name, material)


# ---------------------------------------------------------------------------
# Sokak objeleri
# ---------------------------------------------------------------------------
def willow(pb, x, y, rng):
    h = rng.uniform(6.0, 8.0)
    pb.cylinder(x, y, 0.0, h * 0.55, 0.25, "agac_govde", segments=6, radius_top=0.18)
    # sarkık dallar: üstte yuvarlak taç, altta yere doğru açılan "perde"
    pb.sphere(x, y, h * 0.72, 2.0, 2.0, 1.4, "yaprak_acik", subdiv=1)
    pb.cylinder(x, y, h * 0.3, h * 0.7, 2.4, "yaprak_acik", segments=8, radius_top=1.6)


def tree_plane(pb, x, y, rng, h=None):
    """Çınar benzeri yuvarlak taçlı ağaç (refüj ve kaldırım)."""
    h = h or rng.uniform(7.0, 10.0)
    pb.cylinder(x, y, 0.0, h * 0.5, 0.22, "agac_govde", segments=6, radius_top=0.15)
    pb.sphere(x, y, h * 0.68, h * 0.3, h * 0.3, h * 0.3, "yaprak", subdiv=1)
    pb.sphere(x + h * 0.12, y - h * 0.08, h * 0.8, h * 0.2, h * 0.2, h * 0.18, "yaprak_acik", subdiv=1)


def tree_poplar(pb, x, y, rng, h=None):
    """Kavak: Ankara'nın uzun ince ağacı."""
    h = h or rng.uniform(12.0, 16.0)
    pb.cylinder(x, y, 0.0, h * 0.25, 0.2, "agac_govde", segments=6, radius_top=0.15)
    pb.sphere(x, y, h * 0.6, 1.4, 1.4, h * 0.42, "yaprak", subdiv=1)


def build_tree(name, material, kind, seed):
    pb = PropBuilder()
    rng = random.Random(seed)
    {"cinar": tree_plane, "kavak": tree_poplar}[kind](pb, 0.0, 0.0, rng)
    return pb.to_object(name, material)


def build_ego_stop(name, material):
    """EGO otobüs durağı: metal çerçeve, cam arka panel, eğimli çatı, bank, durak tabelası direği.
    Ön yüz (yolcuların baktığı taraf) yola bakar. 6 m genişlik."""
    pb = PropBuilder()
    W, D, H = 6.0, 1.8, 2.7
    for x in (-W / 2, 0.0, W / 2):
        pb.box(x - 0.06, x + 0.06, D - 0.12, D, 0.0, H, "metal")
        pb.box(x - 0.06, x + 0.06, 0.0, 0.12, 0.0, H - 0.15, "metal")
    pb.box(-W / 2, W / 2, D - 0.08, D - 0.04, 0.2, H - 0.1, "dukkan_cam")
    pb.box(-W / 2 - 0.2, W / 2 + 0.2, -0.3, D + 0.1, H, H + 0.12, "ego_mavi",
           matrix=Matrix.Translation((0, 0, H)) @ Matrix.Rotation(math.radians(-4), 4, "X") @ Matrix.Translation((0, 0, -H)))
    pb.box(-W / 2, W / 2, -0.3, -0.2, H - 0.35, H, "ego_mavi")
    pb.box(-W / 2 + 0.3, -W / 2 + 0.4, -0.31, -0.2, H - 0.3, H - 0.05, "tabela_beyaz")
    # bank
    pb.box(-W / 2 + 0.6, W / 2 - 0.6, D - 0.7, D - 0.2, 0.45, 0.52, "metal")
    for x in (-W / 2 + 0.8, W / 2 - 0.8):
        pb.box(x - 0.05, x + 0.05, D - 0.6, D - 0.3, 0.0, 0.45, "korkuluk")
    # yan cam panel (sol)
    pb.box(-W / 2 - 0.02, -W / 2 + 0.02, 0.15, D - 0.12, 0.2, H - 0.1, "dukkan_cam")
    # durak tabelası direği: hat numaraları paneli
    pb.cylinder(W / 2 + 1.2, -0.2, 0.0, 3.2, 0.06, "metal", segments=6)
    pb.box(W / 2 + 0.85, W / 2 + 1.55, -0.25, -0.15, 2.3, 3.2, "ego_mavi")
    pb.box(W / 2 + 0.9, W / 2 + 1.5, -0.26, -0.24, 2.4, 2.9, "tabela_beyaz")
    # zemin platformu
    pb.box(-W / 2 - 0.3, W / 2 + 1.6, -0.5, D + 0.2, -0.05, 0.02, "kaldirim")
    return pb.to_object(name, material)


def build_street_lamp(name, material):
    """Çift kollu bulvar/refüj lambası, 9 m."""
    pb = PropBuilder()
    pb.cylinder(0, 0, 0.0, 0.6, 0.18, "metal", segments=8)
    pb.cylinder(0, 0, 0.6, 9.0, 0.11, "metal", segments=8, radius_top=0.07)
    for sx in (-1, 1):
        pb.box(min(0, sx * 1.6), max(0, sx * 1.6), -0.04, 0.04, 8.9, 9.0, "metal")
        pb.box(sx * 1.6 - 0.35, sx * 1.6 + 0.35, -0.15, 0.15, 8.75, 8.95, "silme")
    return pb.to_object(name, material)


def build_traffic_light(name, material):
    """Kavşak trafik ışığı: 3,6 m direk, yola uzanan kol, üç lambalı kafa.
    Lambalar ayrı objeler ("Lamba_Kirmizi", "Lamba_Sari", "Lamba_Yesil"); TrafficSignal yanan lambayı açar,
    sönükken arkadaki koyu lens görünür. Kafa yerel +Z'ye (Unity) bakar; araçlara dönük yerleştirilir."""
    root = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(root)
    pb = PropBuilder()
    pb.cylinder(0, 0, 0.0, 0.3, 0.16, "metal", segments=8)
    pb.cylinder(0, 0, 0.3, 4.6, 0.08, "metal", segments=8)
    # kol: yerel +X (Unity) yönüne uzanır. Işık sağ kaldırımda araçlara dönük konunca (Y açısı = yön + 180°)
    # kol yolun ortasına doğru uzanır. Blender'da Unity +X = -X.
    pb.box(-2.2, 0.0, -0.05, 0.05, 4.5, 4.6, "metal")
    hx = -2.1
    pb.box(hx - 0.2, hx + 0.2, -0.12, 0.12, 3.55, 4.5, "lastik")          # kafa gövdesi
    pb.box(hx - 0.24, hx + 0.24, -0.16, -0.12, 3.5, 4.55, "lastik")        # arka plaka
    lamps = (("Lamba_Kirmizi", 4.25, "isik_kirmizi"), ("Lamba_Sari", 4.02, "isik_sari"), ("Lamba_Yesil", 3.79, "isik_yesil"))
    for _, z, _ in lamps:
        pb.box(hx - 0.1, hx + 0.1, -0.135, -0.12, z - 0.1, z + 0.1, "lens_koyu")
        pb.box(hx - 0.13, hx + 0.13, -0.24, -0.12, z + 0.1, z + 0.12, "lastik")  # siperlik
    # direkte yayalar için küçük ikinci kafa
    pb.box(-0.1, 0.1, -0.2, -0.08, 2.3, 2.9, "lastik")
    body = pb.to_object("Direk", material)
    body.parent = root
    for lname, z, color in lamps:
        lb = PropBuilder()
        lb.box(hx - 0.095, hx + 0.095, -0.15, -0.135, z - 0.095, z + 0.095, color)
        lo = lb.to_object(lname, material)
        lo.parent = root
    return root


ITEMS = {
    "Atakule": ("Landmarks", build_atakule),
    "KizilayAVM": ("Landmarks", build_kizilay_avm),
    "TBMM_Duvar_20m": ("Landmarks", build_tbmm_wall),
    "TBMM_Kapi": ("Landmarks", build_tbmm_gate),
    "KuguluPark_Golet": ("Landmarks", build_kugulu_park),
    "EGO_Durak": ("Props", build_ego_stop),
    "Lamba_Bulvar": ("Props", build_street_lamp),
    "Trafik_Lambasi": ("Props", build_traffic_light),
    "Agac_Cinar_1": ("Props", lambda n, m: build_tree(n, m, "cinar", 1)),
    "Agac_Cinar_2": ("Props", lambda n, m: build_tree(n, m, "cinar", 2)),
    "Agac_Kavak": ("Props", lambda n, m: build_tree(n, m, "kavak", 3)),
}


def render_scene(objs, path):
    """Atakule ve diğer simge yapıları bir sahnede gösterir."""
    layout = {
        "Atakule": (-20, 60, 0), "KizilayAVM": (55, 40, 0), "TBMM_Kapi": (-5, -2, 0),
        "TBMM_Duvar_20m": [(-27, -2, 0), (17, -2, 0)], "KuguluPark_Golet": (-60, 0, 0),
        "EGO_Durak": (25, -8, 0), "Lamba_Bulvar": [(10, -12, 0), (35, -12, 0)],
        "Agac_Cinar_1": [(-15, -10, 0), (5, -10, 0)], "Agac_Cinar_2": [(-35, -10, 0), (45, -10, 0)],
        "Agac_Kavak": [(-25, 20, 0), (-30, 25, 0), (5, 25, 0)],
    }
    scene = bpy.context.scene
    for name, obj in objs.items():
        spots = layout.get(name)
        if spots is None:
            obj.hide_render = True
            continue
        spots = spots if isinstance(spots, list) else [spots]
        obj.location = spots[0]
        for loc in spots[1:]:
            o = obj.copy()
            o.location = loc
            scene.collection.objects.link(o)
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 40
    scene.cycles.use_denoising = True
    scene.render.resolution_x, scene.render.resolution_y = 1600, 900
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.view_settings.exposure = -0.3
    world = bpy.data.worlds.new("Gok")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.5, 0.68, 0.92, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.5
    scene.world = world
    sun = bpy.data.lights.new("Gunes", "SUN")
    sun.energy = 3.5
    so = bpy.data.objects.new("Gunes", sun)
    so.rotation_euler = (math.radians(50), 0, math.radians(-40))
    scene.collection.objects.link(so)
    mat = bpy.data.materials.new("Zemin")
    mat.use_nodes = True
    mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.25, 0.25, 0.26, 1)
    bpy.ops.mesh.primitive_plane_add(size=400, location=(0, 0, -0.01))
    bpy.context.object.data.materials.append(mat)
    cam = bpy.data.cameras.new("Kamera")
    cam.lens = 22
    co = bpy.data.objects.new("Kamera", cam)
    scene.collection.objects.link(co)
    co.location = (45, -115, 28)
    target = Vector((-8, 30, 52))
    co.rotation_euler = (target - co.location).to_track_quat("-Z", "Y").to_euler()
    scene.camera = co
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True, help="Assets/_Project/Maps gibi; Landmarks/ ve Props/ alt klasörleri açılır")
    ap.add_argument("--render", default=None)
    args = ap.parse_args(argv)
    kit.clear_scene()
    img = kit.build_palette_image(os.path.join(args.out, "T_AnkaraPalet.png"))
    mat = kit.palette_material(img)
    objs = {}
    for name, (folder, make) in ITEMS.items():
        d = os.path.join(args.out, folder)
        os.makedirs(d, exist_ok=True)
        obj = make(name, mat)
        if obj.type == "EMPTY":
            # çok parçalı obje (ör. trafik ışığı): kök ve çocukları birlikte
            bpy.ops.object.select_all(action="DESELECT")
            for o in [obj] + list(obj.children):
                o.select_set(True)
            bpy.ops.export_scene.fbx(filepath=os.path.join(d, name + ".fbx"), use_selection=True,
                                     apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                                     bake_space_transform=True, path_mode="STRIP", object_types={"MESH", "EMPTY"},
                                     mesh_smooth_type="FACE")
            tris = sum(len(p.vertices) - 2 for c in obj.children for p in c.data.polygons)
        else:
            kit.export_fbx(obj, os.path.join(d, name + ".fbx"))
            tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)
        print(f"| `{folder}/{name}` | {obj.dimensions.x:.1f} × {obj.dimensions.y:.1f} × {obj.dimensions.z:.1f} | {tris} |")
        objs[name] = obj
    if args.render:
        render_scene(objs, args.render)


if __name__ == "__main__":
    main()
