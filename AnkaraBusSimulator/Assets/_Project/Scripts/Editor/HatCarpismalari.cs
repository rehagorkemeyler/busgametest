using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AnkaraBus.EditorTools
{
    /// <summary>
    /// Haritadaki binalara, simge yapılara ve durak çatılarına çarpışma ekler (otobüs içlerinden geçmesin, kamera
    /// duvarın içine girmesin, çarpınca puan cezası gelsin). Apartmanlar ve Kızılay blokları kutu (ucuz, binalar
    /// kutuya yakın), simge yapılar gerçek şekliyle (MeshCollider) çarpışır. Duraklar da kutu: sundurma ince paneller ve
    /// çatıdan oluşur; içbükey çarpışmada otobüs kutusu üçgenlerin içine giriyor, dışbükey kabukta yere inen eğik yüze
    /// biniyordu, ikisinde de körüklü fırlayıp devriliyordu (Y14, docs/RAPOR_Y14.md). Ağaç ve lambalar eklenmez.
    /// HaritaKurucu haritayı kurarken çağırır; var olan sahneler için menü (ışık bake'i bozulmaz, yeniden bake gerekmez):
    /// Ankara Bus > Hat Sahnelerine Çarpışma Ekle
    /// </summary>
    public static class HatCarpismalari
    {
        private static readonly string[] KutuGruplari = { "Binalar", "Duraklar" };
        private static readonly string[] SekilGruplari = { "SimgeYapilar" };

        /// <summary>Var olan hat sahnelerinde durakların MeshCollider'ını kutuyla değiştirir (Y14 fırlama düzeltmesi).</summary>
        [MenuItem("Ankara Bus/Durak Çarpışmalarını Kutu Yap")]
        public static void DuraklariKutuYap()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            foreach (var yol in new[] { Hat1SahneKurucu.ScenePath, Hat2SahneKurucu.ScenePath })
            {
                var sahne = EditorSceneManager.OpenScene(yol);
                int adet = 0;
                foreach (var kok in sahne.GetRootGameObjects())
                    foreach (Transform grup in kok.transform)
                        if (grup.name == "Duraklar")
                            foreach (var mf in grup.GetComponentsInChildren<MeshFilter>(true))
                            {
                                if (mf.sharedMesh == null || mf.GetComponent<LODGroup>() != null)
                                    continue;
                                foreach (var c in mf.GetComponents<Collider>())
                                    Object.DestroyImmediate(c);
                                DurakKutusu(mf);
                                adet++;
                            }
                EditorSceneManager.SaveScene(sahne);
                Debug.Log($"[Carpisma] {sahne.name}: {adet} durak çarpışması kutu yapıldı");
            }
            if (Application.isBatchMode)
                EditorApplication.Exit(0);
        }

        [MenuItem("Ankara Bus/Hat Sahnelerine Çarpışma Ekle")]
        public static void Menu()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            foreach (var yol in new[] { Hat1SahneKurucu.ScenePath, Hat2SahneKurucu.ScenePath })
            {
                var sahne = EditorSceneManager.OpenScene(yol);
                int adet = 0;
                foreach (var kok in sahne.GetRootGameObjects())
                    adet += Ekle(kok.transform);
                EditorSceneManager.SaveScene(sahne);
                Debug.Log($"[Carpisma] {sahne.name}: {adet} çarpışma eklendi");
            }
        }

        /// <summary>
        /// Durak kutusu: genişliği ve derinliği yerden 2,2 m'ye kadarki köşelerden (direkler, arka duvar, bank), yüksekliği
        /// tamamından. Çatının yola taşan saçağı kutuya girmez: yoksa yokuştaki durakta (Cinnah) otobüs kutuya değiyordu.
        /// </summary>
        private static void DurakKutusu(MeshFilter mf)
        {
            var mesh = mf.sharedMesh;
            var tum = mesh.bounds;
            var alt = new Bounds();
            bool ilk = true;
            foreach (var v in mesh.vertices)
                if (v.y <= tum.min.y + 2.2f)
                {
                    if (ilk) { alt = new Bounds(v, Vector3.zero); ilk = false; }
                    else alt.Encapsulate(v);
                }
            if (ilk)
                alt = tum;
            var box = mf.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(alt.center.x, tum.center.y, alt.center.z);
            // yanlardan 15 cm pay: Cinnah'ta yokuştaki durak cebinde duran körüklünün arka köşesi kutuya değiyordu
            box.size = new Vector3(Mathf.Max(0.2f, alt.size.x - 0.3f), tum.size.y, Mathf.Max(0.2f, alt.size.z - 0.3f));
            Debug.Log($"[Carpisma] durak kutusu {mf.name}: tüm {tum.size}, saçaksız {box.size}");
        }

        /// <summary>Kökün altındaki grup objelerine çarpışma ekler; zaten çarpışması olanlara dokunmaz.</summary>
        public static int Ekle(Transform kok)
        {
            int adet = 0;
            foreach (Transform grup in kok)
            {
                bool kutu = System.Array.IndexOf(KutuGruplari, grup.name) >= 0;
                bool sekil = System.Array.IndexOf(SekilGruplari, grup.name) >= 0;
                if (!kutu && !sekil)
                    continue;
                foreach (var mf in grup.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh == null || mf.GetComponent<Collider>() != null || mf.GetComponent<LODGroup>() != null)
                        continue;
                    // LOD'lu modelde yalnızca en ayrıntılı (LOD0) ya da LOD'suz parça
                    if (mf.name.Contains("_LOD") && !mf.name.EndsWith("_LOD0"))
                        continue;
                    if (grup.name == "Duraklar")
                        DurakKutusu(mf);
                    else if (kutu)
                    {
                        var b = mf.sharedMesh.bounds;
                        var box = mf.gameObject.AddComponent<BoxCollider>();
                        box.center = b.center;
                        box.size = b.size;
                    }
                    else
                        mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                    adet++;
                }
            }
            return adet;
        }
    }
}
