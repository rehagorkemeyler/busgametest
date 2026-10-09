"""
Caio Millennium II (Mercedes-Benz O500M, "piso baixo central") → Unity'ye hazır FBX.
Model: Marcos Elias Picão (MEP), tekerlekler Victor Ortega, silecek/hız göstergesi Luiz Felipe Bonamigo,
hız göstergesi dokusu Dimitrius Caio Vespasiano. Lisans CC-BY 3.0 (docs/KREDILER.md).

Yaptıkları:
  - .blend'i açar (içindeki betikler çalıştırılmaz: Blender'ı --disable-autoexec ile başlatın),
    değiştiricileri uygular, parçaları bmc_donustur ile aynı adlara toplar:
    Govde, Teker_OnSol/OnSag/ArkaSol/ArkaSag, Kapi_1_1..Kapi_2_2 (sağ kapılar), Direksiyon, Lamba_*.
  - Sol taraftaki BRT kapıları gövdeye katılır (kapalı kalır). İç cam katmanı (vidrosInt*) atılır.
  - Dokuları PNG'ye çevirir (BMP/TGA → PNG, en çok 2048 px); gövde dokusu "caroserie" adını alır (BusLivery),
    beyaz şablondan (base.bmp) EGO kırmızı, EGO mavi ve Özel Halk kaplamalarını boyar.
  - İçe bakan dış yüzleri düzeltir (bmc_donustur.iki_tarafli_yap).
  - Modeli 180° döndürür: Unity'de ön +Z, sağ +X. Kök pivot: zeminde, otobüsün tam ortası.

Kullanım (Blender 5.x):
    Blender -b --disable-autoexec --python millennium_donustur.py -- \
        --src "MILLENNIUM II PBC BY MEP" --out <klasör> [--mobil] [--render önizleme.png]

İki çıktı repoda (BMC ile aynı düzen):
    Caio_Millennium_II.fbx            --mobil ile (Düşük/Normal): iç mekân sadeleştirilmiş, gölge kabuğu, küçük dokular
    Caio_Millennium_II_TamKalite.fbx  seçeneksiz (Yüksek); üretildikten sonra bu adla kaydedilir
"""
import argparse
import math
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
import bmesh  # noqa: E402
import numpy as np  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402

import bmc_donustur as bmc  # noqa: E402

AD = "Caio_Millennium_II"
PARCALAR = {
    "rodafrente1": "Teker_OnSol", "rodafrente2": "Teker_OnSag",
    "rodatras1": "Teker_ArkaSol", "rodatras2": "Teker_ArkaSag",
    "portadir1x1": "Kapi_1_1", "portadir1x2": "Kapi_1_2",
    "portadir2x1": "Kapi_2_1", "portadir2x2": "Kapi_2_2",
    "zzVolante": "Direksiyon", "zzVolanteCapa": "Direksiyon",
    "setasFarolBaixo": "Lamba_KisaFar", "setasFarolAlto": "Lamba_UzunFar", "setasFreio": "Lamba_Fren",
    "setasD": "Lamba_SinyalSag", "setasE": "Lamba_SinyalSol", "setasRe": "Lamba_Geri",
    "setasLanternas": "Lamba_Park", "zzLuz1": "Lamba_Ic1", "zzLuz0": "Lamba_Ic2",
}
AT = [r"^vidrosInt", r"^setasInuteis$"]
# mobilde atılan küçük kokpit parçaları (düğmeler, IBIS tuşları)
AT_MOBIL = [r"^bt", r"^ibis", r"^xp"]
# Model OMSI için yapılmış: dokular eski Blender'ın malzeme yuvalarındaydı, yeni sürüm okumuyor.
# Malzeme → doku dosyası (textures/) eşlemesi; (dosya, renk çarpanı)
DOKULAR = {
    "Material.001": "volantemb-mep.bmp", "O500M (Dianteira)": "rodafrenteNOVA.bmp", "O500M (Traseira)": "rodatrasNOVA.bmp",
    "adesivosTransp": "adesivostransparentes.tga", "bag amarelo piso": "bagulhoamarelo.bmp", "bags Acende": "bagulhosint.bmp",
    "bagulhos": "bagulhosmep.bmp", "bagulhos2INT": "bagulhosint.bmp", "bagulhos2INT.002": "bagulhosint.bmp",
    "bagulhos2INT Escuro": ("bagulhosint.bmp", 0.45), "bagulhos2INT Escuro.001": ("bagulhosint.bmp", 0.45),
    "bagulhos2INT Escuro.002": ("bagulhosint.bmp", 0.45), "bagulhos2INT Escuro.003": ("bagulhosint.bmp", 0.45),
    "bagulhos2INT Escuro.005": ("bagulhosint.bmp", 0.45),
    "bakeAzulCYCLES": "bakeazul.bmp", "bakePreto": "bakepreto.bmp", "bakebrancoCYCLES": "bakebranco.bmp",
    "bakelateralCYCLES": "bakelateral.bmp", "bancos especiais.001": "bancos especiais.bmp", "bancos.000": "bancos.bmp",
    "base": "base.bmp", "base ESCURA": ("base.bmp", (0.74, 0.79, 1.0)), "borracha": "borracha.bmp",
    "botoes": "botoes_painel.bmp", "detalhesMEP": "bagulhosmep.bmp", "detalhesMEP.001": "bagulhosmep.bmp",
    "extras": "extras.bmp", "instrumentos.002": "painel.bmp", "lampada": "lampada.bmp", "lampadaSmpApagada": "lampada.bmp",
    "lant": "lanternas.bmp", "letreiro": "vmatrix_voll.bmp", "masca": "mascara.bmp", "mep ibis": "mepibis.bmp",
    "painel": "painel.bmp", "placa chassi": "placachassi.bmp", "preto": "pretoreal.bmp", "ref0": "reflexion0.bmp",
    "ref1": "reflexion1.bmp", "ref2": "reflexion2.bmp", "tampao": "tampaoRender.bmp", "tv": "tv.bmp",
    "vidroCamadas": "vidrosCamadas.tga", "vidroCamadasSEMTRANSP": "vidrosCamadas.tga",
    "vidroitinerario": "vidroitinerario.tga",
}
# saydam malzemeler (Unity'de "_Cam": OtobusKurucu cam yapar)
CAM_MALZEME = {"vidroCamadas", "vidroCamadasSEMTRANSP", "vidroitinerario", "adesivosTransp"}
KAYNAK = ""  # build() doldurur: textures/ klasörünün üstü
DOKU_EN_COK = 2048

# Kaplamalar: (dosya, alt gövde rengi, şerit rengi). Beyaz şablondaki gövde pikselleri boyanır.
KAPLAMALAR = [
# Renkler tools/kaplama.py ile aynı (BMC kaplamaları)
    ("caroserie", (214 / 255, 28 / 255, 32 / 255), None),                               # EGO kırmızı (varsayılan)
    ("Kaplamalar/ego_mavi", (28 / 255, 64 / 255, 160 / 255), (246 / 255, 246 / 255, 244 / 255)),  # EGO mavi
    ("Kaplamalar/ozel_halk", (52 / 255, 150 / 255, 222 / 255), (246 / 255, 246 / 255, 244 / 255)),  # Özel Halk
]


def png_kaydet(img, yol, en_cok):
    """Blender görüntüsünü PNG olarak kaydeder (gerekirse küçültür)."""
    kopya = img.copy()
    w, h = kopya.size
    k = min(1.0, en_cok / max(w, h, 1))
    if k < 1.0:
        kopya.scale(max(4, int(w * k)), max(4, int(h * k)))
    kopya.filepath_raw = yol
    kopya.file_format = "PNG"
    kopya.save()
    bpy.data.images.remove(kopya)


def kaplama_boya(sablon, tex_out, en_cok):
    """Beyaz şablonda doygunluğu düşük, açık pikselleri (gövde boyası) renklendirir; gölgelendirme korunur.
    Şerit: her yan şeridin pencere altındaki bandı (görüntü yüksekliğine göre değil, açık piksellerin
    sütun bazında alt %20'si) ikinci renge boyanır."""
    w, h = sablon.size
    px = np.array(sablon.pixels[:], dtype=np.float32).reshape(h, w, 4)
    rgb = px[..., :3]
    parlak = rgb.max(axis=2)
    doygun = parlak - rgb.min(axis=2)
    govde = (parlak > 0.78) & (doygun < 0.06)
    for ad, renk, serit in KAPLAMALAR:
        out = px.copy()
        golge = (parlak / 0.97).clip(0, 1)[..., None]
        boya = np.array(renk, dtype=np.float32)[None, None, :] * golge
        out[..., :3] = np.where(govde[..., None], boya, rgb)
        if serit is not None:
            # yan şeritler: dokunun üst kısmındaki iki yatay bant (alt kenarları otobüsün altı); şerit alttan %18–26
            # Blender pikselleri alttan yukarı: dokunun üst %51'i dizinin son satırları
            ust = int(h * 0.49)
            satirlar = np.where(govde[ust:].sum(axis=1) > w * 0.3)[0] + ust
            for bant in np.split(satirlar, np.where(np.diff(satirlar) > 4)[0] + 1):
                if len(bant) < h * 0.08:  # tavan kenarı gibi ince bantlar
                    continue
                lo, hi = bant.min(), bant.max()
                y0, y1 = lo + int((hi - lo) * 0.18), lo + int((hi - lo) * 0.26)
                bolge = govde[y0:y1]
                s = np.array(serit, dtype=np.float32)[None, None, :] * golge[y0:y1]
                out[y0:y1, :, :3] = np.where(bolge[..., None], s, out[y0:y1, :, :3])
        yol = os.path.join(tex_out, ad + ".png")
        os.makedirs(os.path.dirname(yol), exist_ok=True)
        img = bpy.data.images.new("kaplama_" + os.path.basename(ad), w, h, alpha=False)
        img.pixels.foreach_set(out.ravel())
        if max(w, h) > en_cok:
            img.scale(int(w * en_cok / max(w, h)), int(h * en_cok / max(w, h)))
        img.filepath_raw = yol
        img.file_format = "PNG"
        img.save()
        bpy.data.images.remove(img)
    return os.path.join(tex_out, "caroserie.png")


def doku_bul(mat):
    """Materyalin temel renk dokusu: düğümlerdeki görüntü, yoksa DOKULAR eşlemesi."""
    if mat is not None and mat.name in DOKULAR:
        d = DOKULAR[mat.name]
        dosya = d[0] if isinstance(d, tuple) else d
        yol = os.path.join(KAYNAK, "textures", dosya)
        img = bpy.data.images.get("__" + dosya) or bpy.data.images.load(yol, check_existing=False)
        img.name = "__" + dosya
        return img
    if not mat or not mat.use_nodes:
        return None
    for n in mat.node_tree.nodes:
        if n.type == "TEX_IMAGE" and n.image and any(l.to_node.type == "BSDF_PRINCIPLED" for l in n.outputs["Color"].links):
            return n.image
    for n in mat.node_tree.nodes:
        if n.type == "TEX_IMAGE" and n.image:
            return n.image
    return None


def temel_renk(mat):
    d = DOKULAR.get(mat.name) if mat else None
    if isinstance(d, tuple):
        c = d[1] if isinstance(d[1], tuple) else (d[1],) * 3
        return (*c, 1.0)
    if mat and mat.name in DOKULAR:
        return (1.0, 1.0, 1.0, 1.0)
    if mat and mat.use_nodes:
        for n in mat.node_tree.nodes:
            if n.type == "BSDF_PRINCIPLED":
                return tuple(n.inputs["Base Color"].default_value)
    return tuple(mat.diffuse_color) if mat else (0.8, 0.8, 0.8, 1.0)


def build(src, out, render_path, mobil):
    global KAYNAK
    KAYNAK = src
    bpy.ops.wm.open_mainfile(filepath=os.path.join(src, "PBCbyMEP.blend"), load_ui=False)
    tex_out = os.path.join(out, "Textures")
    os.makedirs(tex_out, exist_ok=True)
    en_cok = 1024 if mobil else DOKU_EN_COK
    sahne = bpy.context.scene
    dg = bpy.context.evaluated_depsgraph_get()

    kaynaklar = [o for o in sahne.objects if o.type == "MESH" and not o.hide_render and o.visible_get()]
    # yeni materyaller: aynı doku → tek materyal; gövde (base) → M_caroserie
    yeni_mat = {}
    kayit = []

    def mat_icin(mat, cam):
        img = doku_bul(mat)
        anahtar = (img.name if img else "renk_" + (mat.name if mat else "yok"), temel_renk(mat)[:3], cam)
        if anahtar in yeni_mat:
            return yeni_mat[anahtar]
        if img is not None and img.name.lstrip("_").lower() in ("base", "base.bmp"):
            ad = "M_caroserie" if temel_renk(mat)[0] > 0.99 else "M_caroserie_Koyu"
        else:
            ad = "M_" + re.sub(r"[^A-Za-z0-9]+", "_", os.path.splitext(img.name.lstrip("_"))[0] if img else (mat.name if mat else "Gri")).strip("_")
        if cam:
            ad += "_Cam"
        m = bpy.data.materials.new(ad)
        m.use_nodes = True
        bsdf = m.node_tree.nodes["Principled BSDF"]
        bsdf.inputs["Base Color"].default_value = temel_renk(mat)
        if img is not None:
            if ad.startswith("M_caroserie"):
                yol = os.path.join(tex_out, "caroserie.png")
                if not os.path.exists(yol):
                    kaplama_boya(img, tex_out, DOKU_EN_COK)
            else:
                yol = os.path.join(tex_out, ad[2:].replace("_Cam", "") + ".png")
                if not os.path.exists(yol):
                    png_kaydet(img, yol, en_cok)
            node = m.node_tree.nodes.new("ShaderNodeTexImage")
            node.image = bpy.data.images.load(yol)
            m.node_tree.links.new(node.outputs["Color"], bsdf.inputs["Base Color"])
        yeni_mat[anahtar] = m
        kayit.append(f"{mat.name if mat else '-'} → {ad} ({img.name if img else 'doku yok'})")
        return m

    gruplar = {}
    atilan = 0
    for o in kaynaklar:
        if any(re.search(p, o.name) for p in AT) or (mobil and any(re.search(p, o.name) for p in AT_MOBIL)):
            atilan += 1
            continue
        if mobil:
            for mod in o.modifiers:
                if mod.type == "SUBSURF":
                    mod.show_render = mod.show_viewport = False
        dg.update()
        me = bpy.data.meshes.new_from_object(o.evaluated_get(dg))
        me.transform(o.matrix_world)
        # ikinci UV katmanı ("Mapa UV") boş; FBX'te tek katman kalsın
        for uv in [u for u in me.uv_layers if not u.active_render]:
            me.uv_layers.remove(uv)
        etiket = PARCALAR.get(o.name, "Govde")
        if mobil and etiket == "Govde" and o.name.startswith("zz") and o.name not in ("zzEspelhos", "zzCaixaPortas", "zzJuncoes"):
            etiket = "Ic"
        mats = [mat_icin(s.material, s.material is not None and s.material.name in CAM_MALZEME)
                for s in o.material_slots] or [mat_icin(None, False)]
        me.materials.clear()
        for m in mats:
            me.materials.append(m)
        gruplar.setdefault(etiket, []).append(me)

    # grupları tek objeye topla, 180° döndür (Blender +Y ön → -Y ön; FBX'te Unity +Z ön)
    flip = Matrix.Rotation(math.pi, 4, "Z")
    objs = {}
    for etiket, meshler in gruplar.items():
        bm = bmesh.new()
        mats = []
        for me in meshler:
            harita = []
            for m in me.materials:
                if m not in mats:
                    mats.append(m)
                harita.append(mats.index(m))
            for p in me.polygons:
                p.material_index = harita[p.material_index] if p.material_index < len(harita) else 0
            bm.from_mesh(me)
        bm.transform(flip)
        mesh = bpy.data.meshes.new(etiket)
        bm.to_mesh(mesh)
        bm.free()
        for m in mats:
            mesh.materials.append(m)
        obj = bpy.data.objects.new(etiket, mesh)
        sahne.collection.objects.link(obj)
        objs[etiket] = obj

    # kaynak sahneyi boşalt (yalnızca yeni objeler kalsın)
    for o in kaynaklar + [o for o in sahne.objects if o.type != "MESH"]:
        bpy.data.objects.remove(o, do_unlink=True)
    for o in list(sahne.objects):
        if o.name not in objs.values() and o not in objs.values():
            bpy.data.objects.remove(o, do_unlink=True)

    def set_origin(obj, nokta):
        obj.data.transform(Matrix.Translation(-nokta))
        obj.location = nokta

    def bb(obj):
        return [Vector(c) for c in obj.bound_box]

    for etiket in ("Teker_OnSol", "Teker_OnSag", "Teker_ArkaSol", "Teker_ArkaSag", "Direksiyon"):
        set_origin(objs[etiket], sum(bb(objs[etiket]), Vector()) / 8)
    # kapı kanatları: menteşe kapı boşluğunun dış kenarında, kanadın iç yüzünde (döndükten sonra x eksi: sağ taraf)
    for n in (1, 2):
        kanatlar = [objs[f"Kapi_{n}_{k}"] for k in (1, 2)]
        merkez = sum((sum(bb(o), Vector()) / 8 for o in kanatlar), Vector()) / 2
        for o in kanatlar:
            b = bb(o)
            ys = [v.y for v in b]
            c = sum(b, Vector()) / 8
            kenar = max(ys) if c.y > merkez.y else min(ys)
            ic_x = max(v.x for v in b)  # döndürülmüş modelde sağ taraf -x; iç yüz daha büyük x
            set_origin(o, Vector((ic_x, kenar, min(v.z for v in b))))

    if mobil:
        ic = objs.pop("Ic")
        mod = ic.modifiers.new("Decimate", "DECIMATE")
        mod.ratio = 0.35
        bpy.context.view_layer.objects.active = ic
        bpy.ops.object.modifier_apply(modifier=mod.name)
        bpy.ops.object.select_all(action="DESELECT")
        ic.select_set(True)
        objs["Govde"].select_set(True)
        bpy.context.view_layer.objects.active = objs["Govde"]
        bpy.ops.object.join()
        for etiket in ("Teker_OnSol", "Teker_OnSag", "Teker_ArkaSol", "Teker_ArkaSag"):
            o = objs[etiket]
            mod = o.modifiers.new("Decimate", "DECIMATE")
            mod.ratio = 0.3
            bpy.context.view_layer.objects.active = o
            bpy.ops.object.modifier_apply(modifier=mod.name)
        objs["Golge_Govde"] = bmc.shadow_proxy([objs["Govde"]] + [objs[t] for t in bmc.WHEELS])

    bmc.iki_tarafli_yap(objs)

    bpy.context.view_layer.update()  # pivotlar taşındı: matrix_world güncel olsun
    pts = [o.matrix_world @ Vector(c) for o in objs.values() for c in o.bound_box]
    cx = (min(p.x for p in pts) + max(p.x for p in pts)) / 2
    cy = (min(p.y for p in pts) + max(p.y for p in pts)) / 2
    # zemin: teker tabanı (kaynakta zeminin altına inen parçalar var)
    zmin = min((o.matrix_world @ Vector(c)).z for t, o in objs.items() if t.startswith("Teker_") for c in o.bound_box)
    kok = bpy.data.objects.new(AD, None)
    sahne.collection.objects.link(kok)
    for o in objs.values():
        o.location -= Vector((cx, cy, zmin))
        o.parent = kok

    rapor, toplam = [], 0
    for etiket, o in sorted(objs.items()):
        t = sum(len(p.vertices) - 2 for p in o.data.polygons)
        toplam += t
        loc = o.location
        rapor.append(f"| `{etiket}` | ({-loc.x:+.3f}, {loc.z:+.3f}, {-loc.y:+.3f}) | {t} | {len(o.data.materials)} |")
    print(f"atılan obje: {atilan}, materyal: {len(yeni_mat)}, toplam üçgen: {toplam}")
    print("\n".join(rapor))
    print("\n".join(kayit))
    with open(os.path.join(out, "parcalar.md"), "w") as fh:
        fh.write(f"Toplam üçgen: {toplam}, materyal: {len(yeni_mat)}\n\n")
        fh.write("| Parça | Unity yerel konum (x, y, z) | Üçgen | Materyal |\n|---|---|---|---|\n")
        fh.write("\n".join(rapor) + "\n")

    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.fbx(filepath=os.path.join(out, AD + ".fbx"), use_selection=True,
                             apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                             bake_space_transform=True, path_mode="STRIP", object_types={"MESH", "EMPTY"},
                             mesh_smooth_type="FACE")
    if render_path:
        from tds_okuyucu import render
        shown = [o for t, o in objs.items() if not t.startswith(("Lamba_", "Golge_"))]
        for t, o in objs.items():
            if t.startswith(("Lamba_", "Golge_")):
                o.hide_render = True
        render(shown, render_path)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    ap = argparse.ArgumentParser()
    ap.add_argument("--src", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--render", default=None)
    ap.add_argument("--mobil", action="store_true")
    args = ap.parse_args(argv)
    os.makedirs(args.out, exist_ok=True)
    build(args.src, args.out, args.render, args.mobil)


if __name__ == "__main__":
    main()
