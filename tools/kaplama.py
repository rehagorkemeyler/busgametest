"""
BMC Procity 12LF kaplamaları (Ankara): kırmızı EGO, mavi EGO (MAN CNG tarzı) ve Özel Halk Otobüsü.

Kaynak: Proton modunun beyaz gövde dokusu (caroserie.png, 4096x2048). Beyaz boya alanları gölgeleri
korunarak boyanır; siyah cam bandı ve detaylar korunur.

Doku yerleşimi (4096x2048 piksel):
  üst şerit  y ≈   80–770 : sağ yan (kapılar), ön sağda, arka tekerlek x ≈ 1680, cam bandı alt kenarı y ≈ 475
  alt şerit  y ≈ 1240–1940: sol yan, ön solda, arka tekerlek x ≈ 3080, cam bandı alt kenarı y ≈ 1629
  orta       y ≈  820–1200: tavan ve tavan modülü
  sol üst: arka yüz, sol alt: ön yüz

Kullanım:
    python kaplama.py --src "BMC Procity 12LF/BMC Procity 12LF" --out <klasör> [--kaplama ego_kirmizi ego_mavi ozel_halk]
Çıktı: <kaplama>.png (gövde) ve gerekiyorsa <kaplama>_cngtank.png (tavan modülü).
"""
import argparse
import math
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFont

BEYAZ = (246, 246, 244)
FONTS = ("/usr/share/fonts/opentype/inter/InterDisplay-Bold.otf",
         "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
         "/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf")
SERIF = "/usr/share/fonts/truetype/dejavu/DejaVuSerif-Bold.ttf"

# Ölçüler 1024x512 ölçeğinde (×4 = gerçek piksel)
# Sağ yanda kapılar (cam bandından aşağı siyah boşluklar) x ≈ 270–355, 570–655, 880–965; yazılar bunların
# arasındaki gövde panellerine konur.
SAG = dict(y_cam=475 / 4, ust=(20, 192), amblem_x=708, yazi_x=515, filo_x=515)
SOL = dict(y_cam=1629 / 4, ust=(308, 487), amblem_x=578, yazi_x=865, filo_x=300)
TAVAN = (440, 200, 1000, 305)          # orta bant: tavan + tavan modülü
ON = (25, 405, 205, 490)               # ön yüz
ARKA = (35, 15, 205, 192)              # arka yüz

KAPLAMALAR = {
    "ego_kirmizi": dict(govde=(214, 28, 32), tavan_boya=True, cng_boya=True, serit=None,
                        ego_panel=True, filo="EGO 22-352", rozet=True, amblem=False, yazi=None, on_panel=None),
    "ego_mavi": dict(govde=(28, 64, 160), tavan_boya=False, cng_boya=False, serit=(BEYAZ, 11),
                     ego_panel=False, filo="EGO-12-503", rozet=False, amblem=True,
                     yazi="ANKARA BÜYÜKŞEHİR\nBELEDİYESİ", on_panel="ANKARA BÜYÜKŞEHİR BELEDİYESİ"),
    "ozel_halk": dict(govde=(52, 150, 222), tavan_boya=False, cng_boya=False, serit=(BEYAZ, 6),
                      ego_panel=False, filo=None, rozet=False, amblem=False,
                      yazi="ANKARA BÜYÜKŞEHİR\nBELEDİYESİ\nÖZEL HALK OTOBÜSÜ", on_panel=None),
}


def font(size, serif=False):
    for path in ((SERIF,) if serif else ()) + FONTS:
        if os.path.exists(path):
            return ImageFont.truetype(path, int(size))
    return ImageFont.load_default()


def paint_mask(rgb):
    a = rgb.astype(np.float32)
    lum = a.mean(axis=2)
    sat = a.max(axis=2) - a.min(axis=2)
    return (lum > 150) & (sat < 30), lum


def recolor(rgb, mask, color):
    a = rgb.astype(np.float32)
    lum = a.mean(axis=2)
    shade = np.clip(lum / 245.0, 0.0, 1.08)[..., None]
    col = np.clip(np.array(color, np.float32)[None, None, :] * shade, 0, 255)
    out = a.copy()
    out[mask] = col[mask]
    return out.astype(np.uint8)


def ego_logo(draw, cx, cy, height, color=(214, 28, 32), bg=BEYAZ):
    """'EGO' yazısı; E harfinde parçalı yatay şeritler ve yanında amblem halkası."""
    f = font(height)
    bbox = draw.textbbox((0, 0), "EGO", font=f)
    w, h = bbox[2] - bbox[0], bbox[3] - bbox[1]
    x, y = cx - w // 2 - int(height * 0.25), cy - h // 2 - bbox[1]
    draw.text((x, y), "EGO", font=f, fill=color)
    e_w = draw.textbbox((0, 0), "E", font=f)[2]
    for k in (1, 2):
        yy = cy - h // 2 + int(h * k / 3) - int(height * 0.02)
        draw.rectangle([x, yy, x + int(e_w * 0.55), yy + max(2, int(height * 0.035))], fill=bg)
    r = int(height * 0.22)
    ex = x + w + int(height * 0.32)
    draw.ellipse([ex - r, cy - r, ex + r, cy + r], outline=color, width=max(3, int(height * 0.04)))
    draw.ellipse([ex - r // 2, cy - r // 2, ex + r // 2, cy + r // 2], fill=color)


def kurdele(draw, cx, cy, h, color=BEYAZ):
    """Mavi EGO otobüslerindeki büyük beyaz kurdele amblemi (stilize): iki kıvrımlı şerit."""
    w = max(6, int(h * 0.09))
    for k, sgn in enumerate((-1, 1)):
        pts = []
        for i in range(25):
            t = i / 24
            x = cx + sgn * (0.12 * h) + sgn * 0.35 * h * math.sin(t * math.pi) * (1 - 0.3 * t)
            y = cy - 0.5 * h + t * h
            pts.append((x, y))
        draw.line(pts, fill=color, width=w, joint="curve")
    draw.ellipse([cx - w, cy - 0.5 * h - w, cx + w, cy - 0.5 * h + w], fill=color)


def badge(draw, x, y, size):
    draw.rectangle([x, y, x + size, y + size], fill=(20, 80, 170))
    f = font(size * 0.42)
    bbox = draw.textbbox((0, 0), "EGO", font=f)
    draw.text((x + (size - bbox[2]) // 2, y + (size - bbox[3]) // 2 - bbox[1] // 2), "EGO", font=f, fill=(255, 255, 255))


def build(src_tex, cng_tex, spec, sx, sy):
    base = np.array(src_tex)
    mask, _ = paint_mask(base)

    def S(x0, y0, x1, y1):
        return int(x0 * sx), int(y0 * sy), int(x1 * sx), int(y1 * sy)

    m = mask.copy()
    keep = [S(40, 120, 70, 175), S(172, 120, 200, 175), S(60, 410, 180, 430)]  # arka stoplar, ön rozetler
    if not spec["tavan_boya"]:
        keep.append(S(*TAVAN))
        # yan şeritlerin tavan kenarı (cam bandının üstü) beyaz kalır
        keep.append((int(215 * sx), int(SAG["ust"][0] * sy), int(1000 * sx), int((SAG["ust"][0] + 8) * sy)))
        keep.append((int(215 * sx), int(SOL["ust"][0] * sy), int(1000 * sx), int((SOL["ust"][0] + 8) * sy)))
    for x0, y0, x1, y1 in keep:
        m[y0:y1, x0:x1] = False
    img = Image.fromarray(recolor(base, m, spec["govde"]))
    arr = np.array(img)

    # bel şeridi: cam bandının hemen altında (yalnızca boya pikselleri)
    if spec["serit"]:
        color, thick = spec["serit"]
        for side in (SAG, SOL):
            y0 = int(side["y_cam"] * sy)
            band = m[y0:y0 + int(thick * sy)]
            arr[y0:y0 + int(thick * sy)][band] = color
    # arka tekerlek üstünde beyaz EGO paneli
    panels = []
    if spec["ego_panel"]:
        panels = [S(330, 100, 470, 192), S(720, 400, 860, 487)]
        for x0, y0, x1, y1 in panels:
            arr[y0:y1, x0:x1][m[y0:y1, x0:x1]] = BEYAZ
    img = Image.fromarray(arr)
    draw = ImageDraw.Draw(img)

    for (x0, y0, x1, y1), dx in zip(panels, (int(14 * sx), 0)):
        ego_logo(draw, (x0 + x1) // 2 + dx, y0 + int((y1 - y0) * 0.38), int((y1 - y0) * 0.42))

    if spec["amblem"]:
        # gövdenin ortasında, iki tekerlek arasında
        for side in (SAG, SOL):
            cx = int(side["amblem_x"] * sx)
            cy = int((side["y_cam"] + 38) * sy)
            kurdele(draw, cx, cy, int(62 * sy))

    if spec["yazi"]:
        # arka tarafa doğru, cam bandının altında küçük beyaz yazı
        f = font(7.5 * sy, serif=True)
        for side in (SAG, SOL):
            cx = int(side["yazi_x"] * sx)
            y = int((side["y_cam"] + 10) * sy)
            draw.multiline_text((cx, y), spec["yazi"], font=f, fill=BEYAZ, anchor="ma", align="center",
                                spacing=int(2 * sy))

    if spec["filo"]:
        size = 13 * sy
        if spec["rozet"]:
            badge(draw, *S(682, 127, 0, 0)[:2], int(13 * sy))
            badge(draw, *S(222, 419, 0, 0)[:2], int(13 * sy))
            draw.text(S(700, 128, 0, 0)[:2], spec["filo"], font=font(size), fill=BEYAZ)
            draw.text(S(240, 420, 0, 0)[:2], spec["filo"], font=font(size), fill=BEYAZ)
        else:
            for side in (SAG, SOL):
                xy = (int(side["filo_x"] * sx), int((side["y_cam"] + 34) * sy))
                draw.text(xy, spec["filo"], font=font(10 * sy), fill=BEYAZ, anchor="ma")
        draw.text(S(118, 448, 0, 0)[:2], spec["filo"], font=font(11 * sy), fill=BEYAZ, anchor="ma")
        draw.text(S(118, 160, 0, 0)[:2], spec["filo"], font=font(10 * sy), fill=BEYAZ, anchor="ma")

    if spec["on_panel"]:
        x0, y0, x1, y1 = S(55, 462, 181, 476)
        draw.rectangle([x0, y0, x1, y1], fill=BEYAZ)
        draw.text(((x0 + x1) // 2, (y0 + y1) // 2), spec["on_panel"], font=font(4.6 * sy, serif=True),
                  fill=spec["govde"], anchor="mm")

    cng = None
    if spec["cng_boya"]:
        c = np.array(cng_tex)
        cm, _ = paint_mask(c)
        cng = Image.fromarray(recolor(c, cm, spec["govde"]))
    return img, cng


def plate(src_path, text="06 CUV 352"):
    """Türk tipi plaka: beyaz zemin, solda mavi TR şeridi. Kaynak dokudaki iki plakanın yerine çizilir."""
    src = Image.open(src_path).convert("RGB")
    W, H = src.size
    img = Image.new("RGB", (W, H), (20, 20, 20))
    draw = ImageDraw.Draw(img)
    for y0 in (int(H * 0.03), int(H * 0.53)):
        y1 = y0 + int(H * 0.40)
        x0, x1 = int(W * 0.04), int(W * 0.96)
        draw.rounded_rectangle([x0, y0, x1, y1], radius=int(H * 0.03), fill=(250, 250, 250), outline=(10, 10, 10),
                               width=max(2, int(H * 0.012)))
        strip = int(W * 0.09)
        draw.rectangle([x0 + 4, y0 + 4, x0 + strip, y1 - 4], fill=(10, 60, 160))
        draw.text((x0 + strip // 2 + 2, y1 - int(H * 0.06)), "TR", font=font(H * 0.07), fill=(255, 255, 255), anchor="ms")
        draw.text(((x0 + strip + x1) // 2, (y0 + y1) // 2), text, font=font(H * 0.27), fill=(10, 10, 10), anchor="mm")
    return img


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--src", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--kaplama", nargs="*", default=list(KAPLAMALAR))
    ap.add_argument("--plaka", default="06 CUV 352")
    args = ap.parse_args()
    os.makedirs(args.out, exist_ok=True)
    base = Image.open(os.path.join(args.src, "textures", "caroserie.png")).convert("RGB")
    cng = Image.open(os.path.join(args.src, "textures", "cngtank.png")).convert("RGB")
    sx, sy = base.size[0] / 1024, base.size[1] / 512
    for name in args.kaplama:
        img, cimg = build(base, cng, KAPLAMALAR[name], sx, sy)
        img.save(os.path.join(args.out, f"{name}.png"), optimize=True)
        if cimg is not None:
            cimg.save(os.path.join(args.out, f"{name}_cngtank.png"), optimize=True)
        img.resize((1024, 512)).save(os.path.join(args.out, f"_onizleme_{name}.png"))
        print("kaydedildi:", name)
    plate(os.path.join(args.src, "textures", "registration_plates.png"), args.plaka).save(
        os.path.join(args.out, "registration_plates.png"), optimize=True)


if __name__ == "__main__":
    main()
