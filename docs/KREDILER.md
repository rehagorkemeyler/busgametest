# Krediler

Oyundaki CC BY lisanslı modeller kaynak gösterilerek kullanılır. Oyunda ana menüdeki **Krediler** düğmesi aynı bilgiyi gösterir
(`Scripts/UI/AnaMenu.cs → KredilerYazisi`); bu dosya değişirse orayı da güncelleyin.

## Mercedes-Benz O500M · Caio Millennium II ("piso baixo central")

- Model: **Marcos Elias Picão (MEP)** — www.explorando.com.br/mep3d, www.viamep.com
- Tekerlekler: Victor Ortega · Silecekler ve hız göstergesi: Luiz Felipe Bonamigo · Hız göstergesi dokusu: Dimitrius Caio Vespasiano
- Lisans: **CC BY 3.0** — https://creativecommons.org/licenses/by/3.0/
- Değişiklikler: mobil için sadeleştirme, Ankara kaplamaları (EGO kırmızı, EGO mavi, Özel Halk), parça ayrımı
  (`tools/blender/millennium_donustur.py`). Model hayran çalışmasıdır; marka adları tanıtım amaçlı.

## Mercedes-Benz O530G Conecto (körüklü)

- Model: **"Zort"** — https://sketchfab.com/privatetrs1
- Kaynak: https://sketchfab.com/3d-models/mercedes-benz-conecto-ceb9e135aedc4b60a8d815fb54920129
- Lisans: **CC BY 4.0** — http://creativecommons.org/licenses/by/4.0/
- Değişiklikler: dokular kaynakta yoktu; yan yüzlere yeni UV, düz renk malzemeler, ön/arka gövde, körük ve teker ayrımı,
  sadeleştirme (`tools/blender/conecto_donustur.py`).

## BMC Procity 12LF

- Proton Bus Simulator modu; üreticisinden izin alınarak kullanılıyor (docs/OTOBUS_BMC_PROCITY.md).

## Türkçe yolcu sesi ("Durakta inecek var!") ve durak anonsları

- Piper TTS (https://github.com/rhasspy/piper), ses modeli `tr_TR-dfki-medium` (https://huggingface.co/rhasspy/piper-voices).
- Veri kümesi: DFKI MARY TTS Türkçe (https://github.com/marytts/dfki-ot-data/) — **CC BY-NC-SA 4.0** (ticari olmayan kullanım;
  oyun satılacaksa bu sesler değiştirilmeli). Üretim: `tools/ses/anons_tr.py`, `tools/ses/anonslar.py` (`Resources/Anonslar/`).
