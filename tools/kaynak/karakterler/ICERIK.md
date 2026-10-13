# Yolcu modelleri kaynak paketi (Y20 hazırlık, 13 Ekim 2026)

**Paket:** Quaternius — *Ultimate Modular Men* (Feb 2022) ve *Ultimate Modular Women* (April 2022). https://quaternius.com , https://www.patreon.com/quaternius
**Lisans: CC0 1.0 (kamu malı)** — public repoda dağıtılabilir; kaynak göstermek zorunlu değil (yine de KREDILER'e yazılabilir).
Kaynak: kullanıcının Mac'inde `~/Downloads/Ultimate Modular Men- Feb 2022-20261003T201618Z-1-001.zip` (54,5 MB) ve
`Ultimate Modular Women - April 2022-20261003T201653Z-1-001.zip` (45,7 MB).

## Neler alındı, neler alınmadı
Pakette dört kullanım biçimi var (`How To Use.txt`): *All together*, *Humanoid Rig(s)*, *Individual Characters*, *Separate Skeletal Meshes and Animations*.
- **Alındı (FBX):** `Humanoid Rig(s)/Individual Characters/FBX` — her karakter tek parça, Unity Humanoid'e uygun iskelet, **animasyonsuz**;
  `Separate Skeletal Meshes and Animations` — her kıyafetin Head/Body/Legs/Feet parçaları (+ aksesuar) ve **ortak `Animations.fbx`**
  (24 klip, aşağıda); kadında ayrıca `Humanoid Rigs/All Together/Rig.fbx`. Lisans, kullanım notu, önizleme (`Preview.jpg`).
- **Alınmadı (repo şişmesin):** `.blend` (~165 MB), `.gltf` (~65 MB), `Individual Characters/FBX` (~185 MB: her karakterde aynı 24
  animasyonun kopyası; aynı içerik Humanoid Rig FBX + Animations.fbx ile elde edilir), `All together` (tüm kıyafetler tek dosyada).
  Gerekirse kullanıcının zip'lerinden çıkarılabilir.
- Dokular: paket ayrı doku dosyası içermiyor (malzemeler FBX içinde düz renk/renk paleti).

## Animasyonlar (`Animations.fbx`, erkek ve kadında aynı 24 klip; erkekte adlar `CharacterArmature|` önekli)
Death, Gun_Shoot, HitRecieve, HitRecieve_2, Idle, Idle_Gun, Idle_Gun_Pointing, Idle_Gun_Shoot, Idle_Neutral, Idle_Sword, Interact,
Kick_Left, Kick_Right, Punch_Left, Punch_Right, Roll, Run, Run_Back, Run_Left, Run_Right, Run_Shoot, Sword_Slash, **Walk**, Wave.

**Oturma (Sit/Sitting) animasyonu YOK.** Koltuğa oturan yolcu için oturma pozu/animasyonu Blender'da üretilmeli (ör. Idle_Neutral'dan
kalça ~90°, diz ~90° bükülerek). `Humanoid Rig` FBX'ler Unity Humanoid'e retarget edilebilir (Mixamo vb. oturma klipleri), ama
Mixamo lisansı repoya dağıtıma izin vermez: oturma pozunu kendimiz yapalım. Kadında `Gun.fbx`/`Sword.fbx`, erkekte `Weapons` ve `Horse` yolcu için gereksiz.

## Lisans dosyası (tam metin; iki pakette aynı metin, başlık "Ultimate Modular Males")
```
------------------------------------------------------
Ultimate Modular Males by @Quaternius
Consider supporting me on Patreon, even $1 helps me a lot!

https://www.patreon.com/quaternius
-------------------------------------------------------

License:
CC0 1.0 Universal (CC0 1.0) 
Public Domain Dedication
https://creativecommons.org/publicdomain/zero/1.0/

```

## Dosyalar (boyut, bayt)
```
     1224  ./erkek/How To Use.txt
   582604  ./erkek/Humanoid Rig/Individual Characters/FBX/Adventurer.fbx
   447948  ./erkek/Humanoid Rig/Individual Characters/FBX/Beach.fbx
   439948  ./erkek/Humanoid Rig/Individual Characters/FBX/Casual.fbx
   429964  ./erkek/Humanoid Rig/Individual Characters/FBX/Casual2.fbx
   426492  ./erkek/Humanoid Rig/Individual Characters/FBX/Farmer.fbx
   571196  ./erkek/Humanoid Rig/Individual Characters/FBX/King.fbx
   425484  ./erkek/Humanoid Rig/Individual Characters/FBX/Punk.fbx
   537932  ./erkek/Humanoid Rig/Individual Characters/FBX/Spacesuit.fbx
   451980  ./erkek/Humanoid Rig/Individual Characters/FBX/Suit.fbx
   481820  ./erkek/Humanoid Rig/Individual Characters/FBX/Swat.fbx
   420604  ./erkek/Humanoid Rig/Individual Characters/FBX/Worker.fbx
      372  ./erkek/License.txt
   360869  ./erkek/Preview.jpg
   180508  ./erkek/Separate Skeletal Meshes and Animations/Adventurer/Adventurer_Backpack.fbx
   232604  ./erkek/Separate Skeletal Meshes and Animations/Adventurer/Adventurer_Body.fbx
   155932  ./erkek/Separate Skeletal Meshes and Animations/Adventurer/Adventurer_Feet.fbx
   223580  ./erkek/Separate Skeletal Meshes and Animations/Adventurer/Adventurer_Head.fbx
   163212  ./erkek/Separate Skeletal Meshes and Animations/Adventurer/Adventurer_Legs.fbx
  7695388  ./erkek/Separate Skeletal Meshes and Animations/Animations.fbx
   225980  ./erkek/Separate Skeletal Meshes and Animations/Beach/Beach_Body.fbx
   162556  ./erkek/Separate Skeletal Meshes and Animations/Beach/Beach_Feet.fbx
   195692  ./erkek/Separate Skeletal Meshes and Animations/Beach/Beach_Head.fbx
   167100  ./erkek/Separate Skeletal Meshes and Animations/Beach/Beach_Legs.fbx
   226348  ./erkek/Separate Skeletal Meshes and Animations/Casual_2/Casual2_Body.fbx
   158492  ./erkek/Separate Skeletal Meshes and Animations/Casual_2/Casual2_Feet.fbx
   183836  ./erkek/Separate Skeletal Meshes and Animations/Casual_2/Casual2_Head.fbx
   163260  ./erkek/Separate Skeletal Meshes and Animations/Casual_2/Casual2_Legs.fbx
   234620  ./erkek/Separate Skeletal Meshes and Animations/Casual_Hoodie/Casual_Body.fbx
   157852  ./erkek/Separate Skeletal Meshes and Animations/Casual_Hoodie/Casual_Feet.fbx
   186396  ./erkek/Separate Skeletal Meshes and Animations/Casual_Hoodie/Casual_Head.fbx
   164492  ./erkek/Separate Skeletal Meshes and Animations/Casual_Hoodie/Casual_Legs.fbx
   244300  ./erkek/Separate Skeletal Meshes and Animations/Farmer/Farmer_Body.fbx
   154956  ./erkek/Separate Skeletal Meshes and Animations/Farmer/Farmer_Feet.fbx
   166060  ./erkek/Separate Skeletal Meshes and Animations/Farmer/Farmer_Head.fbx
   166732  ./erkek/Separate Skeletal Meshes and Animations/Farmer/Farmer_Legs.fbx
   168652  ./erkek/Separate Skeletal Meshes and Animations/Horse/Horse_Head.fbx
   287436  ./erkek/Separate Skeletal Meshes and Animations/King/King_Body.fbx
   157564  ./erkek/Separate Skeletal Meshes and Animations/King/King_Feet.fbx
   226316  ./erkek/Separate Skeletal Meshes and Animations/King/King_Head.fbx
   191340  ./erkek/Separate Skeletal Meshes and Animations/King/King_Legs.fbx
   232492  ./erkek/Separate Skeletal Meshes and Animations/Punk/Punk_Body.fbx
   158348  ./erkek/Separate Skeletal Meshes and Animations/Punk/Punk_Feet.fbx
   176652  ./erkek/Separate Skeletal Meshes and Animations/Punk/Punk_Head.fbx
   164140  ./erkek/Separate Skeletal Meshes and Animations/Punk/Punk_Legs.fbx
   263148  ./erkek/Separate Skeletal Meshes and Animations/SpaceSuit/SpaceSuit_Body.fbx
   158412  ./erkek/Separate Skeletal Meshes and Animations/SpaceSuit/SpaceSuit_Feet.fbx
   236556  ./erkek/Separate Skeletal Meshes and Animations/SpaceSuit/SpaceSuit_Head.fbx
   174412  ./erkek/Separate Skeletal Meshes and Animations/SpaceSuit/SpaceSuit_Legs.fbx
   232604  ./erkek/Separate Skeletal Meshes and Animations/Suit/Suit_Body.fbx
   157132  ./erkek/Separate Skeletal Meshes and Animations/Suit/Suit_Feet.fbx
   199580  ./erkek/Separate Skeletal Meshes and Animations/Suit/Suit_Head.fbx
   163212  ./erkek/Separate Skeletal Meshes and Animations/Suit/Suit_Legs.fbx
   235788  ./erkek/Separate Skeletal Meshes and Animations/Swat/Swat_Body.fbx
   156716  ./erkek/Separate Skeletal Meshes and Animations/Swat/Swat_Feet.fbx
   198988  ./erkek/Separate Skeletal Meshes and Animations/Swat/Swat_Head.fbx
   188716  ./erkek/Separate Skeletal Meshes and Animations/Swat/Swat_Legs.fbx
   169404  ./erkek/Separate Skeletal Meshes and Animations/Weapons/Pistol.fbx
   163356  ./erkek/Separate Skeletal Meshes and Animations/Weapons/Sword.fbx
   234652  ./erkek/Separate Skeletal Meshes and Animations/Worker/Worker_Body.fbx
   156572  ./erkek/Separate Skeletal Meshes and Animations/Worker/Worker_Feet.fbx
   168348  ./erkek/Separate Skeletal Meshes and Animations/Worker/Worker_Head.fbx
   165692  ./erkek/Separate Skeletal Meshes and Animations/Worker/Worker_Legs.fbx
     1224  ./kadin/How To Use.txt
  3787612  ./kadin/Humanoid Rigs/All Together/FBX/Humanoid Rig.fbx
   439148  ./kadin/Humanoid Rigs/Individual Characters/FBX/Adventurer.fbx
   428380  ./kadin/Humanoid Rigs/Individual Characters/FBX/Casual.fbx
   413996  ./kadin/Humanoid Rigs/Individual Characters/FBX/Formal.fbx
   430764  ./kadin/Humanoid Rigs/Individual Characters/FBX/Medieval.fbx
   420124  ./kadin/Humanoid Rigs/Individual Characters/FBX/Punk.fbx
   445612  ./kadin/Humanoid Rigs/Individual Characters/FBX/SciFi.fbx
   456508  ./kadin/Humanoid Rigs/Individual Characters/FBX/Soldier.fbx
   430396  ./kadin/Humanoid Rigs/Individual Characters/FBX/Suit.fbx
   441788  ./kadin/Humanoid Rigs/Individual Characters/FBX/Witch.fbx
   420396  ./kadin/Humanoid Rigs/Individual Characters/FBX/Worker.fbx
      372  ./kadin/License.txt
   286665  ./kadin/Preview.jpg
   160524  ./kadin/Separate Skeletal Meshes and Animations/Adventurer/Adventurer_Backpack.fbx
   219772  ./kadin/Separate Skeletal Meshes and Animations/Adventurer/Adventurer_Body.fbx
   149852  ./kadin/Separate Skeletal Meshes and Animations/Adventurer/Adventurer_Feet.fbx
   147996  ./kadin/Separate Skeletal Meshes and Animations/Adventurer/Adventurer_Head.fbx
   165708  ./kadin/Separate Skeletal Meshes and Animations/Adventurer/Adventurer_Legs.fbx
  7671276  ./kadin/Separate Skeletal Meshes and Animations/Animations.fbx
   209036  ./kadin/Separate Skeletal Meshes and Animations/Casual/Casual_Body.fbx
   144764  ./kadin/Separate Skeletal Meshes and Animations/Casual/Casual_Feet.fbx
   188860  ./kadin/Separate Skeletal Meshes and Animations/Casual/Casual_Head.fbx
   148700  ./kadin/Separate Skeletal Meshes and Animations/Casual/Casual_Legs.fbx
   209740  ./kadin/Separate Skeletal Meshes and Animations/Formal/Formal_Body.fbx
   143804  ./kadin/Separate Skeletal Meshes and Animations/Formal/Formal_Feet.fbx
   181564  ./kadin/Separate Skeletal Meshes and Animations/Formal/Formal_Head.fbx
   144604  ./kadin/Separate Skeletal Meshes and Animations/Formal/Formal_Legs.fbx
    95676  ./kadin/Separate Skeletal Meshes and Animations/Gun.fbx
   228636  ./kadin/Separate Skeletal Meshes and Animations/Medieval/Medieval_Body.fbx
   145292  ./kadin/Separate Skeletal Meshes and Animations/Medieval/Medieval_Feet.fbx
   178092  ./kadin/Separate Skeletal Meshes and Animations/Medieval/Medieval_Head.fbx
   147180  ./kadin/Separate Skeletal Meshes and Animations/Medieval/Medieval_Legs.fbx
   223660  ./kadin/Separate Skeletal Meshes and Animations/Punk/Punk_Body.fbx
   148668  ./kadin/Separate Skeletal Meshes and Animations/Punk/Punk_Feet.fbx
   165036  ./kadin/Separate Skeletal Meshes and Animations/Punk/Punk_Head.fbx
   150796  ./kadin/Separate Skeletal Meshes and Animations/Punk/Punk_Legs.fbx
   228588  ./kadin/Separate Skeletal Meshes and Animations/SciFi/SciFi_Body.fbx
   147228  ./kadin/Separate Skeletal Meshes and Animations/SciFi/SciFi_Feet.fbx
   192076  ./kadin/Separate Skeletal Meshes and Animations/SciFi/SciFi_Head.fbx
   150236  ./kadin/Separate Skeletal Meshes and Animations/SciFi/SciFi_Legs.fbx
   213148  ./kadin/Separate Skeletal Meshes and Animations/Soldier/Soldier_Body.fbx
   155900  ./kadin/Separate Skeletal Meshes and Animations/Soldier/Soldier_Feet.fbx
   189356  ./kadin/Separate Skeletal Meshes and Animations/Soldier/Soldier_Head.fbx
   166012  ./kadin/Separate Skeletal Meshes and Animations/Soldier/Soldier_Legs.fbx
   213532  ./kadin/Separate Skeletal Meshes and Animations/Suit/Suit_Body.fbx
   144316  ./kadin/Separate Skeletal Meshes and Animations/Suit/Suit_Feet.fbx
   188940  ./kadin/Separate Skeletal Meshes and Animations/Suit/Suit_Head.fbx
   148652  ./kadin/Separate Skeletal Meshes and Animations/Suit/Suit_Legs.fbx
    89852  ./kadin/Separate Skeletal Meshes and Animations/Sword.fbx
   216220  ./kadin/Separate Skeletal Meshes and Animations/Witch/Witch_Body.fbx
   144924  ./kadin/Separate Skeletal Meshes and Animations/Witch/Witch_Feet.fbx
   198364  ./kadin/Separate Skeletal Meshes and Animations/Witch/Witch_Head.fbx
   148828  ./kadin/Separate Skeletal Meshes and Animations/Witch/Witch_Legs.fbx
   211548  ./kadin/Separate Skeletal Meshes and Animations/Worker/Worker_Body.fbx
   144316  ./kadin/Separate Skeletal Meshes and Animations/Worker/Worker_Feet.fbx
   176524  ./kadin/Separate Skeletal Meshes and Animations/Worker/Worker_Head.fbx
   152732  ./kadin/Separate Skeletal Meshes and Animations/Worker/Worker_Legs.fbx
```
