using System.Collections;
using AnkaraBus.Route;
using AnkaraBus.Vehicle;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnkaraBus.Diagnostics
{
    /// <summary>
    /// Otomatik performans turu. Sahnede hat varsa otobüsü her durağa taşıyıp dış ve kokpit
    /// kamerasıyla ölçer; yoksa duran otobüs, kokpit ve tam gaz sürüşü ölçer.
    /// Sonuçlar "[Perf]" satırları olarak loga yazılır (adb logcat -s Unity).
    /// Yalnızca performans build'inde sahneye eklenir.
    /// </summary>
    public class PerfBenchmark : MonoBehaviour
    {
        [SerializeField] private string label = "Sahne";
        [SerializeField] private float warmupSeconds = 5f;
        [SerializeField] private float segmentSeconds = 10f;
        [Tooltip("Bitince yüklenecek sahne. Boşsa tur biter.")]
        [SerializeField] private string nextScene;

        private PerfProbe probe;
        private bool measuring;

        private void Update()
        {
            if (measuring)
                probe.Sample();
        }

        private IEnumerator Start()
        {
            var bus = FindAnyObjectByType<BusVehicle>();
            var rig = FindAnyObjectByType<BusCameraRig>();
            var input = bus != null ? bus.GetComponent<BusInput>() : null;
            if (bus == null || rig == null)
            {
                Debug.LogError("[Perf] Otobüs veya kamera bulunamadı");
                yield break;
            }

            probe = new PerfProbe();
            Debug.Log($"[Perf] BASLA {label} cihaz={SystemInfo.deviceModel} gpu={SystemInfo.graphicsDeviceName} " +
                      $"api={SystemInfo.graphicsDeviceType} kalite={GrafikAyarlari.Mevcut} ram_mb={SystemInfo.systemMemorySize} ekran={Screen.width}x{Screen.height}");
            yield return new WaitForSeconds(warmupSeconds);

            var route = FindAnyObjectByType<BusRoute>();
            if (route != null && route.StopCount > 0)
            {
                for (int i = 0; i < route.StopCount; i++)
                {
                    var stop = route.GetStop(i);
                    Teleport(bus, stop.transform);
                    rig.SetTarget(bus);
                    yield return Measure($"{label}/{i + 1}_{stop.StopName}/dis", rig, BusCameraRig.Mode.Chase);
                    yield return Measure($"{label}/{i + 1}_{stop.StopName}/kokpit", rig, BusCameraRig.Mode.Cockpit);
                }
            }
            else
            {
                yield return Measure($"{label}/duran/dis", rig, BusCameraRig.Mode.Chase);
                yield return Measure($"{label}/duran/kokpit", rig, BusCameraRig.Mode.Cockpit);
                if (input != null)
                    input.AutoThrottle = 1f;
                yield return Measure($"{label}/surus/dis", rig, BusCameraRig.Mode.Chase);
                if (input != null)
                    input.AutoThrottle = 0f;
            }

            probe.Dispose();
            if (!string.IsNullOrEmpty(nextScene))
            {
                Debug.Log($"[Perf] SONRAKI {nextScene}");
                SceneManager.LoadScene(nextScene);
            }
            else
            {
                Debug.Log("[Perf] BITTI");
            }
        }

        private IEnumerator Measure(string name, BusCameraRig rig, BusCameraRig.Mode mode)
        {
            rig.SetMode(mode);
            yield return new WaitForSeconds(1.5f);
            probe.Reset();
            measuring = true;
            yield return new WaitForSeconds(segmentSeconds);
            measuring = false;
            Debug.Log(probe.Report(name));
        }

        private static void Teleport(BusVehicle bus, Transform target)
        {
            var body = bus.GetComponent<Rigidbody>();
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = target.position + Vector3.up * 0.3f;
            body.rotation = Quaternion.Euler(0f, target.eulerAngles.y, 0f);
            bus.transform.SetPositionAndRotation(body.position, body.rotation);
            bus.Handbrake = true;
        }
    }
}
