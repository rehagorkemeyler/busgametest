"""
Mercedes-Benz O530G Conecto (körüklü) yan kaplamaları: EGO kırmızı, EGO mavi, Özel Halk.

Doku (conecto_donustur.py'nin düzlemsel UV'si, 2048x512): u = boy (kaynak y −12,15 arka → 6,05 ön, 112,5 px/m),
v = yükseklik (0–3,2 m, yarım doku: 80 px/m). Üst yarı sağ yan, alt yarı sol yan. Sol yan dokuda aynalı durur
(dışarıdan bakınca önü solda): yazılar önce dışarıdan görüldüğü gibi çizilip sonra yatay çevrilir. Pikseller eşit
olmadığından her yan 112,5 px/m ile (2048x360) çizilip 256 satıra sıkıştırılır.

Çizilenler: cam bandı, sağdaki 4 kapı (camlı, iki kanatlı), alt etek, bel şeridi, logolar, filo numarası,
belediye yazıları. Ön ve arka yüzün rengi (M_Govde, dokusuz) BusLivery'de kaplamayla birlikte değişir.

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
                        amblem=False, yazi=None, cikti="caroserie.png"),
    "ego_mavi": dict(govde=(28, 64, 160), serit=0.09, ego_panel=False, filo="EGO-12-601", rozet=False, amblem=True,
                     yazi="ANKARA BÜYÜKŞEHİR\nBELEDİYESİ", cikti="Kaplamalar/ego_mavi.png"),
    "ozel_halk": dict(govde=(52, 150, 222), serit=0.05, ego_panel=False, filo=None, rozet=False, amblem=False,
                      yazi="ANKARA BÜYÜKŞEHİR\nBELEDİYESİ\nÖZEL HALK OTOBÜSÜ", cikti="Kaplamalar/ozel_halk.png"),
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


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True)
    args = ap.parse_args()
    for name, spec in KAPLAMALAR.items():
        yol = os.path.join(args.out, spec["cikti"])
        os.makedirs(os.path.dirname(yol), exist_ok=True)
        doku(spec).save(yol, optimize=True)
        print("kaydedildi:", yol)


if __name__ == "__main__":
    main()
