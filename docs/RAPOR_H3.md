# H3 raporu (yerel oturum, 15 Ekim 2026) — Hat 408 Kızılay → Güneşevler, Hat 2 ilk yerel test

Testler Editor'de (oynatmadan geometri denetimi: `Editor/H3Kurulum.cs` → `AnalizBatch`, `NoktaBatch`, `YenidenAktarBatch`, `Hat2Batch`)
ve telefonda (S24 FE; Y11 test APK'sı, yeni `h3` görevi: `Diagnostics/H3Testi.cs`, `-e sahne Hat3_KizilayGunesevler -e gorev h3`).
A32 bu turda bağlı değildi. Görüntüler `docs/gorseller/h3_yerel/` (`editor/` sissiz geometri görüntüleri, `s24/` oyun içi),
editör analizinin tam çıktısı `docs/gorseller/h3_yerel/editor_analizi.txt`. Düzeltmeleri bulut yapar; aşağıda her bulgu dosya/konum/görüntüyle.

Konumlar: dünya koordinatı (x, y, z) ve güzergâhta Kızılay'dan uzaklık **s** (m, `RotaRehberi` şerit yolu, toplam **9 133 m**);
"Durak+N m" en yakın durağa uzaklık.

## 1. Kurulum ve derleme

- `git pull --rebase` (8fbfe2d). **Derleme hatası yok** (bulutun Unity 6 API'leri sorunsuz derlendi).
- İlk açılış: 352 FBX + dokular dahil ilk içe aktarma (Asset Pipeline Refresh) **25,6 sn**.
- **Hata: FBX'ler `M_H3_*` malzemelerine bağlanmamıştı.** `Maps/Hat3/Materials/` ilk açılışta yoktu (yalnız sahne kurulurken
  `Hat3Malzemeler.HepsiniKur` oluşturuyor); FBX'ler o sırada içe aktarıldığı için `Hat3IceAktarma.OnAssignMaterialModel` malzeme
  bulamadı ve FBX'ler kendi gömülü, **dokusuz** malzemelerini aldı (`H3_Cephe`, `H3_Asfalt` … düz renk). Pembe yoktu ama her şey
  tek renkti (`editor/kopru_01_yaklasma` ilk hali gibi).
  Yerelde çözüm: `H3Kurulum.YenidenAktarBatch` (malzemeleri kur → 352 FBX'i yeniden içe aktar, 4 sn → Hat 3 ve Hat 2 sahnelerini kur).
  Şimdi sahnede 1 309 renderer'ın hepsi `M_H3_*` (13 malzeme + `M_H3_Carpisma`), **pembe/null 0**.
  **Bulut için:** malzemeler FBX içe aktarılmadan önce var olmalı (ör. `Hat3IceAktarma.OnPreprocessModel`'de `Hat3Malzemeler.HepsiniKur()`
  ya da `.mat` dosyaları repoda), yoksa her temiz klonda aynı sorun çıkar.
- Bulut `.meta` dosyalarını göndermemişti (Hat3 klasörü, 352 FBX, dokular, anonslar, `Hat3Parcalari.cs`, `Harita/*.cs`); Unity'nin
  ürettikleri ve `Maps/Hat3/Materials/` bu commit'te.
- **Hata: uzak şehir halkası ve tek çocuklu parçalar sahneye hiç girmiyordu.** Tek nesneli FBX'lerde (61 `H3U_*_Ufuk`, ayrıca yalnız
  `SimgeYapilar` ya da yalnız `Yol` içeren parçalar) Unity kök düğümü çocukla birleştiriyor (`preserveHierarchy` kapalı); `Hat3Parcalari.Kur`
  çocuk bulamayıp örneği siliyordu — uyarı da yok. İlk kurulumda hiyerarşi: SimgeYapilar(51) Yol(186), **Ufuk grubu yok**: harita kenarını
  gizleyecek 2 km'lik halka oyunda yoktu.
  **Yerelde düzeltildi** (tek satır, `Hat3IceAktarma.OnPreprocessModel`: `mi.preserveHierarchy = true`) ve yeniden içe aktarıldı.
  Şimdi **Ankara Bus → Hat 3 Sahnesini Kur**: "[Hat 3] 352 parça kuruldu", "274 obje ve 18 durak yerleştirildi"; eksik FBX/model uyarısı **yok**.
  Hiyerarşi `Hat3_KizilayGunesevler`: SimgeYapilar(105) Duraklar(18) Lambalar(250) Yol(234) Zemin(180) Binalar(359) Agaclar(123)
  **Ufuk(61)** Hat_408(18) Trafik(66: 56 şerit + 10 ışık) ✔; 1 472 renderer, pembe 0. Hat 408 '**Kızılay – Güneşevler**', 18 durak ✔.
- **Hat 2 Sahnesini Kur** (bake yok): "[Hat 2] 274 obje ve 4 durak", rota Hat_2 ✔ (Hat 2 sahne dosyası 152 bin satır büyüdü: parçalar).
- Anons klipleri: 18 durağın hepsinin `sonraki_*` klibi var ✔.

## 2. Güzergâh boyunca geometri (Editor, `AnalizBatch`)

Güzergâh 2 m'de bir örneklendi (5 506 nokta); her noktada şeridin 1,5 m üstünden aşağı ışın (yol yüzeyi + malzeme), yüzeyden yukarı
üç ışın (orta, ±1,25 m: otobüs genişliği), 20 m eğim, 2 m'de ani yükseklik farkı. (Not: `RaycastAll` çarpıştırıcı başına tek isabet
verdiği için köprü tabliyesi ile altındaki yol — ikisi aynı `…_Yol` ağında — yukarıdan tek ışınla ayırt edilemiyor; ölçüm alçaktan yapıldı.)

### 2.1 Köprüler / üst geçitler (otobüs üstünden geçen yapılar)

| s (m) | Yer | Orta / yan (±1,25) açıklık | Durum |
|---|---|---|---|
| 526–648 | **Sıhhiye köprüsü** (56, −9.6, 502), Sıhhiye+270 m | **2,21 / 1,38 m** | ✘ Alt geçide girişte, otobüs yolunun üstüne başka bir yolun rampası/tabliyesi iniyor; yol 6 m'de 3 m alçalıyor (basamak). `editor/kopru_00_alti`, `_yaklasma`. Tabliyenin altı (s 560–640) 6,2–6,6 m ✔ |
| 845–848 | Sıhhiye üst geçidi | 5,09 m | ✔ |
| 1240–1248 | **Opera, Talatpaşa köprüsü girişi** (−84, −6.8, 1174), Opera+289 m | **0,38 / 0,17 m** | ✘ Köprünün beton rampa duvarı şeridin üstünde: otobüs duvara girer. `editor/kopru_02_alti`, `_yaklasma` |
| 1263–1286 | Talatpaşa köprüsü altı | 4,70 m | ✔ |
| 4060–4063 | Veteriner Fak. üst geçidi | 4,88 m | ✔ |
| 4371–4392 | **Aydınlıkevler, Turgut Özal 2 Blv.** (927, −2.9, 3960) | 4,24 / 4,23 m | ✔ (körüklü 3,3 m) ama yol yüzeyi kırık (2.2) |
| 4394–4416 | **Aydınlıkevler ikinci tabliye** (939, −2.5, 3981), Veteriner+304 m | **2,64 / 2,47 m** | ✘ Tabliye alçak ve eğik; yol yüzeyi altında kırık. `editor/kopru_06_alti`, `_yaklasma` |
| 5045–5050 | AİHL üst geçidi | 4,79 m | ✔ |
| 5511–5514 | Altınpark üst geçidi | 7,74 m | ✔ |

Planın "en dar açıklık 5,2 m" hesabı üç yerde tutmuyor (Sıhhiye girişi, Opera rampası, Aydınlıkevler). Dışkapı'da güzergâhın köprü
üstünden geçtiği doğrulanamadı: Dışkapı çevresinde (s 3 300–3 700) otobüsün üstünden geçen yapı yok, güzergâh altında ayrı bir tabliye
de görünmüyor (ölçüm yalnız üstten geçenleri buluyor). Telefonda körüklüyle geçiş: 3.4.

### 2.2 Güzergâh asfaltın dışında (şerit ↔ yol ağı uyuşmazlığı)
Güzergâhın altındaki yüzey: asfalt 4 730, **refüj 315, kaldırım 286, yol yok (arazi) 161** örnek (2 m). 142 kesit; tam liste
`editor_analizi.txt` ("güzergâh kesitleri"). Hepsinde şerit (yani `RotaRehberi`'nin sarı çizgisi ve trafik araçları) asfaltın **sağında**
kalıyor ("en yakın asfalt N m solda"). Öbekler:

| Yer | s | Ne |
|---|---|---|
| Ulus Meydanı / Zafer Anıtı | 1 913–2 246 | refüj ve kaldırım üstünde 9 kesit, asfalt 2–7 m solda; meydanın dönüşü anıt platformunun kaldırımından geçiyor (`editor/kesit_16`, `_surucu`) |
| Ulus → Yıba (Çankırı Cd.) | 2 388–2 668 | kaldırım/refüj, asfalt 2,5–6 m solda |
| Opera | 1 621–1 655 | kavşakta kaldırım ve 5 m **delik** (hiçbir yüzey yok, (−10, −7.4, 1546)) — refüj adasında üçgen boşluk (`editor/kesit_10`) |
| Subayevleri → Güneşevler (Malazgirt Cd., 126. Sk., Çiçek Taksi, M. Akif Ersoy) | 6 166–9 111 | **düzenli desen**: her kavşak/dönüşte 10–15 m "yol yok (arazi)" + iki yanda refüj, asfalt 3–8 m solda. 20'den fazla dönüş. Şerit yolun dışından, binaların önündeki çim/kaldırımdan geçiyor (`editor/kesit_64`, `_surucu`, `kesit_98_surucu`) |
| Güneşevler son 500 m | 8 539–9 040 | kaldırım üstünde 10 kesit, asfalt 1–2,5 m solda (şerit yolun sağ kenarında) |

Otobüsü oyuncu sürdüğü için asfaltta kalınabilir; ama mini haritadaki güzergâh ve trafik araçları kaldırıma çıkıyor, durak dönüşlerinde
"rotadan çıktın" uyarısı erken gelebilir.

### 2.3 Yol yüzeyi: delik, arazi, basamak
- **Delik** (yol da arazi de yok): Opera+91 m (−10, −7.4, 1546) 5 m; Veteriner+334 m (963, −4.1, 4001) 2 m.
- **Arazi yolun üstünde**: 14 örnek, en çok 0,20 m (Sıhhiye+140 m (35, −12.6, 630) 0,10–0,17 m; Aydınlıkevler (928, −3.1, 3964) 0,17–0,20 m).
- **Basamak** (2 m'de > 0,25 m): Sıhhiye girişi (s 528–534, −8,15 → −11,01: alt geçit rampası 6 m'de 3 m), Aydınlıkevler (s 4 372–4 426,
  0,3–0,4 m), diğerleri (0,3–0,5 m) yukarıdaki "yol yok" kesitlerinin kenarlarında (asfalt ↔ çim/kaldırım geçişi).
- **Parça sınırında çatlak**: güzergâhta 250 m'lik parça sınırlarına denk gelen delik yok (yukarıdakiler parça içinde).

### 2.4 Mehmet Akif Ersoy yokuşu
En dik 20 m: **%9,2** s≈8 977 (Güneşevler+141 m); %7 üstü 78 örnek (≈160 m), %10 üstü yok ✔ (plan %7–9). Dolu otobüsle çıkış: 4.5.

### 2.5 Duraklar ve sundurmalar (Editor)
- 18 durak noktasının hepsi **asfaltta** (sağ şerit, yol yüzeyinin 2–9 cm üstünde; Çeyizciler 42 cm üstünde) ✔.
- Sundurmalar durak noktasının 4,7–4,8 m sağında ✔. Kızılay → Hasköy (12 durak) ve Güneşevler: **kaldırımda** (ayaklar 0,24–0,40 m
  kaldırıma gömülü). **Pazar Yeri, Esenevler A.L., Çeyizciler, Çiçek Taksi, Güneşevler Spor Tes.: çim/arazide**, Pazar Yeri ve
  Çiçek Taksi'de taban zeminden **0,30–0,31 m havada** (`editor/durak_12_ust`, `durak_15_ust`); Çağdaş Sk., AİHL, Hasköy refüjde.

### 2.6 Simge yapılar ve diğer
- Zafer Anıtı (13, 7.8, 2039) Ulus Meydanı'nda ✔; Emek İşhanı (10, −361), Kızılay AVM (−118, −213), Güvenlik Anıtı (−104, −368)
  Kızılay'da, başlangıçta otobüsün **arkasında** ✔; Hitit Güneş Kursu Sıhhiye'de (50, 369) ✔; Opera Binası (−99, 1324) ✔.
- **Su yüzeyleri −33 616 m'de**: **32 parçanın** `SimgeYapilar` ağında su (`M_H3_Su`) ve kenar (`M_H3_Bordur`) köşeleri y ≈ −33 616 /
  −33 637 m (DEM "veri yok" değeri gibi): H3_2_27, 3_21, 3_22, 3_25, 3_27, 4_22–4_25, 5_22–5_24, 6_22, 6_23, 7_22, 7_23, 8_23, 9_23,
  10_23–10_26, 11_26, 12_25, 12_26, 13_25, 13_26, 14_26–14_28, 15_27, 15_28 (Altınpark kuzeyi → Güneşevler kuzeyi; z > 5 300).
  Bu havuz/göletler görünmüyor; ayrıca parçaların sınır kutusu 33 km boyunda (gölge ve görünürlük ayıklaması bozulur).
  Liste ve köşe sayıları `editor_analizi.txt` "simge yapılar".
- Yolda engel (bina/simge yapı çarpışması şerit üstünde): **yok** ✔. Üst geçit merdiven kuleleri yolda değil ✔ (açıklıklar 4,8–7,7 m, orta = yan).


## 3. Telefonda (S24 FE, Hat 3, `h3` görevi)

Test sürücüsü (`H3Testi.Sur`) güzergâhı (şeritten hesaplanan `RotaRehberi` yolu) izler; oyunda otomatik pilot yok. İlk turda ışınlama
hatası vardı (uzak parçalar ParcaAkisi'yle kapalı olduğu için zemin bulunamıyor, otobüs yerinde kalıyordu); düzeltildi
(`H3Testi.Yerlestir`: önce taşı, parçaları aç, sonra zemine oturt). Köprü ve trafik adımları düzeltmeden sonra yeniden koşuldu.
Wi-Fi adb her 2–3 dakikada koptuğu için görüntüler parça parça çekildi.

### 3.1 Duraklar (Yüksek, gündüz, BMC)
| | Sonuç |
|---|---|
| Durak sayılıyor mu | **17/18** ✔ (kapılar açılınca 10–25 sn'de). **Atatürk A. İmam Hatip Lisesi (08)**: otobüs durak alanında değil, sayılmadı (`s24/h3_Yuksek_Gunduz_durak08_hava`: sundurma otobüsün içinde). O tur ışınlama düzeltmesinden önceydi; durak noktası AİHL üst geçidine 30 m, güzergâh orada refüjde (2.2). Tekrar ölçülemedi. |
| Biniş / iniş | Her durakta bekleyenler bindi (2–8), Hasköy'den sonra inenler var; son durakta 33 → 0 ✔ (`durak09_kapi`) |
| Anons | **16/17** kalkışta doğru durak anonsu (`sonraki_sihhiye` … `sonraki_son_gunesevler`) ✔; AİHL'de kalkış olmadığı için çalmadı |
| Trafik (otobüsün 150 m çevresi) | 3–17 araç, toplam 24; son durakta 14 → **9 km'de kaybolmuyor** ✔ |
| Sundurma | Kaldırımda/çim şeritte, otobüsün sağında ✔ (`durak03_hava`); Güneşevler son durakta otobüs kaldırıma açılı (`durak17_ekran`) |
| HUD / LED / mini harita | Mini harita hazır doku + sarı güzergâh ✔, "Sıradaki durak", "Hat tamamlandı" ✔. LED ön tabela kayan "408 GÜNEŞEVLER", arka "408" ✔. **HUD'da hat adı kesik: "408 Kızılay – Güneşevle"** (`durak00_ekran`) |

### 3.2 Trafik (40 sn, otobüs duruyor)
| Yer | Çevrede araç | Kırmızıda duran (örnek) | Kırmızıda çizgiyi geçen |
|---|---|---|---|
| Kızılay | 9 | 118 | 2 (sarıda duramayan) |
| Işık K09 | 11 | 181 | 0 |
| Işık K10 | 13 | 223 | 0 |

Araçlar kırmızıda duruyor ✔ (`s24/h3_Yuksek_Gunduz_trafik_kizilay_2`, `_isik_Isik_K09_2`). Çapraz yollardan geçen ve T kavşakta katılan
araçlar sayısal ölçülmedi; görüntülerde çapraz yolda araç var. Şeritler 2.2'deki gibi asfaltın sağına kaydığı için araçlar dönüşlerde
kaldırımdan geçiyor.

### 3.3 Yokuş (Mehmet Akif Ersoy, kütle +5,6 t = 80 yolcu, tam gaz)
s 8 700 → 9 080, 19,8 m yükselme, 36 sn, **en düşük 27 km/s, çıktı** ✔ (`s24/h3_Yuksek_Gunduz_yokus_2`). Not: oyunda yolcu kütlesi yok
(`BusPassengers` kütleye eklemiyor); test kütleyi elle artırdı.

### 3.4 Köprü ve üst geçitler (BMC 18 körüklü, 20 km/s, 100 m)
| Yer | Sonuç |
|---|---|
| Sıhhiye üst geçidi, Veteriner üst geçidi, Altınpark | **geçti** ✔ (`kopru_sihhiye_ustgecit`, `kopru_altinpark_kokpit`) |
| **Sıhhiye köprüsü** (s 500 → 515) | ✘ **takıldı**, hız 0, gövde teması yok (alt geçit girişindeki 6 m'de 3 m'lik basamak/rampa, 2.1, `editor/kopru_00_alti`) |
| **Opera / Talatpaşa** (s 1 205 → 1 232) | ✘ **takıldı**, köprü rampası duvarı (açıklık 0,38 m, `editor/kopru_02_alti`) |
| **Aydınlıkevler 1** (s 4 320) | ✘ 6 m'de bir trafik aracına çarptı; tekrar edilmedi |
| **Aydınlıkevler 2** (s 4 345 → 4 359) | ✘ **takıldı** (alçak, eğik tabliye ve kırık yüzey, `editor/kopru_06_alti`) |
| **AİHL üst geçidi** (s 4 987 → 5 038) | ✘ üst geçit yapısına çarptı (`H3_5_17_SimgeYapilar`): şerit refüjde, otobüs ayağa giriyor (`s24/h3_Yuksek_Gunduz_kopru_aihl_ustgecit`). Asfalttan giden oyuncu için açıklık 4,79 m yeterli |

Takılan üç yerde telefondan görüntü yok (otobüs çekim noktasına varmadan durdu); editör görüntüleri geçerli.

### 3.5 Harita kenarı / uzak şehir (havadan, 45 m; gündüz, akşam, yağmur)
- Akşam ve yağmurda uzak şehir halkası (Ufuk, 61 parça) ve tepeler sise karışıyor, Kızılay'dan Ulus yönünde harita sonu boşluğu
  görünmüyor ✔ (`s24/h3_Yuksek_Aksam_ufuk_kizilay_yukari`, `_ulus_yukari`, `h3_Yuksek_GunduzYagmur_ufuk_kizilay_yukari`).
- ✘ **Kızılay'dan kuzeye (Ulus) bakışta, yağmurda sis çizgisinin üstünde havada koyu, yassı bir şekil** (sise karışmıyor) ve solda
  uzak halkanın **düz yatay kenarı** görünüyor (`s24/ufuk_yagmur_kizilay_yakin`). Hangi nesne olduğu belirlenmedi.
- Güneşevler'de havadan: bina seyrek, bloklar arası geniş kaldırım dokulu düzlük (`h3_Yuksek_Aksam_ufuk_gunesevler_yukari`).
- ✘ **Göz hizası ufuk görüntüleri alınamadı**: test kamerası otobüsün içinde kaldı (test aracı hatası). Gündüz göz hizası için durak
  görüntüleri: ufukta boşluk yok (`durak00_ekran`, `durak03_ekran`). Gündüz havadan ufuk görüntüleri ışınlama hatası yüzünden yanlış yerde.
- ParcaAkisi açılıp kapanırken belirme ölçülmedi (sürüş görüntüsü yok).

### 3.6 Performans (S24 FE, gündüz, durakta trafik ve yolcularla; dış / kokpit 12'şer sn)
| Kalite | Yer | FPS (1% düşük) | GPU ms | CPU ms | Çizim | Üçgen | Bellek MB |
|---|---|---|---|---|---|---|---|
| Yüksek (900 m) | Kızılay | 60,0 (59,5) / 60,0 | 10,3 / 10,4 | 9,7 | 659 / 599 | 877 / 768 bin | 669 |
| Yüksek | Ulus | 60,0 / 60,0 | 10,2 / 9,8 | 9,7 | 537 / 488 | 769 / 617 bin | 696 |
| Yüksek | Dışkapı | 60,0 / 60,0 | 10,4 / 10,0 | 9,7 | 508 / 421 | 734 / 544 bin | 701 |
| Yüksek | Altınpark | 60,0 / 60,0 | 9,7 / 10,2 | 10,0 | 500 / 427 | 831 / 591 bin | 712 |
| Yüksek | Güneşevler | 60,0 / 60,0 (58,3) | 9,5 / 8,6 | 9,0 | 297 / 221 | 572 / 366 bin | 714 |
| Normal (650 m) | Kızılay | 60,0 / 60,0 | 5,4 / 5,5 | 8,0 | 524 / 418 | 663 / 547 bin | 545 |
| Normal | Ulus | 60,0 / 60,0 | 4,8 / 5,6 | 8,5 | 394 / 381 | 498 / 423 bin | 551 |
| Normal | Dışkapı | 60,0 / 60,0 | 4,5 / 5,2 | 8,6 | 229 / 328 | 310 / 344 bin | 551 |
| Normal | Altınpark | 59,8 / 60,0 | 5,0–5,3 / 6,0 | 4,8–7,6 | 300 / 282 | 405–435 / 372 bin | 547 |
| Normal | Güneşevler | 60,0 | 4,0 | 8,1 | 188 | 261 bin | 547 |

**S24 kabul (60 FPS) ✔** her yerde. Kızılay Yüksek'te 659 çizim, 877 bin üçgen en yüksek değer (Hat 1'in iki katı); bellek Yüksek'te
670–714 MB. `ParcaAkisi.Yaricap` ve LOD eşikleri değiştirilmedi. **A32 ölçülmedi** (bağlı değildi).

### 3.7 Hattı baştan sona sürme
Yapılmadı: oyunda otomatik pilot yok ve otopilotla sürüş testi kullanıcı kararıyla koşulmuyor. Duraklar tek tek (3.1), köprüler (3.4) ve
yokuş (3.3) test sürücüsüyle parça parça denendi; tam sürüş (süre, puan) elle sürülerek yapılmalı. Takılınacak yerler: Sıhhiye ve
Opera köprü girişleri, Aydınlıkevler.

## 4. Menü ve APK
- **Menü (S24, 2340×1080):** üç hat kartı (1, 2, 408) ekrana sığıyor, alt bilgiler okunuyor ✔ (`s24/menu_uc_kart`). 408 kartı seçilip
  "Sefere Başla" → Hat 3 sahnesi açıldı, Kızılay durağında ✔ (`s24/menu_408_sahne`). 1920×1080 ekranda denenmedi (telefon 1080 yükseklik).
- APK'lar hatasız: test 1,37 GB, **oyun 1,28 GB** (Hat 2/3 parçalarıyla; önceki ~0,3 GB'tan büyük). Oyun APK'sı (üç hat) S24'e kuruldu,
  `~/Desktop/AnkaraOtobus.apk`'ye kopyalandı; telefonda test APK'sı kalmadı. Testler ayarları değiştirdiği için menüde hava "Açık"a geri alındı.

## 5. Bulut için özet (öncelik sırasıyla)
1. **Köprü girişleri**: Opera/Talatpaşa rampası şeridi kesiyor (0,38 m), Sıhhiye alt geçit girişi (2,2 m açıklık, 6 m'de 3 m basamak),
   Aydınlıkevler ikinci tabliye (2,6 m, kırık yüzey). Körüklü üçünde de takılıyor (2.1, 3.4).
2. **Şeritler asfaltın dışında** (142 kesit, özellikle Ulus Meydanı ve Hasköy–Güneşevler dönüşleri; AİHL'de refüjdeki üst geçit ayağı):
   mini harita çizgisi, trafik ve durak yanaşması etkileniyor (2.2, 3.1, 3.4).
3. **Su yüzeyleri −33 616 m** (32 parça, 2.6).
4. Ufukta Ulus yönünde havada koyu şekil ve uzak halkanın düz kenarı (3.5).
5. Malzemeler FBX'ten önce kurulmalı (1); `preserveHierarchy` yerelde eklendi, bulut korumalı (1).
6. Küçükler: HUD'da hat adı kesik ("Güneşevle"), Pazar Yeri/Çiçek Taksi sundurmaları 0,3 m havada, Opera'da 5 m delik (2.3, 2.5, 3.1).

## 6. Test araçları (yeni)
- `Editor/H3Kurulum.cs`: `AnalizBatch` (hiyerarşi, malzeme, güzergâh boyunca yüzey/açıklık/eğim/kesitler, duraklar, simge yapılar,
  görüntüler `Builds/H3/editor`), `NoktaBatch` (`H3_NOKTA="x,z;…"`: noktadaki ışınlar ve üçgenler), `YenidenAktarBatch`, `Hat2Batch`.
- `Diagnostics/H3Testi.cs` (`-e gorev h3 -e sahne Hat3_KizilayGunesevler -e adim durak|perf|ufuk|trafik|kopru|yokus|hepsi`);
  `Y10Baslatici`'ya `sahne` ve `adim` intent'leri. Bilinen kusur: `ufuk` adımının göz hizası kamerası otobüsün içinde (düzeltilecek).
