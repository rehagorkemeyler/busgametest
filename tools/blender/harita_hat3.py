"""
Hat 3 (EGO 408 Kızılay → Güneşevler) parçalarını Blender nesnelerine çevirir; FBX'e aktarır ya da önizleme render'ı alır.

Girdi: tools/harita/hat3 üreticilerinin ara çıktıları (<ISLEM>/parcalar/*.pkl; Unity koordinatı, CCW = yukarı).
Her parça (250 m kare) bir FBX: Maps/Hat3/Parcalar/H3_<i>_<j>.fbx, çocuk nesneler grup adlarıyla (Yol, Zemin, Binalar...).

Kullanım (Blender'ın python'u; tools/harita PYTHONPATH'te):
    python harita_hat3.py -- --fbx AnkaraBusSimulator/Assets/_Project/Maps/Hat3
    python harita_hat3.py -- --render <klasör> --kamera "x,y,z,yaw,pitch" [--kamera ...] [--yaricap 700]
"""
import argparse
import glob
import math
import os
import pickle
import re
import sys

import bpy
import numpy as np

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "harita"))
from hat3 import kok  # noqa: E402
from hat3.mesh import PARCA  # noqa: E402

# malzeme → (grup, önizleme rengi (doğrusal), doku dosyası)
MALZEMELER = {
    "H3_Asfalt": ("Yol", (0.045, 0.045, 0.05), "asfalt.png"),
    "H3_Kaldirim": ("Yol", (0.35, 0.33, 0.31), "kaldirim.png"),
    "H3_Refuj": ("Yol", (0.12, 0.22, 0.06), "refuj.png"),
    "H3_Bordur": ("Yol", (0.45, 0.45, 0.43), "bordur.png"),
    "H3_Cizgi": ("Yol", (0.8, 0.8, 0.78), None),
    "H3_Zemin": ("Zemin", (0.32, 0.26, 0.18), "zemin.png"),
    "H3_Cim": ("Zemin", (0.1, 0.2, 0.05), "cim.png"),
    "H3_Cephe": ("Binalar", (0.6, 0.55, 0.48), "cephe_atlas.png"),
    "H3_Cati": ("Binalar", (0.3, 0.28, 0.27), "cati_atlas.png"),
}


def malzeme(ad, doku_klasoru=None):
    m = bpy.data.materials.get(ad)
    if m:
        return m
    m = bpy.data.materials.new(ad)
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    grup, renk, doku = MALZEMELER.get(ad, ("Diger", (0.5, 0.5, 0.5), None))
    bsdf.inputs["Base Color"].default_value = (*renk, 1)
    bsdf.inputs["Roughness"].default_value = 0.9
    if doku and doku_klasoru and os.path.exists(os.path.join(doku_klasoru, doku)):
        img = bpy.data.images.load(os.path.join(doku_klasoru, doku))
        tex = m.node_tree.nodes.new("ShaderNodeTexImage")
        tex.image = img
        m.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    return m


def nesne(ad, parcalar, doku_klasoru=None):
    """parcalar: [(malzeme, V (Unity), UV, F)] → tek Blender nesnesi (malzeme yuvaları)."""
    Vs, Fs, UVs, Ms = [], [], [], []
    n = 0
    mats = []
    for mal, V, UV, F in parcalar:
        if mal not in mats:
            mats.append(mal)
        Vs.append(np.stack([-V[:, 0], -V[:, 2], V[:, 1]], 1))     # Unity (x,y,z) → Blender (-x, -z, y)
        Fs.append(F + n)
        UVs.append(UV[F].reshape(-1, 2))
        Ms.append(np.full(len(F), mats.index(mal)))
        n += len(V)
    V = np.concatenate(Vs)
    F = np.concatenate(Fs)
    me = bpy.data.meshes.new(ad)
    me.vertices.add(len(V))
    me.vertices.foreach_set("co", V.astype(np.float32).ravel())
    me.loops.add(len(F) * 3)
    me.loops.foreach_set("vertex_index", F.astype(np.int32).ravel())
    me.polygons.add(len(F))
    me.polygons.foreach_set("loop_start", np.arange(0, len(F) * 3, 3, dtype=np.int32))
    me.polygons.foreach_set("loop_total", np.full(len(F), 3, dtype=np.int32))
    me.polygons.foreach_set("material_index", np.concatenate(Ms).astype(np.int32))
    uv = me.uv_layers.new(name="UVMap")
    uv.data.foreach_set("uv", np.concatenate(UVs).astype(np.float32).ravel())
    me.update(calc_edges=True)
    me.validate(clean_customdata=False)
    for mal in mats:
        me.materials.append(malzeme(mal, doku_klasoru))
    o = bpy.data.objects.new(ad, me)
    bpy.context.scene.collection.objects.link(o)
    return o


def parcalari_yukle(on_ek):
    """{(i, j): {malzeme: (V, UV, F)}} — <ISLEM>/parcalar/<on_ek>_<i>_<j>.pkl"""
    out = {}
    for f in glob.glob(os.path.join(kok.ISLEM, "parcalar", f"{on_ek}_*_*.pkl")):
        m = re.match(rf"{on_ek}_(-?\d+)_(-?\d+)\.pkl", os.path.basename(f))
        out[(int(m.group(1)), int(m.group(2)))] = pickle.load(open(f, "rb"))
    return out


def parca_nesneleri(k, katmanlar, doku_klasoru=None):
    """Bir parçanın grup nesneleri: {grup: nesne}. Köşeler parça merkezine göre (nesne konumu merkezde)."""
    i, j = k
    cx, cz = (i + 0.5) * PARCA, (j + 0.5) * PARCA
    gruplar = {}
    for kat in katmanlar:
        for mal, (V, UV, F) in kat.get(k, {}).items():
            grup = MALZEMELER.get(mal, ("Diger",))[0]
            V = V.copy()
            V[:, 0] -= cx
            V[:, 2] -= cz
            gruplar.setdefault(grup, []).append((mal, V, UV, F))
    out = {}
    for grup, liste in gruplar.items():
        o = nesne(f"H3_{i}_{j}_{grup}", liste, doku_klasoru)
        o.location = (-cx, -cz, 0)
        out[grup] = o
    return out


def sahne_temizle():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def render(args, katmanlar, doku_klasoru):
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"           # sunucuda GPU/EGL yok
    sc.cycles.samples = 24
    sc.cycles.use_denoising = False
    sc.view_settings.view_transform = "Standard"
    sc.render.resolution_x, sc.render.resolution_y = args.genislik, int(args.genislik * 9 / 16)
    w = bpy.data.worlds.new("w")
    sc.world = w
    w.use_nodes = True
    w.node_tree.nodes["Background"].inputs["Color"].default_value = (0.55, 0.68, 0.85, 1)
    w.node_tree.nodes["Background"].inputs["Strength"].default_value = 1.0
    gunes = bpy.data.objects.new("gunes", bpy.data.lights.new("gunes", "SUN"))
    gunes.data.energy = 3.5
    gunes.rotation_euler = (math.radians(50), math.radians(10), math.radians(-35))
    sc.collection.objects.link(gunes)
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    sc.collection.objects.link(cam)
    sc.camera = cam
    cam.data.lens = 18
    cam.data.clip_end = 3000
    for ki, kam in enumerate(args.kamera):
        x, y, z, yaw, pitch = map(float, kam.split(","))
        # yalnız yakındaki parçalar
        for o in list(bpy.data.objects):
            if o.type == "MESH":
                bpy.data.objects.remove(o, do_unlink=True)
        for k in set().union(*[set(kat) for kat in katmanlar]):
            if math.hypot((k[0] + 0.5) * PARCA - x, (k[1] + 0.5) * PARCA - z) < args.yaricap:
                parca_nesneleri(k, katmanlar, doku_klasoru)
        # Unity yaw (0 = +z kuzey, saat yönü) → Blender
        cam.location = (-x, -z, y)
        cam.rotation_euler = (math.radians(90 + pitch), 0, math.radians(180 - yaw))
        sc.render.filepath = os.path.join(args.render, f"h3_{ki:02d}.png")
        bpy.ops.render.render(write_still=True)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument("--fbx", default=None)
    ap.add_argument("--render", default=None)
    ap.add_argument("--kamera", action="append", default=[])
    ap.add_argument("--yaricap", type=float, default=700)
    ap.add_argument("--genislik", type=int, default=1280)
    ap.add_argument("--dokular", default=None)
    args = ap.parse_args(argv)
    sahne_temizle()
    katmanlar = [parcalari_yukle(on) for on in ("yol", "bina", "simge", "donati")]
    if args.render:
        os.makedirs(args.render, exist_ok=True)
        render(args, katmanlar, args.dokular)
    if args.fbx:
        cikti = os.path.join(args.fbx, "Parcalar")
        os.makedirs(cikti, exist_ok=True)
        anahtarlar = sorted(set().union(*[set(kat) for kat in katmanlar]))
        for k in anahtarlar:
            for o in list(bpy.data.objects):
                bpy.data.objects.remove(o, do_unlink=True)
            for m in list(bpy.data.meshes):
                bpy.data.meshes.remove(m)
            nes = parca_nesneleri(k, katmanlar)
            bpy.ops.object.select_all(action="DESELECT")
            for o in nes.values():
                o.select_set(True)
            bpy.ops.export_scene.fbx(filepath=os.path.join(cikti, f"H3_{k[0]}_{k[1]}.fbx"), use_selection=True,
                                     apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                                     bake_space_transform=True, mesh_smooth_type="FACE", path_mode="STRIP",
                                     object_types={"MESH"})
        print(f"{len(anahtarlar)} parça FBX → {cikti}")


if __name__ == "__main__":
    main()
