# Y18 eylem planı — otobüs kapıları, ışıklandırma, gece görünümü

Kaynak: `docs/RAPOR_Y17.md`, `docs/GOREVLER.md` → "Y17 — bulut için", kullanıcının A32'de gördükleri (12 Ekim).
Kar ve Y16'dan kalan testler bu işler bitince **hep birlikte** yapılacak (kullanıcı kararı; otopilotlu test yok).

## Durum özeti

| # | Sorun | Nerede | Kök neden | Kimin işi |
|---|---|---|---|---|
| A | Conecto kapıları açılıp kapanmıyor | Conecto, 4 kapı | Kapı kanatları gövde mesh'inin parçası (dış yüzü `M_caroserie`, kapı resmi dokuya çizili); kurucu yalnızca boş `Kapi_1..4` noktası koyuyor (`BusDoor` Slide, kayma 0). KAPILAR düğmesi oyun mantığında çalışıyor ama görünürde hiçbir şey hareket etmiyor. | Bulut (model + kurucu), yerel (kurulum, test) |
| B | Gece otobüsün dışı çok gri | Üç otobüs, en çok Conecto ve karda | Otobüsler gece yalnız seyrek ışık problarından (lambalar 25 m arayla, gök kısık) ve kısık mavi ay ışığından aydınlanıyor; binalar lightmap'le aydınlık, otobüs değil. HDR ve son işlem (renk düzeltme) kapalı. Yerel prob çarpanı (gece ×1,8) yetmedi. | Bulut |
| C | Gece içerisi gri | Üç otobüs | İç ışık eklendi (yerel, gövde başına bir nokta ışık) ama iç yüzeylerin çoğu dokusuz düz gri; ışık panelleri yanmıyor (emissive yok). | Bulut |
| D | Conecto içi koltuklar dışında kaplamasız | Conecto | `conecto_ic.py`: `M_IcDuvar` (0,80 gri), `M_IcTavan` (0,90) düz renk, dokusuz. | Bulut |
| E | Gündüz de biraz soluk | Üç otobüs | Conecto `M_Govde` `_BaseColor` FBX'ten 0,906 geliyor (%10 koyu); HDR/renk düzeltme yok; kaplama dokuları düz, panel ayrıntısı az. | Bulut |
| F | BMC ve Millennium kapıları/içi | – | Raporda sorun yok; aynı "gri" görünüm var mı bakılmadı. | Yerel (önce bak) |

## Sıra ve yapılacaklar

### 1. Conecto kapıları (A) — en önce (bulut kısmı yapıldı: conecto_kapilar.py, BusDoor Plug, SetupPlugKapilar; GOREVLER Y18)
**Bulut**
- `tools/blender/conecto_kapilar.py` (FBX son işlem, `conecto_ic.py`'den sonra): sağ yandaki 4 kapının dikdörtgeni içindeki yüzleri
  (dış kaplama, iç panel, tutamak, cam; `KAPILAR` y aralıkları, z 0,3–2,66, x < −1,0) gövdeden ayırıp her kapı için iki kanat yap:
  `Kapi_1_1`, `Kapi_1_2` … `Kapi_4_2`. Kanatlar `Govde_On` (kapı 1–2) / `Govde_Arka` (kapı 3–4) altında çocuk obje, pivot kanadın dış
  kenarında. Kapı boşluğunun kenarına (kasaya) koyu kauçuk çerçeve.
- Conecto'nun gerçek kapısı dışa açılan "plug" (sürgülü-salınır) kapı: oyunda kanat önce 8 cm dışarı, sonra gövde boyunca kendi
  yarısı kadar kayar. `BusDoor`'a yeni hareket türü `Plug` (iki aşamalı: dışarı + yana) — yoksa `Slide` ile çapraz kayma.
- Kurucu: Conecto için `KapiNoktalari` yerine `SetupDoors` (DoorGroups) yolu; 4 grup ("Ön kapı", "2. kapı", "3. kapı", "4. kapı").
  Arka gövdedeki kanatlar `KorukluOtobus` ile arka gövdeyle birlikte hareket eder (Govde_Arka'nın çocuğu oldukları için ek iş yok);
  yolcu kapı noktaları (`BusDoorController.DoorCenter`) kanat merkezlerinden.
- Kapı sesleri (`BusAudio` kapı kaynakları) dört kapıya.
- Blender'da kapalı / açık render (dışarıdan ve içeriden) ile kontrol.
**Yerel**: prefabı kur; Editor ve telefonda dört kapı açılıp kapanıyor mu, kanatlar gövdeye gömülüyor/ayrılıyor mu, körük kırıkken 3–4. kapı
arka gövdeyle mi hareket ediyor, yolcular dört kapıdan inip biniyor mu, kapı kamerası ön kapıyı gösteriyor mu.
**Kabul:** dört kapı görünür biçimde açılıp kapanıyor; kapalıyken boşluk yok, içerisi görünmüyor.

### 2. Gece otobüs dış görünümü (B) ve soluk renk (E)
**Bulut**
- **Otobüse özel dolgu ışığı:** gece ve akşam, yalnız otobüsü aydınlatan ikinci yönlü ışık (URP rendering layer: otobüs renderer'ları
  ayrı katmanda, ışığın `renderingLayerMask`'ı yalnız o katman; üç kalite ayarında `m_SupportsLightLayers` zaten açık). Sokak lambası
  rengine yakın (sıcak), şiddet gece 0,35, akşam 0,2; gölgesiz. Maliyeti: otobüs için bir ek ışık, binalara etkisi yok.
- **Kendi ışıkları:** gece farlar, park lambaları, stoplar, iç ışıktan pencere parlaması (iç ışık yanarken yan camlar hafif sıcak
  emissive), LED tabela zaten parlıyor. Lamba camlarına emissive malzeme (`Lamba_*` parçaları için).
- **Renk:** Conecto `M_Govde` `_BaseColor` 1,0 (kurucuda); BMC ve Millennium kaplama malzemelerinin rengi kontrol. Hafif renk
  düzeltme (URP Volume: LDR'de çalışan Color Adjustments + düşük kontrast/doygunluk artışı, Lift-Gamma-Gain) — maliyeti A32'de ölçülecek;
  pahalıysa yalnız Normal/Yüksek'te.
- Sis rengi gece mavi-gri yerine daha koyu ve az yoğun; otobüs dış kamerada sisin içinde kalmasın (Conecto ~21 m geride).
**Yerel**: üç otobüs × gündüz/akşam/gece × açık/kar, dış ve kokpit görüntüsü (önce/sonra), A32 ve S24 FPS.
**Kabul:** gece otobüsün kaplama rengi (kırmızı/mavi) ve logosu seçiliyor; gündüz renkler canlı; A32 Düşük'te FPS kaybı ≤ 2.

### 3. İç mekân dokuları ve gece iç aydınlatma (C, D)
**Bulut**
- Conecto iç dokuları (`conecto_ic.py`): yan paneller açık krem/bej, pencere altında koyu gri plastik bant, tavanda ışık şeritleri (gece
  emissive) ve reklam çerçeveleri, zeminde kapı önlerinde sarı uyarı şeridi, kapı çevresinde sarı kenar.
- BMC ve Millennium: iç tavan ışık panellerine gece emissive (`Lamba_Ic*` parçaları var; gerekirse malzeme ayrımı).
- `HavaVeZaman.IcIsik` (yerelin eklediği gövde başına ışık) kalsın; şiddet panel parlamasıyla birlikte ayarlanır.
**Yerel**: gece yolcu ve kokpit kamerası, üç otobüs; Düşük kalitede de.
**Kabul:** gece içeride renkler (koltuk, panel, zemin şeridi) seçiliyor, tavan ışıkları yanıyor.

### 4. Ertelenen testler (Y17 tablosu)
Menü (Karlı düğme, vitrin "ANKARA"), kar (gündüz/akşam/gece, Yüksek, Conecto), tutuş (BMC, Conecto), kar FPS, tabela/kamera (Millennium,
Türkçe harfler, BMC tavan şeridi, Conecto körük ağzı), anons (kulakla). Yukarıdaki 1–3 bitince tek turda.

## Bulut sırası
1. Conecto kapı kanatları + `BusDoor` Plug + kurucu (A).
2. Dolgu ışığı, lamba emissive, pencere parlaması, `M_Govde` rengi, sis (B, E).
3. İç dokular ve tavan ışıkları (C, D).
Her adım ayrı commit; her adımdan sonra yerel için kısa bir doğrulama promptu (`GOREVLER.md`).

## Riskler
- Kapı kanatlarını gövdeden ayırmak dış kaplamanın UV'sini bozmamalı (kanat yüzleri aynı UV'yi taşır, doku kanatla birlikte kayar).
  Kapı boşluğunun kenarında ince delik kalabilir → kasaya koyu çerçeve.
- Rendering layer ışığı A32'de ek çizim maliyeti: otobüs mesh'leri bir ışık daha alır; ölçülecek.
- Renk düzeltme (Volume) mobilde son işlem geçişi açar; pahalıysa yalnız malzeme rengi ve dolgu ışığıyla yetinilir.
