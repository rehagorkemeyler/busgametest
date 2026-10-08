using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AnkaraBus.Gameplay;
using AnkaraBus.UI;
using AnkaraBus.Vehicle;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace AnkaraBus.Diagnostics
{
    /// <summary>
    /// Ana menü ve oyun içi kamera testi. Menüden başlar, sahne değişimlerinde yaşar (DontDestroyOnLoad).
    /// Sanal dokunmatik ekranla gerçek parmak hareketleri gönderir (sürükleme, iki parmak, pinch, çift dokunuş).
    /// Sonuçlar "[MenuTest]" satırları olarak loga yazılır; ekran görüntüleri RENDER_OUT klasörüne
    /// (ortam değişkeni; yoksa Application.persistentDataPath) kaydedilir. En sonda "[MenuTest] BITTI".
    /// Yalnızca SurusTestiEditor (Menü ve Kamera Testi) tarafından sahneye eklenir.
    /// </summary>
    public class MenuKameraTesti : MonoBehaviour
    {
        private Touchscreen ts;
        private string cikti;
        private int hata;

        private static MenuKameraTesti calisan;

        private void Awake()
        {
            // test menü sahnesine eklenir; menüye her dönüşte yeni kopyası gelir, yalnızca ilki çalışsın
            if (calisan != null)
            {
                Destroy(gameObject);
                return;
            }
            calisan = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Log(string s) => Debug.Log("[MenuTest] " + s);

        private void Check(bool ok, string what)
        {
            if (!ok) hata++;
            Log($"{(ok ? "OK  " : "HATA")} {what}");
        }

        private IEnumerator Start()
        {
            cikti = System.Environment.GetEnvironmentVariable("RENDER_OUT");
            if (string.IsNullOrEmpty(cikti))
                cikti = Application.persistentDataPath;
            ts = InputSystem.AddDevice<Touchscreen>();
            yield return new WaitForSeconds(1.5f);
            Log($"BASLA ekran={Screen.width}x{Screen.height} sahne={SceneManager.GetActiveScene().name}");

            yield return Menu();
            yield return SahneyiBekle("Hat1_KizilayAtakule");
            yield return new WaitForSeconds(2f);
            yield return Kamera();
            yield return HattanMenuye();
            yield return Hat2VeHatSonu();

            Log($"SONUC hata={hata}");
            Log("BITTI");
        }

        // ---------------- Ana menü ----------------

        private IEnumerator Menu()
        {
            var vitrin = FindAnyObjectByType<MenuVitrini>();
            Check(vitrin != null, "menüde vitrin var");
            if (vitrin == null) yield break;
            var cam = (Camera)Alan(vitrin, "vitrinKamerasi");
            var platform = (Transform)Alan(vitrin, "platform");
            var livery = FindAnyObjectByType<BusLivery>();
            var kalite = livery != null ? livery.GetComponent<BusKaliteModeli>() : null;
            var bus = livery != null ? livery.transform : platform;

            // Dönüyor mu
            float a0 = platform.eulerAngles.y;
            yield return new WaitForSeconds(1f);
            float donus = Mathf.DeltaAngle(a0, platform.eulerAngles.y);
            Check(Mathf.Abs(donus) > 3f, $"podyum kendiliğinden dönüyor ({donus:F1}°/sn)");

            // Yerleşim: 16:9 ve 20:9
            foreach (var (ad, oran, w) in new[] { ("16x9", 16f / 9f, 1920), ("20x9", 20f / 9f, 2400) })
            {
                cam.aspect = oran;
                yield return null;
                yield return null;
                var b = Sinirlar(bus);
                var min = cam.WorldToViewportPoint(b.min);
                var max = cam.WorldToViewportPoint(b.max);
                // köşelerin hepsine bak (döndükçe değişir)
                float xMin = 1f, xMax = 0f, yMin = 1f, yMax = 0f;
                foreach (var k in Koseler(b))
                {
                    var v = cam.WorldToViewportPoint(k);
                    xMin = Mathf.Min(xMin, v.x); xMax = Mathf.Max(xMax, v.x);
                    yMin = Mathf.Min(yMin, v.y); yMax = Mathf.Max(yMax, v.y);
                }
                var merkez = cam.WorldToViewportPoint(b.center);
                var parcalar = bus.GetComponentsInChildren<Renderer>().Where(rr => rr.enabled)
                    .Select(rr => cam.WorldToViewportPoint(rr.bounds.center)).ToList();
                float sagda = parcalar.Count(v => v.x > 0.5f && v.x < 1f && v.y > 0f && v.y < 1f) / (float)Mathf.Max(1, parcalar.Count);
                Check(merkez.x > 0.55f && merkez.x < 0.9f && sagda > 0.9f,
                    $"{ad}: otobüs sağ yarıda (merkez x={merkez.x:F2}, parçaların %{sagda * 100f:F0}'ı sağ yarıda; kutu x {xMin:F2}–{xMax:F2})");
                Kaydet(cam, w, 1080, $"menu_{ad}.png");
            }
            cam.ResetAspect();

            // Parmakla çevirme (sağ yarıda, arayüz dışında)
            float once = platform.eulerAngles.y;
            yield return Surukle(1, new Vector2(Screen.width * 0.72f, Screen.height * 0.5f), new Vector2(-Screen.width * 0.3f, 0f), 15);
            float sonra = platform.eulerAngles.y;
            Check(Mathf.Abs(Mathf.DeltaAngle(once, sonra)) > 20f, $"parmakla çevrildi ({Mathf.DeltaAngle(once, sonra):F0}°)");

            // Kaplama düğmeleri
            foreach (var ad in new[] { "EGO mavi", "Özel Halk", "EGO kırmızı" })
            {
                Bas(ad);
                yield return null;
                Check(livery != null && livery.SelectedName == ad, $"kaplama '{ad}' vitrinde ({livery?.SelectedName})");
            }

            // Görüntü kalitesi
            Bas("Yüksek");
            yield return null;
            yield return null;
            Check(kalite != null && kalite.TamKaliteAktif, "Yüksek: vitrinde tam kalite model");
            Bas("Düşük");
            yield return null;
            Check(kalite != null && !kalite.TamKaliteAktif, "Düşük: vitrinde mobil model");
            Bas("Normal");

            // Arka plan: bina ön cepheleri (+Z) merkeze bakıyor mu
            var arka = GameObject.Find("Arkaplan");
            var binalar = (arka != null ? arka.transform : null)?.Cast<Transform>().ToList() ?? new List<Transform>();
            int dogru = 0, toplam = 0;
            foreach (var t in binalar)
            {
                if (t.name.Contains("Atakule")) continue;
                Vector3 merkeze = Vector3.zero - t.position; merkeze.y = 0f;
                if (merkeze.sqrMagnitude < 1f) continue;
                toplam++;
                if (Vector3.Dot(t.forward, merkeze.normalized) > 0.9f) dogru++;
            }
            Log($"arka plan: {binalar.Count} obje, Atakule={(binalar.Any(t => t.name.Contains("Atakule")) ? "var" : "YOK")}");
            Check(toplam > 0 && dogru == toplam, $"bina ön cepheleri merkeze bakıyor ({dogru}/{toplam})");

            // Kartlarda en iyi puan
            foreach (var kart in new[] { "Hat_1", "Hat_2" })
            {
                var go = GameObject.Find(kart);
                var eniyi = go != null ? go.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "EnIyi") : null;
                var yazi = eniyi != null ? eniyi.GetComponentInChildren<Text>(true) : null;
                Check(yazi != null && yazi.text.Length > 0, $"{kart} kartı: '{yazi?.text}'");
            }

            // Hat 1, Özel Halk ile başla
            Bas("Hat_1");
            Bas("Özel Halk");
            yield return null;
            Bas("SEFERE BAŞLA");
        }

        // ---------------- Hat 1: kamera ----------------

        private IEnumerator Kamera()
        {
            var bus = FindAnyObjectByType<BusVehicle>();
            var rig = FindAnyObjectByType<BusCameraRig>();
            var input = bus.GetComponent<BusInput>();
            var livery = bus.GetComponent<BusLivery>();
            Check(livery != null && livery.SelectedName == "Özel Halk", $"menüde seçilen kaplama hatta ({livery?.SelectedName})");

            // KAMERA düğmesi sırası ve adı
            var sira = new List<string>();
            var etiket = Etiket("KAMERA");
            for (int i = 0; i < 6; i++)
            {
                sira.Add($"{rig.ModeName}[{etiket?.text.Replace("\n", " ")}]");
                Bas("KAMERA");
                yield return null;
                yield return null;
            }
            Log("KAMERA sırası: " + string.Join(" → ", sira));
            Check(sira.Take(5).Select(s => s.Split('[')[0]).SequenceEqual(new[] { "DIŞ", "KOKPİT", "YOLCU", "KAPI", "SERBEST" }) && sira[5].StartsWith("DIŞ"),
                "KAMERA: DIŞ → KOKPİT → YOLCU → KAPI → SERBEST → DIŞ");
            Check(etiket != null && etiket.text.Contains(rig.ModeName), $"düğmede görünüm adı yazıyor ('{etiket?.text.Replace("\n", " ")}')");

            // YOLCU ve KAPI görünüm görüntüleri
            var cam = rig.GetComponent<Camera>();
            rig.SetMode(BusCameraRig.Mode.Interior);
            yield return null; yield return null;
            Kaydet(cam, 1280, 720, "kamera_yolcu.png", canvas: false);
            rig.SetMode(BusCameraRig.Mode.Door);
            yield return null; yield return null;
            Kaydet(cam, 1280, 720, "kamera_kapi.png", canvas: false);

            // DIŞ: 360° sürükleme (boş ekran)
            rig.SetMode(BusCameraRig.Mode.Chase);
            yield return new WaitForSeconds(0.5f);
            Vector2 bos = BosNokta();
            float toplam = 0f, onceki = (float)Alan(rig, "orbitYaw");
            float pikselPer360 = 360f / ((float)Alan(rig, "dragDegrees") / Screen.height);
            yield return Surukle(1, bos, new Vector2(pikselPer360 * 1.1f, 0f), 60, () =>
            {
                float y = (float)Alan(rig, "orbitYaw");
                toplam += Mathf.DeltaAngle(onceki, y);
                onceki = y;
            });
            Check(Mathf.Abs(toplam) >= 360f, $"DIŞ: otobüsün çevresinde 360° döndü (toplam {toplam:F0}°)");

            // Dururken bırakılınca kalıyor
            yield return Surukle(1, bos, new Vector2(pikselPer360 * 0.25f, 0f), 10);
            float birakilan = (float)Alan(rig, "orbitYaw");
            yield return new WaitForSeconds(3.5f);
            float kalan = (float)Alan(rig, "orbitYaw");
            Check(Mathf.Abs(Mathf.DeltaAngle(birakilan, kalan)) < 2f && Mathf.Abs(kalan) > 30f,
                $"dururken bırakılan açıda kalıyor ({birakilan:F0}° → {kalan:F0}°)");

            // Giderken arkaya dönüyor
            input.AutoThrottle = 0.45f;
            float t0 = Time.time;
            while (Time.time - t0 < 9f) yield return null;
            float giderken = (float)Alan(rig, "orbitYaw");
            Check(Mathf.Abs(giderken) < 15f, $"giderken arkaya döndü ({kalan:F0}° → {giderken:F0}°, hız {bus.SpeedKmh:F0} km/s)");

            // Direksiyonda bir parmak, öbürüyle sürükleme
            var direksiyon = FindAnyObjectByType<TouchSteeringWheel>();
            var dRect = (RectTransform)direksiyon.transform;
            Vector2 dMerkez = RectTransformUtility.WorldToScreenPoint(null, dRect.position);
            float r = dRect.rect.width * dRect.lossyScale.x * 0.35f;
            float yaw0 = (float)Alan(rig, "orbitYaw");
            Dokun(2, dMerkez + new Vector2(0f, r), TouchPhase.Began);
            Dokun(3, bos, TouchPhase.Began);
            yield return null;
            for (int i = 1; i <= 20; i++)
            {
                float aci = Mathf.Deg2Rad * (90f - i * 4f);
                Dokun(2, dMerkez + new Vector2(Mathf.Cos(aci), Mathf.Sin(aci)) * r, TouchPhase.Moved);
                Dokun(3, bos + new Vector2(i * 12f, 0f), TouchPhase.Moved);
                yield return null;
            }
            float direksiyonDegeri = direksiyon.Value;
            float yaw1 = (float)Alan(rig, "orbitYaw");
            Check(Mathf.Abs(direksiyonDegeri) > 0.1f, $"iki parmak: direksiyon döndü (değer {direksiyonDegeri:F2})");
            Check(Mathf.Abs(Mathf.DeltaAngle(yaw0, yaw1)) > 10f, $"iki parmak: öbür parmak kamerayı çevirdi ({Mathf.DeltaAngle(yaw0, yaw1):F0}°)");
            Dokun(3, bos + new Vector2(240f, 0f), TouchPhase.Ended);
            yield return null;
            // Yalnızca direksiyon parmağı: kamera dönmemeli
            float yaw2 = (float)Alan(rig, "orbitYaw");
            for (int i = 1; i <= 20; i++)
            {
                float aci = Mathf.Deg2Rad * (10f + i * 4f);
                Dokun(2, dMerkez + new Vector2(Mathf.Cos(aci), Mathf.Sin(aci)) * r, TouchPhase.Moved);
                yield return null;
            }
            float yaw3 = (float)Alan(rig, "orbitYaw");
            Check(Mathf.Abs(Mathf.DeltaAngle(yaw2, yaw3)) < 2f, $"direksiyonu çevirirken kamera dönmüyor ({Mathf.DeltaAngle(yaw2, yaw3):F1}°)");
            Dokun(2, dMerkez, TouchPhase.Ended);
            input.AutoThrottle = 0f;
            input.AutoBrake = 1f;
            while (!bus.IsStopped) yield return null;
            yield return null;

            // Pinch (iki parmak açılıyor → yaklaş)
            float mesafe0 = (float)Alan(rig, "distance");
            yield return Pinch(bos, 60f, 260f);
            float mesafe1 = (float)Alan(rig, "distance");
            Check(mesafe1 < mesafe0 - 1f, $"pinch: yakınlaştı ({mesafe0:F1} → {mesafe1:F1} m)");

            // Çift dokunuş: sıfırlar
            yield return Surukle(1, bos, new Vector2(300f, 0f), 10);
            float yawOnce = (float)Alan(rig, "orbitYaw");
            yield return Dokunus(bos);
            yield return new WaitForSeconds(0.1f);
            yield return Dokunus(bos);
            yield return null;
            float yawSonra = (float)Alan(rig, "orbitYaw");
            float mesafe2 = (float)Alan(rig, "distance");
            Check(Mathf.Abs(yawSonra) < 1f && Mathf.Abs(mesafe2 - (float)Alan(rig, "chaseDistance")) < 0.1f,
                $"çift dokunuş: sıfırlandı (açı {yawOnce:F0}° → {yawSonra:F0}°, mesafe {mesafe2:F1} m)");

            // KOKPİT: etrafa bak, bırakınca yola dön
            rig.SetMode(BusCameraRig.Mode.Cockpit);
            yield return null;
            yield return Surukle(1, bos, new Vector2(Screen.height * 0.5f, 0f), 15);
            float bak = (float)Alan(rig, "lookYaw");
            Check(Mathf.Abs(bak) > 40f, $"KOKPİT: etrafa bakıldı ({bak:F0}°)");
            yield return new WaitForSeconds(4.5f);
            float don = (float)Alan(rig, "lookYaw");
            Check(Mathf.Abs(don) < 8f, $"KOKPİT: bırakınca yola döndü ({bak:F0}° → {don:F0}°)");

            // Fareyle sürükleme (Editor)
            var mouse = Mouse.current ?? InputSystem.AddDevice<Mouse>();
            rig.SetMode(BusCameraRig.Mode.Chase);
            yield return null;
            float mYaw0 = (float)Alan(rig, "orbitYaw");
            InputSystem.QueueStateEvent(mouse, new MouseState { position = bos }.WithButton(MouseButton.Left));
            yield return null;
            for (int i = 1; i <= 10; i++)
            {
                InputSystem.QueueStateEvent(mouse, new MouseState { position = bos + new Vector2(i * 20f, 0f), delta = new Vector2(20f, 0f) }.WithButton(MouseButton.Left));
                yield return null;
            }
            InputSystem.QueueStateEvent(mouse, new MouseState { position = bos + new Vector2(200f, 0f) });
            yield return null;
            float mYaw1 = (float)Alan(rig, "orbitYaw");
            Check(Mathf.Abs(Mathf.DeltaAngle(mYaw0, mYaw1)) > 10f, $"fareyle sürükleme ({Mathf.DeltaAngle(mYaw0, mYaw1):F0}°)");
            rig.SetMode(BusCameraRig.Mode.Chase);
            yield return new WaitForSeconds(0.5f);
        }

        // ---------------- Menüye dönüşler ----------------

        private IEnumerator HattanMenuye()
        {
            Bas("AYARLAR");
            yield return null;
            Check(Bas("ANA MENÜ"), "AYARLAR → ANA MENÜ düğmesi var");
            yield return SahneyiBekle("AnaMenu");
            Check(SceneManager.GetActiveScene().name == "AnaMenu", "AYARLAR → ANA MENÜ menüye döndü");
            yield return new WaitForSeconds(1f);
        }

        private IEnumerator Hat2VeHatSonu()
        {
            Bas("Hat_2");
            yield return null;
            Bas("SEFERE BAŞLA");
            yield return SahneyiBekle("Hat2_KizilayUlus");
            Check(SceneManager.GetActiveScene().name == "Hat2_KizilayUlus", "menüden Hat 2 açıldı");
            yield return new WaitForSeconds(2f);

            // Hat sonunu tetikle (özet paneli için hattın bitmiş olması yeterli)
            var puanlama = FindAnyObjectByType<SeferPuanlama>();
            var m = typeof(SeferPuanlama).GetMethod("OnRouteCompleted", BindingFlags.NonPublic | BindingFlags.Instance);
            m?.Invoke(puanlama, null);
            yield return new WaitForSeconds(1f);
            var panel = GameObject.Find("SeferOzeti");
            Check(panel != null, "Hat 2: hat sonu özeti açıldı");
            Check(Bas("MENÜ"), "hat sonu MENÜ düğmesi var");
            yield return SahneyiBekle("AnaMenu");
            Check(SceneManager.GetActiveScene().name == "AnaMenu", "hat sonu MENÜ menüye döndü");
            yield return new WaitForSeconds(1f);
            var eniyi2 = GameObject.Find("Hat_2")?.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "EnIyi");
            var yazi = eniyi2 != null ? eniyi2.GetComponentInChildren<Text>(true) : null;
            Log($"Hat 2 kartı dönüşte: '{yazi?.text}'");
        }

        // ---------------- Yardımcılar ----------------

        private IEnumerator SahneyiBekle(string ad)
        {
            float t0 = Time.realtimeSinceStartup;
            while (SceneManager.GetActiveScene().name != ad && Time.realtimeSinceStartup - t0 < 30f)
                yield return null;
            yield return null;
        }

        private static object Alan(object o, string ad)
        {
            var f = o.GetType().GetField(ad, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            return f?.GetValue(o);
        }

        private bool Bas(string ad)
        {
            var b = FindObjectsByType<Button>().FirstOrDefault(x => x.name == ad && x.isActiveAndEnabled);
            if (b == null)
            {
                Log($"düğme yok: {ad}");
                return false;
            }
            b.onClick.Invoke();
            return true;
        }

        private Text Etiket(string dugme)
        {
            var b = FindObjectsByType<Button>().FirstOrDefault(x => x.name == dugme);
            return b != null ? b.GetComponentInChildren<Text>(true) : null;
        }

        /// <summary>Arayüze değmeyen bir ekran noktası (ekranın ortası civarı).</summary>
        private Vector2 BosNokta()
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            foreach (var p in new[] { new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.45f), new Vector2(0.62f, 0.6f), new Vector2(0.4f, 0.65f) })
            {
                var pos = new Vector2(Screen.width * p.x, Screen.height * p.y);
                var hits = new List<UnityEngine.EventSystems.RaycastResult>();
                es?.RaycastAll(new UnityEngine.EventSystems.PointerEventData(es) { position = pos }, hits);
                if (hits.Count == 0) return pos;
            }
            return new Vector2(Screen.width * 0.5f, Screen.height * 0.55f);
        }

        private void Dokun(int id, Vector2 pos, TouchPhase phase) =>
            InputSystem.QueueStateEvent(ts, new TouchState { touchId = id, position = pos, phase = phase, pressure = 1f });

        private IEnumerator Surukle(int id, Vector2 from, Vector2 by, int adim, System.Action herKare = null)
        {
            Dokun(id, from, TouchPhase.Began);
            yield return null;
            for (int i = 1; i <= adim; i++)
            {
                Dokun(id, from + by * (i / (float)adim), TouchPhase.Moved);
                yield return null;
                herKare?.Invoke();
            }
            Dokun(id, from + by, TouchPhase.Ended);
            yield return null;
            herKare?.Invoke();
        }

        private IEnumerator Pinch(Vector2 merkez, float bas, float son)
        {
            Dokun(4, merkez - new Vector2(bas * 0.5f, 0f), TouchPhase.Began);
            Dokun(5, merkez + new Vector2(bas * 0.5f, 0f), TouchPhase.Began);
            yield return null;
            for (int i = 1; i <= 15; i++)
            {
                float d = Mathf.Lerp(bas, son, i / 15f) * 0.5f;
                Dokun(4, merkez - new Vector2(d, 0f), TouchPhase.Moved);
                Dokun(5, merkez + new Vector2(d, 0f), TouchPhase.Moved);
                yield return null;
            }
            Dokun(4, merkez - new Vector2(son * 0.5f, 0f), TouchPhase.Ended);
            Dokun(5, merkez + new Vector2(son * 0.5f, 0f), TouchPhase.Ended);
            yield return null;
        }

        private IEnumerator Dokunus(Vector2 pos)
        {
            Dokun(6, pos, TouchPhase.Began);
            yield return null;
            Dokun(6, pos, TouchPhase.Ended);
            yield return null;
        }

        private static Bounds Sinirlar(Transform t)
        {
            var rs = t.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
            var b = rs.Length > 0 ? rs[0].bounds : new Bounds(t.position, Vector3.one);
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        private static IEnumerable<Vector3> Koseler(Bounds b)
        {
            for (int i = 0; i < 8; i++)
                yield return new Vector3(i % 2 == 0 ? b.min.x : b.max.x, (i / 2) % 2 == 0 ? b.min.y : b.max.y, i / 4 == 0 ? b.min.z : b.max.z);
        }

        /// <summary>Kameradan, ekran yerleşimiyle aynı oranda görüntü (arayüz dahil).</summary>
        private void Kaydet(Camera cam, int w, int h, string ad, bool canvas = true)
        {
            var canvases = canvas ? FindObjectsByType<Canvas>().Where(c => c.renderMode == RenderMode.ScreenSpaceOverlay).ToList() : new List<Canvas>();
            foreach (var c in canvases) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = cam.nearClipPlane + 0.5f; }
            var rt = new RenderTexture(w, h, 24);
            var eski = cam.targetTexture;
            cam.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            File.WriteAllBytes(Path.Combine(cikti, ad), tex.EncodeToPNG());
            cam.targetTexture = eski;
            RenderTexture.active = null;
            foreach (var c in canvases) c.renderMode = RenderMode.ScreenSpaceOverlay;
            Log($"görüntü: {ad}");
        }
    }
}
