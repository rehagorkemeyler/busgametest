"""
Hat 3 — EGO 408 (Kızılay → Güneşevler yönü) güzergâhı: durakları sırayla OSM yol ağı üzerinden bağlar.

Durak sırası ve numaraları EGO'nun "Hareket Saatleri" sayfasından (ego.gov.tr/hareketsaatleri?hat_no=408, dönüş yönü
67–84). Konumlar OSM'deki EGO durak numarasından (bus_stop ref); OSM'de olmayanlar (Güneşevler ucu) adresinden
(cadde/mahalle) ve çevredeki yer imlerinden yaklaşık (YAKLASIK=True).

Yönlendirme: araç yolları (tek yön kurallarıyla), uzunluk × yol sınıfı cezası; her durak kendi yönündeki en yakın
yol düğümüne bağlanır (Atatürk Bulvarı gibi ayrık yollarda doğru şerit). Ara noktalar (GECIS) EGO adresinden bilinen
caddeye zorlamak için.

Çıktı (JSON): {"duraklar": [{ad, no, lat, lon, yaklasik}], "yol": [[lat, lon], ...], "osm_yollar": [way id...],
"caddeler": [[ad, uzunluk_m], ...], "uzunluk_m": ...}

Kullanım:
    python hat408_guzergah.py --osm osm.json.gz --out hat408.json
Veri: © OpenStreetMap katkıcıları (ODbL); durak listesi: EGO Genel Müdürlüğü.
"""
import argparse
import gzip
import heapq
import json
import math

# (EGO no, ad, lat, lon, yaklaşık mı) — Kızılay → Güneşevler (408 dönüş yönü, sıra 67–84)
DURAKLAR = [
    (11621, "Kızılay", None, None, False),
    (30010, "Sıhhiye", 39.93030, 32.85448, True),      # OSM'de 30010 yok; kuzey yönü, demiryolu köprüsünün kuzeyi (Hacettepe Mh.)
    (30384, "Opera", None, None, False),
    (30391, "Ulus", None, None, False),
    (30039, "Yıba Çarşısı", None, None, False),
    (31682, "Dışkapı Hastanesi", None, None, False),
    (30431, "Veteriner Fakültesi", None, None, False),
    (41481, "Çağdaş Sokak", None, None, False),
    (40487, "Atatürk Anadolu İmam Hatip Lisesi", None, None, False),
    (41847, "Altınpark", None, None, False),
    (40077, "Subayevleri", None, None, False),
    (40079, "Hasköy Akşemsettin Camii", None, None, False),
    (40090, "Pazar Yeri", 39.97000, 32.88468, True),    # Malazgirt Cd., Sade Pazar karşısı
    (40492, "Esenevler Anadolu Lisesi", 39.97060, 32.88560, True),  # 126. Sk. (127. Sk. ağzı)
    (40559, "Çeyizciler", 39.97150, 32.88730, True),    # 126. Sk.
    (41532, "Çiçek Taksi", 39.97195, 32.88860, True),   # OSM'de taksi durağı 39.97169, 32.88803 (Celal Esat Arseven başı)
    (41530, "Güneşevler Spor Tesisleri", 39.96985, 32.89870, True),  # Mehmet Akif Ersoy Cd. (doğu yönü), yüzme havuzu tesisleri karşısı
    (41488, "Güneşevler", 39.96860, 32.90420, True),    # Ali Ersoy Mh., son durak
]
# durak sırası arasında zorunlu ara noktalar (lat, lon): önceki durak no → [noktalar]
GECIS = {
    40079: [(39.97250, 32.88290)],      # Malazgirt Caddesi'ne sağa dönüş
}
SINIF = {"motorway": 1.0, "trunk": 1.0, "primary": 1.0, "secondary": 1.05, "tertiary": 1.15, "unclassified": 1.4,
         "residential": 1.8, "living_street": 3.0, "service": 4.0, "trunk_link": 1.0, "primary_link": 1.0,
         "secondary_link": 1.05, "tertiary_link": 1.15}
LAT0, LON0 = 39.9234, 32.8546


def xy(la, lo):
    return ((lo - LON0) * 111320 * math.cos(math.radians(LAT0)), (la - LAT0) * 110574)


def mesafe(a, b):
    (x1, y1), (x2, y2) = xy(*a), xy(*b)
    return math.hypot(x2 - x1, y2 - y1)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--osm", required=True)
    ap.add_argument("--out", required=True)
    args = ap.parse_args()
    d = json.load(gzip.open(args.osm, "rt", encoding="utf-8"))
    N = {int(k): v for k, v in d["nodes"].items()}
    G, kenar = {}, {}
    for wid, w in d["ways"].items():
        t = w["t"]
        h = t.get("highway")
        if h not in SINIF or t.get("access") in ("no", "private") or t.get("area") == "yes":
            continue
        ow = t.get("oneway")
        ileri = ow != "-1"
        geri = ow not in ("yes", "1", "true", "-1") and t.get("junction") != "roundabout"
        if ow == "-1":
            geri = True
        ns = [n for n in w["n"] if n in N]
        for a, b in zip(ns, ns[1:]):
            L = mesafe(N[a], N[b])
            c = L * SINIF[h]
            if ileri:
                G.setdefault(a, []).append((b, c))
                kenar[(a, b)] = (int(wid), L)
            if geri:
                G.setdefault(b, []).append((a, c))
                kenar[(b, a)] = (int(wid), L)

    tagged = d["tagged_nodes"]
    ref = {}
    for i, t in tagged.items():
        if t.get("highway") == "bus_stop" and t.get("ref"):
            ref.setdefault(int(t["ref"]), N[int(i)])
    duraklar = []
    for no, ad, la, lo, yak in DURAKLAR:
        if la is None:
            la, lo = ref[no]
        duraklar.append({"ad": ad, "no": no, "lat": la, "lon": lo, "yaklasik": yak})

    def dijkstra(kaynaklar, hedefler):
        dist = {}
        pq = []
        for s, c0 in kaynaklar:
            dist[s] = c0
            heapq.heappush(pq, (c0, s))
        prev = {}
        hed = dict(hedefler)
        while pq:
            c, u = heapq.heappop(pq)
            if c > dist.get(u, 1e18):
                continue
            if u in hed:
                yol = [u]
                while yol[-1] in prev:
                    yol.append(prev[yol[-1]])
                return yol[::-1]
            for v, w in G.get(u, ()):
                nc = c + w
                if nc < dist.get(v, 1e18):
                    dist[v] = nc
                    prev[v] = u
                    heapq.heappush(pq, (nc, v))
        raise RuntimeError("yol yok")

    def yonlu_kenar(p, yon, r=40.0):
        """Durağın yanındaki yönlü kenar: durak kenarın sağında (Türkiye'de sağdan), kenar yolculuk yönünde.
        (kaynak düğüm, hedef düğüm) döner; yoksa en yakın düğüm."""
        px, py = xy(*p)
        en, enIyi = None, 1e18
        for (a, b), (wid, L) in kenar.items():
            if L < 0.5:
                continue
            ax, ay = xy(*N[a])
            bx, by = xy(*N[b])
            dx, dy = bx - ax, by - ay
            u = max(0.0, min(1.0, ((px - ax) * dx + (py - ay) * dy) / (L * L)))
            qx, qy = ax + u * dx, ay + u * dy
            m = math.hypot(px - qx, py - qy)
            if m > r:
                continue
            sag = (dx * (py - ay) - dy * (px - ax)) < 0          # durak kenarın sağında
            hiza = (dx * yon[0] + dy * yon[1]) / L               # yolculuk yönüyle kosinüs
            if hiza < 0.3:
                continue
            puan = m + (0 if sag else 25) + (1 - hiza) * 20
            if puan < enIyi:
                enIyi, en = puan, (a, b)
        return en

    noktalar = []
    for dz in duraklar:
        noktalar.append((dz["lat"], dz["lon"]))
        for g in GECIS.get(dz["no"], ()):
            noktalar.append(g)
    kenarlar = []
    for i, p in enumerate(noktalar):
        q0 = noktalar[max(i - 1, 0)]
        q1 = noktalar[min(i + 1, len(noktalar) - 1)]
        (x0, y0), (x1, y1) = xy(*q0), xy(*q1)
        yon = (x1 - x0, y1 - y0)
        k = yonlu_kenar(p, yon)
        if k is None:
            raise RuntimeError(f"durağa yönlü yol yok: {p}")
        kenarlar.append(k)
    tum = [kenarlar[0][0], kenarlar[0][1]]
    for a, b in kenarlar[1:]:
        if tum[-1] == b or (tum[-2] == a and tum[-1] == b):
            continue
        parca = dijkstra([(tum[-1], 0.0)], [(a, 0.0)]) if tum[-1] != a else [a]
        tum.extend(parca[1:])
        tum.append(b)
    yollar, caddeler, L = [], [], 0.0
    for a, b in zip(tum, tum[1:]):
        wid, l = kenar[(a, b)]
        L += l
        ad = d["ways"][str(wid)]["t"].get("name", "?")
        if not yollar or yollar[-1] != wid:
            yollar.append(wid)
        if caddeler and caddeler[-1][0] == ad:
            caddeler[-1][1] += l
        else:
            caddeler.append([ad, l])
    out = {"duraklar": duraklar, "yol": [N[n] for n in tum], "dugumler": tum, "osm_yollar": yollar,
           "caddeler": [[a, round(l)] for a, l in caddeler], "uzunluk_m": round(L)}
    json.dump(out, open(args.out, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    print(f"uzunluk {L:.0f} m, {len(tum)} düğüm")
    for a, l in out["caddeler"]:
        print(f"  {l:6d} m  {a}")


if __name__ == "__main__":
    main()
