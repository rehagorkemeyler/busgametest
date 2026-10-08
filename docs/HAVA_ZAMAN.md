# Zaman (Gündüz / Akşam / Gece) ve Yağmur

Ana menünün **ZAMAN · HAVA** satırında seçilir (`OyunSecimi.SeciliZaman`, `OyunSecimi.Yagmur`).
Hat sahnesinde `Gameplay/HavaVeZaman.cs` uygular. Bu bileşen sahneye eklenmez: otobüs ve rota olan her sahne yüklenince kendiliğinden oluşur.

## Akşam ve gece

Sahnenin ışığı bake'li olduğu için (binalar ve yol lightmap'ten aydınlanır) yalnızca güneşi kısmak yetmez. Her hat için akşam ve gece ayrıca bake edilir.

**Ankara Bus → Akşam ve Gece Işığını Bake Et → Hat 1 / Hat 2** (`Editor/HavaZamanKurucu.cs`) şunları yapar:
1. Güneşi ve gökyüzünü `ZamanAyarlari` değerlerine göre kurar. Akşam: alçak turuncu güneş. Gece: soluk mavi ay ışığı ve karanlık gök.
2. Her sokak lambasının iki başına geçici bake ışıkları koyar (sodyum turuncu, gölgeli) ve bake eder.
3. Lightmap'leri ve ışık problarını `Resources/IsikSetleri/<sahne>_<Aksam|Gece>`'ye kopyalar (`IsikSeti`).
4. Gündüzü `HatIsikBake` ile yeniden bake eder; sahne gündüz haliyle kalır. Bir sahne için toplam 3 bake yapılır.

Oyunda seçilen set uygulanır:
- **Işık:** Lightmap dizisi değişir (yerleşim aynı sahneden geldiği için renderer indeksleri geçerli kalır). Problar kopya bir `LightProbes` nesnesine yazılır.
- **Güneş, gökyüzü ve sis:** Gökyüzü malzemesi kopyalanıp ayarlanır.
- **Pencereler:** Haritanın palet malzemesi gece kopyasıyla değişir. Perdeli pencereler sıcak, dükkân vitrinleri beyaz, ofis camları soğuk floresan yanar; akşam daha sönüktür.
- **Lamba başları:** Hepsi tek bir birleşik parlama mesh'iyle (tek çizim) parlar.
- **Otobüs:**
  - Kısa far, park ve iç lambaları (prefabdaki `Lamba_*` parçaları) yanar.
  - Kabine iç ışık konur.
  - Normal/Yüksek kalitede gerçek zamanlı far ışığı (spot) eklenir. Düşük'te yalnızca far camları yanar, A32 doluluk sınırında olduğu için.
  - Fren lambası her zaman frene basınca yanar.

Set bake edilmemişse uyarı yazılır ve binalar gündüz ışığında kalır.

## Yağmur

- **Damlalar:** Kameranın 12 m üstünde 40×40 m alandan yağar. En çok 1200 parçacık ve tek malzeme kullanılır (`Resources/Hava/Yagmur.prefab`).
- **Islak görünüm:** Yollar ve binalar palet malzemesinin ıslak kopyasıyla biraz koyu ve parlak olur. Gecede gece+ıslak kopyası kullanılır.
- **Gök ve sis:** Gök gri, sis yoğun (25–420 m), güneş kısık.
- **Ses:** Yağmur sesi oyun açılırken üretilir (gürültü + damla tıkırtıları, ses dosyası yok). Kokpit ve yolcu görünümünde daha kısık duyulur.

## Malzemeler

**Ankara Bus → Hava ve Gece Malzemelerini Kur** bir kez çalıştırılır. Şunları `Resources/Hava` altına üretir:
- Palet malzemesinin gece, ıslak ve gece+ıslak kopyaları ve pencere ışık dokusu.
- Lamba parlama malzemesi.
- Yağmur damlası dokusu ve malzemesi, Yagmur prefabı.

Bu malzemeler hazır durur, çünkü oyunda shader anahtar kelimesi açmak build'de çıkarılmış varyanta takılır. Araç ayrıca sis shader varyantlarının build'de kalmasını sağlar.

## Bilinen eksikler

- **Silecekler:** BMC modunda silecek tek bir obje olarak geliyor (iki silecek birlikte, 20 animasyon karesi). Ayrı dönen parçalara ayırmak modelin yeniden dönüştürülmesini gerektiriyor; ayrı bir iş olarak bırakıldı.
- **Trafik araçlarında far yok:** Low-poly modellerde lamba parçası yok.
- **Islak yolda tutuş:** Yağmurda yol tutuşu değişmiyor.
