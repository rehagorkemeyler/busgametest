"""
Ortak: yerel koordinat, veri yükleme, yol sınıfları.

Yerel koordinat (metre): x doğu, z kuzey (Unity ekseni), y yukarı. Kök: Kızılay durağı (EGO 11621). Rakım Kızılay
durağının arazi rakımına göre (oyunda Kızılay y ≈ 0).
"""
import gzip
import json
import math
import os

KLASOR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
VERI = os.path.join(KLASOR, "veri")

LAT0, LON0 = 39.9234046, 32.8546322          # Kızılay durağı
_f = math.radians(LAT0)
MY = 111132.92 - 559.82 * math.cos(2 * _f) + 1.175 * math.cos(4 * _f)      # m / derece enlem
MX = 111412.84 * math.cos(_f) - 93.5 * math.cos(3 * _f)                      # m / derece boylam


def yerel(lat, lon):
    return ((lon - LON0) * MX, (lat - LAT0) * MY)


def cografi(x, z):
    return (LAT0 + z / MY, LON0 + x / MX)


_osm = None


def osm():
    """OSM verisi (osm_birlestir.py çıktısı): düğümler yerel koordinata çevrilmiş."""
    global _osm
    if _osm is None:
        d = json.load(gzip.open(os.path.join(VERI, "osm_ankara.json.gz"), "rt", encoding="utf-8"))
        d["xy"] = {int(k): yerel(*v) for k, v in d["nodes"].items()}
        d["tagged_nodes"] = {int(k): v for k, v in d["tagged_nodes"].items()}
        d["ways"] = {int(k): v for k, v in d["ways"].items()}
        d["relations"] = {int(k): v for k, v in d["relations"].items()}
        _osm = d
    return _osm


# Hatlar (docs/PLAN_HARITA_V2.md): her biri hat408_guzergah.py çıktısı, veri/hatlar/<ad>.json. Harita iki hattın birleşik
# koridorunu kapsar; sahneler (Unity) hat başına ayrı yerleşim JSON'u alır.
HATLAR = {
    "408_gunesevler": {"klasor": "Hat3", "dosya": "Hat3_Yerlesim.json", "numara": "408", "ad": "Kızılay – Güneşevler"},
    "408_atakule": {"klasor": "Hat1", "dosya": "Hat1_Yerlesim.json", "numara": "408", "ad": "Kızılay – Atakule"},
}


def hatlar():
    """{hat adı: güzergâh JSON'u} — yalnız dosyası olanlar."""
    sonuc = {}
    for ad in HATLAR:
        f = os.path.join(VERI, "hatlar", f"{ad}.json")
        if os.path.exists(f):
            sonuc[ad] = json.load(open(f, encoding="utf-8"))
    return sonuc


def guzergah():
    """Bütün hatların birleşimi: duraklar (sırayla, hat hat), osm_yollar birleşimi. Tek çizgi için guzergah_cizgisi()."""
    h = hatlar()
    return {"duraklar": [dz for g in h.values() for dz in g["duraklar"]],
            "osm_yollar": sorted({w for g in h.values() for w in g["osm_yollar"]}),
            "hatlar": h}


_cizgiler = None


def hat_s(x, z):
    """Noktaya en yakın hat ve o hat boyunca konum: (hat adı, s m). Bölge kuralları (binalar) hat başına."""
    global _cizgiler
    from shapely.geometry import LineString, Point
    if _cizgiler is None:
        _cizgiler = {ad: LineString([yerel(*p) for p in g["yol"]]) for ad, g in hatlar().items()}
    p = Point(x, z)
    ad = min(_cizgiler, key=lambda a: _cizgiler[a].distance(p))
    return ad, _cizgiler[ad].project(p)


def guzergah_cizgisi():
    """Bütün hatların güzergâhları (yerel xz), shapely MultiLineString."""
    from shapely.geometry import LineString, MultiLineString
    return MultiLineString([LineString([yerel(*p) for p in g["yol"]]) for g in hatlar().values()])


# araç yolları: sınıf → (varsayılan toplam şerit (iki yön), şerit genişliği m, kaldırım m)
ARAC_YOLU = {
    "motorway": (6, 3.6, 0.0), "trunk": (6, 3.5, 4.0), "primary": (4, 3.4, 4.0), "secondary": (4, 3.25, 3.0),
    "tertiary": (2, 3.25, 2.5), "unclassified": (2, 3.0, 2.0), "residential": (2, 3.0, 2.0),
    "living_street": (2, 2.75, 1.5), "service": (1, 3.0, 0.0), "motorway_link": (1, 3.75, 0.0),
    "trunk_link": (1, 3.75, 0.0), "primary_link": (1, 3.5, 1.5), "secondary_link": (1, 3.5, 1.5),
    "tertiary_link": (1, 3.25, 1.5),
}


def kaydet_json(veri, ad):
    with open(ad, "w", encoding="utf-8") as fh:
        json.dump(veri, fh, ensure_ascii=False, separators=(",", ":"))

# ara çıktılar (yeniden üretilebilir; repoda değil)
ISLEM = os.environ.get("HAT3_ISLEM", os.path.join(VERI, "_islem"))
os.makedirs(ISLEM, exist_ok=True)
