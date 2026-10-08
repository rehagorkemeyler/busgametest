"""
Hat 1 (Kızılay AVM → Atakule) harita yerleşimi.

Üretir:
  - <maps>/Hat1/Hat1_Yerlesim.json : Unity editör scripti (Ankara Bus > Hat 1 Haritasını Kur) bunu okur.
      Her model için Assets/_Project/Maps/<model>.fbx yolu, konum (Unity), Y açısı ve grup.
  - <maps>/Hat1/Zemin_Hat1.fbx      : güzergâhı izleyen zemin (yükseklik = en yakın yolun yüksekliği).
  - isteğe bağlı önizleme render'ları (kuşbakışı ve sürücü gözü).

Unity çerçevesi: x sağ, y yukarı, z ileri. Güzergâh başlangıçta +z yönünde ilerler.
Bu scriptte yol ekseni (f, r, z, θ) ile tutulur: Unity konumu = (r, z, f), Y açısı = θ (derece, sağa dönüş +).

Kullanım:
    python harita_hat1.py --maps AnkaraBusSimulator/Assets/_Project/Maps [--render-dir <klasör>]
"""
import argparse
import json
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
import numpy as np  # noqa: E402
from mathutils import Vector  # noqa: E402

import apartman_kit as kit  # noqa: E402
import yapilar_kit as yapi  # noqa: E402
import yol_kit as yol  # noqa: E402

B, C = yol.BULVAR, yol.CINNAH
# parça adı -> (uzunluk, eğim, dönüş°)
SPEC = {
    "Bulvar_Duz_20m": (20, 0.0, 0), "Bulvar_Egim2_20m": (20, 0.02, 0), "Bulvar_Egim4_20m": (20, 0.04, 0),
    "Bulvar_DurakCebi_40m": (40, 0.0, 0), "Bulvar_DurakCebi_Egim4_40m": (40, 0.04, 0),
    "Kavsak_Bulvar_Cinnah_T": (40, 0.0, 0),
    "Cinnah_Duz_20m": (20, 0.0, 0), "Cinnah_Egim8_20m": (20, 0.08, 0), "Cinnah_Egim10_20m": (20, 0.10, 0),
    "Cinnah_Egim12_20m": (20, 0.12, 0), "Cinnah_Viraj_Sag15_Egim10": (20, 0.10, 15),
    "Cinnah_Viraj_Sol15_Egim10": (20, 0.10, -15), "Cinnah_Viraj_Sag30_Egim8": (30, 0.08, 30),
    "Cinnah_Viraj_Sol30_Egim8": (30, 0.08, -30), "Cinnah_DurakCebi_Egim8_30m": (30, 0.08, 0),
    "Atakule_DonusHalkasi": (14, 0.0, 0),
}

BULVAR_CHAIN = (
    ["Bulvar_Duz_20m"] * 2                      # Kızılay AVM önü (başlangıcın gerisi)
    + ["Bulvar_DurakCebi_40m"]                  # Durak 1: Kızılay AVM
    + ["Bulvar_Egim2_20m"] * 26
    + ["Bulvar_DurakCebi_40m"]                  # Durak 2: Meclis
    + ["Bulvar_Egim4_20m"] * 31
    + ["Bulvar_DurakCebi_Egim4_40m"]            # Durak 3: Kuğulu Park
    + ["Bulvar_Duz_20m", "Kavsak_Bulvar_Cinnah_T", "Bulvar_Duz_20m", "Bulvar_Duz_20m"]
)
CINNAH_CHAIN = (
    ["Cinnah_Duz_20m"] + ["Cinnah_Egim8_20m"] * 5 + ["Cinnah_Viraj_Sol30_Egim8"] + ["Cinnah_Egim10_20m"] * 5
    + ["Cinnah_Viraj_Sag15_Egim10", "Cinnah_DurakCebi_Egim8_30m"]   # Durak 4: Cinnah
    + ["Cinnah_Egim10_20m"] * 4 + ["Cinnah_Viraj_Sag15_Egim10"] + ["Cinnah_Egim12_20m"] * 4
    + ["Cinnah_Viraj_Sol15_Egim10"] + ["Cinnah_Egim8_20m"] * 3 + ["Cinnah_Duz_20m", "Atakule_DonusHalkasi"]
)


class Chain:
    """Uç uca eklenmiş yol parçaları; s (zincir boyunca metre) ile sorgulanır."""

    def __init__(self, names, start=(0.0, 0.0, 0.0, 0.0), s0=0.0):
        self.parts = []  # (ad, s_başlangıç, poz (f, r, z, θ), RoadPath)
        f, r, z, th = start
        s = s0
        for n in names:
            L, slope, turn = SPEC[n]
            path = yol.RoadPath(L, slope, turn)
            self.parts.append((n, s, (f, r, z, th), path))
            lf, lr, lz, lth = path.frame(L)
            f, r = f + lf * math.cos(th) - lr * math.sin(th), r + lf * math.sin(th) + lr * math.cos(th)
            z += lz
            th += lth
            s += L
        self.s_start, self.s_end = s0, s
        self.end = (f, r, z, th)

    def index_of(self, name, nth=0):
        hits = [p for p in self.parts if p[0] == name]
        return hits[nth]

    def frame(self, s, offset=0.0):
        """Zincirde s noktasında, eksenden 'offset' m sağdaki nokta: (f, r, z, θ)."""
        s = min(max(s, self.s_start), self.s_end - 1e-6)
        for n, ps, (f0, r0, z0, th0), path in self.parts:
            if ps <= s < ps + path.length:
                lf, lr, lz, lth = path.frame(s - ps)
                f = f0 + lf * math.cos(th0) - lr * math.sin(th0)
                r = r0 + lf * math.sin(th0) + lr * math.cos(th0)
                th = th0 + lth
                return f - offset * math.sin(th), r + offset * math.cos(th), z0 + lz, th
        raise ValueError(s)


def unity(f, r, z):
    return [round(r, 3), round(z, 3), round(f, 3)]


class Layout:
    def __init__(self):
        self.items, self.stops, self.lanes, self.signals = [], [], [], []

    def add(self, model, f, r, z, rot_deg, group):
        self.items.append({"model": model, "pos": unity(f, r, z), "rotY": round(rot_deg % 360, 2), "group": group})

    def stop(self, name, f, r, z, rot_deg, radius=8.0):
        self.stops.append({"name": name, "pos": unity(f, r, z), "rotY": round(rot_deg % 360, 2), "radius": radius})


def place_roads(lay, chain):
    for n, s, (f, r, z, th), path in chain.parts:
        lay.add(f"Roads/{n}", f, r, z, math.degrees(th), "Yol")


def line_buildings(lay, chain, side, s_from, s_to, pool, widths, rng, *, offset, gap, exclude=(), group="Binalar"):
    """Zincirin bir yanına (side +1 sağ, -1 sol) binaları uç uca dizer."""
    s = s_from
    while s < s_to:
        name = rng.choices([p for p, _ in pool], [w for _, w in pool])[0]
        w = widths[name]["w"]
        if s + w > s_to:
            break
        if any(a < s + w and s < b for a, b in exclude):
            s += 4.0
            continue
        setback = widths[name]["setback"]
        f, r, _, th = chain.frame(s + w / 2, side * (offset + setback))
        z = min(chain.frame(s)[2], chain.frame(s + w)[2])
        rot = math.degrees(th) + (-90 if side > 0 else 90)
        lay.add(f"Buildings/Apartmanlar/{name}", f, r, z, rot, group)
        s += w + gap


def build_layout(widths, seed=2024):
    rng = random.Random(seed)
    lay = Layout()
    bul = Chain(BULVAR_CHAIN, start=(-40.0, 0.0, 0.0, 0.0), s0=-40.0)
    place_roads(lay, bul)
    HB, WB = B["half_road"], B["sidewalk"]
    HC, WC = C["half_road"], C["sidewalk"]

    # kavşak ve Cinnah zinciri
    n, sj, (fj, rj, zj, thj), _ = bul.index_of("Kavsak_Bulvar_Cinnah_T")
    s_mid = sj + 20.0
    stub_len = 12.0
    cf, cr, cz, cth = bul.frame(s_mid, HB + WB + stub_len)
    cin = Chain(CINNAH_CHAIN, start=(cf, cr, cz, cth + math.pi / 2))
    place_roads(lay, cin)

    # --- duraklar -------------------------------------------------------
    stops_bul = [p for p in bul.parts if p[0].startswith("Bulvar_DurakCebi")]
    names_bul = ["Kızılay AVM", "Meclis", "Kuğulu Park"]
    bay_ranges = []
    for (n, s, pose, path), name in zip(stops_bul, names_bul):
        sc = s + path.length / 2
        f, r, z, th = bul.frame(sc, HB + 1.5)
        lay.stop(name, f, r, z, math.degrees(th))
        f, r, z, th = bul.frame(sc, HB + 3.3)
        lay.add("Props/EGO_Durak", f, r, z + 0.15, math.degrees(th) - 90, "Duraklar")
        bay_ranges.append((s, s + path.length))
    n, s, pose, path = cin.index_of("Cinnah_DurakCebi_Egim8_30m")
    sc = s + path.length / 2
    f, r, z, th = cin.frame(sc, HC + 1.5)
    lay.stop("Cinnah", f, r, z, math.degrees(th))
    f, r, z, th = cin.frame(sc, HC + WC + 0.3)
    lay.add("Props/EGO_Durak", f, r, z + 0.15, math.degrees(th) - 90, "Duraklar")
    cin_bay = (s, s + path.length)
    n, s_ring, pose, path = cin.index_of("Atakule_DonusHalkasi")
    f, r, z, th = cin.frame(s_ring + 7.0, HC - 1.6)
    lay.stop("Atakule", f, r, z, math.degrees(th))
    f, r, z, th = cin.frame(s_ring + 7.0, HC + 0.3)
    lay.add("Props/EGO_Durak", f, r, z + 0.15, math.degrees(th) - 90, "Duraklar")

    # otobüs başlangıcı: Kızılay AVM cebinin başı
    s_kiz = stops_bul[0][1]
    f, r, z, th = bul.frame(s_kiz + 10.0, HB + 1.5)
    spawn = {"pos": unity(f, r, z + 0.3), "rotY": math.degrees(th)}

    # --- simge yapılar ---------------------------------------------------
    f, r, z, th = bul.frame(-22.0, HB + WB + 0.5)
    lay.add("Landmarks/KizilayAVM", f, r, z, math.degrees(th) - 90, "SimgeYapilar")
    s_meclis = stops_bul[1][1] + 40.0
    tbmm_end = s_meclis + 300.0
    s = s_meclis
    gate_s = s_meclis + 60.0
    while s < tbmm_end:
        if gate_s - 16 <= s + 10 <= gate_s + 16:
            s += 20.0
            continue
        f, r, z, th = bul.frame(s + 10, HB + WB + 0.6)
        lay.add("Landmarks/TBMM_Duvar_20m", f, r, z, math.degrees(th) - 90, "SimgeYapilar")
        s += 20.0
    f, r, z, th = bul.frame(gate_s, HB + WB + 0.6)
    lay.add("Landmarks/TBMM_Kapi", f, r, z, math.degrees(th) - 90, "SimgeYapilar")
    for k in range(int((tbmm_end - s_meclis) / 12)):
        for depth in (8.0, 16.0, 26.0):
            sk = s_meclis + 6 + k * 12 + rng.uniform(-3, 3)
            f, r, z, th = bul.frame(sk, HB + WB + depth + rng.uniform(-2, 2))
            lay.add(f"Props/{rng.choice(['Agac_Kavak', 'Agac_Cinar_1', 'Agac_Cinar_2'])}", f, r, z, rng.uniform(0, 360), "Agaclar")
    kugulu_s = sj - 40.0
    f, r, z, th = bul.frame(kugulu_s, -(HB + WB + 0.5))
    lay.add("Landmarks/KuguluPark_Golet", f, r, z, math.degrees(th) + 90, "SimgeYapilar")
    ef, er, ez, eth = cin.frame(cin.s_end)
    ring_center = (ef + 15.75 * math.cos(eth), er + 15.75 * math.sin(eth), ez, eth)
    lay.add("Landmarks/Atakule", ring_center[0], ring_center[1], ring_center[2] + 0.2, math.degrees(ring_center[3]), "SimgeYapilar")
    for k in range(10):
        a = 2 * math.pi * k / 10
        rr = 32.0
        th = ring_center[3]
        f = ring_center[0] + rr * math.cos(a)
        r = ring_center[1] + rr * math.sin(a)
        if abs(math.atan2(math.sin(a - (th + math.pi)), math.cos(a - (th + math.pi)))) < 0.6:
            continue  # giriş yolunu kapatma
        lay.add("Props/Agac_Kavak", f, r, ring_center[2], rng.uniform(0, 360), "Agaclar")

    # --- binalar ---------------------------------------------------------
    pool_a = [("A1_Bulvar_6Kat_Krem", 3), ("A1_Bulvar_7Kat_Somon", 3), ("A1_Bulvar_8Kat_Bej", 3),
              ("A2_Kose_7Kat_Gri", 1), ("A5_Ofis_10Kat_Mavi", 1), ("A5_Ofis_8Kat_Yesil", 1)]
    pool_b = [("A1_Bulvar_6Kat_Krem", 2), ("A1_Bulvar_8Kat_Bej", 1), ("A3_Cankaya_5Kat_SariKirma", 3),
              ("A5_Ofis_10Kat_Mavi", 2), ("A5_Ofis_8Kat_Yesil", 2), ("A2_Kose_6Kat_Somon", 1)]
    pool_c = [("A4_Yamac_5Kat_Yesil", 3), ("A4_Yamac_4Kat_BeyazKirma", 3), ("A3_Cankaya_5Kat_SariKirma", 2),
              ("A1_Bulvar_6Kat_Krem", 1)]
    s_b = stops_bul[1][1] + 40.0
    mouth = HC + 2.0
    bul_excl_r = bay_ranges + [(-40.0, 10.0), (s_meclis, tbmm_end), (s_mid - mouth - 4, bul.s_end)]
    bul_excl_l = [(kugulu_s - 26, kugulu_s + 26)]
    line_buildings(lay, bul, +1, -40.0, s_b, pool_a, widths, rng, offset=HB + WB, gap=0.0, exclude=bul_excl_r)
    line_buildings(lay, bul, -1, -40.0, s_b, pool_a, widths, rng, offset=HB + WB, gap=0.0, exclude=bul_excl_l)
    line_buildings(lay, bul, +1, s_b, bul.s_end, pool_b, widths, rng, offset=HB + WB, gap=2.0, exclude=bul_excl_r)
    line_buildings(lay, bul, -1, s_b, bul.s_end, pool_b, widths, rng, offset=HB + WB, gap=2.0, exclude=bul_excl_l)
    cin_end = s_ring - 28.0
    line_buildings(lay, cin, +1, 20.0, cin_end, pool_c, widths, rng, offset=HC + WC, gap=3.0, exclude=[cin_bay])
    line_buildings(lay, cin, -1, 40.0, cin_end, pool_c, widths, rng, offset=HC + WC, gap=3.0)

    # --- refüj ve kaldırım ağaçları, lambalar ----------------------------
    s = -36.0
    k = 0
    while s < bul.s_end - 4:
        f, r, z, th = bul.frame(s)
        if k % 2 == 0:
            lay.add("Props/Lamba_Bulvar", f, r, z + 0.2, math.degrees(th), "Lambalar")
        else:
            lay.add(f"Props/{rng.choice(['Agac_Cinar_1', 'Agac_Cinar_2'])}", f, r, z + 0.2, rng.uniform(0, 360), "Agaclar")
        s += 20.0
        k += 1
    for side in (+1, -1):
        s = 10.0
        while s < cin_end:
            if not (side > 0 and cin_bay[0] - 4 < s < cin_bay[1] + 4):
                f, r, z, th = cin.frame(s, side * (HC + WC - 0.8))
                lay.add(f"Props/{rng.choice(['Agac_Cinar_1', 'Agac_Kavak'])}", f, r, z + 0.15, rng.uniform(0, 360), "Agaclar")
            s += 26.0

    lay.lanes, lay.signals = build_lanes(bul, cin, s_mid, mouth)
    return lay, bul, cin, spawn


def build_lanes(bul, cin, s_mid, mouth, step=5.0):
    """Trafik şeritleri, Kuğulu kavşağındaki dönüş bağlantıları, durma çizgileri ve trafik ışığı.
    Her şerit tek yönlü nokta dizisi (Unity koordinatı, düz liste x,y,z,...). Ters yön şeritleri aynı ofsetin
    solunda, noktaları ters sırada. Mesafeler şerit başından metre."""
    lanes = {}

    def polyline_length(pts):
        n = len(pts) // 3
        total = 0.0
        for i in range(1, n):
            a, b = pts[(i - 1) * 3:i * 3], pts[i * 3:i * 3 + 3]
            total += math.dist(a, b)
        return total

    def lane(chain, offset, s0, s1, name, limit):
        svals, pts = [], []
        s = s0
        while s < s1:
            svals.append(s)
            s += step
        svals.append(s1)
        for sv in svals:
            f, r, z, _ = chain.frame(sv, offset)
            pts += unity(f, r, z)
        cum = [0.0]
        for i in range(1, len(svals)):
            cum.append(cum[-1] + math.dist(pts[(i - 1) * 3:i * 3], pts[i * 3:i * 3 + 3]))
        reverse = offset < 0
        if reverse:
            pts = [c for i in range(len(pts) // 3 - 1, -1, -1) for c in pts[i * 3:i * 3 + 3]]
        total = cum[-1]

        def dist_at(sq):
            # zincir mesafesi -> şerit başından mesafe
            for i in range(1, len(svals)):
                if sq <= svals[i]:
                    t = (sq - svals[i - 1]) / max(svals[i] - svals[i - 1], 1e-6)
                    d = cum[i - 1] + t * (cum[i] - cum[i - 1])
                    return round(total - d if reverse else d, 2)
            return round(0.0 if reverse else total, 2)

        lanes[name] = {"name": name, "limitKmh": limit, "points": pts, "exits": [], "stopLine": -1.0,
                       "group": 0, "signal": "", "_dist": dist_at, "_len": total}

    m, w = B["median"], B["lane"]
    for k in range(3):
        off = m + w * (k + 0.5)
        lane(bul, off, bul.s_start, bul.s_end, f"Bulvar_Gidis_{k + 1}", 50)
        lane(bul, -off, bul.s_start, bul.s_end, f"Bulvar_Donus_{k + 1}", 50)
    wc = C["lane"]
    for k in range(2):
        off = wc * (k + 0.5)
        lane(cin, off, 2.0, cin.s_end, f"Cinnah_Gidis_{k + 1}", 40)
        lane(cin, -off, 2.0, cin.s_end, f"Cinnah_Donus_{k + 1}", 40)

    def bezier(name, p0, d0, p2, d2, limit=25):
        """İki şeridi bağlayan ikinci dereceden eğri. p: (f, r, z), d: hareket yönü (f, r)."""
        # kontrol noktası: p0'dan d0 yönündeki doğru ile p2'ye d2 yönünde gelen doğrunun kesişimi
        den = d0[0] * d2[1] - d0[1] * d2[0]
        t = ((p2[0] - p0[0]) * d2[1] - (p2[1] - p0[1]) * d2[0]) / den
        c = (p0[0] + d0[0] * t, p0[1] + d0[1] * t)
        pts = []
        for i in range(13):
            u = i / 12
            f = (1 - u) ** 2 * p0[0] + 2 * (1 - u) * u * c[0] + u ** 2 * p2[0]
            r = (1 - u) ** 2 * p0[1] + 2 * (1 - u) * u * c[1] + u ** 2 * p2[1]
            z = p0[2] + (p2[2] - p0[2]) * u
            pts += unity(f, r, z)
        lanes[name] = {"name": name, "limitKmh": limit, "points": pts, "exits": [], "stopLine": -1.0,
                       "group": 0, "signal": "", "_len": polyline_length(pts)}

    def exit_(src, at, dst, dst_at, p):
        lanes[src]["exits"].append({"target": dst, "at": round(at, 2), "targetAt": round(dst_at, 2), "probability": p})

    def pt(chain, sv, off):
        f, r, z, th = chain.frame(sv, off)
        return (f, r, z), th

    HBl = B["half_road"]
    HCl = C["half_road"]
    # 1) bulvardan Cinnah'a sağa dönüş (en sağ şeritten)
    s_a = s_mid - mouth
    p0, th0 = pt(bul, s_a, m + w * 2.5)
    p2, th2 = pt(cin, 2.0, wc * 1.5)
    bezier("Baglanti_Bulvar_Cinnah", p0, (math.cos(th0), math.sin(th0)), p2, (math.cos(th2), math.sin(th2)))
    exit_("Bulvar_Gidis_3", lanes["Bulvar_Gidis_3"]["_dist"](s_a), "Baglanti_Bulvar_Cinnah", 0.0, 0.35)
    exit_("Baglanti_Bulvar_Cinnah", lanes["Baglanti_Bulvar_Cinnah"]["_len"] - 0.05, "Cinnah_Gidis_2", 0.0, 1.0)
    # 2) Cinnah'tan Kızılay yönüne sola dönüş (iç şeritten)
    p0, th0 = pt(cin, 2.0, -wc * 0.5)
    s_b = s_mid - mouth - 4.0
    p2, th2 = pt(bul, s_b, -(m + w * 0.5))
    back0 = (-math.cos(th0), -math.sin(th0))
    back2 = (-math.cos(th2), -math.sin(th2))
    bezier("Baglanti_Cinnah_Kizilay", p0, back0, p2, back2)
    L = lanes["Cinnah_Donus_1"]["_len"]
    exit_("Cinnah_Donus_1", L - 0.3, "Baglanti_Cinnah_Kizilay", 0.0, 1.0)
    exit_("Baglanti_Cinnah_Kizilay", lanes["Baglanti_Cinnah_Kizilay"]["_len"] - 0.05, "Bulvar_Donus_1",
          lanes["Bulvar_Donus_1"]["_dist"](s_b), 1.0)
    # 3) Cinnah'tan bulvarın kuzey koluna sağa dönüş (dış şeritten)
    p0, th0 = pt(cin, 2.0, -wc * 1.5)
    s_c = s_mid + mouth + 2.0
    p2, th2 = pt(bul, s_c, m + w * 2.5)
    bezier("Baglanti_Cinnah_Kuzey", p0, (-math.cos(th0), -math.sin(th0)), p2, (math.cos(th2), math.sin(th2)))
    L = lanes["Cinnah_Donus_2"]["_len"]
    exit_("Cinnah_Donus_2", L - 0.3, "Baglanti_Cinnah_Kuzey", 0.0, 1.0)
    exit_("Baglanti_Cinnah_Kuzey", lanes["Baglanti_Cinnah_Kuzey"]["_len"] - 0.05, "Bulvar_Gidis_3",
          lanes["Bulvar_Gidis_3"]["_dist"](s_c), 1.0)

    # durma çizgileri ve trafik ışığı (grup 0: bulvar, grup 1: Cinnah'tan çıkış)
    signal = "Kavsak_Kugulu"
    s_line_g = s_mid - mouth - 7.0
    s_line_d = s_mid + mouth + 7.0
    for k in range(1, 4):
        for nm, sl in ((f"Bulvar_Gidis_{k}", s_line_g), (f"Bulvar_Donus_{k}", s_line_d)):
            lanes[nm].update(stopLine=lanes[nm]["_dist"](sl), group=0, signal=signal)
    for k in range(1, 3):
        nm = f"Cinnah_Donus_{k}"
        lanes[nm].update(stopLine=round(lanes[nm]["_len"] - 1.5, 2), group=1, signal=signal)

    heads = []
    f, r, z, th = bul.frame(s_line_g - 1.0, HBl + 0.8)
    heads.append({"model": "Props/Trafik_Lambasi", "pos": unity(f, r, z + 0.15), "rotY": round((math.degrees(th) + 180) % 360, 2), "group": 0})
    f, r, z, th = bul.frame(s_line_d + 1.0, -(HBl + 0.8))
    heads.append({"model": "Props/Trafik_Lambasi", "pos": unity(f, r, z + 0.15), "rotY": round(math.degrees(th) % 360, 2), "group": 0})
    f, r, z, th = cin.frame(4.0, -(HCl + 0.8))
    heads.append({"model": "Props/Trafik_Lambasi", "pos": unity(f, r, z + 0.15), "rotY": round(math.degrees(th) % 360, 2), "group": 1})
    signals = [{"name": signal,
                "phases": [{"greenGroups": [0], "green": 20.0, "yellow": 3.0, "allRed": 1.5},
                           {"greenGroups": [1], "green": 10.0, "yellow": 3.0, "allRed": 1.5}],
                "heads": heads}]

    out = []
    for ln in lanes.values():
        out.append({k: v for k, v in ln.items() if not k.startswith("_")})
    return out, signals


def build_ground(bul, cin, cell=8.0, margin=90.0):
    """Güzergâhı izleyen zemin ızgarası (Unity koordinatı). Yükseklik: en yakın yol ekseni noktası − 0,3 m."""
    pts = []
    for ch in (bul, cin):
        s = ch.s_start
        while s <= ch.s_end:
            f, r, z, _ = ch.frame(s)
            pts.append((r, f, z))
            s += 3.0
    pts = np.array(pts)
    x0, x1 = pts[:, 0].min() - margin, pts[:, 0].max() + margin
    y0, y1 = pts[:, 1].min() - margin, pts[:, 1].max() + margin
    nx, ny = int((x1 - x0) / cell) + 1, int((y1 - y0) / cell) + 1
    gx, gy = np.meshgrid(np.linspace(x0, x1, nx), np.linspace(y0, y1, ny))
    flat = np.stack([gx.ravel(), gy.ravel()], axis=1)
    d2 = ((flat[:, None, :] - pts[None, :, :2]) ** 2).sum(-1)
    near = d2.argmin(1)
    h = pts[near, 2] - 0.3
    verts = [(-x, -y, z) for (x, y), z in zip(flat, h)]  # Unity (x, z_ileri) -> Blender (-x, -y)
    faces = []
    for j in range(ny - 1):
        for i in range(nx - 1):
            a = j * nx + i
            faces.append((a, a + 1, a + nx + 1, a + nx))
    return verts, faces


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    ap = argparse.ArgumentParser()
    ap.add_argument("--maps", required=True)
    ap.add_argument("--render-dir", default=None)
    args = ap.parse_args(argv)
    out = os.path.join(args.maps, "Hat1")
    os.makedirs(out, exist_ok=True)

    kit.clear_scene()
    img = kit.build_palette_image(os.path.join(out, "_palet_gecici.png"))
    img.pack()  # render için belleğe al, geçici dosyayı sil
    os.remove(os.path.join(out, "_palet_gecici.png"))
    mat = kit.palette_material(img)

    # modelleri bellekte üret (genişlikler ve önizleme için)
    models = {}
    widths = {}
    for name, (kind, kw) in kit.VARIANTS.items():
        o = kit.BUILDERS[kind](name, material=mat, **kw)
        models[f"Buildings/Apartmanlar/{name}"] = o
        widths[name] = {"w": o.dimensions.x, "setback": 3.2 if kw.get("garden") else 0.0}
    for name, make in yol.PIECES.items():
        rb, _ = make()
        models[f"Roads/{name}"] = rb.to_object(name, mat, recalc_normals=False)
    for name, (folder, make) in yapi.ITEMS.items():
        models[f"{folder}/{name}"] = make(name, mat)
    for o in models.values():
        o.hide_render = True
        o.hide_viewport = True

    lay, bul, cin, spawn = build_layout(widths)
    verts, faces = build_ground(bul, cin)
    mesh = bpy.data.meshes.new("Zemin_Hat1")
    mesh.from_pydata(verts, [], faces)
    uv = mesh.uv_layers.new(name="UVMap")
    for loop in uv.data:
        loop.uv = kit.palette_uv("cim")
    mesh.materials.append(mat)
    ground = bpy.data.objects.new("Zemin_Hat1", mesh)
    bpy.context.scene.collection.objects.link(ground)
    kit.export_fbx(ground, os.path.join(out, "Zemin_Hat1.fbx"))
    lay.add("Hat1/Zemin_Hat1", 0.0, 0.0, 0.0, 0.0, "Zemin")

    data = {"lineNumber": "1", "lineName": "Kızılay AVM - Atakule", "spawnPos": spawn["pos"],
            "spawnRotY": round(spawn["rotY"], 2), "items": lay.items, "stops": lay.stops,
            "lanes": lay.lanes, "signals": lay.signals}
    with open(os.path.join(out, "Hat1_Yerlesim.json"), "w", encoding="utf-8") as fh:
        json.dump(data, fh, ensure_ascii=False, indent=1)
    counts = {}
    for it in lay.items:
        counts[it["group"]] = counts.get(it["group"], 0) + 1
    print("yerleşim:", counts, "durak:", [s["name"] for s in lay.stops], "şerit:", len(lay.lanes),
          "nokta:", sum(len(l["points"]) // 3 for l in lay.lanes))
    print("bulvar uzunluğu", round(bul.s_end), "m, Cinnah uzunluğu", round(cin.s_end), "m, Atakule rakımı",
          round(cin.end[2], 1), "m")

    if args.render_dir:
        render(models, lay, ground, spawn, args.render_dir)


def render(models, lay, ground, spawn, out_dir, prefix="hat1"):
    os.makedirs(out_dir, exist_ok=True)
    scene = bpy.context.scene
    for it in lay.items:
        src = models.get(it["model"])
        if src is None:
            continue
        o = src.copy()
        o.hide_render = False
        o.hide_viewport = False
        scene.collection.objects.link(o)
        x, y, z = it["pos"]
        o.location = (-x, -z, y)
        o.rotation_euler = (0, 0, -math.radians(it["rotY"]))
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 24
    scene.cycles.use_denoising = True
    scene.render.resolution_x, scene.render.resolution_y = 1600, 900
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.exposure = -0.3
    world = bpy.data.worlds.new("Gok")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.5, 0.68, 0.92, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.6
    scene.world = world
    sun = bpy.data.lights.new("Gunes", "SUN")
    sun.energy = 3.5
    so = bpy.data.objects.new("Gunes", sun)
    so.rotation_euler = (math.radians(45), 0, math.radians(-30))
    scene.collection.objects.link(so)
    cam = bpy.data.cameras.new("Kamera")
    co = bpy.data.objects.new("Kamera", cam)
    scene.collection.objects.link(co)
    scene.camera = co
    pts = np.array([it["pos"] for it in lay.items])

    # 1) kuşbakışı
    cam.lens = 24
    cx, cz = pts[:, 0].mean(), pts[:, 2].mean()
    target = Vector((-cx, -cz, 30))
    co.location = target + Vector((520, 420, 620))
    co.rotation_euler = (target - co.location).to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = os.path.join(out_dir, f"{prefix}_kusbakisi.png")
    bpy.ops.render.render(write_still=True)
    # 2) sürücü gözü: Kızılay AVM durağından ileri bakış
    cam.lens = 26
    x, y, z = spawn["pos"]
    co.location = (-(x - 0.6), -(z + 5.0), y + 2.2)
    th = math.radians(spawn["rotY"])
    fwd = Vector((-math.sin(th), -math.cos(th), 0))
    co.rotation_euler = (fwd + Vector((0, 0, 0.04))).to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = os.path.join(out_dir, f"{prefix}_surucu.png")
    bpy.ops.render.render(write_still=True)
    # 3) sondan bir önceki duraktan son durağa (Hat 1: Cinnah → Atakule) bakış
    prev, last = lay.stops[-2], lay.stops[-1]
    x, y, z = prev["pos"]
    th = math.radians(prev["rotY"])
    co.location = (-(x - 4.0 * math.sin(th)), -(z - 4.0 * math.cos(th)), y + 2.5)
    ax, ay, az = last["pos"]
    target = Vector((-ax, -az, ay + 60))
    co.rotation_euler = (target - co.location).to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = os.path.join(out_dir, f"{prefix}_son_durak.png")
    bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    main()
