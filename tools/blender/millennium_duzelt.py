"""
Caio Millennium II FBX son işlemi (Y19): iç zemin ve yan aynalar. millennium_donustur.py çıktısına (mobil ve tam kalite) bir kez
uygulanır; körüklü versiyon (koruklu_yap.py) bundan sonra yeniden üretilir.

1. Zemin: kaynakta zemin tek yüzlü ve normali aşağı bakıyordu (malzemesi düz siyah "pretoreal"); içeriden bakınca arka yüz
   çizilmediği için oyunda delik/siyah görünüyordu (yerel ölçüm: içeriden aşağı bakışta görüntünün %26–44'ü). Aşağı bakan zemin
   yüzleri (gövde içinde, yatay) 2 mm yukarıda yukarı bakan kopyayla kapanır: yeni malzeme M_Zemin, dokusu Textures/zemin.png
   (gri kaymaz kauçuk, kabarık nokta deseni), düzlemsel UV (0,6 m'de bir tekrar). Alttaki siyah yüz dışarıdan görünen alt kalır.
2. Yan aynalar: ayna kafaları ve kolları M_caroserie_Koyu kullanıyordu (kaplama dokusundan rastgele bölge görünüyor, kaplama
   değişince desen değişiyordu). Gövdenin dışına taşan küçük M_caroserie_Koyu adacıkları (ön uçta) ve ön üst köşedeki küçük
   adacıklar (sağ aynanın tavan kolu) aynadır: arkaya bakan düz
   yüzler M_AynaCam (koyu gri, parlak), kalanlar M_AynaGovde (siyaha yakın plastik). Kaplamadan bağımsız.

Kullanım:
    Blender -b --factory-startup --python millennium_duzelt.py -- --fbx Caio_Millennium_II.fbx
"""
import argparse
import os
import random
import sys

import bpy  # noqa: E402
import bmesh  # noqa: E402
from mathutils import Vector  # noqa: E402

ZEMIN_KAYNAK = "M_pretoreal"
ZEMIN_X = 1.18                 # gövde içi (yarı genişlik)
ZEMIN_Y = (0.25, 1.35)         # alçak taban ve arkadaki yüksek taban
ZEMIN_TEKRAR = 0.6             # m
SUPURGELIK_X = 1.02            # zeminin bu genişliği aşan dış kenarlarından süpürgelik kalkar
SUPURGELIK_BOY = 0.45          # m
SUPURGELIK_Y = 0.65            # yalnız alçak zemin (teker yuvası üstü / yüksek platform kenarlarında değil)
SUPURGELIK_DUVAR = 0.3         # dışarı doğru bu mesafede opak duvar yoksa (kapı camı, açıklık) süpürgelik yok
# sağdaki kapı boşlukları (Unity z): orada süpürgelik yok (basamak)
KAPI_Z = [(4.30, 5.53), (-0.62, 0.67)]
AYNA_KAYNAK = "M_caroserie_Koyu"
AYNA_X = 1.255                 # bu genişliği aşan adacık gövdenin dışına taşıyor
AYNA_Z = 4.5                   # yalnız ön uç (Unity z)
AYNA_BOY = 1.2                 # ayna adacığı en çok bu kadar
# sağ aynanın tavana bağlanan kolu gövde genişliğinin içinde kalıyor: ön üst köşedeki küçük adacıklar da ayna
AYNA_KOL = dict(x=1.05, y=2.15, z=5.4, boy=0.7)


def malzeme(ad, renk, doku=None, parlaklik=0.2):
    m = bpy.data.materials.get(ad) or bpy.data.materials.new(ad)
    m.use_nodes = True
    bsdf = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Base Color"].default_value = (*renk, 1)
    bsdf.inputs["Roughness"].default_value = 1.0 - parlaklik
    if doku and not any(n.type == "TEX_IMAGE" for n in m.node_tree.nodes):
        t = m.node_tree.nodes.new("ShaderNodeTexImage")
        t.image = bpy.data.images.load(doku)
        m.node_tree.links.new(t.outputs["Color"], bsdf.inputs["Base Color"])
    return m


def zemin_dokusu(yol, n=512):
    """Gri kaymaz kauçuk: hafif gürültülü zemin, düzenli kabarık yuvarlak noktalar (üstü açık, altı gölgeli)."""
    if os.path.exists(yol):
        return yol
    from PIL import Image, ImageDraw, ImageFilter
    rnd = random.Random(11)
    img = Image.new("RGB", (n, n), (92, 95, 99))
    px = img.load()
    for y in range(n):
        for x in range(n):
            g = rnd.randint(-6, 6)
            px[x, y] = (92 + g, 95 + g, 99 + g)
    d = ImageDraw.Draw(img)
    adim = n // 16
    r = adim * 0.28
    for j in range(16):
        for i in range(16):
            cx = i * adim + adim / 2 + (adim / 2 if j % 2 else 0)
            cy = j * adim + adim / 2
            for ox in (0, -n, n):
                d.ellipse([cx + ox - r, cy - r + 1.5, cx + ox + r, cy + r + 1.5], fill=(66, 68, 72))   # gölge
                d.ellipse([cx + ox - r, cy - r, cx + ox + r, cy + r], fill=(116, 119, 124))
    img = img.filter(ImageFilter.GaussianBlur(0.6))
    img.save(yol)
    return yol


def adaciklar(bm, faces):
    kume = set(f.index for f in faces)
    gorulen = set()
    for f in faces:
        if f.index in gorulen:
            continue
        grup, yigin = [], [f]
        gorulen.add(f.index)
        while yigin:
            g = yigin.pop()
            grup.append(g)
            for e in g.edges:
                for h in e.link_faces:
                    if h.index in kume and h.index not in gorulen:
                        gorulen.add(h.index)
                        yigin.append(h)
        yield grup


def zemin(obj, mat):
    mw = obj.matrix_world
    r = mw.to_3x3()
    inv = mw.inverted()
    me = obj.data
    adlar = [m.name if m else "" for m in me.materials]
    if ZEMIN_KAYNAK not in adlar:
        print("  zemin: kaynak malzeme yok")
        return
    ki = adlar.index(ZEMIN_KAYNAK)
    me.materials.append(mat)
    zi = len(me.materials) - 1
    bm = bmesh.new()
    bm.from_mesh(me)
    uv = bm.loops.layers.uv.active
    secilen = []
    for f in bm.faces:
        if f.material_index != ki:
            continue
        c = mw @ f.calc_center_median()
        n = (r @ f.normal).normalized()
        if n.z < -0.9 and abs(c.x) < ZEMIN_X and ZEMIN_Y[0] < c.z < ZEMIN_Y[1]:
            secilen.append(f)
    esle = {}
    alan = 0.0
    yukari = inv.to_3x3() @ Vector((0, 0, 0.002))
    for f in secilen:
        vs = []
        for v in reversed(f.verts):            # ters sıra: normal yukarı
            if v not in esle:
                esle[v] = bm.verts.new(v.co + yukari)
            vs.append(esle[v])
        try:
            nf = bm.faces.new(vs)
        except ValueError:
            continue
        nf.material_index = zi
        nf.smooth = False
        for lo in nf.loops:
            w = mw @ lo.vert.co
            lo[uv].uv = (w.x / ZEMIN_TEKRAR, w.y / ZEMIN_TEKRAR)
        alan += f.calc_area()
    # süpürgelik: zemin kopyasının dış kenarları (yalnız bir yüze ait, iki ucu da duvara yakın) ile yan duvar arasında
    # ince bir boşluk kalıyordu (içeriden aşağı bakınca yol görünüyor); kenardan yukarı, içe bakan dikey şerit
    yeni = set(esle.values())
    from mathutils.bvhtree import BVHTree
    bm.verts.index_update()
    eski = [f for f in bm.faces if f.material_index != zi]        # kaynağın yüzleri (yeni zemin hariç)
    bvh = BVHTree.FromPolygons([v.co.copy() for v in bm.verts], [[v.index for v in f.verts] for f in eski])
    eski_mat = [f.material_index for f in eski]

    def opak_duvar(w, isaret):
        """w (dünya) noktasından dışarı (Blender x işareti) yatay ışın: opak (cam olmayan) yüze değiyor mu."""
        p = inv @ (w + Vector((0, 0, 0.15)))
        d = (inv.to_3x3() @ Vector((isaret, 0, 0))).normalized()
        hit = bvh.ray_cast(p, d, SUPURGELIK_DUVAR)
        if hit[0] is None:
            return False
        ad = adlar[eski_mat[hit[2]]] if eski_mat[hit[2]] < len(adlar) else ""
        return not (ad.endswith("_Cam") or "vidro" in ad.lower())
    kenar_say = 0
    for e in list(bm.edges):
        if len(e.link_faces) != 1 or e.verts[0] not in yeni or e.verts[1] not in yeni:
            continue
        w0, w1 = mw @ e.verts[0].co, mw @ e.verts[1].co
        if min(abs(w0.x), abs(w1.x)) < SUPURGELIK_X or (w0.x > 0) != (w1.x > 0):
            continue
        if max(w0.z, w1.z) > SUPURGELIK_Y:
            continue
        # uzun kenar (kapı camlarının önünden geçebilir) 25 cm'lik parçalara bölünür, her parça ayrı denetlenir
        isaret = 1 if w0.x > 0 else -1
        parca = max(1, int((w1 - w0).length / 0.25 + 0.999))
        yuk = inv.to_3x3() @ Vector((0, 0, SUPURGELIK_BOY))
        for k in range(parca):
            a_co = e.verts[0].co.lerp(e.verts[1].co, k / parca)
            b_co = e.verts[0].co.lerp(e.verts[1].co, (k + 1) / parca)
            wa, wb = mw @ a_co, mw @ b_co
            orta_z = -(wa.y + wb.y) / 2
            if -wa.x > 0 and any(a - 0.05 < orta_z < b + 0.05 for a, b in KAPI_Z):
                continue                           # sağ kapı basamakları açık kalır (Unity sağ = Blender −x)
            if not opak_duvar((wa + wb) / 2, isaret):
                continue
            vs = [bm.verts.new(a_co), bm.verts.new(b_co), bm.verts.new(b_co + yuk), bm.verts.new(a_co + yuk)]
            nf = bm.faces.new(vs)
            nf.normal_update()
            merkez = mw @ nf.calc_center_median()
            if (r @ nf.normal).x * merkez.x > 0:  # duvara değil, otobüsün ortasına baksın
                nf.normal_flip()
            nf.material_index = zi
            for lo in nf.loops:
                w = mw @ lo.vert.co
                lo[uv].uv = (w.y / ZEMIN_TEKRAR, w.z / ZEMIN_TEKRAR)
            kenar_say += 1
            if os.environ.get("SUPURGELIK_YAZ"):
                print(f"    kenar x {-wa.x:+.2f} z {-wa.y:.2f} {-wb.y:.2f} y {wa.z:.2f}")
    print(f"  süpürgelik: {kenar_say} kenar")
    bm.to_mesh(me)
    bm.free()
    print(f"  zemin: {len(secilen)} yüz, {alan:.1f} m² (yukarı bakan kopya, M_Zemin)")


def aynalar(obj, govde_mat, cam_mat):
    mw = obj.matrix_world
    r = mw.to_3x3()
    me = obj.data
    adlar = [m.name if m else "" for m in me.materials]
    if AYNA_KAYNAK not in adlar:
        print("  ayna: kaynak malzeme yok")
        return
    ki = adlar.index(AYNA_KAYNAK)
    me.materials.append(govde_mat)
    gi = len(me.materials) - 1
    me.materials.append(cam_mat)
    ci = len(me.materials) - 1
    bm = bmesh.new()
    bm.from_mesh(me)
    kaynak = [f for f in bm.faces if f.material_index == ki]
    n_ada = n_cam = n_govde = 0
    for grup in adaciklar(bm, kaynak):
        ws = [mw @ v.co for f in grup for v in f.verts]
        xs = [abs(w.x) for w in ws]
        zs = [-w.y for w in ws]
        boy = max(max(w.x for w in ws) - min(w.x for w in ws), max(w.z for w in ws) - min(w.z for w in ws),
                  max(zs) - min(zs))
        kol = (min(xs) > AYNA_KOL["x"] and min(w.z for w in ws) > AYNA_KOL["y"] and min(zs) > AYNA_KOL["z"]
               and boy < AYNA_KOL["boy"])
        if not kol and (max(xs) < AYNA_X or min(zs) < AYNA_Z or boy > AYNA_BOY):
            continue
        n_ada += 1
        for f in grup:
            n = (r @ f.normal).normalized()
            # arkaya (Unity −z = Blender +y) bakan düz yüz ayna camı
            if n.y > 0.75:
                f.material_index = ci
                n_cam += 1
            else:
                f.material_index = gi
                n_govde += 1
    bm.to_mesh(me)
    bm.free()
    print(f"  ayna: {n_ada} adacık, {n_cam} cam yüzü, {n_govde} gövde yüzü")


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    ap = argparse.ArgumentParser()
    ap.add_argument("--fbx", required=True)
    ap.add_argument("--out", default=None)
    args = ap.parse_args(argv)
    klasor = os.path.dirname(os.path.abspath(args.fbx))

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=args.fbx, axis_forward="-Z", axis_up="Y", bake_space_transform=True)
    if bpy.data.materials.get("M_Zemin") is not None:
        sys.exit("FBX'e zaten uygulanmış (M_Zemin var)")
    govde = bpy.data.objects["Govde"]
    doku = zemin_dokusu(os.path.join(klasor, "Textures", "zemin.png"))
    zemin(govde, malzeme("M_Zemin", (1, 1, 1), doku))
    aynalar(govde, malzeme("M_AynaGovde", (0.035, 0.035, 0.038), parlaklik=0.35),
            malzeme("M_AynaCam", (0.16, 0.17, 0.19), parlaklik=0.95))
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.fbx(filepath=args.out or args.fbx, use_selection=True,
                             apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                             bake_space_transform=True, path_mode="STRIP", object_types={"MESH", "EMPTY"},
                             mesh_smooth_type="FACE")


if __name__ == "__main__":
    main()
