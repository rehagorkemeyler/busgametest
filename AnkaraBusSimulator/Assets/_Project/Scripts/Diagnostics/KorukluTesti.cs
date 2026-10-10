using System.Collections;
using System.Collections.Generic;
using AnkaraBus.Route;
using AnkaraBus.Vehicle;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnkaraBus.Diagnostics
{
    /// <summary>
    /// Y14: körüklü otobüsün (ve her otobüsün) fırlayıp fırlamadığını izler. Her zaman: sahne yüklenmeleri, başlangıçta gövde
    /// kutularına değen nesneler, ilk karelerde ön/arka gövdenin konumu ve hızı, mafsal ayrılması; sonra sürekli bekçi
    /// (hız, dikey hız, eğim, havada kalma, yerden yükselme). "zorla" açıksa fırlamayı zorlayan senaryoları sürer:
    /// binaya ve durağa 45 km/s çarpma, kaldırıma tırmanma, Cinnah'ta sert fren ve geri vites, keskin dönüşte tam gaz,
    /// geri geri binaya çarpma (arka gövde çarpışma cezası), durak durak ve 45 m yana ışınlama.
    /// Sonuçlar "[KorTest]" satırları; her senaryo "SONUC … durum=OK|FIRLADI".
    /// Yalnızca testlerde eklenir (AndroidBuild Y11 "koruklu" görevi, SurusTestiEditor.RunKorukluBatch).
    /// </summary>
    public class KorukluTesti : MonoBehaviour
    {
        [SerializeField] private bool zorla;
        [SerializeField] private string sonrakiSahne;

        private const float IlkKareSuresi = 3f;
        private const float HavadaSiniri = 0.4f;    // sn: bir gövdenin bütün tekerleri yerden kesik
        private const float HizSiniri = 25f;        // m/s (90 km/s; otobüs 85'i geçmez)
        private const float DikeySiniri = 4f;       // m/s
        private const float EgimSiniri = 20f;       // derece
        private const float YukselmeSiniri = 1f;    // m (başlangıçtaki yerden yüksekliğe göre)

        private static int sahneSayaci;
        private static readonly List<string> sahneler = new List<string>();

        private BusVehicle bus;
        private BusInput input;
        private Rigidbody on;
        private KorukluOtobus kor;
        private Rigidbody arka;
        private readonly List<WheelCollider> onTeker = new List<WheelCollider>();
        private readonly List<WheelCollider> arkaTeker = new List<WheelCollider>();
        private readonly List<Collider> kendi = new List<Collider>();
        private OtobusOlcusu olcu;
        private string sahne;
        private float baslangicZamani = -1f;
        private float zeminPayi = float.NaN;   // kök ile altındaki zemin arası, otobüs oturunca

        // bölüm (senaryo) en büyükleri
        private string bolum = "baslangic";
        private float mHiz, mDikey, mEgim, mHavada, mYukselme, mAcisal, mAyrilma;
        private float onHavada, arkaHavada;
        private int ayrilmaUyarisi;
        private readonly List<string> carpismalar = new List<string>();
        private string ilkFirlama;
        private int firlamaSayisi;
        private float sonFirlamaLogu = -10f;
        private int firlayanBolum;
        private float isinlamaPayi;   // ışınlamadan sonra bu zamana kadar havada/dikey denetlenmez
        private Vector3 oncekiKonum;
        private int sonuc;

        public void Ayarla(bool zorlamaSenaryolari, string sonraki)
        {
            zorla = zorlamaSenaryolari;
            sonrakiSahne = sonraki;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void SayaciSifirla()
        {
            sahneSayaci = 0;
            sahneler.Clear();
            SceneManager.sceneLoaded -= Yuklendi;
            SceneManager.sceneLoaded += Yuklendi;
        }

        private static void Yuklendi(Scene s, LoadSceneMode m)
        {
            sahneSayaci++;
            sahneler.Add(s.name);
            Debug.Log($"[KorTest] sahne yüklendi #{sahneSayaci}: {s.name} ({m}), sıra: {string.Join(" → ", sahneler)}");
        }

        private void Log(string s) => Debug.Log("[KorTest] " + s);

        private void OnEnable() => Application.logMessageReceived += LogGeldi;
        private void OnDisable() => Application.logMessageReceived -= LogGeldi;

        private void LogGeldi(string m, string st, LogType t)
        {
            if (m.StartsWith("[Puan] çarpışma"))
                carpismalar.Add(m.Substring("[Puan] çarpışma: ".Length));
            else if (m.StartsWith("[Koruklu]"))
                ayrilmaUyarisi++;
        }

        // OtobusDegistirici otobüsü sahne yüklenince (Awake'lerden sonra) değiştirir: otobüs Start'ta bulunur
        private IEnumerator Start()
        {
            sahne = SceneManager.GetActiveScene().name;
            bus = FindAnyObjectByType<BusVehicle>();
            if (bus == null)
            {
                Log("HATA otobüs yok");
                yield break;
            }
            on = bus.GetComponent<Rigidbody>();
            input = bus.GetComponent<BusInput>();
            kor = bus.GetComponent<KorukluOtobus>();
            arka = kor != null ? kor.ArkaGovde : null;
            foreach (var w in bus.GetComponentsInChildren<WheelCollider>())
                (w.attachedRigidbody == arka && arka != null ? arkaTeker : onTeker).Add(w);
            kendi.AddRange(bus.GetComponentsInChildren<Collider>(true));
            if (arka != null)
                foreach (var w in arka.GetComponentsInChildren<WheelCollider>())
                    if (!arkaTeker.Contains(w)) arkaTeker.Add(w);
            if (arka != null)
                kendi.AddRange(arka.GetComponentsInChildren<Collider>(true));
            olcu = OtobusOlcusu.Olc(bus);
            baslangicZamani = Time.time;
            oncekiKonum = on.position;

            Log($"BASLA sahne={sahne} otobüs={bus.Definition?.displayName ?? bus.name} körüklü={(arka != null ? "evet" : "hayır")} " +
                $"zorla={zorla} konum={on.position} yön={on.rotation.eulerAngles.y:F0}° boy=+{olcu.On:F1}/-{olcu.Arka:F1} m " +
                $"teker ön={onTeker.Count} arka={arkaTeker.Count} sahne_yükleme={sahneSayaci}");
            Log($"başlangıç temasları: {Temaslar()}");
            if (arka != null)
                Log($"başlangıç arka gövde (ön gövdeye göre) {bus.transform.InverseTransformPoint(arka.position)}, mafsal ayrılması " +
                    $"{Ayrilma():F3} m, arka hız {arka.linearVelocity.magnitude:F2} m/s");

            yield return new WaitForSeconds(IlkKareSuresi + 2f);
            zeminPayi = ZeminPayi();
            Sonuc();
            if (!zorla)
            {
                // yalnızca izleme (otomatik pilot, ölçüm turu): sahnenin geri kalanı tek bölüm, sonucu sahne kapanınca
                Sifirla("surus");
                yield break;
            }

            yield return Bolum("bina_45kmh", () => CarpmaSenaryosu(false, false, 12.5f));
            yield return Bolum("durak_45kmh", () => CarpmaSenaryosu(true, false, 12.5f));
            yield return Bolum("kaldirim", Kaldirim);
            yield return Bolum("cinnah_fren_geri", () => Cinnah(false));
            yield return Bolum("cinnah_ters_yon", () => Cinnah(true));
            yield return Bolum("keskin_donus_tam_gaz", KeskinDonus);
            yield return Bolum("geri_geri_binaya", () => CarpmaSenaryosu(false, true, 4f));
            yield return Bolum("durak_durak_isinlama", DurakDurak);
            yield return Bolum("yana_45m_isinlama", YanaIsinla);
            Log($"SONUC_TOPLAM sahne={sahne} bölüm_fırlayan={firlayanBolum} sahne_yükleme={sahneSayaci} ({string.Join(" → ", sahneler)})");
            Log("BITTI " + sahne);
            if (!string.IsNullOrEmpty(sonrakiSahne))
            {
                yield return new WaitForSeconds(2f);
                SceneManager.LoadScene(sonrakiSahne);
            }
        }

        // ---------------- bekçi ----------------

        private float kareIzleBitis;

        /// <summary>Verilen süre boyunca her fizik adımını loglar (ışınlamadan sonra fırlamanın kök nedeni için).</summary>
        private void KareIzle(float sure) => kareIzleBitis = Time.time + sure;

        private string KareDurumu() =>
            $"ön y={on.position.y:F2} v={on.linearVelocity} w={on.angularVelocity.magnitude:F2}" +
            (arka != null ? $" arka y={arka.position.y:F2} v={arka.linearVelocity} w={arka.angularVelocity.magnitude:F2} ayrılma={Ayrilma():F3} mafsal={kor.MafsalAcisi:F0}°" : "") +
            $" yerde ön={Yerde(onTeker)} arka={Yerde(arkaTeker)} teker[{TekerYuk()}] temas={Temaslar()}";

        private string TekerYuk()
        {
            var l = new List<string>();
            foreach (var w in onTeker) l.Add(Teker(w, ""));
            foreach (var w in arkaTeker) l.Add(Teker(w, "A"));
            return string.Join(" ", l);
        }

        private string Teker(WheelCollider w, string on)
        {
            w.GetWorldPose(out var p, out _);
            float merkez = w.transform.position.y;
            string zemin = $"zeminΔ{ZeminY(p) - (merkez - w.suspensionDistance - w.radius):+0.00;-0.00}";
            return w.GetGroundHit(out var h)
                ? $"{on}{w.name}:{h.force / 1000f:F0}kN sıkışma{(merkez - h.point.y - w.radius) :F2}/{w.suspensionDistance:F2} {h.collider.name} {zemin}"
                : $"{on}{w.name}:- {zemin}";
        }

        private float Ayrilma() =>
            kor == null || arka == null ? 0f : (arka.position - bus.transform.TransformPoint(arka.GetComponent<ConfigurableJoint>().connectedAnchor)).magnitude;

        private void FixedUpdate()
        {
            if (bus == null || on == null || baslangicZamani < 0f)
                return;
            float t = Time.time - baslangicZamani;
            if (t <= IlkKareSuresi)
            {
                int kare = Mathf.RoundToInt(t / Time.fixedDeltaTime);
                if (kare < 12 || kare % 10 == 0)
                    Log($"ilk kare t={t:F2} ön={on.position} v={on.linearVelocity.magnitude:F2} vy={on.linearVelocity.y:F2}" +
                        (arka != null ? $" arka={bus.transform.InverseTransformPoint(arka.position)} va={arka.linearVelocity.magnitude:F2} " +
                                        $"ayrılma={Ayrilma():F3} mafsal={kor.MafsalAcisi:F1}°" : "") +
                        $" yerde ön={Yerde(onTeker)}/{onTeker.Count} arka={Yerde(arkaTeker)}/{arkaTeker.Count}");
            }

            if (Time.time < kareIzleBitis)
                Log($"kare t={t:F2} {KareDurumu()}");

            // ışınlama (test ya da koruma): bir adımda 5 m'den fazla yer değiştirme
            if ((on.position - oncekiKonum).magnitude > 5f)
                isinlamaPayi = Time.time + 2f;
            oncekiKonum = on.position;
            bool pay = Time.time < isinlamaPayi;

            foreach (var rb in arka != null ? new[] { on, arka } : new[] { on })
            {
                float hiz = rb.linearVelocity.magnitude;
                float dikey = Mathf.Abs(rb.linearVelocity.y);
                float egim = Vector3.Angle(rb.transform.up, Vector3.up);
                float acisal = Vector3.ProjectOnPlane(rb.angularVelocity, rb.transform.up).magnitude;
                mHiz = Mathf.Max(mHiz, hiz);
                mEgim = Mathf.Max(mEgim, egim);
                mAcisal = Mathf.Max(mAcisal, acisal);
                if (!pay)
                    mDikey = Mathf.Max(mDikey, dikey);
                string ad = rb == on ? "ön" : "arka";
                if (hiz > HizSiniri) Firladi($"{ad} gövde hızı {hiz:F1} m/s");
                if (egim > EgimSiniri) Firladi($"{ad} gövde eğimi {egim:F0}°");
                if (!pay && dikey > DikeySiniri) Firladi($"{ad} gövde dikey hızı {rb.linearVelocity.y:F1} m/s");
            }
            mAyrilma = Mathf.Max(mAyrilma, Ayrilma());

            onHavada = Yerde(onTeker) == 0 ? onHavada + Time.fixedDeltaTime : 0f;
            arkaHavada = arkaTeker.Count > 0 && Yerde(arkaTeker) == 0 ? arkaHavada + Time.fixedDeltaTime : 0f;
            if (!pay)
            {
                mHavada = Mathf.Max(mHavada, Mathf.Max(onHavada, arkaHavada));
                if (onHavada > HavadaSiniri) Firladi($"ön gövde {onHavada:F1} sn havada");
                if (arkaHavada > HavadaSiniri) Firladi($"arka gövde {arkaHavada:F1} sn havada");
                if (!float.IsNaN(zeminPayi) && Time.frameCount % 5 == 0)
                {
                    float y = ZeminPayi() - zeminPayi;
                    if (!float.IsNaN(y))
                    {
                        mYukselme = Mathf.Max(mYukselme, y);
                        if (y > YukselmeSiniri) Firladi($"otobüs yerden {y:F1} m yükseldi");
                    }
                }
            }
        }

        private static int Yerde(List<WheelCollider> l)
        {
            int n = 0;
            foreach (var w in l)
                if (w != null && w.isGrounded) n++;
            return n;
        }

        private void Firladi(string neden)
        {
            if (Time.time - sonFirlamaLogu < 3f)
                return;
            sonFirlamaLogu = Time.time;
            firlamaSayisi++;
            ilkFirlama ??= neden;
            Log($"FIRLADI bölüm={bolum} {neden} t={Time.time - baslangicZamani:F1} konum={on.position} v={on.linearVelocity}" +
                (arka != null ? $" arka_v={arka.linearVelocity} ayrılma={Ayrilma():F2} mafsal={kor.MafsalAcisi:F0}°" : "") +
                $" son_çarpışma={(carpismalar.Count > 0 ? carpismalar[carpismalar.Count - 1] : "yok")} temas={Temaslar()}");
        }

        /// <summary>Kök ile altındaki (otobüse ait olmayan) zemin arası.</summary>
        private float ZeminPayi()
        {
            var p = on.position;
            float en = float.NaN;
            foreach (var h in Physics.RaycastAll(p + Vector3.up * 3f, Vector3.down, 30f, ~0, QueryTriggerInteraction.Ignore))
                if (!kendi.Contains(h.collider) && (float.IsNaN(en) || h.distance < en))
                    en = h.distance;
            return float.IsNaN(en) ? float.NaN : en - 3f;
        }

        private string Temaslar()
        {
            var adlar = new List<string>();
            foreach (var c in olcu.Kutular)
            {
                if (c == null) continue;
                var merkez = c.transform.TransformPoint(c.center);
                var yari = Vector3.Scale(c.size * 0.5f, c.transform.lossyScale);
                foreach (var h in Physics.OverlapBox(merkez, yari, c.transform.rotation, ~0, QueryTriggerInteraction.Ignore))
                    if (!kendi.Contains(h) && !(h is WheelCollider) && adlar.Count < 8)
                    {
                        var p = bus.transform.InverseTransformPoint(h.ClosestPoint(merkez));
                        adlar.Add($"{c.name}×{h.name} ({p.x:F1},{p.y:F1},{p.z:F1})");
                    }
            }
            return adlar.Count > 0 ? string.Join(", ", adlar) : "yok";
        }

        private bool TemasVar() => OtobusIsinla.Temas(bus) != null;

        private void Sonuc()
        {
            string durum = ilkFirlama == null ? "OK" : "FIRLADI";
            if (ilkFirlama != null) firlayanBolum++;
            Log($"SONUC {bolum} durum={durum}{(ilkFirlama != null ? $" ({firlamaSayisi} kez, ilki: {ilkFirlama})" : "")} maks_hız={mHiz * 3.6f:F0} km/s " +
                $"maks_dikey={mDikey:F2} m/s maks_eğim={mEgim:F1}° maks_yalpa={mAcisal:F2} rad/s havada={mHavada:F2} sn " +
                $"yükselme={mYukselme:F2} m maks_ayrılma={mAyrilma:F3} m mafsal_uyarısı={ayrilmaUyarisi} " +
                $"çarpışma={carpismalar.Count}{(carpismalar.Count > 0 ? " [" + string.Join(" | ", carpismalar) + "]" : "")}");
        }

        private void Sifirla(string ad)
        {
            bolum = ad;
            mHiz = mDikey = mEgim = mHavada = mYukselme = mAcisal = mAyrilma = 0f;
            ayrilmaUyarisi = 0;
            carpismalar.Clear();
            ilkFirlama = null;
            firlamaSayisi = 0;
            sonFirlamaLogu = -10f;
            Log($"BOLUM {ad}");
        }

        private void OnDestroy()
        {
            if (bolum == "surus")
                Sonuc();
        }

        private IEnumerator Bolum(string ad, System.Func<IEnumerator> senaryo)
        {
            Sifirla(ad);
            yield return senaryo();
            Birak();
            yield return new WaitForSeconds(2f);
            Sonuc();
            yield return Ekran(ad);
        }

        private IEnumerator Ekran(string ad)
        {
            if (Application.isBatchMode)
                yield break;
            yield return new WaitForEndOfFrame();
            Y11Hat.Kaydet($"y14_{sahne}_{ad}.png");
        }

        // ---------------- sürüş yardımcıları ----------------

        private void Birak()
        {
            input.AutoThrottle = 0f;
            input.AutoBrake = 1f;
            input.AutoSteer = 0f;
        }

        /// <summary>Otobüsü verilen yere (zemine oturtup), yöne ve ileri hıza koyar; arka gövde düz.</summary>
        private IEnumerator Yerlestir(Vector3 nokta, float yon, float hiz, System.Action<bool> sonuc)
        {
            if (!Yerlestir(nokta, yon, 0f))
            {
                sonuc(false);
                yield break;
            }
            // ışınlanan tekerler bir önceki yerin süspansiyon durumunu taşır: önce hızsız otursun, sonra hız ver.
            // Y15: ilk fizik adımında amortisör eski yerle yeni yer arasındaki sıkışma farkını hız sayıp ön gövdeyi
            // 14,7 m/s yukarı itiyordu (Cinnah yokuşu → Kızılay AVM durağı); oturma adımlarında gövde hızları sıfırlanır.
            bus.Handbrake = true;
            for (int i = 0; i < 10; i++)
            {
                yield return new WaitForFixedUpdate();
                foreach (var rb in arka != null ? new[] { on, arka } : new[] { on })
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }
            bus.Handbrake = false;
            var donus = Quaternion.Euler(0f, yon, 0f);
            bus.SetSelector(hiz < 0f ? BusVehicle.GearSelector.Reverse : BusVehicle.GearSelector.Drive);
            on.linearVelocity = donus * Vector3.forward * hiz;
            if (kor != null)
                kor.ArkayiHizala();
            sonuc(true);
        }

        private bool Yerlestir(Vector3 nokta, float yon, float hiz)
        {
            var donus = Quaternion.Euler(0f, yon, 0f);
            float zemin = ZeminY(nokta);
            if (float.IsNaN(zemin))
                return false;
            OtobusIsinla.Tasi(bus, new Vector3(nokta.x, zemin + (float.IsNaN(zeminPayi) ? 0.3f : zeminPayi + 0.05f), nokta.z), donus);
            if (TemasVar())
                return false;
            bus.Handbrake = false;
            bus.SetSelector(hiz < 0f ? BusVehicle.GearSelector.Reverse : BusVehicle.GearSelector.Drive);
            on.linearVelocity = donus * Vector3.forward * hiz;
            if (kor != null)
                kor.ArkayiHizala();
            isinlamaPayi = Time.time + 0.5f;
            return true;
        }

        private float ZeminY(Vector3 p)
        {
            float en = float.NaN, y = float.NaN;
            // durak noktaları yol düzeyinde: yukarıdan inen ışın durak çatısına değmesin
            foreach (var h in Physics.RaycastAll(new Vector3(p.x, p.y + 1.5f, p.z), Vector3.down, 30f, ~0, QueryTriggerInteraction.Ignore))
                if (!kendi.Contains(h.collider) && !(h.collider is WheelCollider) && h.collider.attachedRigidbody == null && (float.IsNaN(en) || h.distance < en))
                {
                    en = h.distance;
                    y = h.point.y;
                }
            return y;
        }

        /// <summary>Hızı (ileri +, geri −) korur; süre dolunca ya da çarpınca biter.</summary>
        private IEnumerator HizTut(float hedef, float sure, float direksiyon, bool carpincaDur)
        {
            int ilk = carpismalar.Count;
            float bitis = Time.time + sure;
            while (Time.time < bitis)
            {
                float v = bus.ForwardSpeed * Mathf.Sign(hedef);
                input.AutoThrottle = v < Mathf.Abs(hedef) ? 1f : 0f;
                input.AutoBrake = 0f;
                input.AutoSteer = direksiyon;
                if (carpincaDur && carpismalar.Count > ilk)
                {
                    Log($"çarpma anı: {carpismalar[ilk]}");
                    break;
                }
                yield return new WaitForFixedUpdate();
            }
            input.AutoThrottle = 0f;
            yield return new WaitForSeconds(carpincaDur ? 3f : 0f);
        }

        private BusRoute Rota => FindAnyObjectByType<BusRoute>();

        private IEnumerable<BusStop> Duraklar()
        {
            var r = Rota;
            if (r == null) yield break;
            for (int i = 0; i < r.StopCount; i++)
                yield return r.GetStop(i);
        }

        // ---------------- senaryolar ----------------

        /// <summary>
        /// Bir binaya (ya da durağa) önünde 15–40 m boş yol varken, verilen hızla (geri: arka gövdeyle) çarpar.
        /// Aday: her durağın 0–40 m gerisinden, yol yönüne göre −90…+90° açılarla.
        /// </summary>
        private IEnumerator CarpmaSenaryosu(bool durak, bool geri, float hiz)
        {
            // önce trafik çarpmasın: araçlar senaryo boyunca kapalı
            var araclar = new List<GameObject>();
            foreach (var c in FindObjectsByType<AnkaraBus.Traffic.TrafficCar>())
                if (c.gameObject.activeSelf) { araclar.Add(c.gameObject); c.gameObject.SetActive(false); }
            try { yield return Carp(durak, geri, hiz); }
            finally { foreach (var a in araclar) if (a != null) a.SetActive(true); }
        }

        private IEnumerator Carp(bool durak, bool geri, float hiz)
        {
            foreach (var s in Duraklar())
                for (float geriMesafe = 0f; geriMesafe <= 40f; geriMesafe += 10f)
                    for (int a = 0; a <= 12; a++)
                    {
                        float aci = (a % 2 == 0 ? 1 : -1) * (a + 1) / 2 * 15f;
                        float yon = s.transform.eulerAngles.y + aci;
                        var ileri = Quaternion.Euler(0f, yon, 0f) * Vector3.forward;
                        var nokta = s.transform.position - s.transform.forward * geriMesafe;
                        var bakis = geri ? -ileri : ileri;
                        float uc = geri ? olcu.Arka : olcu.On;
                        float zemin = ZeminY(nokta);
                        if (float.IsNaN(zemin))
                            continue;
                        var basla = new Vector3(nokta.x, zemin + 1.6f, nokta.z) + bakis * (uc + 0.3f);
                        if (!Hedef(basla, bakis, out var hit))
                            continue;
                        bool durakMi = hit.collider.name.StartsWith("EGO_Durak");
                        if (durak != durakMi || hit.distance < 10f || hit.distance > 40f)
                            continue;
                        bool kondu = false;
                        yield return Yerlestir(nokta, yon, geri ? -hiz : hiz, r => kondu = r);
                        if (!kondu)
                            continue;
                        Log($"hedef {hit.collider.name} ({hit.collider.transform.root.name}) {hit.distance:F0} m {(geri ? "geride" : "önde")}, " +
                            $"durak {s.StopName}, açı {aci:+0;-0}°, başlangıç hızı {Mathf.Abs(hiz) * 3.6f:F0} km/s");
                        yield return HizTut(geri ? -hiz : hiz, 14f, 0f, true);
                        yield break;
                    }
            Log($"ATLANDI {bolum}: uygun hedef bulunamadı");
        }

        /// <summary>Yolda (zemin kesintisiz) önündeki ilk engel; araçlar ve yayalar sayılmaz.</summary>
        private bool Hedef(Vector3 basla, Vector3 yon, out RaycastHit hit)
        {
            hit = default;
            var yari = new Vector3(olcu.YariGenislik * 0.9f, 1.2f, 0.2f);
            var don = Quaternion.LookRotation(yon);
            float en = float.MaxValue;
            bool bulundu = false;
            foreach (var h in Physics.BoxCastAll(basla, yari, yon, don, 45f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (kendi.Contains(h.collider) || h.collider is WheelCollider || h.distance <= 0f || h.distance >= en)
                    continue;
                if (h.collider.attachedRigidbody != null)   // trafik araçları, yayalar
                    continue;
                if (Mathf.Abs(h.normal.y) > 0.6f)
                    continue;
                en = h.distance;
                hit = h;
                bulundu = true;
            }
            if (!bulundu)
                return false;
            // yol boyunca zemin var mı, çok inip çıkmıyor mu
            float y0 = ZeminY(basla);
            for (float d = 0f; d < hit.distance; d += 3f)
            {
                float y = ZeminY(basla + yon * d);
                if (float.IsNaN(y) || Mathf.Abs(y - y0) > 1.5f)
                    return false;
            }
            return true;
        }

        /// <summary>Durağın 15 m gerisinden kaldırıma (sağa) 25° açıyla 25 km/s'de çıkar.</summary>
        private IEnumerator Kaldirim()
        {
            int n = 0;
            foreach (var s in Duraklar())
            {
                float yon = s.transform.eulerAngles.y + 25f;
                bool kondu = false;
                yield return Yerlestir(s.transform.position - s.transform.forward * 15f, yon, 7f, r => kondu = r);
                if (!kondu)
                    continue;
                Log($"kaldırım: durak {s.StopName}");
                yield return HizTut(7f, 5f, 0f, false);
                Birak();
                yield return new WaitForSeconds(2f);
                if (++n >= 2)
                    yield break;
            }
            if (n == 0)
                Log($"ATLANDI {bolum}: yer bulunamadı");
        }

        /// <summary>Cinnah yokuşu: 40 km/s, sert fren, geri vites tam gaz, sonra geri giderken tam direksiyon (mafsal kırılır).</summary>
        private IEnumerator Cinnah(bool tersYon)
        {
            BusStop cinnah = null;
            foreach (var s in Duraklar())
                if (s.StopName.Contains("Cinnah"))
                    cinnah = s;
            if (cinnah == null)
            {
                Log($"ATLANDI {bolum}: Cinnah durağı bu hatta yok");
                yield break;
            }
            float yon = cinnah.transform.eulerAngles.y + (tersYon ? 180f : 0f);
            var ileri = Quaternion.Euler(0f, yon, 0f) * Vector3.forward;
            bool kondu = false;
            for (float d = 60f; d >= 20f && !kondu; d -= 10f)
                yield return Yerlestir(cinnah.transform.position - ileri * d, yon, 11f, r => kondu = r);
            if (!kondu)
            {
                Log($"ATLANDI {bolum}: yer bulunamadı");
                yield break;
            }
            float y0 = on.position.y;
            yield return HizTut(11f, 2.5f, 0f, false);
            Log($"cinnah: {bus.SpeedKmh:F0} km/s'de sert fren (eğim {on.position.y - y0:+0.0;-0.0} m / 2,5 sn)");
            input.AutoThrottle = 0f;
            input.AutoBrake = 1f;
            float t = Time.time;
            while (bus.SpeedKmh > 1f && Time.time - t < 8f)
                yield return null;
            yield return new WaitForSeconds(0.5f);
            Log($"cinnah: durdu ({Time.time - t:F1} sn), geri vites tam gaz");
            input.AutoBrake = 0f;
            bus.SetSelector(BusVehicle.GearSelector.Reverse);
            input.AutoThrottle = 1f;
            yield return new WaitForSeconds(4f);
            Log($"cinnah: geri {bus.SpeedKmh:F0} km/s, tam sağ direksiyon");
            input.AutoSteer = 1f;
            yield return new WaitForSeconds(2.5f);
            Log($"cinnah: mafsal {kor?.MafsalAcisi ?? 0f:F0}°, sert fren");
            input.AutoThrottle = 0f;
            input.AutoBrake = 1f;
            yield return new WaitForSeconds(2f);
            bus.SetSelector(BusVehicle.GearSelector.Drive);
        }

        /// <summary>İlk durakta: 40 km/s'den tam gaz tam sağ, sonra duruştan tam gaz tam sol.</summary>
        private IEnumerator KeskinDonus()
        {
            foreach (var s in Duraklar())
            {
                bool kondu = false;
                KareIzle(1f);
                Log($"keskin dönüş: {s.StopName} durağına ışınlanıyor, nokta {s.transform.position}");
                yield return Yerlestir(s.transform.position, s.transform.eulerAngles.y, 11f, r => kondu = r);
                if (!kondu)
                    continue;
                Log($"keskin dönüş: durak {s.StopName}, 40 km/s tam gaz tam sağ");
                KareIzle(3.5f);
                input.AutoBrake = 0f;
                input.AutoThrottle = 1f;
                input.AutoSteer = 1f;
                float en = 0f;
                for (float t = 0f; t < 5f; t += Time.deltaTime)
                {
                    en = Mathf.Max(en, Mathf.Abs(kor?.MafsalAcisi ?? 0f));
                    yield return null;
                }
                Birak();
                yield return new WaitForSeconds(2f);
                Log($"keskin dönüş: duruştan tam gaz tam sol (en büyük mafsal {en:F0}°)");
                input.AutoBrake = 0f;
                input.AutoThrottle = 1f;
                input.AutoSteer = -1f;
                for (float t = 0f; t < 6f; t += Time.deltaTime)
                {
                    en = Mathf.Max(en, Mathf.Abs(kor?.MafsalAcisi ?? 0f));
                    yield return null;
                }
                Log($"keskin dönüş: en büyük mafsal {en:F0}°, hız {bus.SpeedKmh:F0} km/s");
                yield break;
            }
            Log($"ATLANDI {bolum}: yer bulunamadı");
        }

        /// <summary>PerfBenchmark gibi: her durağa ışınla, el freni, 2 sn bekle.</summary>
        private IEnumerator DurakDurak()
        {
            foreach (var s in Duraklar())
            {
                OtobusIsinla.Tasi(bus, s.transform.position + Vector3.up * 0.3f, Quaternion.Euler(0f, s.transform.eulerAngles.y, 0f));
                bus.Handbrake = true;
                KareIzle(0.3f);
                yield return new WaitForSeconds(2f);
                Log($"ışınlama {s.StopName}: temas={Temaslar()} hız={bus.SpeedKmh:F1} km/s ayrılma={Ayrilma():F3}");
            }
        }

        /// <summary>Y10Gozlemci.RotadanCik gibi: 45 m yana ve 2 m yukarı, 1,5 sn sonra geri.</summary>
        private IEnumerator YanaIsinla()
        {
            var eski = on.position;
            var donus = on.rotation;
            float yerden = float.IsNaN(zeminPayi) ? 0.3f : zeminPayi + 0.05f;
            foreach (float d in new[] { 45f, -45f, 55f, -55f, 65f, -65f, 75f, -75f })
                if (OtobusIsinla.ZemineTasi(bus, eski + bus.transform.right * d, donus, yerden) && OtobusIsinla.Temas(bus) == null)
                {
                    Log($"{d:+0;-0} m yana ışınlandı: temas={Temaslar()}");
                    KareIzle(1.5f);
                    break;
                }
            yield return new WaitForSeconds(1.5f);
            Log($"yanda: hız={bus.SpeedKmh:F1} km/s ayrılma={Ayrilma():F3} temas={Temaslar()}");
            OtobusIsinla.Tasi(bus, eski, donus);
            Log($"geri ışınlandı: {KareDurumu()}");
            KareIzle(1.5f);
            yield return new WaitForSeconds(1f);
        }
    }
}
