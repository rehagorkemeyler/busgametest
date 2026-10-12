# Y18 raporu (yerel oturum, 12–13 Ekim 2026) — ARA RAPOR

> Testler Editor'de (batch Play modu, Hat 1) yapıldı. **A32 FPS ölçümü yapılmadı** (telefon aşamasına geçilmeden durduruldu).
> Kullanıcı Conecto modelini çok kötü buldu; orijinal (dokulu) bir körüklü model bulunamadığı için **mevcut Conecto toparlanacak** →
> `GOREVLER.md` "Y19 — bulut için". Görüntüler `docs/gorseller/y18/`.

## Kurulum
- `git pull --rebase origin Ozan` (07772dd). **Mercedes Conecto (Körüklü) Prefabını Kur** (batch): hata yok, iki FBX uyarısı:
  `A polygon of Mesh 'Govde_Arka' … is self-intersecting and has been discarded`, aynısı `Kapi_4_1` için (iki FBX'te de).
- `Materials/M_LambaPencere/Park/Fren/Ic`: URP/Unlit ✔; Pencere/Park/Fren alfa kesmeli (`_AlphaClip` 1, `gece_yan/onarka/fren.png`) ✔, Ic düz renk ✔.

## Yeni test araçları
- `Diagnostics/KapiTesti.cs` (görev `kapi`): kapı yapısı, kanat yer değiştirmesi zamanla, kapalı/açık dış-iç görüntüler, kaplamalar, kapı
  sesleri, yolcuların hangi kapıdan indiği (üç durak), Yüksek ↔ Düşük, körük kırıkken 3–4. kapı (titreme). Conecto → BMC → Millennium.
- `Diagnostics/GeceTesti.cs` (görev `gece`): lamba parçaları/malzemeleri, dolgu ışığı (rendering layer; **otobüs kadrajda değilken açık/kapalı
  parlaklık farkı** = binalara etkisi), her kaplamada ön/yan/arka/yolcu/kokpit, fren lambası, kalite, körük, dolgu açık/kapalı FPS.
- `Editor/Y18Kurulum.cs`: `-executeMethod AnkaraBus.EditorTools.Y18Kurulum.TestBatch -gorev kapi|gece -zaman … -hava … -kalite …`
  (Editor'de Play modu, görüntüler `Builds/Y18`). Not: batch modda `WaitForEndOfFrame` hiç dönmüyor → `Goruntu.KareSonu()`.
- Y11 test APK'sında da aynı görevler (`-e gorev kapi|gece`).

## 1–7. Conecto kapıları
| Madde | Sonuç |
|---|---|
| 2. Yapı | `Model/Kapi_1_1 … Kapi_4_2` var, hepsi `BusDoor` Plug (dışarı 0,09, kayma ±0,6, 1,6 sn); 3–4. kapıda `takip=ArkaGovde` ✔; `BusDoorController` 4 grup "Ön kapı", "2. kapı", "3. kapı", "4. kapı" ✔ |
| 3. Hareket | Ölçüldü: 0–0,4 sn yalnız dışarı (+0,09 m), sonra yana; öndeki kanat +0,60 öne, arkadaki −0,60 geriye ✔. Kapanış tersi ✔. Ayar değiştirilmedi. |
| 3. Kaplama | Kanatlar `M_caroserie` taşıyor, kaplama değişince kanat dokusu da değişiyor (caroserie → ego_mavi → ozel_halk) ✔ |
| 3. Görünüm | 1–2. kapı kapalıyken temiz. **3–4. kapı kapalıyken camda üçgen delikler, içerisi görünüyor** (`kapali_kapi3/4`). **Açık kanatların alt iç köşesinde kırmızı üçgen** (gövde yüzü kanada karışmış, `acik_kapi3/4`). Arka kanatlar açılınca teker davlumbazının dışından kayıyor (gömülme yok). |
| 4. Körük | Mafsal 33°: kanatlar arka gövdeye göre aynı açık/kapalı farkı (0,09; ±0,60), 3 sn'de en büyük titreme 0,0–0,5 mm ✔ (`korukte_acik_kapi3`) |
| 5. Kalite | Yüksek ↔ Düşük: kanat merkezleri 1–6 mm içinde aynı, kapalı ve açık ✔ |
| 6. Yolcular | Üç durakta inen kapı başına [0,4,2,3], [0,0,1,3], [1,1,2,1]: 2–4. kapılardan iniyor ✔ (ön kapı sayımı biniş kuyruğu). Biniş ön kapıdan (BusPassengers tasarımı). |
| 6. Ses | Kapı 1–3 ✔. **Kapı 4 sessizdi** (BusAudio'da 3 klip) → **düzeltildi**: fazladan kapı son klibi çalar (`BusAudio.OnDoor`). |
| 6. Kapı kamerası | Durakta (sığınak engeli) kamera gövdeye yapışıyor; **açılan ön kanat (öne kayıyor) görüntünün yarısını kapatıyor** (`acik_kapi_kamerasi`). BMC'de sorun yok. 13 Ekim iki deneme (ışın kaynağını burnun önüne almak → kamera aynanın arkasında; gövdeye yaklaşınca yükselmek → tavan kenarı görüntüyü kaplıyor, BMC de bozuldu) geri alındı. Açık sorun. |
| 7. A32 | **Yapılmadı.** |

**BMC ve Millennium kapıları:** üçü/ikisi Rotate, açılıp kapanıyor, sesler her kapıda ✔ (`bmc_acik_kapi2`, `caio_acik_kapi1`). Yeni sorun görülmedi.

## 8. Gece
| Kontrol | Sonuç |
|---|---|
| Lambalar | Gece/akşam: `Lamba_Pencere_On/Arka`, `Lamba_Park(_Arka)`, `Lamba_IcSerit(_Arka)` açık; gündüz hepsi kapalı ✔ |
| Fren | `Lamba_Fren` frende yanıyor, gece ve gündüz, üç otobüs ✔. Fren bırakılınca da yanık: duruşta yokuş kalkış desteği/kapı freni basıncı (`BrakePressure` > 0,1) tutuyor — tasarım gereği, değiştirilmedi. |
| Kalite | Yüksek ↔ Düşük: lambalar kaybolmuyor ✔ |
| Körük | Arka gövde lambaları arka gövdeye göre 0,0 cm kayma ✔ |
| **Dolgu ışığı binaları aydınlatıyordu** | Otobüs kadrajda değilken dolgu açık/kapalı ortalama parlaklık farkı gece 0,146–0,158, akşam 0,074–0,080 (bina renderer'larında bit 6 yok ama ışık yine de etkiliyordu). → **Düzeltildi**: `HavaVeZaman.DolguIsigi` ışığa `UniversalAdditionalLightData` ekleyip `renderingLayers = 1<<6`. Sonra fark **0,0000** ✔ |
| Şiddet | Düzeltmeden sonra 0,4/0,22'de otobüs biraz sönüktü. **13 Ekim: gece 0,55, akşam 0,3** yapıldı; binalara etkisi yine yok (fark 0,0000 / −0,0007), otobüs kadrajda fark gece 0,015 → 0,020; mavi kaplama gece seçiliyor. |
| Kaplama rengi | Kırmızı, mavi, Özel Halk gece yandan seçiliyor (`gece_mb_*_yan_sag`); yan camlar sıcak yanıyor ama **opak bej panel** gibi (içerisi görünmüyor) → Y19 |
| FPS (Mac) | Dolgu açık/kapalı 60/60 (vsync sınırı, anlamsız). A32 yapılmadı. |

## Değişen dosyalar
- `Vehicle/BusAudio.cs` (4. kapı sesi), `Gameplay/HavaVeZaman.cs` (dolgu ışığı URP rendering layer)
- Yeni: `Diagnostics/KapiTesti.cs`, `Diagnostics/GeceTesti.cs`, `Editor/Y18Kurulum.cs`; `Diagnostics/Y11Hat.cs`, `Y10Baslatici.cs` (görevler)
- `Buses/MB_Conecto_G/*` (yeniden kurulum: prefab, `.meta`, `M_Lamba*.mat`, `gece_*.png.meta`)
