# Y14 raporu (yerel oturum, 10 Ekim 2026)

Telefon: Galaxy S24 FE (SM-S721B), USB. Kalite Yüksek, gündüz, yağmursuz (aksi yazılmadıkça).
Görüntüler: `docs/onizleme/y14/`. Oyun APK'sı: `~/Desktop/AnkaraOtobus.apk` (16:29, 155 MB, bu rapordaki bütün düzeltmeler içinde).
Telefon oturum boyunca üç kez USB'den düştü (ikisi Unity derlemesinden sonra, biri derleme yokken); yarım kalan adımlar sonda.

## Hazırlık
- `git pull` → derleme hatası yok. `Rigidbody.maxLinearVelocity` bu sürümde (6000.6.4f1) var, satır kaldı.
- `OtobusKurucu.BuildAllBatch` (üç prefab): **"eşleşmedi" uyarısı yalnızca `M_mepibis`** (Millennium IBIS tuşları, Y12'den beri bilinen,
  zararsız). Millennium için yeni `Materials/M_Gri.mat` çıktı (bulutun teker yuvası kopyaları). Conecto kutuları: ön (0, 1.80, 3.76) 2.50×2.75×10.67,
  arka (0, 0.80, −3.81) 2.50×2.75×7.61.

## 1. Körüklünün fırlaması

### Ölçüm aracı
Yeni `Scripts/Diagnostics/KorukluTesti.cs`. Her Y11 test görevinde **bekçi** olarak çalışır (sahne yüklenmeleri, başlangıçta gövde kutularına
değen nesneler, ilk 3 sn'de her fizik adımında ön/arka gövde konum-hız-mafsal ayrılması; sonra sürekli: hız > 90 km/s, dikey hız > 4 m/s,
eğim > 20°, bir gövdenin bütün tekerleri 0,4 sn havada, yerden 1 m yükselme → `[KorTest] FIRLADI`). `koruklu` görevinde
(`adb shell am start … -e gorev koruklu -e otobus 2`) ayrıca zorlama senaryolarını sürer. Editor: `SurusTestiEditor.RunKorukluBatch -otobus 2 -hat 1|2`,
menü **Ankara Bus → Körüklü Fırlama Testi (Hat 1)**.

### Açılışta (Hat 1 ve Hat 2, Editor ve telefon): otobüs yerinde duruyor
```
[KorTest] sahne yüklendi #2: Hat1_KizilayAtakule (Single), sıra: AnaMenu → Hat1_KizilayAtakule
[Otobus] Mercedes-Benz O530G Conecto (körüklü) seçildi (sahne Hat1_KizilayAtakule)
[KorTest] başlangıç temasları: yok
[KorTest] başlangıç arka gövde (ön gövdeye göre) (0.01, 1.00, -1.57), mafsal ayrılması 0.000 m, arka hız 0.00 m/s
[KorTest] SONUC baslangic durum=OK maks_hız=5 km/s maks_dikey=1.29 m/s maks_eğim=5.8° … havada=0.12 sn yükselme=0.00 m maks_ayrılma=0.001 m mafsal_uyarısı=0 çarpışma=0
```
Hat 2'de aynı değerler (konum (13.50, 0.35, 6.65)). `OtobusDegistirici.BosYer` kaydırma yapmadı (başlangıç yeri boş), `[Koruklu]` uyarısı yok.
İlk karelerde ön gövde 0,4 m'lik süspansiyon oturmasıyla 1,3 m/s dikey hız gösteriyor; arka gövde mafsalda (ayrılma ≤ 1 mm).

### S1 — Durak sundurmasına çarpınca körüklü fırlıyor / devriliyordu (düzeltildi, telefonda doğrulandı)
- **Otobüs:** Conecto. **Kamera:** hepsi. **Yer:** Hat 1 Kızılay AVM durağı (keskin dönüş, geri geri), Cinnah (ters yönde fren + geri).
- **Tekrar (eski APK):** `KorukluTesti` "keskin_donus_tam_gaz": Kızılay AVM durağında 40 km/s, tam gaz, tam sağ direksiyon.
- **Log (telefon, düzeltmeden önce):**
  ```
  [KorTest] FIRLADI bölüm=keskin_donus_tam_gaz ön gövde … konum=(13.57, 0.05, 20.28) v=(0.74, 14.65, 11.37) arka_v=(-1.12, 0.12, 10.09) ayrılma=0.02
  [KorTest] FIRLADI bölüm=keskin_donus_tam_gaz ön gövde eğimi 130° … son_çarpışma=EGO_Durak, hız 10.7 m/s, otobüste (1.4, 0.9, -5.2)
  [KorTest] SONUC keskin_donus_tam_gaz durum=FIRLADI … maks_dikey=11.30 m/s maks_eğim=142.8° havada=3.58 sn yükselme=8.95 m
  ```
  Mafsal ayrılmadı (≤ 4 cm); fırlayan ön gövde. Çarpışan nesne `EGO_Durak` (sundurma) ve durak cebi (`Bulvar_DurakCebi_40m`) kenarı.
- **Kök neden:** `Editor/HatCarpismalari.cs` durakları gerçek şekliyle, içbükey `MeshCollider` olarak çarpıştırıyordu. Sundurma ince paneller ve
  çatıdan oluşuyor: otobüs kutusu üçgenlerin içine girince PhysX onu üçgen normaline (çatıda yukarı) itiyor. Önce dışbükey yapmayı denedim
  (`convex`): bu kez kabuğun yere inen eğik yüzü rampa oldu, Cinnah'ta trafik aracına 53 km/s çarpan otobüs yan yattı (eğim 93°).
- **Düzeltme:**
  1. `Editor/HatCarpismalari.cs:18` duraklar artık kutu (`KutuGruplari`), `DurakKutusu` (`:70`) yerden 2,2 m'ye kadarki köşelerden, yanlardan
     15 cm paylı. Var olan sahneler için menü **Ankara Bus → Durak Çarpışmalarını Kutu Yap** (`:23`); Hat 1'de 5, Hat 2'de 4 durak değişti
     (ışık bake'i bozulmaz). Kutu 7,6×3,25×2,2 m.
  2. `Vehicle/KorukluOtobus.cs:30,125` her fizik adımında iki gövdede dikey hız 3 m/s'ye, yalpa/baş vurma açısal hızı 1,2 rad/s'ye kırpılır
     (`[Koruklu] … gövde fırlatılıyordu (dikey … m/s, yalpa … rad/s): kırpıldı`). Otobüs %10 yokuşu 85 km/s'de çıkarken bile 2,4 m/s yükselir.
- **Doğrulama (telefon, yeni APK, Hat 1 + Hat 2):** bütün bölümler `durum=OK`, `bölüm_fırlayan=0`. Aynı keskin dönüş:
  ```
  [Koruklu] ön gövde fırlatılıyordu (dikey 14.7 m/s, yalpa 5.2 rad/s): kırpıldı
  [KorTest] SONUC keskin_donus_tam_gaz durum=OK maks_hız=53 km/s maks_dikey=1.42 m/s maks_eğim=14.6° havada=0.24 sn yükselme=0.48 m … çarpışma=1 [Bulvar_DurakCebi_40m, hız 10.8 m/s …]
  ```
  Kırpma hâlâ tetikleniyor: durak cebinin (yol parçası) kenarına 39 km/s yandan çarpınca ön gövde bir adımda 14,7 m/s'lik itki alıyor.
  Koruma bunu sönümlüyor ama kaynağı (durak cebi kenarının çarpışma şekli) ayrı iş.

### Zorlama senaryoları (telefon, son APK)
| Bölüm | Hat 1 | Hat 2 |
|---|---|---|
| Binaya 45 km/s (`bina_45kmh`) | OK — A5_Ofis_10Kat_Mavi'ye 12,5 m/s, eğim 5,3°, yükselme 0,06 m | OK — A1_Bulvar_7Kat_Somon'a 12,5 m/s, eğim 7,3° |
| Durağa 45 km/s (`durak_45kmh`) | **atlandı** (önünde 10–40 m boş yol olan bir durak açısı bulunamadı); durağa çarpma kaldırım ve keskin dönüşte oldu | atlandı |
| Kaldırıma tırmanma (25°, 25 km/s, iki durak) | OK — EGO_Durak'a 4,8 m/s, eğim 6,7°, havada 0 | OK — eğim 8,9°, havada 0,14 sn |
| Cinnah: 40 km/s → sert fren → geri vites tam gaz → geri tam direksiyon | OK — eğim 8,1°, durma 1,6 sn | (Cinnah yok) |
| Cinnah ters yönde | OK — eğim 10°, 1 kırpma (arka, 1,6 m/s) | — |
| Keskin dönüşte tam gaz (40 km/s tam sağ, sonra duruştan tam sol; mafsal 52–54°) | OK (yukarıda) | OK — eğim 8,0° |
| Geri geri binaya 14 km/s (arka gövdeyle) | OK — çarpışma yok (bina 39 m, süre yetmedi) | OK — A1_Bulvar_6Kat_Krem'e 4,0 m/s, `[Puan] çarpışma … otobüste (-1.0, 0…` |
| Durak durak ışınlama (PerfBenchmark gibi) | OK | OK |
| 45 m yana ışınlama (Y10 rotadan çıkma gibi) | OK | OK |

Editor'de (aynı senaryolar) Hat 2 temiz; Hat 1'de bir koşuda **sınırda**: geri giderken mafsal 52° kırık, arka gövde kaldırıma çıkıp
tam 20,0° eğildi (havaya kalkmadı, yükselme 0,49 m). Telefonda tekrarlanmadı. Trafik araçları her koşuda farklı yerde olduğundan sonuçlar
koşudan koşuya biraz değişiyor.

### S2 — Testlerin kendi ışınlaması otobüsü durağın/binanın içine koyuyordu (düzeltildi)
- `Diagnostics/Y10Gozlemci.cs:311` `RotadanCik` 45 m yana **ve 2 m yukarı** ışınlıyordu; Hat 1'in son durağında (Atakule) otobüs
  `EGO_Durak` ve `Atakule_DonusHalkasi`nın içine düştü (Editor: arka gövde 1,8 sn havada, eğim 17°).
  Şimdi ±45…75 m'de zemine oturan ve bir binaya/durağa değmeyen ilk yer seçiliyor (`OtobusIsinla.ZemineTasi`, `OtobusIsinla.Temas`).
  Telefonda: `[Y10] rotadan çıkma: +55 m yana ışınlandı`, `[Y10] OK rotadan çıkınca uyarı ('ROTADAN ÇIKTIN / sarı çizgiye dön', sapma 53 m)`, fırlama yok.
- `PerfBenchmark` (telefon, Conecto, durak durak ışınlama + 3 kamera): fırlama, mafsal uyarısı, çarpışma yok; 5 durakta 58,4–60 FPS
  (Kızılay AVM dış 58,4 FPS, CPU 9,9 ms, GPU 10,7 ms, 589 draw).

### "seçildi" satırı neden birden çok kez görünüyordu
`OtobusDegistirici` her hat sahnesi yüklendiğinde bir kez değiştirir ve bir kez loglar; satıra artık sahne adı da yazılıyor
(`Vehicle/OtobusDegistirici.cs:118`). Bu oturumdaki bütün telefon koşularında sıra yalnızca `AnaMenu → Hat1 → Hat2` ve her hatta tek
"seçildi" (`[KorTest] sahne yüklendi #n` satırları). Y13'teki üç satır: Y11 test görevi Hat 1'den sonra Hat 2'yi yükler (2 satır); üçüncüsü
o oturumda uygulamanın yeniden başlatılmasından (test APK'sı yeniden kuruldu/başlatıldı) — fırlayan otobüs sahneyi yeniden yüklemiyor;
oyunda sahneyi yeniden yükleyen yalnızca PuanGostergesi "TEKRAR" ve ana menüye dönüş var (`UI/PuanGostergesi.cs:179`, `Gameplay/OyunSecimi.cs:84`).
Y13'teki 108 m/s, 167 m/s'lik çarpışmalar o oturumdaki test ışınlamalarıydı (S2).

### Otomatik pilot: `RunHat1OtobusBatch -otobus 2` (Editor)
```
[Surus] başlangıç konum=(13.52, 0.07, 6.65) temas=yok arka_gövde=(0.01, 1.00, -1.57) mafsal=0.1°
[Surus] DURAK Kızılay AVM … Meclis … Kuğulu Park … Cinnah … Atakule tamamlandı, t=325 sn
[Surus] PUAN puan=407 yıldız=4 kasa=430.00 yolcu=24 konfor=68 süre=328/532 sn atlanan_durak=0 kırmızı_ışık=0 sert_sürüş=10 hız_ihlali=0 çarpışma=0
[SurusTesti] BAŞARILI: [Surus] BITTI süre=326 sn, sorun sayısı 0
```

### S3 — Otomatik pilot Kuğulu → Cinnah dönüşünde takılıyor (Y12 S6, telefonda tekrarladı, açık)
- **Otobüs:** Conecto. **Yer:** Hat 1, Kuğulu Park'tan sonra Cinnah'a sağa dönüş (`Serit_Cinnah_Donus_2`). Yalnızca otomatik pilot + trafik.
- **Tekrar:** `am start … -e gorev y10 -e otobus 2 -e hiz 2` (Y10Gozlemci). Editor'deki koşuda olmadı.
- **Log:** `[Puan] çarpışma: Klasik_Sahin_Beyaz, hız 6.4 m/s, otobüste (-0.8, 1.4, 9.1)` → `… A3_Cankaya_5Kat_SariKirma, hız 5.9 m/s` →
  `[Surus] TAKILDI konum=(23.60, 36.77, 1303.67) … temas=Carpisma_Govde×Klasik_Sahin_Beyaz … gaz=1.00 direksiyon=0.87 … mafsal=-52.0°`
  (tekerlerde `motor=0`). Fırlama yok; trafik aracı dönüş şeridinde duruyor, pilot geniş dönüşte ona sürtünüp köşedeki binaya itiliyor.
- **Kaynak (tahmin):** `Diagnostics/OtomatikPilot.cs` yavaşlama yalnızca öndeki araca bakıyor, sol önden kesen araç sayılmıyor; ayrıca gaz 1'ken
  `motor=0` (vites/tork kesimi) `Vehicle/BusVehicle.cs` `Step` içinde incelenmeli. Ayrı iş.

## 2. Kamera
Yeni `Scripts/Diagnostics/KameraTesti.cs` (`-e gorev kamera -e otobus N`): beş kip × (başlangıç Kızılay binalar arası, Meclis durağı, düz yol
30 km/s, dönüş 15 km/s tam sağ — Conecto'da mafsal 42°, geri 8 km/s — mafsal 52°), her birinde 1,2 sn ölçüm: kamera otobüs kutusunun içinde mi,
bir binanın/durağın içinde mi, otobüse göre kareden kareye sıçrama (> 0,5 m), dönüş sıçraması (> 4°), titreme (ikinci fark > 5 cm). Ayrıca yolcu
gözünden yanlara/arkaya ve kokpitten sağa bakış.

| Sorun | Otobüs | Kamera | Yer | Durum |
|---|---|---|---|---|
| K1 Kamera durak sundurmasının içinde (`DUVARIN İÇİNDE 56/56 kare`) | üçü, her kalite | KAPI | Kızılay başlangıcı (sağda durak) | **düzeltildi**, telefonda BMC/Conecto 0 sorun |
| K2 İçeriden SERBEST'e geçince kamera 20 m'yi süzülerek alıyor (`SIÇRAMA 0.54–0.64 m/kare`) | Conecto | SERBEST | her yerde | **düzeltildi** |
| K3 Yolcu gözü körüğün içinde: sağa bakınca yalnız körük kıvrımları, önünde kırmızı direk | Conecto | YOLCU | her yerde | **düzeltildi**, telefonda görüntüyle doğrulanmadı |
| K4 Ön kapı görüntünün sağ kenarına sıkışıyor | Conecto | KAPI | Meclis | **düzeltildi**, telefonda görüntüyle doğrulanmadı |
| K5 Yakınlaştırınca (7 m) kamera arka gövdenin içinde | Conecto | DIŞ, SERBEST | her yerde | **düzeltildi** (kod incelemesi; arka uç kökten 9,2 m) |
| K6 Dönüşte mafsal kırıkken kamera karede 0,52 m oynuyor | Conecto | SERBEST | dönüş, 14 km/s, mafsal −42° | açık (sınırda) |

- **K1** `Vehicle/BusCameraRig.cs:256` `KapiEngeli`: gövde yanından kameraya küre ışını; engel varsa kamera önüne çekilir.
- **K2** `BusCameraRig.cs:134` içeriden DIŞ'a geçerken yapılan anında yerleştirme SERBEST'e de uygulanır.
- **K3** `BusCameraRig.cs:244` `YolcuKonumu`: körüklüde mafsalın 3 m önü (z −1,2 → +1,4). Eski: `tel_conecto_yolcu_eski.png`,
  `tel_conecto_yolcu_sag_koruk_eski.png`, `tel_conecto_yolcu_arka_eski.png`.
- **K4** kapı kamerasının baktığı nokta ön uca göre kaydırıldı (BMC'de değişmez). Eski: `tel_conecto_kapi_meclis_eski.png`; BMC iyi: `tel_bmc_kapi_meclis.png`.
- **K5** `BusCameraRig.cs:95` `EnYakinMesafe = max(7, arka boy + 2,5)`.
- **K6** SERBEST'te kamera 21 m geride; dönüşte otobüsün yönünü gecikmeli izlerken engel küre ışını (ağaç/bina) mesafeyi kısa süre kısaltıp
  bırakıyor olabilir (tahmin, `BusCameraRig.cs` `OutsidePosition`). DIŞ kipte aynı yerde sorun ölçülmedi.

Ölçümde sorun çıkmayanlar (eski APK, üç otobüs): DIŞ, KOKPİT, YOLCU hiçbir yerde otobüsün/duvarın içinde değil, sıçrama/titreme yok (içeride
kameralar otobüse sabit: 0,000 m). Görsel kontrol:
- **Conecto kokpit** (`tel_conecto_kokpit.png`): göz direksiyon/koltuk hizasında; yol, gösterge paneli, vites görünüyor. Sağ üstteki
  tabela kutusu görüşün üst ortasında küçük bir parça kapatıyor (HUD'un altında kalıyor); Y13'e göre küçük. Sağa bakınca (`tel_conecto_kokpit_sag.png`)
  ön kapının sarı tutunma borusu göze çok yakın, görüntünün sağ üçte birini kaplıyor (açık, küçük).
- **Millennium yolcu** sağ/sol ön (`tel_millennium_yolcu_sag_on.png`, `…_sol_on.png`): teker görünmüyor, yuvalar kapalı. ✔
- BMC ve Millennium KAPI kamerası durakta kapıları ve kaldırımı gösteriyor.

## 3. Y13 düzeltmeleri
- **Conecto içinde kırmızı/mavi:** pencere dikmeleri, alt bölmeler, A direği gri ✔ (`tel_conecto_yolcu_eski.png`, `tel_conecto_kokpit.png`).
  **Kalan:** körüğün önünde/yanında **kırmızı bir dikey çubuk** (yolcu arka bakış `tel_conecto_yolcu_arka_eski.png`, yolcu ileri bakışta ortada).
  Kaplama rengi mi, düğme direği mi belirsiz — bulut için: `tools/blender/conecto_ic.py` mafsal çevresindeki `M_Govde` parçaları.
- **Silecekler ve kapı fitilleri** sarı değil ✔; tutunma boruları sarı ✔.
- **Millennium teker yuvaları** içeriden kapalı ✔.
- **BMC "ACİL DURUM":** `Atlas_BMC.png` (Düşük/Normal) kol halkasının altında "ACİL DURUM" ✔ (`bmc_atlas_acil_durum.png`). Oyunda kol, test
  açılarına girmediği için ekranda görülmedi.

## 4. Y13'ten kalanlar
- **Yolcu sesi** ("Durakta inecek var!"): telefonda çaldığı loglandı — `[Y10] yolcu sesi: 'stop' düzey 0.15, kapı kapandıktan 18.3 sn sonra,
  kamera Chase` ve `… 8.5 sn sonra`. Düzey dış kamerada 0,15, kokpitte 0,40'a çıkar (`Vehicle/BusAudio.cs:242`). **Türkçe olduğu kulakla
  doğrulanmadı** (aşağıda).
- **Conecto 4 kapıdan yolcu:** otomatik pilot koşusunda (Editor) 24 yolcu bindi, 24 indi, durak başına 9–19 sn; kapı başına dağılım ölçülmedi.
- **Arka gövdeyle çarpışma cezası:** telefonda Hat 2 geri geri binaya: `[Puan] çarpışma: A1_Bulvar_6Kat_Krem, hız 4.0 m/s, otobüste (-1.0, 0…)`
  (arka gövdenin çarpışması `KorukluOtobus.ArkaCarpti` → `SeferPuanlama` cezası) ✔.
- **Eğerek direksiyon:** test görevi hazır (`Diagnostics/EgimTesti.cs`, `-e gorev egim`: 60 sn eğim açısı + direksiyon logu, aynı anda yolcu sesi 6 sn'de bir
  kokpit/dış sırayla). Telefon koptuğu için koşmadı.

## Bitmeyen adımlar
- Son APK ile (kamera düzeltmeleri) **Millennium kamera testi** ve **Conecto/BMC görüntülerinin çekilmesi**: telefon USB'den düştü
  (`adb: device 'R5CXA2NZ50B' not found`). Ölçüm satırları alındı (BMC 0 sorun, Conecto yalnızca K6), görüntüler alınamadı.
- **Eğerek direksiyon ve yolcu sesinin kulakla dinlenmesi:** `EgimTesti` kişi telefonu tutarken koşmalı:
  `adb shell am start -n com.ankarabus.simulator/com.unity3d.player.UnityPlayerGameActivity -e gorev egim -e otobus 0` (test APK'sı `Builds/AnkaraBus_y11_test.apk`).
- Conecto ile elle sürüş (binaya çarpma, kaldırım, Cinnah) yalnızca otomatik senaryolarla yapıldı; dokunmatikle elle denenmedi.

## Değişen dosyalar
- `Editor/HatCarpismalari.cs` (durak kutusu, menü), `Scenes/Hat1_KizilayAtakule.unity`, `Scenes/Hat2_KizilayUlus.unity` (durak çarpışmaları)
- `Vehicle/KorukluOtobus.cs` (kırpma), `Vehicle/BusCameraRig.cs` (K1–K5), `Vehicle/OtobusDegistirici.cs` (log)
- `Diagnostics/OtobusIsinla.cs` (`Temas`, `ZemineTasi`), `Diagnostics/Y10Gozlemci.cs` (`RotadanCik`), `Diagnostics/Y11Hat.cs`, `Diagnostics/Y10Baslatici.cs`
  (görevler `koruklu`, `kamera`, `egim`), yeni `Diagnostics/KorukluTesti.cs`, `KameraTesti.cs`, `EgimTesti.cs`, `Editor/SurusTestiEditor.cs` (`RunKorukluBatch`)
- Prefablar yeniden kuruldu (Millennium `M_Gri`, Conecto)

## Görüntüler (`docs/onizleme/y14/`)
| Dosya | İçerik |
|---|---|
| `tel_firlama_hat1_*.png`, `tel_firlama_hat2_*.png` | zorlama senaryolarının sonu (son APK): otobüs dik, yerinde |
| `tel_conecto_kokpit.png`, `tel_conecto_kokpit_sag.png` | Conecto kokpit; sağa bakışta sarı boru göze yakın |
| `tel_conecto_yolcu_eski.png`, `tel_conecto_yolcu_sag_koruk_eski.png`, `tel_conecto_yolcu_arka_eski.png` | K3 öncesi, kırmızı çubuk |
| `tel_conecto_kapi_meclis_eski.png`, `tel_bmc_kapi_meclis.png`, `tel_bmc_kapi_baslangic_eski.png` | K4 öncesi, BMC karşılaştırma, K1 öncesi |
| `tel_conecto_donus_dis.png` | Conecto dönüşte dış kamera |
| `tel_millennium_yolcu_sag_on.png`, `tel_millennium_yolcu_sol_on.png` | teker yuvaları kapalı |
| `tel_bmc_dusuk_yolcu_sag_on.png`, `bmc_atlas_acil_durum.png` | BMC Düşük iç, atlas "ACİL DURUM" |
