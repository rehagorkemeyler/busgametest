"""
BMC Procity 12LF için EGO (Ankara) kaplaması üretir.

Kaynak: Proton modunun beyaz gövde dokusu (caroserie.png, 4096x2048).
- Beyaz boya alanları gölgeleri korunarak EGO kırmızısına boyanır; siyah cam bandı ve detaylar korunur.
- Arka tekerlek üzerine beyaz panel ve kırmızı "EGO" logosu, önde ve yanlarda filo numarası, mavi EGO rozeti.
- Tavan CNG modülü (cngtank.png) da kırmızıya boyanır.

Doku yerleşimi (4096x2048, piksel):
  üst şerit  y ≈  80–760 : sağ yan (kapılar), ön sağda, arka tekerlek x ≈ 1680
  alt şerit  y ≈ 1240–1940: sol yan, ön solda, arka tekerlek x ≈ 3080
  sol üst  : arka yüz, sol alt: ön yüz

Kullanım:
    python kaplama_ego.py --src "BMC Procity 12LF/BMC Procity 12LF" --out <klasör> [--filo "22-352"]
"""
import argparse
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFont

EGO_KIRMIZI = np.array([214, 28, 32], dtype=np.float32)
FONT = "/usr/share/fonts/opentype/inter/InterDisplay-Bold.otf"


def font(size):
    for path in (FONT, "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
                 "/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf"):
        if os.path.exists(path):
            return ImageFont.truetype(path, size)
    return ImageFont.load_default()


def repaint(rgb, keep_boxes=()):
    """Beyaz, doygunluğu düşük boya piksellerini kırmızıya çevirir (parlaklık gölge olarak korunur)."""
    a = rgb.astype(np.float32)
    lum = a.mean(axis=2)
    sat = a.max(axis=2) - a.min(axis=2)
    mask = (lum > 150) & (sat < 30)
    for x0, y0, x1, y1 in keep_boxes:
        mask[y0:y1, x0:x1] = False
    shade = np.clip(lum / 245.0, 0.0, 1.08)[..., None]
    red = np.clip(EGO_KIRMIZI[None, None, :] * shade, 0, 255)
    out = a.copy()
    out[mask] = red[mask]
    return out.astype(np.uint8), mask


def white_panel(img, paint_mask, x0, y0, x1, y1):
    """Boya alanı içinde kalan kısmı beyaz yapar (tekerlek boşluğu ve camlar etkilenmez)."""
    arr = np.array(img)
    region = paint_mask[y0:y1, x0:x1]
    arr[y0:y1, x0:x1][region] = (246, 246, 244)
    return Image.fromarray(arr)


def ego_logo(draw, cx, cy, height):
    """Kırmızı 'EGO' yazısı; E harfinde EGO logosundaki gibi yatay çizgiler ve yanında amblem halkası."""
    f = font(int(height))
    text = "EGO"
    bbox = draw.textbbox((0, 0), text, font=f)
    w, h = bbox[2] - bbox[0], bbox[3] - bbox[1]
    x, y = cx - w // 2 - int(height * 0.25), cy - h // 2 - bbox[1]
    draw.text((x, y), text, font=f, fill=(214, 28, 32))
    # E harfinin üzerine beyaz yatay şeritler (logodaki parçalı E)
    e_w = draw.textbbox((0, 0), "E", font=f)[2]
    for k in range(1, 3):
        yy = cy - h // 2 + int(h * k / 3) - int(height * 0.02)
        draw.rectangle([x, yy, x + int(e_w * 0.55), yy + max(2, int(height * 0.035))], fill=(246, 246, 244))
    # amblem halkası
    r = int(height * 0.22)
    ex = x + w + int(height * 0.32)
    draw.ellipse([ex - r, cy - r, ex + r, cy + r], outline=(214, 28, 32), width=max(3, int(height * 0.04)))
    draw.ellipse([ex - r // 2, cy - r // 2, ex + r // 2, cy + r // 2], fill=(214, 28, 32))


def badge(draw, x, y, size):
    draw.rectangle([x, y, x + size, y + size], fill=(20, 80, 170))
    f = font(int(size * 0.42))
    bbox = draw.textbbox((0, 0), "EGO", font=f)
    draw.text((x + (size - bbox[2]) // 2, y + (size - bbox[3]) // 2 - bbox[1] // 2), "EGO", font=f, fill=(255, 255, 255))


def label(draw, x, y, text, size, fill=(255, 255, 255), anchor="la"):
    draw.text((x, y), text, font=font(size), fill=fill, anchor=anchor)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--src", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--filo", default="22-352")
    args = ap.parse_args()
    os.makedirs(args.out, exist_ok=True)

    base = Image.open(os.path.join(args.src, "textures", "caroserie.png")).convert("RGB")
    W, H = base.size
    sx, sy = W / 1024, H / 512  # yerleşim 1024x512 ölçeğinde ölçüldü

    def S(x0, y0, x1, y1):
        return int(x0 * sx), int(y0 * sy), int(x1 * sx), int(y1 * sy)

    keep = [
        S(40, 120, 70, 175), S(172, 120, 200, 175),   # arka stoplar
        S(60, 410, 180, 430),                         # ön rozetler
    ]
    rgb, mask = repaint(np.array(base), keep)
    img = Image.fromarray(rgb)

    # arka tekerlek üstündeki beyaz EGO paneli (iki yan)
    panels = [S(330, 100, 470, 192),    # sağ yan: arka tekerlek x≈420
              S(720, 400, 860, 487)]    # sol yan: arka tekerlek x≈770
    for p in panels:
        img = white_panel(img, mask, *p)
    draw = ImageDraw.Draw(img)
    shifts = [int(14 * sx), 0]  # sağ yanda modelin kendi çıkartması logonun soluna denk geliyor
    for (x0, y0, x1, y1), dx in zip(panels, shifts):
        ego_logo(draw, (x0 + x1) // 2 + dx, y0 + int((y1 - y0) * 0.38), int((y1 - y0) * 0.42))

    # filo numarası ve rozet: yanlarda ön kapı/ön tekerlek gerisinde, ön yüzde camın altında
    size = int(13 * sy)
    label(draw, *S(700, 128, 0, 0)[:2], f"EGO {args.filo}", size)
    badge(draw, *S(682, 127, 0, 0)[:2], int(13 * sy))
    label(draw, *S(240, 420, 0, 0)[:2], f"EGO {args.filo}", size)
    badge(draw, *S(222, 419, 0, 0)[:2], int(13 * sy))
    label(draw, *S(118, 448, 0, 0)[:2], f"EGO {args.filo}", int(11 * sy), anchor="ma")
    # arka yüzde filo numarası
    label(draw, *S(118, 160, 0, 0)[:2], f"EGO-{args.filo}", int(10 * sy), anchor="ma")

    img.save(os.path.join(args.out, "caroserie.png"), optimize=True)

    cng = Image.open(os.path.join(args.src, "textures", "cngtank.png")).convert("RGB")
    c_rgb, _ = repaint(np.array(cng))
    Image.fromarray(c_rgb).save(os.path.join(args.out, "cngtank.png"), optimize=True)
    img.resize((1024, 512)).save(os.path.join(args.out, "_onizleme_doku.png"))
    print("kaydedildi:", args.out)


if __name__ == "__main__":
    main()
