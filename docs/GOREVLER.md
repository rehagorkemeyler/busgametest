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
- [x] Ana menü: hat/kaplama/görüntü seçimi, en iyi puanlar, döner otobüs vitrini → [ANA_MENU.md](ANA_MENU.md)
- [x] Oyun içi kamera: sürükleyerek 360° bakış, pinch yakınlaştırma; DIŞ / KOKPİT / YOLCU / KAPI / SERBEST → [KAMERA.md](KAMERA.md)
- [x] Yol gösterici: güzergâh şeritlerden otomatik, mini harita, kalan mesafe, dönüş uyarısı, büyük harita → [YOL_GOSTERICI.md](YOL_GOSTERICI.md)
- [x] Gündüz/akşam/gece (ayrı bake, yanan pencereler ve lambalar, otobüs farları) ve yağmur → [HAVA_ZAMAN.md](HAVA_ZAMAN.md)
- [x] Trafik: sollama, korna, duraklarda dolmuş; yaya geçitleri ve karşıya geçen yayalar → [TRAFIK.md](TRAFIK.md)
- [x] SketchUp modelleri: gerçek Kızılay AVM, Emek İşhanı, Güvenlik Anıtı (Güvenpark), nostaljik refüj lambası, Kızılay blokları (ikinci sıra) → `Maps/SKP/` ([MODEL_KITLERI.md](MODEL_KITLERI.md#sketchup-modelleri-mapsskp))
- [x] Düzeltmeler (9 Ekim): yolcu iniş-binişi bitince durak tamamlanır (atlandı cezası gelmiyordu), otobüs gövdesindeki delikler, zemin çimden betona, binalar/simge yapılar/duraklara çarpışma
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

### Y9 — Ana menüyü ve yeni kamerayı kur, dene
**Prompt:**
> `git pull origin Ozan` yap. Bulut tarafı ana menüyü (`docs/ANA_MENU.md`) ve yeni oyun içi kamerayı (`docs/KAMERA.md`) ekledi. Derleme hatası varsa düzelt. Sırayla: **Ankara Bus → BMC Procity Prefabını Kur** (BusLivery'ye yeni alan eklendi), **Ankara Bus → Ana Menü Sahnesini Kur**. Build Settings'te sıranın AnaMenu, Hat 1, Hat 2 olduğunu kontrol et (TestTrack kalabilir ama sona). Hat sahneleri prefab örneğini kullandığı için yeniden kurulmaları gerekmiyor; otobüste `BusLivery.useMenuChoice` açık görünmüyorsa sahnedeki örneği prefabla eşitle. AnaMenu'de Play'e bas ve kontrol et: otobüs podyumda dönüyor ve ekranın sağ yarısında mı (16:9 ve 20:9 Game görünümünde), parmak/fareyle çevrilebiliyor mu, kaplama düğmeleri vitrindeki otobüsü değiştiriyor mu, Yüksek seçince tam kalite model geliyor mu, arka plan (Atakule, apartmanlar) düzgün mü (bina ön cepheleri merkeze bakmalı; bakmıyorsa `AnaMenuKurucu.Place` dönüşünü düzelt), SEFERE BAŞLA seçili hattı seçili kaplamayla açıyor mu, AYARLAR → ANA MENÜ ve hat sonu MENÜ düğmesi menüye dönüyor mu, en iyi puan kartta görünüyor mu. Görünüm kötüyse (yerleşim, kamera, ışık) düzelt. Kamera: Hat 1'de KAMERA düğmesi DIŞ → KOKPİT → YOLCU → KAPI → SERBEST sırasıyla geçiyor mu ve düğmede adı yazıyor mu; boş ekranı (Editor'de fareyle) sürükleyince dış görünümde otobüsün çevresinde 360° dönülüyor mu, otobüs giderken arkaya geri dönüyor mu, kokpitte etrafa bakılıp bırakınca yola dönülüyor mu; direksiyon/pedal/düğmeye basarken kamera dönmüyor mu (telefonda bir parmak direksiyondayken öbürüyle sürükle); pinch ve çift dokunuş çalışıyor mu. YOLCU ve KAPI kameralarının konumu tahmini: yolcu kamerası koltuk/direk içinde ya da tabanın altındaysa `BusCameraRig.interiorPosition`'ı, kapı kamerası kapıları ve kaldırımı iyi göstermiyorsa `doorPosition`/`doorLookAt`'ı düzelt (otobüsün yerel koordinatı; sahnelerdeki kameraya da işle). **Ankara Bus → Android → Oyun APK'sı** ile build alıp telefonda menüden iki hattı da aç. Menü ekranından bir ekran görüntüsünü `docs/gorseller/ana_menu.png` olarak ekle. Commit'le, push'la.

### Y10 — Yol gösterici, gece/yağmur, yayalar ve trafik ✅
**Prompt:**
> `git pull origin Ozan` yap. Bulut tarafı yol göstericiyi (`docs/YOL_GOSTERICI.md`), gündüz/akşam/gece ve yağmuru (`docs/HAVA_ZAMAN.md`), yayaları ve yeni trafik davranışlarını (`docs/TRAFIK.md`, sonundaki iki bölüm) ekledi; menüye ZAMAN · HAVA satırı geldi. Burada derlenemedi: önce derleme hatalarını düzelt. Sonra sırayla: **Ankara Bus → BMC Procity Prefabını Kur** (RotaRehberi + MiniHarita eklendi), **Ankara Bus → Ana Menü Sahnesini Kur** (Atakule otobüsün arkasına alındı), **Ankara Bus → Hava ve Gece Malzemelerini Kur**, **Ankara Bus → Akşam ve Gece Işığını Bake Et → Hat 1** ve **→ Hat 2** (her biri 3 bake; sonunda gündüz yeniden bake edilir). Kontrol et:
> (1) Yol gösterici: Hat 1 ve Hat 2'de sarı güzergâh doğru mu (Hat 1'de Kuğulu'dan Cinnah'a sağa dönüyor mu), mini harita otobüsle dönüyor mu, bilgi bandında durak/mesafe ve "… m sonra SAĞA" doğru zamanda çıkıyor mu, rotadan çıkınca uyarı geliyor mu, dokununca büyük harita açılıp kapanıyor mu; EL FRENİ/KAPILAR ve mini harita üst üste binmiyor mu. Menüde Atakule otobüsün üstünde görünüyor mu.
> (2) Akşam ve gece: binalar karanlık, sokak lambalarının altı aydınlık mı, pencereler ve lamba başları yanıyor mu, otobüs farı/iç ışıkları yanıyor mu, fren lambası frende yanıyor mu, gece de puan/yolcu/trafik çalışıyor mu. Yağmurlu: damlalar, ıslak parlak yol, gri gök/sis, ses (kokpitte kısık). Görüntü kötüyse `ZamanAyarlari` ve `HavaZamanKurucu` değerlerini ayarla (değişiklik bake'i gerektirir).
> (3) Trafik: duraklardan sonra zebra çizgileri doğru yerde mi (yolun üstünde, refüjde yok), yayalar karşıya geçiyor mu, araçlar yaya varken duruyor mu, yayaya çarpınca/yol vermeyince ceza geliyor mu; otobüs şeritte durunca arkadaki araçlar solluyor mu ya da korna çalıyor mu; dolmuşlar duraklara yanaşıp devam ediyor mu, kaldırıma çıkmıyor mu.
> Telefonda (A32 Düşük ve S24 FE Yüksek) gece+yağmurlu Hat 1'de FPS ölç, `docs/PERFORMANS.md`'ye yaz; gerekirse yağmur parçacık sayısını düşür. Menüden gece ve yağmurlu bir ekran görüntüsünü `docs/gorseller/gece_yagmur.png` olarak ekle. Commit'le, push'la.

### Y11 — Toparlama: derle, telefonda dene, ölç, APK
Y7'den beri çok şey eklendi (yol gösterici, gece/yağmur, yayalar, trafik, SketchUp modelleri) ama telefonda birlikte hiç denenmedi.
**Prompt:**
> `git pull origin Ozan` yap. Projeyi Unity'de aç; derleme hatası varsa düzelt. Unity açılınca `Resources/Hava` kendiliğinden kurulur (gece/ıslak malzemeler, yağmur prefabı); oluşan dosyaları commit'e kat. **Ankara Bus → Ana Menü Sahnesini Kur** çalıştır (Atakule arka planı). Otobüs prefabını yeniden kurmak gerekmez: mini harita oyunda kendiliğinden eklenir, ama **Ankara Bus → BMC Procity Prefabını Kur** yaparsan da olur.
> Editor'de menüden Hat 1 ve Hat 2'yi Gündüz/Açık ve Gece/Yağmurlu olarak birer kez aç, 1–2 dakika sür: konsolda kırmızı hata olmasın; mini harita, sarı güzergâh, "… m sonra SAĞA", gece pencereleri/lamba parlamaları, otobüs farları, yağmur, zebra çizgileri ve karşıya geçen yayalar, sollayan/korna çalan araçlar, durağa yanaşan dolmuşlar görünsün. Bozuk olanı düzelt (düzeltemediğini `docs/GOREVLER.md`'ye yaz).
> **Ankara Bus → Android → Oyun APK'sı** ile APK al, `~/Desktop/AnkaraOtobus.apk`'ye kopyala. A32 (Düşük) ve S24 FE (Yüksek) telefonlarda Kızılay ve Kuğulu'da dış ve kokpit kamerasıyla, gündüz ve gece+yağmurda FPS ölç (yeni SketchUp binaları ve lambalarla), `docs/PERFORMANS.md`'ye yeni bir ölçüm olarak yaz. A32 Düşük'te 30 FPS'in altına düşen yer varsa nedenini bul (Profiler) ve en ucuz çözümü uygula (ör. yağmur parçacık sayısı, Emek İşhanı/lamba LOD, gölge mesafesi). Gece+yağmurlu bir menü ve bir sürüş ekran görüntüsünü `docs/gorseller/` altına ekle. Y10 ve Y11'i ✅ işaretle, sonuçları özetle. Commit'le, push'la.

**Sonuç (9 Ekim 2026):** Derleme hatası yok (Y10'daki `shadowRadius` uyarısı düzeltildi), `Resources/Hava` kuruldu, ana menü yeniden kuruldu. APK `~/Desktop/AnkaraOtobus.apk` (135 MB).
S24 FE'de gece + yağmurda Hat 1 → Hat 2 otomatik pilotla sürüldü (`Y10Gozlemci`): gece lightmap'i, farlar, iç lambalar, lamba parlamaları, yağmur (1100 parçacık) ve sis,
sarı güzergâh, "220 m sonra SAĞA", mini harita ve büyük harita, 4+3 yaya geçidi, 21 yaya geçişi (araçlar duruyor), korna, dolmuş yanaşma (kaldırıma çıkan yok) çalışıyor.
Düzeltilen: build'de kırpılan `SphereCollider` yüzünden lamba parlamasında çıkan kırmızı hata (`link.xml`); gözlemcinin yanlış alarmları.
FPS: S24 FE Yüksek her yerde 60 (docs/PERFORMANS.md, Ölçüm 7). Görüntüler: `docs/gorseller/gece_yagmur.png`, `gece_yagmur_surus.png`.
**Açık kalanlar:** A32 (Düşük) ölçümü — telefon bağlı değildi; Kızılay'da draw call SketchUp binalarıyla 279 → 643 oldu, A32'de 30 altı olası.
Menünün arka planı gece/yağmur seçilince değişmiyor (menü sahnesi sabit ışıklı). Otomatik pilotlu testler artık 3× hızlı (Editor `-hiz N`, telefonda intent `hiz`).
Ek: Mercedes otobüsleri (docs/OTOBUSLER.md, docs/KREDILER.md).


### Y12 — Düzeltmeleri al ve dene
Bulut tarafı şunları düzeltti/ekledi: (M) Millennium'a logo, filo numarası ve plaka; Mercedes Conecto körüklü otobüs (kaplama, iki gövdeli fizik, 4 kapı) ve otobüs boyuna göre ölçülen kodlar (docs/OTOBUSLER.md), (0) motor güçlendirildi (düzde 85, yokuşta 50–60 km/s; `docs/OTOBUS_BMC_PROCITY.md` sonu), telefonu eğerek direksiyon (AYARLAR → DİREKSİYON; `docs/KAMERA.md` sonu), (1) yolcu indirip bindirince durak tamamlanıyor (önceden kapı hemen kapanınca "atlandı" −100 geliyordu), (2) otobüs gövdesindeki delikler (iki FBX yeniden üretildi, parça adları aynı), (3) boş zemin çimden betona (iki zemin FBX'i, geometri aynı; bake gerekmez), (4) binalar, simge yapılar ve duraklar için çarpışma aracı.
**Prompt:**
> `git pull origin Ozan` yap, Unity'de derleme hatası varsa düzelt. **Ankara Bus → Hat Sahnelerine Çarpışma Ekle**'yi çalıştır (Hat 1 ve Hat 2'yi açıp binalara kutu, simge yapılar ve durak çatılarına MeshCollider ekler, kaydeder; bake gerekmez). Otobüs prefabını yeniden kurmak gerekmez (FBX'ler yerinde güncellendi), ama tam kalite modelde sorun görürsen **Ankara Bus → BMC Procity Prefabını Kur** yap. Kontrol et: menüde ve oyunda otobüs gövdesinde delik kalmadı mı (özellikle arka teker ile orta kapı arası, kapı camları; içeriden kokpit ve yolcu kamerasıyla da bak, içeride eksik bir şey çıkmadı mı); Hat 1'de bir durakta kapıyı açıp yolcular inip binince durağın tamamlandığı ("… durağı +puan") ve kapıyı hemen kapatıp kalkınca "atlandı" cezası gelmediği; zemin artık yeşil değil mi; otobüs binaya, Atakule'ye ve durak çatısına girmiyor ve çarpınca ceza geliyor mu; dış kamera binaların içine girmiyor mu. **Ankara Bus → Sürüş Testi** ile yeni motoru ölç: düzde azami hız ~85, Cinnah yokuşunda (%8–12) tam gazla kaç km/s, vites arıyor mu (yokuşta sürekli büyütüp küçültmesin; ararsa `upshiftRpm`/`downshiftRpm` ayarla), kalkışta patinaj/devrilme var mı; sonuçları `docs/OTOBUS_BMC_PROCITY.md`'deki tabloya yaz. **Otobüsler:** **Ankara Bus → BMC Procity / Caio Millennium / Mercedes Conecto (Körüklü) Prefabını Kur**'u sırayla çalıştır (BusVehicle ve kurucu değişti; Conecto ilk kez kuruluyor, katalog güncellenir). Menüde Conecto seçilebiliyor mu, Krediler'de Conecto satırı var mı. Millennium'da üç kaplamada EGO logosu, filo numarası ve plaka (06 CUM 353) doğru yerde ve okunur mu (ters/kesik değil). Conecto: üç kaplama yanlarda doğru mu (sağda 4 kapı, solda kapı yok; yazılar sol yanda ters değil mi), ön/arka yüz kaplama renginde mi. Sürüş: kalkışta ve frende mafsal titremiyor/kopmuyor mu, körük dönüşte yarı açıda mı, arka gövde kaldırıma/yola gömülmüyor mu; Kuğulu → Cinnah dönüşü ve Cinnah yokuşu (çeken aks arkada — patinaj, yokuşta hız). Otomatik pilotla `RunHat1OtobusBatch -otobus 2` (Conecto) ve `-otobus 1` (Millennium; önceden 2 çarpışma vardı, otomatik pilot artık boyu ölçüyor) çalıştır, durak/çarpışma sonuçlarını `docs/OTOBUSLER.md`'ye yaz. Mafsal kararsızsa önce `OtobusKurucu.SetupArkaGovde` sönümlerini, sonra `trailerMassRatio`/ağırlık merkezini ayarla. Kapı kamerası ve yolcuların 4 kapıdan inip binmesi; arka gövdeyle bir şeye çarpınca ceza geliyor mu. Telefonda AYARLAR → DİREKSİYON → TELEFONU EĞ'i dene: telefonu sağa çevirince sağa dönüyor mu (ters ise `KontrolAyarlari.EgimAcisi` yönünü düzelt), ORTALA çalışıyor mu, ekrandaki direksiyon gizleniyor mu, hassasiyet uygun mu. Sorunları düzeltip `docs/GOREVLER.md`'ye yaz. **Ankara Bus → Android → Oyun APK'sı** ile yeni APK'yı `~/Desktop/AnkaraOtobus.apk`'ye koy. Commit'le, push'la.

**Sonuç (9 Ekim 2026, Editor; telefon bağlı değildi):** Derleme hatası yok (yalnızca eskimiş API uyarıları, düzeltildi). Hat sahnelerine çarpışma eklendi, üç otobüs prefabı kuruldu, katalogda 3 otobüs; Krediler'de Conecto satırı var.
- **Motor:** düzde 85; %8/%10/%12 yokuşta BMC 57/46/40, Conecto 48/41/35 km/s; kalkışta geri kaçma ve devrilme yok (docs/OTOBUS_BMC_PROCITY.md).
  **Düzeltildi:** kalkışta patinajın yol açtığı 1→2→1 vites titremesi (vites kararı artık yere göre hızdan) ve yokuşta kickdown sonrası vites arama.
  %12'de 3↔4 arasında yavaş bir geçiş kaldı (40 sn'de 3 değişim).
- **Conecto:** hiç hareket etmiyordu (PhysX "yapışkan teker": ön gövdenin hiçbir tekeri çekmediği için kilitleniyordu) → **düzeltildi** (`BusVehicle.SerbestTork`).
  Ön/arka yüz kaplama renginde değildi → **düzeltildi** (`conecto_donustur.py` normal güncellemesi). Otomatik pilotta 3/5 durak; **Kuğulu → Cinnah'ta sol
  şeritteki araca sürtünüp binaya itiliyor, takılıyor (açık, docs/OTOBUSLER.md)**. Mafsal/körük koşularda kararlı.
- **Millennium:** 5/5 durak, 0 çarpışma (önceden 2). Üç kaplamada logo, filo numarası ve plaka (06 CUM 353) doğru ve okunur.
- Otobüs binaya çarpınca ceza geliyor (otomatik pilot kaydında `[Puan] çarpışma: A3_Cankaya…`), durak yolcu inip binince tamamlanıyor.
- Hat değişiminde yeni otobüs eskisinin önüyle hizalanıyor (uzun otobüs geriye uzar).
- Yeni araçlar: `OtobusOnizleme.CizBatch` (her otobüs × kaplama, dış ve iç görüntü PNG), `OtobusSurusTesti -otobus N` (körüklü dahil), otomatik pilot takılınca temas,
  teker yükü/kayma/tork ve gövde durumu yazar.
- **Bekleyen (telefon):** eğerek direksiyon (yön, ORTALA, gizlenen direksiyon, hassasiyet), kapı kamerası, 4 kapıdan yolcu, oyunda gövde delikleri ve kokpit/yolcu kamerası,
  zemin rengi, dış kameranın binaya girmesi, "atlandı" cezası senaryosu, A32 ölçümü.


### Y13 — İç ve dış kaplamalar
Bulut tarafı: Conecto'nun iç mekânı (koltuklar, sarı borular, kaymaz zemin, iç duvar ve tavan; içeride kaplama rengine boyanan zemin
ve kırmızı şerit düzeldi), ön/arka yüzü (düzgün ön cam, hat tabelası, plaka, stop lambaları, motor ızgarası; her kaplamanın kendi dokusu)
ve BMC/Millennium içindeki Rumence/Portekizce etiketler Türkçe (Millennium'da Hat 1 durak şeridi, EGO afişi, Ankarakart, Alo 153), yolcunun Rusça sesi Türkçe ("Durakta inecek var!", docs/SESLER.md).
Ayrıntı: docs/OTOBUSLER.md → "İç ve dış kaplama (Y13)".
**Prompt:**
> `git pull origin Ozan` yap, derleme hatası varsa düzelt. **Ankara Bus → Mercedes Conecto (Körüklü) Prefabını Kur**'u çalıştır (iki Conecto FBX'i yeni malzemelerle güncellendi: `M_Zemin`, `M_Koltuk`, `M_KoltukKabuk`, `M_Direk`, `M_IcDuvar`, `M_IcTavan`, `M_onarka`; kurucu bunları Materials/'a çıkarır, Konsol'da "Tam kalite materyali eşleşmedi" uyarısı olmamalı). `M_Zemin` ve `M_Koltuk` dokularının (ic_zemin.png, ic_koltuk.png) bağlandığını ve Wrap Mode'un Repeat olduğunu kontrol et. `OtobusOnizleme.CizBatch` ile üç otobüsün her kaplamada dış ve iç görüntüsünü al: Conecto'nun önünde düzgün kenarlı ön cam, "ANKARA" tabelası ve plaka, arkasında stoplar/ızgara/plaka var mı ve EGO mavi ile Özel Halk'ta ön/arka yüz kendi renginde mi (kırmızı kalmamalı); içeride lacivert desenli koltuklar, sarı borular, koyu gri zemin, açık duvar/tavan var mı, zemin ya da bölmeler kaplama rengine boyanmış mı, içeriden kırmızı şerit görünüyor mu. BMC içinde etiketler (acil çıkış vanası, kapıya yaslanmayın, klima, tekerlekli sandalye) ve Millennium içinde (özel alan, öncelikli koltuk, hat şeridi, EGO afişi, ANKARAKART okuyucu, ALO 153) Türkçe ve okunur mu. Yanlış sınıflanan parça görürsen (ör. koltuk ayağı sarı, tutamak gri) `tools/blender/conecto_ic.py` eşiklerini not et. Bir durakta yolcu alıp kalktıktan 8–20 sn sonra yolcunun Türkçe "Durakta inecek var!" dediğini duy (önceden Rusçaydı; `stop.wav` aynı adla değişti, ses seviyesi diğer seslere göre uygun mu). Telefonda (S24 FE) Conecto ile kokpit ve yolcu kamerasından bak, FPS'i Y11 ölçümüyle karşılaştır. Görüntüleri `docs/onizleme/` altına koy, sonuçları buraya yaz. APK'yı güncelle (`~/Desktop/AnkaraOtobus.apk`). Commit'le, push'la.
