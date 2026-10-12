# Y17 raporu (yerel oturum, 12 Ekim 2026) — ARA RAPOR, TESTLER ERTELENDİ

> **Kullanıcı kararı:** oyuna otopilot eklenmeyecek, otopilotlu test yok. Kar testleri (FPS tekrarı, tutuş, görüntü) ve Y16'dan
> kalanlar, bulut `GOREVLER.md` → "Y17 — bulut için" işlerini (gri iç/dış, Conecto kapıları) bitirince **hep birlikte** test edilecek.
> A32'de ve `~/Desktop/AnkaraOtobus.apk`'de oyun APK'sı (otopilotsuz) kurulu.

> Telefon testleri **kullanıcının onayını bekliyor** (kullanıcı bir süre bilgisayar başında değil). Bu ara rapor yalnızca Mac'teki
> hazırlığı içerir; 1–5. maddelerin sonuçları telefon testlerinden sonra eklenecek.

## Hazırlık
- `git pull origin Ozan` (01f72ab, bulut: kar) → derleme hatası yok.
- **Hava ve Gece Malzemelerini Kur** (batch): Konsol "[HavaZaman] Malzemeler, yağmur ve kar prefabları kuruldu". Yeni: `M_AnkaraPalet_Karli`,
  `M_AnkaraPalet_GeceKarli`, `M_Kar`, `T_KarTanesi`, `Kar.prefab`. (`M_Yagmur` bu kurulumla saydam işaretlendi: RenderType Transparent,
  gölge/derinlik geçişi kapalı; önceki kayıt Opaque görünüyordu.)
- `Resources/Hava/T_AnkaraPalet_Karli.png`: süzgeç **Point**, sıkıştırma **yok**, mipmap yok; Android/Standalone için ayrı ayar yok
  (varsayılan geçerli) ✔.
- **Mercedes Conecto (Körüklü) Prefabını Kur** (batch): gövde kutuları Y16 ile aynı (ön 2.50×2.75×10.67, arka 2.50×2.75×7.61).

## APK'lar
- Oyun: `~/Desktop/AnkaraOtobus.apk` (Y17 hazırlığıyla, kar dahil). Test: `Builds/AnkaraBus_y11_test.apk` (yeni görevlerle). İkisi de 0 hata.

## Kod
- **Kar sürerken kayboluyordu (düzeltildi, telefonda doğrulanacak):** kar taneleri dünya uzayında, kameranın 10 m üstünden ~1,25 m/sn'yle
  iniyor; göz hizasına ~5–6 sn'de varıyor. Bu sürede otobüs 50 km/s'de ~80 m ilerliyor, yayıcı kutusu ise 50×50 m: sürerken kameranın
  çevresindeki taneler geride kalıyordu. `HavaVeZaman` yayıcıyı otobüsün yatay hızı × 4,5 sn kadar ileri kaydırıyor (en çok 80 m).
  (Yağmur 20 m/sn düştüğü için bu sorun yağmurda yok.)
- Test başlatıcısı (`Y10Baslatici`): yeni intent'ler `-e hava Acik|Yagmur|Kar` (eski `yagmur`'u ezer) ve `-e kaplama N`.
- Yeni görevler:
  - `-e gorev kar` (`Diagnostics/KarTesti.cs`): hava durumu satırı (tutuş, sis, gök, bina palet malzemesi, parçacık ayarları, rüzgâr sesi);
    ilk ve son durakta dış ve kokpit kamerasıyla 10'ar sn FPS + görüntü + kameranın 15 m çevresindeki tane sayısı + rüzgâr düzeyi; sonra
    trafik kaldırılıp 40 km/s sürerken aynı ölçüm. Hat 1 → Hat 2. Satırlar `[Kar]`, görüntüler `y17_kar_*.png`.
  - `-e gorev tutus` (`Diagnostics/TutusTesti.cs`): tutuş 1 / 0,85 / 0,6 için Atatürk Bulvarı'nda 50 km/s'den tam fren (mesafe, süre,
    kayma, şeritten sapma), Cinnah'ın en dik 20 m'sinde duruştan kalkış (tam ve yarım gaz: geri kayma, patinaj, 10 km/s'ye süre),
    Bulvar → Cinnah sağa dönüşü 25 ve 35 km/s (şeritten sapma, kayma açısı, dönüş hızı). Satırlar `[Tutus]`.

## A32 sonuçları (SM-A325F, Düşük kalite, Conecto, gece) — sürüyor
Kullanıcı A32'yi bağladı (onay). A32 Düşük'te kare hızı **45'te sınırlı** (90 Hz ekranın yarısı), ölçümler buna göre.

| Yer / kamera | Açık gece | Karlı gece | Karlı gece, yeni ışık (telefon ısınmış, termal 1) |
|---|---|---|---|
| Hat 1 Kızılay AVM dış | 44,8 | 42,4 | 34,7 |
| Hat 1 Kızılay AVM kokpit | 41,4 | 42,0 (kamera SERBEST'te kaldı) | 23,9 |
| Hat 1 Atakule dış | 45,0 | 33,8 (bir kez 2 sn takılma) | 44,9 |
| Hat 1 Atakule kokpit | 45,0 | 45,0 | 41,7 |
| Hat 2 Kızılay dış | 45,0 | 45,0 | 35,7 |
| Hat 2 Kızılay kokpit | 42,0 | 41,5 | 28,8 |
| Hat 2 Ulus dış / kokpit | 45,0 / 45,0 | 45,0 / 45,0 | 45,0 / 43,2 |

- Üçüncü koşu ısınmış telefonda (AP 41,9 °C, pil 27 → 35 °C, termal durum 1): ilk iki koşuyla karşılaştırılamaz, soğuyunca yinelenecek.
- **Kar sürerken:** 41 km/s'de kameranın 15 m çevresinde 154–228 tane (dururken 183–432); yayıcı 51 m ileri kayıyor. Kar sürerken de yağıyor.
- **Rüzgâr:** dışarıda 0,30, içeride 0,08 ✔.
- **Kar taneleri ekran görüntüsünde kare** görünüyor, ama kullanıcı telefonda **beyaz ve yuvarlak** gördüğünü söyledi: yalnızca
  `ScreenCapture.CaptureScreenshotAsTexture` görüntüsünde (saydamlık yakalamada kayboluyor). Kar malzemesine dokunulmadı.
- Görüntüde: kaldırımlar ve çatılar beyaz, asfalt koyu, pencereler yanıyor, ağaçlar karlı.

## Kullanıcının telefonda gördükleri (bulut görevleri `GOREVLER.md` → "Y17 — bulut için")
- **Gece otobüs çok gri.** Neden: hareketli nesneler gece yalnızca 25 m arayla dizilmiş ışık problarından (seyrek lamba + kısık gök) ve
  mavi, kısık ay ışığından aydınlanıyor; karda ayrıca sis 15 m'de başlıyor, dış kamera Conecto'da ~21 m geride → otobüs sisin içinde.
  **Yerelde yapıldı:** gece probları ×1,8, akşam ×1,25 (`IsikSeti.Uygula(hareketliCarpan)`; binalar lightmap'le aydınlandığı için
  etkilenmez, maliyeti yok), karlı sis 30 m'den başlıyor. Sonrasında Conecto'nun yan yüzünde kırmızı EGO kaplaması seçiliyor; arka yüz
  dokusu kendisi açık gri.
- **Gece iç mekân gri:** tek iç ışık sürücünün arkasındaydı (menzil 9 m); 18 m'lik Conecto'nun ortası ve arkası loş problarla
  kalıyordu. **Yerelde yapıldı:** her gövdenin (körüklüde ön ve arka) tavanı altında, ortasında bir ışık, menzil gövde boyunun
  %75'i (en az 9 m), şiddet 1,6 → 2 (`HavaVeZaman.IcIsik`). A32 görüntüsünde körüğe kadar iç aydınlık; duvarlar dokusuz düz gri (bulut).
- Kullanıcı: gündüz de biraz soluk, gece çok daha belirgin. Bu düzeltmelerle oyun APK'sı A32'ye kuruldu, kullanıcı gece bakacak.
- **Conecto kapıları çalışmıyor, kapı yerinden içerisi görünüyor:** modelde kapı kanadı yok, kurucu yalnızca boş kapı noktaları koyuyor.
- **İç mekân koltuklar dışında gri, kaplamasız:** iç duvar/tavan dokusuz düz renk (`conecto_ic.py`).

## Ertelenen telefon test planı (bulut düzeltmelerinden sonra; otopilot maddesi çıkarıldı)
| # | Görev | Ayarlar |
|---|---|---|
| 1 | `menu` | Karlı seçili menü (üç düğme sığıyor mu, vitrinde "ANKARA") |
| 1 | `kar` | gündüz / akşam / gece, Yüksek, Conecto |
| 2 | `tutus` | BMC ve Conecto |
| 3 | `kar` | gece, Yüksek (FPS satırları) |
| 4 | `tabela`, `kamera` | Millennium tabelası, Türkçe harfler, BMC tavan şeridi, Conecto körük ağzı (mavi ve kırmızı kaplama) |
| 5 | `anons` | kulakla (kullanıcı) |

## Değişen dosyalar
- `Gameplay/HavaVeZaman.cs` (kar yayıcısı ileri kayar)
- `Diagnostics/Y10Baslatici.cs`, `Diagnostics/Y11Hat.cs`; yeni `Diagnostics/KarTesti.cs`, `Diagnostics/TutusTesti.cs`
- `Resources/Hava/*` (kurulum), `Buses/MB_Conecto_G/MB_Conecto_G.prefab` (yeniden kuruldu)
