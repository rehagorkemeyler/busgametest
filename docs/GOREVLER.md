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
- [x] Hat 1 yerleşimi + Unity editör kurucusu (`harita_hat1.py`, `HaritaKurucu.cs`)
- [x] Trafik araçları (taksi, klasikler, dolmuş) + C# trafik (şerit, araç, spawner %30 taksi) → [TRAFIK.md](TRAFIK.md)
- [ ] C#: yolcu sistemi
- [ ] Dokümantasyon, plan güncellemeleri

## Yerelde yapılacaklar (sen + yerel Claude Code)

Her maddedeki "Prompt" kısmını yerel Claude Code oturumuna olduğu gibi yapıştırabilirsin.

### Y1 — Unity projesini oluştur
**Prompt:**
> Repodaki `AnkaraBusSimulator/` klasörü Unity projemiz ama içinde henüz `Packages/` ve `ProjectSettings/` yok. Unity Hub ile repo dışında geçici bir klasörde "Universal 3D (URP)" şablonuyla `AnkaraBusSimulator` adlı yeni proje oluşturmama yardım et. Sonra o projenin `Packages/` ve `ProjectSettings/` klasörlerini repodaki `AnkaraBusSimulator/` içine kopyala, repodaki `Assets/`, `.gitignore` ve `.gitattributes` dosyalarının üzerine yazma. Ardından Unity Hub'da "Add project from disk" ile repodaki klasörü açtır. Project Settings'te Asset Serialization = Force Text, Version Control = Visible Meta Files olsun. Android'e geç: IL2CPP, ARM64, min API 26, Graphics API Vulkan + OpenGLES3. Unity açılıp `Assets/_Project/Scripts` derlendikten sonra derleme hatası varsa düzelt. Oluşan `.meta` dosyaları, `Packages/` ve `ProjectSettings/` dahil commit'le ve kendi branch'ıma push'la. `git lfs install` çalıştırılmış olsun.

### Y2 — Realistic Car Controller'ı import et ✅ (bırakıldı)
RCC Pro Lite denendi; otobüs için gereken ayarlar kilitli olduğundan kendi sürüş kodumuza (`BusVehicle`) geçildi. Ayrıntı: [RCC_NOTLARI.md](RCC_NOTLARI.md).
**Prompt:**
> RCC'yi import ettim. `Assets/` altında nereye kurulduğunu bul, demo sahnelerini ve örnek araçları ayrı tut, gereksiz demo içeriklerinin build'e girmediğinden emin ol. RCC'nin mobil kontrol arayüzünü (dokunmatik gaz/fren/direksiyon) nasıl açacağımızı bul ve `docs/RCC_NOTLARI.md` dosyasına not al. Commit'le, push'la.

### Y3 — İlk sürülebilir otobüs ✅
Yapıldı: prefab, kendi sürüş fiziği, dokunmatik kontroller, kamera, `TestTrack` sahnesi. Unity'de `Scenes/TestTrack` açıp Play ile denenebilir (klavye: W/S, A/D, Space el freni, 1/2/3 = D/N/R, K kapılar).

Eski prompt (referans):
**Prompt:**
> `docs/OTOBUS_BMC_PROCITY.md`'yi oku. `Assets/_Project/Buses/BMC_Procity_12LF/BMC_Procity_12LF.fbx`'ten bir otobüs prefabı kur: materyalleri çıkar ve `_Cam` ile bitenleri saydam yap, 4096'lık dokuları 2048'e sınırla. Prefab ve `BusDefinition` **Ankara Bus → BMC Procity Prefabını Kur** ile kurulur (hazır). Sürüş değerleri `BMC_Procity_12LF.asset` → Physics; değiştirince **Ankara Bus → Sürüş Testi** ile ölç. Kokpit ve dış takip kamerası ekle. `Assets/_Project/Scenes/TestTrack.unity` adında bir test sahnesi oluştur: düz zemin + %10 eğimli 100 m rampa. Dokunmatik kontrolleri (`BusInput`'un Touch* özellikleri ve buton metotları) ve kapı butonunu kur. Commit'le, push'la.

### Y4 — Telefonda test ✅
Yapıldı: TestTrack ve Hat 1 ölçüldü, sonuçlar [PERFORMANS.md](PERFORMANS.md). Tekrar ölçmek için: **Ankara Bus → Android → Performans APK'sı**.

Eski prompt (referans):
**Prompt:**
> `TestTrack` sahnesini Android APK olarak build al. Telefonuma (USB hata ayıklama açık) yükle, Unity Profiler'ı telefona bağla. FPS, draw call ve bellek değerlerini `docs/PERFORMANS.md` dosyasına yaz. Commit'le, push'la.

### Y5 — Haritayı sahneye kur (hazır yerleşimle)
**Prompt:**
> `docs/HARITA_TASARIMI.md`'yi oku. `Assets/_Project/Scenes/Hat1_KizilayAtakule.unity` adında yeni bir sahne oluştur ve Unity menüsünden **Ankara Bus → Hat 1 Haritasını Kur**'u çalıştır (`Scripts/Editor/HaritaKurucu.cs`). Konsolda "Model bulunamadı" uyarısı varsa nedenini bul. Bir bina ve bir yol parçasında ön yönün doğru olduğunu kontrol et (bina ön cephesi yola, yol parçaları birbirine bitişik). Hata varsa düzeltip `docs/MODEL_KITLERI.md`'deki eksen notunu güncelle. Y3'te kurulan otobüs prefabını `OtobusBaslangic` noktasına koy, etiketini `Player` yap, `RouteTracker`'ın Route alanına `Hat_1` objesini bağla, `BusHud` ekle. Trafiğin (`Trafik` objesi, bkz. `docs/TRAFIK.md`) çalıştığını kontrol et. Directional Light'ı ayarla, ışığı ve occlusion culling'i bake et. Play modunda Kızılay'dan Atakule'ye sür ve durakların tamamlandığını doğrula. Commit'le, push'la.

### Y6 — (İsteğe bağlı) Blender'da elle rötuş
Bulutta üretilen modelleri senin Blender'ında Blender MCP ile beğenine göre düzeltmek istersen yerel oturum bunu yapabilir. Bulut tarafındaki üretim scriptleri `tools/blender/` altında olacak; aynı scriptleri yerelde de çalıştırabilirsin.
