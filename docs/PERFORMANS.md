# Performans Ölçümleri

## Ölçüm 1 — 8 Ekim 2026 (Y4)

**Cihaz:** Samsung Galaxy S24 FE (SM-S721B), Exynos 2400e, GPU Samsung Xclipse 940, 7 GB RAM, Android 16, ekran 2340×1080, Vulkan.
**Build:** Development, IL2CPP ARM64, hedef 60 FPS. APK 67 MB.
**Koşullar:** USB'ye bağlı (şarj oluyordu), pil 34–36 °C, ısıl durum 0 (kısma yok). Her ölçüm 10 sn, öncesinde 1,5 sn bekleme.
**Sahneler:** `TestTrack` (yalnızca otobüs + düz pist) ve `Hat1_KizilayAtakule` (499 obje, ışık/occlusion bake'i yok, LOD yok). Hat 1'de otobüs her durağa ışınlanıp dış takip ve kokpit kamerasıyla ölçüldü. Trafik açıktı.

Nasıl tekrar edilir: **Ankara Bus → Android → Performans APK'sı**, telefona yükle, uygulamayı aç, `adb logcat -s Unity | grep "\[Perf\]"`.
Ölçüm kodu (`PerfBenchmark`) yalnızca bu build'e eklenir; sahne dosyaları değişmez.

### Sonuçlar

`cpu` ve `gpu`: bir karenin gerçek işlem süresi (ms). FPS 60'a sabitlendiği için asıl pay göstergesi bunlar; 16,7 ms'nin altı = 60 FPS tutar.

| Yer | Kamera | FPS | CPU ms | GPU ms | Draw call | Üçgen | Gölge veren |
|---|---|---|---|---|---|---|---|
| TestTrack, duran | dış | 60 | 5,9 | 6,5 | 141 | 312 bin | 66 |
| TestTrack, duran | kokpit | 60 | 6,1 | 7,6 | 99 | 262 bin | 50 |
| TestTrack, tam gaz | dış | 60 | 6,4 | 6,5 | 139 | 312 bin | 65 |
| Hat 1 · Kızılay AVM | dış | 60 | 7,9 | **10,0** | **358** | **1,23 M** | 86 |
| Hat 1 · Kızılay AVM | kokpit | 60 | 7,9 | **10,3** | 305 | 1,18 M | 71 |
| Hat 1 · Meclis | dış | 60 | 5,9 | 8,3 | 311 | 821 bin | 80 |
| Hat 1 · Meclis | kokpit | 60 | 7,2 | 9,4 | 256 | 754 bin | 79 |
| Hat 1 · Kuğulu Park | dış | 60 | 6,9 | 6,8 | 172 | 447 bin | 73 |
| Hat 1 · Kuğulu Park | kokpit | 60 | 6,7 | 7,9 | 164 | 406 bin | 86 |
| Hat 1 · Cinnah | dış | 60 | 7,0 | 7,2 | 209 | 540 bin | 82 |
| Hat 1 · Cinnah | kokpit | 60 | 6,9 | 8,4 | 190 | 489 bin | 79 |
| Hat 1 · Atakule | dış | 60 | 6,8 | 6,3 | 134 | 396 bin | 67 |
| Hat 1 · Atakule | kokpit | 60 | 6,7 | 7,6 | 84 | 344 bin | 44 |

- **Bellek:** TestTrack 260 MB (grafik 197 MB), Hat 1 324 MB (grafik 251 MB). Sorun yok.
- **SetPass:** 13–21. SRP Batcher çalışıyor; materyal değişimi sorun değil.
- **Kare süresi:** her yerde p95 16,8 ms, en kötü %1 ≥ 58 FPS. Takılma yok.
- Üçgen sayısına gölge geçişi de dahil (gölge veren objeler iki kez çizilir).

### Değerlendirme

Bu telefonda her yer 60 FPS, en ağır yerde (Kızılay) bile GPU'da %40 pay var. Ancak S24 FE üst-orta sınıf bir telefon. **MVP hedefi "orta seviye telefonda ≥ 30 FPS"**; bütçemiz (<150 draw call, görünür alanda <300 bin üçgen) Kızılay'da 2,4 kat (draw call) ve 4 kat (üçgen) aşılıyor. Orta seviye bir GPU'nun bu telefondakinden kabaca 2–4 kat yavaş olduğu varsayılırsa Kızılay'da 30 FPS sınırda kalır. Bu bir tahmin; ikinci bir telefonda ölçülmeli.

**Yükün kaynağı:**
1. **Otobüsün kendisi:** boş pistte bile 140 draw call ve 312 bin üçgen. Yani bütçenin tamamını tek başına harcıyor. Sebep: 196 bin üçgenlik model (çoğu iç mekân: koltuklar, tutamaklar, kokpit) ve 35 materyal; her materyal ayrı draw call, gölge geçişinde bir daha.
2. **Kızılay ve Meclis bölümünün binaları:** otobüs çıkarılınca Kızılay'da ~220 draw call ve ~900 bin üçgen. Uzaktaki binalar da tam detayla çiziliyor (LOD yok, occlusion culling bake edilmedi).

### Önerilen optimizasyon sırası

1. **Otobüs (en büyük kazanç, tek model):**
   - Dış kamera için iç mekânı sadeleştirilmiş bir LOD (OTOBUS_BMC_PROCITY.md'de önerilen) ve materyalleri birkaç atlasta birleştirmek. Hedef: dış görünüm < 40 draw call, < 50 bin üçgen.
   - Gölgeyi iç mekân parçaları değil, basit bir gövde kutusu versin (iç parçalarda Cast Shadows kapalı).
2. **Binalarda LOD:** LOD0 / LOD1 / görünmez (≈ 300–400 m'de kaybolur). Bulut tarafındaki bina kitine LOD1 eklenmesi gerekiyor.
3. **Occlusion culling bake'i ve ışık bake'i** (Y5'te zaten planlı). Bulvarda iki yanı apartman dolu olduğu için occlusion iyi sonuç verir.
4. Küçük sokak objelerinde (lamba, ağaç, durak) gölgeyi kapatmak ve kamera mesafesine göre katman bazlı çizim mesafesi (layer cull distance).

Her adımdan sonra aynı APK ile tekrar ölçülür ve buraya yeni bir "Ölçüm N" bölümü eklenir.

## Ölçüm 2 — 8 Ekim 2026, düşük seviye telefon

**Cihaz:** Samsung Galaxy A32 (SM-A325F), MediaTek Helio G80, GPU Mali-G52 MC2, 6 GB RAM, Android 13, ekran 2400×1080 **90 Hz**, Vulkan.
**Build:** Ölçüm 1 ile aynı (Development, hedef 60 FPS).
**Koşullar:** Pil %14'ten şarj oluyordu, 35 °C, **ısıl durum 2 (orta) baştan sona**. Telefon büyük olasılıkla bir miktar kısılmış durumdaydı; sonuçlar kötümser olabilir.
Bu GPU'da kare zamanlama (CPU/GPU ms) desteklenmiyor; yalnızca FPS var. Draw call ve üçgen sayıları Ölçüm 1 ile aynı.

**Ekran 90 Hz olduğu için FPS basamaklı:** kare 90 Hz yenilemenin katlarına oturuyor (45 = 2 yenileme, 30 = 3, 22,5 = 4, 18 = 5). Hedef 60 FPS 90 Hz ekranda tam tutturulamadığından **dış kameradaki 45 FPS bir tavan**; telefonun daha hızlı çizebildiği yerlerde de 45 görünür.

Aşağıda iki build var: varsayılan ayar (render scale 0.8, MSAA 2x) ve deneme (render scale 0.6, MSAA kapalı; repoya alınmadı).

| Yer | Kamera | FPS varsayılan | %1 en kötü | FPS 0.6 + MSAA yok | %1 en kötü |
|---|---|---|---|---|---|
| TestTrack, duran | dış | 44,9 | 44,8 | 45,0 | 44,6 |
| TestTrack, duran | kokpit | **22,8** | 11,3 | **33,9** | 29,9 |
| TestTrack, tam gaz | dış | 44,7 | 30,0 | 45,0 | 44,6 |
| Hat 1 · Kızılay AVM | dış | **29,0** | 15,0 | **32,9** | 22,5 |
| Hat 1 · Kızılay AVM | kokpit | **18,0** | **4,3** | **26,5** | 18,0 |
| Hat 1 · Meclis | dış | 37,1 | 8,2 | 44,2 | 30,0 |
| Hat 1 · Meclis | kokpit | 23,1 | 11,3 | 32,8 | 30,0 |
| Hat 1 · Kuğulu Park | dış | 45,0 | 44,8 | 45,0 | 44,7 |
| Hat 1 · Kuğulu Park | kokpit | 25,6 | 22,5 | 37,1 | 30,0 |
| Hat 1 · Cinnah | dış | 44,8 | 44,8 | 45,0 | 44,7 |
| Hat 1 · Cinnah | kokpit | 23,4 | 9,0 | 36,6 | 30,0 |
| Hat 1 · Atakule | dış | 44,9 | 44,8 | 45,0 | 44,7 |
| Hat 1 · Atakule | kokpit | 27,0 | 15,0 | 38,6 | 30,0 |

- **Bellek:** 239 MB (grafik 166 MB), sistem toplamı 650 MB'a kadar. 6 GB RAM'de sorun yok.

### Değerlendirme

1. **Varsayılan ayarla A32, MVP hedefini (≥ 30 FPS) kokpitte hiçbir yerde, dış kamerada Kızılay'da tutturamıyor.** Kızılay kokpitte en kötü %1 kare 4 FPS: belirgin takılma.
2. **Kokpit, daha az obje çizmesine rağmen dış kameradan yaklaşık iki kat yavaş → darboğaz piksel doldurma (fill-rate).** Kokpitte ekranın tamamı yakındaki iç mekân, gösterge paneli ve üst üste binen saydam camlarla kaplı; her piksel birkaç kez gölgeli olarak çiziliyor.
3. **Çözünürlüğü düşürmek bunu doğruladı:** render scale 0.6 + MSAA kapalı ile kokpit %40–50 hızlandı (23 → 34–39 FPS), Kızılay kokpitte en kötü kare 4 → 18 FPS'e çıktı. Kızılay dış kamerada ise kazanç küçük (29 → 33): orada piksel değil **obje/üçgen sayısı** sınırlıyor (Ölçüm 1'deki 358 draw call, 1,2 M üçgen).

### Güncellenmiş optimizasyon sırası

1. **Kalite kademesi (en hızlı kazanç, kod/ayar):** Zayıf GPU'larda otomatik düşük kalite: render scale ~0.6–0.7, MSAA kapalı, gölge mesafesi ve kalitesi düşük. Güçlü telefonlar (S24 FE) mevcut ayarda kalır. Kademe GPU'ya veya ilk saniyelerdeki FPS'e göre seçilir.
2. **Kokpitin piksel maliyeti:** camları ucuzlatmak (Unlit saydam veya iç/dış iki katman yerine tek katman), kokpit kamerasında iç mekân parçalarının gölge almasını/vermesini kapatmak.
3. **Otobüs LOD ve materyal atlası** (Ölçüm 1'deki 1. madde): dış kamerada 140 draw call / 312 bin üçgen.
4. **Binalarda LOD, occlusion ve ışık bake'i:** Kızılay dış kamera için asıl çözüm bu.
5. **90 Hz ekranlar için hedef FPS:** 60 hedefi 90 Hz ekranda 45'e düşüyor. Ekran 90 Hz ise hedefi 45 (kararlı) ya da 90 (güçlü telefonlarda) seçmek, 60/120 Hz ekranlarda 60 kalmak.

Telefon soğukken (ısıl durum 0) ve şarjda değilken tekrar ölçmek, kısılmanın etkisini ayırmak için faydalı olur.

## Uygulanan optimizasyonlar

- **Otobüs (bulut, `bmc_donustur.py`):** İç mekân %45'e sadeleştirildi, 30 doku tek atlasta, gölgeyi yalnızca 170 üçgenlik `Golge_Govde` veriyor. Tahmini otobüs maliyeti ~140 → ~30 draw call, 158 → 124 bin üçgen. Ayrıntı: `docs/OTOBUS_BMC_PROCITY.md` → Performans. Prefab yerelde yeniden kuruldu, ölçüldü: aşağıda Ölçüm 3.

## Ölçüm 3 — 8 Ekim 2026, optimize otobüsle (Galaxy A32)

Bulutun otobüs optimizasyonu (iç mekân sadeleştirme, doku atlası, `Golge_Govde` gölge vekili) sonrası, prefab ve sahneler yeniden kurularak. Ayarlar varsayılan (render scale 0.8, MSAA 2x). Koşullar Ölçüm 2 ile aynı: şarjda, pil %17, **ısıl durum 2**.

| Yer | Kamera | FPS önce → sonra | %1 en kötü önce → sonra | Draw call önce → sonra | Üçgen önce → sonra |
|---|---|---|---|---|---|
| TestTrack, duran | dış | 44,9 → 45,0 | 44,8 → 44,6 | 141 → **55** | 312 → **125 bin** |
| TestTrack, duran | kokpit | 22,8 → 23,8 | 11,3 → 12,9 | 99 → 39 | 262 → 98 bin |
| Hat 1 · Kızılay AVM | dış | 29,0 → **34,3** | 15,0 → **29,9** | 364 → 279 | 1,24 M → 1,05 M |
| Hat 1 · Kızılay AVM | kokpit | 18,0 → 22,1 | 4,3 → 18,0 | 313 → 243 | 1,18 M → 1,02 M |
| Hat 1 · Meclis | dış | 37,1 → **45,0** | 8,2 → 44,7 | 300 → 211 | 823 → 633 bin |
| Hat 1 · Meclis | kokpit | 23,1 → 24,1 | 11,3 → 22,5 | 284 → 214 | 763 → 596 bin |
| Hat 1 · Kuğulu Park | dış | 45,0 → 45,0 | 44,8 → 44,6 | 180 → 98 | 450 → 263 bin |
| Hat 1 · Kuğulu Park | kokpit | 25,6 → 26,9 | 22,5 → 22,5 | 160 → 106 | 410 → 245 bin |
| Hat 1 · Cinnah | dış | 44,8 → 45,0 | 44,8 → 44,7 | 219 → 124 | 546 → 355 bin |
| Hat 1 · Cinnah | kokpit | 23,4 → 26,1 | 9,0 → 22,5 | 203 → 117 | 495 → 324 bin |
| Hat 1 · Atakule | dış | 44,9 → 45,0 | 44,8 → 44,6 | 131 → 43 | 396 → 208 bin |
| Hat 1 · Atakule | kokpit | 27,0 → 28,4 | 15,0 → 22,5 | 84 → 27 | 344 → 182 bin |

- **Bellek:** 239 → 200 MB (grafik 166 → 127 MB).
- **Gölge veren obje:** 82 → 32 (Kızılay).

### Değerlendirme

- **Dış kamera artık her yerde ≥ 30 FPS**; Kızılay'daki takılma gitti (en kötü %1: 15 → 30). Kızılay ve Meclis'te kalan yük binalar: Kızılay'da hâlâ 279 draw call ve 1 M üçgen → bina LOD'u ve occlusion (sıradaki adım).
- **Kokpit neredeyse değişmedi (22–28 FPS)**: obje sayısı yarıya inse de piksel maliyeti aynı. Ölçüm 2'deki teşhisi doğruluyor. Kokpit için çözüm kalite kademesi (render scale 0.6, MSAA kapalı → Ölçüm 2'de 34–39 FPS) ve ucuz cam.

**Sıradaki adımlar:** (1) zayıf GPU'lar için otomatik düşük kalite kademesi + kokpit camı, (2) bina LOD'u, (3) occlusion ve ışık bake'i (Y5).

## Görüntü kalitesi seviyeleri

Oyunda **AYARLAR → Görüntü Kalitesi** ile seçilir, cihazda saklanır. İlk açılışta GPU'ya göre öneri seçilir (`GrafikAyarlari.CihazaGoreOner`): Xclipse, Immortalis, Adreno 7xx/8xx, Mali-G710+ → Yüksek; Mali-G3x/G5x, Adreno 6xx'in alt serisi ve altı, PowerVR, < 3,5 GB RAM → Düşük; diğerleri → Normal. Kurulum: **Ankara Bus → Proje Ayarlarını Uygula** (`KaliteKurulumu`).

| Seviye | Render scale | MSAA | Kaynak |
|---|---|---|---|
| Yüksek | 0.8 | 2x | Ölçüm 1'deki ayar (Galaxy S24 FE) |
| Normal | 0.7 | kapalı | ikisinin arası (henüz ölçülmedi) |
| Düşük | 0.6 | kapalı | Ölçüm 2'de A32'de denenen ayar |

Ortak: gölge 60 m, 1024, tek kademe, HDR kapalı.

### Ölçüm 4 — Galaxy A32, otomatik seçilen Düşük

A32 ilk açılışta **Düşük** seçti. Optimize otobüsle, ısıl durum 1 (Ölçüm 2–3'te 2 idi, yani telefon biraz daha serindi).

| Yer | Dış FPS (%1) | Kokpit FPS (%1) |
|---|---|---|
| TestTrack | 45,0 (44,7) | 36,2 (30,0) |
| Kızılay AVM | 38,2 (30,0) | 28,7 (22,5) |
| Meclis | 45,0 (44,6) | 35,8 (30,0) |
| Kuğulu Park | 45,0 (44,6) | 38,9 (30,0) |
| Cinnah | 45,0 (44,7) | 37,2 (30,0) |
| Atakule | 45,0 (44,6) | 41,7 (30,0) |

Düşük ayarda A32 her yerde ≥ 30 FPS, yalnızca Kızılay kokpit 28,7 (bina LOD'u ve occlusion ile çözülecek).
