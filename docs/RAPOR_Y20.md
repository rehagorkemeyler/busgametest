# Y20 raporu (yerel oturum, 13 Ekim 2026) — Quaternius yolcuları koltuklara oturuyor

Testler Editor'de (karakter/koltuk denetimi: `Y20Kurulum.KarakterBatch|KoltukBatch`; Play modu ön kontrolü: `Y18Kurulum.TestBatch -gorev yolcu`)
ve telefonda (S24 FE, Hat 1, gündüz, Düşük / Normal / Yüksek; `-e gorev yolcu`). A32 bu turda kullanılmadı (kullanıcı: "A32'ye şimdilik
ihtiyacımız yok"). Görüntüler `docs/gorseller/y20/`, koltuk ölçüm listesi `docs/gorseller/y20/koltuk_denetimi.txt`.
Bulut görevleri: `GOREVLER.md` → "Y20 — bulut için: Millennium koltuk noktaları, yolcu boyu, yolcu kamerası".

## 1. Kurulum ve derleme
- `git pull --rebase` (f884419). **Derleme hatası yok** (bulutun kodu ilk seferde derlendi).
- **Ankara Bus → Yolcu Karakterlerini Kur** (`YolcuKurucu.KurBatch`): `Resources/Yolcular/` altında 14 prefab (8 E, 6 K),
  `Yolcu_E.controller` / `Yolcu_K.controller` (Bekle varsayılan, Yuru, Otur, Tutun; klipler bağlı, döngülü), `M_Yolcu` (URP/Lit, palet,
  noktasal). **Konsolda uyarı yok.** Her prefabda: Animator (controller + avatar), `Passenger`, tek SkinnedMeshRenderer
  (E 9,4–14 bin köşe / 79 kemik, K 10–11,8 bin köşe / 62 kemik), tek malzeme `M_Yolcu`.
- **Dört prefab yeniden kuruldu** (`OtobusKurucu.BuildAllBatch`), konsolda: BMC 26 koltuk, 12 ayakta yer; BMC 18 38 / 17; Millennium 38 / 13;
  Millennium körüklü 51 / 20 ✔. Bilinen tek uyarı: `Tam kalite materyali eşleşmedi: M_mepibis` (Y12'den beri).
  Körüklülerde mafsalın gerisindeki koltuk/ayakta noktaları `ArkaGovde` altında (BMC 18: Koltuk_23–38, Ayakta_11–17; Millennium körüklü:
  Koltuk_31–51, Ayakta_12–20) ✔.

## 2. Karakterler (`Y20Kurulum.KarakterBatch`, Editor)
| Kontrol | Sonuç |
|---|---|
| Bakış yönü | **+Z** ✔ (ayak → parmak +0,18 z; önden çekimde yüzler görünüyor). 180° düzeltme gerekmedi. |
| Boy (Bekle, saç dahil) | **E 1,85–1,88 m, K 1,82–1,85 m** (Punk saçla 1,95). Beklenen ~1,7 — **uzun**, kadınlarla erkekler aynı boyda. Bulut ölçeği küçültsün (bkz. bulut görevleri). |
| Dört durum | Bekle: kollar aşağı (üst kol 23–30°), Yuru: adım (uyluk 4/29°), Otur: uyluk 83° yatay, eller dizde; Tutun: sağ kol yukarı (164°). Görüntü: `karakter_E_dort_durum_onden`, `karakter_K_dort_durum_yandan`. |
| T-poz kalan kemik | **Yok** ✔ — her klip iskeletin bütün kemiklerini (E 79, K 62) sürüyor. |
| Otur pozu | Ayakkabı tabanı kökün 6 cm altına iniyor (oturan yolcunun ayakları zemine 6 cm gömülü; gözle fark edilmiyor). |
- Hepsi: `karakter_hepsi_onden` (sağda 1,7 m çubuk).

## 3. Koltuk noktaları (`Y20Kurulum.KoltukBatch`, Editor)
Her koltuğa Otur pozunda, her ayakta yere Tutun pozunda yolcu kondu; koltuk noktasının 0,13 m gerisinden aşağı mesh ışını (minder
kökten 0,45 m yukarıda olmalı), koridordan yakın görüntü. Liste: `koltuk_denetimi.txt`.
- **BMC ve BMC 18: bütün koltuklar doğru** — minder her koltukta kökün +0,44 m üstünde, yön öne/arkaya doğru, yolcular minderde
  (`koltuk_BMC_Procity_12LF_Koltuk_03_sorun`, `_19_sorun`: ölçüm "zemin" uyarısı verdi ama görüntüde doğru, ışın koltuk ayağına çarpıyor).
  Ayakta yerler koridorda, zeminde ✔.
- **Millennium: 38 koltuğun 21'i yanlış; Millennium körüklü: 51'in 29'u yanlış** (görüntüyle doğrulandı):

| Otobüs | Yanlış `Koltuk_NN` | Ne görünüyor |
|---|---|---|
| Millennium II | 05, 07, 08, 09, 10, 11, 27, 28, 37, 38 (yön "arkaya") | Minder yok: yolcu iki koltuk arasındaki sarı borunun üstünde / havada; gerçek koltuk yanında ters yöne bakıyor (`koltuk_Caio_Millennium_II_Koltuk_05_sorun`, `_08_sorun`). |
| | 29, 30, 31, 32 (y 1,20, "arkaya") | Sırt sırta iki koltuğun sırtlıkları arasına oturmuş (`_29_sorun`). |
| | 06 (minder +0,16), 14, 15 | 14/15 ön kapıda bilet makinesinin yanında, sürücü bölmesinde havada (`_14_sorun`). |
| | 18, 20, 21 (kök zeminde, minder yok) | Kapı önündeki boş alanda havada oturuyor (`_21_sorun`). |
| | 22 (kök zeminden 0,18 m yukarıda) | Kapı yanında havada (`_22_sorun`). |
| Millennium körüklü | 05–11, 14, 15, 18, 20–22, 27–30 (ön gövde, Millennium ile aynı) ve 31, 33, 34, 35, 40–45, 50, 51 (arka gövde; tek gövdelinin aynı tip koltuklarının kopyası) | Aynı hatalar. |

- Doğru olanlar (minder +0,45): Millennium 01–04, 12, 13, 16, 17, 19, 23–26, 33–36 (`koltuk_Caio_Millennium_II_Koltuk_01`).
- Ortak neden: `koltuk_cikar.py`'nin "sırtlıktan koltuk" yolu (Millennium minderi kumaş değil); sırtlığın arka yüzü de "öne bakan kumaş"
  sayılmış gibi — arkaya bakan koltukların çoğu sahte. Ayrıca kapı/sürücü bölmesindeki kumaş/plastik paneller koltuk sanılmış.

## 4. Oyunda (S24, Hat 1, dört otobüs, Düşük / Normal / Yüksek) — yeni `yolcu` görevi
`Diagnostics/YolcuTesti.cs`: otobüs 1. durağa (Meclis) konur, bekleyenlere görünen sınırın 4 fazlası kadar yolcu eklenir, kapılar açılır;
sonra Bulvar → Cinnah bağlantısında 20 km/s viraj; sonra 2. durakta (Kuğulu Park) iniş; sonra FPS. Otopilot yok (yalnız test betiği).

| Kontrol | Düşük (sınır 12) | Normal (sınır 20) | Yüksek (sınır 30) |
|---|---|---|---|
| Binenler ön kapıdan girip koridordan koltuğa yürüyor, oturuyor | ✔ 12/12 dört otobüste | ✔ 20/20 | ✔ BMC 26 + 4, Millennium 30, BMC 18 30, Millennium körüklü 30 |
| Oturanın koltuk noktasına uzaklığı / açı | 0,000 m / 0° | 0 / 0 | 0 / 0 |
| Takılan (yürümeye devam eden) yolcu | 0 | 0 | 0 |
| Koltuk dolunca ayakta tutunan | — (sınır koltuktan az) | — | ✔ BMC'de 4 kişi tutunuyor (`s24_bmc_Yuksek_oturdu_arkadan_one`) |
| Giderken oturanlar yerinde (koltuğa göre en büyük kayma, viraj dahil) | 0,0000 m, 0,00° | 0,0000 m | 0,0000 m |
| Körüklüde viraj (mafsal en çok) | BMC 18 16°, Millennium 20° | 17° / 20° | 17° / 20° — arka gövdedeki yolcular gövdeyle dönüyor (`s24_bmc18_Yuksek_viraj_onden_arkaya`) |
| Arka gövde koltuklarına mafsaldan geçiş | — | — | ✔ BMC 18: 8 yolcu arka gövdeye oturdu; inişte 4'ü 3.–4. kapıdan indi |
| İnenler kalkıp en yakın açık kapıdan iniyor | ✔ (2 otobüste o durakta rastgele 0 inen) | ✔ 2.–3. kapılar (ön kapıdan inen yok) | ✔ |
| Duraktaki bekleyenler / yaya geçitleri | yeni karakterler (eski model 0) | aynı | aynı |

- **Görünen sınır koltuk sayısından az:** Düşük (12) ve Normal (20)'de ayakta tutunan hiç olmuyor, körüklülerde arka gövdeye kimse oturmuyor
  (`BosKoltuk` kapının gövdesini +400 puanla tercih ediyor; ön gövdede BMC 18 22, Millennium körüklü 30 koltuk var). Mafsaldan geçiş yalnız
  Yüksek'te görülüyor. Millennium körüklünün ön gövdesinde 30 koltuk olduğu için Yüksek'te bile arka gövde boş kalıyor (sahte koltuklar
  düzelince 30'un altına inecek).
- **Yolcu kamerası (BMC):** kamera (`interiorPosition` 0,45 / 2,0 / −1,2) `Koltuk_12`'de oturan yolcunun kafasının içinde kalıyor;
  koltuk dolunca görüntünün yarısı kafa (`s24_bmc_Normal_oturdu_yolcu`). Diğer otobüslerde sorun yok.
- **Millennium'larda** sahte koltuklara oturan yolcular oyunda da görünüyor (`s24_caiog_Normal_oturdu_yolcu`: bilet makinesinin yanında
  baretli yolcu havada).
- Görüntüler: biniş `s24_bmc_Normal_binis_on_kapi`, oturmuş `s24_bmc_Normal_oturdu_arkadan_one`, `s24_caio_Normal_oturdu_onden_arkaya`,
  kokpit `s24_bmc_Normal_oturdu_kokpit`, durak `s24_bmc_Normal_durak_bekleyenler`, iniş `s24_bmc18_Normal_inis_kapi`, `s24_sundurma_once_bmc18_inis_yan`,
  viraj `s24_caiog_Normal_viraj_onden_arkaya`.

## 5. Durak sundurmasının içinden geçen yayalar (kullanıcı, 13 Ekim) — düzeltildi
Kullanıcı: "Siviller durakların içinden geçiyor." İnen yolcular kapıdan kaldırımdaki hedefe dümdüz yürüyor, sundurmanın kapalı arka
duvarından geçiyordu (`s24_sundurma_once_bmc18_inis_yan`). Hat 1 duraklarında sundurmanın çarpışma kutusu (`Duraklar/EGO_Durak`,
7,60 × 3,25 × 2,20 m) durak noktasının 1,45–3,65 m sağında. Otobüs yanaşınca gövdesi 1,25 m'de bitiyor, kapı önü noktası
(gövdenin 0,8 m dışı) **kutunun içine** düşüyor. Sundurmanın yol tarafı açık; arka ve yanlar kapalı.
- **Düzeltme** (`Passengers/YayaYolu.cs`, yeni):
  - `Passenger.WalkTo` artık düz çizgi değil. Bel hizasında sabit bir çarpışıcı yolu kesiyorsa yolcu onun köşelerinden dolaşıyor
    (sundurma, direk, bina; araçlar ve otobüs Rigidbody'li olduğu için sayılmıyor). Köşe başka bir engelin içindeyse kullanılmıyor.
    Köşe yanaşmış otobüse çarpıyorsa dar payla (0,12 m) otobüsle sundurma arasındaki şeritten geçiliyor. Hedef bir engelin içindeyse
    dışına alınıyor.
  - `BusPassengers.DoorOutside`: kapı önü noktası sundurmanın içindeyse açık önüne, otobüsle sundurma arasındaki şeride alınıyor.
  - `PassengerManager.SpawnWaiting`: sundurmanın altına düşen bekleyen arkaya doğru dağıtılmıyor, açık önüne yakın duruyor.
- Yaya geçitlerindeki yayalar da aynı `WalkTo`'yu kullandığı için onlar da sabit engellerden dolaşıyor.
- **Ölçüm (S24, Normal, `yolcu` görevi):** Durakta sundurmanın arka/yan duvarını bel hizasında kesen yaya sayısı (açık ön sayılmaz).
  İnenler ve binenler birlikte sayıldı.
  - Düzeltmeden önce kaba sayaçla (sundurma kutusunun içinden yürüyen) her inen sayıldı: BMC 7/7, Millennium 4/4, BMC 18 1/1,
    Millennium körüklü 5/5.
  - **Düzeltmeden sonra dört otobüste 0.** O turda BMC'den 8 kişi indi, diğerlerinde rastgele 0–2 kişi; her otobüste 4–6 kişi bindi.
- Görüntü: `s24_sundurma_sonra_bmc_inis_yan` (inenler sundurmanın iki ucundan kaldırıma çıkıyor), `s24_sundurma_sonra_bmc_inis_kapi`.
- İlk denemede yalnız köşeden dolaşma vardı, yetmedi: kapı önü noktası kutunun içinde olduğu için ışın engeli görmüyordu. Kutunun
  ölçüleri `Y20Kurulum.DurakBatch` ile sahneden okundu.

## 6. Performans (S24 FE, Hat 1, gündüz, 2. durakta dururken; dış + yolcu kamerası 12'şer sn, iki tekrar ortalaması)
Önce = içerideki yolcular gizli (Y19'daki gibi otobüs içi boş), sonra = görünür. 60 FPS sınırlı; **her durumda 60,0–60,1 FPS, 1% düşük
59,4–59,7** — FPS kaybı yok. Yük GPU süresinde görülüyor:

| Kalite | Otobüs | görünen | GPU ms dış (önce → sonra) | GPU ms yolcu kamerası | Çizim (dış) | Üçgen (dış) |
|---|---|---|---|---|---|---|
| Düşük | BMC | 12 | 3,0 → 3,8 | 3,6 → 4,2 | 104 → 116 | 218 → 290 bin |
| Düşük | Millennium | 12 | 2,5 → 3,5 | 3,2 → 4,3 | 115 → 127 | 172 → 245 bin |
| Düşük | BMC 18 | 6 | 2,9 → 3,4 | 3,3 → 3,8 | 118 → 124 | 262 → 304 bin |
| Düşük | Millennium körüklü | 5 | 3,0 → 3,1 | 3,5 → 3,7 | 142 → 148 | 203 → 236 bin |
| Normal | BMC | 17 | 3,4 → 5,0 | 4,2 → 5,5 | 103 → 120 | 216 → 321 bin |
| Normal | Millennium | 20 | 3,0 → 4,8 | 4,1 → 6,2 | 115 → 136 | 172 → 299 bin |
| Normal | BMC 18 | 13 | 3,5 → 4,8 | 4,2 → 4,4 | 119 → 132 | 266 → 346 bin |
| Normal | Millennium körüklü | 16 | 3,1 → 4,5 | 4,1 → 5,7 | 144 → 159 | 209 → 303 bin |
| Yüksek | BMC | 29 | 7,2 → 9,6 | 8,2 → 9,4 | 190 → 220 | 404 → 588 bin |
| Yüksek | Millennium | 25 | 7,0 → 9,1 | 8,6 → 10,1 | 146 → 172 | 398 → 562 bin |
| Yüksek | BMC 18 | 23 | 7,7 → 9,6 | 8,2 → 8,7 | 246 → 268 | 523 → 663 bin |
| Yüksek | Millennium körüklü | 27 | 7,2 → 9,3 | 8,5 → 10,1 | 197 → 224 | 513 → 679 bin |

- Görünen sayısı inişten sonraki sayı (2. durakta inenler çıktıktan sonra). Yolcu başına ~6 bin üçgen, ~1 çizim, ~0,1 ms GPU.
- CPU ana iş parçacığı 8–9,6 ms (yolcularla +0,3–1,4 ms; 30 Animator).
- **A32 ölçülmedi** (kullanıcı kararı); kabul ölçütü "A32'de en çok 3 FPS kayıp" bu turda doğrulanmadı, `BusPassengers.MaxGorunur` değiştirilmedi.
  S24'te Yüksek'te bile 16,7 ms bütçenin GPU'da ~%60'ı kullanılıyor.

## 7. Test araçları (yeni)
- `Editor/Y20Kurulum.cs`: `KarakterBatch` (boy, yön, kol/bacak açıları, klibin sürmediği kemikler, görüntüler), `KoltukBatch` (her koltuğa
  oturan yolcu, minder/zemin mesh ışını, yakın görüntüler; `Builds/Y20/`).
- `Diagnostics/YolcuTesti.cs` (`yolcu` görevi; `Y11Hat`'e eklendi): biniş / viraj / iniş / FPS, sundurma içinden yürüyen sayacı; Editor'de
  `YAYAYOLU=0` (kaçınma kapalı, önce/sonra) ve `YOLCU_FPS=0` ortam değişkenleri. `Y20Kurulum.DurakBatch`: durak çarpışıcılarının ölçüleri.
- Bilinen test aracı kusuru: körüklülerde "arkadan öne" koridor görüntüsü arka gövdenin çatısından çekiliyor (oyunla ilgisi yok).

## 8. APK
- Test (`Builds/AnkaraBus_y11_test.apk`) ve oyun (`Builds/AnkaraBus.apk`, `~/Desktop/AnkaraOtobus.apk`) APK'ları hatasız. Oyun APK'sı sundurma düzeltmesini içeriyor, `~/Desktop/AnkaraOtobus.apk`'ye kopyalandı.
- Testlerden sonra S24'e oyun APK'sı (sundurma düzeltmesi dahil) geri kuruldu; telefonda test APK'sı kalmadı.
