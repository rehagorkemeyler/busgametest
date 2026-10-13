# Y19 raporu (yerel oturum, 13 Ekim 2026) — körüklü BMC Procity 18 ve Caio Millennium II körüklü

Testler Editor'de (batch Play modu, Hat 1; `Y18Kurulum.TestBatch -gorev koruk|kapi|gece|tutus|tabela`) ve telefonda (A32 Düşük, S24 FE
Yüksek; `-e gorev olcum`). Görüntüler `docs/gorseller/y19/`. **Kullanıcı kararları (13 Ekim):** Conecto projeden kaldırıldı;
A32 için optimizasyon yapılmayacak (ölçümler yalnız bilgi). Bulut görevleri: `GOREVLER.md` → "Y19 — bulut için: Mercedes otobüslerinde
zemin ve ayna".

## 1. Kurulum
- `git pull --rebase` (322020f). FBX içe aktarma uyarısı: **`A polygon of Mesh 'Govde_On' in BMC_Procity_18.fbx (ve _TamKalite) is
  self-intersecting and has been discarded`**. Millennium körüklüde uyarı yok. Bilinen: `Tam kalite materyali eşleşmedi: M_mepibis` (Y12'den beri).
- `OtobusKurucu.BuildAllBatch` (dört prefab): hata yok. Gövde kutuları: BMC 18 ön 2,50×3,12×9,34, arka 2,50×2,70×6,64; Millennium körüklü
  ön 2,50×2,81×10,01, arka 2,44×2,77×7,24.
- **Tek gövdeliler değişmedi:** kurulumdan önce/sonra önizleme (`OtobusOnizleme`, 40 görüntü) gözle aynı (10 dosyada bayt farkı, gözle
  görünmüyor); prefab farkı yalnız `BusDoor`'un yeni varsayılan alanları (`plugOutOffset`).
- `Materials/`: yinelenen malzeme yok; yeni `M_Koruk` (dokulu), `M_KorukCerceve`, `M_KorukTabla` (iki klasörde); gömülü malzeme kalmadı (dört prefab).
- Prefab yapısı (`Y18Kurulum.YapiBatch`): `KorukluOtobus.arkaGorseller` = Golge_Arka, Govde_Arka, Lamba_*_Arka (BMC 18: 9, Millennium: 9),
  `koruk` boş; `Koruk/KorukDeri` SkinnedMeshRenderer, kemikler [kök, ArkaGovde], 8126 köşe; `BusDoorController` BMC 18: 4 grup,
  Millennium körüklü: 3 grup, arka gövde kanatlarında `takip = ArkaGovde` ✔. **Katalog:** BMC, Millennium, BMC 18, Millennium körüklü ✔.
- Mobil üçgen sayısı: BMC 124 bin → BMC 18 174 bin; Millennium 80 bin → körüklü 113 bin.

## 2. Test araçları
- `KapiTesti`, `GeceTesti`, `TabelaTesti`: sıra iki körüklü (bütün adımlar) + iki tek gövdeli; dosya adları `bmc`, `bmc18`, `caio`, `caiog`.
  GeceTesti arka gövde lambalarını (`Lamba_*_Arka`) ve `Lamba_Fren_Arka`'yı kapsar. TabelaTesti batch modda görüntü alabiliyor.
- Yeni görev **`koruk`** (`Diagnostics/KorukTesti.cs`; mevcut `koruklu` fırlama testiyle karışmasın diye bu ad): üç kaplamada dış görünüm,
  körük yakın, koridor boyunca iç, Yüksek ↔ Düşük, mafsal kararlılığı (dururken / 40 km/s / sert fren), sola dönüşte körük bükülmesi, takip kamerası.
- `Y18Kurulum`: `YapiBatch`, `ZeminBatch` (macenta arka planla delik denetimi), `-otobus`, `-sure`.

## 3. Görünüm (iki körüklü, üç kaplama)
| Kontrol | Sonuç |
|---|---|
| Dış (sağ/sol/ön/arka/üst), üç kaplama | Kaplama iki gövdede doğru (EGO kırmızı, mavi, Özel Halk) ✔ (`koruk_bmc18_gunduz_ego_kirmizi_sag`, `koruk_caiog_gunduz_ego_mavi_sag`) |
| Körük | Kıvrımlı, dokulu; kesim çerçevesi gövdeye oturuyor ✔. Virajda bükülüyor: dış köşede boşluk yok, iç köşede gövdeye gömülme yok ✔ (`bukum_dis_kose`, `bukum_ic_kose`, `bukum_ust`) |
| İç (ön gövde → körük → arka gövde), kokpit | Koridor kesintisiz, kesik koltuk yok ✔ (`ic_koruk_onune`, `bukum_ic`) |
| Yüksek ↔ Düşük | Görünür parça sayısı aynı, parça merkezi en çok 1,2–1,4 cm kayıyor (kapı kanadı / arka tabela) ✔ |
| **Sorunlar (bulut)** | **Millennium (iki versiyon) zemini yok** — içeriden aşağı bakışta görüntünün %26–44'ü delik (BMC %0–8, yalnız kapı basamağı); oyunda zemin siyah görünüyor (`zemin_*`). **Millennium aynaları** kaplama atlasından rastgele doku taşıyor. **Millennium körüklü** arka gövde başında "EGO" yazısı körükle kesiliyor (`koruk_caiog_ego_kesik`). **BMC 18** körük kırıkken dış köşenin üstünde körük derisi tavanın üstüne sivri çıkıntı yapıyor (`kapi_bmc18_korukte_acik_kapi3`, sol üst). |

## 4. Sürüş
Elle tam sefer **yapılmadı** (otopilot yok; Editor'de betikli manevralar): `koruk` görevi ve `tutus` görevi (Atatürk Bulvarı fren,
Cinnah yokuşu kalkış, Bulvar → Cinnah dönüşü; kuru/yağmur/kar), her ikisinde fırlama bekçisi (`KorukluTesti`).
| Ölçüm | BMC 18 | Millennium körüklü |
|---|---|---|
| Mafsal dururken (5 sn) | std 0,000°, titreme yok | std 0,000° |
| Mafsal 40 km/s düz (8 sn) | aralık −0,85…0,08°, en hızlı 0,9°/sn, yön değişimi 0 | −0,64…0,04°, 0,7°/sn, 0 |
| Sert fren 40 km/s | en hızlı 1,1°/sn | 0,1°/sn |
| 50 km/s'den fren kuru / yağmur / kar | 16,0 / 19,0 / 22,4 m (mafsal ≤ 2,4°) | 16,7 / 19,1 / 16,6 m (≤ 3,4°) |
| Cinnah kalkış tam gaz, 10 km/s'ye kuru / yağmur / kar | 2,0 / 2,6 sn / **ulaşamadı** (6 sn'de 6,6 km/s) | 1,8 / 3,1 / 4,7 sn |
| Bulvar → Cinnah 25/35 km/s | mafsal 20–24°, fırlama yok | 23–29°, fırlama yok |
| Fırlama bekçisi | durum=OK, çarpışma 0, havada 0 | durum=OK |
Arka gövde kaldırıma gömülmedi, takip kamerası dönüşte arka gövdeye takılmadı (`donerken_takip_kamerasi`). Sönüm / `trailerMassRatio`
değiştirilmedi. Not: BMC 18 karlı Cinnah yokuşunda yavaş kalkıyor (elle sürüşte bakılmalı).

## 5. Kapılar
- BMC 18: 4 kapı, Millennium körüklü: 3 kapı açılıp kapanıyor (menteşeli); sesler her kapıda (BMC 18'in 4. kapısı son klibi çalar, Y18 düzeltmesi) ✔.
- Körük kırıkken (mafsal 31°) arka gövde kapıları arka gövdeyle birlikte, 3 sn'de titreme 0,0 mm ✔ (`kapi_bmc18_korukte_acik_kapi3`).
- Yolcular üç durakta bütün kapılardan iniyor: BMC 18 [0,1,1,1], [1,1,5,3], [1,3,2,1]; Millennium körüklü [2,1,4], [2,1,2], [0,2,1] ✔. Biniş ön kapıdan (tasarım).
- Yüksek ↔ Düşük: kanat merkezleri aynı ✔. Tek gövdeliler sorunsuz.
- **Kapı kamerası:** Y18'deki iki deneme (Conecto) geri alınmıştı; BusCameraRig değişmedi. Körüklülerde kanatlar menteşeli (içe döner),
  Conecto'daki öne kayan kanat sorunu yok.

## 6. Gece / akşam (açık ve kar)
- Farlar, park lambaları (iki gövde), iç ışıklar (iki gövde) açık; stop: frende `Lamba_Fren_Arka` yanıyor (gece ve gündüz) ✔.
- Körük kırıkken arka gövde lambaları arka gövdeye göre 0,0 cm kayma ✔; Yüksek ↔ Düşük lambalar kaybolmuyor ✔.
- Dolgu ışığı binaları aydınlatmıyor: fark BMC 18 +0,0003, Millennium körüklü −0,0024 (ters yönde, trafik/yaya gürültüsü) ✔.
- LED tabela: ön ✔, arka tabela arka gövdede, ebeveyni `ArkaGovde`, açı 0,0° ✔ (`tabela_hat1_bmc18_gece_koruklu_kirik_arka`).
  (TabelaTesti'nin Millennium "vmatrix 0/0" satırı testin malzeme kontrolü; tabela görüntüde yazıyor: `tabela_hat1_caio_gece_ego_kirmizi_on`.)
- Görüntüler: `gece_gece_bmc18_ego_kirmizi_yan_sag`, `gece_gece_caiog_ego_mavi_arka`, `gece_gece_bmc18_frende_arka`, `gece_gece_kar_caiog_ozel_halk_yan_sag`.

## 7. Performans
### A32 (SM-A325F) Düşük, Hat 1, 15 nokta (5 durak × dış/kokpit/yolcu), 45 FPS sınırlı — yalnız bilgi
| Otobüs | Gündüz ort. / en düşük | Gece ort. / en düşük | Çizim ort. (gündüz) | Üçgen ort. (gündüz) |
|---|---|---|---|---|
| BMC | 44,5 / 40,8 | 26,8 / 17,8 | 242 | 362 bin |
| BMC 18 | 44,9 / 43,2 | 25,9 / 14,9 | 236 | 380 bin |
| Millennium | 43,2 / 34,4 | 31,2 / 19,5 | 273 | 377 bin |
| Millennium körüklü | 41,0 / 30,5 (11 nokta) | 29,5 / 17,0 | 296 | 378 bin |
- **Kabul (körüklü ≤ tek gövdeli − 5):** gündüz ve gece ✔ (fark 0–2 FPS).
- Telefon ısındıkça (pil 32 → 37 °C) ölçüldü. Gece dolgu ışığı aç/kapa (aynı sahne, dörder kez): BMC 18 41,9–42,6 → 34,8–35,0,
  Millennium körüklü 32,2 → 28,4–28,6. Düşük'te dolguyu kapatma denemesi otobüsü griye çevirdi, **geri alındı**; kullanıcı A32
  optimizasyonu istemiyor.

### S24 FE (SM-S721B, Xclipse 940) Yüksek, tam kalite model, 60 FPS sınırlı
| Otobüs | Gündüz ort. / en düşük | Gece ort. / en düşük | Çizim ort. gündüz / gece | Üçgen ort. gündüz / gece |
|---|---|---|---|---|
| BMC | 60,0 / 59,1 | 60,0 / 58,7 | 326 / 352 | 563 / 616 bin |
| BMC 18 | 60,0 / 58,7 | 60,0 / 58,8 | 320 / 372 | 559 / 641 bin |
| Millennium | 60,0 / 58,4 | 60,0 / 58,8 | 260 / 300 | 516 / 601 bin |
| Millennium körüklü | 60,0 / 58,4 | 60,0 / 58,7 | 311 / 334 | 562 / 616 bin |
- Kabul ✔ (fark yok). Telefon ölçüm boyunca 38,5 → 42,2 °C; bir kez USB hata ayıklama kapandı (kullanıcı yeniden açtı), üç gece koşusu sonra tamamlandı.

## 8. APK
- Test (`Builds/AnkaraBus_y11_test.apk`) ve oyun (`Builds/AnkaraBus.apk`, `~/Desktop/AnkaraOtobus.apk`) APK'ları 0 hata. A32'de oyun APK'sı kurulu.
- Menü (S24, `-e gorev menu`): otobüs seçicisinde "Mercedes-Benz O500MA · Caio Millennium II (körüklü)", vitrinde körüklü model düzgün
  (`menu_s24_millennium_koruklu`); katalogda dört otobüs. Logda `The referenced script on this Behaviour (Game Object 'VitrinOtobusu') is
  missing!` uyarısı — A32'deki önceki koşularda da var (yeni değil), vitrin görünümünü etkilemiyor.
- Telefon testlerinden sonra iki telefona da oyun APK'sı geri kuruldu.
- Krediler (`AnaMenu.KredilerYazisi`): Millennium satırında "körüklü versiyon", BMC satırında "Körüklü versiyon (BMC Procity 18) bu modelden türetildi"; Conecto satırı yok ✔.

## Conecto kaldırıldı (kullanıcı kararı)
`Buses/MB_Conecto_G`, `tools/kaplama_conecto.py`, `tools/blender/conecto_*.py` silindi; kurucudan Conecto tanımı, menü kaydı ve yalnız
Conecto'nun kullandığı kod (plug kapı kurulumu, kapı noktaları, Conecto lamba malzemeleri), `HatTabelasi` yerleşimi, `KREDILER.md` satırı.
`BusDoor.Motion.Plug` genel bir kapı türü olarak kaldı. Eski raporlardaki Conecto anlatımı değiştirilmedi.

## Değişen dosyalar
- Kod: `Gameplay/HavaVeZaman.cs` (değişiklik geri alındı, net değişiklik yok), `Editor/OtobusKurucu.cs`, `Vehicle/HatTabelasi.cs`,
  `Vehicle/KorukluOtobus.cs` (yorum); yeni `Diagnostics/KorukTesti.cs`; `KapiTesti`, `GeceTesti`, `TabelaTesti`, `Y11Hat`, `Y10Baslatici`, `Editor/Y18Kurulum.cs`
- Prefablar ve varlıklar: `BMC_Procity_18.*`, `Caio_Millennium_II_G.*`, `*_KorukDeri.asset`, `M_Koruk*.mat`, `OtobusKatalogu.asset`

## Ek (13 Ekim, akşam): bulutun Millennium zemini, aynalar ve EGO düzeltmesi (8a13490, 685e39c, b0ad225)
- `BuildAllBatch` (dört prefab): hata yok; uyarılar yalnız bilinenler (`M_mepibis`, BMC 18 `Govde_On` kendini kesen poligon — BMC 18 değişmedi,
  prefab farkı yalnız fileID sırası). `Materials/`: yeni `M_Zemin` (dokulu), `M_AynaGovde`, `M_AynaCam`.
- **Zemin (`ZeminBatch`, macenta = delik), içeriden aşağı bakış:**
  | | önce | sonra |
  |---|---|---|
  | Millennium | %26–44 | **%0–4** |
  | Millennium körüklü | %20–38 | **%0–1,2** |
  | BMC / BMC 18 (karşılaştırma) | %0–8 | aynı |
  Zemin gri, kaymaz desenli (`zemin_sonra_Caio_Millennium_II_asagi_z1`; kalan macenta yan camlardan). Oyunda körüklü Millennium yolcu
  kamerası gündüz (Normal) ve gece (Düşük): zemin gri, siyah değil, koltuk ve direkler zeminde ✔ (`sonra_caiog_gunduz_ic_yolcu_zemin`,
  `sonra_caiog_gece_dusuk_yolcu_zemin`).
- **Aynalar:** üç kaplamada ayna cam/gövdesi koyu gri, kaplama değişince desen değişmiyor ✔ (`zemin_sonra_Caio_Millennium_II_k1_ayna_sol_arka`, mavi kaplama).
- **Körüklüde EGO:** arka gövdenin başında kesik yazı yok (düz gövde rengi) ✔ (`sonra_koruk_caiog_ego_yok`). Mafsal, kalite geçişi öncekiyle aynı.
- Açık kalan (bulut): BMC 18 körük üst köşesinde sivri çıkıntı (GOREVLER madde 4).
