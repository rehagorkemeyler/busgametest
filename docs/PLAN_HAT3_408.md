# Hat 3 — EGO 408: Kızılay → Güneşevler (gerçek coğrafya planı)

Kullanıcı isteği (14 Ekim): haritayı Kızılay'dan Güneşevler'e büyüt; binalar ve simge yapılar gerçeğe benzesin, duraklar gerçekle
tutarlı olsun; harita sınırları görünmesin (özellikle Kızılay–Ulus arasında belli oluyordu). **Her aşama bitmeden yerel teste
geçilmeyecek**; yerel test promptu en sonda (A8).

Hat 1 ve Hat 2 "kısaltılmış, stilize" haritalardı (sabit 20 m'lik yol parçaları, ~1,3–1,9 km). Hat 3 farklı bir yaklaşım:
**1:1 gerçek geometri**, OpenStreetMap'ten yol ve bina ayak izleri, SRTM'den arazi, EGO'dan durak listesi.

## Gerçek hat (kaynaklar)

- **EGO 408** "Gülpınar–Güneşevler–15 Temmuz Kızılay Milli İrade Meydanı–Atakule–Birlik Mh." (39 km, 94 durak;
  `ego.gov.tr/hareketsaatleri?hat_no=408`). Oyun, dönüş yönünün Kızılay → Güneşevler bölümünü (sıra 67–84) sürer.
- Durak konumları: OSM'deki EGO durak numaraları (`bus_stop` + `ref`); 12'si birebir eşleşti. Sıhhiye kuzey yönü ve Güneşevler
  ucundaki 6 durak OSM'de numarasız → EGO adresinden (cadde/mahalle) yaklaşık (aşağıda ✱).
- Güzergâh: durakları sırayla OSM yol ağında yönlü bağlama (`tools/harita/hat408_guzergah.py`; durak kendi yönündeki şeridin
  sağında). Sonuç **8,93 km**:

| Cadde | m | Not |
|---|---|---|
| Atatürk Bulvarı (Kızılay → Sıhhiye → Opera → Ulus) | 2 140 | 4 şerit tek yön (ayrık), Sıhhiye köprüsü |
| Ulus Meydanı → Çankırı Caddesi | 1 090 | Zafer Anıtı, ardından Çankırı Cd. kuzeye |
| Samsun–Ankara Yolu, Şehit Ömer Halisdemir Bulvarı (eski İrfan Baştuğ Cd.) | 3 230 | Dışkapı, Veteriner Fak., Aydınlıkevler, Altınpark, Subayevleri, Hasköy; ana arter (trunk) |
| Malazgirt Caddesi → 126. Sokak | 815 | Hasköy'de bulvardan sağa; Güneşevler mahalle içi |
| Celal Esat Arseven → Mehmet Akif Ersoy Caddesi | 1 450 | Çiçek Taksi'den doğuya, yokuş yukarı son durak |

| # | Durak (EGO no) | Rakım | Kaynak |
|---|---|---|---|
| 1 | Kızılay (11621) | 869 | OSM |
| 2 | Sıhhiye (30010) | 856 | ✱ (OSM'de bu numara yok; demiryolu köprüsünün kuzeyi, Hacettepe Mh.) |
| 3 | Opera (30384) | 861 | OSM |
| 4 | Ulus (30391) | 872 | OSM |
| 5 | Yıba Çarşısı (30039) | 872 | OSM |
| 6 | Dışkapı Hastanesi (31682) | 862 | OSM |
| 7 | Veteriner Fakültesi (30431) | 864 | OSM |
| 8 | Çağdaş Sokak (41481) | 868 | OSM |
| 9 | Atatürk Anadolu İmam Hatip Lisesi (40487) | 865 | OSM |
| 10 | Altınpark (41847) | 880 | OSM |
| 11 | Subayevleri (40077) | 897 | OSM |
| 12 | Hasköy Akşemsettin Camii (40079) | 889 | OSM |
| 13 | Pazar Yeri (40090) | 885 | ✱ Malazgirt Cd. |
| 14 | Esenevler Anadolu Lisesi (40492) | 890 | ✱ 126. Sk. |
| 15 | Çeyizciler (40559) | 888 | ✱ 126. Sk. |
| 16 | Çiçek Taksi (41532) | 893 | ✱ OSM'deki taksi durağının yanı |
| 17 | Güneşevler Spor Tesisleri (41530) | 937 | ✱ Mehmet Akif Ersoy Cd., yüzme havuzu tesisleri karşısı |
| 18 | Güneşevler (41488) — son durak | 964 | ✱ Ali Ersoy Mh. |

Rakım profili (SRTM): Kızılay 869 → Sıhhiye çukuru 855 → Ulus 872 → Dışkapı 862 → Subayevleri 897 → Güneşevler 964 m.
En dik yerler: Mehmet Akif Ersoy yokuşu (son 1,4 km, %7–9), Altınpark–Subayevleri (%5–7).

Koridor: güzergâhın 60 m çevresinde 616, 120 m'de 1 174, 250 m'de 2 453, 400 m'de 4 037 bina (OSM ayak izi; yalnız ~%6'sında kat
bilgisi). Güzergâhta OSM'de 16 trafik ışığı, 18 yaya geçidi.

## Kararlar

1. **Ölçek 1:1.** 8,9 km, 18 durak; gerçek sürüş ~25–30 dk. İsteğe bağlı başlangıç durağı seçimi (A7) kısa oyun için.
2. **Hat 1 ve Hat 2 yerinde kalır.** Hat 3 Kızılay–Ulus'u gerçek geometriyle zaten içerir; kabul edilirse Hat 2 ileride Hat 3
   verisinden yeniden üretilir (Kızılay–Ulus kesiti).
3. **Veri lisansı:** OSM © OpenStreetMap katkıcıları, ODbL (KREDILER'e). Uydu görüntüsü ve fotoğraflar yalnızca **referans**;
   oyuna doku olarak girmez. Cepheler prosedürel (PIL) ya da elle modellenir.
4. **Mobil bütçe** (PERFORMANS.md): görünür alanda < 300k üçgen hedefi, < 150 draw call (Hat 1 Kızılay ölçümü 279–643 DC);
   harita parçalara bölünür, uzak parçalar kapanır, binalar parça başına tek mesh + LOD.

## Durum (14 Ekim, bulut)

Bütün aşamalar (A0–A7) tamam, yerel test bekliyor (A8). Ayrıntı ve önizlemeler: `docs/HAT3_KIZILAY_GUNESEVLER.md`.
Plandan farklar: Hat 2 de bu haritaya taşındı (ilk dört durak; eski stilize Hat 2'nin harita kenarları görünüyordu); durak cepleri
yok (otobüs sağ şeritte, Kızılay/Ulus'taki gibi); çapraz trafik yalnız ışıklı kavşaklarda; ışık bake'i yok.

## Aşamalar

### A0 — Veri ✔ (bu commit)
- `tools/harita/osm_birlestir.py`: OSM API 0.6 `/map` 0,01° karoları (39.915–39.985 K, 32.840–32.910 D; 49 karo) → `osm.json.gz`
  (190k düğüm, 34,6k yol/alan, 600 ilişki). Overpass bu boyutta zaman aşımına düşüyor; ana API karoları güvenilir.
- `tools/harita/hat408_guzergah.py`: durak listesi + güzergâh → `hat408.json`.
- `tools/harita/dem.py`: terrarium karoları (z15, ~4,8 m/piksel örnekleme, kaynak çözünürlüğü ~30 m).
- Veri `tools/harita/veri/` altında: `osm_hat408.json.gz` (~3 MB), `hat408.json`, `dem/` (90 terrarium karosu, ~6 MB).

### A1 — Arazi ve güzergâh profili
- Yerel koordinat: Kızılay durağı (0,0,0), Unity +Z kuzey, +X doğu; rakım Kızılay'a göre (Unity y = rakım − 868,7).
- Yol rakımı: DEM'den yol ekseninde örnek, 60 m pencereli yumuşatma + en çok %10 eğim; köprüler (OSM `bridge`/`layer`: Sıhhiye
  demiryolu köprüsü, Dışkapı ve Aydınlıkevler kavşak köprüleri) ayrı profil.
- Arazi ağı: koridorda (güzergâhtan 450 m) 6 m ızgara, dışında 1,5 km'ye kadar 30 m; yolların altı yol rakımına oturtulur,
  bina tabanları zemine. 300 m'lik parçalara (chunk) bölünür; parça adları `H3_<i>_<j>`.

### A2 — Yol ağı
- Güzergâh yolları tam ayrıntı: şerit sayısı (OSM `lanes`, yoksa sınıftan), ayrık yollarda refüj, bordür (15 cm), kaldırım
  (OSM `sidewalk`/genişlik ya da sınıfa göre 3–5 m), şerit çizgileri, durak cepleri (18 durak, gerçek konumda; Kızılay/Ulus'ta
  cep yerine kaldırım durağı gerçekteki gibi), yaya geçitleri (OSM `crossing`), trafik ışıklı kavşaklarda durma çizgisi.
- Kavşaklar: kesişen yolların çokgen birleşimi (shapely), köşe yarıçapı 6–10 m.
- Yan sokaklar: güzergâhtan 120 m'ye kadar gerçek geometri, sonra binaların arkasında biter (A5).
- Çarpışma: yol + kaldırım + bordür tek MeshCollider/parça.

### A3 — Binalar
- Tüm ayak izleri 250 m'ye kadar tam model, 250–450 m dolgu (düşük ayrıntı), dışı yok (A5 silueti).
- Kat sayısı: OSM `building:levels`/`height` varsa o; yoksa bölge kuralı (Kızılay–Sıhhiye 6–10, Ulus 3–6 eski kesme taş,
  Çankırı Cd. 5–8, Dışkapı–Aydınlıkevler 5–9, Subayevleri 4–7 lojman blokları, Hasköy/Güneşevler 3–6, Mehmet Akif Ersoy 4–8)
  + ayak izi alanı/şekli + uydu görüntüsünden gölge boyu (referans, elle ayar listesi).
- Tipoloji ve cephe: 10–12 Ankara cephe dokusu (PIL atlası; 1960–80 bulvar apartmanı, kesme taş Ulus, 1990 sonrası
  mantolama/PVC, cam ofis, hastane, okul, cami avlusu duvarı), zemin katta güzergâha bakan cephelerde dükkân + Türkçe tabela
  (eczane, büfe, fırın, emlak, kuaför, lokanta, banka şubesi…), balkon şeritleri; düz çatı (su deposu, anten, güneş paneli)
  ya da kiremit kırma çatı (alçak binalar).
- Tek mesh/parça, iki LOD (LOD1: düz kutu + aynı doku), çarpışma kutusu yalnız ön sıra.

### A4 — Simge yapılar (elle, Blender)
Önce güzergâhtan görünenler, fotoğraf referansıyla (Wikimedia Commons):
- Kızılay: mevcut SKP AVM, Emek İşhanı, Güvenlik Anıtı (gerçek konumlarına).
- Sıhhiye: Hitit Güneş Kursu (mevcut), demiryolu köprüsü, Ankaray girişi.
- Opera: Opera binası (mevcut), Gençlik Parkı (mevcut; gerçek ayak izine göre yeniden ölçeklenir).
- Ulus: Zafer Anıtı (mevcut), Ulus Meydanı çevresi (Ziraat Bankası Genel Müdürlüğü, İş Bankası eski binası, Ulus İşhanı/çarşı
  blokları), Ankara Kalesi silueti (doğuda tepede).
- Çankırı Cd.: Yıba Çarşısı, Hacı Bayram Camii minareleri (sağda, uzakta).
- Dışkapı: Dışkapı Yıldırım Beyazıt Eğitim ve Araştırma Hastanesi, Ankara Üniversitesi Veteriner Fakültesi, Dışkapı metro girişi.
- Altınpark: göl, giriş, fuar alanı (soldan görünen kısım).
- Güzergâhtaki camiler (OSM'de koridorda onlarca): kubbe + 1–2 minare, 3 boyut ailesi.
- Güneşevler: Merkez Camii, yüzme havuzu/spor tesisi, Cemre Parkı.

### A5 — Harita sınırlarını gizleme
- 250–450 m arası dolgu binaları (gerçek ayak izleri, düşük ayrıntı) → sokak aralarından bakınca boşluk yok.
- 450 m–3 km: arazi (DEM) + doku ile boyanmış "uzak şehir" kabukları (Ankara'nın tepelere tırmanan apartman/gecekondu dokusu),
  Kale ve tepelerin silueti; ufukta gökyüzü kartı.
- Sis: gündüz 250–1500 m mevcut ayar korunur; uzak kabuklar sise karışacak renkte.
- Yan sokaklar 120 m sonra bir binanın arkasında/virajda biter; ağızlarda görünmez duvar; otobüs güzergâh dışına çıkarsa
  mevcut "güzergâh dışı" uyarısı.

### A6 — Sokak donatısı ve ağaçlar
- OSM ağaç noktaları + refüj/kaldırım ağaç dizileri (uydu referansı), park alanları (çim + ağaç kümeleri).
- Bulvar lambaları (Atatürk Bulvarı nostaljik lamba mevcut), Halisdemir Bulvarı refüj direkleri.
- Trafik ışıkları OSM `traffic_signals` noktalarında (16), faz grupları kavşak geometrisinden.
- 18 EGO durağı (gerçek ad tabelası), reklam panoları, çöp kutuları, bariyerler.

### A7 — Unity entegrasyonu
- `HaritaKurucu` ile aynı JSON şeması + yeni alanlar: `parcalar` (chunk listesi), parça başına model; gruplar aynı adlarla
  (`Yol, Zemin, Binalar, SimgeYapilar, Agaclar, Lambalar, Duraklar`).
- Parça yöneticisi: otobüse uzaklığa göre parçaları açar/kapar (Düşük 450 m, Normal 650 m, Yüksek 900 m), uzak kabuklar hep açık.
- Şeritler: güzergâh yollarının iki yönü + önemli kesişen yollar; kavşak bağlantıları; 16 sinyal.
- `TrafficSpawner`: rastgele şerit yerine otobüse yakın şerit parçalarından seçim (uzamsal dizin) — 9 km'de araçlar kaybolmasın.
- `MiniHarita`: tek doku yerine otobüs çevresi (2 km) ya da parça dokuları.
- Rota, 18 durak, anonslar (Piper TTS, `anonslar.py`'ye 408 durakları), menü kartı "408 Kızılay – Güneşevler", LED tabela
  "408 GÜNEŞEVLER", puanlama hedef süreleri.
- Işık/occlusion bake: parça başına; 9 km tek lightmap'e sığmaz → önce gerçek zamanlı güneş + ışık probları, bake sonra.

### A8 — Performans, dokümanlar, yerel test
- Blender'da parça başına üçgen/malzeme sayımı ve sürücü gözü önizlemeleri (`docs/gorseller/hat3/`).
- KREDILER (OSM ODbL, EGO durak listesi), HARITA dokümanı, GOREVLER'e tek yerel prompt (derle, kur, telefonda sür, ölç).
