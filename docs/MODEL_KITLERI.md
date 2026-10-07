# Model Kitleri: Binalar ve Yollar

![Yol kiti](onizleme/yol_kiti_v1.png)

Tüm modeller `tools/blender/` altındaki scriptlerle **üretilir**. Elle düzenlemek yerine scripti değiştirip yeniden üretmek tercih edilir; böylece her şey tutarlı kalır.

```bash
pip install bpy matplotlib           # Python 3.13 için bpy 5.x
python tools/blender/apartman_kit.py --out <klasör> [--render onizleme.png] [--only A1_Bulvar_6Kat_Krem ...]
python tools/blender/yol_kit.py      --out <klasör> [--render onizleme.png]
```

## Ortak palet materyali

Bina ve yolların hepsi **tek bir doku** kullanır: `Assets/_Project/Materials/T_AnkaraPalet.png` (8×8 renk karesi). Her yüzün UV'si bir renk karesinin ortasına bakar. Sonuç olarak:
- Bütün bina ve yollar **tek materyali** paylaşır. Mobilde static batching ile çok az draw call olur.
- Renk değiştirmek için palet dokusunu düzenlemek yeterlidir.

**Unity'de kurulum:**
1. `T_AnkaraPalet.png` → Inspector: **Filter Mode: Point**, **Generate Mip Maps: kapalı**, Compression: None ya da ASTC 4x4.
2. `Materials/M_AnkaraPalet` adında URP/Lit (veya mobil için URP/Simple Lit) materyal oluştur, Base Map = `T_AnkaraPalet`, Smoothness ~0.1.
3. FBX'lerin Import Settings → Materials sekmesinde **Material Creation Mode: None** yap ve prefablarda `M_AnkaraPalet`'i ata. Ya da Remapped Materials ile `M_AnkaraPalet` materyaline bağla.
4. Model sekmesinde **Generate Lightmap UVs** açık olsun (ışık bake için gerekli).

Palete yeni renk eklenecekse `apartman_kit.py` içindeki `PALETTE` sözlüğünün **sonuna** eklenmeli. Araya eklenirse eski FBX'lerin renkleri kayar.

## Eksen ve pivot kuralları (Unity)

> Blender → FBX → Unity dönüşümü hesapla belirlendi. Unity'de ilk açılışta bir bina ve bir yol parçasını sahneye koyup kontrol edin. Yön tersse bu belgeyi düzeltin.

| | Pivot | Yön |
|---|---|---|
| Binalar | Ön cephe çizgisinin ortası, zemin seviyesi (0) | Ön cephe **+Z**'ye bakar. Yolun kenarına dizerken binayı ön cephesi yola bakacak şekilde döndürün. |
| A4 yamaç binaları | Aynı | Zeminin altına 3 m inen taş kaide var; yokuşta binayı kaideye gömerek yerleştirin. |
| Yollar | Başlangıç kenarının ortası, yol yüzeyi (0) | Yol **+Z** yönünde ilerler, sağ taraf **+X**. |

## Bina listesi

| Model | Tip | Boyut (G × D × Y, m) | Üçgen |
|---|---|---|---|
| `A1_Bulvar_6Kat_Krem` | Bulvar apartmanı, dükkânlı | 14.6 × 15.3 × 25.0 | 4236 |
| `A1_Bulvar_7Kat_Somon` | Bulvar apartmanı, dükkânlı | 18.2 × 15.3 × 28.0 | 7248 |
| `A1_Bulvar_8Kat_Bej` | Bulvar apartmanı, dükkânlı | 14.6 × 15.3 × 31.0 | 5544 |
| `A2_Kose_7Kat_Gri` | Köşe apartmanı (köşe sol önde) | 18.8 × 15.2 × 28.0 | 10880 |
| `A2_Kose_6Kat_Somon` | Köşe apartmanı (köşe sol önde) | 15.2 × 15.2 × 25.0 | 8708 |
| `A3_Cankaya_5Kat_SariKirma` | 70'ler Çankaya, kırma çatı | 11.8 × 14.1 × 19.1 | 2370 |
| `A4_Yamac_5Kat_Yesil` | Yamaç, taş kaide, bahçe | 11.0 × 15.4 × 24.0 | 3408 |
| `A4_Yamac_4Kat_BeyazKirma` | Yamaç, taş kaide, bahçe, kırma çatı | 15.4 × 15.8 × 22.5 | 3056 |
| `A5_Ofis_10Kat_Mavi` | Cam ofis | 21.6 × 18.5 × 44.5 | 468 |
| `A5_Ofis_8Kat_Yesil` | Cam ofis | 18.6 × 18.5 × 37.3 | 408 |

## Yol parçaları

Kesitler:
- **Bulvar** (Atatürk Bulvarı): 2×3 şerit × 3,5 m, ortada 3 m çimli refüj, iki yanda 4,5 m kaldırım. Toplam genişlik 33 m.
- **Cinnah**: 2×2 şerit × 3,25 m, ortada çift düz çizgi, iki yanda 3 m kaldırım. Toplam genişlik 19 m.
- Bordür yüksekliği 15 cm.

Bir parçanın **bitiş konumu ve yönü**, sonraki parçanın başlangıcıdır. Parçaları uç uca eklerken sonrakini bu konuma koyup Y ekseninde bu açı kadar döndürün.

| Parça | Bitiş konumu (x, y, z) | Bitiş yönü (Y ekseni) | Üçgen |
|---|---|---|---|
| `Bulvar_Duz_20m` | (+0.00, +0.00, +20.00) | +0.0° | 96 |
| `Bulvar_Egim2_20m` | (+0.00, +0.40, +20.00) | +0.0° | 384 |
| `Bulvar_Egim4_20m` | (+0.00, +0.80, +20.00) | +0.0° | 384 |
| `Bulvar_DurakCebi_40m` | (+0.00, +0.00, +40.00) | +0.0° | 172 |
| `Bulvar_DurakCebi_Egim4_40m` | (+0.00, +1.60, +40.00) | +0.0° | 872 |
| `Kavsak_Bulvar_Cinnah_T` | (+0.00, +0.00, +40.00) | +0.0° | 320 |
| `Cinnah_Duz_20m` | (+0.00, +0.00, +20.00) | +0.0° | 64 |
| `Cinnah_Egim8_20m` | (+0.00, +1.60, +20.00) | +0.0° | 272 |
| `Cinnah_Egim10_20m` | (+0.00, +2.00, +20.00) | +0.0° | 272 |
| `Cinnah_Egim12_20m` | (+0.00, +2.40, +20.00) | +0.0° | 272 |
| `Cinnah_Viraj_Sag15_Egim10` | (+2.60, +2.00, +19.77) | +15.0° | 272 |
| `Cinnah_Viraj_Sol15_Egim10` | (-2.60, +2.00, +19.77) | -15.0° | 272 |
| `Cinnah_Viraj_Sag30_Egim8` | (+7.68, +2.40, +28.65) | +30.0° | 406 |
| `Cinnah_Viraj_Sol30_Egim8` | (-7.68, +2.40, +28.65) | -30.0° | 406 |
| `Cinnah_DurakCebi_Egim8_30m` | (+0.00, +2.40, +30.00) | +0.0° | 488 |
| `Atakule_DonusHalkasi` | (+0.00, +0.00, +14.00) | +0.0° | 968 |

Notlar:
- `Kavsak_Bulvar_Cinnah_T`: Yan yolun (Cinnah) ağzı parçanın ortasında (Z = 20), sağ tarafta. Yan yol koluna 12 m'lik başlangıç dahil; Cinnah parçaları **(X = +28.5, Z = +20)** noktasından, Y ekseninde **+90°** dönmüş olarak başlar.
- `Atakule_DonusHalkasi`: 14 m giriş yolu + halka (ada yarıçapı 12 m). Atakule adanın merkezine oturur: **(0, 0, +29.75)**. Tablodaki bitiş, giriş yolunun ucudur.
- Durak cepleri sağ kaldırımda, 3 m derinliğinde, uçları 8 m'lik geçişle. `BusStop` objesini cebin ortasına koyun.
- Fizik: Yol FBX'lerine **Mesh Collider** ekleyin. Kaldırım bordürleri de collider'da olduğu için otobüs bordüre çarpar.
- Yollar araziyle birleşmez; yol kenarlarında 0,6 m aşağı inen etek var. Arazi (Terrain) bu eteğin biraz altında kalacak şekilde düzenlenmeli.
