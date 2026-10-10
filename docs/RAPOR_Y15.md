# Y15 raporu (yerel oturum, 10 Ekim 2026)

Telefon: Galaxy S24 FE (SM-S721B), USB. Kalite Yüksek, gündüz, yağmursuz (aksi yazılmadıkça).
Görüntüler: `docs/onizleme/y15/`. Oyun APK'sı: `~/Desktop/AnkaraOtobus.apk`.

## Hazırlık
- `git pull origin Ozan` → derleme hatası yok.
- **Ankara Bus → Mercedes Conecto (Körüklü) Prefabını Kur** (batch): gövde kutusu ön (0, 1.80, 3.76) 2.50×2.75×10.67, değişen yalnızca
  dosya kimlikleri.
- Hız sınırı: üç prefabda `SeferPuanlama.hizSiniriKmh: 70` (BMC, Millennium, Conecto). Hat sahnelerinde geçersiz kılma yok
  (`Hat1_KizilayAtakule.unity`, `Hat2_KizilayUlus.unity` içinde `hizSiniriKmh` geçmiyor).

## 3. Otomatik pilot S3 — Kuğulu → Cinnah dönüşünde takılma (kök neden bulundu, düzeltildi)

### Editor'de tekrarladı (bulutun `YolBoyuncaEngel` düzeltmesiyle)
`RunHat1OtobusBatch -otobus 2`:
```
[Surus] DURAK Kuğulu Park tamamlandı, t=172 sn
[Puan] çarpışma: Klasik_Broadway_Kirmizi, hız 9.8 m/s, otobüste (-1.2, 0.4, 9.1)
[Surus] TAKILDI konum=(25.49, 36.87, 1307.24) ilerleme=1348 m hız=0 öndeki=yok temas=Carpisma_Govde×Klasik_Broadway_Kirmizi(BoxCollider) … gaz=1.00 direksiyon=0.41
```
`öndeki=yok`: `YolBoyuncaEngel` aracı görmüyor, çünkü araç güzergâhın üstünde değil — **otobüs güzergâhtan çıkmış**.

### Kök neden: dönüş şeridi körüklü için çizilemez, pilot otobüsün kökünü izliyor
Pilota kavşak/viraj izi eklendi (`[Surus] İZ`, `OtomatikPilot.cs` yol hızı dönüş için düşürülen yerlerde 0,5 sn'de bir: kök, burun,
yön, direksiyon, burnun güzergâhtan sapması). Eski kodla:
```
İZ ilerleme=1326 konum=(10.3, 1284.6) burun=(11.0, 1293.8) yön=4°  hız=15 direksiyon=0.54 sapma_burun=0.9 m
İZ ilerleme=1334 konum=(12.2, 1292.3) burun=(17.1, 1300.1) yön=32° hız=20 direksiyon=0.82 sapma_burun=5.6 m
İZ ilerleme=1340 konum=(16.1, 1296.8) burun=(24.0, 1301.5) yön=59° hız=21 direksiyon=0.71 sapma_burun=6.5 m
[Puan] çarpışma: Modern_Linea_Gri, hız 6.4 m/s, otobüste (-0.8, 1.5, 9.1)
```
- `Serit_Baglanti_Bulvar_Cinnah` (trafik şeridi) Bulvar'ın sağ şeridinden (x 10,25) Cinnah gidiş şeridine (z 1295,1) 90°'yi **~3 m
  yarıçapla** dönüyor (sahneden okunan 13 nokta). Arabalar için sorun değil.
- Pilot pure pursuit ile otobüsün **kökünü** (ön gövdenin arka aksı) güzergâhta tutar. 18 m'lik körüklünün burnu kökün 9,2 m önünde:
  3 m'lik köşede burun 6,5 m dışarı savrulup Cinnah'ın orta çizgisini (z 1300) geçiyor, karşı yöndeki (`Serit_Cinnah_Donus_1/2`,
  `Serit_Baglanti_Cinnah_Kizilay`) araçlara çarpıyor. Trafik araçları kinematik: otobüsü itip sıkıştırıyor, pilot tam gazla ona
  yükleniyor → TAKILDI. Ayrıca otobüs dönüşte 21–23 km/s'e hızlanıyordu (yol hızı 6 nokta ilerisinden okunuyordu).
- Trafik aracı da otobüsü görmüyordu: `TrafficCar.FreeDistanceAhead` düz ileri kutu ışını; eğri şeritte (kavşak bağlantısı) yolu kesen
  otobüs ışının dışında kalıyor.

### Düzeltme
1. `Diagnostics/OtomatikPilot.cs` `KoseyiYuvarla`: güzergâhtaki kavşak bağlantı şeridinin (`Baglanti` adlı) köşesi, giriş ve çıkış
   doğrultularına teğet bir yay olur; yarıçap otobüsün ön boyuna göre (Conecto 10 m, BMC 7 m, Millennium 7,2 m). Yaydan sonraki durma
   çizgileri ve şerit parçaları yolun kısalması kadar kayar.
   `[Surus] köşe yuvarlandı: 90°, yarıçap 10.0 m, yay (10.3, 0.0, 1285.1) → (20.3, 0.0, 1295.1), 15 nokta → 13, yol -1.7 m`
2. Virajda hedef hız: önümüzdeki 12 m'nin **en düşük** yol hızı; körüklüde keskin dönüş 11 km/s (solo 15).
3. `Traffic/TrafficCar.cs` `OtobusSeritBoyunca`: araç, şeridi boyunca (kesin çıkışta sonraki şeritte de) önündeki 25 m'de otobüsün
   gövde kutularına (körüklüde iki gövde) 1,3 m'den yakın nokta varsa ona göre yavaşlar/durur. Otobüs uzaktaysa bakılmaz (ucuz).

Düzeltmeden sonra aynı dönüş (Conecto, Editor):
```
İZ ilerleme=1326 konum=(10.6, 1284.6) burun=(11.8, 1293.7) yön=8°  hız=10 direksiyon=0.48 sapma_burun=2.1 m
İZ ilerleme=1336 konum=(15.0, 1294.1) burun=(22.3, 1299.6) yön=53° hız=15 direksiyon=0.70 sapma_burun=4.4 m
İZ ilerleme=1349 konum=(27.5, 1297.6) burun=(36.6, 1296.8) yön=95° hız=21 direksiyon=0.10 sapma_burun=1.7 m
```
Burun en çok z 1299,6'ya geliyor (gidiş yarısında; köşesi orta çizgiye ~0,5 m taşabilir); Cinnah'taki araçların durma çizgisine
(x ≈ 38) gelmeden düzleşiyor. Takılma, çarpışma yok.

### Yan bulgular (pilotla ortaya çıktı, düzeltildi)
- **Kırmızı ışık (Conecto, kavşak):** pilotun ilerlemesi 2 m arayla dizilen güzergâh noktalarına yuvarlanıyordu; körüklü durma
  çizgisine payı sıfırla yanaşıp burnuyla çizgiyi geçiyordu (`ceza: Kırmızı ışık -50 (ilerleme 1316 m)`). İlerleme artık en yakın
  segment üzerine izdüşümle (`OtomatikPilot.Sur`).
- **Yaya (Conecto, Kuğulu sonrası geçit ve Cinnah sonrası):** `ceza: Yayaya çarptın -150`. İki neden:
  - Pilot yaya geçitlerine bakmıyordu → dolu geçidin önünde durur (`seritParcalari`, `TrafficLane.Gecitler`).
  - `Traffic/YayaGecitleri.cs` `Yaklasiyor`: 4 m/s'den yavaş gelen aracı "yaklaşmıyor" sayıyordu; 10 km/s ile geçide gelen otobüsün
    burnunun önüne yaya çıkıyordu. **Oyuncuyu da etkiler** (yavaşça geçide gelen oyuncu −150 alırdı). Şimdi 12 m'den yakın ya da
    6 sn içinde varacak araç da yaklaşıyor sayılır; otobüs için kök değil **ön uç** kullanılır (körüklüde 9 m fark).
- Pilot cezaları artık tek tek loglanır (`[Surus] ceza: … (ilerleme … m)`), PUAN satırında `yaya=`.

### `RunHat1OtobusBatch` (Editor, son kod)
| Otobüs | Durak | Süre | Puan | Kırmızı | Yaya | Çarpışma |
|---|---|---|---|---|---|---|
| BMC (0) | 5/5 | 310 sn | 427 (4★) | 0 | 0 | 0 |
| Millennium (1) | 5/5 | 315 sn | 432 (4★) | 0 | 0 | 0 |
| Conecto (2) | 5/5 | 332 sn | 444 (4★) | 0 | 0 | 0 |

Süre Y14'e göre Conecto 325 → 332 sn (dönüşte 11 km/s); BMC/Millennium değişmedi. Hedef süre 532 sn. Bir önceki koşuda Conecto
5/5 bitirdi ama Cinnah cebine girerken taksi, Atakule'de dolmuş arka gövdeye yandan sürttü (`otobüste (-1.2, 1.2, -9.2)`); trafik
aracının otobüs kontrolü yana kayışı (sollama, dolmuşun cebe yanaşması) hesaba katmıyordu → şimdiki ve hedef yan konumda bakılır,
menzil 25 m. Otobüse çarpan trafik aracı artık loglanır (`[Trafik] … otobüse çarptı: şerit, yan, hız, dolmuş_durağı`); son koşularda
satır yok.

TELEFON_S3

## 4. "Durak cebi kenarı itkisi" — aslında testin ışınlaması (kök neden bulundu, düzeltildi)
Kırpma loguna iki gövdenin son fizik adımındaki temasları eklendi (`Vehicle/KorukluOtobus.cs` `Temas`: çarpışan nesne, itki,
bağıl hız, her temas noktasının gövdedeki yeri, normali, derinliği). Keskin dönüş bölümünde:
```
[KorTest] BOLUM keskin_donus_tam_gaz
[Koruklu] ön gövde fırlatılıyordu (dikey 14.7 m/s, yalpa 5.3 rad/s): kırpıldı | ön temas: EGO_Durak (40 ms önce) … | arka temas: yok
[KorTest] keskin dönüş: durak Kızılay AVM, 40 km/s tam gaz tam sağ
```
- Kırpma **sürüşten önce**, test otobüsü Cinnah yokuşundan Kızılay AVM durağına ışınlarken oluyor. Ön gövdenin o anda hiçbir teması
  yok (son teması 40 ms önce Cinnah'taki durak). Yani durak cebinin bordürü, kaldırımı ya da giriş kenarı değil.
- Işınlamadan sonraki ilk fizik adımının kare logu (`KorukluTesti.KareIzle`):
  ```
  kare t=73.20 ön y=-0.05 v=(0,0,0)     … WC_OnSag: sıkışma-59.54/0.25 Cinnah_DurakCebi_Egim8_30m   (eski yerin teker verisi)
  kare t=73.22 ön y=0.09  v=(-1.68, 14.68, -1.11) w=5.23 … WC_OnSol sıkışma0.48/0.25  WC_OnSag sıkışma0.52/0.25 Bulvar_DurakCebi_40m
  ```
  Tekerler önceki yerin süspansiyon durumunu taşıyor; ilk adımda ön tekerler azami yolun (0,25 m) iki katı sıkışmış görünüyor,
  süspansiyon ön gövdeyi 14,7 m/s fırlatıyor. Her koşuda aynı değer (deterministik). Y14'teki `çarpışma … Bulvar_DurakCebi_40m,
  hız 10,8 m/s` bu fırlamadan sonra gövdenin cebe geri düşmesi.
- Oyunda otobüsü ışınlayan kod yok (yalnızca `Diagnostics`). Otomatik pilotun bütün Conecto koşularında (5 durak cebi, Cinnah eğimli cep
  dahil) `[Koruklu]` kırpması **0**; `kaldirim` bölümünde (25 km/s, 25° ile iki durağın kaldırımına tırmanma) de 0.
- Denenip bırakılanlar: ışınlamada WheelCollider'ı kapat-aç (durum sıfırlanmadı, 14,7 aynı), yerleştirme yüksekliğini her tekerin
  altındaki en yüksek zemine göre almak (yine 13,3).
- **Düzeltme** (`Diagnostics/KorukluTesti.cs` `Yerlestir`): ışınlamadan sonraki 10 oturma adımında (el freni) her fizik adımından
  sonra iki gövdenin hızı sıfırlanır; eski teker durumu bu sürede söner.
- **Sonuç (Editor, Hat 1, bütün bölümler):** `[Koruklu]` satırı **yok**. Keskin dönüş:
  ```
  önce:  SONUC keskin_donus_tam_gaz durum=OK maks_dikey=1.44 m/s maks_eğim=14.3° maks_yalpa=5.25 rad/s … mafsal_uyarısı=1 çarpışma=1 [Bulvar_DurakCebi_40m, hız 10.8 m/s …]
  sonra: SONUC keskin_donus_tam_gaz durum=OK maks_dikey=1.15 m/s maks_eğim=8.1°  maks_yalpa=0.82 rad/s … mafsal_uyarısı=0 çarpışma=0
  ```
  Aynı koşuda `yana_45m_isinlama`daki kırpma (dikey −5,3 m/s) da kalktı.

TELEFON_S4

KAMERA_ICERIK

HIZ_SINIRI

## Değişen dosyalar
- `Diagnostics/OtomatikPilot.cs` (köşe yayı, viraj hızı, ilerleme izdüşümü, yaya geçidi, iz ve ceza logu)
- `Traffic/TrafficCar.cs` (şerit boyunca otobüs), `Traffic/YayaGecitleri.cs` (yavaş yaklaşan araç, otobüsün ön ucu)
- `Vehicle/KorukluOtobus.cs` (kırpma logunda iki gövdenin temasları, gövde başına ayrı log zamanlayıcısı)
- `Diagnostics/KorukluTesti.cs` (ışınlamadan sonra oturma), `Diagnostics/KameraTesti.cs` (engel mesafesi, `y15_kamera_*.png`)
- Yeni `Diagnostics/HizTesti.cs` (görev `hiz`), `Diagnostics/Y11Hat.cs`, `Diagnostics/Y10Baslatici.cs`
- `Buses/MB_Conecto_G/MB_Conecto_G.prefab` (yeniden kuruldu)
