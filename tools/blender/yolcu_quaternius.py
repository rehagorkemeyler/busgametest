"""
Quaternius Ultimate Modular Men/Women (CC0; tools/kaynak/karakterler, ICERIK.md) → oyunun yolcu karakterleri (Y20).

Her cinsiyet için paketin "Separate Skeletal Meshes and Animations" klasörü kullanılır: kıyafet parçaları (Head, Body, Legs, Feet) ve
ortak Animations.fbx aynı iskeleti (79 kemik) paylaşır. Çıktı (Unity Generic rig; aynı cinsiyetin bütün karakterleri aynı klipleri oynar):
  Yolcu_<E|K>_<Kiyafet>.fbx     iskelet + tek mesh (parçalar birleşik), tek malzeme M_Yolcu (ortak renk paleti dokusu)
  Yolcu_<E|K>_Animasyon.fbx     iskelet + klipler: Bekle (Idle_Neutral), Yuru (Walk), Otur, Tutun
  Textures/yolcu_palet.png      bütün karakterlerin renkleri (paket düz renk malzeme kullanıyor; her malzeme bir hücre)
Pakette oturma animasyonu yok: Otur ve Tutun pozları burada üretilir (Idle_Neutral'ın ilk karesinden, kemik yönleri dünya uzayında
verilerek). İskeletin ayakları IK için köke bağlı (Foot.L/R, Root'un çocuğu): oturuşta ayak kemikleri dizin altına ayrıca taşınır.
Oturuşta kalça (UpperLeg başı) koltuk yüksekliğinin ~8 cm üstünde ve karakterin kökünden OTUR_GERI kadar geride, ayak tabanları
kök yüksekliğinde: Unity'de oturan yolcunun kökü koltuk noktasına (minderin önü-ortası, zemin seviyesi) konur (OtobusKoltuklari).
Karakterler Unity'de OLCEK ile küçültülür (YolcuKurucu); oturuş ölçülerinin hepsi ölçekten sonraki (oyundaki) değerlerdir.

Kullanım:
    Blender -b --factory-startup --python yolcu_quaternius.py -- --kaynak tools/kaynak/karakterler \
        --out AnkaraBusSimulator/Assets/_Project/Passengers/Quaternius [--render önizleme_klasörü]
"""
import argparse
import math
import os
import sys

import bpy  # noqa: E402
import bmesh  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402

# (klasör, ad) — şehirde yolcu olabilecek kıyafetler (kral, zırh, uzay giysisi, cadı vb. dışarıda)
KIYAFETLER = {
    "erkek": [("Suit", "Suit"), ("Casual_Hoodie", "Casual"), ("Casual_2", "Casual2"), ("Worker", "Worker"),
              ("Farmer", "Farmer"), ("Beach", "Beach"), ("Adventurer", "Adventurer"), ("Punk", "Punk")],
    "kadin": [("Casual", "Casual"), ("Formal", "Formal"), ("Suit", "Suit"), ("Worker", "Worker"),
              ("Adventurer", "Adventurer"), ("Punk", "Punk")],
}
PARCALAR = ("Head", "Body", "Legs", "Feet")
KLIPLER = {"Idle_Neutral": "Bekle", "Walk": "Yuru"}
KOLTUK_Y = 0.45            # oturuşta uyluk altının yüksekliği (m, karakter kökünden)
OTUR_GERI = 0.10           # kalça eklemi kökün bu kadar gerisinde (+y Blender = karakterin arkası)
# Unity'de içe aktarma ölçeği (YolcuKurucu.Olcek ile aynı olmalı): paketin karakterleri ~1,85 m, yolcular E ~1,72 / K ~1,62 m.
# Otur pozu bu ölçeği bilerek kurulur: oyunda (ölçekten sonra) kalça yine koltuk + 8 cm'de, tabanlar zeminde.
OLCEK = {"E": 0.93, "K": 0.89}
PALET_HUCRE, PALET_N = 4, 16   # 16 × 16 hücre, 64 px


def eylemi_bul(ad):
    for a in bpy.data.actions:
        if a.name.split("|")[-1] == ad:
            return a
    return None


def yon_ver(arm, ad, hedef, kok=None):
    """Kemiği (poz uzayında) başı yerinde kalarak hedef yöne (dünya = iskelet uzayı) çevirir; kok verilirse başı oraya taşınır."""
    pb = arm.pose.bones[ad]
    bpy.context.view_layer.update()
    m = pb.matrix.copy()
    bas = m.translation.copy() if kok is None else Vector(kok)
    simdiki = (pb.tail - pb.head).normalized()
    fark = simdiki.rotation_difference(Vector(hedef).normalized())
    yeni = fark.to_matrix().to_4x4() @ Matrix.Translation(-m.translation) @ m
    yeni.translation = bas
    pb.matrix = yeni
    bpy.context.view_layer.update()


def tasi(arm, ad, kok):
    pb = arm.pose.bones[ad]
    bpy.context.view_layer.update()
    m = pb.matrix.copy()
    m.translation = Vector(kok)
    pb.matrix = m
    bpy.context.view_layer.update()


def poz_eylemi(arm, ad, kur, bekle):
    """Bekle (Idle_Neutral) eyleminin ilk karesinden başlayıp kur(arm) ile kemikleri yerleştirir, 2 karelik sabit (döngü) eylem."""
    arm.animation_data.action = bekle
    bpy.context.scene.frame_set(1)
    bpy.context.view_layer.update()
    # mevcut pozu sabitle (eylemden bağımsız)
    matrisler = {pb.name: pb.matrix.copy() for pb in arm.pose.bones}
    yeni = bpy.data.actions.new(ad)
    arm.animation_data.action = yeni
    for pb in arm.pose.bones:
        pb.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()
    for pb in arm.pose.bones:          # ebeveynden çocuğa sırayla
        pb.matrix = matrisler[pb.name]
        bpy.context.view_layer.update()
    kur(arm)
    for kare in (1, 2):
        for pb in arm.pose.bones:
            pb.keyframe_insert("location", frame=kare)
            pb.keyframe_insert("rotation_quaternion", frame=kare)
            pb.keyframe_insert("scale", frame=kare)
    yeni.use_fake_user = True
    return yeni


def yeniden_hedefle(kaynak, hedef, eylemler):
    """Kaynak iskeletin eylemlerini hedef iskelete dünya uzayında kopyalar (Copy Transforms + görsel pişirme): iki iskeletin
    kemik adları aynı, dinlenme pozları farklı."""
    if hedef.animation_data is None:
        hedef.animation_data_create()
    for pb in hedef.pose.bones:
        if pb.name in kaynak.pose.bones:
            c = pb.constraints.new("COPY_TRANSFORMS")
            c.target = kaynak
            c.subtarget = pb.name
    bpy.ops.object.select_all(action="DESELECT")
    hedef.select_set(True)
    bpy.context.view_layer.objects.active = hedef
    bpy.ops.object.mode_set(mode="POSE")
    bpy.ops.pose.select_all(action="SELECT")
    sonuc = {}
    for ad, e in eylemler.items():
        kaynak.animation_data.action = e
        bas, son = (int(x) for x in e.frame_range)
        hedef.animation_data.action = None
        bpy.ops.nla.bake(frame_start=bas, frame_end=son, only_selected=False, visual_keying=True,
                         clear_constraints=False, use_current_action=False, bake_types={"POSE"})
        yeni = hedef.animation_data.action
        yeni.name = ad
        yeni.use_fake_user = True
        sonuc[ad] = yeni
    bpy.ops.object.mode_set(mode="OBJECT")
    for pb in hedef.pose.bones:
        for c in list(pb.constraints):
            pb.constraints.remove(c)
    print("  yeniden hedeflendi:", {k: tuple(int(x) for x in v.frame_range) for k, v in sonuc.items()})
    return sonuc


def otur_kur(arm, olcek):
    p = arm.pose.bones
    # ayak bileğinin ayaktaki yüksekliği (taban zeminde): oturuşta da bilek bu yükseklikte olmalı
    bilek_y = (p["Foot.L"].head.z + p["Foot.R"].head.z) / 2
    # kalça (iki UpperLeg başı ortası) hedef: koltuk + 8 cm, OTUR_GERI kadar geride (oyundaki ölçekten önceki birimle)
    sol, sag = p["UpperLeg.L"].head.copy(), p["UpperLeg.R"].head.copy()
    orta = (sol + sag) / 2
    hedef = Vector((orta.x, OTUR_GERI / olcek, (KOLTUK_Y + 0.08) / olcek))
    fark = hedef - orta
    for ad in ("Body",):
        tasi(arm, ad, p[ad].matrix.translation + fark)
    # uyluklar öne, hafif aşağı ve dışa; baldırlar aşağı, hafif öne; ayaklar dizin altında, düz
    for yan, isaret in (("L", 1), ("R", -1)):
        yon_ver(arm, f"UpperLeg.{yan}", (0.06 * isaret, -1.0, -0.12))
        diz = p[f"UpperLeg.{yan}"].tail.copy()
        # baldır: bilek ayaktaki yüksekliğe insin (taban zeminde); yetmiyorsa dik (ayak sarkar)
        boy = p[f"LowerLeg.{yan}"].length
        dikey = min(1.0, max(0.0, (diz.z - bilek_y) / boy))
        yon_ver(arm, f"LowerLeg.{yan}", (0.0, -math.sqrt(1.0 - dikey * dikey), -dikey))
        bilek = p[f"LowerLeg.{yan}"].tail.copy()
        tasi(arm, f"Foot.{yan}", bilek)
        yon_ver(arm, f"Foot.{yan}", (0.05 * isaret, -1.0, -0.05))
        # kollar: üst kol aşağı-öne, ön kol öne (eller uyluk üstünde)
        yon_ver(arm, f"UpperArm.{yan}", (0.10 * isaret, -0.22, -1.0))
        yon_ver(arm, f"LowerArm.{yan}", (-0.12 * isaret, -1.0, -0.6))
    # gövde hafif geriye yaslı
    yon_ver(arm, "Abdomen", (0, 0.08, 1))


def tutun_kur(arm):
    # ayakta, sağ el tavan borusunda (~1,85 m), sol kol yanda
    yon_ver(arm, "UpperArm.R", (-0.15, -0.25, 1.0))
    yon_ver(arm, "LowerArm.R", (-0.05, -0.15, 1.0))


def palet_hucresi(renkler, renk):
    anahtar = tuple(round(c, 3) for c in renk[:3])
    if anahtar not in renkler:
        if len(renkler) >= PALET_N * PALET_N:
            raise RuntimeError("palet doldu")
        renkler[anahtar] = len(renkler)
    i = renkler[anahtar]
    u = ((i % PALET_N) + 0.5) / PALET_N
    v = 1 - ((i // PALET_N) + 0.5) / PALET_N
    return u, v


def malzeme_rengi(m):
    if m is None:
        return (0.5, 0.5, 0.5)
    if m.use_nodes:
        b = next((n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
        if b is not None:
            return tuple(b.inputs["Base Color"].default_value)[:3]
    return tuple(m.diffuse_color)[:3]


def karakter(arm, klasor, ad, renkler, palet_mat):
    once = set(bpy.data.objects)
    for parca in PARCALAR:
        yol = os.path.join(klasor, f"{ad}_{parca}.fbx")
        if os.path.exists(yol):
            bpy.ops.import_scene.fbx(filepath=yol)
    yeniler = [o for o in bpy.data.objects if o not in once]
    meshler = [o for o in yeniler if o.type == "MESH"]
    for o in yeniler:
        if o.type == "ARMATURE":
            bpy.data.objects.remove(o, do_unlink=True)
    for o in meshler:
        mw = o.matrix_world.copy()
        o.parent = None
        o.matrix_world = mw
        # her yüze kendi malzemesinin palet hücresi (UV), sonra tek malzeme
        bm = bmesh.new()
        bm.from_mesh(o.data)
        uv = bm.loops.layers.uv.active or bm.loops.layers.uv.new("UVMap")
        for f in bm.faces:
            m = o.data.materials[f.material_index] if f.material_index < len(o.data.materials) else None
            u, v = palet_hucresi(renkler, malzeme_rengi(m))
            for lo in f.loops:
                lo[uv].uv = (u, v)
            f.material_index = 0
        bm.to_mesh(o.data)
        bm.free()
        o.data.materials.clear()
        o.data.materials.append(palet_mat)
        for mod in list(o.modifiers):
            if mod.type == "ARMATURE":
                o.modifiers.remove(mod)
    bpy.ops.object.select_all(action="DESELECT")
    for o in meshler:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshler[0]
    bpy.ops.object.join()
    mesh = bpy.context.view_layer.objects.active
    mesh.name = "Govde"
    mesh.data.name = "Govde"
    mesh.parent = arm
    mesh.matrix_parent_inverse = arm.matrix_world.inverted()
    mod = mesh.modifiers.new("Iskelet", "ARMATURE")
    mod.object = arm
    eksik = [g.name for g in mesh.vertex_groups if g.name not in arm.data.bones]
    if eksik:
        print("  uyarı: iskelette olmayan köşe grupları", eksik[:8])
    print(f"  {ad}: {len(mesh.data.polygons)} yüz, {len(mesh.vertex_groups)} köşe grubu")
    return mesh


def disari(dosya, nesneler, animasyon):
    bpy.ops.object.select_all(action="DESELECT")
    for o in nesneler:
        o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=dosya, use_selection=True, apply_scale_options="FBX_SCALE_ALL",
                             axis_forward="-Z", axis_up="Y", object_types={"ARMATURE", "MESH"},
                             add_leaf_bones=False, bake_anim=animasyon, bake_anim_use_all_actions=animasyon,
                             bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True,
                             bake_anim_simplify_factor=0.0, path_mode="STRIP", mesh_smooth_type="FACE")


def render_et(arm, mesh, eylem, dosya):
    sc = bpy.context.scene
    arm.animation_data.action = eylem
    sc.frame_set(1)
    cam = bpy.data.objects.get("cam") or bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    if cam.name not in sc.collection.objects:
        sc.collection.objects.link(cam)
    sc.camera = cam
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 2.2
    sc.render.resolution_x, sc.render.resolution_y = 500, 500
    sc.render.engine = "CYCLES"
    sc.cycles.samples = 8
    if sc.world is None:
        sc.world = bpy.data.worlds.new("w")
    sc.world.color = (0.6, 0.6, 0.6)
    gorus = []
    for ad, konum in (("yan", (3.0, 0.0, 0.9)), ("on", (0.0, -3.0, 0.9))):
        cam.location = konum
        cam.rotation_euler = (Vector((0, 0, 0.9)) - Vector(konum)).to_track_quat("-Z", "Y").to_euler()
        sc.render.filepath = f"{dosya}_{ad}.png"
        bpy.ops.render.render(write_still=True)
        gorus.append(sc.render.filepath)
    return gorus


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    ap = argparse.ArgumentParser()
    ap.add_argument("--kaynak", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--render", default=None)
    ap.add_argument("--yalniz", default=None, help="test: yalnız bu kıyafet (ör. Suit)")
    args = ap.parse_args(argv)
    os.makedirs(os.path.join(args.out, "Textures"), exist_ok=True)
    renkler = {}

    for cins, kod in (("erkek", "E"), ("kadin", "K")):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        kok = os.path.join(args.kaynak, cins, "Separate Skeletal Meshes and Animations")
        # kaynak iskelet: Animations.fbx (dinlenme pozu T değil); hedef iskelet: kıyafet parçalarının T-poz iskeleti
        bpy.ops.import_scene.fbx(filepath=os.path.join(kok, "Animations.fbx"))
        kaynak = next(o for o in bpy.context.scene.objects if o.type == "ARMATURE")
        kaynak.name = "Kaynak"
        for o in [o for o in bpy.context.scene.objects if o.type != "ARMATURE"]:
            bpy.data.objects.remove(o, do_unlink=True)
        if kaynak.animation_data is None:
            kaynak.animation_data_create()
        eylemler = {}
        for kaynak_ad, ad in KLIPLER.items():
            e = eylemi_bul(kaynak_ad)
            e.name = "k_" + ad
            e.use_fake_user = True
            eylemler[ad] = e
        eylemler["Otur"] = poz_eylemi(kaynak, "k_Otur", lambda a: otur_kur(a, OLCEK[kod]), eylemler["Bekle"])
        eylemler["Tutun"] = poz_eylemi(kaynak, "k_Tutun", tutun_kur, eylemler["Bekle"])
        for e in list(bpy.data.actions):
            if e not in eylemler.values():
                bpy.data.actions.remove(e)

        ilk = KIYAFETLER[cins][0]
        once = set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=os.path.join(kok, ilk[0], f"{ilk[1]}_Body.fbx"))
        arm = next(o for o in bpy.data.objects if o not in once and o.type == "ARMATURE")
        for o in [o for o in bpy.data.objects if o not in once and o.type != "ARMATURE"]:
            bpy.data.objects.remove(o, do_unlink=True)
        arm.name = "Iskelet"
        hedef_eylemler = yeniden_hedefle(kaynak, arm, eylemler)
        bpy.data.objects.remove(kaynak, do_unlink=True)
        for e in list(eylemler.values()):
            bpy.data.actions.remove(e)
        eylemler = hedef_eylemler
        palet_mat = bpy.data.materials.new("M_Yolcu")
        # animasyon dosyası ilk karakterin mesh'iyle birlikte: mesh'siz FBX'te bağlama pozu (bind pose) yazılmıyor, içe aktarıcı
        # dinlenme pozunu ilk kareden tahmin ediyor (kollar karakter dosyasındakinden farklı çıkıyordu)
        ornek = karakter(arm, os.path.join(kok, ilk[0]), ilk[1], renkler, palet_mat)
        arm.animation_data.action = eylemler["Bekle"]
        disari(os.path.join(args.out, f"Yolcu_{kod}_Animasyon.fbx"), [arm, ornek], True)
        bpy.data.objects.remove(ornek, do_unlink=True)

        for klasor, ad in KIYAFETLER[cins]:
            if args.yalniz and ad != args.yalniz:
                continue
            mesh = karakter(arm, os.path.join(kok, klasor), ad, renkler, palet_mat)
            arm.animation_data.action = None
            for pb in arm.pose.bones:
                pb.matrix_basis = Matrix.Identity(4)
            disari(os.path.join(args.out, f"Yolcu_{kod}_{ad}.fbx"), [arm, mesh], False)
            if args.render:
                os.makedirs(args.render, exist_ok=True)
                for e in ("Bekle", "Otur", "Tutun"):
                    render_et(arm, mesh, eylemler[e], os.path.join(args.render, f"{kod}_{ad}_{e}"))
                arm.animation_data.action = None
            bpy.data.objects.remove(mesh, do_unlink=True)

    # palet dokusu
    from PIL import Image
    n = PALET_N * PALET_HUCRE
    img = Image.new("RGB", (n, n), (128, 128, 128))
    px = img.load()
    for renk, i in renkler.items():
        srgb = tuple(int(round(255 * (c * 12.92 if c <= 0.0031308 else 1.055 * c ** (1 / 2.4) - 0.055))) for c in renk)
        x0, y0 = (i % PALET_N) * PALET_HUCRE, (i // PALET_N) * PALET_HUCRE
        for y in range(y0, y0 + PALET_HUCRE):
            for x in range(x0, x0 + PALET_HUCRE):
                px[x, y] = srgb
    img.save(os.path.join(args.out, "Textures", "yolcu_palet.png"))
    print(f"palet: {len(renkler)} renk")


if __name__ == "__main__":
    main()
