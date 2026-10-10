using System.Collections.Generic;
using AnkaraBus.Gameplay;
using AnkaraBus.Passengers;
using AnkaraBus.Route;
using AnkaraBus.Vehicle;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnkaraBus.Traffic
{
    /// <summary>
    /// Durakların hemen ilerisine yaya geçitleri (zebra çizgisi) koyar ve yayaları karşıya geçirir. Sahneye eklenmesi
    /// gerekmez: rota ve trafik şeritleri olan her sahne yüklenince kendiliğinden oluşur.
    /// <list type="bullet">
    /// <item>Geçit, durağın 26 m ilerisinde bütün yolu keser; kavşak içine (dik şerit yakınsa) konmaz.</item>
    /// <item>Yayalar (yolcu modelleri, PassengerManager havuzundan) 7–20 sn'de bir, 1–2 kişi, yaklaşan araç yoksa geçer.</item>
    /// <item>Geçit doluyken trafik araçları önünde durur (TrafficLane.IGecit).</item>
    /// <item>Otobüs: yayaya çarparsa ya da üzerinde yaya varken geçidi geçerse ceza (SeferPuanlama.YayaIhlali).</item>
    /// </list>
    /// Ayrıntı: docs/TRAFIK.md (Yayalar)
    /// </summary>
    public class YayaGecitleri : MonoBehaviour
    {
        private const float DuraktanUzaklik = 26f;
        private const float SeritYariGenislik = 1.75f;

        private readonly List<Gecit> gecitler = new List<Gecit>();
        private BusVehicle otobus;
        private SeferPuanlama puanlama;
        private Material palet;
        private OtobusOlcusu olcu = OtobusOlcusu.Varsayilan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Baslat()
        {
            SceneManager.sceneLoaded -= SahneYuklendi;
            SceneManager.sceneLoaded += SahneYuklendi;
            SahneYuklendi(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void SahneYuklendi(Scene sahne, LoadSceneMode mod)
        {
            if (FindAnyObjectByType<YayaGecitleri>() != null || FindAnyObjectByType<BusRoute>() == null
                || FindAnyObjectByType<TrafficLane>() == null)
                return;
            new GameObject("YayaGecitleri").AddComponent<YayaGecitleri>();
        }

        private void Start()
        {
            otobus = FindAnyObjectByType<BusVehicle>();
            puanlama = otobus != null ? otobus.GetComponent<SeferPuanlama>() : null;
            olcu = OtobusOlcusu.Olc(otobus);
            palet = PaletBul();
            foreach (var stop in FindObjectsByType<BusStop>())
                GecitKur(stop);
        }

        private void Update()
        {
            foreach (var g in gecitler)
                g.Guncelle(Time.deltaTime);
        }

        private static Material PaletBul()
        {
            foreach (var kok in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (Transform grup in kok.transform)
                    if (grup.name == "Yol")
                    {
                        var r = grup.GetComponentInChildren<MeshRenderer>();
                        if (r != null)
                            return r.sharedMaterial;
                    }
            return null;
        }

        private void GecitKur(BusStop stop)
        {
            Vector3 ileri = stop.transform.forward;
            ileri.y = 0f;
            ileri.Normalize();
            Vector3 sag = Vector3.Cross(Vector3.up, ileri);
            Vector3 merkez = stop.transform.position + ileri * DuraktanUzaklik;

            var seritler = new List<(TrafficLane serit, float at, float yan, float y)>();
            foreach (var serit in TrafficLane.Hepsi)
            {
                float at = serit.EnYakinKonum(merkez, out float uzaklik);
                if (uzaklik > 40f)
                    continue;
                serit.Sample(at, out var q, out var f);
                float boyuna = Vector3.Dot(q - merkez, ileri);
                if (Mathf.Abs(Vector3.Dot(f, ileri)) < 0.9f)
                {
                    if (uzaklik < 15f)
                        return; // kavşak: dik şerit çok yakın
                    continue;
                }
                if (Mathf.Abs(boyuna) > 3f || serit.Length - at < 5f || at < 5f)
                    continue;
                seritler.Add((serit, at, Vector3.Dot(q - merkez, sag), q.y));
            }
            if (seritler.Count < 2)
                return;

            float yanMin = float.MaxValue, yanMax = float.MinValue;
            foreach (var s in seritler)
            {
                yanMin = Mathf.Min(yanMin, s.yan);
                yanMax = Mathf.Max(yanMax, s.yan);
            }
            var go = new GameObject("YayaGecidi_" + stop.StopName);
            go.transform.SetParent(transform, false);
            var gecit = new Gecit(this, merkez, ileri, sag, yanMin - SeritYariGenislik - 0.3f, yanMax + SeritYariGenislik + 0.3f, seritler);
            foreach (var s in seritler)
                s.serit.GecitEkle(s.at, gecit);
            if (palet != null)
                ZebraCiz(go, gecit, seritler);
            gecitler.Add(gecit);
        }

        /// <summary>Beyaz bantlar: yalnızca şerit olan yerlerde (refüjün içine girmez), yol yüzeyinin 3 cm üstünde.</summary>
        private void ZebraCiz(GameObject go, Gecit g, List<(TrafficLane serit, float at, float yan, float y)> seritler)
        {
            // palet: serit_beyaz (tools/blender/apartman_kit.py PALETTE sırası 42; 16×16 ızgara)
            var uv = new Vector2((42 % 16 + 0.5f) / 16f, 1f - (42 / 16 + 0.5f) / 16f);
            var verts = new List<Vector3>();
            var tris = new List<int>();
            const float bant = 0.5f, aralik = 0.6f, boy = 3f;
            for (float yan = g.YanMin + 0.3f; yan + bant <= g.YanMax - 0.3f; yan += bant + aralik)
            {
                float orta = yan + bant * 0.5f;
                float? y = null;
                foreach (var s in seritler)
                    if (Mathf.Abs(s.yan - orta) <= SeritYariGenislik + 0.05f)
                        y = s.y;
                if (y == null)
                    continue;
                Vector3 a = g.Merkez + g.Sag * yan, b = g.Merkez + g.Sag * (yan + bant);
                a.y = b.y = y.Value + 0.03f;
                int i = verts.Count;
                verts.Add(a - g.Ileri * (boy * 0.5f));
                verts.Add(a + g.Ileri * (boy * 0.5f));
                verts.Add(b + g.Ileri * (boy * 0.5f));
                verts.Add(b - g.Ileri * (boy * 0.5f));
                tris.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            }
            if (verts.Count == 0)
                return;
            var mesh = new Mesh { name = go.name };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            var uvs = new Vector2[verts.Count];
            for (int i = 0; i < uvs.Length; i++)
                uvs[i] = uv;
            mesh.uv = uvs;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = palet;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // ---------------------------------------------------------------- geçit

        private class Gecit : TrafficLane.IGecit
        {
            private readonly YayaGecitleri sahip;
            public readonly Vector3 Merkez, Ileri, Sag;
            public readonly float YanMin, YanMax;
            private readonly List<Yaya> yayalar = new List<Yaya>();
            private float zamanlayici = Random.Range(3f, 10f);
            private float sonBoyuna = float.NaN;
            private float sonCeza = -10f;

            public Gecit(YayaGecitleri sahip, Vector3 merkez, Vector3 ileri, Vector3 sag, float yanMin, float yanMax,
                         List<(TrafficLane serit, float at, float yan, float y)> seritler)
            {
                this.sahip = sahip;
                Merkez = merkez;
                Ileri = ileri;
                Sag = sag;
                YanMin = yanMin;
                YanMax = yanMax;
            }

            public bool Dolu
            {
                get
                {
                    foreach (var y in yayalar)
                        if (y.YoldaMi)
                            return true;
                    return false;
                }
            }

            public float YariGenislik => 2f;

            public void Guncelle(float dt)
            {
                var otobus = sahip.otobus;
                bool yakin = otobus == null || (otobus.transform.position - Merkez).sqrMagnitude < 170f * 170f;

                for (int i = yayalar.Count - 1; i >= 0; i--)
                    if (yayalar[i].Bitti)
                    {
                        yayalar[i].Birak();
                        yayalar.RemoveAt(i);
                    }

                zamanlayici -= dt;
                if (zamanlayici <= 0f)
                {
                    zamanlayici = Random.Range(7f, 20f);
                    if (yakin && yayalar.Count == 0 && PassengerManager.Instance != null)
                    {
                        bool sagdan = Random.value < 0.5f;
                        int kisi = Random.value < 0.35f ? 2 : 1;
                        for (int k = 0; k < kisi; k++)
                        {
                            var y = Yaya.Olustur(this, sagdan, k);
                            if (y != null)
                                yayalar.Add(y);
                        }
                    }
                }
                bool guvenli = GuvenliMi();
                foreach (var y in yayalar)
                    y.Guncelle(guvenli, sahip.otobus);

                YolVermeKontrolu();
            }

            /// <summary>
            /// Geçide 30 m'den yakın, ona doğru 4 m/sn'den hızlı gelen ya da 12 m'den yakın veya 6 sn'de varacak kadar hızlı gelen
            /// araç ya da otobüs yoksa güvenli. Y15: 10 km/s ile geçide gelen otobüsü yayalar "yaklaşmıyor" sayıp burnunun önüne
            /// çıkıyordu ("Yayaya çarptın", Hat 1 Kuğulu sonrası geçit).
            /// </summary>
            private bool GuvenliMi()
            {
                var otobus = sahip.otobus;
                // otobüsün önü (körüklüde kök önden 9 m geride)
                if (otobus != null && Yaklasiyor(otobus.transform.position + otobus.transform.forward * sahip.olcu.On,
                                                 otobus.GetComponent<Rigidbody>().linearVelocity))
                    return false;
                foreach (var car in TrafficCar.Aktifler)
                    if (Yaklasiyor(car.transform.position, car.transform.forward * car.Speed))
                        return false;
                return true;
            }

            private bool Yaklasiyor(Vector3 p, Vector3 hiz)
            {
                Vector3 d = Merkez - p;
                d.y = 0f;
                float uzaklik = d.magnitude;
                float yaklasma = uzaklik > 0.01f ? Vector3.Dot(hiz, d) / uzaklik : 0f;
                if (uzaklik > 30f || yaklasma < 0.5f)
                    return false;
                if (hiz.sqrMagnitude < 16f && uzaklik > 12f && uzaklik / yaklasma > 6f)
                    return false;
                float yan = Vector3.Dot(p - Merkez, Sag);
                return yan > YanMin - 3f && yan < YanMax + 3f;
            }

            /// <summary>Otobüsün önü, üzerinde (otobüsün 7 m yakınında) yaya varken geçit çizgisini geçerse ceza.</summary>
            private void YolVermeKontrolu()
            {
                var otobus = sahip.otobus;
                if (otobus == null || sahip.puanlama == null)
                    return;
                Vector3 on = otobus.transform.position + otobus.transform.forward * sahip.olcu.On;
                float boyuna = Vector3.Dot(on - Merkez, Ileri);
                float yan = Vector3.Dot(on - Merkez, Sag);
                bool icinde = yan > YanMin && yan < YanMax && Mathf.Abs(boyuna) < 8f;
                if (icinde && !float.IsNaN(sonBoyuna) && Mathf.Sign(boyuna) != Mathf.Sign(sonBoyuna) && Time.time - sonCeza > 5f)
                    foreach (var y in yayalar)
                        if (y.YoldaMi && Mathf.Abs(y.Yan - yan) < 7f)
                        {
                            sonCeza = Time.time;
                            sahip.puanlama.YayaIhlali(false);
                            break;
                        }
                sonBoyuna = icinde ? boyuna : float.NaN;
            }

            public void Carpti()
            {
                if (sahip.puanlama != null)
                    sahip.puanlama.YayaIhlali(true);
            }

            public Vector3 Nokta(float yan, float boyuna)
            {
                Vector3 p = Merkez + Sag * yan + Ileri * boyuna;
                if (Physics.Raycast(p + Vector3.up * 4f, Vector3.down, out var hit, 10f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                    && hit.rigidbody == null)
                    p.y = hit.point.y;
                return p;
            }

            public float YanKonumu(Vector3 p) => Vector3.Dot(p - Merkez, Sag);

            public OtobusOlcusu Olcu => sahip.olcu;
        }

        // ---------------------------------------------------------------- yaya

        /// <summary>Karşıya geçen bir yaya: kaldırımda bekler, güvenliyse geçer, karşıda biraz yürüyüp kaybolur.</summary>
        private class Yaya
        {
            private enum Durum { Bekliyor, Geciyor, Uzaklasiyor, Dustu, Bitti }

            private Gecit gecit;
            private Passenger passenger;
            private Durum durum;
            private Vector3 karsi, son;
            private float bekleme;

            public bool YoldaMi => durum == Durum.Geciyor || durum == Durum.Dustu;
            public bool Bitti => durum == Durum.Bitti;
            public float Yan => gecit.YanKonumu(passenger.transform.position);

            public static Yaya Olustur(Gecit gecit, bool sagdan, int sira)
            {
                float bas = sagdan ? gecit.YanMax + 1.3f : gecit.YanMin - 1.3f;
                float bit = sagdan ? gecit.YanMin - 1.3f : gecit.YanMax + 1.3f;
                float boyuna = (sira == 0 ? -0.6f : 0.7f) + Random.Range(-0.3f, 0.3f);
                Vector3 baslangic = gecit.Nokta(bas, boyuna);
                var p = PassengerManager.Instance.Get(baslangic, Quaternion.LookRotation(sagdan ? -gecit.Sag : gecit.Sag));
                if (p == null)
                    return null;
                return new Yaya
                {
                    gecit = gecit,
                    passenger = p,
                    karsi = gecit.Nokta(bit, boyuna),
                    son = gecit.Nokta(bit + (sagdan ? -1f : 1f) * 1.5f, boyuna + Random.Range(4f, 8f) * (Random.value < 0.5f ? -1f : 1f)),
                    bekleme = Random.Range(0.5f, 2f) + sira * 0.4f,
                };
            }

            public void Guncelle(bool guvenli, BusVehicle otobus)
            {
                switch (durum)
                {
                    case Durum.Bekliyor:
                        bekleme -= Time.deltaTime;
                        if (bekleme <= 0f && guvenli)
                        {
                            durum = Durum.Geciyor;
                            passenger.WalkTo(karsi, () =>
                            {
                                durum = Durum.Uzaklasiyor;
                                passenger.WalkTo(son, () => durum = Durum.Bitti);
                            });
                        }
                        break;
                    case Durum.Geciyor:
                        if (otobus != null && Carpiyor(otobus))
                            Dus();
                        break;
                    case Durum.Dustu:
                        bekleme -= Time.deltaTime;
                        if (bekleme <= 0f)
                            durum = Durum.Bitti;
                        break;
                }
            }

            /// <summary>Yaya otobüsün gövde kutusunun içinde mi (otobüs giderken)? Fizik bileşeni gerekmez.</summary>
            private bool Carpiyor(BusVehicle otobus)
            {
                if (otobus.SpeedKmh < 4f)
                    return false;
                // gövde kutuları: otobüsün boyuna göre (körüklüde arka gövde dahil)
                return gecit.Olcu.Icinde(passenger.transform.position + Vector3.up * 0.9f);
            }

            /// <summary>Düşer, 4 sn yerde kalır (o sürede geçit dolu sayılır, araçlar bekler).</summary>
            private void Dus()
            {
                var t = passenger.transform;
                passenger.Stand(t.forward);
                t.rotation = Quaternion.LookRotation(Vector3.up, -t.forward);
                durum = Durum.Dustu;
                bekleme = 4f;
                gecit.Carpti();
            }

            public void Birak()
            {
                var t = passenger.transform;
                Vector3 duz = Vector3.ProjectOnPlane(t.up, Vector3.up);
                if (durum == Durum.Bitti && duz.sqrMagnitude > 0.01f)
                    t.rotation = Quaternion.LookRotation(duz.normalized); // düşmüşse ayağa kaldır
                if (PassengerManager.Instance != null)
                    PassengerManager.Instance.Release(passenger);
                else
                    passenger.gameObject.SetActive(false);
            }
        }
    }
}
