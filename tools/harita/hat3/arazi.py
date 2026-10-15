"""
A1 — Arazi ve yol yükseklikleri.

1. DEM (terrarium z15, ~30 m kaynak) → 10 m ızgara, Gauss yumuşatma (σ 20 m; SRTM yüzey modeli, bina/ağaç gürültüsü).
2. Yol ağı yükseklikleri: araç yolları ve demiryolları ≤ 8 m aralıkla sıklaştırılır; her düğüm için hedef = arazi + katman
   farkı, komşular arası yay (graf Laplace) ile birlikte en küçük kareler çözümü → kavşaklarda yollar aynı yükseklikte,
   köprü/alt geçit rampaları kendiliğinden (~%6–9).
   Katman kuralı: oyuncu güzergâhı her yerde arazide kalır (OSM'de köprü/tünel de olsa). Güzergâhı kesen başka bir yol/hat
   aradaki katman farkı kadar (6,5 m/katman) yukarıda ya da aşağıda: Sıhhiye'de demiryolu ve Celal Bayar Bulvarı köprüleri,
   Aydınlıkevler'de güzergâhın üstünden geçen yol, Dışkapı köprüsünün altındaki yol çukurda. Güzergâhtan uzak köprü ve
   tüneller kendi katmanlarıyla (×6,5 m). Tünel içindeki (yer altı) yollar ve raylar çizilmez.

Çıktı: <ISLEM>/arazi.npz
    H (ny, nx) yumuşatılmış arazi, x0, z0, hucre
    dugum_xz (n, 2), dugum_y (n,), dugum_fark (n,) — sıklaştırılmış yol ağı düğümleri (fark: katman hedefi, 0 = zemin)
    ve <ISLEM>/yollar.json: [{id, sinif, t (etiketler), d (düğüm indisleri), guzergah (bool), katman_farki}]
Rakımlar Kızılay durağının yumuşatılmış arazi rakımına göre (Y0).

Kullanım: python -m hat3.arazi   (tools/harita içinden)
"""
import glob
import math
import os

import numpy as np
import shapely
from PIL import Image
from scipy import ndimage, sparse
from scipy.sparse.linalg import spsolve
from shapely.geometry import LineString, Point
from shapely.strtree import STRtree

from . import kok

HUCRE = 10.0
SIGMA = 20.0
KATMAN = 6.5          # m / katman (köprü altı açıklığı + tabliye)
SIKLIK = 8.0          # m, yol düğümleri arası en çok
LAMBDA = 300.0        # yay katsayısı (graf yumuşatma uzunluğu ~40 m)
DEM_Z = 15
RAYLAR = ("rail", "light_rail", "tram", "narrow_gauge")


GECERSIZ = 300.0     # m; terrarium "veri yok" (−32 768) ve bozuk pikseller (Ankara > 700 m)


def _mozaik(klasor, z):
    karolar = {}
    for f in glob.glob(os.path.join(klasor, f"{z}_*_*.png")):
        _, x, y = os.path.basename(f)[:-4].split("_")
        a = np.asarray(Image.open(f).convert("RGB"), dtype=np.float64)
        karolar[(int(x), int(y))] = a[:, :, 0] * 256 + a[:, :, 1] + a[:, :, 2] / 256 - 32768
    if not karolar:
        return None
    xs = sorted({k[0] for k in karolar})
    ys = sorted({k[1] for k in karolar})
    m = np.full(((ys[-1] - ys[0] + 1) * 256, (xs[-1] - xs[0] + 1) * 256), -32768.0)
    for (x, y), a in karolar.items():
        m[(y - ys[0]) * 256:(y - ys[0] + 1) * 256, (x - xs[0]) * 256:(x - xs[0] + 1) * 256] = a
    return m, xs[0], ys[0]


def dem_mozaik(klasor=None):
    """Terrarium karolarını tek diziye birleştirir: (dizi, ilk karo x, ilk karo y).

    z15'te Ankara'nın kuzeyinde (39.985 K) bütün bir karo satırı "veri yok" (sunucuda boş; z14'te de); geçersiz ya da eksik
    pikseller sırayla z14 ve z13 mozaiklerinden çift doğrusal örneklenir.
    """
    klasor = klasor or os.path.join(kok.VERI, "dem")
    m, kx0, ky0 = _mozaik(klasor, DEM_Z)
    for z in (DEM_Z - 1, DEM_Z - 2):
        kotu = m < GECERSIZ
        if not kotu.any():
            break
        alt = _mozaik(klasor, z)
        if alt is None:
            continue
        ma, ax0, ay0 = alt
        r, c = np.nonzero(kotu)
        k = 2 ** (DEM_Z - z)
        py = ((ky0 * 256 + r + 0.5) / k) - ay0 * 256 - 0.5
        px = ((kx0 * 256 + c + 0.5) / k) - ax0 * 256 - 0.5
        v = ndimage.map_coordinates(ma, [py, px], order=1, mode="nearest")
        iyi = v >= GECERSIZ
        m[r[iyi], c[iyi]] = v[iyi]
        print(f"  DEM: z{DEM_Z}'te {len(r)} geçersiz piksel, {int(iyi.sum())} tanesi z{z}'ten dolduruldu")
    if (m < GECERSIZ).any():
        raise ValueError(f"DEM'de hâlâ {int((m < GECERSIZ).sum())} geçersiz piksel (karo eksik mi?)")
    return m, kx0, ky0


def dem_ornekle(m, kx0, ky0, lat, lon):
    n = 2 ** DEM_Z * 256
    px = (lon + 180) / 360 * n - kx0 * 256 - 0.5
    py = (1 - np.arcsinh(np.tan(np.radians(lat))) / np.pi) / 2 * n - ky0 * 256 - 0.5
    return ndimage.map_coordinates(m, [py, px], order=1, mode="nearest")


class Arazi:
    """Yumuşatılmış arazi ızgarası; y(x, z) çift doğrusal."""

    def __init__(self, H, x0, z0, hucre):
        self.H, self.x0, self.z0, self.hucre = H, x0, z0, hucre

    def y(self, x, z):
        tek = np.ndim(x) == 0
        x = np.atleast_1d(np.asarray(x, dtype=np.float64))
        z = np.atleast_1d(np.asarray(z, dtype=np.float64))
        v = ndimage.map_coordinates(self.H, [(z - self.z0) / self.hucre, (x - self.x0) / self.hucre],
                                    order=1, mode="nearest")
        return float(v[0]) if tek else v

    @staticmethod
    def yukle():
        d = np.load(os.path.join(kok.ISLEM, "arazi.npz"))
        return Arazi(d["H"], float(d["x0"]), float(d["z0"]), float(d["hucre"]))


def arazi_izgarasi():
    m, kx0, ky0 = dem_mozaik()
    # DEM'in kapsadığı alan (kenarda yarım hücre pay)
    n = 2 ** DEM_Z
    lon0 = kx0 / n * 360 - 180
    lon1 = (kx0 + m.shape[1] / 256) / n * 360 - 180
    lat0 = math.degrees(math.atan(math.sinh(math.pi * (1 - 2 * (ky0 + m.shape[0] / 256) / n))))
    lat1 = math.degrees(math.atan(math.sinh(math.pi * (1 - 2 * ky0 / n))))
    xa, za = kok.yerel(lat0, lon0)
    xb, zb = kok.yerel(lat1, lon1)
    x0 = math.ceil(xa / HUCRE) * HUCRE + HUCRE
    z0 = math.ceil(za / HUCRE) * HUCRE + HUCRE
    nx = int((xb - x0) / HUCRE) - 1
    nz = int((zb - z0) / HUCRE) - 1
    gx, gz = np.meshgrid(x0 + np.arange(nx) * HUCRE, z0 + np.arange(nz) * HUCRE)
    lat, lon = kok.cografi(gx, gz)
    H = dem_ornekle(m, kx0, ky0, lat.ravel(), lon.ravel()).reshape(gz.shape)
    H = ndimage.gaussian_filter(H, SIGMA / HUCRE, mode="nearest")
    return Arazi(H, x0, z0, HUCRE)


def katman(t):
    try:
        return int(float(t.get("layer", "0")))
    except ValueError:
        return 0


# --- katmanlar (docs/PLAN_HARITA_V2.md, M3) ---------------------------------------------------------------------------
# Bütün yollar (güzergâh dahil) OSM'deki katmanlarını izler: köprü/tünel/katmanlı yol hedefi arazi + katman × KATMAN.
# Rampalar: tabliye uçlarından aynı adlı yol boyunca RAMPA_EGIM ile sıfıra iner. Kesişen ama bağlı olmayan yollar arasında
# yinelemeli açıklık kısıtı: üstteki yol yüzeyi alttakinin en az ACIKLIK_Y üstünde (5,0 m açıklık + 1,2 m tabliye).
# Bağlı yollar (rampa ↔ ana yol) birbirinin izi içinde aynı yükseklikte: rampa ancak ana yoldan ayrılınca yükselir.
RAMPA_EGIM = 0.06
ACIKLIK_Y = 6.4
GUZ_EGIM = 0.085      # güzergâh en çok eğimi (Cinnah gerçekte ~%12 yerel; ortalama altı)


def yol_agi(arazi):
    from .yollar import serit_bilgisi
    o = kok.osm()
    xy = o["xy"]
    g = kok.guzergah()
    guz_id = set(g["osm_yollar"])
    secili = []
    # güzergâh tünellerinin ikizleri (karşı yön): aynı adlı ve güzergâha 40 m'den yakın tüneller de çizilir
    guz_ad = {o["ways"][w]["t"].get("name") for w in guz_id if w in o["ways"]} - {None}
    guz_cizgi = kok.guzergah_cizgisi()
    for wid, w in o["ways"].items():
        t = w["t"]
        h = t.get("highway")
        r = t.get("railway")
        if h in kok.ARAC_YOLU and t.get("area") != "yes":
            sinif = h
        elif r in RAYLAR:
            sinif = "ray"
        else:
            continue
        ns = [n for n in w["n"] if n in xy]
        if len(ns) < 2:
            continue
        if t.get("tunnel") in ("yes", "building_passage", "culvert") or t.get("location") == "underground":
            ikiz = t.get("name") in guz_ad and LineString([xy[n] for n in ns]).distance(guz_cizgi) < 40.0
            if wid not in guz_id and not ikiz:
                continue                        # güzergâh dışı tüneller yer altında: çizilmez (Akay gibi çok katlı yer altı
                                                # kavşaklarının ağızları güzergâhı kesiyordu); güzergâhtaki alt geçitler çizilir
        secili.append((wid, sinif, t, ns))

    def kat(t):
        """Yükseklik katmanı: yalnız köprü (+) ve tünel (−) etiketli parçalar. OSM 'layer' tek başına yükseklik değil, üst üste
        sıralamadır (ör. Atatürk Bulvarı'nın bir parçası alttan geçen yol yüzünden layer=1): sıralama için sira() kullanılır."""
        kopru = t.get("bridge") not in (None, "no")
        tunel = t.get("tunnel") not in (None, "no") or t.get("location") == "underground"
        L = katman(t)
        if kopru:
            return max(L, 1)
        if tunel:
            return min(L, -1)
        return 0

    # sıklaştırma: OSM düğümleri paylaşılır, aralara ara düğüm
    idx = {}
    pts = []
    kenarlar = []
    yollar = []
    dugum_yol = []           # düğüm → yol indisleri

    def dugum(osm_id, p, yi):
        if osm_id is not None and osm_id in idx:
            i = idx[osm_id]
            dugum_yol[i].add(yi)
            return i
        pts.append(p)
        dugum_yol.append({yi})
        i = len(pts) - 1
        if osm_id is not None:
            idx[osm_id] = i
        return i

    for yi, (wid, sinif, t, ns) in enumerate(secili):
        guz = wid in guz_id
        dlist = []
        for a, b in zip(ns, ns[1:]):
            pa, pb = np.array(xy[a]), np.array(xy[b])
            ia = dugum(a, tuple(pa), yi)
            if not dlist:
                dlist.append(ia)
            L_ab = float(np.linalg.norm(pb - pa))
            k = max(1, int(math.ceil(L_ab / SIKLIK)))
            onceki = ia
            for j in range(1, k):
                p = pa + (pb - pa) * j / k
                ij = dugum(None, tuple(p), yi)
                kenarlar.append((onceki, ij, L_ab / k, guz))
                dlist.append(ij)
                onceki = ij
            ib = dugum(b, tuple(pb), yi)
            kenarlar.append((onceki, ib, L_ab / k, guz))
            dlist.append(ib)
        if sinif == "ray":
            gen, kal = 4.0, 0.0
        else:
            _, gen, kal, _ = serit_bilgisi(t, sinif, guz)
        yollar.append({"id": wid, "sinif": sinif, "t": t, "d": dlist, "guzergah": guz, "katman": kat(t),
                       "sira": katman(t) + (0.5 if kat(t) > 0 else -0.5 if kat(t) < 0 else 0.0), "yari": gen / 2 + kal})

    P = np.array(pts)
    n = len(P)
    zemin = arazi.y(P[:, 0], P[:, 1])
    hedef = np.zeros(n)
    W = np.ones(n)

    # 1) köprü/tünel parçalarının hedefi (güçlü). Uçlarda incelir: zincirin ucu bir kavşaktaysa (zemindeki yol düz devam
    # etmiyor) sıfırdan, aynı yolun düz devamına bağlanıyorsa yarı yükseklikten RAMPA_EGIM ile tam yüksekliğe çıkar.
    def uc_turu(yi, k2, ic2):
        """'zincir' (aynı katmanda devam), 'devam' (aynı yol zeminde düz devam), 'kavsak'."""
        v_ = P[ic2] - P[k2]
        tur = "kavsak"
        for j in dugum_yol[k2] - {yi}:
            dj = yollar[j]["d"]
            if yollar[j]["katman"] == yollar[yi]["katman"]:
                return "zincir"
            if yollar[j]["katman"] == 0 and k2 in (dj[0], dj[-1]):
                u_ = P[dj[1]] - P[dj[0]] if k2 == dj[0] else P[dj[-2]] - P[dj[-1]]
                if float(np.dot(u_, v_) / (np.linalg.norm(u_) * np.linalg.norm(v_) + 1e-9)) < -0.7:
                    tur = "devam"
        return tur

    for yi, w in enumerate(yollar):
        L = w["katman"]
        if L == 0:
            continue
        dd = w["d"]
        Ls = np.concatenate([[0], np.cumsum(np.linalg.norm(np.diff(P[dd], axis=0), axis=1))])
        R = abs(L * KATMAN) / RAMPA_EGIM
        oran = np.ones(len(dd))
        for uc_i, ic_i, s_uc in ((0, 1, Ls), (-1, -2, Ls[-1] - Ls)):
            tur = uc_turu(yi, dd[uc_i], dd[ic_i])
            if tur == "zincir":
                continue
            bas = 0.0 if tur == "kavsak" else 0.5
            oran = np.minimum(oran, np.clip(bas + s_uc / R, 0.0, 1.0))
        for k, i in enumerate(dd):
            f = L * KATMAN * oran[k]
            if abs(f) >= abs(hedef[i]):
                hedef[i] = f
                W[i] = 400.0 if oran[k] >= 0.999 else 50.0

    komsu = [[] for _ in range(n)]
    guz_dugum = np.zeros(n, bool)
    for i, j, l, guz in kenarlar:
        if guz:
            guz_dugum[i] = guz_dugum[j] = True
    for i, j, l, guz in kenarlar:
        komsu[i].append((j, l))
        komsu[j].append((i, l))

    def gevset(kaynak, R=250.0, w_min=0.1):
        """Kaynak düğümlerden R m (yol boyunca) içindeki düğümlerin araziye bağlılığı w_min: rampayı yay sistemi kurar
        (ölçek √(LAMBDA/w)·adım ≈ 100 m → 6,5 m'de ~%6)."""
        import heapq
        gor = {int(i): 0.0 for i in kaynak}
        pq = [(0.0, int(i)) for i in kaynak]
        heapq.heapify(pq)
        while pq:
            dist, u = heapq.heappop(pq)
            if dist > gor.get(u, 1e18):
                continue
            for v, l in komsu[u]:
                nd = dist + l
                if nd < R and nd < gor.get(v, 1e18):
                    gor[v] = nd
                    heapq.heappush(pq, (nd, v))
        for v in gor:
            if W[v] < 1.01:
                # güzergâh yayları 8 kat sert: aynı rampa uzunluğu için bağlılık da 8 kat (yoksa güzergâh 400 m'lik düz bir
                # kalas gibi tünel ağzına iniyor, araziden 11 m aşağıda kalıp çıkışta %17 tırmanıyordu)
                W[v] = w_min * (8.0 if guz_dugum[v] else 1.0)

    hata_p = os.environ.get("HARITA_HATA")
    hata_p = np.array([float(v) for v in hata_p.split(",")]) if hata_p else None

    def iz(ad):
        if hata_p is None:
            return
        for i in np.nonzero(np.linalg.norm(P - hata_p, axis=1) < 4)[0]:
            print(f"   HATA {ad}: düğüm {i} {P[i].round(1)} hedef {hedef[i]:.2f} W {W[i]:.1f} yollar {[yollar[j]['id'] for j in dugum_yol[i]]}")

    iz("1-hedef")
    gevset(np.nonzero(W >= 400)[0])
    iz("gevset")

    # yay matrisi
    rows, cols, vals = [], [], []
    for i, j, l, guz in kenarlar:
        c = LAMBDA * (8.0 if guz else 1.0) / max(l, 0.5)     # güzergâh daha yumuşak (sürüş; kısa arazi dalgaları %13+ eğim yapıyordu)
        rows += [i, j, i, j]
        cols += [i, j, j, i]
        vals += [c, c, -c, -c]
    K = sparse.coo_matrix((vals, (rows, cols)), shape=(n, n)).tocsr()

    def coz(hedef_y, W_):
        A = (K + sparse.diags(W_)).tocsc()
        return spsolve(A, W_ * hedef_y)

    y = coz(zemin + hedef, W)

    # 3) açıklık ve bağlı-iz kısıtları (yinelemeli)
    seg_a, seg_b, seg_yol = [], [], []
    for yi, w in enumerate(yollar):
        for a_, b_ in zip(w["d"], w["d"][1:]):
            seg_a.append(a_)
            seg_b.append(b_)
            seg_yol.append(yi)
    seg_a, seg_b, seg_yol = np.array(seg_a), np.array(seg_b), np.array(seg_yol)
    cizgi = shapely.linestrings(np.stack([P[seg_a], P[seg_b]], axis=1))
    tr = STRtree(cizgi)
    pi, pj = tr.query(cizgi, predicate="intersects")
    k_ = (pi < pj) & (seg_yol[pi] != seg_yol[pj])
    pi, pj = pi[k_], pj[k_]
    # bağlı mı (ortak düğüm)
    yol_dugum = [set(w["d"]) for w in yollar]
    capraz = []
    for a_, b_ in zip(pi, pj):
        ya, yb = seg_yol[a_], seg_yol[b_]
        if yol_dugum[ya] & yol_dugum[yb]:
            continue
        x = shapely.intersection(cizgi[a_], cizgi[b_])
        if x.is_empty:
            continue
        c = np.array(x.representative_point().coords[0])
        capraz.append((int(ya), int(yb), c))
    print(f"  bağlantısız kesişme {len(capraz)}")
    # paralel bağlama: aynı katmanda, aynı yönde uzanan ve izleri üst üste binen iki yolun düğümleri (ör. güzergâh ile
    # yanındaki bağlantı yolu) yaylarla birbirine bağlanır; yoksa güzergâhın yumuşatması onu komşusundan 1–2 m ayırıyor,
    # örtüşen asfalt kaldırım/basamak gibi üstte kalıyordu
    sa, sb, sy = [], [], []
    for yi, w in enumerate(yollar):
        if w["sinif"] == "ray":
            continue
        for a_, b_ in zip(w["d"], w["d"][1:]):
            sa.append(a_)
            sb.append(b_)
            sy.append(yi)
    sa, sb, sy = np.array(sa), np.array(sb), np.array(sy)
    su = P[sb] - P[sa]
    su /= np.maximum(np.linalg.norm(su, axis=1, keepdims=True), 1e-9)
    tr_p = STRtree(shapely.linestrings(np.stack([P[sa], P[sb]], axis=1)))
    tan = np.zeros((n, 2))
    for a_, b_, u_ in zip(sa, sb, su):
        tan[a_] += u_
        tan[b_] += u_
    tan /= np.maximum(np.linalg.norm(tan, axis=1, keepdims=True), 1e-9)
    dyol = np.full(n, -1)
    for yi, w in enumerate(yollar):
        if w["sinif"] != "ray":
            dyol[w["d"]] = yi
    yari = np.array([w["yari"] for w in yollar])
    adaylar = np.nonzero(dyol >= 0)[0]
    qi, qk = tr_p.query(shapely.points(P[adaylar]), predicate="dwithin", distance=float(2 * yari.max()))
    qi = adaylar[qi]
    ya_, yb_ = dyol[qi], sy[qk]
    ab = P[sb[qk]] - P[sa[qk]]
    tt = np.clip(((P[qi] - P[sa[qk]]) * ab).sum(1) / np.maximum((ab ** 2).sum(1), 1e-9), 0, 1)
    dist = np.linalg.norm(P[qi] - (P[sa[qk]] + ab * tt[:, None]), axis=1)
    kat_ = np.array([w["katman"] for w in yollar])
    m_ = ((ya_ != yb_) & (kat_[ya_] == kat_[yb_]) & (dist < yari[ya_] + yari[yb_] - 1.0)
          & (np.abs((tan[qi] * su[qk]).sum(1)) > 0.8))
    guz_yol = np.array([w["guzergah"] for w in yollar])
    m_ &= guz_yol[ya_] | guz_yol[yb_]
    # köprü/tünel rampalarında (gevşetilmiş, W < 1) bağlama yok: alt geçit çıkışı komşu zemin yoluna çekilip %15 oluyordu
    m_ &= (W[qi] >= 0.99) & (W[sa[qk]] >= 0.99) & (W[sb[qk]] >= 0.99)
    if capraz:
        ic = np.nonzero(m_)[0]
        uzak = shapely.STRtree(shapely.points(np.array([c for _, _, c in capraz]))).query(
            shapely.points(P[qi[ic]]), predicate="dwithin", distance=100.0)[0]
        m_[ic[np.unique(uzak)]] = False
    bag = 0
    cb = LAMBDA * 8.0 / SIKLIK
    rows, cols, vals = [], [], []
    for i, k, t_ in zip(qi[m_], qk[m_], tt[m_]):
        for j, wj in ((sa[k], 1 - t_), (sb[k], t_)):
            if j == i or wj < 0.05:
                continue
            c = cb * wj
            rows += [i, j, i, j]
            cols += [i, j, j, i]
            vals += [c, c, -c, -c]
            bag += 1
    print(f"  paralel bağlama yayı {bag}")
    if bag:
        K = K + sparse.coo_matrix((vals, (rows, cols)), shape=(n, n)).tocsr()
        y = coz(zemin + hedef, W)
    yol_cizgi = [LineString(P[w["d"]]) for w in yollar]

    def kesit_y(yi, p):
        """yi yolunun p'ye en yakın noktasındaki yükseklik."""
        gg = yol_cizgi[yi]
        s_ = gg.project(Point(p))
        d_ = yollar[yi]["d"]
        Ls = np.concatenate([[0], np.cumsum(np.linalg.norm(np.diff(P[d_], axis=0), axis=1))])
        return float(np.interp(s_, Ls, y[d_]))

    ihlaller = []
    for tur in range(14):
        ihlal_listesi = []
        alt_sinir = np.full(n, -np.inf)       # düğüm ≥
        ust_sinir = np.full(n, np.inf)        # düğüm ≤
        ihlal = 0
        for ya, yb, c in capraz:
            La, Lb = yollar[ya]["sira"], yollar[yb]["sira"]
            if La != Lb:
                ust, alt = (ya, yb) if La > Lb else (yb, ya)
            else:
                ust, alt = (ya, yb) if kesit_y(ya, c) >= kesit_y(yb, c) else (yb, ya)
            yu, yl = kesit_y(ust, c), kesit_y(alt, c)
            if yu - yl >= ACIKLIK_Y - 0.05:
                pass
            # etkilenen düğümler: üst yolun, alt yolun izine (yarı genişlik + 2 m) yakın ve kesişmeye 80 m'den yakın olanlar
            alt_ciz = yol_cizgi[alt]
            ust_ciz = yol_cizgi[ust]
            tunel_alt = yollar[alt]["katman"] < 0 and yollar[ust]["katman"] >= 0
            ikisi = tur >= 3            # üç turda çözülmeyen kesişme: açık iki yola bölünür (üst yükselir, alt iner)
            for i in yollar[ust]["d"]:
                if np.linalg.norm(P[i] - c) > 80:
                    continue
                if alt_ciz.distance(Point(P[i])) <= yollar[alt]["yari"] + yollar[ust]["yari"] * 0.3 + 2.0:
                    yl_i = kesit_y(alt, P[i])
                    if y[i] - yl_i < ACIKLIK_Y - 0.05:
                        ihlal += 1
                        ihlal_listesi.append((round(float(y[i] - yl_i), 2), yollar[ust]["t"].get("name") or yollar[ust]["sinif"],
                                              yollar[alt]["t"].get("name") or yollar[alt]["sinif"], tuple(np.round(P[i]))))
                        if not tunel_alt:
                            eksik = ACIKLIK_Y - (y[i] - yl_i)
                            alt_sinir[i] = max(alt_sinir[i], y[i] + (eksik / 2 if ikisi else eksik))
            if tunel_alt or ikisi:
                for i in yollar[alt]["d"]:
                    if np.linalg.norm(P[i] - c) > 80:
                        continue
                    if ust_ciz.distance(Point(P[i])) <= yollar[ust]["yari"] + yollar[alt]["yari"] * 0.3 + 2.0:
                        yu_i = kesit_y(ust, P[i])
                        if yu_i - y[i] < ACIKLIK_Y - 0.05:
                            ihlal += 1
                            eksik = ACIKLIK_Y - (yu_i - y[i])
                            ust_sinir[i] = min(ust_sinir[i], y[i] - (eksik / 2 if (ikisi and not tunel_alt) else eksik))
        print(f"  açıklık turu {tur}: ihlal {ihlal}")
        if tur == 13 or ihlal == 0:
            ihlaller = ihlal_listesi
        if ihlal == 0:
            break
        m1 = np.isfinite(alt_sinir)
        m2 = np.isfinite(ust_sinir)
        hedef_y = zemin + hedef
        hedef_y[m1] = np.maximum(hedef_y[m1], alt_sinir[m1] + 0.5)
        hedef_y[m2] = np.minimum(hedef_y[m2], ust_sinir[m2] - 0.5)
        W[m1 | m2] = 2000.0
        hedef[m1 | m2] = hedef_y[m1 | m2] - zemin[m1 | m2]
        gevset(np.nonzero(m1 | m2)[0])
        y = coz(zemin + hedef, W)
        iz(f"açıklık {tur}")

    # 4) bağlı iz kuralı (tek tur): katmanlı yolun zeminden yükselmiş düğümü, uçlarında bağlı olduğu zemin yolunun izi
    # içindeyse onun yüksekliğine çekilir (rampa ana yoldan ayrılınca yükselir)
    hedef_y = zemin + hedef
    degisti = 0
    for yi, w in enumerate(yollar):
        if w["katman"] == 0 and not w["sinif"].endswith("_link"):
            continue                    # yalnız köprü/tünel ve bağlantı (rampa) yolları; 40 m sınırı paralel şeride çivilemeyi önler
        baglilar = []
        for k2, ic2 in ((w["d"][0], w["d"][1]), (w["d"][-1], w["d"][-2])):
            v_ = P[ic2] - P[k2]
            for j in dugum_yol[k2] - {yi}:
                if yollar[j]["katman"] != 0:
                    continue
                dj = yollar[j]["d"]
                if k2 in (dj[0], dj[-1]):
                    u_ = P[dj[1]] - P[dj[0]] if k2 == dj[0] else P[dj[-2]] - P[dj[-1]]
                    c_ = float(np.dot(u_, v_) / (np.linalg.norm(u_) * np.linalg.norm(v_) + 1e-9))
                    if c_ < -0.7:
                        continue            # aynı yolun düz devamı (tünel/köprü öncesi parça): rampa onun boyunca
                baglilar.append((j, k2))
        for i in w["d"]:
            if abs(y[i] - zemin[i]) < 0.3:
                continue
            for yj, k2 in baglilar:
                if np.linalg.norm(P[i] - P[k2]) > 40.0:
                    continue
                if yol_cizgi[yj].distance(Point(P[i])) < yollar[yj]["yari"] - 0.5:
                    hedef_y[i] = kesit_y(yj, P[i])
                    W[i] = 2000.0
                    degisti += 1
                    break
    print(f"  bağlı iz: {degisti} düğüm")
    print("  kalan açıklık ihlalleri (üst / alt / açıklık):" + "".join(f"\n    {a} m  {u} / {l} @{p}" for a, u, l, p in sorted(set(ihlaller))[:20]))
    if degisti:
        hedef = hedef_y - zemin
        y = coz(hedef_y, W)
    iz("bağlı iz")

    # 5) güzergâh eğim sınırı (GUZ_EGIM): hat boyunca sıralı güzergâh düğümlerinde |dy/ds| ≤ sınır, en az değişiklikle (ardışık
    # çiftlerde fark yarı yarıya düzeltilir, yinelemeli). Açıklık kısıtı kavşak düğümünü çiviliyor, güzergâh tünel ağzına 400 m
    # düz inip çıkışta %15 tırmanıyordu (Meclis alt geçidi). Sonuç güçlü hedef olur, komşu yollar yaylarla uyar.
    degisen = 0
    for ad, hg in kok.hatlar().items():
        cizgi = LineString([kok.yerel(*p_) for p_ in hg["yol"]])
        ids = set(hg["osm_yollar"])
        dd = sorted({i for w in yollar if w["id"] in ids for i in w["d"]})
        dd = np.array([i for i in dd if cizgi.distance(Point(P[i])) < 12.0])
        if len(dd) < 3:
            continue
        sv = np.array([cizgi.project(Point(P[i])) for i in dd])
        o_ = np.argsort(sv)
        dd, sv = dd[o_], sv[o_]
        yy = y[dd].copy()
        ds = np.maximum(np.diff(sv), 0.5)
        # arazinin kendisi dikse (Cinnah ~%12) o eğime izin: sınır = max(GUZ_EGIM, arazi eğimi (50 m ortalama) + %1,5)
        zs = np.interp(np.arange(sv[0], sv[-1] + 1, 1.0), sv, zemin[dd])
        k_ = 25
        zp = np.pad(zs, k_, mode="edge")
        zort = np.convolve(zp, np.ones(2 * k_ + 1) / (2 * k_ + 1), mode="valid")
        ze = np.abs(np.gradient(zort))
        sinir = np.maximum(GUZ_EGIM, np.interp((sv[:-1] + sv[1:]) / 2, np.arange(sv[0], sv[-1] + 1, 1.0), ze) + 0.015)
        for _ in range(4000):
            fark_ = np.diff(yy)
            asim = np.abs(fark_) - sinir * ds
            m = asim > 1e-3
            if not m.any():
                break
            duz = np.where(m, np.sign(fark_) * asim / 2, 0.0)
            yy[:-1] += duz
            yy[1:] -= duz
        ch = np.abs(yy - y[dd]) > 0.05
        if ch.any():
            hy = zemin + hedef
            hy[dd[ch]] = yy[ch]
            hedef[dd[ch]] = yy[ch] - zemin[dd[ch]]
            W[dd[ch]] = 5000.0
            degisen += int(ch.sum())
            print(f"  {ad}: eğim sınırı {int(ch.sum())} düğüm, en çok {np.abs(yy - y[dd]).max():.1f} m")
    if degisen:
        y = coz(zemin + hedef, W)

    hata_yol = os.environ.get("HARITA_YOL")
    if hata_yol:
        for w in yollar:
            if str(w["id"]) in hata_yol.split(","):
                dd = w["d"]
                Ls = np.concatenate([[0], np.cumsum(np.linalg.norm(np.diff(P[dd], axis=0), axis=1))])
                for k, i in enumerate(dd):
                    print(f"   YOL {w['id']} kat {w['katman']} s {Ls[k]:6.1f} {P[i].round(0)} y {y[i]:7.2f} zemin {zemin[i]:7.2f} "
                          f"hedef {hedef[i]:6.2f} W {W[i]:7.1f} yollar {len(dugum_yol[i])}")

    # katmanlı sayılan düğümler (yüzeyler: köprü/alt geçit şeridi): hedefi ya da sonucu zeminden > 0,5 m farklı
    # rampanın gevşetilmiş (köprü/tünel çevresindeki) kısmı da araziden sapıyorsa katmanlıdır: yarma (istinat duvarı) ya da dolgu
    fark = np.where((np.abs(hedef) > 0.3) | ((W < 0.99) & (np.abs(y - zemin) > 1.5)), y - zemin, 0.0)
    fark[np.abs(fark) < 0.5] = 0.0
    for w in yollar:
        dd = np.array(w["d"])
        w["katman_farki"] = float(fark[dd][np.argmax(np.abs(fark[dd]))]) if len(dd) else 0.0

    # eğim raporu: katmanlı yollarda > %8
    dik = []
    for w in yollar:
        dd = np.array(w["d"])
        if len(dd) < 3 or not np.any(fark[dd]):
            continue
        L_ = np.linalg.norm(np.diff(P[dd], axis=0), axis=1)
        e = np.abs(np.diff(y[dd])) / np.maximum(L_, 0.5)
        if e.max() > 0.08:
            dik.append((float(e.max()), w["t"].get("name") or w["sinif"], P[dd[int(np.argmax(e))]]))
    dik.sort(key=lambda x: -x[0])
    print(f"  %8'den dik katmanlı yol: {len(dik)}" + "".join(f"\n    %{e * 100:.0f} {ad} ({p[0]:.0f}, {p[1]:.0f})" for e, ad, p in dik[:12]))
    for w in yollar:
        w.pop("yari", None)
        w.pop("sira", None)
    return P, y, yollar, fark


def main():
    ar = arazi_izgarasi()
    P, y, yollar, fark = yol_agi(ar)
    Y0 = float(ar.y(0.0, 0.0))
    np.savez_compressed(os.path.join(kok.ISLEM, "arazi.npz"), H=ar.H - Y0, x0=ar.x0, z0=ar.z0, hucre=ar.hucre,
                        dugum_xz=P, dugum_y=y - Y0, dugum_fark=fark, Y0=Y0)
    kok.kaydet_json(yollar, os.path.join(kok.ISLEM, "yollar.json"))
    print(f"arazi {ar.H.shape} ({ar.H.shape[1] * HUCRE / 1000:.1f} × {ar.H.shape[0] * HUCRE / 1000:.1f} km), Kızılay {Y0:.1f} m;"
          f" yol ağı {len(yollar)} yol, {len(P)} düğüm")


if __name__ == "__main__":
    main()
