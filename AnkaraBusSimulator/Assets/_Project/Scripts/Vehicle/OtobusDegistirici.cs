using AnkaraBus.Route;
using AnkaraBus.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnkaraBus.Vehicle
{
    /// <summary>
    /// Hat sahnelerinde varsayılan otobüs (BMC) vardır. Menüde başka bir otobüs seçildiyse sahne yüklenince,
    /// sahnedeki bileşenlerin Start'ından önce, otobüsü seçilen prefabla değiştirir ve rota, kamera, dokunmatik
    /// kontroller ve gösterge bağlantılarını yeni otobüse taşır.
    /// </summary>
    public static class OtobusDegistirici
    {
        // diğer sceneLoaded dinleyicilerinden (RotaRehberi, HavaVeZaman, YayaGecitleri) önce çalışsın
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Baslat()
        {
            SceneManager.sceneLoaded -= SahneYuklendi;
            SceneManager.sceneLoaded += SahneYuklendi;
        }

        // ilk sahne için sceneLoaded gelmeyebilir (Editor'de Play, uygulama açılışı); Start'lardan önce burada da bak
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void IlkSahne() => SahneYuklendi(SceneManager.GetActiveScene(), LoadSceneMode.Single);

        private static void SahneYuklendi(Scene sahne, LoadSceneMode mod)
        {
            var katalog = OtobusKatalogu.Yukle();
            var secili = katalog != null ? katalog.Secili : null;
            if (secili == null || secili.prefab == null)
                return;
            foreach (var eski in Object.FindObjectsByType<BusVehicle>())
            {
                // yalnızca oyuncunun otobüsü (rotası olan); vitrin ve test pistindekine dokunma
                var eskiTakip = eski.GetComponent<RouteTracker>();
                if (eskiTakip == null || eski.Definition == secili || eski.gameObject.scene != sahne)
                    continue;
                Degistir(eski, eskiTakip, secili);
            }
        }

        /// <summary>
        /// Yeni otobüsün gövde kutuları bir binaya, durağa ya da araca değmeyecek ilk konum: önce verilen yer, sonra 1 m'lik
        /// adımlarla ileri (en çok 10 m), sonra geri. Uzun otobüs (körüklü) eskisinin yerine konunca arkası bir engelin içinde
        /// kalabiliyordu; fizik onu oradan iterken mafsal arka gövdeyi savuruyor, otobüs fırlıyordu.
        /// </summary>
        private static Vector3 BosYer(GameObject prefab, Vector3 konum, Quaternion donus, Transform eski)
        {
            Physics.SyncTransforms();
            var kok = prefab.transform;
            var kutular = new System.Collections.Generic.List<(Vector3 merkez, Vector3 yari, Quaternion don)>();
            foreach (var c in prefab.GetComponentsInChildren<BoxCollider>(true))
            {
                if (!c.name.StartsWith("Carpisma_"))
                    continue;
                var yari = Vector3.Scale(c.size * 0.5f, c.transform.lossyScale) - Vector3.one * 0.05f;
                kutular.Add((kok.InverseTransformPoint(c.transform.TransformPoint(c.center)), Vector3.Max(yari, Vector3.one * 0.01f),
                             Quaternion.Inverse(kok.rotation) * c.transform.rotation));
            }
            if (kutular.Count == 0)
                return konum;
            Vector3 ileri = donus * Vector3.forward;
            for (int i = 0; i <= 14; i++)
            {
                // 0, +1 … +10, −1 … −4 m
                float kayma = i <= 10 ? i : 10 - i;
                var aday = konum + ileri * kayma;
                bool cakisti = false;
                foreach (var k in kutular)
                {
                    foreach (var h in Physics.OverlapBox(aday + donus * k.merkez, k.yari, donus * k.don, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (h is WheelCollider || h.transform.IsChildOf(eski))
                            continue;
                        cakisti = true;
                        if (i == 0)
                            Debug.Log($"[Otobus] başlangıç yeri dolu ({h.name}); yer aranıyor");
                        break;
                    }
                    if (cakisti)
                        break;
                }
                if (!cakisti)
                {
                    if (i > 0)
                        Debug.Log($"[Otobus] otobüs {kayma:+0;-0} m kaydırıldı (çakışma yok)");
                    return aday;
                }
            }
            Debug.LogWarning("[Otobus] boş yer bulunamadı, otobüs başlangıç yerine kondu");
            return konum;
        }

        private static void Degistir(BusVehicle eski, RouteTracker eskiTakip, BusDefinition secili)
        {
            var t = eski.transform;
            // ön uçları hizala: durakta / başlangıçta yeni otobüs de aynı çizgide dursun, uzunsa geriye uzasın
            float eskiOn = OtobusOlcusu.Olc(eski).On;
            float yeniOn = OtobusOlcusu.Olc(secili.prefab.transform).On;
            var konum = BosYer(secili.prefab, t.position + t.forward * (eskiOn - yeniOn), t.rotation, t);
            var yeniGo = Object.Instantiate(secili.prefab, konum, t.rotation, t.parent);
            yeniGo.name = secili.prefab.name;
            var yeni = yeniGo.GetComponent<BusVehicle>();
            var yeniTakip = yeniGo.GetComponent<RouteTracker>();
            if (yeniTakip != null && eskiTakip.Route != null)
                yeniTakip.AssignRoute(eskiTakip.Route);

            foreach (var rig in Object.FindObjectsByType<BusCameraRig>())
                rig.SetTarget(yeni);
            foreach (var kontrol in Object.FindObjectsByType<BusTouchControls>())
                kontrol.Bind(yeniGo.GetComponent<BusInput>(), Object.FindAnyObjectByType<BusCameraRig>());
            foreach (var hud in Object.FindObjectsByType<BusHud>())
                hud.Baglan(yeniTakip);

            // hemen yok et: Start'ta FindAnyObjectByType eski otobüsü bulmasın
            Object.DestroyImmediate(eski.gameObject);
            Debug.Log($"[Otobus] {secili.displayName} seçildi");
        }
    }
}
