# Y13 raporu (yerel oturum, 9 Ekim 2026)

Test kullanıcının isteğiyle yarıda durduruldu (Mac kapanıyor). Unity ve adb süreçleri kapatıldı. Kod derleniyor: son derleme
`AndroidBuild.Y11Build` → `Succeeded … 0 hata` (OtobusIsinla.cs dahil). APK bu oturumda yeniden alınmadı.
Görüntüler: `docs/onizleme/y13/`.

## Yapılanlar

1. `git pull` → derleme hatası yok. Benim eklediğim iki dosyadaki eskimiş `FindObjectsSortMode` uyarıları Y12'de düzeltilmişti.
2. **Ankara Bus → Mercedes Conecto (Körüklü) Prefabını Kur**: yeni malzemeler `Materials/`a çıktı (M_Direk, M_IcDuvar, M_IcTavan, M_Koltuk,
   M_KoltukKabuk, M_Zemin, M_onarka). Konsolda **"Tam kalite materyali eşleşmedi" uyarısı yok**.
   `M_Zemin` → ic_zemin.png, `M_Koltuk` → ic_koltuk.png bağlı (guid'ler eşleşiyor); ikisinde de `wrapU: 0` (= Repeat).
3. `OtobusOnizleme.CizBatch` ile üç otobüs × üç kaplama dış görüntü + iç görüntü alındı. Önizleme aracına koridordan yan duvarlara
   bakan beş yakın iç görüntü eklendi (`ic_on_sag`, `ic_on_sol`, `ic_orta_sag`, `ic_orta_sol`, `ic_arka`).
4. **BMC etiketleri (düzeltildi):** `stickers.png`'de yeşil (acil çıkış vanası) ve sarı (kapıya yaslanmayın) etiketin iki kopyası var;
   bulut yalnızca birini Türkçeleştirmişti, model ikisini de kullanıyor → içeride "DO NOT LEAN ON DOOR" ve "EMERGENCY EXIT VALVE" görünüyordu.
   Türkçe paneller İngilizcelerin üzerine kopyalandı. Ayrıca **Düşük/Normal'deki BMC etiketleri `Atlas_BMC.png`'den okuyor ve atlas hiç
   güncellenmemişti** (Rumence/İngilizce: "NU VA SPRIJINITI", "SUPAPA IESIRE"). Güncel stickers.png atlas hücresine yazıldı
   (hücre: 2048'lik doku 1024'lük, sol üst (2068, 4)). Aynı iki adım `tools/etiketler_tr.py`'ye `bmc_ikizleri_ve_atlas` olarak eklendi
   (PIL ile; burada PIL olmadığı için Blender+numpy ile uygulandı, araç burada çalıştırılmadı).
   Görüntüler: `bmc_etiket_eski_ingilizce.png`, `bmc_atlas_etiket_eski.png`, `bmc_atlas_etiket_yeni.png`, `bmc_stickers_yeni.png`.
5. **Yolcu sesi:** `stop.wav` 1,31 sn, tepe −0,2 dBFS, RMS −10,7 dBFS (eski Rusça: 1,26 sn, RMS −8,0). Oyunda
   `BusAudio.cs:242` `masterVolume * (0.15 + 0.25 * interiorBlend)` ile çalıyor: kokpitte ≈ −18,7 dBFS, kapı sesleri (−20…−25 dBFS) ile
   aynı aralıkta, dışarıda ≈ −27 dBFS. Kapı kapandıktan `BusAudio.cs:308` `Random.Range(8, 20)` sn sonra. **Telefonda dinlenmedi / kayıtla
   doğrulanmadı** (aşağıda "bitmeyenler").
6. **Telefon (S24 FE, Wi-Fi adb) — Conecto FPS, Yüksek, gündüz, Hat 1** (Y11 Ölçüm 7 BMC ile karşılaştırma):

   | Durak | dış (Y11 BMC) | kokpit | yolcu | draw dış/kokpit/yolcu |
   |---|---|---|---|---|
   | Kızılay AVM | 59,1 (57,8) | 59,8 | 60,0 | 549 / 552 / 546 (BMC 643) |
   | Meclis | 60 | 60 | 60 | 348 / 356 / 438 |
   | Kuğulu Park | 60 | 60 | 60 | 147 / 181 / 224 |
   | Cinnah | 60 | 60 | 60 | 167 / 165 / 211 |
   | Atakule | 60 | 60 | 60 | 95 / 55 / 98 |

   Kızılay kokpitte CPU 13,5 ms (diğerleri 8–11 ms), GPU ≤ 10,1 ms. Log: `[Otobus] Mercedes-Benz O530G Conecto (körüklü) seçildi`.
   Ölçüm turuna yolcu kamerası eklendi (`PerfBenchmark.cs:69`).

## Bulunan sorunlar

### S1 — Conecto kokpit kamerası sağ ön kapı boşluğunda / ön camın içinde (düzeltildi, telefonda bir kez doğrulandı)
- **Otobüs:** Conecto. **Kamera:** kokpit. **Hat:** Hat 1, Kızılay AVM ve Meclis durakları (her yerde aynı).
- **Ne oluyor:** Görüntünün üst ve sağ yarısını gri bir panel kaplıyor, yol sol alttan görünüyor (`tel_conecto_kokpit_eski.png`,
  `tel_conecto_kokpit_meclis_eski.png`).
- **Tekrarlama:** Menüde Conecto → SEFERE BAŞLA → KAMERA → KOKPİT.
- **Kaynak:** `Editor/OtobusKurucu.cs:603` (eski: `new Vector3(-0.62f, 2.25f, onUc - 1.25f)`). `onUc` Govde_On renderer sınırından
  ölçülüyor; öne taşan aynalar sınırı uzatıyor, göz ön camın neredeyse içinde kalıyordu.
- **Düzeltme:** `(-0.62, 2.05, onUc - 1.9)`. Telefonda: yol, gösterge paneli, vites kolu görünüyor (`tel_conecto_kokpit_yeni.png`).
  Sağ üstte tabela kutusu görüşün bir kısmını hâlâ kapatıyor (küçük).

### S2 — Conecto içinde pencere dikmeleri ve alt bölmeler kaplama renginde (açık)
- **Otobüs:** Conecto (Yüksek = tam kalite model). **Kamera:** yolcu (kokpitte sol A direği de kırmızı). **Hat:** Hat 1, Kızılay AVM.
- **Ne oluyor:** İç pencere dikmeleri ve alt bölmeler kırmızı, sağ üstte mavi bir panel (`tel_conecto_yolcu.png`).
- **Önizleme bunu göstermedi:** `OtobusOnizleme` iç görüntüleri `Resources/…_TamKalite` görselinden alıyor; o görselde `BusLivery` yok,
  `M_Govde` boyanmıyor (`conecto_ic_yolcu_editor.png` beyaz/gri). Telefonda `BusLivery` M_Govde'yi kaplama rengine boyuyor.
- **Kaynak (tahmin):** `tools/blender/conecto_ic.py` `siniflandir()` (satır 121–173) — içeride kalan `M_Govde` yüzlerinin bir kısmı
  (pencere dikmeleri, alt bölmeler) iç duvara alınmıyor; bu yüzler M_Govde kaldığı için `BusLivery.colorMaterialName = "M_Govde"` onları da
  boyuyor. Sağ üstteki mavi panel muhtemelen `M_caroserie`'nin içe bakan yüzü (tahmin).
- **Tekrarlama:** Menüde Conecto, kalite Yüksek → SEFERE BAŞLA → KAMERA → YOLCU.

### S3 — Conecto kokpitinde gösterge paneli kenarlarında sarı şeritler (açık, küçük)
- **Otobüs:** Conecto. **Kamera:** kokpit (Editor önizlemesi ve telefon).
- Gösterge paneli kenarları sarı: boru (`M_Direk`) olarak sınıflanmış (`conecto_ic_kokpit_editor.png`, `tel_conecto_kokpit_yeni.png`).
- **Kaynak (tahmin):** `tools/blender/conecto_ic.py:158` — "alan, boyuna göre çok küçük" kuralı ince uzun pano kenarlarını da direk sayıyor.
  Ön bölgede (kokpit, y > ön teker) direk sınıflamasını kapatmak ya da kesit (en/boy) eşiğini sıkılaştırmak gerekiyor.

### S4 — Millennium ön tekerleği içeriden görünüyor (açık)
- **Otobüs:** Millennium (tam kalite). **Kamera:** yolcu / iç önizleme, ön kapının arkasındaki koltuk podyumu.
- Koltuk podyumunun altında teker davlumbazı yok; teker ve zemin boşluğu içeriden görünüyor (`millennium_ic_on_sag.png`, `millennium_ic_on_sol.png`).
- **Kaynak (tahmin):** `tools/blender/millennium_donustur.py` — davlumbaz kaynak .blend'de gizli ya da render dışı bir objede olabilir
  (`kaynaklar` yalnızca `not hide_render and visible_get()` objeleri alıyor), ya da tek yüzlü olup `iki_tarafli_yap` zemin kuralına
  (`c.z < 0.35` / `n.z > 0.7 and c.z < 1.2` hariç) takılıyor.

### S5 — Test bileşenleri Conecto'yu fırlatıyordu (düzeltildi, telefonda doğrulanmadı)
- **Otobüs:** Conecto. **Kamera:** hepsi. **Hat:** Hat 1, ilk durakta (Kızılay AVM), Y10/Y11 test APK'sı.
- **Ne oluyor:** Otobüs havaya fırlıyor; uygulama sahneyi tekrar tekrar açıyor. Telefon logu (aynen):
  ```
  [Otobus] Mercedes-Benz O530G Conecto (körüklü) seçildi
  [Puan] çarpışma: KizilayAVM, hız 108.7 m/s, otobüste (-5.5, 2.2, -5.5)
  [Puan] çarpışma: EGO_Durak, hız 74.0 m/s, otobüste (-1.1, 0.6, 2.4)
  [Otobus] Mercedes-Benz O530G Conecto (körüklü) seçildi
  [Puan] çarpışma: KizilayAVM, hız 167.2 m/s, otobüste (-1.4, 5.9, -8.0)
  [Otobus] Mercedes-Benz O530G Conecto (körüklü) seçildi
  [Puan] çarpışma: Taksi_AccentBlue, hız 6.3 m/s, otobüste (-1.3, 1.7, 6.4)
  ```
- **Kaynak:** `Diagnostics/Y10Gozlemci.cs:311` `RotadanCik()` "rotadan çıkma" uyarısını denemek için otobüsü 45 m yana ışınlıyordu;
  yalnızca ön gövde (kök Rigidbody) taşınınca mafsal (ConfigurableJoint, `OtobusKurucu.SetupArkaGovde`) arka gövdeyi 45 m'den çekiyor.
  Aynı sorun `Diagnostics/PerfBenchmark.cs` `Teleport()`'ta da vardı (her durağa ışınlama).
- **Düzeltme:** yeni `Diagnostics/OtobusIsinla.cs` arka gövdeyi ön gövdeye göre düz (mafsal 0°) taşıyor; `Y10Gozlemci.cs:316/322` ve
  `PerfBenchmark.cs:106` bunu kullanıyor. Oyunun kendi kodunda otobüsü ışınlayan yer yok (`body.position`/`MovePosition` taraması: yalnızca
  `TrafficCar` ve bu iki test). **Not:** "seçildi" satırının üç kez görünmesinin sebebi bulunmadı (tahmin: fırlayan otobüs haritadan
  düşünce test/oyun sahneyi yeniden yüklüyor).
- **Doğrulama:** Düzeltilmiş test APK'sı derlendi ve kuruldu (`Success`), ama telefon bağlantısı koptu (`- waiting for device -`); sürüş koşmadı.

### S6 — Conecto Kuğulu → Cinnah dönüşünde takılıyor (Y12'den açık, bu oturumda yeniden denenmedi)
- docs/OTOBUSLER.md "Y12 sonuçları": sol şeritteki araca sürtünüp köşedeki binaya (A3_Cankaya_5Kat) itiliyor. Otomatik pilot; elle sürüşte denenmedi.

### S7 — BMC acil çıkış kolu üzerindeki "EMERGENCY" yazısı İngilizce (açık, küçük)
- `stickers.png` alttaki yuvarlak kolun alt kenarı (`bmc_stickers_yeni.png`). Üst kenarda "ACİL ÇIKIŞ KOLU" Türkçe.

## Konsol uyarıları (Editor, Conecto kurulumu)
Hata yok. Bilgi satırları (aynen):
```
[OtobusKurucu] Govde_On gövde kutusu: merkez (0.00, 1.80, 3.76), boyut (2.50, 2.75, 10.67)
[OtobusKurucu] MB_Conecto_G gövde kutusu: merkez (0.00, 1.80, 3.76), boyut (2.50, 2.75, 10.67)
[OtobusKurucu] Prefab kuruldu: Assets/_Project/Buses/MB_Conecto_G/MB_Conecto_G.prefab
```
BuildAll'da (Y12'den beri): `[OtobusKurucu] Tam kalite materyali eşleşmedi: M_mepibis` (Millennium; IBIS tuşları yalnızca tam kalite modelde, zararsız).
Telefonda her açılışta: `java.lang.ClassNotFoundException: Didn't find class "com.google.android.play.core.assetpacks.AssetPackManager"` (Unity'nin
Play Asset Delivery denetimi; oyunu etkilemiyor).

## Bitmeyen adımlar
- **Yolcu sesi telefonda:** "Durakta inecek var!" duyulmadı; `Y10Gozlemci` artık `stop` sesinin çaldığı anı, düzeyi ve kamerayı loglar
  (`[Y10] yolcu sesi: …`, `Y10Gozlemci.cs:76`) ama koşu tamamlanamadı (S5 ve bağlantı kopması). Dilin Türkçe olduğu kulakla kontrol edilmeli.
- **Conecto kapı kamerası, 4 kapıdan yolcu iniş-binişi, arka gövdeyle çarpışma cezası:** telefonda denenmedi.
- **Eğerek direksiyon** (Y12'den): denenmedi.
- **Görüntüler `docs/onizleme/y13/`'e kondu; docs/GOREVLER.md'ye Y13 sonucu yazılmadı** (bu rapor onun yerine).
- APK güncellenmedi (istenmedi). `~/Desktop/AnkaraOtobus.apk` saat 17:06'daki oyun build'i: BMC etiket/atlas düzeltmesi ve S1 kokpit düzeltmesi **içinde değil**.

## Yapılan ama test edilmeyen değişiklikler
- `Scripts/Diagnostics/OtobusIsinla.cs` (yeni) ve onu kullanan `Y10Gozlemci.RotadanCik`, `PerfBenchmark.Teleport`: derleniyor, telefonda çalıştırılmadı.
- `Scripts/Diagnostics/Y10Gozlemci.cs:67–77` yolcu sesi logu: derleniyor, çıktısı görülmedi.
- `tools/etiketler_tr.py` `bmc_ikizleri_ve_atlas`: çalıştırılmadı (yerelde PIL yok); aynı işlem Blender ile yapıldı, sonuç dokuları commit'te.
- `Textures/Atlas_BMC.png` güncellemesi: dokuda doğrulandı (`bmc_atlas_etiket_yeni.png`), Düşük/Normal ayarda oyunda görülmedi.
- `OtobusOnizleme.cs` yeni iç açılar: çalıştırıldı (görüntüler bunlar).
- `PerfBenchmark` yolcu kamerası: telefonda çalıştı (tablo yukarıda).

## Görüntüler (`docs/onizleme/y13/`)
| Dosya | İçerik |
|---|---|
| `tel_conecto_kokpit_eski.png`, `tel_conecto_kokpit_meclis_eski.png` | S1 öncesi, telefon, Kızılay / Meclis |
| `tel_conecto_kokpit_yeni.png` | S1 sonrası, telefon |
| `tel_conecto_yolcu.png` | S2, telefon, yolcu kamerası |
| `conecto_kirmizi_on.png`, `conecto_mavi_on.png`, `conecto_ozelhalk_arka.png`, `conecto_ozelhalk_sag.png` | Conecto dış: ön cam, ANKARA tabelası, plaka, stoplar; ön/arka kendi renginde, sağda 4 kapı |
| `conecto_ic_yolcu_editor.png`, `conecto_ic_kokpit_editor.png` | Conecto iç (Editor, kaplamasız tam kalite görsel); S3 |
| `bmc_ic_on_sag.png`, `bmc_ic_orta_sag.png`, `bmc_etiket_eski_ingilizce.png` | BMC iç, düzeltme öncesi etiketler |
| `bmc_stickers_yeni.png`, `bmc_atlas_etiket_eski.png`, `bmc_atlas_etiket_yeni.png` | BMC etiket dokusu ve atlas, düzeltme sonrası |
| `millennium_ic_on_sag.png`, `millennium_ic_on_sol.png` | Millennium iç: ANKARAKART afişi, ÇÖP, Hat 1 şeridi Türkçe; S4 (teker içeriden görünüyor) |

Kontrol listesinden doğrulananlar: Conecto önünde düzgün kenarlı ön cam, "ANKARA" tabelası ve plaka; arkada stoplar, ızgara, plaka; EGO mavi ve
Özel Halk'ta ön/arka kendi renginde (kırmızı kalmadı). Editor'de içeride lacivert desenli koltuklar, sarı borular, gri zemin, açık duvar/tavan
(telefonda S2). Millennium içinde ANKARAKART afişi, ÇÖP, Hat 1 durak şeridi okunur; "özel alan", "öncelikli koltuk", "ALO 153" bu açılarda
görüntüye girmedi.

## Bulut yanıtı (9 Ekim akşam)
- **S2 düzeltildi:** `conecto_ic.py` içeride kalan `M_Govde` / `M_caroserie` yüzlerini artık ışın testiyle buluyor (yüzün normali yönündeki
  ışın 4 m içinde kendi gövdesine çarpıyorsa yüz içeridedir) ve iç duvar yapıyor: pencere dikmeleri, alt bölmeler, sol A direği, sağ üstteki
  mavi panel (içe bakan dış kaplama). İki Conecto FBX'i orijinalinden (81ac20d) yeniden üretildi. Kırmızı boyalı render'da içeride kaplama rengi kalmadı.
- **S3 düzeltildi:** sarı şeritler gösterge paneli değil, ön camın önündeki **silecekler** ve kapı fitilleriydi. Boru kuralı artık
  kapalı kesit ister (alan ağırlıklı normaller birbirini götürmeli) ve borunun gövde içinde (|x| < 1,1, ön uçtan 0,45 m geride) olmasını;
  dışarıdakiler ve koltuk ayakları (1 m altı) koyu gri.
- **S4 düzeltildi:** davlumbaz duvarı modelde vardı ama tek yüzlüydü ve tekere bakıyordu. `tools/blender/millennium_davlumbaz.py` her
  tekerin çevresinde tekere bakan yüzlerin ters kopyasını (M_Gri) ekler; iki Millennium FBX'i güncellendi (parça adları ve konumları aynı).
- **S7 düzeltildi:** acil çıkış kolunun alt yayında "EMERGENCY" yerine "ACİL DURUM" (`etiketler_tr.bmc_acil_kol`), atlas hücresi de güncellendi.
- S1, S5 (local) yerinde. S6 açık (otomatik pilot / trafik), ayrı iş.
