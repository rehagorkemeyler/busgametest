# Oyun İçi Kamera

Oyun içi kamerayı `Scripts/Vehicle/BusCameraRig.cs` yönetir.
Görünümler arasında **KAMERA** düğmesiyle (klavyede **C**) sırayla geçilir. Düğmede o anki görünümün adı yazar.

| Görünüm | Ne gösterir | Sürükleyince | Bırakınca |
|---|---|---|---|
| **DIŞ** | arkadan takip | otobüsün çevresinde 360° döner, yukarı/aşağı eğilir | otobüs giderken 2 sn sonra yavaşça arkaya döner; dururken olduğu yerde kalır |
| **KOKPİT** | sürücü gözü (`SurucuGozu`) | başını çevirir (±160°, yukarı/aşağı) | 2 sn sonra yola döner |
| **YOLCU** | yolcu salonu, ayakta yolcu göz hizası | serbestçe etrafa bakar | olduğu yerde kalır |
| **KAPI** | sağ ön köşeden yan tarafa: kapılar ve kaldırım (durağa yanaşmak için) | etrafa bakar | 2 sn sonra kapılara döner |
| **SERBEST** | dış görünüm | DIŞ gibi döner | bırakılan açıda kalır (ör. yandan izleyerek sürmek) |

## Dokunmatik ve fare

- **Sürükleme:** Yalnızca arayüze (direksiyon, pedallar, düğmeler) değmeyen parmakla olur. Direksiyonu çevirirken öbür parmakla kamera sürüklenebilir.
- **Yakınlaştırma:** İki parmakla (pinch). Dış görünümlerde mesafe 7–40 m, içeride görüş açısı 35–85° arasında değişir.
- **Çift dokunuş:** Boş ekrana çift dokununca görünüm varsayılana döner.
- **Fare (Editor):** Sol ya da sağ tuşla sürükle, tekerlekle yakınlaştır, orta tuşla sıfırla.
- Dış görünümlerde kamera bina ya da yokuşun içine girmez (küre testi).

## Ayarlar

- **Konumlar:** `interiorPosition` (yolcu), `doorPosition`/`doorLookAt` (kapı) ve `cockpitFallback` (prefabda `SurucuGozu` yoksa kokpit) otobüsün yerel koordinatlarıdır.
- **Hassasiyet:** `dragDegrees`: ekran yüksekliği kadar sürükleme kaç derece döndürür.
- **Geri dönüş:** `returnDelay`: bırakıldıktan kaç saniye sonra kamera arkaya ya da yola döner.

Sesler kameraya göre değişir: KOKPİT ve YOLCU'da iç motor seti ve kabin tıkırtısı duyulur (`BusCameraRig.IsInside`).
