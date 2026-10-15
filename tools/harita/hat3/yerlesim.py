"""
A6/A7 — Hat 3 yerleşim dosyası (HaritaKurucu şeması + parçalar) ve mini harita dokusu.

Hat başına yerleşim (kok.HATLAR): Maps/Hat3/Hat3_Yerlesim.json (408 Kızılay → Güneşevler), Maps/Hat1/Hat1_Yerlesim.json
(408 Kızılay → Atakule). Harita ortak: öğeler, bütün hatların şeritleri ve ışıkları her dosyada; duraklar ve başlangıç hatta özgü.
    lineNumber "408", lineName, spawnPos, spawnRotY
    items     hazır modeller (simgeler.py), lambalar (Lambalar), durak sundurmaları (Duraklar)
    stops     18 durak: güzergâhta durağın izdüşümü, sağ şeridin kaldırıma yakın yarısında (kaldırım ≤ 7 m sağda)
    lanes     güzergâh yönü: Gidis_1 (en sağ şerit, kesintisiz; trafik ışıklarında bölünür, çıkışla bağlı),
              Gidis_2 (2+ şeritli bölümler); karşı yön Donus_1 (ayrık yolda karşı yol, tek yolda solda)
    signals   OSM trafik ışıkları (güzergâhtan ≤ 15 m, 30 m kümeleme): güzergâh yönü grup 0, çapraz grup 1
    parcalar  [{ad, model, pos, uzak}] — Maps/Hat3/Parcalar/*.fbx (harita_hat3.py)
    haritaDokusu {dosya, min, pikselMetre} — Maps/Hat3/Hat3_Harita.png (MiniHarita)
Kullanım: python -m hat3.yerlesim --maps AnkaraBusSimulator/Assets/_Project/Maps
"""
import argparse
import glob
import json
import math
import os
import pickle
import re

import numpy as np
import shapely
from PIL import Image, ImageDraw
from shapely.geometry import LineString, Point

from . import kok
from .mesh import PARCA, cokgenler
from .ufuk import PARCA_UFUK
from .yollar import serit_bilgisi
from .yuzeyler import SonArazi, Yukseklik

ADIM = 5.0
SERIT = 3.4
ISIK_YESIL = (30.0, 15.0)


def hat_bilesenleri(o, g, geo, d, yh, ar, on_ek="", nostaljik_s=2150.0):
    """Bir hattın (g: hat408_guzergah.py çıktısı) şeritleri, ışıkları, durakları, sundurma ve lambaları, başlangıcı.
    on_ek: ikinci hattın şerit/ışık adlarına önek (Unity'de adlar tekil olmalı)."""
    A, S = geo["A"], geo["S"]
    guz = LineString([kok.yerel(*p) for p in g["yol"]])
    guz_id = set(g["osm_yollar"])

    # güzergâh yollarının şerit bilgisi (s'ye göre)
    guz_yol = {}
    for sr in geo["seritler"]:
        if sr["id"] in guz_id:
            guz_yol[sr["id"]] = sr
    yol_geo = [(LineString(d["dugum_xz"][sr["d"]]), sr) for sr in guz_yol.values()]
    agac_y = shapely.STRtree([gg for gg, _ in yol_geo])

    L = guz.length
    ss = np.arange(0, L, ADIM)
    P = np.array([guz.interpolate(s).coords[0] for s in ss])
    T = np.gradient(P, axis=0)
    T /= np.linalg.norm(T, axis=1, keepdims=True)
    N = np.stack([T[:, 1], -T[:, 0]], 1)          # sağ

    def isin(p, yon, uzunluk=45.0):
        """p'den yon'e ışın; A ile kesişim aralıkları [(a, b)] (m, p'den)."""
        q = (p[0] + yon[0] * uzunluk, p[1] + yon[1] * uzunluk)
        x = LineString([p, q]).intersection(A)
        out = []
        for gg in getattr(x, "geoms", [x]):
            if gg.is_empty or gg.geom_type != "LineString":
                continue
            c = np.asarray(gg.coords)
            t = np.dot(c - p, yon)
            out.append((float(t.min()), float(t.max())))
        return sorted(out)

    sag_kenar = np.zeros(len(ss))
    sol_kenar = np.zeros(len(ss))
    karsi = np.full(len(ss), np.nan)
    refuj_orta = np.full(len(ss), np.nan)
    tek = np.zeros(len(ss), bool)
    nser = np.ones(len(ss), int)
    W = np.zeros(len(ss))
    for i, p in enumerate(P):
        sr = yol_geo[agac_y.nearest(Point(p))][1]
        n, gen, kal, tk = serit_bilgisi({"lanes": str(sr["n"]), "oneway": "yes" if sr["tek"] else "no"}, sr["sinif"])
        tek[i], nser[i], W[i] = sr["tek"], sr["n"], sr["genislik"]
        sag = isin(p, N[i])
        sag_kenar[i] = sag[0][1] if sag and sag[0][0] < 0.5 else W[i] / 2
        sol = isin(p, -N[i])
        sol_kenar[i] = sol[0][1] if sol and sol[0][0] < 0.5 else W[i] / 2
        if sr["tek"] and len(sol) >= 2 and sol[1][0] - sol[0][1] < 25 and sol[1][1] - sol[1][0] >= 3:
            karsi[i] = -(sol[1][1] - 1.8)
            if sol[1][0] - sol[0][1] > 1.0:
                refuj_orta[i] = -(sol[0][1] + sol[1][0]) / 2
        elif not sr["tek"]:
            karsi[i] = -max(1.7, min(sol_kenar[i] - 1.8, W[i] / 2 - 1.7))
    # güzergâh şeridi: sağ kenardan 1,8 m içeride (otobüs sağ şeritte)
    of1 = np.clip(sag_kenar - 1.8, 0.5, None)
    # yumuşatma (kavşaklarda ışın sıçramaları)
    def yumusat(a, k=7):
        a = a.copy()
        m = ~np.isnan(a)
        if m.sum() < 3:
            return a
        b = np.where(m, a, 0.0)
        w = np.convolve(m.astype(float), np.ones(k), "same")
        s_ = np.convolve(b, np.ones(k), "same")
        out = np.where(w > 0, s_ / np.maximum(w, 1), np.nan)
        out[~m] = np.nan
        return out
    of1 = yumusat(of1)
    karsi = yumusat(karsi)

    def nokta(i, of):
        p = P[i] + N[i] * of
        return p

    def y_at(xz):
        xz = np.asarray(xz)
        return yh(xz[:, 0], xz[:, 1])

    # --- trafik ışıkları: OSM düğümleri güzergâhta, s'ye göre kümelenir
    isiklar = []
    for nid, t in o["tagged_nodes"].items():
        if t.get("highway") == "traffic_signals" and nid in o["xy"]:
            p = Point(o["xy"][nid])
            if guz.distance(p) < 15:
                isiklar.append(guz.project(p))
    isiklar.sort()
    kume = []
    for s in isiklar:
        if kume and s - kume[-1][-1] < 30:
            kume[-1].append(s)
        else:
            kume.append([s])
    isik_s = [min(k) for k in kume if 30 < min(k) < L - 40]
    print(f"  trafik ışığı kavşağı {len(isik_s)}")

    signals, lanes, items = [], [], []
    for k, s0 in enumerate(isik_s):
        i = int(s0 / ADIM)
        heads = []
        # güzergâh yönü: sağ kaldırım kenarında, gelen trafiğe bakar
        p = P[i] + N[i] * (sag_kenar[i] + 0.8)
        yaw = math.degrees(math.atan2(T[i][0], T[i][1]))
        heads.append({"model": "Props/Trafik_Lambasi", "pos": [round(p[0], 2), round(float(ar.y(*p)[0]) + 0.15, 2), round(p[1], 2)],
                      "rotY": round((yaw + 180) % 360, 1), "group": 0})
        j = min(i + 6, len(P) - 1)
        if not np.isnan(karsi[j]):
            q = P[j] - N[j] * (abs(karsi[j]) + 2.6)
            heads.append({"model": "Props/Trafik_Lambasi", "pos": [round(q[0], 2), round(float(ar.y(*q)[0]) + 0.15, 2), round(q[1], 2)],
                          "rotY": round(yaw % 360, 1), "group": 0})
        signals.append({"name": f"{on_ek}K{k + 1:02d}", "heads": heads,
                        "phases": [{"greenGroups": [0], "green": ISIK_YESIL[0], "yellow": 3.0, "allRed": 2.0},
                                   {"greenGroups": [1], "green": ISIK_YESIL[1], "yellow": 3.0, "allRed": 2.0}]})

    # --- güzergâh şeritleri: ışıklarda bölünmüş Gidis_1 parçaları
    sinirlar = [0.0] + isik_s + [L]

    def serit_parcasi(ad, s_a, s_b, ofset, ters=False):
        ia, ib = int(s_a / ADIM), min(int(s_b / ADIM) + 1, len(P))
        idx = [i for i in range(ia, ib) if not np.isnan(ofset[i])]
        if len(idx) < 4:
            return None
        pts = np.array([nokta(i, ofset[i]) for i in idx])
        if ters:
            pts = pts[::-1]
        ys = y_at(pts) + 0.02
        return {"name": ad, "limitKmh": 50.0, "points": [round(float(v), 2) for xyz in zip(pts[:, 0], ys, pts[:, 1]) for v in xyz],
                "exits": [], "stopLine": -1.0, "group": 0, "signal": ""}

    gidis = []
    for k in range(len(sinirlar) - 1):
        ln = serit_parcasi(f"{on_ek}Gidis_1_{k:02d}", sinirlar[k], sinirlar[k + 1], of1)
        if ln is None:
            continue
        if k < len(isik_s):
            n_pts = len(ln["points"]) // 3
            uz = sum(math.dist(ln["points"][3 * a:3 * a + 3], ln["points"][3 * a + 3:3 * a + 6]) for a in range(n_pts - 1))
            ln["signal"] = f"{on_ek}K{k + 1:02d}"
            ln["stopLine"] = round(max(uz - 4.0, 0.0), 1)
        gidis.append(ln)
    for a, b in zip(gidis, gidis[1:]):
        n_pts = len(a["points"]) // 3
        uz = sum(math.dist(a["points"][3 * q:3 * q + 3], a["points"][3 * q + 3:3 * q + 6]) for q in range(n_pts - 1))
        a["exits"].append({"target": b["name"], "at": round(uz - 0.5, 1), "targetAt": 0.0, "probability": 1.0})
    lanes += gidis
    # Gidis_2: 2+ şeritli (aynı yönde) bölümler, en sağ şeridin solunda
    iki = np.where((tek & (nser >= 2)) | (~tek & (nser >= 4)), of1 - SERIT, np.nan)
    bas = None
    parca_no = 0
    for i in range(len(P) + 1):
        var = i < len(P) and not np.isnan(iki[i])
        if var and bas is None:
            bas = i
        elif not var and bas is not None:
            if (i - bas) * ADIM >= 150:
                ln = serit_parcasi(f"{on_ek}Gidis_2_{parca_no:02d}", bas * ADIM, (i - 1) * ADIM, iki)
                if ln:
                    lanes.append(ln)
                    parca_no += 1
            bas = None
    # karşı yön
    bas = None
    parca_no = 0
    for i in range(len(P) + 1):
        var = i < len(P) and not np.isnan(karsi[i])
        if var and bas is None:
            bas = i
        elif not var and bas is not None:
            if (i - bas) * ADIM >= 80:
                ln = serit_parcasi(f"{on_ek}Donus_1_{parca_no:02d}", bas * ADIM, (i - 1) * ADIM, karsi, ters=True)
                if ln:
                    lanes.append(ln)
                    parca_no += 1
            bas = None
    # çapraz trafik: ışıklı kavşakta güzergâhı kesen OSM yolları boyunca ±CAPRAZ m, grup 1
    CAPRAZ = 120.0
    cap_sayi = 0
    tum_yollar = [(LineString(d["dugum_xz"][sr["d"]]), sr) for sr in geo["seritler"]
                  if sr["id"] not in guz_id and sr["sinif"] in ("trunk", "primary", "secondary", "tertiary", "unclassified",
                                                            "residential", "primary_link", "secondary_link", "trunk_link")]
    agac_t = shapely.STRtree([gg for gg, _ in tum_yollar])
    for k, s0 in enumerate(isik_s):
        i = min(int((s0 + 10) / ADIM), len(P) - 1)
        c = Point(P[i])
        yaw_r = T[i]
        goruldu = set()
        for j in agac_t.query(c.buffer(25)):
            gg, sr = tum_yollar[j]
            if sr["id"] in goruldu or gg.distance(c) > 25:
                continue
            # güzergâha dik sayılır (yön farkı > 45°)
            t0 = gg.project(c)
            a_, b_ = gg.interpolate(max(t0 - 5, 0)), gg.interpolate(min(t0 + 5, gg.length))
            yv = np.array([b_.x - a_.x, b_.y - a_.y])
            if np.linalg.norm(yv) < 1 or abs(np.dot(yv / np.linalg.norm(yv), yaw_r)) > 0.7:
                continue
            goruldu.add(sr["id"])
            yonler = [False] if sr["tek"] else [False, True]
            for ters in yonler:
                ss_ = np.arange(max(t0 - CAPRAZ, 0), min(t0 + CAPRAZ, gg.length), ADIM)
                if len(ss_) < 6:
                    continue
                pts = np.array([gg.interpolate(v).coords[0] for v in ss_])
                if sr["ters"]:
                    pts = pts[::-1]
                if ters:
                    pts = pts[::-1]
                tt = np.gradient(pts, axis=0)
                tt /= np.maximum(np.linalg.norm(tt, axis=1, keepdims=True), 1e-9)
                nn = np.stack([tt[:, 1], -tt[:, 0]], 1)
                of = (sr["genislik"] / 2 - 1.7) if sr["tek"] else sr["genislik"] / 4
                pts = pts + nn * max(of, 0.0)
                ys = y_at(pts) + 0.02
                uz = np.concatenate([[0], np.cumsum(np.linalg.norm(np.diff(pts, axis=0), axis=1))])
                # durma çizgisi: güzergâh yolunun kenarından 4 m önce
                dmin = int(np.argmin(np.linalg.norm(pts - P[i], axis=1)))
                durma = max(uz[dmin] - (W[i] / 2 + 6.0), 0.0)
                if durma < 10:
                    continue
                ad = f"{on_ek}Capraz_{k + 1:02d}_{sr['id']}_{int(ters)}"
                # ışık başı: çapraz yolun sağında, durma çizgisinde, gelen trafiğe bakar (şerit kısaltılmadan önce)
                q = pts[dmin] + nn[dmin] * 3.5 - tt[dmin] * (W[i] / 2 + 5)
                yawq = math.degrees(math.atan2(tt[dmin][0], tt[dmin][1]))
                ln = {"name": ad, "limitKmh": 40.0, "points": [], "exits": [], "stopLine": round(float(durma), 1),
                      "group": 1, "signal": f"{on_ek}K{k + 1:02d}"}
                # T kavşak (yol güzergâhta bitiyor): durma çizgisinden sonra sağa dönüp en yakın uygun şeride bağlanır
                devam = uz[-1] - uz[dmin]
                if devam < 30:
                    son = int(np.searchsorted(uz, durma + 3.0))
                    pts, ys, uz = pts[:son + 1], ys[:son + 1], uz[:son + 1]
                    p0, t0v = pts[-1], tt[min(son, len(tt) - 1)]
                    hedef = None
                    for hl in lanes:
                        if not (hl["name"].startswith(on_ek + "Gidis_1") or hl["name"].startswith(on_ek + "Donus_1")):
                            continue
                        hp = np.array(hl["points"]).reshape(-1, 3)[:, [0, 2]]
                        dd = np.linalg.norm(hp - p0, axis=1)
                        m = int(np.argmin(dd))
                        if dd[m] > 30 or m + 4 >= len(hp):
                            continue
                        ht = hp[min(m + 1, len(hp) - 1)] - hp[max(m - 1, 0)]
                        ht /= np.linalg.norm(ht) or 1
                        capraz = t0v[0] * ht[1] - t0v[1] * ht[0]
                        if capraz < -0.5 and (hedef is None or dd[m] < hedef[0]):
                            hedef = (dd[m], hl, m, hp, ht)
                    if hedef is not None:
                        _, hl, m, hp, ht = hedef
                        huz = np.concatenate([[0], np.cumsum(np.linalg.norm(np.diff(hp, axis=0), axis=1))])
                        m2 = int(np.searchsorted(huz, huz[m] + 12.0))
                        m2 = min(m2, len(hp) - 1)
                        q2 = hp[m2]
                        kontrol = p0 + t0v * np.linalg.norm(q2 - p0) * 0.5
                        bz = np.array([(1 - u) ** 2 * p0 + 2 * (1 - u) * u * kontrol + u ** 2 * q2 for u in np.linspace(0, 1, 9)])
                        bys = y_at(bz) + 0.02
                        donus = {"name": ad + "_Don", "limitKmh": 25.0,
                                 "points": [round(float(v), 2) for xyz in zip(bz[:, 0], bys, bz[:, 1]) for v in xyz],
                                 "exits": [{"target": hl["name"], "at": round(float(np.sum(np.linalg.norm(np.diff(bz, axis=0), axis=1))) - 0.3, 1),
                                            "targetAt": round(float(huz[m2]), 1), "probability": 1.0}],
                                 "stopLine": -1.0, "group": 0, "signal": ""}
                        ln["exits"].append({"target": donus["name"], "at": round(float(uz[-1]) - 0.3, 1), "targetAt": 0.0,
                                            "probability": 1.0})
                        lanes.append(donus)
                ln["points"] = [round(float(v), 2) for xyz in zip(pts[:, 0], ys, pts[:, 1]) for v in xyz]
                lanes.append(ln)
                signals[k]["heads"].append({"model": "Props/Trafik_Lambasi",
                                            "pos": [round(q[0], 2), round(float(ar.y(*q)[0]) + 0.15, 2), round(q[1], 2)],
                                            "rotY": round((yawq + 180) % 360, 1), "group": 1})
                cap_sayi += 1
    print(f"  şerit {len(lanes)} (gidiş {len(gidis)}, çapraz {cap_sayi})")

    # --- duraklar
    stops = []
    for dz in g["duraklar"]:
        x, z = kok.yerel(dz["lat"], dz["lon"])
        s = guz.project(Point(x, z))
        i = min(int(s / ADIM), len(P) - 1)
        p = nokta(i, sag_kenar[i] - 1.6)
        yaw = math.degrees(math.atan2(T[i][0], T[i][1]))
        stops.append({"name": dz["ad"], "pos": [round(p[0], 2), round(float(y_at([p])[0]) + 0.05, 2), round(p[1], 2)],
                      "rotY": round(yaw % 360, 1), "radius": 10.0})
        # sundurma: kaldırımda, kenardan 2,4 m içeride
        q = nokta(i, sag_kenar[i] + 2.4)
        items.append({"model": "Props/EGO_Durak", "pos": [round(q[0], 2), round(float(ar.y(*q)[0]) + 0.15, 2), round(q[1], 2)],
                      "rotY": round((yaw - 90) % 360, 1), "group": "Duraklar"})
    # --- lambalar: refüj varsa ortasında, yoksa sağ kaldırımda; Atatürk Bulvarı'nda nostaljik
    son = -1e9
    durak_s = [guz.project(Point(*kok.yerel(dz["lat"], dz["lon"]))) for dz in g["duraklar"]]
    for i in range(len(P)):
        s = ss[i]
        if s - son < 33 or any(abs(s - d_) < 15 for d_ in durak_s):
            continue
        yaw = math.degrees(math.atan2(T[i][0], T[i][1]))
        nostaljik = s < nostaljik_s
        if not np.isnan(refuj_orta[i]):
            p = nokta(i, refuj_orta[i])
            model = "SKP/Props/Lamba_Nostaljik" if nostaljik else "Props/Lamba_Bulvar"
            rot = yaw + 90
        else:
            p = nokta(i, sag_kenar[i] + 0.7)
            if not shapely.contains_xy(S, p[0], p[1]):
                continue
            model = "Props/Lamba_Bulvar"
            rot = yaw + 90
        items.append({"model": model, "pos": [round(p[0], 2), round(float(ar.y(*p)[0]) + 0.15, 2), round(p[1], 2)],
                      "rotY": round(rot % 360, 1), "group": "Lambalar"})
        son = s
    print(f"  lamba {sum(1 for it in items if it['group'] == 'Lambalar')}, durak {len(stops)}")
    i0 = 2
    yaw0 = math.degrees(math.atan2(T[i0][0], T[i0][1]))
    sp = nokta(i0, of1[i0])
    spawn = ([round(sp[0], 2), round(float(y_at([sp])[0]) + 0.3, 2), round(sp[1], 2)], round(yaw0 % 360, 1))
    return lanes, signals, stops, items, spawn


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--maps", required=True)
    args = ap.parse_args()
    o = kok.osm()
    geo = pickle.load(open(os.path.join(kok.ISLEM, "yollar_geo.pkl"), "rb"))
    A, S = geo["A"], geo["S"]
    d = np.load(os.path.join(kok.ISLEM, "arazi.npz"))
    yh = Yukseklik(d["dugum_xz"], d["dugum_y"], d["dugum_fark"])
    ar = SonArazi()
    shapely.prepare(A)
    shapely.prepare(S)
    # hat başına bileşenler; harita ortak: bütün hatların şeritleri, ışıkları, sundurma ve lambaları her sahnede
    hatlar = kok.hatlar()
    bilesen = {}
    for k, (ad, g) in enumerate(hatlar.items()):
        print(f"  hat {ad}")
        bilesen[ad] = hat_bilesenleri(o, g, geo, d, yh, ar, on_ek="" if k == 0 else "A_",
                                      nostaljik_s=2150.0 if ad == "408_gunesevler" else 2650.0)
    items = json.load(open(os.path.join(kok.ISLEM, "simge_items.json"), encoding="utf-8"))
    lanes, signals = [], []
    for ad, (ln, sg, st, it, sp) in bilesen.items():
        lanes += ln
        signals += sg
        items += it

    # --- parçalar
    parcalar = []
    klasor = os.path.join(kok.ISLEM, "parcalar")
    anahtarlar = set()
    for f in glob.glob(os.path.join(klasor, "*.pkl")):
        m = re.match(r"(yol|bina|agac|simge|donati)_(-?\d+)_(-?\d+)\.pkl", os.path.basename(f))
        if m:
            anahtarlar.add((int(m.group(2)), int(m.group(3))))
    for i, j in sorted(anahtarlar):
        parcalar.append({"ad": f"H3_{i}_{j}", "model": f"Hat3/Parcalar/H3_{i}_{j}",
                         "pos": [(i + 0.5) * PARCA, 0.0, (j + 0.5) * PARCA], "uzak": False})
    for f in sorted(glob.glob(os.path.join(klasor, "ufuk_*.pkl"))):
        m = re.match(r"ufuk_(-?\d+)_(-?\d+)\.pkl", os.path.basename(f))
        i, j = int(m.group(1)), int(m.group(2))
        parcalar.append({"ad": f"H3U_{i}_{j}", "model": f"Hat3/Parcalar/H3U_{i}_{j}",
                         "pos": [(i + 0.5) * PARCA_UFUK, 0.0, (j + 0.5) * PARCA_UFUK], "uzak": True})

    # --- mini harita dokusu (0,4 px/m): yeşil, su, yollar, kaldırım, binalar
    pm = 0.4
    x0, z0, x1, z1 = geo["koridor"].bounds
    w, h_ = int((x1 - x0) * pm) + 1, int((z1 - z0) * pm) + 1
    im = Image.new("RGB", (w, h_), (38, 46, 41))
    dr = ImageDraw.Draw(im)

    def ciz(gg, renk):
        for pg in cokgenler(gg):
            dr.polygon([((a - x0) * pm, (z1 - b) * pm) for a, b in pg.exterior.coords], fill=renk)
            for hh in pg.interiors:
                dr.polygon([((a - x0) * pm, (z1 - b) * pm) for a, b in hh.coords], fill=(38, 46, 41))

    for wid, wy in o["ways"].items():
        t = wy["t"]
        if t.get("leisure") in ("park", "garden") or t.get("landuse") in ("grass", "cemetery", "forest"):
            ns = [o["xy"][n] for n in wy["n"] if n in o["xy"]]
            if len(ns) >= 4:
                ciz(shapely.Polygon(ns).buffer(0), (46, 78, 52))
    ciz(S, (110, 112, 116))
    ciz(A, (150, 154, 160))
    for b in pickle.load(open(os.path.join(kok.ISLEM, "binalar.pkl"), "rb")):
        ciz(b["taban"], (84, 86, 94))
    # Unity'de doku satırı 0 = alt (z0) → dikey çevir
    im = im.transpose(Image.FLIP_TOP_BOTTOM)
    os.makedirs(os.path.join(args.maps, "Hat3"), exist_ok=True)
    im.save(os.path.join(args.maps, "Hat3", "Hat3_Harita.png"))

    for ad, (ln, sg, stops, it, (sp, sp_yaw)) in bilesen.items():
        h = kok.HATLAR[ad]
        yerlesim = {
            "lineNumber": h["numara"], "lineName": h["ad"], "spawnPos": sp, "spawnRotY": sp_yaw,
            "items": items, "stops": stops, "lanes": lanes, "signals": signals, "parcalar": parcalar,
            "haritaDokusu": {"dosya": "Hat3/Hat3_Harita.png", "min": [round(x0, 2), round(z0, 2)], "pikselMetre": pm},
        }
        os.makedirs(os.path.join(args.maps, h["klasor"]), exist_ok=True)
        yol = os.path.join(args.maps, h["klasor"], h["dosya"])
        json.dump(yerlesim, open(yol, "w", encoding="utf-8"), ensure_ascii=False, separators=(",", ":"))
        print(f"{yol}: {len(items)} öğe, {len(stops)} durak, {len(lanes)} şerit, {len(signals)} ışık, {len(parcalar)} parça; "
              f"harita dokusu {w}×{h_}")

if __name__ == "__main__":
    main()
