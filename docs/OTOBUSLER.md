# Otobüsler

Oyunda seçilebilen otobüsler `Resources/OtobusKatalogu` içinde; menüde SEFERE BAŞLA'nın üstündeki **< ad >** ile seçilir
(`OyunSecimi.Otobus`). Hat sahnelerinde BMC durur; başka otobüs seçildiyse `OtobusDegistirici` sahne yüklenince (Start'lardan önce)
onu seçilen prefabla değiştirir ve rota, kamera, dokunmatik kontroller, gösterge bağlantılarını taşır. Menüdeki vitrin de değişir.
Kaynaklar: docs/KREDILER.md.

| Sıra | Otobüs | Klasör | Tam / mobil üçgen | Durum |
|---|---|---|---|---|
| 0 | BMC Procity 12LF | `Buses/BMC_Procity_12LF` | 158 / 124 bin | oyunda |
| 1 | Mercedes-Benz O500M · Caio Millennium II (12,5 m, 2 kapı) | `Buses/Caio_Millennium_II` | 157 / 79 bin | oyunda |
| – | Mercedes-Benz O530G Conecto (18 m, körüklü, 4 kapı) | `Buses/MB_Conecto_G` | 145 / 73 bin | yalnızca model |

## Kurulum

- **Ankara Bus → BMC Procity Prefabını Kur / Caio Millennium Prefabını Kur** (komut satırı: `OtobusKurucu.BuildAllBatch`).
  `OtobusKurucu` her otobüs için bir `Tanim` tutar (klasör, kaplamalar, gövde kutusu, fizik farkları); sürüş değerleri ilk kurulumda
  BMC'nin `BusDefinition`'ından kopyalanır, modele özgü olanlar üzerine yazılır. Kurulum kataloğu da günceller.
- Modeller: `tools/blender/millennium_donustur.py` (OMSI .blend'i; `--disable-autoexec` ile açın), `tools/blender/conecto_donustur.py` (Sketchfab GLB).
  İkisi de `bmc_donustur.iki_tarafli_yap` ve `shadow_proxy` kullanır; `--mobil` Düşük/Normal modelini, seçeneksiz hali `_TamKalite`'yi üretir.

## Caio Millennium II

- Parçalar BMC adlarıyla: `Govde`, `Teker_OnSol/OnSag/ArkaSol/ArkaSag`, `Kapi_1_1..Kapi_2_2` (sağ, kanatlı; menteşe kapı boşluğunun dış kenarında),
  `Direksiyon`, `Lamba_KisaFar/UzunFar/Fren/Geri/Park/SinyalSol/SinyalSag/Ic1/Ic2`. Soldaki BRT kapıları gövdeye katıldı, kapalı.
- OMSI modelinde dokular malzemeye bağlı değildi; `DOKULAR` tablosu malzeme adından doku dosyasını bulur.
- Kaplama: kaynaktaki beyaz şablon (`base.bmp`) boyanır → `Textures/caroserie.png` (EGO kırmızı), `Kaplamalar/ego_mavi.png`, `Kaplamalar/ozel_halk.png`
  (renkler `tools/kaplama.py` ile aynı). **EGO logoları, filo numarası ve plaka henüz yok** (bulut çizecek; yerleşim `base.bmp` ile aynı:
  üstteki iki yatay bant yan yüzler, alt yarıda ön/arka).
- Fizik: BMC değerleri + teker yarıçapı 0,525 m, direksiyon 48° (dingil mesafesi 6,6 m; 40° ile Kuğulu → Cinnah dönüşünde kaldırıma takılıyordu).
- Sürüş testi (Editor, otomatik pilot, Hat 1): 5/5 durak, takılma yok, 2 çarpışma (trafik aracı öne/arkaya). BMC aynı testte 0 çarpışma.
  `Unity -batchmode ... -executeMethod AnkaraBus.EditorTools.SurusTestiEditor.RunHat1OtobusBatch -otobus 1`.

## Mercedes O530G Conecto (körüklü) — bulut için

- `MB_Conecto_G.fbx` (mobil) ve `MB_Conecto_G_TamKalite.fbx`; prefab, fizik ve menü kaydı **yok**.
- Parçalar (Unity yerel, kök zeminde, ön +Z): `Govde_On`, `Govde_Arka` ve `Koruk` (pivotları mafsalda: (0, 1.0, −1.57)),
  `Teker_OnSol/OnSag` (z +6.15), `Teker_OrtaSol/OrtaSag` (z +0.26, ön gövdenin arka aksı), `Teker_ArkaSol/ArkaSag` (z −5.87, çeken aks),
  teker yarıçapı 0,51 m; mobilde `Golge_On`, `Golge_Arka`.
- Kaynak GLB'de dokular yoktu. Yan yüzlere düzlemsel UV verildi, `M_caroserie` düz beyaz (`Textures/caroserie.png`, 2048×512):
  u = boy (Blender y −12,15 → 6,05; arka→ön), v = yükseklik 0–3,2 m; **üst yarı sağ yan, alt yarı sol yan**. Sağdaki 4 kapının y aralıkları
  ve cam bandı yüksekliği `conecto_donustur.py` başında (`KAPILAR`, `CAM_Z`, `KAPI_CAM_Z`). Diğer yüzler düz renk
  (`M_Tavan`, `M_Koruk`, `M_On_Cam`, `M_Koyu`, `M_Teker`, `M_Jant`, `M_Ic`).
- Yapılacaklar (bulut): kaplama ve logolar; iki gövdeli fizik (ön ve arka Rigidbody, mafsal, çeken aks arkada, körük mafsal açısının
  yarısı kadar döner); 4 kapı ve kapasite; uzunluğa bağlı kodlar (puanlamada ön uç, `YayaGecitleri` çarpma kutusu, kapı kamerası).
