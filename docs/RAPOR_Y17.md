# Y17 raporu (yerel oturum, 12 Ekim 2026) — ARA RAPOR

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

## Telefon test planı (onay bekliyor)
| # | Görev | Ayarlar |
|---|---|---|
| 1 | `menu` | Karlı seçili menü (üç düğme sığıyor mu, vitrinde "ANKARA") |
| 1 | `kar` | gündüz / akşam / gece, Yüksek, Conecto |
| 2 | `tutus` | BMC ve Conecto |
| 2 | `y10` (otomatik pilot) | `-e hava Kar -e otobus 0` ve `2` |
| 3 | `kar` | gece, Yüksek (FPS satırları) |
| 4 | `tabela`, `kamera` | Millennium tabelası, Türkçe harfler, BMC tavan şeridi, Conecto körük ağzı (mavi ve kırmızı kaplama) |
| 5 | `anons` | kulakla (kullanıcı) |

## Değişen dosyalar
- `Gameplay/HavaVeZaman.cs` (kar yayıcısı ileri kayar)
- `Diagnostics/Y10Baslatici.cs`, `Diagnostics/Y11Hat.cs`; yeni `Diagnostics/KarTesti.cs`, `Diagnostics/TutusTesti.cs`
- `Resources/Hava/*` (kurulum), `Buses/MB_Conecto_G/MB_Conecto_G.prefab` (yeniden kuruldu)
