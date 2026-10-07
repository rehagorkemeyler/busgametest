# Ankara Bus Simulator — Aksiyon Planı

İlgili belgeler: [Harita tasarımı](HARITA_TASARIMI.md) · [Görev dağılımı (bulut ↔ yerel)](GOREVLER.md)

**Hedef:** Unity ile Android'de akıcı çalışan, Ankara otobüsleriyle Ankara sokaklarında (binalar, yokuşlar) sürülen, modüler ve üstüne eklenebilir bir otobüs simülasyonu.

**MVP tanımı (ilk elle tutulur sürüm):**
Android telefonda açılan bir APK. İçinde bir Ankara otobüsü, Kızılay AVM'den Atakule'ye uzanan stilize bir harita (~2,2 km, Cinnah yokuşu dahil) ve 5 duraklı bir hat: Kızılay AVM → Meclis → Kuğulu Park → Cinnah → Atakule. Oyuncu otobüsü dokunmatik kontrollerle sürer, durakta durup kapıları açar, hat tamamlanır. Orta seviye bir telefonda en az 30 FPS.

MVP'de **yok**: yolcu karakterleri, trafik, bilet/para sistemi, kariyer, gece/gündüz, hava durumu. Bunlar MVP'den sonra modül olarak eklenecek.

---

## Mimari ilkeleri

1. **Fizik paketinden bağımsızlık.** Araç sürüşü Realistic Car Controller (RCC) ile yapılır. Kapılar, duraklar ve HUD ise RCC'ye değil `IVehicleTelemetry` arayüzüne bağlıdır. İleride fizik paketi değişirse oyunun geri kalanı etkilenmez.
2. **Veriyle eklenen içerik.** Yeni otobüs = yeni `BusDefinition` asset'i + prefab. Yeni hat = sahneye `BusStop`'lar + bir `BusRoute`. Kod değişikliği gerekmez.
3. **Mobil öncelikli.** Her karar önce orta seviye bir Android telefonda test edilir, PC'de değil.

### Klasör yapısı

```
busgametest/
├── AnkaraBusSimulator/          ← Unity projesi
│   └── Assets/_Project/
│       ├── Scripts/
│       │   ├── Vehicle/   IVehicleTelemetry, RigidbodyTelemetry, BusDoor, BusDoorController, BusDefinition
│       │   ├── Route/     BusStop, BusRoute, RouteTracker
│       │   └── UI/        BusHud
│       ├── Buses/         her otobüs kendi klasöründe (model, doku, ses, prefab, BusDefinition)
│       ├── Maps/          harita parçaları, binalar, yollar
│       ├── Scenes/
│       └── UI/
├── BMC Procity 12LF/            ← ham kaynak (Proton Bus Simulator modu)
├── MAN SL EGO EDIT/             ← ham kaynak (OMSI 2 modu)
└── docs/
```

Script'ler şu an hazır (`Assets/_Project/Scripts`). Unity bunları ilk açılışta derleyecek ve `.meta` dosyalarını üretecek; o `.meta` dosyaları da commit'lenmeli.

---

## Faz 0 — Proje kurulumu (1–2 gün)

- [ ] Unity Hub → **New project → Universal 3D (URP)**, adı `AnkaraBusSimulator`, repo **dışında** geçici bir yere oluştur.
- [ ] Oluşan projeden `Packages/` ve `ProjectSettings/` klasörlerini repodaki `AnkaraBusSimulator/` içine kopyala (repodaki `Assets/` ve `.gitignore` korunur).
- [ ] Unity Hub → **Add → Add project from disk** → repodaki `AnkaraBusSimulator`.
- [ ] Build Settings → **Android**'e geç. Player Settings: IL2CPP, ARM64, minimum API 26+, Graphics API: Vulkan + OpenGLES3.
- [ ] URP ayarı: mobil renderer, gölge mesafesi ~60 m, MSAA 2x, HDR kapalı.
- [ ] **Git LFS** kur (`git lfs install`). Unity projesindeki `.gitattributes` model, doku ve sesleri otomatik LFS'e alır. Her iki geliştirici de kurmalı.
- [ ] Unity Editor → Project Settings → Editor → **Asset Serialization: Force Text**, **Version Control: Visible Meta Files**.

## Faz 1 — Otobüs sürülebilir olsun (3–5 gün)

- [ ] **RCC** paketini Asset Store'dan import et.
- [ ] İlk otobüsü seç (öneri: MAN SL 223 EGO — Ankara'nın simge otobüsü).
- [ ] Modeli Unity'ye getir:
  - BMC Procity: `.3ds` → Blender'da aç, temizle, `.fbx` olarak çıkar.
  - MAN SL: OMSI `.o3d` → Blender'a OMSI o3d importer eklentisiyle al → `.fbx`.
  - Gövde, tekerlekler (4–6 ayrı obje), kapı kanatları **ayrı objeler** olmalı; pivotlar menteşe noktasında.
- [ ] Otobüs prefabı: RCC ile araç kurulumu (kütle ~12–16 t, ağırlık merkezi alçak, tekerlek collider'ları), `RigidbodyTelemetry`, `BusDoorController` + her kanatta `BusDoor`, `RouteTracker`.
- [ ] Düz bir test zemini + **%8–10 eğimli rampa** üzerinde sürüş ayarı (yokuş kalkışı, el freni, otomatik vites).
- [ ] RCC'nin mobil kontrol arayüzünü aç (gaz, fren, direksiyon/eğim). Arayüze kapı butonu ekle → `BusDoorController.ToggleAll`.
- [ ] Kamera: kokpit + dış takip kamerası.
- [ ] **Telefonda ilk APK testi.**

## Faz 2 — Ankara haritası, ilk parça (1–2 hafta)

- [x] Güzergâh: Kızılay AVM → Atakule. OSM ve SRTM verisiyle incelendi, stilize harita tasarlandı ([HARITA_TASARIMI.md](HARITA_TASARIMI.md)). Gerçek harita birebir kopyalanmayacak.
- [ ] Blender'da modüler yol kiti: düz, eğimli, kıvrım, kavşak, refüj, kaldırım, durak cebi.
- [ ] Simge yapılar: Atakule, Kızılay AVM, TBMM duvarı ve kapısı, Kuğulu gölet.
- [ ] Binalar: 3–5 tip **modüler Ankara apartmanı** (4–8 kat, balkonlu, sıva renkleri, zemin katta dükkân), her birinden renk/kat varyasyonu. Teker teker modellemek yerine kit mantığı.
- [ ] Mobil bütçe: bina başına 1 materyal + doku atlası, LOD0/LOD1/billboard, harita parçası başına < 150 draw call, < 300k üçgen görünür alanda.
- [ ] Unity: statik batching, ışık **bake** (gerçek zamanlı gölge sadece otobüse), occlusion culling.

## Faz 3 — Oynanış döngüsü (3–5 gün)

- [ ] Haritaya 4–6 `BusStop` + bir `BusRoute` yerleştir.
- [ ] `BusHud`: hız, hat, sıradaki durak.
- [ ] Hat başlangıcı → duraklar → bitiş ekranı.
- [ ] Basit ana menü: otobüs seç, hat seç, başla.
- [ ] Ses: motor, kapı, fren, sinyal (mevcut `.wav` dosyalarından).

## Faz 4 — MVP cilası ve test (3–5 gün)

- [ ] Profiler ile telefonda ölç: FPS, draw call, bellek, ısınma.
- [ ] Doku sıkıştırma ASTC, sesler Vorbis, gereksiz asset temizliği.
- [ ] APK boyutu < 150 MB hedefi.
- [ ] 2–3 farklı telefonda test.

**MVP tamam.** ✅

## MVP sonrası modüller (sırayla eklenebilir)

| Modül | Not |
|---|---|
| Yolcular | Durakta bekleyen / binen / inen basit karakterler, otobüs doluluk |
| Trafik | Şerit takip eden basit AI araçlar, trafik ışıkları. ~%30 sarı Hyundai Accent Blue taksi, gerisi klasik Türkiye arabaları ve dolmuş |
| İkinci otobüs | BMC Procity (doğalgazlı EGO otobüsü) |
| Yeni hatlar ve harita parçaları | Kızılay, Ulus, Dikmen, Çankaya, Keçiören… |
| Puanlama / kariyer | Zamanında varış, sarsıntısız sürüş, para, otobüs satın alma |
| Gündüz/gece, hava | Kar özellikle Ankara'ya çok yakışır |
| Göstergeler | Etkileşimli kokpit, hat tabelası (LED), anonslar |

---

## Telif ve lisans

Bu proje **kişisel prototip**tir; yayınlanması planlanmıyor. Repodaki `BMC Procity 12LF` (Proton Bus Simulator modu) ve `MAN SL EGO EDIT` (OMSI 2 modu) başkalarının yaptığı modlar, marka araç modelleri (Hyundai, Tofaş vb.) de lisanslı değil. İleride yayın gündeme gelirse bunların hepsi değiştirilmeli veya izni alınmalı.

## Paralel çalışma kuralları

- Herkes kendi branch'ında (`Ozan`, `gorkem`), birleştirme `main`'e pull request ile.
- **Aynı sahne (`.unity`) dosyasını aynı anda iki kişi düzenlemesin.** Unity sahneleri birleştirmede kolay bozulur. İş prefab'lara bölünmeli: biri otobüs prefabında, diğeri harita prefabında çalışabilir.
- İşe başlamadan önce `git pull origin main`.
