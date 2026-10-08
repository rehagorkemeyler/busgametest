# Ana Menü

Ana menü sahnesi `Scenes/AnaMenu.unity`'dir ve **Ankara Bus → Ana Menü Sahnesini Kur** ile kurulur (`Editor/AnaMenuKurucu.cs`). Kurucu sahneyi Build Settings'te ilk sıraya koyar, yani oyun bu sahneyle açılır.

## Ekran

Sol yarıda menü:
- **HAT:** Her hat için bir kart: hat numarası, adı, durakları ve en iyi puanı ("Henüz oynanmadı" ya da "En iyi puan: 437"). Sahnesi build'de olmayan hat seçilemez.
- **KAPLAMA:** Rastgele, EGO kırmızı, EGO mavi, Özel Halk. Seçince sağdaki otobüs hemen o kaplamaya geçer.
- **GÖRÜNTÜ:** Düşük / Normal / Yüksek (`GrafikAyarlari`). Altında telefon için önerilen seviye yazar. Yüksek seçilince vitrindeki otobüs de tam kalite modele geçer.
- **SEFERE BAŞLA** (sağ alt): Seçili hattı bir yükleniyor ekranıyla açar. **ÇIKIŞ** ve Android geri tuşu oyunu kapatır.

Sağ yarıda döner podyumda otobüs (`UI/MenuVitrini.cs`) durur:
- Kendiliğinden yavaşça döner, parmakla ya da fareyle çevrilebilir.
- Kamera ekran oranına göre yerleşir, otobüsü ekranın sağ yarısında tutar.
- Arkada Atakule ve yarım halka apartmanlar (hat kitlerinden) vardır.

Vitrindeki otobüs, prefabın görsel kopyasıdır: fizik, ses, puan ve girdi bileşenleri kurulumda çıkarılır. Yalnızca `BusLivery` ve `BusKaliteModeli` kalır.

## Seçimler

Seçimler `Gameplay/OyunSecimi.cs` üzerinden PlayerPrefs'te saklanır:

| Anahtar | Değer |
|---|---|
| `SeciliHat` | `OyunSecimi.Hatlar` sırası |
| `Kaplama` | −1 rastgele, 0.. kaplama sırası (kayıt yoksa prefabın kendi kaplaması) |
| `EnIyiPuan_<hat no>` | Hattın en iyi puanı, `SeferPuanlama` yazar |

Hat sahnesindeki otobüs kaplamayı kendisi uygular (`BusLivery.useMenuChoice`). Bir hat sahnesi Editor'de doğrudan açılıp oynanırsa da son menü seçimi geçerli olur.

Yeni bir hat eklemek için `OyunSecimi.Hatlar`'a satır eklemek ve sahnesini Build Settings'e koymak yeterli.

## Oyundan menüye dönüş

- **Hat sonu özeti** (`PuanGostergesi`): TEKRAR / MENÜ / KAPAT.
- **AYARLAR paneli** (`BusTouchControls`): ANA MENÜ / KAPAT.

Menü sahnesi build'de değilse MENÜ düğmeleri görünmez (örneğin ölçüm APK'larında).

## Build

**Ankara Bus → Android → Oyun APK'sı** ana menü + Hat 1 + Hat 2 ile `Builds/AnkaraBus.apk` üretir. Ölçüm ve otomatik pilot eklenmez.
