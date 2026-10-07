using System;
using System.Text;
using AnkaraBus.Vehicle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AnkaraBus.EditorTools
{
    /// <summary>
    /// Otobüs prefabını boş bir sahnede fizik adımlarıyla sürüp ölçer: hızlanma, fren, dönüş çapı,
    /// yokuş kalkışı, kapı freni. Sonuçları konsola yazar; ayar yaparken karşılaştırmak için.
    /// Menü: Ankara Bus > Sürüş Testi (açık sahne kaydedilmeden kapatılır)
    /// Komut satırı: -executeMethod AnkaraBus.EditorTools.OtobusSurusTesti.RunBatch
    /// </summary>
    public static class OtobusSurusTesti
    {
        private const float Dt = 0.02f;

        [MenuItem("Ankara Bus/Sürüş Testi")]
        public static void RunMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            Debug.Log(Run());
        }

        public static void RunBatch()
        {
            int code = 0;
            try
            {
                Debug.Log(Run());
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                code = 1;
            }
            EditorApplication.Exit(code);
        }

        public static string Run()
        {
            var report = new StringBuilder("[SurusTesti]\n");
            var previousMode = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
            try
            {
                Flat(report);
                Turning(report);
                Hill(report, 10f);
                Hill(report, 12f);
                Doors(report);
            }
            finally
            {
                Physics.simulationMode = previousMode;
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            return report.ToString();
        }

        private static BusVehicle Spawn(float gradePercent, out Transform ground)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            float angle = Mathf.Atan(gradePercent / 100f) * Mathf.Rad2Deg;
            var tilt = Quaternion.Euler(-angle, 0f, 0f);

            var plane = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plane.name = "Zemin";
            plane.transform.localScale = new Vector3(600f, 1f, 3000f);
            plane.transform.SetPositionAndRotation(tilt * new Vector3(0f, -0.5f, 0f), tilt);
            ground = plane.transform;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OtobusKurucu.PrefabPath);
            if (prefab == null)
                throw new InvalidOperationException("Önce Ankara Bus > BMC Procity Prefabını Kur.");
            var bus = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            bus.transform.SetPositionAndRotation(tilt * new Vector3(0f, 0.05f, -1200f), tilt);

            var vehicle = bus.GetComponent<BusVehicle>();
            vehicle.Initialize();
            vehicle.Handbrake = true;
            Simulate(vehicle, 3f);
            return vehicle;
        }

        private static void Simulate(BusVehicle vehicle, float seconds, Action<float> each = null)
        {
            int steps = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < steps; i++)
            {
                vehicle.Step(Dt);
                Physics.Simulate(Dt);
                each?.Invoke((i + 1) * Dt);
            }
        }

        private static void Drive(BusVehicle v, float throttle, float brake, float steer, bool handbrake)
        {
            v.Throttle = throttle;
            v.Brake = brake;
            v.Steer = steer;
            v.Handbrake = handbrake;
        }

        private static void Flat(StringBuilder r)
        {
            var v = Spawn(0f, out _);
            Vector3 rest = v.transform.position;
            r.AppendLine($"Duran otobüs yüksekliği: {rest.y:F3} m (tekerlek altı 0), eğim {Vector3.Angle(v.transform.up, Vector3.up):F2}°");

            Drive(v, 1f, 0f, 0f, false);
            float[] marks = { 10f, 20f, 30f, 50f, 70f };
            int next = 0;
            var gears = new StringBuilder();
            int lastGear = 0;
            Simulate(v, 60f, t =>
            {
                if (next < marks.Length && v.SpeedKmh >= marks[next])
                {
                    r.AppendLine($"  0-{marks[next]:F0} km/s: {t:F1} sn");
                    next++;
                }
                if (v.Gear != lastGear)
                {
                    gears.Append($"{v.Gear}.vites@{v.SpeedKmh:F0} ");
                    lastGear = v.Gear;
                }
            });
            r.AppendLine($"  Tam gaz 60 sn: {v.SpeedKmh:F1} km/s, {v.EngineRpm:F0} rpm. Vitesler: {gears}");

            // Fren: 50 km/s'den tam fren
            Drive(v, 0f, 1f, 0f, false);
            Simulate(v, 0.1f);
            SlowTo(v, 50f);
            Vector3 start = v.transform.position;
            float time = 0f;
            Drive(v, 0f, 1f, 0f, false);
            Simulate(v, 15f, t => { if (time == 0f && v.SpeedKmh < 0.5f) time = t; });
            r.AppendLine($"  50→0 tam fren: {Vector3.Distance(start, v.transform.position):F1} m, {time:F1} sn");

            // Durma: frende 10 sn kayma
            Vector3 stopped = v.transform.position;
            Simulate(v, 10f);
            r.AppendLine($"  Frende 10 sn kayma: {Vector3.Distance(stopped, v.transform.position) * 100f:F1} cm");

            // Sürünme: D'de gaz ve fren yok
            Drive(v, 0f, 0f, 0f, false);
            Simulate(v, 15f);
            r.AppendLine($"  Sürünme hızı (D, gazsız): {v.SpeedKmh:F1} km/s");
        }

        private static void SlowTo(BusVehicle v, float kmh)
        {
            Drive(v, 0f, 0.6f, 0f, false);
            for (int i = 0; i < 3000 && v.SpeedKmh > kmh; i++)
                Simulate(v, Dt);
        }

        private static void Turning(StringBuilder r)
        {
            var v = Spawn(0f, out _);
            Drive(v, 0.25f, 0f, 1f, false);
            Simulate(v, 5f);
            Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
            float speed = 0f;
            Simulate(v, 25f, t =>
            {
                Drive(v, v.SpeedKmh < 8f ? 0.3f : 0f, v.SpeedKmh > 10f ? 0.2f : 0f, 1f, false);
                min = Vector3.Min(min, v.transform.position);
                max = Vector3.Max(max, v.transform.position);
                speed = v.SpeedKmh;
            });
            float diameter = Mathf.Max(max.x - min.x, max.z - min.z);
            r.AppendLine($"Tam kilit dönüş (~{speed:F0} km/s): orta nokta çapı {diameter:F1} m");

            // Yüksek hızda şerit değişimi: devrilme/kayma kontrolü
            v = Spawn(0f, out _);
            Drive(v, 1f, 0f, 0f, false);
            for (int i = 0; i < 5000 && v.SpeedKmh < 50f; i++)
                Simulate(v, Dt);
            float maxRoll = 0f;
            Simulate(v, 6f, t =>
            {
                Drive(v, 0.5f, 0f, t < 1.5f ? 0.5f : t < 3f ? -0.5f : 0f, false);
                maxRoll = Mathf.Max(maxRoll, Mathf.Abs(Mathf.DeltaAngle(0f, v.transform.eulerAngles.z)));
            });
            r.AppendLine($"50 km/s'de zikzak: en fazla yatma {maxRoll:F1}°, sonra {v.SpeedKmh:F0} km/s");
        }

        private static void Hill(StringBuilder r, float grade)
        {
            var v = Spawn(grade, out _);
            Vector3 p0 = v.transform.position;
            Simulate(v, 5f);
            r.AppendLine($"%{grade:F0} yokuş:");
            r.AppendLine($"  El freninde 5 sn kayma: {Vector3.Distance(p0, v.transform.position) * 100f:F1} cm");

            Vector3 p1 = v.transform.position;
            Drive(v, 0f, 0f, 0f, false);
            Simulate(v, 3f);
            float rolled = Vector3.Dot(v.transform.position - p1, v.transform.forward);
            r.AppendLine($"  El freni bırakılınca 3 sn (gazsız, D): {rolled:F2} m ({(rolled < 0 ? "geri kaçtı" : "ileri")})");

            // Fren pedalından gaza geçiş (yokuş kalkış desteği)
            var vh = Spawn(grade, out _);
            Drive(vh, 0f, 1f, 0f, false);
            Simulate(vh, 2f);
            Vector3 ph = vh.transform.position;
            float minHold = 0f;
            Drive(vh, 0f, 0f, 0f, false);
            Simulate(vh, 1f, t => minHold = Mathf.Min(minHold, Vector3.Dot(vh.transform.position - ph, vh.transform.forward)));
            Drive(vh, 1f, 0f, 0f, false);
            Simulate(vh, 5f, t => minHold = Mathf.Min(minHold, Vector3.Dot(vh.transform.position - ph, vh.transform.forward)));
            r.AppendLine($"  Fren bırak, 1 sn sonra gaz: en fazla geri kaçma {-minHold:F2} m");

            var v2 = Spawn(grade, out _);
            Vector3 p2 = v2.transform.position;
            float minAlong = 0f;
            Drive(v2, 1f, 0f, 0f, false);
            Simulate(v2, 20f, t => minAlong = Mathf.Min(minAlong, Vector3.Dot(v2.transform.position - p2, v2.transform.forward)));
            r.AppendLine($"  Tam gaz kalkış: en fazla geri kaçma {-minAlong:F2} m, 20 sn sonra {v2.SpeedKmh:F1} km/s, {v2.Gear}. vites");
        }

        private static void Doors(StringBuilder r)
        {
            var v = Spawn(0f, out _);
            var doors = v.GetComponentInChildren<BusDoorController>();
            doors.ToggleAll();
            Vector3 p = v.transform.position;
            Drive(v, 1f, 0f, 0f, false);
            Simulate(v, 5f);
            r.AppendLine($"Kapılar açık, tam gaz 5 sn: {Vector3.Distance(p, v.transform.position):F2} m, durak freni {(v.DoorBrakeActive ? "aktif" : "pasif")}");
        }
    }
}
