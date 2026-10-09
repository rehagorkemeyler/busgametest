# Otobüsler

Oyunda seçilebilen otobüsler `Resources/OtobusKatalogu` içinde; menüde SEFERE BAŞLA'nın üstündeki **< ad >** ile seçilir
(`OyunSecimi.Otobus`). Hat sahnelerinde BMC durur; başka otobüs seçildiyse `OtobusDegistirici` sahne yüklenince (Start'lardan önce)
onu seçilen prefabla değiştirir ve rota, kamera, dokunmatik kontroller, gösterge bağlantılarını taşır. Menüdeki vitrin de değişir.
Kaynaklar: docs/KREDILER.md.

| Sıra | Otobüs | Klasör | Tam / mobil üçgen | Durum |
|---|---|---|---|---|
| 0 | BMC Procity 12LF | `Buses/BMC_Procity_12LF` | 158 / 124 bin | oyunda |
| 1 | Mercedes-Benz O500M · Caio Millennium II (12,5 m, 2 kapı) | `Buses/Caio_Millennium_II` | 157 / 79 bin | oyunda |
| – | Mercedes-Benz O530G Conecto (18 m, körüklü, 4 kapı) | `Buses/MB_Conecto_G` | 145 / 73 bin | prefab kuruluyor (Y12) |

## Kurulum

- **Ankara Bus → BMC Procity / Caio Millennium / Mercedes Conecto (Körüklü) Prefabını Kur** (komut satırı: `OtobusKurucu.BuildAllBatch`).
  `OtobusKurucu` her otobüs için bir `Tanim` tutar (klasör, kaplamalar, gövde kutusu, fizik farkları); sürüş değerleri ilk kurulumda
  BMC'nin `BusDefinition`'ından kopyalanır, modele özgü olanlar üzerine yazılır. Kurulum kataloğu da günceller.
- Modeller: `tools/blender/millennium_donustur.py` (OMSI .blend'i; `--disable-autoexec` ile açın), `tools/blender/conecto_donustur.py` (Sketchfab GLB).
  İkisi de `bmc_donustur.iki_tarafli_yap` ve `shadow_proxy` kullanır; `--mobil` Düşük/Normal modelini, seçeneksiz hali `_TamKalite`'yi üretir.

## Caio Millennium II

- Parçalar BMC adlarıyla: `Govde`, `Teker_OnSol/OnSag/ArkaSol/ArkaSag`, `Kapi_1_1..Kapi_2_2` (sağ, kanatlı; menteşe kapı boşluğunun dış kenarında),
  `Direksiyon`, `Lamba_KisaFar/UzunFar/Fren/Geri/Park/SinyalSol/SinyalSag/Ic1/Ic2`. Soldaki BRT kapıları gövdeye katıldı, kapalı.
- OMSI modelinde dokular malzemeye bağlı değildi; `DOKULAR` tablosu malzeme adından doku dosyasını bulur.
- Kaplama: kaynaktaki beyaz şablon (`base.bmp`) boyanır → `Textures/caroserie.png` (EGO kırmızı), `Kaplamalar/ego_mavi.png`, `Kaplamalar/ozel_halk.png`
  (renkler `tools/kaplama.py` ile aynı). EGO logoları, kurdele, filo numarası (kırmızı EGO 22-353, mavi EGO-12-504) ve plaka
  (06 CUM 353; `bagulhosmep.png` içinde, 90° dönük; ön ve arka plaka aynı bölgeyi kullanır) `tools/kaplama_millennium.py` ile çizilir
  (girdi: düz boyalı dokular). Yerleşim: üstteki bant sağ yan (ön dokunun sağında), ikinci bant sol yan.
  Kalan: kaynaktaki Brezilya etiketleri ve "RESERVADO" yazısı.
- Fizik: BMC değerleri + teker yarıçapı 0,525 m, direksiyon 48° (dingil mesafesi 6,6 m; 40° ile Kuğulu → Cinnah dönüşünde kaldırıma takılıyordu).
- Sürüş testi (Editor, otomatik pilot, Hat 1): 5/5 durak, takılma yok, 2 çarpışma (trafik aracı öne/arkaya). BMC aynı testte 0 çarpışma.
  `Unity -batchmode ... -executeMethod AnkaraBus.EditorTools.SurusTestiEditor.RunHat1OtobusBatch -otobus 1`.

## Mercedes O530G Conecto (körüklü)

- `MB_Conecto_G.fbx` (mobil) ve `MB_Conecto_G_TamKalite.fbx`. Prefab: **Ankara Bus → Mercedes Conecto (Körüklü) Prefabını Kur**.
- Parçalar (Unity yerel, kök zeminde, ön +Z): `Govde_On`, `Govde_Arka` ve `Koruk` (pivotları mafsalda: (0, 1.0, −1.57)),
  `Teker_OnSol/OnSag` (z +6.15), `Teker_OrtaSol/OrtaSag` (z +0.26, ön gövdenin arka aksı), `Teker_ArkaSol/ArkaSag` (z −5.87, çeken aks),
  teker yarıçapı 0,51 m; mobilde `Golge_On`, `Golge_Arka`. Boy −9,28 … +8,92.
- Kaplama: yan yüzlerde düzlemsel UV (`Textures/caroserie.png`, 2048×512; u = boy, **üst yarı sağ yan, alt yarı sol yan** aynalı).
  `tools/kaplama_conecto.py` cam bandı, sağdaki 4 kapı, etek, bel şeridi, EGO paneli/kurdele ve filo numarasını (EGO 22-501 / EGO-12-601)
  çizer → `caroserie.png` (kırmızı), `Kaplamalar/ego_mavi.png`, `Kaplamalar/ozel_halk.png`. Dokusuz ön/arka yüz (`M_Govde`) kaplamaya
  göre `BusLivery` renk tonuyla boyanır (`Livery.color`, `colorMaterialName`). Plaka yok (ön/arka yüzde UV yok).
- İki gövde (`KorukluOtobus`): kök = ön gövde (BusVehicle'ın Rigidbody'si); `ArkaGovde` ayrı Rigidbody, mafsalda `ConfigurableJoint`
  (konum kilitli, sapma ±52°, eğilme ±10°, yatma ±4°, sönümlü). Oyun başlayınca sahne köküne alınır; arka görseller (`Govde_Arka`,
  `Golge_Arka`) her karede ona taşınır, `Koruk` mafsal açısının yarısı kadar döner. Kütle `trailerMassRatio` (0,4) ile bölünür.
  Teker: ön aks + orta aks (ön gövde, `passive` = tahriksiz) + arka aks (arka gövde, çeken). `BusVehicle` her aksın viraj
  denge çubuğunu kendi gövdesine uygular. Arka gövdenin çarpışmaları puanlamaya `KorukluOtobus.ArkaCarpti` ile gelir.
- 4 kapı: kapı parçası yok; sağda sanal kapı noktaları (z 7.78 / 1.89 / −4.23 / −7.43; arka ikisi arka gövdede), kapasite 150.
  Motor sesi arkada (0, 1, −8.2). Direksiyon 45°, kütle 18 t. Kokpit kamerası: `SurucuGozu` (modelde direksiyon yok).
- Uzunluğa bağlı kodlar `OtobusOlcusu` ile gövde kutularından (`Carpisma_*`) ölçülür: puanlamada ön uç, `YayaGecitleri` ön uç ve
  çarpma kutusu, kapı kamerası ve takip mesafesi (`BusCameraRig`), otomatik pilotta ön uç ve dingil mesafesi.

## Y12 sonuçları (9 Ekim, Editor, otomatik pilot, Hat 1, 3× hız)

| Otobüs | Durak | Çarpışma | Puan | Not |
|---|---|---|---|---|
| Caio Millennium II | 5/5 | 0 (önceden 2) | 189–230 | boyu ölçen otomatik pilotla temiz |
| Mercedes Conecto | 3/5 | 2–5 | – | Kuğulu → Cinnah'ta takılıyor (aşağıda) |

**Düzeltilen: Conecto hiç hareket etmiyordu.** Tam gazda motor devir sınırında, arka (çeken) tekerler boşa dönüyor, otobüs yerinde duruyordu.
Sebep PhysX'in "yapışkan teker" kuralı: düşük hızda tahrik torku olmayan bir gövdenin tekerleri yere yapıştırılır; ön gövdenin dört
tekerinin (ön + orta aks) hiçbiri çekmediği için ön gövde kilitleniyor, arka gövde onu itemiyordu (mafsal kaldırılınca arka gövde tek başına
1 sn'de 5,7 m/s'ye çıktı). `BusVehicle.ApplyWheelTorques` artık çekmeyen tekerlere 0,0001 Nm veriyor (`SerbestTork`); BMC ve Millennium etkilenmedi.

**Düzeltilen: Conecto ön/arka yüz kaplama renginde değildi** (ön gri, arka beyaz). `conecto_donustur.py` dünya dönüşümünden sonra yüz
normallerini güncellemiyordu (kaynak kökünde Y-yukarı → Z-yukarı); normalin y'sine bakan kurallar (ön/arka yüz, camlar, tavan) yanlış
yüzleri seçiyordu. `bm.normal_update()` eklendi; ön/arka yüz (normal yönünden bağımsız) cam bandı koyu cam, gerisi `M_Govde`.
Ön tampon çevresinde birkaç küçük üçgen kaplama renginde kalıyor (kaynak model).

**Açık: Kuğulu → Cinnah.** Kuğulu durağından çıkıp sağa dönerken ön gövdenin sol ön köşesi sol şeritteki bir araca sürtünüyor
(`[Puan] çarpışma: Klasik_…, otobüste (-1.3, 1.5, 9.0)`), araç otobüsü köşedeki binaya (A3_Cankaya_5Kat) itiyor ve otobüs takılıyor.
İki koşuda da aynı yerde. Otomatik pilotun cepten çıkışta yan şeridi kontrol etmesi ya da 18 m için dönüşe daha geç/daha geniş girmesi gerekiyor;
trafik aracının da otobüsün ön ucuna yol vermesi. Mafsal ve körük bu koşularda kararlıydı (kopma/titreme kaydı yok).

**Görsel kontrol (Editor, `OtobusOnizleme.CizBatch -cikti klasör`):** Millennium üç kaplamada EGO logosu, filo numarası (EGO 22-353) ve
plaka (06 CUM 353, önde ve arkada) doğru ve okunur, sol yandaki yazılar ters değil. Conecto: sağda 4 kapı, solda kapı yok, yazılar ters değil;
ön/arka yüz kaplama renginde. İçeriden (yolcu kamerası) Conecto'da yan duvarın alt kısmında kaplama şeridi görünüyor (iç yüze de kaplama düşüyor), küçük.
