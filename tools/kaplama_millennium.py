"""
Caio Millennium II (Mercedes O500M) kaplamalarına yazı ve işaretler: EGO logoları, filo numarası, belediye yazıları,
bel şeridi ve Türk plakası. Boyalı düz dokular millennium_donustur.py'den gelir (EGO kırmızı, EGO mavi, Özel Halk);
bu script onların üstüne çizer. Çizim tools/kaplama.py'deki BMC kaplamalarıyla aynı yapıdadır.

Doku yerleşimi (2048x2048, kaynak base.bmp ile aynı; render ile doğrulandı):
  sağ yan  y ≈ 285–460 : arka solda (ızgara x ≈ 115), arka teker x ≈ 485–670, orta kapı 975–1155,
                         ön teker 1515–1700, ön kapı 1735–1910
  sol yan  y ≈ 830–1000: ön solda, ön teker 345–520, BRT kapıları 595–775 ve 1080–1260, arka teker 1370–1545
  ön yüz alt parçası x ≈ 750–1300, y ≈ 1540–1720 (amblem dairesi 1035,1710); arka yüz x ≈ 1385–1935 (amblem 1655,1560)
  Plakalar gövde dokusunda değil: ön ve arka plaka aynı bölgeyi kullanır, bagulhosmep.png'de x 16–123, y 1663–2021,
  90° dönük (plakanın sağı dokuda aşağı, üstü dokuda sağ). Kaynakta Brezilya plakası vardı.

Kullanım:
    python kaplama_millennium.py --src <düz dokular klasörü> --out <Textures klasörü>
    (--src içinde ego_kirmizi.png, ego_mavi.png, ozel_halk.png ve bagulhosmep.png; çıktı: caroserie.png,
    Kaplamalar/ego_mavi.png, ozel_halk.png, bagulhosmep.png)
"""
import argparse
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kaplama import BEYAZ, badge, ego_logo, font, kurdele  # noqa: E402

SAG_Y = 290           # sağ yanda cam bandının alt kenarı (alt gövdenin üstü)
SOL_Y = 834
PLAKA_DOKU = "bagulhosmep.png"
PLAKA_YERI = (16, 1663, 123, 2021)   # x0, y0, x1, y1 (dönük)

KAPLAMALAR = {
    "ego_kirmizi": dict(govde=(214, 28, 32), serit=None, ego_panel=True, filo="EGO 22-353", rozet=True,
                        amblem=False, yazi=None, on_panel=None, cikti="caroserie.png"),
    "ego_mavi": dict(govde=(28, 64, 160), serit=20, ego_panel=False, filo="EGO-12-504", rozet=False, amblem=True,
                     yazi="ANKARA BÜYÜKŞEHİR\nBELEDİYESİ", on_panel="ANKARA BÜYÜKŞEHİR BELEDİYESİ",
                     cikti="Kaplamalar/ego_mavi.png"),
    "ozel_halk": dict(govde=(52, 150, 222), serit=11, ego_panel=False, filo=None, rozet=False, amblem=False,
                      yazi="ANKARA BÜYÜKŞEHİR\nBELEDİYESİ\nÖZEL HALK OTOBÜSÜ", on_panel=None,
                      cikti="Kaplamalar/ozel_halk.png"),
}


def boya_maskesi(arr, renk):
    """Gövde boyası pikselleri (gölgeli tonlar dahil): renk yönü aynı, parlaklık değişebilir."""
    a = arr.astype(np.float32)
    c = np.array(renk, np.float32)
    an = a / (np.linalg.norm(a, axis=2, keepdims=True) + 1e-3)
    cn = c / np.linalg.norm(c)
    return ((an * cn).sum(axis=2) > 0.985) & (a.max(axis=2) > 60)


def plaka(draw, rect, text):
    x0, y0, x1, y1 = rect
    h = y1 - y0
    draw.rectangle([x0, y0, x1, y1], fill=(250, 250, 250), outline=(15, 15, 15), width=max(1, h // 14))
    serit = int((x1 - x0) * 0.09)
    draw.rectangle([x0 + 2, y0 + 2, x0 + serit, y1 - 2], fill=(10, 60, 160))
    draw.text((x0 + serit // 2 + 1, y1 - 3), "TR", font=font(h * 0.28), fill=(255, 255, 255), anchor="ms")
    # yazı plakaya sığsın: yüksekliğin %62'si ya da genişliğe sığan en büyük boy
    boy = h * 0.62
    while boy > 6:
        f = font(boy)
        x_a, _, x_b, _ = draw.textbbox((0, 0), text, font=f)
        if x_b - x_a <= (x1 - x0 - serit) * 0.9:
            break
        boy -= 1
    draw.text(((x0 + serit + x1) // 2, (y0 + y1) // 2), text, font=f, fill=(10, 10, 10), anchor="mm")


def ciz(img, spec, plaka_yazi):
    arr = np.array(img.convert("RGB"))
    m = boya_maskesi(arr, spec["govde"])

    if spec["serit"]:
        for y0 in (SAG_Y, SOL_Y):
            band = m[y0:y0 + spec["serit"]]
            arr[y0:y0 + spec["serit"]][band] = BEYAZ
    panels = []
    if spec["ego_panel"]:
        # beyaz EGO paneli: sağda arka teker ile orta kapı arası, solda iki BRT kapısı arası
        panels = [(700, 305, 955, 445), (795, 850, 1060, 990)]
        for x0, y0, x1, y1 in panels:
            arr[y0:y1, x0:x1][m[y0:y1, x0:x1]] = BEYAZ
    img = Image.fromarray(arr)
    d = ImageDraw.Draw(img)

    for x0, y0, x1, y1 in panels:
        ego_logo(d, (x0 + x1) // 2, y0 + int((y1 - y0) * 0.45), int((y1 - y0) * 0.42))
    if spec["amblem"]:
        for cx, cy in ((1330, 375), (930, 918)):
            kurdele(d, cx, cy, 120)
    if spec["yazi"]:
        f = font(19, serif=True)
        for cx, y in ((820, SAG_Y + 22), (1690, SOL_Y + 22)):
            d.multiline_text((cx, y), spec["yazi"], font=f, fill=BEYAZ, anchor="ma", align="center", spacing=4)
    if spec["filo"]:
        if spec["rozet"]:
            for x, y in ((1185, 305), (1585, 852)):
                badge(d, x, y, 26)
                d.text((x + 34, y + 1), spec["filo"], font=font(25), fill=BEYAZ)
        else:
            for cx, y in ((820, SAG_Y + 100), (1690, SOL_Y + 105)):
                d.text((cx, y), spec["filo"], font=font(24), fill=BEYAZ, anchor="ma")
        d.text((1030, 1672), spec["filo"], font=font(22), fill=BEYAZ, anchor="mm")   # ön yüz, ön camın altı
        d.text((1655, 1505), spec["filo"], font=font(26), fill=BEYAZ, anchor="mm")   # arka yüz
    if spec["on_panel"]:
        x0, y0, x1, y1 = 880, 1600, 1180, 1626
        d.rectangle([x0, y0, x1, y1], fill=BEYAZ)
        d.text(((x0 + x1) // 2, (y0 + y1) // 2), spec["on_panel"], font=font(13, serif=True), fill=spec["govde"],
               anchor="mm")
    return img


def plaka_dokusu(src_path, out_path, text):
    """Ön/arka plaka: Türk plakası, dokudaki yerine 90° saat yönünde döndürülüp konur."""
    img = Image.open(src_path).convert("RGB")
    x0, y0, x1, y1 = PLAKA_YERI
    w, h = y1 - y0, x1 - x0                      # plaka yatay: genişlik dokuda y, yükseklik dokuda x
    p = Image.new("RGB", (w, h), (250, 250, 250))
    plaka(ImageDraw.Draw(p), (0, 0, w - 1, h - 1), text)
    img.paste(p.rotate(-90, expand=True), (x0, y0))
    img.save(out_path, optimize=True)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--src", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--plaka", default="06 CUM 353")
    args = ap.parse_args()
    for name, spec in KAPLAMALAR.items():
        img = ciz(Image.open(os.path.join(args.src, name + ".png")), spec, args.plaka)
        yol = os.path.join(args.out, spec["cikti"])
        os.makedirs(os.path.dirname(yol), exist_ok=True)
        img.save(yol, optimize=True)
        print("kaydedildi:", yol)
    kaynak = os.path.join(args.src, PLAKA_DOKU)
    if os.path.exists(kaynak):
        plaka_dokusu(kaynak, os.path.join(args.out, PLAKA_DOKU), args.plaka)
        print("kaydedildi:", PLAKA_DOKU)


if __name__ == "__main__":
    main()
