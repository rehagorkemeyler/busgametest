# Y16 raporu (yerel oturum, 12 Ekim 2026) — ARA RAPOR

Telefon: Galaxy S24 FE (SM-S721B), USB. Kalite Yüksek, yağmursuz.
Görüntüler: `docs/onizleme/y16/` (`tel_*`). Oyun APK'sı: `~/Desktop/AnkaraOtobus.apk` (14:46, düzeltmeler içinde).
Test APK'sı (`Builds/AnkaraBus_y11_test.apk`) yeni görevler: `tabela`, `anons` (aşağıda).

> Düzeltmelerden (Millennium tabelası, Türkçe harf boşluğu) **sonraki** telefon doğrulaması henüz yapılmadı;
> aşağıdaki tabela bulguları düzeltmeden önceki APK ile. "Açık" bölümüne bakın.

## Hazırlık
- `git pull origin Ozan` → derleme hatası yok.
- **Mercedes Conecto (Körüklü) Prefabını Kur** (batch): gövde kutuları Y15 ile aynı (ön 2.50×2.75×10.67, arka 2.50×2.75×7.61).
- `Resources/Anonslar/`: 20 wav içe aktarıldı, Load Type **Decompress On Load**, dosyalar mono 48 kHz; Force To Mono açıldı.

## Yeni test görevleri
- `-e gorev tabela` (`Diagnostics/TabelaTesti.cs`): otobüs başlangıçta durur, kamera önüne/arkasına/yanlarına konur. Her kaplamada
  ön/arka/yan; ilk kaplamada ön tabelanın iki sayfası (yakın), arka yakın, Türkçe harf sınaması (`İŞÇĞÖÜ ıi`), Düşük↔Yüksek kalite
  (Millennium malzemesinde tabela dokusu var mı sayılır), körüklüde arka gövde kırıkken arka levha, "SERVİS DIŞI". Üç otobüs, sonra Hat 2.
  Satırlar `[Tabela]`, görüntüler `y16_tabela_<hat>_<otobüs>_<zaman>_*.png`.
- `-e gorev anons` (`Diagnostics/AnonsTesti.cs`): gerçek zamanlı otomatik pilot, Hat 1 → Hat 2, üçüncü durak bilerek atlanır;
  kamera her durakta kokpit → yolcu → dış. Anons, ses düzeyi, "inecek var" sesinin anonsla çakışması, 10 sn'lik FPS. Satırlar `[AnonsTest]`.
- `KameraTesti`: yolcu kamerasından tavana bakan üç açı (BMC ışık şeridi için).

## 1. Tabela (gündüz ve gece, iki hat, üç otobüs, üç kaplama)
| | BMC | Millennium | Conecto |
|---|---|---|---|
| Ön "1 ATAKULE"/"2 ULUS" → hat adı | ✔ | ✔ | ✔ |
| Sayfa süreleri | 1. sayfa 4,0 sn; Hat 1 kayan sayfa 6,5 sn; Hat 2 "2 KIZILAY - ULUS" sığar, kaymaz | aynı | aynı |
| Arka hat numarası | ✔ "1" (`tel_hat1_bmc_gunduz_arka_yakin.png`) | ✘ arka camdaki küçük tabela **ön yazıyı sıkıştırıp** gösteriyordu (`tel_hat2_caio_gunduz_arka_yakin.png`) → düzeltildi | ✔ arka gövdede |
| Yüzeye oturma | camın arkasında ✔ | modelin kendi tabelası | ön/arka tabela kutusunda ✔ |
| Türkçe harfler | işaretler var ama gövdeye bitişik, İ "‡" gibi (`tel_hat1_bmc_gunduz_on_yakin_turkce.png`) → düzeltildi | **işaretler hiç görünmüyor** ("ISCGOU II", "SERVIS DISI") → düzeltildi | BMC ile aynı |
| "RESERVADO" | – | yok (ön, arka, yanlar) ✔ | – |
| Gece | parlak ✔ | **sönük** (aydınlatmalı malzeme, `tel_hat1_caio_gece_on_yakin_sayfa1.png`) → düzeltildi | ✔ |
| Kalite Düşük↔Yüksek | – | tabela dokusu 1/1, kaybolmuyor ✔ | – |
| SERVİS DIŞI | ✔ | ✔ (Türkçe işaretsiz) | ✔ |

- Conecto arka levhası `ArkaGovde`'nin çocuğu, arka gövdeyle açı farkı 0,0° (`tel_hat1_mb_gunduz_koruklu_kirik_arka.png`). Testteki dönüş
  mafsalı yalnızca birkaç derece kırdı.
- Harfler ters/aynalı değil (üç otobüs, iki yön).
- Menü vitrininde "ANKARA": **henüz bakılmadı**.

### Düzeltmeler (`Vehicle/HatTabelasi.cs`, `Vehicle/LedYazi.cs`)
- Millennium'da `vmatrix` malzemesi, tabela dokulu **ışıksız** (opak URP/Unlit, `Resources/Hava/M_LambaParlama` kopyası; varyantı build'de
  olduğu için `Shader.Find` kullanılmadı) malzemeyle değiştirilir: gece yanar. Kalite değişince `MalzemeyiYenile` yeniden takar.
- Millennium ön tabela çerçevesi dokunun üst/alt kenarını örtüyordu (UV 0–1, 1,51 × 0,20 m — Blender'la okundu): doku dikeyde %82'ye
  küçültülür (`_BaseMap` ölçek/kaydırma), Türkçe işaretler görünür.
- Aynı malzemeyi kullanan küçük arka tabela (0,28 × 0,15 m) ve iki yan tabela (0,28 × 0,29 m) ön yazıyı sıkıştırıyordu: üstlerine hat
  numarası levhası (`Yerlesim.arkaGenislik` 30 nokta, `Levha.boy` ile yana uzatma).
- `LedYazi`: İ, Ğ, Ö, Ü'nün gövdesi bir satır kısaldı; işaretle gövde arasında bir satır boşluk.

## 2. Anons (telefon, Conecto, gerçek zamanlı otomatik pilot)
```
Hat 1: Kızılay AVM tamamlandı → [Anons] sonraki_meclis (12 km/s, kapı kapalı, yolcu kam., düzey 0.90)
       Meclis tamamlandı → sonraki_kugulu_park (dış kam., düzey 0.25)
       Kuğulu Park ATLANDI → sonraki_cinnah (kokpit, 0.90)
       Cinnah tamamlandı → sonraki_son_atakule (yolcu, 0.90) → Atakule → hat_sonu
Hat 2: Kızılay → sonraki_sihhiye → Sıhhiye → sonraki_opera → Opera ATLANDI → sonraki_son_ulus → Ulus → hat_sonu
```
- Bütün anonslar 12 km/s'de kapılar kapalıyken; atlanan durakta da sıradaki durak anonsu geliyor; "Son durak." ve hat sonu doğru.
- İçeride (kokpit/yolcu) düzey 0,90, dış kamerada 0,25. Kamera durakta değiştiği için geçişteki ilk değer arada olabilir (Opera atlanınca
  0,28 → kokpite geçince 0,90'a çıkar; düzey her kare güncellenir).
- "Durakta inecek var" sesi 4 kez, hepsi anons bittikten sonra (anonstan 5,1–15,6 sn sonra), **çakışma yok**.
- **Kulakla**: durak adlarının okunuşu kullanıcıdan bekleniyor (Kızılay AVM, Kuğulu Park, Cinnah, Sıhhiye, Ulus). `OKUNUS` notu yok.

## 3. İç
- **Conecto körük ağzı:** şerit **kaldı** (`tel_conecto_yolcu_arka.png`): körüğün sol dikmesinde tavandan yere ince şerit, bu kez **mavi**
  (seçili kaplama EGO mavi; Y15'te kırmızıydı). Yani parça hâlâ kaplama dokusundan boyanıyor. Bulut: `tools/blender/conecto_ic.py` —
  körük ağzının sol (arka gövde tarafı) çerçevesi iç sayılmıyor.
- **BMC gece ışık şeridi (Ankarakart):** mevcut kamera açıları tavanı göstermiyor; tavan açıları eklendi, telefonda **henüz bakılmadı**.

## 4. FPS (telefon, Conecto, Yüksek, gerçek zamanlı sürüş, tabela kayarken)
| Hat | Ortalama | En kötü 10 sn | %1 en yavaş kare | En uzun kare | 20 ms üstü |
|---|---|---|---|---|---|
| Hat 1 (327 sn) | 60,0 | 59,9 | 16,8 ms | 42 ms | %0,05 |
| Hat 2 (195 sn) | 60,0 | 60,0 | 16,9 ms | 33 ms | %0,02 |

Y15'te sürüş FPS'i ölçülmemişti; Y14 `PerfBenchmark` (Conecto, Hat 1) 58,4–60. Tabela dokusunun her kare yüklenmesi FPS'i düşürmüyor.

## Açık
- Düzeltilmiş APK ile telefon doğrulaması: Millennium (gündüz/gece, Türkçe işaretler, arka/yan hat no), Türkçe harf boşluğu, BMC Hat 2.
- Menü vitrini "ANKARA", BMC gece tavan şeridi.
- Durak adları kulakla.
- Conecto körük ağzı şeridi (bulut).

## Değişen dosyalar
- `Vehicle/HatTabelasi.cs`, `Vehicle/LedYazi.cs` (yukarıdaki düzeltmeler)
- Yeni `Diagnostics/TabelaTesti.cs`, `Diagnostics/AnonsTesti.cs`; `Diagnostics/Y11Hat.cs`, `Y10Baslatici.cs` (görevler), `KameraTesti.cs` (tavan)
- `Buses/MB_Conecto_G/MB_Conecto_G.prefab` (yeniden kuruldu), `Resources/Anonslar/*.wav.meta` (mono)
