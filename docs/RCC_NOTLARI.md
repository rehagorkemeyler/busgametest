# Realistic Car Controller Notları

> **8 Ekim 2026:** RCC Lite projeden kaldırıldı. Otobüs için gereken ayarlar Lite'ta kilitli olduğundan kendi sürüş kodumuz `BusVehicle` yazıldı. Bu notlar ileride RCC Pro'ya dönülürse diye duruyor.

## Kurulu sürüm

**Realistic Car Controller Pro Lite** (BoneCracker Games, ücretsiz deneme sürümü)
Konum: `AnkaraBusSimulator/Assets/Realistic Car Controller Pro/`

- Çalışma zamanı kodu derlenmiş DLL (`Plugins/RCCP_Lite.dll`), kaynak kodu yok.
- Oyunda Pro ile birebir aynı çalışır. Fark yalnızca Editor'de: gelişmiş ayarlar inspector'da kilitli (gri).
- Lite'tan Pro'ya geçiş: klasörü sil, Pro'yu import et. Bileşen tipleri ve alan adları aynı olduğu için sahne ve prefab bağlantıları korunur.

## Repoya girmez

Lisans (madde 3 ve 6) paketin dağıtımını yasaklıyor ve repo **public**. Bu yüzden klasör `AnkaraBusSimulator/.gitignore` ile dışarıda tutuluyor.

- Her geliştirici paketi Unity → Window → Package Manager → **My Assets** → Realistic Car Controller Pro Lite → Import ile kendisi kurar.
- Asset Store paketlerinin GUID'leri sabittir. Bizim prefablarımızdaki RCC referansları herkeste aynı dosyalara bağlanır.
- RCC klasöründeki dosyaları **düzenlemiyoruz**: değişiklikler git'e girmez, diğer geliştiriciye ulaşmaz ve güncellemede kaybolur. Gereken ayarları kendi script'lerimizden çalışma zamanında yapıyoruz (aşağıda).

## Lite'ta kilitli olanlar (otobüs için önemli)

| Ayar | Lite |
|---|---|
| Azami hız, tork, devir aralığı | açık |
| Şanzıman tipi (otomatik/manuel/CVT) | açık |
| ABS/ESP/TCS aç-kapa | açık |
| Rigidbody kütlesi (Unity bileşeni) | açık |
| **Ağırlık merkezi** | kilitli |
| **Vites oranları, son tahrik oranı** | kilitli |
| **Direksiyon açısı, fren torku** | kilitli |
| **Lastik sürtünmesi, süspansiyon/anti-roll** | kilitli |
| **Kamera ve ses ayarları** | kilitli |
| **Bileşen ekleme/çıkarma, araç oluşturma sihirbazı** | yok |

Lite'ta yeni araç kurmanın tek yolu, demo sahnedeki prototip aracı (Skyline) kopyalayıp modelini değiştirmek. Ancak spor araba için ayarlanmış vites oranları, fren, direksiyon açısı ve ağırlık merkezi değiştirilemiyor. 12 tonluk bir otobüs için bunlar uygun değil.

## Demo içerik ve build

- Demo sahnesi: `Scenes/RCCP_Scene_Blank_Prototype.unity`. Build Settings'teki sahne listesi boş olduğu için build'e girmez. Bizim sahnelerimiz listeye eklenirken bu sahne eklenmemeli.
- Örnek araç: `Prefabs/Prototype/Model_Skyline… (Prototype).prefab`, `Models/Prototype Vehicle/`.
- `Resources/` altındaki her şey build'e girer. `RCCP_Settings.asset` çalışma zamanında buradan yüklendiği için bu klasör gerekli. `RCCP_DemoVehicles` ve `RCCP_PrototypeContent` prototip aracı referans ettiği için onu da build'e çeker (birkaç MB). APK boyutu sorun olursa Faz 4'te bakılacak.
- `Documentation/` HTML dosyaları build'e girmez.
- Import sırasında `com.unity.editorcoroutines` paketi `Packages/manifest.json`'a eklendi; RCC'nin Editor araçları kullanıyor.

## Mobil kontroller

Ayarlar `Resources/RCCP_Settings.asset` içinde (Tools → BoneCracker Games → RCCP → RCCP Settings):

| Alan | Şu anki | Anlamı |
|---|---|---|
| `mobileControllerEnabled` | 0 (kapalı) | Dokunmatik arayüzü açar |
| `mobileController` | 0 (TouchScreen) | TouchScreen / Gyro / SteeringWheel / Joystick |

Modlar:

| Mod | Arayüz |
|---|---|
| TouchScreen | Gaz, fren, sol, sağ butonları |
| Gyro | Gaz/fren butonları, direksiyon telefonu eğerek |
| SteeringWheel | Sanal direksiyon + pedallar (otobüs için önerilen) |
| Joystick | Sanal joystick + gaz/fren |

Ayar dosyası repoda olmadığından mobil kontrolleri bir bootstrap script'inden açacağız:

```csharp
RCCP_Settings.Instance.mobileControllerEnabled = true;
RCCP.SetMobileController(RCCP_Settings.MobileController.SteeringWheel);
```

Arayüz (`Prefabs/UI/RCCP_Canvas.prefab`, `RCCP_UIManager`) oyuncu aracı sahneye kaydolunca otomatik oluşturulur. Gösterge, vites ve mobil kontroller bunun içinde. Elle eklemek için: Tools → BoneCracker Games → RCCP → Create → Add UI Canvas.

**Kapı butonu:** RCC'nin canvas prefabını değiştirmek yerine `Assets/_Project/UI/` altında kendi canvas'ımızı kuracağız. Kapı butonu `BusDoorController.ToggleAll`'ı çağıracak. Böylece RCC güncellense ya da Pro'ya geçilse bile buton kaybolmaz.

## Kodla kontrol

Kendi script'lerimizden kullanılabilecek API (yolcu/durak sistemi, AI trafik için):

```csharp
vehicle.Inputs.OverrideInputs(new RCCP_Inputs { throttleInput = 0f, brakeInput = 1f });
vehicle.Inputs.DisableOverrideInputs();
vehicle.canControl = false; // örn. kapılar açıkken sürüşü kilitlemek için
```
