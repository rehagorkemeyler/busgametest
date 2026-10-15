"""
A2 — Yol ağı geometrisi (2B çokgenler + yükseklik).

Koridor: güzergâhın KORIDOR m çevresi. İçindeki araç yolları (OSM) şerit sayısı ve sınıfına göre genişlikle tamponlanır:
- asfalt  A = ⋃ şerit tamponları (zemin seviyesindeki parçalar); kavşaklarda köşeler KOSE_R yarıçapıyla yuvarlanır
- kaldırım S = ⋃ (şerit + kaldırım) tamponları − A  (ayrık yollarda arada kalan dar refüj de kaldırım/refüj olur)
- yaya alanları (highway=pedestrian alanları, meydanlar) kaldırıma eklenir
- katmanlı parçalar (köprü üstü, alt geçit; arazi.py'de hedef farkı ≠ 0) ayrı şeritler: zemin çokgenlerine girmez
Yükseklik: zemin seviyesindeki yol düğümlerinden ters uzaklık ağırlıklı (k=6); kaldırım +BORDUR.

Çıktı <ISLEM>/yollar_geo.pkl: {"A", "S", "refuj", "katmanli": [{id, sinif, xz (n,2), y (n,), genislik, fark}],
                               "koridor", "kavsaklar": [(x, z, r)], "seritler": [...]}
Kullanım: python -m hat3.yollar   (tools/harita içinden; Blender'ın python'u: shapely, scipy)
"""
import json
import math
import os
import pickle

import numpy as np
import shapely
from scipy.spatial import cKDTree
from shapely import ops
from shapely.geometry import LineString, MultiPolygon, Point, Polygon
from shapely.strtree import STRtree

from . import kok

KORIDOR = 450.0
KOSE_R = 5.0           # kavşak köşe yuvarlatma
BORDUR = 0.15
REFUJ_EN = 8.0         # bu genişlikten dar, iki asfalt arasında kalan uzun şerit = refüj
ATLA_SERVIS = ("parking_aisle", "driveway")
# OSM'de kaldırım genişliği yok; ana caddelerde gerçeğe yakın (uydu görüntüsünden ölçülerek)
GENIS_KALDIRIM = {"Atatürk Bulvarı": 7.0, "Ulus Meydanı": 7.0, "Erbakan Meydanı": 6.0, "Çankırı Caddesi": 5.0,
                  "Cumhuriyet Caddesi": 5.0, "Anafartalar Caddesi": 5.0, "Şehit Ömer Halisdemir Bulvarı": 4.5,
                  "Talatpaşa Bulvarı": 4.5, "Celal Bayar Bulvarı": 4.5}


def serit_bilgisi(t, sinif, guzergah=False):
    """(şerit sayısı, toplam asfalt genişliği, kaldırım genişliği, tek yön mü).
    guzergah: otobüs hattının geçtiği yol — tek yönlüde en az 2 şerit / 7 m (OSM'de şerit sayısı yazılmayan tek yönlü sokaklar
    3,25 m çıkıyordu: 126. Sokak; otobüs şeridi asfalta sığmıyordu)."""
    varsay, sw, kal = kok.ARAC_YOLU[sinif]
    tek = t.get("oneway") in ("yes", "1", "-1", "true") or t.get("junction") == "roundabout" or sinif.endswith("_link")
    try:
        n = int(float(t["lanes"]))
    except (KeyError, ValueError):
        n = max(1, varsay // 2) if tek else varsay
    if sinif == "service":
        n, sw = 1, 4.0 if t.get("service") != "alley" else 3.5
    try:
        gen = float(t["width"].replace("m", "").strip())
    except (KeyError, ValueError):
        gen = n * sw
    if guzergah and sinif != "service":
        if tek:
            n = max(n, 2)
        gen = max(gen, n * sw, 7.0 if tek else 6.5)
    ad = t.get("name", "")
    if kal > 0 and ad in GENIS_KALDIRIM:
        kal = GENIS_KALDIRIM[ad]
    if t.get("sidewalk") == "no":
        kal = 0.0
    return n, gen, kal, tek


def yuvarlak(geo):
    return geo.buffer(0)


def main():
    import time
    t0 = time.time()
    o = kok.osm()
    d = np.load(os.path.join(kok.ISLEM, "arazi.npz"))
    P, Y, F = d["dugum_xz"], d["dugum_y"], d["dugum_fark"]
    yollar = json.load(open(os.path.join(kok.ISLEM, "yollar.json"), encoding="utf-8"))
    g = kok.guzergah()
    guz = kok.guzergah_cizgisi()
    koridor = guz.buffer(KORIDOR, quad_segs=16)

    asfalt, dis, katmanli, seritler = [], [], [], []
    guz_asfalt = []          # zemindeki güzergâh asfaltı: katmanlı komşunun yarma kesimi bunu silmez (Akay)
    derece = {}
    for w in yollar:
        if w["sinif"] == "ray":
            continue
        t = w["t"]
        if w["sinif"] == "service" and t.get("service") in ATLA_SERVIS:
            continue
        xz = P[w["d"]]
        geo = LineString(xz)
        if not geo.intersects(koridor):
            continue
        n, gen, kal, tek = serit_bilgisi(t, w["sinif"], w["guzergah"])
        f = np.abs(F[w["d"]]) > 0.3
        seg_k = f[:-1] | f[1:]                  # parça katmanlı: uçlarından biri katmanlı
        bas = 0
        for i in range(1, len(seg_k) + 1):
            if i < len(seg_k) and seg_k[i] == seg_k[bas]:
                continue
            idx = w["d"][bas:i + 1]             # parçalar bas..i-1 → düğümler bas..i
            if seg_k[bas]:
                katmanli.append({"id": w["id"], "sinif": w["sinif"], "xz": P[idx], "y": Y[idx], "fark": F[idx],
                                 "genislik": gen, "kaldirim": kal, "ad": t.get("name")})
            else:
                pg = LineString(P[idx])
                asfalt.append(pg.buffer(gen / 2, cap_style="flat", join_style="round"))
                if w["guzergah"]:
                    guz_asfalt.append(asfalt[-1])
                if kal > 0:
                    dis.append(pg.buffer(gen / 2 + kal, cap_style="flat", join_style="round"))
            bas = i
        for k in (w["d"][0], w["d"][-1]):
            derece[k] = derece.get(k, 0) + 1
        for k in w["d"][1:-1]:
            derece[k] = derece.get(k, 0) + 2
        seritler.append({"id": w["id"], "sinif": w["sinif"], "ad": t.get("name"), "d": w["d"], "n": n,
                         "genislik": gen, "tek": tek, "guzergah": w["guzergah"], "ters": t.get("oneway") == "-1"})

    # yaya alanları
    yaya = []
    for wid, w in o["ways"].items():
        t = w["t"]
        if (t.get("highway") == "pedestrian" and t.get("area") == "yes") or t.get("place") == "square" or \
                t.get("area:highway") == "traffic_island":
            ns = [o["xy"][n] for n in w["n"] if n in o["xy"]]
            if len(ns) >= 4:
                pg = Polygon(ns).buffer(0)
                if pg.intersects(koridor):
                    yaya.append(pg)

    print(f"  {len(asfalt)} asfalt parçası ({time.time() - t0:.0f} sn)")
    A = ops.unary_union(asfalt)
    print(f"  birleşim ({time.time() - t0:.0f} sn)")
    # kavşaklar: 3+ yolun buluştuğu düğümler (yalnız zemin seviyesi)
    kavsak = []
    for k, dd in derece.items():
        if dd >= 3 and abs(F[k]) < 0.3:
            kavsak.append(Point(P[k]).buffer(18.0))
    kav = ops.unary_union(kavsak)
    kapali = A.buffer(KOSE_R, join_style="round").buffer(-KOSE_R, join_style="round")
    # köşe dolgusu katmanlı yolların (köprü, alt geçit yarması) izine taşmasın: tünel ağzının üstüne "kapak" oluyordu
    kat_iz = ops.unary_union([LineString(kp["xz"]).buffer(kp["genislik"] / 2 + max(min(kp["kaldirim"], 2.0), 0.6) + 0.5, cap_style="flat")
                              for kp in katmanli]) if katmanli else Polygon()
    A = ops.unary_union([A, kapali.intersection(kav).difference(kat_iz)]).buffer(0)
    print(f"  kavşak köşeleri ({time.time() - t0:.0f} sn)")
    S = ops.unary_union(dis + yaya).difference(A).buffer(0)
    print(f"  kaldırım ({time.time() - t0:.0f} sn)")
    # katmanlı yolların alçak kısmı (zeminden 0,3–5,5 m: rampa, dolgu, açık yarma) zemindeki asfalt ve kaldırımın üstüne/altına
    # taşmasın: izi zemin çokgenlerinden kesilir. Yüksek tabliyenin (> 5,5 m) altı ve kapalı tünelin (tunnel=yes) üstü kalır.
    # Derin açık yarma (tünel etiketsiz, zeminden > 5,5 m aşağıda; ör. Meclis alt geçidine iniş) de kesilir: yandaki zemin yollarının
    # asfalt/kaldırımı yarmanın üstünü tavan gibi örtüyordu. Yarmanın üstünden dik geçen yollar (köprü gibi) kesilmez.
    etiket = {w["id"]: w["t"] for w in yollar}
    kes, derin = [], []
    for kp in katmanli:
        f = np.abs(np.asarray(kp["fark"], float))
        tunel = bool(etiket.get(kp["id"], {}).get("tunnel"))
        r = kp["genislik"] / 2 + max(min(kp["kaldirim"], 2.0), 0.6) + 0.3     # yuzeyler.KOPRU_KALDIRIM
        for i in range(len(kp["xz"]) - 1):
            fi = max(f[i], f[i + 1])
            if tunel and min(kp["fark"][i], kp["fark"][i + 1]) < 0:
                continue
            if fi >= 5.5:
                if max(kp["fark"][i], kp["fark"][i + 1]) < 0:
                    derin.append(LineString(kp["xz"][i:i + 2]))
                    kes.append(derin[-1].buffer(r, cap_style="flat"))
                continue
            kes.append(LineString(kp["xz"][i:i + 2]).buffer(r, cap_style="flat"))
    if derin:
        dagac = shapely.STRtree(derin)
        ustten = []
        for w in yollar:
            if w["sinif"] == "ray" or w["katman"] != 0:
                continue
            _, gen_w, kal_w, _ = serit_bilgisi(w["t"], w["sinif"], w["guzergah"])
            xz = P[w["d"]]
            for i in range(len(xz) - 1):
                sg = LineString(xz[i:i + 2])
                u = xz[i + 1] - xz[i]
                for k in dagac.query(sg, predicate="intersects"):
                    v = np.diff(np.asarray(derin[k].coords), axis=0)[0]
                    c = abs(float(np.dot(u, v))) / (np.linalg.norm(u) * np.linalg.norm(v) + 1e-9)
                    if c < 0.7 and min(abs(F[w["d"][i]]), abs(F[w["d"][i + 1]])) < 0.3:
                        ustten.append(sg.buffer(gen_w / 2 + kal_w + 2.0, cap_style="flat"))
                        break
        print(f"  derin yarma {len(derin)} parça, üstünden geçen {len(ustten)} yol parçası korunur")
        if ustten:
            U_ = ops.unary_union(ustten)
            kes = [k_.difference(U_) for k_ in kes]
    if kes:
        Y_ = ops.unary_union(kes)
        if guz_asfalt:
            Y_ = Y_.difference(ops.unary_union(guz_asfalt).buffer(-0.2))
        A = A.difference(Y_).buffer(0)
        S = S.difference(Y_).buffer(0)
    A = A.intersection(koridor)
    S = S.intersection(koridor)
    # koridor sınırında kalan asfalt kırıntıları (sınıra değen < 150 m² parçalar) atılır
    sinir_ = koridor.boundary.buffer(2.0)
    A = shapely.union_all([pg for pg in (A.geoms if isinstance(A, MultiPolygon) else [A])
                           if not (pg.area < 150.0 and pg.intersects(sinir_))])
    # refüj: kaldırımın iki asfalt arasında kalan dar parçaları
    A05 = A.buffer(0.5)
    shapely.prepare(A05)
    refuj = []
    for pg in (S.geoms if isinstance(S, MultiPolygon) else [S]):
        if pg.area < 20:
            continue
        en = 2 * pg.area / max(pg.length, 1)        # ortalama genişlik
        if en < REFUJ_EN:
            kenar = pg.exterior.buffer(0.6)
            if kenar.difference(A05).area < 0.25 * kenar.area:     # çevresinin çoğu asfalta değiyor
                refuj.append(pg)

    print(f"asfalt {A.area / 1e4:.1f} ha, kaldırım {S.area / 1e4:.1f} ha, refüj {len(refuj)} parça, "
          f"katmanlı {len(katmanli)} parça, şerit çizgisi için {len(seritler)} yol, kavşak {len(kavsak)}")
    with open(os.path.join(kok.ISLEM, "yollar_geo.pkl"), "wb") as fh:
        pickle.dump({"A": A, "S": S, "refuj": refuj, "katmanli": katmanli, "koridor": koridor,
                     "kavsaklar": [(*P[k], 18.0) for k, dd in derece.items() if dd >= 3 and abs(F[k]) < 0.3],
                     "seritler": seritler, "guzergah": guz}, fh)


if __name__ == "__main__":
    main()
