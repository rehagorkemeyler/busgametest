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

## Uygulanan optimizasyonlar

- **Otobüs (bulut, ):** İç mekân %45'e sadeleştirildi, 30 doku tek atlasta, gölgeyi yalnızca 170 üçgenlik  veriyor. Tahmini otobüs maliyeti ~140 → ~30 draw call, 158 → 124 bin üçgen. Ayrıntı:  → Performans. **Ölçüm 2 bekleniyor:** prefab yeniden kurulduktan sonra aynı APK ile.
