"""
Tek gövdeli otobüsün Unity'ye hazır FBX'inden körüklü versiyonunu üretir (BMC Procity 12LF → BMC_Procity_18,
Caio Millennium II → Caio_Millennium_II_G). Plan: docs/PLAN_Y19_KORUKLU.md.

Kaynak gövde iki yerden kesilir: ön gövde kaynağın önünden ON_KESIM'e kadar, arka gövde kaynağın ARKA_BAS'ından arkasına
kadar (kaydırılıp mafsalın gerisine konur). Aradaki boşluğa kıvrımlı kauçuk körük, gövdelerin açık uçlarına kauçuk çerçeve.
Doku, UV ve malzemeler kaynağınkiler (kaplamalar aynen çalışır); çıktı FBX kaynağın klasörüne yazılır.

Parçalar (Unity: ön +Z, sağ +X; Blender'da ön −Y):
  Govde_On (pivot kaynağın Govde pivotu), Govde_Arka ve Koruk (pivot mafsalda, yerden 1 m), Teker_OnSol/OnSag,
  Teker_OrtaSol/OrtaSag (kaynağın arka aksı), Teker_ArkaSol/ArkaSag (arka gövdede, çeken aks), Kapi_N_K (arka gövdedekiler
  kaynağın kanat kopyaları), Direksiyon, Lamba_* (ön gövde) ve Lamba_*_Arka (arka gövde), Golge_On/Golge_Arka (mobil).
İç mekânın küçük parçaları (koltuk, direk) kesilmez: kesim düzleminden geçenler silinir (körük bölgesi boş kalır, gerçekte de
orada koltuk yok); dış kabuk ve büyük parçalar düzlemde kesilir.

Kullanım (mobil ve tam kalite için ayrı ayrı):
    Blender -b --factory-startup --python koruklu_yap.py -- --kaynak BMC_Procity_12LF.fbx --otobus bmc [--render klasör]
"""
import argparse
import math
import os
import sys

import bpy  # noqa: E402
import bmesh  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402

# Unity z değerleri (ön +Z). arka gövde kaydırması = (MAFSAL − KORUK_BOY/2) − ARKA_BAS
OTOBUSLER = {
    "bmc": dict(ad="BMC_Procity_18", on_kesim=-3.55, arka_bas=0.85, mafsal=-4.00, koruk_boy=0.90,
                arka_kapilar=[("Kapi_2", "Kapi_3"), ("Kapi_3", "Kapi_4")],
                # tavandaki CNG kutusu kaynakta z 0,4 … 4,3: kuyruğu arka gövdenin başına düşüyor (kesik çıkıntı) → silinir
                arka_sil=[("M_cngtank", 1.2)]),
    "millennium": dict(ad="Caio_Millennium_II_G", on_kesim=-4.00, arka_bas=0.80, mafsal=-4.45, koruk_boy=0.90,
                       arka_kapilar=[("Kapi_2", "Kapi_3")]),
}
MAFSAL_Y = 1.0            # mafsal (Govde_Arka, Koruk pivotu) yerden yüksekliği
KUCUK_PARCA = 1.6         # bu boydan (Unity z) kısa ve iç mekânda kalan adacıklar kesilmez: kesimden geçiyorsa silinir
IC_X, IC_ALT, IC_UST = 1.12, 0.25, 2.95   # iç mekân kutusu (dış kabuk bunun dışına taşar)
GECIS_X = 1.05            # körük geçiş açıklığı (yarı genişlik)
GECIS_UST = 2.45          # açıklığın üst kenarı (yerden)
KIVRIM = 14               # körük kıvrım sayısı
KIVRIM_DERIN = 0.055      # m
CERCEVE_RENK = (0.035, 0.035, 0.038)
KORUK_RENK = (0.11, 0.11, 0.115)
TABLA_RENK = (0.30, 0.31, 0.32)


def uz(v):
    """Blender dünya → Unity z."""
    return -v.y


def malzeme(ad, renk, doku=None):
    m = bpy.data.materials.get(ad) or bpy.data.materials.new(ad)
    m.use_nodes = True
    bsdf = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Base Color"].default_value = (*renk, 1)
    bsdf.inputs["Roughness"].default_value = 0.85
    if doku and not any(n.type == "TEX_IMAGE" for n in m.node_tree.nodes):
        t = m.node_tree.nodes.new("ShaderNodeTexImage")
        t.image = bpy.data.images.load(doku)
        m.node_tree.links.new(t.outputs["Color"], bsdf.inputs["Base Color"])
    return m


def kopya(obj, ad):
    yeni = obj.copy()
    yeni.data = obj.data.copy()
    yeni.name = ad
    yeni.data.name = ad
    for c in obj.users_collection:
        c.objects.link(yeni)
    return yeni


def adaciklar(bm):
    """Bağlı yüz grupları (köşe paylaşımı)."""
    gorulen = set()
    gruplar = []
    for f in bm.faces:
        if f.index in gorulen:
            continue
        grup, yigin = [], [f]
        gorulen.add(f.index)
        while yigin:
            g = yigin.pop()
            grup.append(g)
            for v in g.verts:
                for h in v.link_faces:
                    if h.index not in gorulen:
                        gorulen.add(h.index)
                        yigin.append(h)
        gruplar.append(grup)
    return gruplar


def kes(obj, sinir, onde_kalsin):
    """obj'yi Unity z = sinir düzleminde keser. onde_kalsin: z > sinir kalır (ön gövde), değilse z < sinir kalır.
    Küçük adacıklar bütün olarak merkezlerine göre; büyükler bisect ile. Kesimde açılan sınır köşelerini döndürür (dünya)."""
    mw = obj.matrix_world
    inv = mw.inverted()
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.faces.ensure_lookup_table()

    def tarafi(z):
        return z > sinir if onde_kalsin else z < sinir

    sil, bol = [], []
    for grup in adaciklar(bm):
        zs = [uz(mw @ v.co) for f in grup for v in f.verts]
        zmin, zmax = min(zs), max(zs)
        if tarafi(zmin) and tarafi(zmax):
            continue
        if not tarafi(zmin) and not tarafi(zmax):
            sil.extend(grup)
            continue
        if zmax - zmin < KUCUK_PARCA:
            ws = [mw @ v.co for f in grup for v in f.verts]
            ic = max(abs(w.x) for w in ws) < IC_X and max(w.z for w in ws) < IC_UST and min(w.z for w in ws) > IC_ALT
            if ic:
                # iç mekân parçası (koltuk, direk, tutamak) kesimden geçiyor: körük bölgesine taşmasın, kesilmesin → silinir
                sil.extend(grup)
                continue
        bol.extend(grup)
    bmesh.ops.delete(bm, geom=list(set(sil)), context="FACES")

    sinir_kose = []
    if bol:
        bol = [f for f in bol if f.is_valid]
        geom = list(set(bol)) + list({e for f in bol for e in f.edges}) + list({v for f in bol for v in f.verts})
        # düzlem (yerel): dünya noktası (0, −sinir, 0), normal dünya −Y (Unity +z)
        nokta = inv @ Vector((0, -sinir, 0))
        normal = (inv.to_3x3().transposed().inverted() @ Vector((0, -1, 0))).normalized()
        # clear_outer: normalin pozitif tarafı (Unity z > sinir) silinir
        sonuc = bmesh.ops.bisect_plane(bm, geom=geom, plane_co=nokta, plane_no=normal,
                                       clear_outer=not onde_kalsin, clear_inner=onde_kalsin, dist=1e-5)
        sinir_kose = [mw @ v.co for v in sonuc["geom_cut"] if isinstance(v, bmesh.types.BMVert) and v.is_valid]
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    bmesh.ops.dissolve_degenerate(bm, edges=bm.edges[:], dist=1e-6)
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()
    return sinir_kose


def yuz_sec_sil(obj, birak):
    """Merkezi birak(z) olmayan yüzleri siler (lambalar gibi küçük parçalar). Boş kalırsa False."""
    mw = obj.matrix_world
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    sil = [f for f in bm.faces if not birak(uz(mw @ f.calc_center_median()))]
    bmesh.ops.delete(bm, geom=sil, context="FACES")
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    bos = len(bm.faces) == 0
    bm.to_mesh(obj.data)
    bm.free()
    return not bos


def malzeme_sil(obj, mad, z0, z1):
    """z0 … z1 (Unity z) aralığında merkezi olan, adı mad olan malzemeli yüzleri siler."""
    adlar = [m.name if m else "" for m in obj.data.materials]
    if mad not in adlar:
        return
    mi = adlar.index(mad)
    mw = obj.matrix_world
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    sil = [f for f in bm.faces if f.material_index == mi and z0 < uz(mw @ f.calc_center_median()) < z1]
    bmesh.ops.delete(bm, geom=sil, context="FACES")
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    bm.to_mesh(obj.data)
    bm.free()
    print(f"  {obj.name}: {mad} {len(sil)} yüz silindi")


def cati_yamasi(obj, z0, z1, yari_en=0.98):
    """Silinen tavan kutusunun altında çatı yoksa: z0 … z1 arasına düz çatı levhası (çatının yüksekliği ve dokusu hemen
    önündeki çatıdan, yukarıdan ışınla ölçülür; levha 4 mm yukarıda, varsa alttaki çatıyla çakışmasın)."""
    from mathutils.bvhtree import BVHTree
    mw = obj.matrix_world
    inv = mw.inverted()
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.faces.ensure_lookup_table()
    bvh = BVHTree.FromBMesh(bm)
    hit = bvh.ray_cast(inv @ Vector((0, -(z0 - 0.3), 6.0)), Vector((0, 0, -1)), 6.0)
    if hit[0] is None:
        bm.free()
        print("  çatı yaması: çatı bulunamadı")
        return
    y = (mw @ hit[0]).z + 0.004
    yuz = bm.faces[hit[2]]
    uv = bm.loops.layers.uv.active
    uvn = tuple(yuz.loops[0][uv].uv)
    vs = [bm.verts.new(inv @ Vector((-x, -z, y))) for x, z in ((-yari_en, z0), (yari_en, z0), (yari_en, z1), (-yari_en, z1))]
    f = bm.faces.new(vs)
    f.normal_update()
    if (mw.to_3x3() @ f.normal).z < 0:
        f.normal_flip()
    f.material_index = yuz.material_index
    for lo in f.loops:
        lo[uv].uv = uvn
    bm.to_mesh(obj.data)
    bm.free()
    print(f"  {obj.name}: çatı yaması y {y:.3f}, z {z0:.2f} … {z1:.2f}")


def kaydir(obj, dz):
    """Unity z'de kaydır (Blender −y)."""
    obj.location.y -= dz


def pivot_tasi(obj, dunya):
    """Objenin pivotunu dünya noktasına taşır (geometri yerinde kalır)."""
    mw = obj.matrix_world.copy()
    fark = mw.inverted() @ dunya
    obj.data.transform(Matrix.Translation(-fark))
    obj.matrix_world = mw @ Matrix.Translation(fark)


def dis_bukey_kontur(noktalar, n=72):
    """Kesit noktalarından (x, y) dışbükey zarf → n eşit açılı nokta (merkezden ışınla)."""
    pts = sorted(set((round(p[0], 4), round(p[1], 4)) for p in noktalar))
    if len(pts) < 3:
        return None

    def cross(o, a, b):
        return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])
    alt, ust = [], []
    for p in pts:
        while len(alt) >= 2 and cross(alt[-2], alt[-1], p) <= 0:
            alt.pop()
        alt.append(p)
    for p in reversed(pts):
        while len(ust) >= 2 and cross(ust[-2], ust[-1], p) <= 0:
            ust.pop()
        ust.append(p)
    zarf = alt[:-1] + ust[:-1]
    cx = sum(p[0] for p in zarf) / len(zarf)
    cy = sum(p[1] for p in zarf) / len(zarf)
    sonuc = []
    for i in range(n):
        a = 2 * math.pi * i / n
        d = Vector((math.cos(a), math.sin(a)))
        en_iyi = None
        for j in range(len(zarf)):
            p, q = Vector(zarf[j]), Vector(zarf[(j + 1) % len(zarf)])
            # ışın (c + t d) ile kenar (p + s (q − p)) kesişimi
            e = q - p
            den = d.x * e.y - d.y * e.x
            if abs(den) < 1e-9:
                continue
            w = p - Vector((cx, cy))
            t = (w.x * e.y - w.y * e.x) / den
            s = (w.x * d.y - w.y * d.x) / den
            if t > 0 and -1e-6 <= s <= 1 + 1e-6 and (en_iyi is None or t < en_iyi):
                en_iyi = t
        if en_iyi is not None:
            sonuc.append((cx + d.x * en_iyi, cy + d.y * en_iyi))
    return sonuc, (cx, cy)


def gecis(n, taban):
    """Geçiş açıklığı: yuvarlak köşeli dikdörtgen (x ±GECIS_X, taban … GECIS_UST), n nokta, merkez açısına göre."""
    r = 0.25
    cx, cy = 0.0, (taban + GECIS_UST) / 2
    hw, hh = GECIS_X, (GECIS_UST - taban) / 2
    sonuc = []
    for i in range(n):
        a = 2 * math.pi * i / n
        d = Vector((math.cos(a), math.sin(a)))
        # süper elips yaklaşık (köşe yuvarlak)
        p = 8.0
        k = (abs(d.x / hw) ** p + abs(d.y / hh) ** p) ** (-1 / p)
        sonuc.append((cx + d.x * k, cy + d.y * k))
    return sonuc


def halka(bm, dis, ic, z_dunya, yon, uv=None):
    """dis ve ic konturları (aynı sayıda nokta, (x_unity, y)) arasında yüz halkası, Unity z'de. yon: +1 normal Unity +z."""
    n = len(dis)
    vd = [bm.verts.new(Vector((-x, -z_dunya, y))) for x, y in dis]
    vi = [bm.verts.new(Vector((-x, -z_dunya, y))) for x, y in ic]
    for i in range(n):
        j = (i + 1) % n
        q = [vd[i], vd[j], vi[j], vi[i]]
        f = bm.faces.new(q)
        f.normal_update()
        # Unity +z = Blender −y
        if (f.normal.y < 0) != (yon > 0):
            f.normal_flip()
    return vd, vi


def taban_yuksekligi(obj, z):
    """Gövdenin içinde (x = 0) z'deki taban yüksekliği: yukarıdan aşağı ışın."""
    from mathutils.bvhtree import BVHTree
    dg = bpy.context.evaluated_depsgraph_get()
    bvh = BVHTree.FromObject(obj, dg)
    inv = obj.matrix_world.inverted()
    enler = []
    for x in (-0.3, 0.0, 0.3):
        p = inv @ Vector((-x, -z, 1.6))
        d = (inv.to_3x3() @ Vector((0, 0, -1))).normalized()
        h = bvh.ray_cast(p, d, 2.0)
        if h[0] is not None:
            enler.append((obj.matrix_world @ h[0]).z)
    return min(enler) if enler else 0.35


def cerceve(govde, sinir_kose, z, yon, taban, cerceve_mat):
    """Gövdenin açık ucuna kauçuk çerçeve (dış: kesit zarfı, iç: geçiş açıklığı), iki yüzlü, gövdenin mesh'ine eklenir."""
    kesit = [(-p.x, p.z) for p in sinir_kose]
    k = dis_bukey_kontur(kesit)
    if k is None:
        print("  çerçeve: kesit yok")
        return None
    dis, merkez = k
    ic = gecis(len(dis), taban)
    bm = bmesh.new()
    halka(bm, dis, ic, z, yon)
    halka(bm, dis, ic, z - yon * 0.012, -yon)
    # açıklığın kenarında 1,2 cm kalınlık (iç yüz)
    me = bpy.data.meshes.new("cerceve")
    bm.to_mesh(me)
    bm.free()
    gecici = bpy.data.objects.new("cerceve", me)
    bpy.context.scene.collection.objects.link(gecici)
    me.materials.append(cerceve_mat)
    # gövdeyle birleştir (gövdenin malzeme listesine eklenir)
    bpy.ops.object.select_all(action="DESELECT")
    gecici.select_set(True)
    govde.select_set(True)
    bpy.context.view_layer.objects.active = govde
    bpy.ops.object.join()
    return dis


def koruk_yap(dis_on, dis_arka, z_bas, z_son, taban, mafsal_dunya, koruk_mat, tabla_mat):
    """Kıvrımlı körük: iki ucun kontur ortalamasından, dış ve iç yüzey; tabanda döner tabla. Pivot mafsalda."""
    n = len(dis_on)
    kontur = [((a[0] + b[0]) / 2, (a[1] + b[1]) / 2) for a, b in zip(dis_on, dis_arka)]
    cx = sum(p[0] for p in kontur) / n
    cy = sum(p[1] for p in kontur) / n
    # alt kenar tabana yaslı kalsın: ölçek merkezi alt kenarda değil, merkezde; taban çizgisi ayrıca kırpılır
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    halkalar = 2 * KIVRIM + 1
    cevre = sum((Vector(kontur[i]) - Vector(kontur[(i + 1) % n])).length for i in range(n))

    def olcekli(s):
        return [(cx + (x - cx) * s, max(cy + (y - cy) * s, taban - 0.02)) for x, y in kontur]

    for katman, (s_dis, s_ic, yon) in enumerate(((0.985, 0.955, 1), (0.93, 0.90, -1))):
        onceki = None
        for i in range(halkalar):
            z = z_bas + (z_son - z_bas) * i / (halkalar - 1)
            s = s_dis if i % 2 == 0 else s_ic
            if i in (0, halkalar - 1):
                s = s_dis
            pts = olcekli(s * (1.0 if katman == 0 else 1.0))
            vs = [bm.verts.new(Vector((-x, -z, y))) for x, y in pts]
            if onceki is not None:
                birikim = 0.0
                for j in range(n):
                    k = (j + 1) % n
                    f = bm.faces.new([onceki[j], onceki[k], vs[k], vs[j]])
                    f.smooth = True
                    # dış katman normali dışarı, iç katman içeri
                    merkez = f.calc_center_median()
                    disari = Vector((merkez.x + cx, 0, merkez.z - cy))
                    if (f.normal.x * disari.x + f.normal.z * disari.z > 0) != (yon > 0):
                        f.normal_flip()
                    u0 = birikim / cevre
                    birikim += (Vector(pts[j]) - Vector(pts[k])).length
                    u1 = birikim / cevre
                    v0, v1 = (i - 1) / (halkalar - 1), i / (halkalar - 1)
                    for lo in f.loops:
                        idx = vs.index(lo.vert) if lo.vert in vs else onceki.index(lo.vert)
                        uu = u1 if idx == k else u0
                        vv = v1 if lo.vert in vs else v0
                        lo[uv].uv = (uu * 6.0, vv)
            onceki = vs
    # döner tabla (tabanda, iki gövdenin taban hizasında)
    w = GECIS_X + 0.1
    tv = [bm.verts.new(Vector((-x, -z, taban + 0.005))) for x, z in ((-w, z_bas), (w, z_bas), (w, z_son), (-w, z_son))]
    f = bm.faces.new(tv)
    if f.normal.z < 0:
        f.normal_flip()
    f.material_index = 1
    for lo, t in zip(f.loops, ((0, 0), (1, 0), (1, 1), (0, 1))):
        lo[uv].uv = t
    me = bpy.data.meshes.new("Koruk")
    bm.to_mesh(me)
    bm.free()
    me.materials.append(koruk_mat)
    me.materials.append(tabla_mat)
    obj = bpy.data.objects.new("Koruk", me)
    bpy.context.scene.collection.objects.link(obj)
    pivot_tasi(obj, mafsal_dunya)
    return obj


def golge_kabugu(golge, ad, sinir, onde, dz):
    """Gölge kabuğunun bir tarafı: köşeler sınıra kırpılıp dışbükey zarf."""
    mw = golge.matrix_world
    pts = []
    for v in golge.data.vertices:
        w = mw @ v.co
        z = uz(w)
        z = max(z, sinir) if onde else min(z, sinir)
        pts.append(Vector((w.x, -z, w.z)))
    bm = bmesh.new()
    for p in pts:
        bm.verts.new(p)
    bmesh.ops.convex_hull(bm, input=bm.verts[:])
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    me = bpy.data.meshes.new(ad)
    bm.to_mesh(me)
    bm.free()
    for m in golge.data.materials:
        me.materials.append(m)
    obj = bpy.data.objects.new(ad, me)
    bpy.context.scene.collection.objects.link(obj)
    obj.parent = golge.parent
    obj.matrix_parent_inverse = Matrix.Identity(4)
    if golge.parent is not None:
        obj.matrix_world = Matrix.Identity(4)
    if dz:
        kaydir(obj, dz)
    return obj


def koruk_dokusu(yol):
    """Kauçuk körük dokusu (256²): koyu gri, ince damar ve dikey hafif çizgi (kıvrım gölgesini geometri verir)."""
    if os.path.exists(yol):
        return yol
    import random
    from PIL import Image, ImageFilter
    rnd = random.Random(7)
    img = Image.new("L", (256, 256))
    px = img.load()
    for y in range(256):
        for x in range(256):
            px[x, y] = 30 + rnd.randint(0, 14) + (6 if x % 32 < 2 else 0)
    img = img.filter(ImageFilter.GaussianBlur(0.8)).convert("RGB")
    img.save(yol)
    return yol


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    ap = argparse.ArgumentParser()
    ap.add_argument("--kaynak", required=True, help="tek gövdelinin FBX'i (mobil ya da _TamKalite)")
    ap.add_argument("--otobus", required=True, choices=sorted(OTOBUSLER))
    ap.add_argument("--out", default=None, help="çıktı FBX (varsayılan: kaynağın klasöründe <ad>[_TamKalite].fbx)")
    args = ap.parse_args(argv)
    c = OTOBUSLER[args.otobus]
    tam = args.kaynak.endswith("_TamKalite.fbx")
    klasor = os.path.dirname(os.path.abspath(args.kaynak))
    cikti = args.out or os.path.join(klasor, c["ad"] + ("_TamKalite" if tam else "") + ".fbx")
    dz = (c["mafsal"] - c["koruk_boy"] / 2) - c["arka_bas"]     # arka gövde kaydırması (Unity z)
    z_on_ucu = c["mafsal"] + c["koruk_boy"] / 2                    # = on_kesim
    assert abs(z_on_ucu - c["on_kesim"]) < 1e-6, "on_kesim mafsal + koruk_boy/2 olmalı"

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=args.kaynak, axis_forward="-Z", axis_up="Y", bake_space_transform=True)
    sahne = {o.name: o for o in bpy.context.scene.objects}
    kok = next(o for o in sahne.values() if o.parent is None and o.type == "EMPTY")
    kok.name = c["ad"]
    govde = sahne["Govde"]
    mafsal = Vector((0, -c["mafsal"], MAFSAL_Y))
    cerceve_mat = malzeme("M_KorukCerceve", CERCEVE_RENK)
    doku = koruk_dokusu(os.path.join(klasor, "Textures", "koruk.png"))
    koruk_mat = malzeme("M_Koruk", KORUK_RENK, doku)
    tabla_mat = malzeme("M_KorukTabla", TABLA_RENK)

    # --- gövdeler
    arka = kopya(govde, "Govde_Arka")
    govde.name = "Govde_On"
    govde.data.name = "Govde_On"
    kes_on = kes(govde, c["on_kesim"], True)
    for mad, boy in c.get("arka_sil", []):
        # kesimden önce: kesit konturu (çerçeve, körük) silinen kutuyu içermesin
        malzeme_sil(arka, mad, c["arka_bas"] - boy, 1e9)
        cati_yamasi(arka, c["arka_bas"] - boy - 0.05, c["arka_bas"])
    kes_arka = kes(arka, c["arka_bas"], False)
    kaydir(arka, dz)
    kes_arka = [Vector((p.x, p.y - dz, p.z)) for p in kes_arka]
    print(f"kesit köşeleri: ön {len(kes_on)}, arka {len(kes_arka)}")
    taban_on = taban_yuksekligi(govde, c["on_kesim"] + 0.3)
    taban_arka = taban_yuksekligi(arka, c["on_kesim"] - c["koruk_boy"] - 0.3)
    print(f"taban: ön {taban_on:.3f}, arka {taban_arka:.3f}")
    taban = max(taban_on, taban_arka)
    dis_on = cerceve(govde, kes_on, c["on_kesim"], -1, taban, cerceve_mat)
    dis_arka = cerceve(arka, kes_arka, c["on_kesim"] - c["koruk_boy"], +1, taban, cerceve_mat)
    pivot_tasi(arka, mafsal)
    arka.parent = kok
    koruk = koruk_yap(dis_on, dis_arka, c["on_kesim"], c["on_kesim"] - c["koruk_boy"], taban, mafsal, koruk_mat, tabla_mat)
    koruk.parent = kok
    koruk.matrix_parent_inverse = kok.matrix_world.inverted()

    # --- tekerler: kaynağın arka aksı orta aks olur, kopyası arka gövdede çeken aks
    for yan in ("Sol", "Sag"):
        t = sahne[f"Teker_Arka{yan}"]
        yeni = kopya(t, f"Teker_Arka{yan}_y")
        t.name = f"Teker_Orta{yan}"
        t.data.name = t.name
        kaydir(yeni, dz)
        yeni.name = f"Teker_Arka{yan}"
        yeni.data.name = yeni.name

    # --- kapılar: ön gövdede olmayanlar silinir, arka gövde kapıları kaynağın kanat kopyaları
    kapilar = {o.name: o for o in bpy.context.scene.objects if o.name.startswith("Kapi_")}
    arka_kanatlar = []
    for kaynak, hedef in c["arka_kapilar"]:
        for k in (1, 2):
            o = kapilar.get(f"{kaynak}_{k}")
            if o is None:
                continue
            y = kopya(o, f"tmp_{hedef}_{k}")
            kaydir(y, dz)
            arka_kanatlar.append((y, f"{hedef}_{k}"))
    for ad, o in kapilar.items():
        z = uz(o.matrix_world.translation)
        if z < c["on_kesim"]:
            bpy.data.objects.remove(o)
    for y, ad in arka_kanatlar:
        y.name = ad
        y.data.name = ad

    # --- lambalar: ön kısmı aynı adla, arka kısmı _Arka
    for o in [o for o in bpy.context.scene.objects if o.name.startswith("Lamba_")]:
        ad = o.name
        a = kopya(o, ad + "_Arka")
        if not yuz_sec_sil(o, lambda z: z > c["on_kesim"]):
            bpy.data.objects.remove(o)
        if yuz_sec_sil(a, lambda z: z < c["arka_bas"]):
            kaydir(a, dz)
        else:
            bpy.data.objects.remove(a)

    # --- gölge kabukları (mobil)
    golge = sahne.get("Golge_Govde")
    if golge is not None:
        golge_kabugu(golge, "Golge_On", c["on_kesim"], True, 0)
        g = golge_kabugu(golge, "Golge_Arka", c["arka_bas"], False, dz)
        bpy.data.objects.remove(golge)

    bpy.context.view_layer.update()
    for o in bpy.context.scene.objects:
        if o.type == "MESH":
            zs = [uz(o.matrix_world @ v.co) for v in o.data.vertices] or [0]
            print(f"  {o.name:22s} {len(o.data.polygons):6d} yüz  z {min(zs):6.2f} … {max(zs):6.2f}  "
                  f"pivot z {uz(o.matrix_world.translation):6.2f}")
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.fbx(filepath=cikti, use_selection=True,
                             apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                             bake_space_transform=True, path_mode="STRIP", object_types={"MESH", "EMPTY"},
                             mesh_smooth_type="FACE")
    print("yazıldı:", cikti)


if __name__ == "__main__":
    main()
