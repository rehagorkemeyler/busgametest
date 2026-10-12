"""
Mercedes Conecto (MB_Conecto_G): sağdaki 4 kapının kanatlarını gövdeden ayırır. Kaynak modelde kapılar gövde mesh'inin parçasıydı
(dış yüzü M_caroserie, kapı resmi kaplamaya çizili); oyunda açılıp kapanabilsinler diye her kapı iki kanada bölünür:
Kapi_1_1, Kapi_1_2 … Kapi_4_2 (N_1 öndeki, N_2 arkadaki kanat). Kanatlar gövdeler gibi kökün çocuğu (kapı 3–4 oyunda
arka gövdeyi BusDoor.Takip ile izler), pivotu kanat dikdörtgeninin ortası (duvar hizasında; iki modelde aynı). Kanat yüzleri UV'lerini korur: kaplamadaki kapı resmi kanatla birlikte kayar.

Yöntem: gövde, kapı dikdörtgeninin kenarlarından (ön/arka y, üst z, alt z) ve kapının ortasından kesilir (bisect); kapı
dikdörtgeninin içinde, sağ yan duvarın kalınlığında (x < −1,06) kalan yüzler (zemin, borular, koltuklar hariç) kanat olur.
Oyunda: OtobusKurucu (Conecto) kanatlara "Plug" hareketi verir (önce dışarı, sonra gövde boyunca kayar; BusDoor).

Kullanım (iki FBX için, conecto_ic.py'den sonra; FBX'i yerinde yazar, ikinci kez çalıştırılmaz):
    Blender -b --factory-startup --python conecto_kapilar.py -- --fbx MB_Conecto_G.fbx
"""
import argparse
import sys

import bpy  # noqa: E402
import bmesh  # noqa: E402
from mathutils import Vector  # noqa: E402

# kapılar Unity z aralıkları (conecto_donustur.KAPILAR kaynak y + 2,87); Blender y = −z, sağ taraf Blender −x
KAPILAR_UNITY_Z = [(7.15, 8.40), (1.27, 2.51), (-4.84, -3.61), (-8.05, -6.80)]
KAPI_ALT, KAPI_UST = 0.30, 2.68
DUVAR_X = -1.06                 # bundan dışarıdaki yüzler (sağ yan duvarın kalınlığı) kanada gider
HARIC = ("M_Zemin", "M_Direk", "M_Koltuk", "M_IcTavan", "M_Tavan")


def kes(bm, nokta, normal, alan):
    geom = list(alan) + list({e for f in alan for e in f.edges}) + list({v for f in alan for v in f.verts})
    bmesh.ops.bisect_plane(bm, geom=geom, plane_co=nokta, plane_no=normal, clear_inner=False, clear_outer=False)


def kapilari_ayir(govde, kapilar):
    """kapilar: [(no, y_bas, y_son)] Blender dünya y'si (ön −y). Kanat objelerini döndürür."""
    mw = govde.matrix_world
    loc = mw.translation
    me = govde.data
    adlar = [m.name for m in me.materials]
    bm = bmesh.new()
    bm.from_mesh(me)

    def bolge(y0, y1):
        out = []
        for f in bm.faces:
            ys = [(mw @ v.co).y for v in f.verts]
            xs = [(mw @ v.co).x for v in f.verts]
            zs = [(mw @ v.co).z for v in f.verts]
            if min(xs) < DUVAR_X + 0.02 and max(ys) > y0 - 0.02 and min(ys) < y1 + 0.02 and min(zs) < KAPI_UST + 0.02:
                out.append(f)
        return out

    # gövdeyi kapı kenarlarından ve kapının ortasından kes (büyük üçgenler kapıyı aşıyordu)
    for no, y0, y1 in kapilar:
        for y in (y0, (y0 + y1) / 2, y1):
            kes(bm, Vector((0, y - loc.y, 0)), Vector((0, 1, 0)), bolge(y0, y1))
        for z in (KAPI_ALT, KAPI_UST):
            kes(bm, Vector((0, 0, z - loc.z)), Vector((0, 0, 1)), bolge(y0, y1))

    bm.faces.ensure_lookup_table()
    secim = {}
    for f in bm.faces:
        if adlar[f.material_index].startswith(HARIC):
            continue
        c = mw @ f.calc_center_median()
        if c.x > DUVAR_X or not (KAPI_ALT + 0.005 < c.z < KAPI_UST - 0.005):
            continue
        for no, y0, y1 in kapilar:
            if y0 + 0.005 < c.y < y1 - 0.005:
                kanat = 1 if c.y < (y0 + y1) / 2 else 2   # ön −y: küçük y öndeki kanat
                secim.setdefault((no, kanat), []).append(f)
                break

    uv_katmanlari = [(bm.loops.layers.uv[k.name], k.name) for k in me.uv_layers]
    kanatlar = []
    for (no, kanat), yuzler in sorted(secim.items()):
        ad = f"Kapi_{no}_{kanat}"
        yeni = bmesh.new()
        yeni_uv = [yeni.loops.layers.uv.new(ad_) for _, ad_ in uv_katmanlari]
        esle = {}
        # pivot sabit noktada (kanadın dikdörtgeninin ortası, duvar hizası): mobil ve tam kalite modelde aynı olmalı,
        # BusKaliteModeli yalnız mesh'i değiştirir
        y0, y1 = next((a, b) for n_, a, b in kapilar if n_ == no)
        ym = (y0 + y1) / 2
        merkez = Vector((DUVAR_X, (y0 + ym) / 2 if kanat == 1 else (ym + y1) / 2, (KAPI_ALT + KAPI_UST) / 2))
        for f in yuzler:
            vs = []
            for v in f.verts:
                if v not in esle:
                    esle[v] = yeni.verts.new(mw @ v.co - merkez)
                vs.append(esle[v])
            try:
                nf = yeni.faces.new(vs)
            except ValueError:
                continue
            nf.material_index = f.material_index
            nf.smooth = f.smooth
            for (kaynak, _), hedef in zip(uv_katmanlari, yeni_uv):
                for lo, ls in zip(nf.loops, f.loops):
                    lo[hedef].uv = ls[kaynak].uv
        kmesh = bpy.data.meshes.new(ad)
        yeni.to_mesh(kmesh)
        yeni.free()
        for m in me.materials:
            kmesh.materials.append(m)
        obj = bpy.data.objects.new(ad, kmesh)
        bpy.context.scene.collection.objects.link(obj)
        # gövdeyle aynı ebeveyn (kök boş obje): mesh'e bağlı çocuklar FBX'te bake_space_transform ile 90° dönüyordu.
        # Arka gövdedeki kanatları oyunda BusDoor.Takip arka gövdeye bağlar (KorukluOtobus).
        obj.parent = govde.parent
        obj.location = (govde.parent.matrix_world.inverted() @ merkez) if govde.parent else merkez
        kanatlar.append(obj)
        print(f"{ad}: {len(kmesh.polygons)} yüz, merkez Unity ({-merkez.x:.2f}, {merkez.z:.2f}, {-merkez.y:.2f})")
        bmesh.ops.delete(bm, geom=yuzler, context="FACES")
    bm.to_mesh(me)
    bm.free()
    me.update()
    return kanatlar


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    ap = argparse.ArgumentParser()
    ap.add_argument("--fbx", required=True)
    ap.add_argument("--out", default=None)
    args = ap.parse_args(argv)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=args.fbx, axis_forward="-Z", axis_up="Y", bake_space_transform=True)
    if any(o.name.startswith("Kapi_") for o in bpy.context.scene.objects):
        sys.exit("FBX'te kapı kanatları zaten var: önce conecto_donustur.py + conecto_ic.py ile yeniden üretin")
    for ad, nolar in (("Govde_On", (1, 2)), ("Govde_Arka", (3, 4))):
        kapilar = [(no, -KAPILAR_UNITY_Z[no - 1][1], -KAPILAR_UNITY_Z[no - 1][0]) for no in nolar]
        kapilari_ayir(bpy.data.objects[ad], kapilar)
    bpy.context.view_layer.update()
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.fbx(filepath=args.out or args.fbx, use_selection=True,
                             apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                             bake_space_transform=True, path_mode="STRIP", object_types={"MESH", "EMPTY"},
                             mesh_smooth_type="FACE")


if __name__ == "__main__":
    main()
