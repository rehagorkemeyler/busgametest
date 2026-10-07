# Görev Dağılımı: Bulut ↔ Yerel Makine

**Bulut oturumu**: Claude, Unity olmadan, GPU'suz. Blender 5.2 arka planda çalışıyor.
**Yerel oturum**: Senin makinendeki Claude Code. Unity Editor'a, Asset Store'a, Android telefona ve senin Blender'ına erişebilir.

Her iki taraf da işini kendi branch'ında commit'leyip push'lar. Aynı dosyaya aynı anda dokunmayın. Özellikle `.unity` sahneleri tek elden düzenlenmeli.

## Bulutta yapılacaklar (Claude)

- [x] Modüler Ankara apartman seti (A1–A5), 10 varyasyon → `Maps/Buildings/Apartmanlar/` ([MODEL_KITLERI.md](MODEL_KITLERI.md))
- [x] Yol kiti: bulvar/Cinnah düz-eğimli-viraj, durak cepleri, T kavşak, dönüş halkası → `Maps/Roads/`
- [x] Simge yapılar: Atakule, Kızılay AVM, TBMM duvarı ve kapısı, Kuğulu gölet → `Maps/Landmarks/`
- [x] Sokak objeleri: EGO durağı, ağaçlar (çınar, kavak), bulvar lambası → `Maps/Props/` (trafik lambası trafik modülüyle gelecek)
- [x] İlk otobüs: **BMC Procity 12LF** → `Buses/BMC_Procity_12LF/` ([OTOBUS_BMC_PROCITY.md](OTOBUS_BMC_PROCITY.md))
- [ ] MAN SL: repodaki mod eksik (dış gövde yok), temel eklenti bulunursa `o3d_okuyucu.py` ile dönüştürülecek
- [ ] EGO kaplaması (`caroserie.png` şablonu üzerine)
- [ ] Trafik araçları: sarı Accent Blue taksi, klasik Türkiye arabaları (low-poly)
- [ ] C#: trafik AI, araç spawner (%30 taksi), yolcu sistemi
- [ ] Dokümantasyon, plan güncellemeleri

## Yerelde yapılacaklar (sen + yerel Claude Code)

Her maddedeki "Prompt" kısmını yerel Claude Code oturumuna olduğu gibi yapıştırabilirsin.

### Y1 — Unity projesini oluştur
**Prompt:**
> Repodaki `AnkaraBusSimulator/` klasörü Unity projemiz ama içinde henüz `Packages/` ve `ProjectSettings/` yok. Unity Hub ile repo dışında geçici bir klasörde "Universal 3D (URP)" şablonuyla `AnkaraBusSimulator` adlı yeni proje oluşturmama yardım et. Sonra o projenin `Packages/` ve `ProjectSettings/` klasörlerini repodaki `AnkaraBusSimulator/` içine kopyala, repodaki `Assets/`, `.gitignore` ve `.gitattributes` dosyalarının üzerine yazma. Ardından Unity Hub'da "Add project from disk" ile repodaki klasörü açtır. Project Settings'te Asset Serialization = Force Text, Version Control = Visible Meta Files olsun. Android'e geç: IL2CPP, ARM64, min API 26, Graphics API Vulkan + OpenGLES3. Unity açılıp `Assets/_Project/Scripts` derlendikten sonra derleme hatası varsa düzelt. Oluşan `.meta` dosyaları, `Packages/` ve `ProjectSettings/` dahil commit'le ve kendi branch'ıma push'la. `git lfs install` çalıştırılmış olsun.

### Y2 — Realistic Car Controller'ı import et
Asset Store paketi senin hesabına bağlı olduğu için bunu Unity Package Manager → My Assets üzerinden sen import etmelisin.
**Prompt:**
> RCC'yi import ettim. `Assets/` altında nereye kurulduğunu bul, demo sahnelerini ve örnek araçları ayrı tut, gereksiz demo içeriklerinin build'e girmediğinden emin ol. RCC'nin mobil kontrol arayüzünü (dokunmatik gaz/fren/direksiyon) nasıl açacağımızı bul ve `docs/RCC_NOTLARI.md` dosyasına not al. Commit'le, push'la.

### Y3 — İlk sürülebilir otobüs (hazır: BMC Procity 12LF)
**Prompt:**
> `docs/OTOBUS_BMC_PROCITY.md`'yi oku. `Assets/_Project/Buses/BMC_Procity_12LF/BMC_Procity_12LF.fbx`'ten bir otobüs prefabı kur: materyalleri çıkar ve `_Cam` ile bitenleri saydam yap, 4096'lık dokuları 2048'e sınırla. RCC ile araç kurulumu yap (belgedeki tablodaki kütle, ağırlık merkezi, tekerlek, motor ve şanzıman değerleriyle; `Teker_*` objeleri tekerlek modeli), gövdeye Box Collider ekle, `Lamba_*` objelerini kapat. `RigidbodyTelemetry`, `BusDoorController` (3 kapı grubu, her kanatta `BusDoor`, ±90° dönüş; yönleri sahnede test et), `RouteTracker` ekle. `Assets/_Project/Buses/BMC_Procity_12LF/BMC_Procity_12LF.asset` adında bir `BusDefinition` oluştur. Kokpit ve dış takip kamerası ekle. `Assets/_Project/Scenes/TestTrack.unity` adında bir test sahnesi oluştur: düz zemin + %10 eğimli 100 m rampa. RCC mobil arayüzüne kapı butonu ekleyip `BusDoorController.ToggleAll`'a bağla. Commit'le, push'la.

### Y4 — Telefonda test
**Prompt:**
> `TestTrack` sahnesini Android APK olarak build al. Telefonuma (USB hata ayıklama açık) yükle, Unity Profiler'ı telefona bağla. FPS, draw call ve bellek değerlerini `docs/PERFORMANS.md` dosyasına yaz. Commit'le, push'la.

### Y5 — Haritayı sahneye kur
Bulut tarafı bina ve yol kitlerini hazırladıktan sonra yapılır.
**Prompt:**
> `docs/HARITA_TASARIMI.md`'ye göre `Assets/_Project/Maps/` altındaki yol, bina ve simge yapı prefablarıyla `Map_A_Kizilay`, `Map_B_Bulvar`, `Map_C_Cinnah` prefablarını kur, `Scenes/Hat1_KizilayAtakule.unity` sahnesinde birleştir. 5 `BusStop` + 1 `BusRoute` yerleştir, otobüsü başlangıca koy. Statik objeleri Static işaretle, ışığı bake et, occlusion culling'i bake et. Commit'le, push'la.

### Y6 — (İsteğe bağlı) Blender'da elle rötuş
Bulutta üretilen modelleri senin Blender'ında Blender MCP ile beğenine göre düzeltmek istersen yerel oturum bunu yapabilir. Bulut tarafındaki üretim scriptleri `tools/blender/` altında olacak; aynı scriptleri yerelde de çalıştırabilirsin.
