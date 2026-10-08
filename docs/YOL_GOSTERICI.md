# Yol Gösterici ve Mini Harita

## Güzergâh: `Route/RotaRehberi.cs`

Otobüs prefabında durur. Prefab yeniden kurulmamışsa, rotası olan otobüse sahne yüklenince `RotaRehberi` ve `MiniHarita` kendiliğinden eklenir. Oyun açılınca hattın güzergâhını sahnedeki trafik şeritlerinden kendisi bulur, bu yüzden sahne dosyasına ya da yerleşim JSON'una ek veri gerekmez:
- **Grafik:** Şeritler 5 m aralıkla örneklenir. Bağlantılar şunlardır: şerit boyunca ilerleme, kavşak çıkışları (`TrafficLane.Exits`) ve aynı yöndeki yan şeride geçiş (küçük cezayla).
- **Yol bulma:** Otobüsün başlangıcından 1. durağa, oradan 2. durağa... her ayak için en kısa yol bulunur (Dijkstra). Uç noktalar, yönü uyan en yakın şerit noktasıdır (durağın yönü `BusStop` dönüşünden alınır). Duraklar güzergâha nokta olarak eklenir, yani çizgi durak cebine girip çıkar.

Her karede şunları hesaplar:

| Değer | Ne |
|---|---|
| `Konum` | otobüsün güzergâh boyunca yeri (m), son konumun çevresinde aranır |
| `DurakaKalan` | sıradaki durağa güzergâh boyunca kalan mesafe |
| `SiradakiDonus` / `DonuseKalan` | 220 m içinde, 15 m'lik pencerede 40°'den büyük yön değişimi (sağ/sol) |
| `RotadanCikti` | güzergâha 25 m'den uzak |

Yeni bir hat için ek iş gerekmez: duraklar arası şeritler (dönüşlerde çıkışlarla) bağlıysa güzergâh bulunur. Bağlı değilse o ayak düz çizgiyle çizilir.

## Ekran: `UI/MiniHarita.cs`

- **Mini harita:** Sağ üstte, D/N/R düğmelerinin altında yuvarlak harita. Otobüsün yönü hep yukarıdadır ve harita otobüsle döner. Kırmızı "K" kuzeyi gösterir.
  - **Görünenler:** gri yollar, koyu binalar, yeşil ağaçlar, sarı güzergâh, beyaz duraklar ve mavi ok (otobüs).
  - **Sıradaki durak:** Sarı noktayla gösterilir. Uzaktaysa haritanın kenarında, durağın yönünde durur.
  - **Kapsam:** Merkezden kenara 160 m.
- **Bilgi bandı:** Haritanın altında.
  - 1\. satır: durak adı ve kalan mesafe ("Meclis  420 m").
  - 2\. satır: "150 m sonra SAĞA" / "durağa yanaş" (120 m kala) / "düz devam".
  - Rotadan çıkınca kırmızı "ROTADAN ÇIKTIN — sarı çizgiye dön".
- **Büyük harita:** Mini haritaya dokununca açılır, bütün hattı numaralı duraklar ve otobüsün yeriyle gösterir. Uzun hatlar yatay çevrilir. Dokununca kapanır.
- **Harita dokusu:** Oyun açılırken bir kez çizilir ve CPU kopyası bırakılır.
  - Kaynaklar: şeritler, `Binalar` / `SimgeYapilar` / `Duraklar` gruplarındaki modellerin taban dikdörtgenleri ve `Agaclar`.
  - Çözünürlük metre başına 1,2 piksel, en çok 2048 piksel. Hat 1 için yaklaşık 900×1900 piksel, ~7 MB.
  - Her karede yalnızca RawImage'in konumu ve dönüşü değişir; ek kamera yoktur.

EL FRENİ ve KAPILAR düğmeleri mini haritaya yer açmak için biraz aşağı indi (`BusTouchControls`).
