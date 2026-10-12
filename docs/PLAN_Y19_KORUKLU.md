# Y19 planı — BMC ve Millennium'un körüklü versiyonları

Kullanıcı kararı (13 Ekim): körüklü otobüsler BMC Procity 12LF ve Caio Millennium II'nin **kendi dokulu modellerinden** üretilecek
(aynı kalite, aynı kaplamalar). Conecto menüden kalkar, dosyaları yerinde kalır (gerekirse geri alınır). Kaynak: `docs/RAPOR_Y18.md`,
`GOREVLER.md` Y19 (Conecto için yazılmıştı; maddelerinin karşılığı aşağıda), görüntüler `docs/gorseller/y18/`.

Hedef: iki körüklü otobüs, tek gövdeli eşleri kadar iyi görünür ve kusursuz çalışır. "Bitti" demek için aşağıdaki **kabul
ölçütlerinin hepsi** yerel testlerle (`Y18Kurulum.TestBatch -gorev kapi|gece` ve yeni `koruklu` görevi) ve telefonda doğrulanmış olmalı.

## 1. Tasarım

### 1.1 Ölçüler (Unity yerel: ön +Z, sağ +X; kökün sıfırı tek gövdelinin sıfırı)
| | BMC Procity 18 (körüklü) | Millennium II körüklü |
|---|---|---|
| Kaynak | BMC 12LF: boy −5,89 … 5,89; ön aks 2,93; arka aks −2,72; kapılar K1 3,99–5,27, K2 −0,55–0,73, K3 −4,95–−3,67 | Millennium: −6,54 … 6,54; ön aks 3,53; arka aks −3,12; K1 4,36–5,47, K2 −0,56–0,61 |
| Ön gövde | kaynak [−3,55 … 5,89] (K1, K2, ön aks, orta aks = kaynağın arka aksı) | kaynak [−4,00 … 6,54] (K1, K2, ön aks, orta aks) |
| Körük | 0,90 m, mafsal ortada (z −4,00) | 0,90 m, mafsal z −4,45 |
| Arka gövde | kaynak [−5,89 … 0,85] → z −5,30 kaydırılır: K2 kopyası = **K3**, kaynak K3 = **K4**, çeken aks (kaynağın arka aksı), arka yüz | kaynak [−6,54 … 0,80] → z −5,70: K2 kopyası = **K3**, çeken aks, arka yüz |
| Toplam boy | ≈ 17,1 m, 4 kapı | ≈ 18,8 m, 3 kapı |

Kesim düzlemleri kapı boşluklarına ve teker yuvalarına ≥ 10 cm uzak (kesim kapıdan/yuvadan geçmez).

### 1.2 Model aracı: `tools/blender/koruklu_yap.py` (tek araç, iki otobüs; ayarlar sözlükte)
Girdi: tek gövdelinin Unity'ye hazır FBX'i (mobil ve tam kalite ayrı ayrı). Çıktı: `<Ad>_G.fbx` ve `<Ad>_G_TamKalite.fbx`, kaynakla
**aynı klasörde** (malzemeler, dokular, kaplamalar ortak; çoğaltma yok).
1. **Kesme:** `Govde` (ve iç mekân) kesim düzleminde ikiye. Küçük adacıklar (koltuk, direk, tutamak, etiket) kesilmez: merkezi hangi
   taraftaysa bütün olarak o tarafa (kesik koltuk kalmaz). Büyük kabuklar (dış gövde, taban, tavan) `bisect` ile kesilir, UV korunur.
2. **Ön gövde** (`Govde_On`): kesim düzleminin önü. **Arka gövde** (`Govde_Arka`): kaynağın arka bölümü, kaydırılmış; pivot mafsalda.
3. **Kesim ağızları:** her gövdenin açık ucuna koyu kauçuk çerçeve (dış kontur = gövdenin kesit konturu, iç = geçiş açıklığı
   x ±1,05, taban … 2,45 m), iki yüzlü; içeriden ve dışarıdan delik/boşluk görünmez.
4. **Körük** (`Koruk`, pivot mafsalda): gövde kesitine oturan kıvrımlı akordeon (≈ 14 kıvrım, derinlik 5 cm), dış ve iç yüzey,
   dokulu kauçuk (`koruk.png`: kıvrım gölgeleri + ince doku), tavanda ve tabanda kapalı; içeride döner tabla (gri, kaymaz desen).
5. **Tekerler:** ön aks olduğu gibi; kaynağın arka aksı → `Teker_OrtaSol/OrtaSag`; kopyası arka gövdede `Teker_ArkaSol/ArkaSag`.
   Kaynağın dokulu tekerleri (sekizgen yok).
6. **Kapılar:** K1, K2 olduğu gibi; arka gövde kapıları kaynağın kanat kopyaları (`Kapi_3_1/2`, BMC'de `Kapi_4_1/2`), aynı menteşe
   pivotu. Kök boş objenin çocuğu (Conecto'daki 90° dönme hatası yok).
7. **Lambalar:** her `Lamba_*` kesimle bölünür: ön gövdedeki kısım aynı adla, arka gövdedeki kısım `Lamba_*_Arka` (stop: `Lamba_Fren_Arka`
   …). Kaynağın iç tavan ışıkları her iki gövdede.
8. **Direksiyon**, sürücü yeri ön gövdede aynen. **Gölge kabuğu** (`Golge_On`, `Golge_Arka`): kesilen kabuğun dışbükey zarfı.
9. **Kontrol:** kendini kesen/yozlaşmış poligon temizliği (`dissolve_degenerate`, `delete_loose`), normal yönü; Blender render
   (dış: sağ/sol/ön/arka/üç çeyrek, iç: yolcu kamerası ön → arka, körük içi; kapalı/açık kapı; kırık mafsal 30°) **her biri gözle**.

### 1.3 Unity tarafı
- `OtobusKurucu`: `Tanim.Klasor` (model kaynakla aynı klasörde) ve iki yeni tanım `BmcKoruklu`, `MillenniumKoruklu` (Koruklu, kaplamalar
  kaynağınkiyle aynı, fizik: 17–18 t, `trailerMassRatio`, ağırlık merkezi, arka ağırlık merkezi, motor sesi konumu). Gömülü
  malzemeler ortak `Materials/`'a eşlenir (`SearchAndRemapMaterials`; yinelenen malzeme yok).
- Kapılar: körüklüde de kaynağın menteşeli kanatları (`SetupDoors`, 4 grup); mafsalın gerisindeki kanatlara `BusDoor.takip` = arka gövde.
- `KorukluOtobus.arkaGorseller`: adı `_Arka` ile biten bütün parçalar (gövde, gölge, lambalar) otomatik.
- `HavaVeZaman`: lamba açma ve fren lambası `_Arka` eşlerini de kapsar; dolgu ışığı ve far zaten otobüs boyuna göre.
- `HatTabelasi.Bul`: iki yeni yerleşim (ön tabela kaynaktaki gibi, arka tabela arka gövdede).
- Katalog/menü: BMC, Millennium, BMC körüklü, Millennium körüklü (Conecto çıkar). Krediler: kaynak modellerin satırı (türetme).
- Kapı kamerası (yerelin Y18 bulgusu): `BusCameraRig` kapı kamerası açılan kanattan ve gövdeden en az 0,5 m dışarıda.

### 1.4 Conecto'ya ait Y19 maddelerinin karşılığı
| Y19 (Conecto) | Körüklü BMC/Millennium'da |
|---|---|
| 1 tekerlekler | kaynağın dokulu tekerleri |
| 2 kapı delikleri, gövde yüzü karışması, kendini kesen poligon | kaynağın ayrı kanatları; kesim kapıdan geçmez; temizlik + Unity içe aktarma uyarısız |
| 3 gölgelendirme, panel çizgileri | kaynağın normalleri ve dokuları |
| 4 körük | yeni kıvrımlı dokulu körük (1.2/4) |
| 5 ön/arka yüz | kaynağın ön/arka yüzü |
| 6 gece camları | kaynağın gerçek camları (içerisi görünür) + iç ışıklar |
| 7 iç mekân | kaynağın dokulu iç mekânı |

## 2. Adımlar (her biri ayrı commit; bulut Blender render'ıyla doğrular, yerel Unity/telefonda)
1. ✔ (bulut, 13 Ekim) **Model aracı + BMC körüklü FBX'leri** (mobil, tam kalite) + render seti. *Kabul (bulut):* render'larda kesik koltuk, delik, boşluk,
   ters yüz yok; kapılar yerinde; lambalar iki gövdede; tam kalite ve mobilde parça adları ve pivotlar aynı (betik karşılaştırır).
2. ✔ bulut kısmı (yerel doğrulama: GOREVLER Y19) **Unity kurulumu** (kurucu, kapılar, lambalar, tabela, katalog) + yerel prompt. *Kabul (yerel):* prefab hatasız/uyarısız kurulur;
   menüde seçilir; sürüş (kalkış, fren, viraj, Kuğulu → Cinnah, yokuş) mafsal titremesiz; 4 kapı açılıp kapanır, körük kırıkken
   3–4. kapı arka gövdeyle; yolcular 4 kapıdan iner; gece lambalar (ön/arka), fren, kaplamalar (3); Yüksek ↔ Düşük; A32 FPS ≥ BMC − 5.
3. ✔ bulut kısmı **Millennium körüklü** (aynı araç) + yerel doğrulama (aynı ölçütler, 3 kapı).
4. **Cila:** körük dokusu/kıvrım, kesim çerçevesi, ikinci filo numarası/etiket tekrarı (arka gövdede aynı yazı iki kez görünüyorsa
   kaplamada düzelt), kapı kamerası, iç ışıkların arka gövdede eşit dağılımı, LED tabela arka gövdede.
5. **Son tur:** üç zaman × iki hava (açık/kar) × iki körüklü × üç kaplama görüntü seti, sürüş testi, A32 ve S24 FPS, APK.
   Kullanıcı onayı → Conecto dosyaları `Buses/_Arsiv`'e taşınır (ya da silinir; kullanıcıya sorulur).

Bulut notu (13 Ekim): körük katı dönmek yerine iki kemikli deri (virajda bükülür) — 1.3'e ek. BMC'de tavandaki CNG kutusunun kuyruğu
arka gövdenin başına düşüyordu: silindi, yerine çatı levhası. Render kontrolleri: dış (sağ, sol, ön, arka, üst), körük yakın,
iç (ön gövde → arka), mafsal 30°.

## 3. Riskler ve önlemler
- **Kesim ağzı:** iç mekân kesitinde (taban altı, tavan arası) boşluk görünebilir → çerçeve konturu kesitten ölçülür, iç/dış ayrı yüz.
- **Atlas UV'si:** arka gövdede kopyalanan bölgedeki yazılar (filo no, etiketler) iki gövdede tekrar eder → kabul edilebilir
  (gerçekte de iki bölümde numara vardır); rahatsız ederse 4. adımda düzeltilir.
- **Fizik:** BMC aks açıklığı 5,65 m, mafsal orta aksın 1,28 m gerisinde; Conecto'nun mafsal sönümleri başlangıç değeri.
  Titreme/fırlama görülürse önce sönüm, sonra arka ağırlık merkezi.
- **Performans:** körüklü BMC ≈ 1,45 × BMC çokgen sayısı (mobil ≈ 130 bin) → A32 Düşük'te ölçülür; gerekirse arka gövde iç
  mekânı sadeleştirilir.
