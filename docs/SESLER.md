# Otobüs Sesleri

Sesler BMC Procity modunun kendi ses paketinden alındı (kişisel prototip), `Buses/BMC_Procity_12LF/Sounds/` altında 29 wav dosyası var.
Oyunda sesleri `Scripts/Vehicle/BusAudio.cs` çalar. Prefaba **Ankara Bus → Otobüs Seslerini Kur** ile bağlanır; **BMC Procity Prefabını Kur** da bunu kendiliğinden yapar.

## Motor

Motor sesi, farklı devirlerde kaydedilmiş döngülerden oluşur. Her döngü kaydedildiği devirde (`nativeRpm`) çalınır. O anki devre en yakın iki kayıt, aradaki farkın logaritmasına göre eşit güçte (cos/sin) karışır; perde `rpm / nativeRpm` oranıyla ayarlanır (0.5–2 arası).

| Set | Dosyalar (devir) | Nerede |
|---|---|---|
| Kokpit | D_2566UH@575, @758, D_2566UHx@1214, D_2566UH@1939, @2230 | 2B, kabinin her yerinden aynı duyulur |
| Dış | D_2566UHx@722, @1214, @1653, @2296 | 3B, arkadaki motor konumunda (`enginePosition`) |
| Yük (dış) | D_2566UHx@1280_Last | gaz pedalıyla açılır |

Kamera kokpite geçince iç set, dışarı çıkınca dış set duyulur. Geçiş yaklaşık 0.3 sn sürer.
Motor çalışınca `D_2566UH_ein` sesi bir kez çalar. `D_2566UH_aus` (stop) şimdilik bağlı ama kullanılmıyor: oyunda henüz kontak kapatma yok.

## Diğer sesler

| Ses | Dosya | Ne zaman |
|---|---|---|
| Retarder | D_Retarder_257 | fren + hız (`RetarderLevel`) |
| Şanzıman uğultusu | Voith_Roll | hızla artar |
| Kabin tıkırtısı | Rattle | yalnızca kokpitte, hızla artar |
| Fren havası | D_bremse_treten / D_bremse_loesen | fren basıncı 0.25'i geçince / bırakılınca |
| El freni | PBrake_On / PBrake_Off | el freni değişince |
| Vites düğmesi | gangwahltaster | D/N/R seçilince |
| Geri vites uyarısı | reverse | R'deyken sürekli |
| Korna | KLAKSON | klavyede **H**, dokunmatikte **KORNA** butonu (basılı tuttukça) |
| Kapılar | DoorOpen1–3 / DoorClose1–3 | her kapı kendi konumunda, kendi sesiyle |
| Kentkart | IBIS_piep | her binen yolcu için ön kapıda |
| "Dur" zili | stop | duraktan çıkıştan 8–20 sn sonra (otobüste yolcu varsa) |

## İçe aktarma ayarları (Android)

`OtobusSesKurucu.ConfigureImporters` ayarları:
- **Codec:** Vorbis, kalite 0.6, örnekleme hızı optimize.
- **Yükleme:** 2 sn'den kısa sesler belleğe açık (Decompress On Load), uzun döngüler sıkıştırılmış halde bellekte (Compressed In Memory).
- **Kanal:** 3B konumlandırılan ve döngüsel sesler mono. Yalnızca motor çalıştırma/durdurma, kentkart ve "dur" zili stereo kalır.

Seviye ayarı için `BusAudio` bileşenindeki katman `volume` değerleri ve `masterVolume` kullanılır. Kurucuyu tekrar çalıştırmak bu değerleri sıfırlar; kalıcı bir değişiklik `OtobusSesKurucu.EngineLayers` içinde yapılmalı.
