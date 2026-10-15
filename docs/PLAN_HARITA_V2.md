# Harita v2 — onarım, Kızılay–Atakule (408) ve Hat 2'nin kaldırılması

Kullanıcı isteği (15 Ekim 2026):
- Haritada hâlâ kesik ve bozuk yerler var (Sıhhiye köprüsünün çıkış yolu gibi), bazı yollar kullanılamaz. **Haritadaki bütün
  yollar** onarılacak.
- Kızılay–Ulus hattı 408 ile aynı yoldan gidiyor → **Hat 2 tamamen silinecek** (kart, sahne, harita, kod).
- Kızılay–Güneşevler için yapılan gerçek ve ayrıntılı harita **Kızılay–Atakule** için de yapılacak; hat gerçek hayattaki gibi olacak.
- **Bütün harita tasarımı bitmeden telefonda test yapılmayacak.** Her aşamanın kabulü Editor'deki otomatik denetim ve görüntülerle.

Kararlar (kullanıcı, 15 Ekim):
1. Kızılay–Atakule menüde **"408 Kızılay – Atakule"** (gerçek numara). Menüde iki 408 kartı: Atakule yönü ve Güneşevler yönü.
   LED tabela "408 ATAKULE".
2. **Tek birleşik harita.** 408 haritası güneye, Atakule'ye kadar büyütülür; iki hat aynı parçaları kullanır, Kızılay ikisinde
   birebir aynı. Sahneler ayrı (`Hat1_KizilayAtakule`, `Hat3_KizilayGunesevler`); ParcaAkisi her sahnede yalnız yakın parçaları açar.
3. Hat 2 **tamamen silinir**: menü kartı, sahne, `Maps/Hat2`, `Hat2SahneKurucu`, Hat 2 bake menüleri, `harita_hat2.py`, anons ve
   test kodundaki Hat 2 dalları.
4. Onarım kapsamı **haritadaki bütün yollar**: güzergâh dışındaki yan yollar dahil her araç yolu sürülebilir olacak (delik, basamak,
   alçak köprü, kesik yol kalmayacak). Kabul, bütün yol ağını tarayan otomatik denetimle.

## Gerçek hat: 408 Kızılay → Atakule

EGO 408 (Gülpınar–Güneşevler–Kızılay–Atakule–Birlik; `ego.gov.tr/hareketsaatleri?hat_no=408`) gidiş yönü, sıra 30–38. Durak konumları
OSM `bus_stop` + EGO numarasıyla eşleşti (15 Ekim):

| # | Durak (EGO no) | Konum (enlem, boylam) | Yol |
|---|---|---|---|
| 1 | Kızılay (11616) | 39.92431, 32.85451 | Atatürk Bulvarı (güney yönü) |
| 2 | Güvenpark (12206) | 39.91959, 32.85383 | Atatürk Bulvarı |
| 3 | Meclis (12217) | 39.91311, 32.85419 | Atatürk Bulvarı (TBMM önü) |
| 4 | Kuğulu Park (12251) | 39.90131, 32.85926 | Atatürk Bulvarı |
| 5 | Farabi (12264) | 39.89659, 32.85774 | Cinnah Caddesi başı / Farabi Sk. |
| 6 | Kırkpınar Sk. (14360) | 39.89070, 32.85550 | Cinnah Caddesi |
| 7 | Pilot Sk. (12177) | 39.88891, 32.85499 | Cinnah Caddesi |
| 8 | Botanik Parkı (12176) | 39.88684, 32.85491 | Cinnah Caddesi |
| 9 | Atakule (12178) — son durak | 39.88473, 32.85557 | Simon Bolivar Caddesi |

Güzergâh: Atatürk Bulvarı güneye (Güvenpark, Meclis, Kuğulu Park) → Cinnah Caddesi yokuşu (Kavaklıdere → Çankaya) → Simon Bolivar Cd.,
yaklaşık 4,6 km, rakım ~870 → ~990 m. OSM'de 408 hat ilişkisi yok; güzergâh bulutun `hat408_guzergah.py` yöntemiyle (durakları yönlü
yol ağında bağlama) çıkarılır ve EGO hat haritasıyla elle karşılaştırılır.

Not: Kızılay'da iki yönün durakları ayrı: Güneşevler yönü 11621 (bugünkü kök, (0,0,0)), Atakule yönü 11616 (yolun karşı tarafı).
Yerel koordinat kökü değişmez; 408 Güneşevler'in bütün konumları aynı kalır.

## Bugünkü hataların kaynağı (RAPOR_H3)
- **Köprüler:** `arazi.py` kuralı "oyuncu güzergâhı her yerde zeminde kalır; güzergâhı kesen yol katman başına 6,5 m kaldırılır/indirilir",
  rampalar yol ağı Laplace yumuşatmasıyla bağlanıyor. Kaldırılan yolun rampası güzergâhın açıklık hacmine giriyor: Opera'da Talatpaşa
  rampası şeritte 0,38 m, Sıhhiye alt geçit girişi 2,2 m (+6 m'de 3 m basamak), Aydınlıkevler 2,6 m. Sıhhiye köprüsünün çıkış yolu da
  aynı nedenle kırık.
- **Şeritler asfaltın dışında:** trafik şeritleri OSM yol ekseninden sabit kaydırmayla üretiliyor, yol çokgeni (genişlik, kavşak
  köşesi) ise ayrı hesaplanıyor; dönüşlerde ve ayrık yollarda şerit 1–8 m kaldırıma/çime kayıyor (142 kesit).
- **Kesik yollar:** OSM yollarının harita/koridor sınırında kesilmesi ve kavşak çokgen birleşimindeki boşluklar (Opera'da 5 m delik).
- **Su −33 616 m:** DEM "veri yok" değeri, su çokgenleri için yükseklik örneklenirken süzülmüyor.
- **Uzak halka:** Ulus yönünde sisin üstünde havada koyu şekil, halkanın düz kenarı.
- **Unity kurulumu:** malzemeler FBX'ten sonra oluşuyor (dokusuz harita), tek çocuklu FBX'ler siliniyordu (yerelde `preserveHierarchy`
  ile düzeltildi).

## Aşamalar

Her aşama: üretim → **otomatik denetim (sıfır hata)** → Editor görüntüleri → commit. Telefona yalnız M9'da geçilir.
Üretim bu makinede: Blender 5.2 (FBX dışa aktarma) + Python ortamı (numpy, scipy, shapely, mapbox_earcut, pillow).
Bulut oturumu bu süre içinde haritaya dokunmaz (çakışma olmasın); GOREVLER'e not düşülür.

### M0 — Hazırlık ve yeniden üretilebilirlik
- Yerel Python ortamı + Blender'la `hat3_uret.sh`'yi bugünkü veriden çalıştır; çıktıyı repodaki FBX'lerle karşılaştır (parça sayısı,
  üçgen sayısı, sınır kutuları). Aynı değilse farkı bul; yerel üretim buluttakiyle aynı olmadan ilerleme.
- Unity içe aktarma kalıcı düzeltmesi: `Hat3Malzemeler` malzemeleri içe aktarmadan önce kurar (`OnPreprocessModel`) ya da `.mat`
  dosyaları repoda; `preserveHierarchy` kalır. Temiz klonda tek menüyle kurulum.

### M1 — Denetim aracı (önce ölç)
Bugünkü haritada bütün sorunları listeleyen, sonraki her aşamada koşulacak denetim. İki katman:
- **Python (FBX'ten önce):** yol çokgenleri ve yükseklikler üzerinde.
- **Unity Editor (`H3Kurulum.AnalizBatch` genişletilir):** sahnedeki gerçek çarpışma ağları üzerinde.

Ölçütler (hepsi **bütün araç yolları** için, yalnız güzergâh değil):
| Denetim | Kabul |
|---|---|
| Yol yüzeyinde delik / boşluk (2 m ızgara, yolun her şeridi) | 0 |
| Basamak: komşu noktalar arası yükseklik farkı | ≤ 5 cm (bordür hariç) |
| Eğim | ≤ %12 (güzergâhlar ≤ %10) |
| Üst açıklık: her yolun üstünden geçen yapı, yol genişliği boyunca | ≥ 5,0 m (körüklü 3,3 m + pay) |
| Yolu kesen engel (bina, üst geçit ayağı, rampa duvarı, direk) | 0 |
| Şerit asfaltta: her trafik şeridinin ekseni asfaltın içinde, kenara ≥ 1,3 m | 100 % |
| Şerit bağlantısı: şerit uçları kavşakta bağlı, çıkmaz yol yok (harita sınırı hariç) | 0 kopuk |
| Yolun harita sınırında bitişi: bina/viraj arkasında, ağızda görünmez duvar | her uç |
| Köşe yüksekliği: −200 m altı / +400 m üstü köşe | 0 |
| Havada / gömülü nesne (bina tabanı, sundurma, lamba, ağaç, durak) | ±10 cm |
| Durak: nokta sağ şeritte, sundurma kaldırımda, aradaki mesafe | her durak |

Çıktı: `docs/gorseller/harita_v2/denetim_<aşama>.txt` + her sorunun konumu ve görüntüsü. **M1'in kendisi bir sorun listesi üretir;
M2–M5 bu listeyi sıfırlar.**

### M2 — Veri: haritayı Atakule'ye büyütme
- OSM: mevcut alan (39.915–39.985 K) güneye **39.875 K**'ye kadar (Atakule, Çankaya, Kavaklıdere, Cinnah), 0,01° karolarla; aynı
  biçimde `osm_ankara.json.gz` (bugünkü `osm_hat408.json.gz`'nin yerine, ikisini kapsar).
- DEM: güney karoları (terrarium z15).
- Güzergâh dosyaları: `hat408.json` → `hatlar.json`: `408_gunesevler` (bugünkü) ve `408_atakule` (yukarıdaki 9 durak + yönlü
  güzergâh). Üretim betikleri tek hatta bağlı değil, hat listesinde dolaşır (`hat3/` paketi `harita/` olarak genelleşir).
- Koridor: iki güzergâhın birleşimi; bina/ağaç/yol yarıçapları aynı (60/120/250/450 m).

### M3 — Köprüler ve katmanlar (yeniden)
- "Güzergâh hep zeminde" kuralı kalkar. Her köprü/alt geçit OSM'deki gerçek düzenine göre (`bridge`, `tunnel`, `layer`) ve gerçek
  referansla kurulur: **Sıhhiye** (Atatürk Bulvarı demiryolu ve Celal Bayar Bulvarı köprülerinin altından; köprünün çıkış/bağlantı
  yolları), **Opera** (Talatpaşa Bulvarı üstten), **Dışkapı**, **Aydınlıkevler** (Turgut Özal 2 Blv.), güney kesimdeki köprü ve alt
  geçitler (Kızılay–Atakule koridorunda varsa).
- Köprü nesnesi: tabliye + kenar duvarı + ayaklar + rampa; rampa **alt yolun açıklık hacmine giremez** (alt yol çokgeni + 1 m pay,
  5,0 m yüksekliğe kadar yasak bölge). Rampa uzunluğu eğimden (≤ %8) hesaplanır; sığmıyorsa üst yol tabliye seviyesinde devam eder.
- Rampa ile zemindeki yol arasında basamak/çatlak yok: rampa ucu yol ağına aynı düğümle bağlanır.
- Kabul: M1 açıklık, basamak, engel ölçütleri her köprüde sıfır; her köprüye yaklaşma, altı ve üstünden görüntü.

### M4 — Yol ağı: kesik yollar, kavşaklar, harita sınırı
- Kavşak çokgenleri bütün kollar birleştirilerek (shapely union) üretilir, köşe yarıçapları 6–12 m; delik kontrolü çokgen düzeyinde.
- Koridor sınırında kesilen yollar: uç ya bir binanın arkasında/virajda biter ya da yola kapatma (bariyer + görünmez duvar) konur;
  asfalt havada/yarım bitmez.
- Kaldırım, refüj, bordür, şerit çizgileri, yaya geçitleri yeni çokgenlerden; Kızılay–Atakule için Atatürk Bulvarı (Güvenpark önü
  geniş refüj, TBMM önü), Cinnah Caddesi (dar, iki yönlü, dik, park eden araçlar), Simon Bolivar.
- Kabul: M1 delik/basamak/kesik ölçütleri bütün ağda sıfır.

### M5 — Şeritler, durak yerleri, trafik
- Şeritler yol çokgeninin kendisinden: yol ekseni = çokgenin orta hattı, şeritler gerçek şerit sayısına göre (OSM `lanes`, yoksa
  sınıftan) eşit bölünür; dönüş yayları kavşak köşe yarıçapını izler. Ayrık yollarda her yön kendi çokgeninden.
- Durak noktaları sağ şeritte, sundurma kaldırımda (durak başına denetim). 9 + 18 durak.
- Trafik ışıkları (OSM `traffic_signals`, iki koridor), faz grupları; trafik şeritleri bütün ana yollarda.
- Kabul: M1 "şerit asfaltta" %100, kopuk şerit 0; `RotaRehberi` iki güzergâhı da baştan sona buluyor (Editor).

### M6 — Kızılay–Atakule kesimi: binalar, simge yapılar, donatı
Kaynak: OSM ayak izleri + kat bilgisi, Wikimedia Commons fotoğrafları (yalnız referans), bölge kuralları.
- **Binalar:** Kızılay–Güvenpark (6–10 kat ofis/çarşı), Bakanlıklar (Atatürk Bulvarı batısı, kamu binaları), Kavaklıdere ve Cinnah
  (4–8 kat apartman, zemin kat dükkân/kafe, büyükelçilikler), Çankaya sırtı (alçak villa/elçilik konutları). Cephe atlasına
  Kavaklıdere apartmanı, elçilik/kamu binası, Cinnah dükkân sırası eklenir.
- **Simge yapılar** (gerçek konumlarına): Güvenpark ve Güven Anıtı (mevcut model konumu doğrulanır), **TBMM** yerleşkesi (mevcut
  duvar/kapı modelleri gerçek ayak izine; ana bina silueti), Bakanlıklar binaları, **Kuğulu Park** (gölet + kuğular; mevcut model),
  **Atakule** (mevcut model, gerçek konum ve yükseklik 125 m), **Botanik Parkı**, Cinnah üzerindeki büyükelçilik duvarları, camiler.
- **Donatı:** Atatürk Bulvarı lambaları, Cinnah refüjsüz sokak lambaları, ağaç dizileri (Kuğulu Park, Botanik Parkı, Cinnah),
  9 durak sundurması (EGO tipi), ışıklar.
- Kabul: M1 havada/gömülü sıfır; her durakta sürücü gözü ve havadan görüntü, simge yapıların referans fotoğrafla yan yana karşılaştırması.

### M7 — Su, uzak şehir halkası, ufuk
- DEM "veri yok" değerleri komşu ortalamasıyla doldurulur; su çokgenleri yerel zemin yüksekliğine (−33 616 m hatası).
- Uzak halka (1 km parçalar) iki koridoru da çevreler: Çankaya tepeleri, Kale, Keçiören sırtları; halka kenarı tepe arkasında ya da
  sise karışan mesafede biter (düz kenar görünmez).
- Ulus yönündeki havada koyu şeklin kaynağı bulunur ve kaldırılır.
- Kabul: Editor'de sis/hava ayarları uygulanmış (HavaVeZaman) görüntülerde gündüz/akşam/gece/yağmur/kar, 10 noktadan dört yön —
  harita sonu, boşluk, havada nesne yok.

### M8 — Unity entegrasyonu ve Hat 2'nin silinmesi
- Birleşik harita klasörü (`Maps/Ankara/…`), tek kurulum menüsü; `HaritaKurucu` iki hattın sahnesini aynı parçalarla kurar.
- **Hat 1 sahnesi yeniden:** 408 Atakule rotası, 9 durak, başlangıç Kızılay (11616), mini harita (iki koridoru kapsayan doku),
  LED "408 ATAKULE", anonslar (Piper TTS: Güvenpark, Meclis, Kuğulu Park, Farabi, Kırkpınar Sokağı, Pilot Sokağı, Botanik Parkı,
  Atakule), puan hedef süreleri. Eski stilize Hat 1 haritası (`Maps/Hat1`, `harita_*.py` Hat 1 kısmı) silinir.
- Menü: iki kart — "408 Kızılay – Atakule", "408 Kızılay – Güneşevler"; en iyi puanlar hat kimliğiyle ayrılır.
- **Hat 2 silinir** (kart, sahne, `Maps/Hat2`, `Hat2SahneKurucu`, bake/test dalları, `harita_hat2.py`, anonslar, Build Settings).
- HUD'da hat adı kesilmesi düzeltilir ("Güneşevle").
- Kabul: derleme hatası yok; iki sahne kurulumu uyarısız; M1 denetimi Unity'de iki sahnede sıfır; APK boyutu ölçülür.

### M9 — Son denetim ve telefon testi (en son)
- M1 denetiminin tamamı, bütün görüntü setleri, Editor'de performans ön ölçümü (görünen üçgen/çizim tahmini).
- Bu aşamada ilk kez telefon: iki hat baştan sona elle sürüş (kullanıcı) + `h3` test görevi (duraklar, anons, trafik, köprü, yokuş, FPS)
  iki hatta. Kabul: S24 60 FPS; A32 yalnız ölçülür.

## Riskler
- OSM'de kat bilgisi az (%6): Çankaya/Kavaklıdere'de yükseklikler bölge kuralıyla; görüntüyle elle düzeltme listesi.
- Cinnah Caddesi dar ve dik (yaklaşık %8–10): körüklü otobüsün dönüşleri ve yokuşu M5/M9'da.
- APK boyutu (bugün 1,28 GB) büyür; Hat 2 ve eski Hat 1 haritasının silinmesi bir kısmını karşılar. Gerekirse doku sıkıştırma (görünümü
  değiştirmeden) M8'de ölçülür.
- Üretim süresi: tam üretim + FBX dışa aktarma bu makinede ölçülecek (M0).

## Durum

### M0 ✔ (15 Ekim, yerel)
- Yerel üretim: `tools/harita/uret_yerel.sh` (Blender 5.2.2 + `tools/harita/.venv`; kurulum adımları betiğin başında). Tam üretim
  bu makinede **~1 dk** (arazi 9, yollar 9, yüzeyler 11, dokular 1, simgeler 2, binalar 6, ağaçlar 3, ufuk 7, yerleşim 7, FBX 10 sn).
- Bulut çıktısıyla karşılaştırma: `Hat3_Yerlesim.json` ve `Hat2_Yerlesim.json` **birebir aynı**; 352 FBX'in 340'ı aynı boyutta, 12'si
  %0,3–2,5 farklı (H3_0_1, 0_5–0_7, 1_6, 7_17, 9_23, 13_19, 13_22, 16_18, 16_19, 17_21; shapely 2.0.7 / Blender 5.2 kayan nokta farkı);
  dokular piksel düzeyinde aynı, yalnız cephe atlasındaki tabela yazılarında %0,18 piksel kenar yumuşatma farkı (font sürümü).
  `dokular.py` fontu `HARITA_FONT` ile değiştirilebilir (macOS'ta Linux yolu yok).
- Unity: `Hat3Parcalari.Kur` FBX'lerin `Maps/Hat3/Materials`'a bağlı olduğunu denetler, değilse bütün parça FBX'lerini yeniden içe
  aktarır (temiz klonda dokusuz harita sorunu); `preserveHierarchy` kalıcı.

### M1 ✔ (15 Ekim, yerel) — denetim aracı ve başlangıç ölçümü
- `tools/harita/hat3/denetim.py` (bütün asfalt 2 m ızgara, 250 bin nokta; ~30 sn) ve `hat3/denetim_goruntu.py` (kümelere Blender
  EEVEE ile havadan + sürücü gözü görüntü; `harita_hat3.py --motor eevee --adlar`). Rapor `docs/gorseller/harita_v2/denetim_m1.txt`,
  görüntüler `docs/gorseller/harita_v2/m1/` (`liste.txt`).
- Başlangıç (bugünkü harita): delik 0 (asfalt çokgeninin içinde), **açıklık < 5 m 53 küme**, basamak 417, eğim > %12 232,
  **şerit asfalt dışı %16** (3 909 / 25 073 m), **kesik yol ucu 154**, **uç köşe 89 küme**, nesne havada/gömülü 129, ağaç dalı 116.
- Görüntüyle doğrulanan başlıca sorunlar:
  - **Kuzey kenarında uçurum** (Hasköy kuzeyi, z > 5 600): DEM karolarında siyah piksel (= −32 768 m "veri yok"); arazi, yollar,
    binalar ve göletler dik duvarlarla yüzlerce metre aşağı iniyor (`kose_27_yer`, `kose_28_hava`, `egim_12_yer`). M2'de DEM karoları
    yeniden indirilir/doldurulur.
  - **Köprüler**: Talatpaşa rampasının duvarı yolun üstünde (`aciklik_01`), Celal Bayar Bulvarı tabliyeleri alttaki yolun
    0,5–0,6 m üstünde (`aciklik_02`, `_03`: Sıhhiye köprüsünün çıkış yolları), Turgut Özal 2 Blv. rampası yolu kesiyor (`aciklik_00`).
  - **Şerit binanın içinden** (126. Sokak, `serit_15`), Aydınlıkevler'de şerit tabliyenin kenarında (`serit_17_yer`).
  - **Kesik yollar**: koridor sınırında asfalt düz zemine/havaya bitiyor (`kesik_20_yer`, `kesik_22_hava`).
  - **Zemin dokusu** (`zemin.png`) kaldırım taşına benziyor: arazi ile kaldırım ayırt edilmiyor (bütün görüntülerde) → M4'te zemin
    dokusu yeniden (toprak/çim/kentsel boşluk).
  - Basamak/eğim kümelerinin bir kısmı kavşaklarda yol yüzeyinin kırık/eğik üçgenleri (`basamak_09_yer`).
