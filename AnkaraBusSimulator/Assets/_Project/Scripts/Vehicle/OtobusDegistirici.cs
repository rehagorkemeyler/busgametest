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

        private static void Degistir(BusVehicle eski, RouteTracker eskiTakip, BusDefinition secili)
        {
            var t = eski.transform;
            // ön uçları hizala: durakta / başlangıçta yeni otobüs de aynı çizgide dursun, uzunsa geriye uzasın
            float eskiOn = OtobusOlcusu.Olc(eski).On;
            float yeniOn = OtobusOlcusu.Olc(secili.prefab.transform).On;
            var konum = t.position + t.forward * (eskiOn - yeniOn);
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
