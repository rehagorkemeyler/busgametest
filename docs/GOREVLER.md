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
- [x] Hat 1 yerleşimi + Unity editör kurucusu (`harita_hat1.py`, `HaritaKurucu.cs`)
- [x] Trafik araçları (taksi, klasikler, dolmuş) + C# trafik (şerit, araç, spawner %30 taksi) → [TRAFIK.md](TRAFIK.md)
- [x] Yolcular: 10 low-poly model + iniş/biniş sistemi → [YOLCULAR.md](YOLCULAR.md)
- [x] Trafik: Kuğulu kavşağında dönüşler ve trafik ışıkları → [TRAFIK.md](TRAFIK.md)
- [x] Hat 2: Kızılay → Sıhhiye → Opera → Ulus (yerleşim, 4 yeni simge yapı) → [HAT2_KIZILAY_ULUS.md](HAT2_KIZILAY_ULUS.md)
- [x] Otobüs sesleri: motor (devir katmanları, iç/dış), retarder, fren, kapılar, korna, kentkart → [SESLER.md](SESLER.md)
- [x] Puan/bilet sistemi: Kentkart, durak hassasiyeti, konfor, kırmızı ışık, hız, çarpışma, sefer özeti → [PUANLAMA.md](PUANLAMA.md)
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

### Y5 — Haritayı sahneye kur (hazır yerleşimle) ✅
Yapıldı (8 Ekim 2026):
- Sahne: **Ankara Bus → Hat 1 Sahnesini Kur** (`Hat1SahneKurucu`): harita (499 obje, eksik model yok), otobüs `OtobusBaslangic`'ta, `Player` etiketi, `RouteTracker` → `Hat_1`, sol üstte `BusHud` (TextMeshPro).
- Yön kontrolü: bina cepheleri yola bakıyor, yol parçaları boşluksuz; `MODEL_KITLERI.md` notu güncellendi.
- Işık ve occlusion: **Ankara Bus → Hat 1 Işık ve Occlusion Bake** (`HatIsikBake`): Mixed güneş, Subtractive (bina/yol gölgeleri lightmap'te, gerçek zamanlı gölgeyi yalnızca otobüs ve trafik verir), 2 lightmap (2048), yol boyunca ışık probları, occlusion (örtücü: binalar ve simge yapılar). Sahne yeniden kurulursa bake tekrar çalıştırılmalı.
- Sürüş doğrulaması: **Ankara Bus → Hat 1 Sürüş Testi (otomatik pilot)** (`SurusTestiEditor` + `OtomatikPilot`). Editor'de 264 sn, Galaxy S24 FE'de 265 sn: 5 durağın hepsi tamamlandı, takılma yok. Trafik: 24 araç, otobüsün 80 m çevresinde 2–13 hareketli araç, pilot öndeki araç için yavaşladı. Telefon için **Ankara Bus → Android → Sürüş Testi APK'sı**.

Eski prompt (referans):
**Prompt:**
> `docs/HARITA_TASARIMI.md`'yi oku. `Assets/_Project/Scenes/Hat1_KizilayAtakule.unity` adında yeni bir sahne oluştur ve Unity menüsünden **Ankara Bus → Hat 1 Haritasını Kur**'u çalıştır (`Scripts/Editor/HaritaKurucu.cs`). Konsolda "Model bulunamadı" uyarısı varsa nedenini bul. Bir bina ve bir yol parçasında ön yönün doğru olduğunu kontrol et (bina ön cephesi yola, yol parçaları birbirine bitişik). Hata varsa düzeltip `docs/MODEL_KITLERI.md`'deki eksen notunu güncelle. Y3'te kurulan otobüs prefabını `OtobusBaslangic` noktasına koy, etiketini `Player` yap, `RouteTracker`'ın Route alanına `Hat_1` objesini bağla, `BusHud` ekle. Trafiğin (`Trafik` objesi, bkz. `docs/TRAFIK.md`) çalıştığını kontrol et. Directional Light'ı ayarla, ışığı ve occlusion culling'i bake et. Play modunda Kızılay'dan Atakule'ye sür ve durakların tamamlandığını doğrula. Commit'le, push'la.

### Y6 — (İsteğe bağlı) Blender'da elle rötuş
Bulutta üretilen modelleri senin Blender'ında Blender MCP ile beğenine göre düzeltmek istersen yerel oturum bunu yapabilir. Bulut tarafındaki üretim scriptleri `tools/blender/` altında olacak; aynı scriptleri yerelde de çalıştırabilirsin.

### Y7 — Hat 1'i güncelle ve Hat 2 sahnesini kur ✅
Yapıldı (8 Ekim 2026):
- Sahneler **Ankara Bus → Hat 1 / Hat 2 Sahnesini Kur** (`HatSahneKurucu`) ile sıfırdan kuruldu (yolcular, kavşak dönüşleri ve ışıklar dahil), **Hat 1 / Hat 2 Işık ve Occlusion Bake** (`HatIsikBake`) ile bake edildi. Hat 2 Build Settings'te. HUD'da yolcu sayısı.
- Otobüs prefabı yeniden kuruldu (sesler, puanlama, puan göstergesi); sürüş testi değerleri değişmedi.
- Sürüş doğrulaması (**Ankara Bus → Hat 1 / Hat 2 Sürüş Testi**, otomatik pilot):
  - **Hat 1:** 333 sn, 5 durak, sorun yok. Yolcular her durakta bekliyor, iniyor ve biniyor (toplam 31 binen). Kuğulu ışığı 49 kez değişti, kırmızıda bekleyen araçlar görüldü; dönüşler: bulvardan Cinnah'a sağa 5, Cinnah'tan Kızılay'a sola 2. Puan 437, 4 yıldız, kırmızı ışık/durak atlama/çarpışma yok.
  - **Hat 2:** 216 sn, 4 durak, sorun yok. 19 binen, 19 inen; Ulus'ta herkes indi. Puan 276, 3 yıldız (1 çarpışma: pilot yalnızca kendi güzergâhındaki araçlara bakıyor).
- Düzeltilenler: son durakta yolcular biniyordu (hat sonunda otobüs dolu kalıyordu) → son durakta biniş yok. Otomatik pilot Kuğulu kavşağında karşı şeritte bekleyen araçlarla kilitleniyordu → yalnızca kendi güzergâhındaki araçlar için yavaşlıyor. Unity 6'da eskimiş `Find*` çağrıları güncellendi.

Eski prompt (referans):
**Prompt:**
> `git pull origin Ozan` yap. Bulut tarafı yolcuları (`docs/YOLCULAR.md`), Kuğulu kavşağında dönüşleri ve trafik ışıklarını (`docs/TRAFIK.md`) ve Hat 2'yi (`docs/HAT2_KIZILAY_ULUS.md`) ekledi. (1) Hat 1 sahnesinde haritayı **Ankara Bus → Hat 1 Haritasını Kur** ile yeniden kur (eski `Hat1_KizilayAtakule` objesini sil); otobüsü yeniden bağla, ışıkları yeniden bake et. Kızılay'dan Atakule'ye sür: yolcular duraklarda bekliyor ve biniyor mu, Kuğulu kavşağında ışıklar dönüyor ve araçlar kırmızıda duruyor mu, sağa/sola dönen araçlar var mı kontrol et. Derleme hatası varsa düzelt. (2) Yeni `Hat2_KizilayUlus.unity` sahnesinde **Ankara Bus → Hat 2 Haritasını Kur**'u çalıştır, otobüsü bağla (`RouteTracker` → `Hat_2`), bake et, Build Settings'e ekle ve Kızılay'dan Ulus'a sür. Sorunları düzeltip commit'le, push'la.

### Y8 — Sesleri ve puan sistemini dene ✅
Yapıldı (8 Ekim 2026):
- Ses düzeyleri ölçülerek dengelendi, fren bırakma sesi düzeltildi ([SESLER.md](SESLER.md)).
- Puanlama: duruşun son anındaki süspansiyon sıçraması sert fren sayılmıyor (10 km/s altı); yan yana şeritlerde kırmızı ışık cezası bir kez ([PUANLAMA.md](PUANLAMA.md)).
- Maliyet: S24 FE Düşük ayarda sesler ve puan arayüzü FPS'i düşürmüyor ([PERFORMANS.md](PERFORMANS.md) Ölçüm 6). A32 ölçümü bekliyor.

Eski prompt (referans):
**Prompt:**
> `git pull origin Ozan` yap. Bulut tarafı otobüs seslerini (`docs/SESLER.md`) ve puan/bilet sistemini (`docs/PUANLAMA.md`) ekledi. Derleme hatası varsa düzelt. **Ankara Bus → BMC Procity Prefabını Kur** çalıştır (sesleri, puanlamayı ve puan arayüzünü de ekler). Hat 1 sahnesinde Play'e bas ve şunları kontrol et: motor sesi rölantide ve gaz verince devirle birlikte yükseliyor mu, kokpit/dış kamera geçişinde ses değişiyor mu, kapı açılınca/kapanınca, fren bırakılınca, el freninde, R'de ve H (korna) ile ses geliyor mu, yolcu binince bip sesi duyuluyor mu. Puan: yolcu binince "+5 Tam bilet" görünüyor mu, sert frende ceza geliyor mu (normal sürüşte gelmemeli; geliyorsa `SeferPuanlama` eşiklerini yükselt), Kuğulu'da kırmızıda geçince −50 alınıyor mu, bir durağı atlayınca hat ilerliyor mu, Atakule'de özet paneli açılıyor mu, TEKRAR çalışıyor mu (sahne Build Settings'te olmalı). Ses düzeyleri birbirine göre kötüyse `OtobusSesKurucu.EngineLayers` ve `BusAudio` içindeki çarpanları ayarla. Telefonda (A32, Düşük ayar) sesler ve puan arayüzü eklenince FPS düşüyor mu ölç, `docs/PERFORMANS.md`'ye not düş. Commit'le, push'la.
