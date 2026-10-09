"""
Otobüs içi etiketleri Türkçeleştirir: BMC Procity (stickers.png: Rumence/İngilizce) ve Caio Millennium II
(adesivostransparentes.png, bagulhosmep.png, extras.png: Portekizce / São Paulo → Ankara: hat şeridi, afiş,
Ankarakart okuyucu, Alo 153, sürücü talimatı). Yazı alanı etiketin zemin rengiyle örtülür, Türkçe yazı sığdırılarak çizilir.
Rumence etiketlerin yerine Türkçesi gelir, İngilizceler kalır (iki dilli). Simgeler değişmez.

Kaynak dokular ilk çalıştırmada `tools/etiket_kaynak/` altına saklanır (Unity dışında); sonraki çalıştırmalar ondan başlar.

Kullanım:
    python tools/etiketler_tr.py --proje AnkaraBusSimulator/Assets/_Project
"""
import argparse
import os
import shutil
import sys

from PIL import Image, ImageDraw

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kaplama import font  # noqa: E402

# (kutu (x0, y0, x1, y1), zemin rengi ya da None = kutunun köşesinden al, yazı, yazı rengi, en büyük punto, hiza)
BMC = [
    # yeşil acil çıkış vanası (Rumence)
    ((490, 650, 842, 812), (40, 129, 89), "ACİL ÇIKIŞ VANASI\n\nACİL DURUMDA\n1- KOLU OK YÖNÜNDE ÇEVİRİN\n2- KAPIYI AÇIN",
     (255, 255, 255), 30, "la"),
    # sarı: kapıya yaslanmayın (Rumence)
    ((1292, 686, 1690, 806), (240, 185, 0), "KAPIYA\nYASLANMAYINIZ", (10, 10, 10), 46, "ma"),
    # sarı: klima (yalnız İngilizcesi vardı → Türkçe)
    ((1192, 1100, 1530, 1236), (240, 185, 0), "KLİMA ÇALIŞIRKEN\nPENCEREYİ AÇMAYINIZ", (10, 10, 10), 34, "ma"),
    # mor: tekerlekli sandalye yeri (Rumence)
    ((702, 1632, 1036, 1846), (57, 46, 146),
     "Tekerlekli sandalye yeri.\nSandalye sırtı sürüş yönüne\nbakacak şekilde, desteğe\nya da arkalığa dayalı ve\n"
     "freni çekili durmalıdır.", (255, 255, 255), 30, "ma"),
]

MILLENNIUM = [
    # öncelikli koltuk (saydam zemin üstünde beyaz yazı)
    ((22, 92, 388, 176), (0, 0, 0, 0),
     "OBEZ, HAMİLE, KUCAĞINDA BEBEK YA DA ÇOCUK OLAN,\nYAŞLI VE ENGELLİ YOLCULARA ÖNCELİKLİ KOLTUKTUR.\n"
     "BU YOLCULAR YOKSA HERKES KULLANABİLİR.", (255, 255, 255), 16, "ma"),
    # sol etiket: başlık ve üç simgenin altı
    ((28, 214, 300, 238), None, "ÖZEL ALAN", (40, 30, 120), 20, "ma"),
    ((24, 352, 114, 398), (250, 250, 250), "TEKERLEĞİ\nDAYAYIN", (40, 30, 120), 13, "ma"),
    ((118, 352, 208, 398), (250, 250, 250), "KEMERİ\nTEKERLEĞE\nGEÇİRİN", (40, 30, 120), 13, "ma"),
    ((212, 352, 304, 398), (250, 250, 250), "KEMERİ ÇEKİP\nKİLİTLEYİN", (40, 30, 120), 13, "ma"),
    # sağ etiket: başlık ve alt yazı
    ((660, 30, 924, 74), None, "Özel Alan", (20, 20, 20), 38, "ma"),
    ((582, 256, 1006, 302), (250, 250, 250),
     "TEKERLEKLİ SANDALYELİ VE REHBER KÖPEKLİ\nGÖRME ENGELLİ YOLCULAR İÇİN AYRILMIŞTIR", (40, 30, 120), 22, "ma"),
]


def yaz(img, kutu, zemin, metin, renk, punto, hiza):
    d = ImageDraw.Draw(img)
    x0, y0, x1, y1 = kutu
    if zemin is None:
        zemin = img.getpixel((x0 + 2, y0 + 2))
    d.rectangle(kutu, fill=zemin)
    # sığana kadar küçült
    for p in range(punto, 7, -1):
        f = font(p)
        sol, ust, sag, alt = d.multiline_textbbox((0, 0), metin, font=f, spacing=int(p * 0.25),
                                                  align="left" if hiza == "la" else "center")
        if sag - sol <= x1 - x0 - 6 and alt - ust <= y1 - y0 - 4:
            break
    yuk = alt - ust
    y = y0 + (y1 - y0 - yuk) // 2 - ust
    if hiza == "la":
        d.multiline_text((x0 + 4, y), metin, font=f, fill=renk, spacing=int(p * 0.25), align="left")
    else:
        d.multiline_text(((x0 + x1) // 2, y), metin, font=f, fill=renk, spacing=int(p * 0.25), align="center",
                         anchor="ma")


def isle(yol, liste):
    klasor = os.path.join(os.path.dirname(os.path.abspath(__file__)), "etiket_kaynak")
    os.makedirs(klasor, exist_ok=True)
    orijinal = os.path.join(klasor, os.path.basename(yol))
    if not os.path.exists(orijinal):
        shutil.copy(yol, orijinal)
    img = Image.open(orijinal).convert("RGBA")
    for kutu, zemin, metin, renk, punto, hiza in liste:
        z = zemin if zemin is None or len(zemin) == 4 else (*zemin, 255)
        yaz(img, kutu, z, metin, (*renk, 255), punto, hiza)
    img.save(yol, optimize=True)
    print("yazıldı:", yol)


HAT1 = ("Kızılay AVM", "Meclis", "Kuğulu Park", "Cinnah", "Atakule")


def kutu_yazi(d, kutu, metin, renk, punto, hiza="ma", kalin=True):
    """Kutuya sığan en büyük puntoyla (en çok `punto`) ortalı/sola dayalı yazı."""
    x0, y0, x1, y1 = kutu
    for p in range(punto, 7, -1):
        f = font(p, serif=not kalin)
        sol, ust, sag, alt = d.multiline_textbbox((0, 0), metin, font=f, spacing=int(p * 0.3),
                                                  align="left" if hiza == "la" else "center")
        if sag - sol <= x1 - x0 and alt - ust <= y1 - y0:
            break
    y = y0 + (y1 - y0 - (alt - ust)) // 2 - ust
    if hiza == "la":
        d.multiline_text((x0, y), metin, font=f, fill=renk, spacing=int(p * 0.3), align="left")
    else:
        d.multiline_text(((x0 + x1) // 2, y), metin, font=f, fill=renk, spacing=int(p * 0.3), align="center",
                         anchor="ma")


def millennium_bagulhos(yol):
    """bagulhosmep.png: çöp kutusu etiketi, kart okuyucu (Ankarakart), sağdaki ihbar hattı şeridi (Alo 153)."""
    img = kaynaktan(yol)
    d = ImageDraw.Draw(img)
    d.rectangle((262, 106, 506, 170), fill=(83, 84, 88))
    kutu_yazi(d, (262, 114, 506, 162), "ÇÖP", (235, 235, 235), 40)
    d.rectangle((1240, 380, 1680, 500), fill=(35, 36, 40))
    kutu_yazi(d, (1250, 390, 1670, 470), "ANKARAKART", (205, 205, 210), 64)
    kutu_yazi(d, (1250, 470, 1670, 500), "kartınızı okutun", (170, 170, 175), 26)
    serit = Image.new("RGB", (1120, 128), (255, 255, 255))
    sd = ImageDraw.Draw(serit)
    kutu_yazi(sd, (20, 10, 640, 118), "ALO 153", (10, 10, 10), 96)
    kutu_yazi(sd, (660, 14, 1100, 114), "Ankara Büyükşehir Belediyesi\nÇözüm Merkezi · 7/24", (10, 10, 10), 34)
    img.paste(serit.rotate(90, expand=True), (1920, 0))
    img.save(yol, optimize=True)
    print("yazıldı:", yol)


def millennium_extras(yol):
    """extras.png: hat şeridi (São Paulo → Hat 1), afiş (EGO / Ankarakart), sürücü tabela talimatı."""
    img = kaynaktan(yol)
    d = ImageDraw.Draw(img)
    # hat şeridi: fırçalanmış metal zeminde kırmızı noktalı durak dizisi
    d.rectangle((4, 4, 1020, 168), fill=(176, 186, 193))
    d.text((14, 10), "Hat 1", font=font(20), fill=(40, 40, 40))
    d.text((1010, 10), "Kızılay AVM – Atakule", font=font(20), fill=(40, 40, 40), anchor="ra")
    d.line((60, 140, 960, 140), fill=(190, 30, 30), width=4)
    for i, ad in enumerate(HAT1):
        x = 60 + i * 205
        d.ellipse((x - 8, 132, x + 8, 148), fill=(190, 30, 30), outline=(255, 255, 255), width=2)
        etiket = Image.new("RGBA", (200, 40), (0, 0, 0, 0))
        ImageDraw.Draw(etiket).text((4, 20), ad, font=font(22), fill=(30, 30, 30, 255), anchor="lm")
        etiket = etiket.rotate(35, expand=True, resample=Image.BICUBIC)
        img.paste(etiket, (x - 6, 128 - etiket.size[1]), etiket)
    # afiş
    d.rectangle((0, 304, 460, 1024), fill=(250, 250, 248))
    d.rectangle((0, 304, 460, 420), fill=(214, 28, 32))
    kutu_yazi(d, (20, 316, 440, 408), "EGO", (255, 255, 255), 90)
    kutu_yazi(d, (20, 450, 440, 560), "ANKARAKART\nile kolay yolculuk", (214, 28, 32), 56)
    kutu_yazi(d, (30, 600, 430, 820), "Binişte kartınızı\nön kapıdaki okuyucuya\nokutun.\n\nİniş orta ve arka\nkapılardan yapılır.",
              (30, 30, 30), 34)
    d.rectangle((0, 900, 460, 1024), fill=(214, 28, 32))
    kutu_yazi(d, (20, 910, 440, 1014), "Ankara Büyükşehir Belediyesi\nEGO Genel Müdürlüğü", (255, 255, 255), 30)
    # sürücü talimatı (tabela kumandası)
    d.rectangle((472, 432, 826, 880), fill=(204, 204, 204))
    kutu_yazi(d, (480, 436, 818, 486), "KULLANIM TALİMATI", (10, 10, 10), 40)
    kutu_yazi(d, (484, 500, 816, 872),
              "Sayın Sürücü,\n\nBu otobüste gerçek zamanlı hat tabelası\nvardır; garaja bağlanmak gerekmez.\n\n"
              "İstediğiniz bölümü (1/2/3) seçip\nmesajınızı yazın. En çok 16 karakter.\n\n"
              "Yeni hat girilince özel mesajlar silinir.\nBunu önlemek için sağ üstteki\ndüğmeyi açın.\n\n"
              "Özel mesajlarda özenli olun.", (20, 20, 20), 20, hiza="la")
    img.save(yol, optimize=True)
    print("yazıldı:", yol)


def kaynaktan(yol):
    klasor = os.path.join(os.path.dirname(os.path.abspath(__file__)), "etiket_kaynak")
    os.makedirs(klasor, exist_ok=True)
    orijinal = os.path.join(klasor, os.path.basename(yol))
    if not os.path.exists(orijinal):
        shutil.copy(yol, orijinal)
    return Image.open(orijinal).convert("RGB")


def bmc_acil_kol(yol):
    """stickers.png alttaki yuvarlak acil çıkış kolu: halkanın altındaki "EMERGENCY" yerine yay üstünde "ACİL DURUM"
    (üstteki "ACİL ÇIKIŞ KOLU" kaynakta zaten Türkçeydi). Halka merkezi (308, 1749), turuncu bant r 215–246 px."""
    import math
    img = Image.open(yol).convert("RGBA")
    d = ImageDraw.Draw(img)
    cx, cy, r0, r1 = 308, 1749, 215, 246
    turuncu = (212, 56, 24, 255)
    for a in range(5800, 12200, 4):            # 58°–122° (görüntüde 90° = alt)
        t = math.radians(a / 100)
        d.line([(cx + r0 * math.cos(t), cy + r0 * math.sin(t)), (cx + r1 * math.cos(t), cy + r1 * math.sin(t))],
               fill=turuncu, width=3)
    metin, f = "ACİL DURUM", font(27)
    rm = (r0 + r1) / 2
    genis = [d.textlength(h, font=f) + 3 for h in metin]
    toplam = sum(genis) / rm                   # yay uzunluğu → açı (radyan)
    t = math.pi / 2 + toplam / 2               # soldan başla (alt yayda soldan sağa açı azalır)
    for h, g in zip(metin, genis):
        orta = t - (g / rm) / 2
        harf = Image.new("RGBA", (48, 48), (0, 0, 0, 0))
        ImageDraw.Draw(harf).text((24, 24), h, font=f, fill=(248, 196, 48, 255), anchor="mm")
        harf = harf.rotate(90 - math.degrees(orta), resample=Image.BICUBIC)
        x, y = cx + rm * math.cos(orta), cy + rm * math.sin(orta)
        img.alpha_composite(harf, (int(x - 24), int(y - 24)))
        t -= g / rm
    img.save(yol, optimize=True)


def bmc_ikizleri_ve_atlas(stickers_yol, atlas_yol):
    """stickers.png'de yeşil (acil çıkış vanası) ve sarı (kapıya yaslanmayın) etiketin iki kopyası var; model ikisini de
    kullanıyor. Türkçeleştirilen sağdaki kopyayı soldakinin (İngilizce) üzerine yazar. Düşük/Normal ayardaki BMC
    etiketleri Atlas_BMC.png'den okur (bmc_donustur.build_atlas: 2048'lik doku 1024'lük hücrede, sol üst (2068, 4));
    güncel stickers.png o hücreye de yazılır."""
    img = Image.open(stickers_yol).convert("RGBA")
    # (Türkçe kopya, İngilizce kopya) panelleri, 2048 px dokuda
    for tr, en in (((466, 459, 868, 861), (49, 456)), ((1292, 462, 1700, 894), (880, 460))):
        img.paste(img.crop(tr), en)
    img.save(stickers_yol)
    atlas = Image.open(atlas_yol).convert("RGB")
    atlas.paste(img.convert("RGB").resize((1024, 1024), Image.LANCZOS), (2068, 4))
    atlas.save(atlas_yol, optimize=True)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--proje", required=True)
    args = ap.parse_args()
    isle(os.path.join(args.proje, "Buses/BMC_Procity_12LF/Textures/stickers.png"), BMC)
    bmc_acil_kol(os.path.join(args.proje, "Buses/BMC_Procity_12LF/Textures/stickers.png"))
    bmc_ikizleri_ve_atlas(os.path.join(args.proje, "Buses/BMC_Procity_12LF/Textures/stickers.png"),
                          os.path.join(args.proje, "Buses/BMC_Procity_12LF/Textures/Atlas_BMC.png"))
    isle(os.path.join(args.proje, "Buses/Caio_Millennium_II/Textures/adesivostransparentes.png"), MILLENNIUM)
    millennium_bagulhos(os.path.join(args.proje, "Buses/Caio_Millennium_II/Textures/bagulhosmep.png"))
    millennium_extras(os.path.join(args.proje, "Buses/Caio_Millennium_II/Textures/extras.png"))


if __name__ == "__main__":
    main()
