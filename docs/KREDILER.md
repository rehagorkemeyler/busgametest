# Krediler

Oyundaki CC BY lisanslı modeller kaynak gösterilerek kullanılır. Oyunda ana menüdeki **Krediler** düğmesi aynı bilgiyi gösterir
(`Scripts/UI/AnaMenu.cs → KredilerYazisi`); bu dosya değişirse orayı da güncelleyin.

## Mercedes-Benz O500M · Caio Millennium II ("piso baixo central")

- Model: **Marcos Elias Picão (MEP)** — www.explorando.com.br/mep3d, www.viamep.com
- Tekerlekler: Victor Ortega · Silecekler ve hız göstergesi: Luiz Felipe Bonamigo · Hız göstergesi dokusu: Dimitrius Caio Vespasiano
- Lisans: **CC BY 3.0** — https://creativecommons.org/licenses/by/3.0/
- Değişiklikler: mobil için sadeleştirme, Ankara kaplamaları (EGO kırmızı, EGO mavi, Özel Halk), parça ayrımı
  (`tools/blender/millennium_donustur.py`); körüklü versiyon bu modelden türetildi (`tools/blender/koruklu_yap.py`).
  Model hayran çalışmasıdır; marka adları tanıtım amaçlı.

## BMC Procity 12LF

- Proton Bus Simulator modu; üreticisinden izin alınarak kullanılıyor (docs/OTOBUS_BMC_PROCITY.md).
- Körüklü versiyon (BMC Procity 18) bu modelden türetildi (`tools/blender/koruklu_yap.py`).

## Türkçe yolcu sesi ("Durakta inecek var!") ve durak anonsları

- Piper TTS (https://github.com/rhasspy/piper), ses modeli `tr_TR-dfki-medium` (https://huggingface.co/rhasspy/piper-voices).
- Veri kümesi: DFKI MARY TTS Türkçe (https://github.com/marytts/dfki-ot-data/) — **CC BY-NC-SA 4.0** (ticari olmayan kullanım;
  oyun satılacaksa bu sesler değiştirilmeli). Üretim: `tools/ses/anons_tr.py`, `tools/ses/anonslar.py` (`Resources/Anonslar/`).

## Yolcular: Quaternius — Ultimate Modular Men (Feb 2022) ve Ultimate Modular Women (April 2022)

- Model: **Quaternius** — https://quaternius.com , https://www.patreon.com/quaternius
- Lisans: **CC0 1.0** (kamu malı; kaynak göstermek zorunlu değil) — https://creativecommons.org/publicdomain/zero/1.0/
- Değişiklikler: 14 kıyafet seçildi, parçalar tek mesh'e ve ortak renk paletine birleştirildi, Bekle/Yürü klipleri T-poz iskelete
  aktarıldı, oturma ve tutunma pozları eklendi (`tools/blender/yolcu_quaternius.py`). Kaynak: `tools/kaynak/karakterler/` (ICERIK.md).

## Hat 2 ve Hat 408 haritası (gerçek coğrafya)

- Yollar, bina ayak izleri, parklar, su yüzeyleri, duraklar, trafik ışıkları: **© OpenStreetMap katkıcıları**, lisans **ODbL 1.0**
  (https://www.openstreetmap.org/copyright). Ham veri `tools/harita/veri/osm_hat408.json.gz` (API 0.6 karoları, 14 Ekim 2026);
  oyundaki harita bu veriden üretilmiş bir eserdir (`tools/harita/hat3`).
- Arazi yükseklikleri: **AWS Terrain Tiles** (Mapzen; SRTM — NASA, Copernicus DEM ve diğer kaynaklar),
  https://registry.opendata.aws/terrain-tiles/ — kaynak göstererek serbest.
- 408 hattının durak listesi ve sırası: **EGO Genel Müdürlüğü** hareket saatleri sayfası (ego.gov.tr).
- Binaların cepheleri, ağaçlar, simge yapılar prosedürel/elle üretildi; fotoğraf ve uydu görüntüsü yalnız referans olarak
  incelendi, oyuna girmedi.
