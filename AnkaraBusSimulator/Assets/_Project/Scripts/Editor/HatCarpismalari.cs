using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AnkaraBus.EditorTools
{
    /// <summary>
    /// Haritadaki binalara, simge yapılara ve durak çatılarına çarpışma ekler (otobüs içlerinden geçmesin, kamera
    /// duvarın içine girmesin, çarpınca puan cezası gelsin). Apartmanlar ve Kızılay blokları kutu (ucuz, binalar
    /// kutuya yakın), simge yapılar ve duraklar gerçek şekliyle (MeshCollider) çarpışır. Ağaç ve lambalar eklenmez.
    /// HaritaKurucu haritayı kurarken çağırır; var olan sahneler için menü (ışık bake'i bozulmaz, yeniden bake gerekmez):
    /// Ankara Bus > Hat Sahnelerine Çarpışma Ekle
    /// </summary>
    public static class HatCarpismalari
    {
        private static readonly string[] KutuGruplari = { "Binalar" };
        private static readonly string[] SekilGruplari = { "SimgeYapilar", "Duraklar" };

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
                    if (kutu)
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
