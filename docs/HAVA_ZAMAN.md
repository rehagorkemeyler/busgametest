# Zaman (Gündüz / Akşam / Gece), Yağmur ve Kar

Ana menünün **ZAMAN · HAVA** satırında seçilir (`OyunSecimi.SeciliZaman`, `OyunSecimi.SeciliHava`: Açık / Yağmurlu / Karlı).
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

## Kar

- **Kar taneleri:** Kameranın 10 m üstünde 50×50 m alandan yavaşça süzülür, rüzgârla salınır (gürültü modülü). En çok 2500 parçacık,
  tek malzeme (`Resources/Hava/Kar.prefab`, `M_Kar`, `T_KarTanesi`).
- **Karlı zemin:** Palet malzemesinin karlı kopyası (`M_AnkaraPalet_Karli`, gecede `_GeceKarli`): çim, toprak, kaldırım, bordür, düz çatı
  ve kiremit beyaz, ağaçlar karlı, asfalt ıslak/sulu kar. Doku `tools/hava/karli_palet.py` ile paletten üretilir
  (`Resources/Hava/T_AnkaraPalet_Karli.png`); palete renk eklenince yeniden çalıştırılmalı.
- **Gök ve sis:** Beyazımsı gri gök, yakın sis (15–320 m; gece koyu mavimsi), güneş kısık, gölgeler soluk.
- **Ses:** Hafif rüzgâr uğultusu (oyun açılırken üretilir), içeride kısık.
- **Yol tutuşu:** Karda tekerlerin sürtünme sertliği %60'a, yağmurda %85'e iner (`BusVehicle.HavaTutusu`): fren mesafesi uzar,
  virajda ve kalkışta kayma artar.

## Malzemeler

**Ankara Bus → Hava ve Gece Malzemelerini Kur** bir kez çalıştırılır. Şunları `Resources/Hava` altına üretir:
- Palet malzemesinin gece, ıslak ve gece+ıslak kopyaları ve pencere ışık dokusu.
- Lamba parlama malzemesi.
- Yağmur damlası dokusu ve malzemesi, Yagmur prefabı.

Malzemeler yoksa Unity açılınca ve her build'den önce kendiliğinden kurulur (`HavaZamanKurucu.EksikseKur`), menüyü unutmak gece ve yağmuru bozmaz. Oluşan `Resources/Hava` klasörü commit'lenmelidir.

Bu malzemeler hazır durur, çünkü oyunda shader anahtar kelimesi açmak build'de çıkarılmış varyanta takılır. Araç ayrıca sis shader varyantlarının build'de kalmasını sağlar.

## Bilinen eksikler

- **Silecekler:** BMC modunda silecek tek bir obje olarak geliyor (iki silecek birlikte, 20 animasyon karesi). Ayrı dönen parçalara ayırmak modelin yeniden dönüştürülmesini gerektiriyor; ayrı bir iş olarak bırakıldı.
- **Trafik araçlarında far yok:** Low-poly modellerde lamba parçası yok.
