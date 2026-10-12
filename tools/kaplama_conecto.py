"""
Mercedes-Benz O530G Conecto (körüklü) yan kaplamaları: EGO kırmızı, EGO mavi, Özel Halk.

Doku (conecto_donustur.py'nin düzlemsel UV'si, 2048x512): u = boy (kaynak y −12,15 arka → 6,05 ön, 112,5 px/m),
v = yükseklik (0–3,2 m, yarım doku: 80 px/m). Üst yarı sağ yan, alt yarı sol yan. Sol yan dokuda aynalı durur
(dışarıdan bakınca önü solda): yazılar önce dışarıdan görüldüğü gibi çizilip sonra yatay çevrilir. Pikseller eşit
olmadığından her yan 112,5 px/m ile (2048x360) çizilip 256 satıra sıkıştırılır.

Çizilenler: cam bandı, sağdaki 4 kapı (camlı, iki kanatlı), alt etek, bel şeridi, logolar, filo numarası,
belediye yazıları.

Ön ve arka yüz ayrı doku (`onarka*.png`, 1024x1024; malzeme M_onarka, UV'si blender/conecto_ic.py'de):
u 0–0,5 ön yüz (önden bakınca, x −1,32 … 1,32 m), u 0,5–1 arka yüz (arkadan bakınca), v = yükseklik 0–3,3 m.
Ön cam (yuvarlak köşeli), hat tabelası, plaka, bel şeridi; arkada cam, stop lambaları, motor ızgarası, plaka, filo no.
Ön/arka yüzde kalan kenarlar (M_Govde, dokusuz) BusLivery'de kaplamayla birlikte boyanır.

Gece maskeleri (RGBA, kaplamadan bağımsız): gece_yan.png (yan camlar), gece_onarka.png (farlar, park lambaları, arka cam),
gece_fren.png (stop lambaları). blender/conecto_lambalar.py bunlarla Lamba_* parçalarını kurar.

Kullanım:
    python kaplama_conecto.py --out AnkaraBusSimulator/Assets/_Project/Buses/MB_Conecto_G/Textures
"""
import argparse
import os
import sys

from PIL import Image, ImageDraw

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kaplama import BEYAZ, badge, ego_logo, font, kurdele  # noqa: E402

PX = 112.5                       # piksel / metre
Y0, Y1 = -12.15, 6.05            # gövde boyu (kaynak y, ön +Y)
H_M = 3.2
W, H = 2048, int(H_M * PX)       # bir yan, eşit ölçekte
KAPILAR = [(4.28, 5.53), (-1.60, -0.36), (-7.71, -6.48), (-10.92, -9.67)]   # sağ yan
CAM_Z = (1.22, 2.66)
KAPI_Z = (0.28, 2.66)
KORUK = (-5.36, -3.55)
AKSLAR = (3.28, -2.62, -8.75)
CAM = (22, 26, 31)
KAPI = (46, 50, 55)
ETEK = (38, 38, 40)

KAPLAMALAR = {
    "ego_kirmizi": dict(govde=(214, 28, 32), serit=None, ego_panel=True, filo="EGO 22-501", rozet=True,
                        amblem=False, yazi=None, cikti="caroserie.png", onarka="onarka.png"),
    "ego_mavi": dict(govde=(28, 64, 160), serit=0.09, ego_panel=False, filo="EGO-12-601", rozet=False, amblem=True,
                     yazi="ANKARA BÜYÜKŞEHİR\nBELEDİYESİ", cikti="Kaplamalar/ego_mavi.png",
                     onarka="Kaplamalar/onarka_mavi.png"),
    "ozel_halk": dict(govde=(52, 150, 222), serit=0.05, ego_panel=False, filo=None, rozet=False, amblem=False,
                      yazi="ANKARA BÜYÜKŞEHİR\nBELEDİYESİ\nÖZEL HALK OTOBÜSÜ", cikti="Kaplamalar/ozel_halk.png",
                      onarka="Kaplamalar/onarka_ozel.png"),
}


def X(y, sol):
    """Kaynak y (m) → dışarıdan görüldüğü gibi yan görüntüde x (px). Sağ yanda ön sağda, sol yanda ön solda."""
    return int(round((Y1 - y) * PX)) if sol else int(round((y - Y0) * PX))


def Z(z):
    return int(round((H_M - z) * PX))


def yan(spec, sol):
    img = Image.new("RGB", (W, H), spec["govde"])
    d = ImageDraw.Draw(img)

    def kutu(ya, yb, za, zb, renk):
        xa, xb = sorted((X(ya, sol), X(yb, sol)))
        d.rectangle([xa, Z(zb), xb, Z(za)], fill=renk)

    # alt etek ve cam bandı (körük ayrı parça; orada bant da kesilir)
    kutu(Y0, Y1, 0.0, 0.22, ETEK)
    for a, b in ((Y0 + 0.25, KORUK[0] - 0.05), (KORUK[1] + 0.05, Y1 - 0.15)):
        kutu(a, b, CAM_Z[0], CAM_Z[1], CAM)   # pencere dikmeleri modelde (ayrı parça), çizilmez
    if spec["serit"]:
        for a, b in ((Y0, KORUK[0]), (KORUK[1], Y1)):
            kutu(a, b, CAM_Z[0] - 0.12 - spec["serit"], CAM_Z[0] - 0.12, BEYAZ)
    if not sol:
        for a, b in KAPILAR:
            kutu(a, b, KAPI_Z[0], KAPI_Z[1] + 0.02, KAPI)
            orta = (a + b) / 2
            for ka, kb in ((a + 0.06, orta - 0.03), (orta + 0.03, b - 0.06)):
                kutu(ka, kb, KAPI_Z[0] + 0.12, KAPI_Z[1] - 0.06, CAM)
                kutu(ka, kb, 1.10, 1.16, KAPI)   # kanat kayıtları
    d = ImageDraw.Draw(img)

    # yazılar ve logolar: ön gövdede, orta kapı ile ön teker arası (y −0,3 … 2,6)
    if spec["ego_panel"]:
        xa, xb = sorted((X(0.15, sol), X(2.55, sol)))
        d.rectangle([xa, Z(1.05), xb, Z(0.40)], fill=BEYAZ)
        ego_logo(d, (xa + xb) // 2, (Z(1.05) + Z(0.40)) // 2, int(0.40 * PX))
    if spec["amblem"]:
        kurdele(d, X(2.0, sol), Z(0.72), int(0.62 * PX))
    if spec["yazi"]:
        d.multiline_text((X(0.75, sol), Z(1.02)), spec["yazi"], font=font(0.085 * PX, serif=True), fill=BEYAZ,
                         anchor="ma", align="center", spacing=3)
    if spec["filo"]:
        # arka uçta, son kapının gerisi (sağ) / aynı yer (sol)
        x = X(-11.85, sol) if not sol else X(-10.75, sol)
        y = Z(1.02)
        if spec["rozet"]:
            badge(d, x, y, int(0.14 * PX))
            d.text((x + int(0.17 * PX), y), spec["filo"], font=font(0.13 * PX), fill=BEYAZ)
        else:
            d.text((X(0.75, sol), Z(0.55)), spec["filo"], font=font(0.12 * PX), fill=BEYAZ, anchor="ma")
    return img


def doku(spec):
    sag = yan(spec, False).resize((W, 256), Image.LANCZOS)
    sol = yan(spec, True).transpose(Image.FLIP_LEFT_RIGHT).resize((W, 256), Image.LANCZOS)
    out = Image.new("RGB", (W, 512))
    out.paste(sag, (0, 0))
    out.paste(sol, (0, 256))
    return out


# ön/arka doku: 1024x1024, her yüz 512 px genişlik (2,64 m) ve 1024 px yükseklik (3,3 m)
OA = 1024
OA_X = 1.32
OA_H = 3.3
FARLAR = ((-1.18, -0.78), (0.78, 1.18))          # ön yüz (önden bakınca), z FAR_Z
FAR_Z = (0.56, 0.74)
SINYAL_ON = ((-1.18, -1.00), (1.00, 1.18))
STOPLAR = ((-1.10, -0.92), (0.92, 1.10))         # arka yüz (arkadan bakınca), z STOP_Z
STOP_Z = (0.95, 1.30)
ARKA_CAM = (-1.04, 1.04, 1.80, 2.60)
GECE_PENCERE = ((176, 166, 140), (62, 56, 46))   # içerisi yanarken camlar: üstte tavan ışığı, altta koltuklar (loş)
PLAKA = {"ego_kirmizi": "06 EGO 501", "ego_mavi": "06 EGO 601", "ozel_halk": "06 HO 1453"}


def onarka(spec, ad):
    """Her yüz önce eşit ölçekte (sz px/m) çizilir, sonra 512 px genişliğe sıkıştırılır (yazılar dünyada doğru en/boyda)."""
    sz = OA / OA_H
    gw = int(round(2 * OA_X * sz))
    yuzler = []
    for arka in (False, True):
        img = Image.new("RGB", (gw, OA), spec["govde"])
        d = ImageDraw.Draw(img)

        def P(x, z):
            """Görüntüde soldan x (m, −1,32 … 1,32), yerden z (m) → piksel."""
            return int(round((x + OA_X) * sz)), int(round((OA_H - z) * sz))

        def kutu(xa, xb, za, zb, renk, r=0.0, **kw):
            (x0, y0), (x1, y1) = P(xa, zb), P(xb, za)
            if r > 0:
                d.rounded_rectangle([x0, y0, x1, y1], radius=int(r * sz), fill=renk, **kw)
            else:
                d.rectangle([x0, y0, x1, y1], fill=renk, **kw)

        def yazi(x, z, metin, boy, renk):
            d.text(P(x, z), metin, font=font(boy * sz), fill=renk, anchor="mm")

        def plaka(z):
            kutu(-0.26, 0.26, z, z + 0.11, (250, 250, 250), r=0.01, outline=(15, 15, 15), width=2)
            kutu(-0.255, -0.215, z + 0.005, z + 0.105, (10, 60, 160))
            yazi(0.02, z + 0.053, PLAKA[ad], 0.068, (10, 10, 10))

        kutu(-OA_X, OA_X, 0.0, 0.30, ETEK)                       # tampon altı
        if spec["serit"]:
            kutu(-OA_X, OA_X, CAM_Z[0] - 0.12 - spec["serit"], CAM_Z[0] - 0.12, BEYAZ)
        if not arka:
            # geniş ön cam (alt kenarı ortada 1,08 m, yanlara doğru hafif yükselir), üstte hat tabelası
            ust, kenar = 2.80, 1.17
            pts = [P(-kenar + 2 * kenar * i / 40, 1.08 + 0.10 * ((-kenar + 2 * kenar * i / 40) / kenar) ** 2)
                   for i in range(41)]
            d.polygon(pts + [P(kenar, ust), P(-kenar, ust)], fill=CAM)
            kutu(-0.98, 0.98, 2.50, 2.74, (8, 8, 8), r=0.02)
            for xa, xb in FARLAR:
                kutu(xa, xb, FAR_Z[0], FAR_Z[1], (200, 205, 212), r=0.04, outline=(30, 30, 34), width=4)  # far
            for xa, xb in SINYAL_ON:
                kutu(xa, xb, FAR_Z[1] + 0.02, FAR_Z[1] + 0.10, (235, 140, 20), r=0.02)                  # sinyal
            plaka(0.47)
        else:
            # arka cam, küçük hat tabelası, stop lambaları, motor ızgarası, plaka, filo numarası
            kutu(*ARKA_CAM, CAM, r=0.06)
            kutu(0.40, 0.96, 2.38, 2.55, (8, 8, 8))
            for xa, xb in STOPLAR:
                kutu(xa, xb, STOP_Z[0], STOP_Z[1], (190, 20, 20), r=0.02, outline=(25, 5, 5), width=4)  # stop / park
                kutu(xa, xb, 0.78, 0.93, (235, 140, 20), r=0.02)   # sinyal
                kutu(xa, xb, 0.60, 0.76, (235, 235, 235), r=0.02)  # geri vites
            for i in range(7):                                    # motor ızgarası
                kutu(-0.70, 0.70, 0.86 + i * 0.06, 0.89 + i * 0.06, (30, 30, 32))
            plaka(0.56)
            if spec["ego_panel"]:
                # modeldeki "CONECTO" ve "Mercedes-Benz" yazıları (z ≈ 1,62–1,72) üstte kalır
                kutu(0.42, 0.88, 1.36, 1.56, BEYAZ)
                x, y = P(0.65, 1.46)
                ego_logo(d, x, y, int(0.15 * sz))
            if spec["filo"]:
                yazi(-0.62, 1.46, spec["filo"], 0.09, BEYAZ)
        yuzler.append(img.resize((OA // 2, OA), Image.LANCZOS))
    out = Image.new("RGB", (OA, OA))
    out.paste(yuzler[0], (0, 0))
    out.paste(yuzler[1], (OA // 2, 0))
    return out


def egim(img, z_ust, z_alt, piksel_z):
    """Yanan (alfa > 0) piksellerin rengini yüksekliğe göre GECE_PENCERE üst → alt renge boyar (yalnız alfası 255 olanlar
    pencere; diğer lambalara dokunmaz). piksel_z(y) → metre."""
    px = img.load()
    (r0, g0, b0), (r1, g1, b1) = GECE_PENCERE
    for y in range(img.height):
        t = min(max((z_ust - piksel_z(y)) / max(z_ust - z_alt, 1e-6), 0.0), 1.0)
        renk = (int(r0 + (r1 - r0) * t), int(g0 + (g1 - g0) * t), int(b0 + (b1 - b0) * t), 255)
        for x in range(img.width):
            if px[x, y] == PENCERE_ISARET:
                px[x, y] = renk


PENCERE_ISARET = (1, 2, 3, 255)   # maskede pencere yeri (sonra egim ile boyanır)


def gece_yan():
    """Gece pencere maskesi (yan doku düzeninde, RGBA): camlar yanar, gerisi saydam. conecto_lambalar.py'nin Lamba_Pencere
    parçaları bu dokuyu kullanır (Unlit, alfa kesme); kapı camları kanatlarla gider, dahil değil."""
    yanlar = []
    for sol in (False, True):
        img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        d = ImageDraw.Draw(img)
        for a, b in ((Y0 + 0.25, KORUK[0] - 0.05), (KORUK[1] + 0.05, Y1 - 0.15)):
            xa, xb = sorted((X(a + 0.04, sol), X(b - 0.04, sol)))
            d.rectangle([xa, Z(CAM_Z[1] - 0.05), xb, Z(CAM_Z[0] + 0.04)], fill=PENCERE_ISARET)
        if not sol:
            for a, b in KAPILAR:
                xa, xb = sorted((X(a, sol), X(b, sol)))
                d.rectangle([xa, Z(KAPI_Z[1] + 0.02), xb, Z(KAPI_Z[0])], fill=(0, 0, 0, 0))
        egim(img, CAM_Z[1], CAM_Z[0], lambda y: H_M - y / PX)
        if sol:
            img = img.transpose(Image.FLIP_LEFT_RIGHT)
        yanlar.append(img.resize((W, 256), Image.NEAREST))
    out = Image.new("RGBA", (W, 512), (0, 0, 0, 0))
    out.paste(yanlar[0], (0, 0))
    out.paste(yanlar[1], (0, 256))
    return out


def gece_onarka(fren):
    """Ön/arka yüz gece maskesi (onarka düzeninde, RGBA). fren=False: farlar (ön), park/stop lambaları ve arka cam (arka);
    fren=True: yalnız stop lambaları, parlak kırmızı (Lamba_Fren, frende yanar)."""
    sz = OA / OA_H
    gw = int(round(2 * OA_X * sz))
    yuzler = []
    for arka in (False, True):
        img = Image.new("RGBA", (gw, OA), (0, 0, 0, 0))
        d = ImageDraw.Draw(img)

        def kutu(xa, xb, za, zb, renk, r=0.0):
            x0, y0 = int(round((xa + OA_X) * sz)), int(round((OA_H - zb) * sz))
            x1, y1 = int(round((xb + OA_X) * sz)), int(round((OA_H - za) * sz))
            d.rounded_rectangle([x0, y0, x1, y1], radius=int(r * sz), fill=renk + (255,))

        if fren:
            if arka:
                for xa, xb in STOPLAR:
                    kutu(xa + 0.02, xb - 0.02, STOP_Z[0] + 0.02, STOP_Z[1] - 0.02, (255, 40, 30), r=0.02)
        elif not arka:
            for xa, xb in FARLAR:
                kutu(xa + 0.03, xb - 0.03, FAR_Z[0] + 0.03, FAR_Z[1] - 0.03, (255, 248, 225), r=0.03)
        else:
            for xa, xb in STOPLAR:
                kutu(xa + 0.02, xb - 0.02, STOP_Z[1] - 0.14, STOP_Z[1] - 0.02, (150, 12, 10), r=0.02)   # park (kısık)
            xa, xb, za, zb = ARKA_CAM
            kutu(xa + 0.05, xb - 0.05, za + 0.05, zb - 0.05, PENCERE_ISARET[:3], r=0.05)
            egim(img, zb, za, lambda y: OA_H - y / sz)
        yuzler.append(img.resize((OA // 2, OA), Image.NEAREST))
    out = Image.new("RGBA", (OA, OA), (0, 0, 0, 0))
    out.paste(yuzler[0], (0, 0))
    out.paste(yuzler[1], (OA // 2, 0))
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True)
    args = ap.parse_args()
    for name, spec in KAPLAMALAR.items():
        yol = os.path.join(args.out, spec["cikti"])
        os.makedirs(os.path.dirname(yol), exist_ok=True)
        doku(spec).save(yol, optimize=True)
        print("kaydedildi:", yol)
        yol = os.path.join(args.out, spec["onarka"])
        onarka(spec, name).save(yol, optimize=True)
        print("kaydedildi:", yol)
    # gece maskeleri (üç kaplamada ortak)
    for ad, img in (("gece_yan.png", gece_yan()), ("gece_onarka.png", gece_onarka(False)), ("gece_fren.png", gece_onarka(True))):
        yol = os.path.join(args.out, ad)
        img.save(yol, optimize=True)
        print("kaydedildi:", yol)


if __name__ == "__main__":
    main()
