# Puan ve Bilet Sistemi

Puan ve kasayı `Scripts/Gameplay/SeferPuanlama.cs` tutar; ekrandaki karşılığı `Scripts/UI/PuanGostergesi.cs`'tir.
İkisi de otobüs prefabının kökündedir ve **Ankara Bus → BMC Procity Prefabını Kur** ile eklenir.
Puanlama diğer sistemleri yalnızca okur: `BusVehicle`, `RouteTracker`, `BusPassengers` ve trafik ışıklı şeritler (`TrafficLane`).

## Puanlar

| Olay | Puan | Not |
|---|---|---|
| Binen yolcu (Kentkart) | +5 | %70 tam bilet (20 TL), %30 indirimli (10 TL) kasaya girer |
| Durağa yanaşma | +20 | |
| Yanaşma hassasiyeti | +0…30 | otobüs durak merkezine ne kadar yakınsa o kadar |
| Durak atlama | −100 | durağın yanından geçip 25 m uzaklaşınca; hat sıradaki durağa geçer |
| Sert fren | −10 × yük | yavaşlama > 3 m/sn², 10 km/s üstünde (duruşun son anındaki süspansiyon sıçraması sayılmaz) |
| Sert kalkış | −5 × yük | hızlanma > 1.8 m/sn² |
| Sert viraj | −8 × yük | yanal ivme > 2.5 m/sn² |
| Hız sınırı | −6 / 3 sn | 75 km/s üstü (sınır 70, tolerans 5) |
| Kırmızıda geçme | −50 | otobüsün önü, kırmızı ışıklı şeridin durma çizgisini aynı yönde geçerse (sarı sayılmaz); yan yana şeritler aynı geçişte tek ceza |
| Çarpışma | −(15 + 5 × hız), en çok −100 | zemin/kaldırım temasları sayılmaz |
| Zamanında bitiş | +100 | gecikilen her 3 sn için −1 |
| Konfor | +0…100 | hat sonunda eklenir |

- **Yük çarpanı:** `1 + yolcu / 30`. Dolu otobüste sarsıntının cezası daha büyüktür.
- **Konfor:** %100 başlar. Her sert hareket konforu düşürür.
- **Hedef süre:** Otobüsün başlangıç noktasından duraklar arasındaki düz mesafe 20 km/s ortalama hızla alınır, buna durak başına 40 sn eklenir.

## Yıldız

5 yıldızdan başlanır, şu düşüşler uygulanır:
- Atlanan her durak için −2.
- Her kırmızı ışık için −1.
- Her çarpışma için −1.
- Konfor %80'in altındaysa −1, %50'nin altındaysa bir −1 daha.
- Süre hedefi %30'dan fazla aşıldıysa −1.

Sonuç en az 1 yıldızdır.

En iyi puan hat numarasına göre `PlayerPrefs`'te tutulur (`EnIyiPuan_<hat>`).

## Ekran

- **Puan şeridi:** Üst ortadaki hız göstergesinin altında puan, kasa ve konfor.
- **Kısa mesajlar:** Şeridin altında 2.5 sn görünür, ör. "+5 Tam bilet" ya da "−10 Sert fren". Aynı mesaj üst üste gelirse toplanır (ör. "+25 Tam bilet").
- **Hat sonu:** Yıldızlar, ayrıntılı özet ve rekor bilgisi gösterilir. Altında **TEKRAR** (sahneyi yeniden yükler) ve **KAPAT** düğmeleri vardır.

Tüm eşikler ve ücretler `SeferPuanlama` bileşeninde Inspector'dan ayarlanabilir. Bilet ücretleri oyun değeridir, gerçek tarifeyle eşleşmesi gerekmez.

## Rota takibine eklenenler

- `RouteTracker.StopSkipped`: Durağın yanına gelip (yarıçap + 8 m) durmadan uzaklaşınca (yarıçap + 25 m) tetiklenir. Hat sıradaki durağa geçer, böylece oyuncu geri dönmek zorunda kalmaz.
- `BusStop.Radius`, `BusStop.HorizontalDistance`, `TrafficLane.HasSignal` ve `TrafficLane.IsRed` eklendi.
