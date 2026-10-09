"""
Mercedes Conecto (MB_Conecto_G) iç mekân ve ön/arka yüz kaplaması: conecto_donustur.py'nin ürettiği FBX'i açar,
tek renk "M_Ic" yüzlerini (ve gövde içinde kalan M_Govde yüzlerini) zemin, koltuk (minder ve kabuk), direk, iç duvar ve
tavana ayırır, dokularını çizer ve FBX'i yerinde yazar. Kaynak GLB'nin dokuları yoktu; ayrım geometriden yapılır
(bkz. siniflandir). Dış kaplamanın (M_caroserie) içe bakan yüzleri (yan duvarın iç yüzü) iç duvara alınır.
Ön ve arka yüzde dışarıdan görünen yüzler (ışın testi) "M_onarka" olur: düzlemsel UV, doku tools/kaplama_conecto.py'nin
onarka*.png'si (ön cam, hat tabelası, plaka, stop lambaları; kaplamayla birlikte BusLivery değiştirir).

Kullanım (her iki FBX için; conecto_donustur.py'den sonra):
    Blender -b --factory-startup --python conecto_ic.py -- --fbx <MB_Conecto_G.fbx> [--doku <Textures klasörü>] [--render <önek>]
Dokular: ic_zemin.png (kaymaz zemin), ic_koltuk.png (koltuk kumaşı). Unity'de OtobusKurucu yeni malzemeleri Materials/'a çıkarır.
"""
import argparse
import math
import os
import sys

import bpy  # noqa: E402
import bmesh  # noqa: E402
import numpy as np  # noqa: E402
from mathutils import Vector  # noqa: E402

RENKLER = {
    "M_Ic": (0.60, 0.61, 0.63),
    "M_IcDuvar": (0.80, 0.81, 0.82),
    "M_IcTavan": (0.90, 0.90, 0.88),
    "M_Direk": (0.93, 0.70, 0.06),       # sarı tutunma boruları
    "M_KoltukKabuk": (0.22, 0.24, 0.28),  # koyu gri plastik kabuk
    "M_Zemin": (1.0, 1.0, 1.0),
    "M_Koltuk": (1.0, 1.0, 1.0),
    "M_onarka": (1.0, 1.0, 1.0),
}
DOKULAR = {"M_Zemin": "ic_zemin.png", "M_Koltuk": "ic_koltuk.png", "M_onarka": "onarka.png"}
# ön/arka doku yerleşimi (kaplama_conecto.py ile aynı): u 0–0,5 ön, 0,5–1 arka; x −1,32 … 1,32, z 0 … 3,3
OA_X, OA_H = 1.32, 3.3
ONARKA_KAYNAK = ("M_Govde", "M_On_Cam", "M_Ic", "M_Tavan", "M_IcDuvar", "M_Yazi")
ZEMIN_M = 1.0    # zemin dokusu bir tekrarda 1 m
KOLTUK_M = 0.25  # kumaş deseni bir tekrarda 25 cm


def zemin_dokusu(yol, n=512):
    """Koyu gri kaymaz zemin (Altro benzeri): ince açık ve koyu benekler."""
    rng = np.random.default_rng(7)
    img = np.full((n, n, 3), (62, 64, 68), np.float32)
    img += rng.normal(0, 3.0, (n, n, 1))
    for renk, adet in (((150, 152, 156), 2600), ((30, 31, 33), 2600), ((105, 98, 80), 600)):
        ys, xs = rng.integers(0, n, adet), rng.integers(0, n, adet)
        img[ys, xs] = renk
        img[ys, (xs + 1) % n] = renk
    kaydet(yol, img)


def koltuk_dokusu(yol, n=256):
    """Koltuk kumaşı: lacivert zemin üstünde küçük renkli kırık çizgiler (belediye otobüsü deseni); dikişsiz tekrar."""
    rng = np.random.default_rng(3)
    img = np.full((n, n, 3), (24, 34, 78), np.float32)
    img += rng.normal(0, 6.0, (n, n, 1))  # dokuma
    yy, xx = np.mgrid[0:n, 0:n]
    img[(xx + yy) % 4 == 0] *= 0.85
    renkler = [(210, 40, 45), (240, 190, 40), (70, 150, 220), (235, 235, 235)]
    for i in range(140):
        r = renkler[i % len(renkler)]
        x0, y0 = rng.integers(0, n, 2)
        uz = rng.integers(6, 16)
        yon = rng.integers(0, 2)
        for t in range(uz):
            x = (x0 + (t if yon == 0 else t // 3)) % n
            y = (y0 + (t // 3 if yon == 0 else t)) % n
            img[y, x] = r
            img[(y + 1) % n, x] = r
    kaydet(yol, img)


def kaydet(yol, img):
    from PIL import Image
    Image.fromarray(np.clip(img, 0, 255).astype(np.uint8)).save(yol)


def malzeme(ad, doku_klasoru):
    m = bpy.data.materials.get(ad) or bpy.data.materials.new(ad)
    m.use_nodes = True
    bsdf = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Base Color"].default_value = (*RENKLER[ad], 1.0)
    if ad in DOKULAR and not any(n.type == "TEX_IMAGE" for n in m.node_tree.nodes):
        node = m.node_tree.nodes.new("ShaderNodeTexImage")
        yol = os.path.join(doku_klasoru, DOKULAR[ad])
        node.image = bpy.data.images.load(yol, check_existing=True) if os.path.exists(yol) else \
            bpy.data.images.new(DOKULAR[ad], 4, 4)
        node.image.filepath = yol
        m.node_tree.links.new(node.outputs["Color"], bsdf.inputs["Base Color"])
    return m


def adalar(bm, uygun):
    """Kenar komşuluğuyla bağlı yüz adaları (yalnızca uygun yüzler)."""
    goruldu, sonuc = set(), []
    for f in bm.faces:
        if f.index in goruldu or not uygun(f):
            continue
        yigin, ada = [f], []
        goruldu.add(f.index)
        while yigin:
            g = yigin.pop()
            ada.append(g)
            for e in g.edges:
                for h in e.link_faces:
                    if h.index not in goruldu and uygun(h):
                        goruldu.add(h.index)
                        yigin.append(h)
        sonuc.append(ada)
    return sonuc


def sinirlar(ada, mw):
    pts = [mw @ v.co for f in ada for v in f.verts]
    mn = Vector([min(p[i] for p in pts) for i in range(3)])
    mx = Vector([max(p[i] for p in pts) for i in range(3)])
    return mn, mx


def siniflandir(o, mats):
    """Yüzlere yeni malzeme dizini verir. Koordinatlar Blender dünyası (z yukarı, ön −y, x = Unity'nin −x'i)."""
    me = o.data
    mw = o.matrix_world
    rot = mw.to_3x3()
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.faces.ensure_lookup_table()
    adlar = [m.name for m in me.materials]
    if "M_Ic" not in adlar:
        bm.free()
        return {}
    ic = adlar.index("M_Ic")
    kar = adlar.index("M_caroserie") if "M_caroserie" in adlar else -1
    govde = adlar.index("M_Govde") if "M_Govde" in adlar else -1
    yeni = {}

    # aday: M_Ic ve gövde içindeki M_Govde yüzleri (M_Govde kaplama rengine boyanır; içeride kalırsa zemin ve
    # bölmeler kırmızı/mavi olur). İç bölge: yan duvarların, tavanın, ön ve arka yüzün içi
    y_on = min((mw @ v.co).y for v in bm.verts) + 0.62
    y_arka = max((mw @ v.co).y for v in bm.verts) - 0.25
    aday = set()
    for f in bm.faces:
        if f.material_index == ic:
            aday.add(f.index)
        elif f.material_index == govde:
            c = mw @ f.calc_center_median()
            if abs(c.x) < 1.15 and 0.35 < c.z < 2.9 and y_on < c.y < y_arka:
                aday.add(f.index)
                yeni[f.index] = "M_Ic"

    for ada in adalar(bm, lambda f: f.index in aday):
        mn, mx = sinirlar(ada, mw)
        d = mx - mn
        alan = sum(f.calc_area() for f in ada)
        en_uzun = max(d)
        if len(ada) >= 10 and en_uzun > 0.25 and alan / en_uzun < 0.22 and min(d) > 0.012:
            hedef = "M_Direk"  # boru: alan, boyuna göre çok küçük
        elif 0.3 < d.x < 0.95 and 0.3 < d.y < 0.95 and 0.35 < d.z < 0.75 and mn.z > 0.6 and len(ada) >= 12:
            hedef = "M_KoltukKabuk" if alan > 0.9 else "M_Koltuk"  # tek/çift koltuk: kabuk ~1,6 m², minder ~0,35–0,6 m²
        else:
            hedef = None
        for f in ada:
            c = mw @ f.calc_center_median()
            n = (rot @ f.normal).normalized()
            if hedef is not None:
                yeni[f.index] = hedef
            elif n.z > 0.8 and c.z < 0.8:
                yeni[f.index] = "M_Zemin"
            elif n.z < -0.5 and c.z > 2.2:
                yeni[f.index] = "M_IcTavan"
            elif abs(n.x) > 0.6 and abs(c.x) > 0.9:
                yeni[f.index] = "M_IcDuvar"
    if kar >= 0:
        for f in bm.faces:
            if f.material_index != kar:
                continue
            c = mw @ f.calc_center_median()
            n = rot @ f.normal
            if n.x * c.x < 0 and abs(c.x) < 1.24:  # içe bakan yan yüz: iç duvar
                yeni[f.index] = "M_IcDuvar"

    # ön/arka yüz: uçtan 0,8 m içinde, dışa bakan ve önünde/arkasında kendi gövdesinden hiçbir şey olmayan yüzler
    from mathutils.bvhtree import BVHTree
    dunya = bm.copy()
    dunya.transform(mw)
    dunya.faces.ensure_lookup_table()
    agac = BVHTree.FromBMesh(dunya)
    on = o.name == "Govde_On"
    yon = -1.0 if on else 1.0
    uc = min(v.co.y for v in dunya.verts) if on else max(v.co.y for v in dunya.verts)
    kaynak = {adlar.index(a) for a in ONARKA_KAYNAK if a in adlar}
    for f in dunya.faces:
        ad_simdi = yeni.get(f.index)
        if f.material_index not in kaynak and ad_simdi is None:
            continue
        if ad_simdi in ("M_Direk", "M_Koltuk", "M_KoltukKabuk", "M_Zemin"):
            continue
        c = f.calc_center_median()
        if abs(c.y - uc) > 0.8 or f.normal.y * yon < 0.3 or c.z < 0.05:
            continue
        isin = Vector((0.0, yon, 0.0))
        vurus = agac.ray_cast(c + isin * 0.003, isin, 20.0)
        if vurus[0] is None:
            yeni[f.index] = "M_onarka"
    dunya.free()

    # malzemeleri ekle, dizinleri ve UV'leri yaz
    for ad in sorted(set(yeni.values())):
        if ad not in adlar:
            me.materials.append(mats[ad])
            adlar.append(ad)
    uv = bm.loops.layers.uv.active
    for i, ad in yeni.items():
        f = bm.faces[i]
        f.material_index = adlar.index(ad)
        if ad not in DOKULAR or uv is None:
            continue
        n = rot @ f.normal
        for lo in f.loops:
            p = mw @ lo.vert.co
            if ad == "M_Zemin":
                lo[uv].uv = (p.x / ZEMIN_M, p.y / ZEMIN_M)
            elif ad == "M_onarka":
                # önden bakınca görüntü sağı +x (Blender), arkadan bakınca −x
                xg = p.x if o.name == "Govde_On" else -p.x
                u = min(max((xg + OA_X) / (2 * OA_X), 0.0), 1.0) * 0.5 + (0.0 if o.name == "Govde_On" else 0.5)
                lo[uv].uv = (u, min(max(p.z / OA_H, 0.0), 1.0))
            else:  # kutu izdüşümü
                ax = max(range(3), key=lambda k: abs(n[k]))
                a, b = [(1, 2), (0, 2), (0, 1)][ax]
                lo[uv].uv = (p[a] / KOLTUK_M, p[b] / KOLTUK_M)
    bm.to_mesh(me)
    bm.free()
    sayim = {}
    for ad in yeni.values():
        sayim[ad] = sayim.get(ad, 0) + 1
    return sayim


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    ap = argparse.ArgumentParser()
    ap.add_argument("--fbx", required=True)
    ap.add_argument("--doku", default=None, help="dokuların yazılacağı klasör (varsayılan: FBX'in yanındaki Textures)")
    ap.add_argument("--out", default=None, help="çıktı FBX (varsayılan: yerinde)")
    args = ap.parse_args(argv)
    doku = args.doku or os.path.join(os.path.dirname(os.path.abspath(args.fbx)), "Textures")
    os.makedirs(doku, exist_ok=True)
    zemin_dokusu(os.path.join(doku, DOKULAR["M_Zemin"]))
    koltuk_dokusu(os.path.join(doku, DOKULAR["M_Koltuk"]))

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=args.fbx, axis_forward="-Z", axis_up="Y", bake_space_transform=True)
    if any(m.name in DOKULAR or m.name == "M_Direk" for m in bpy.data.materials):
        sys.exit("FBX'te iç malzemeler zaten var: önce conecto_donustur.py ile yeniden üretin")
    mats = {ad: malzeme(ad, doku) for ad in RENKLER}
    for o in bpy.context.scene.objects:
        if o.type == "MESH" and o.name.startswith("Govde_"):
            print(o.name, siniflandir(o, mats))
    # dışa aktarma conecto_donustur.py ile aynı
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.fbx(filepath=args.out or args.fbx, use_selection=True,
                             apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                             bake_space_transform=True, path_mode="STRIP", object_types={"MESH", "EMPTY"},
                             mesh_smooth_type="FACE")


if __name__ == "__main__":
    main()
