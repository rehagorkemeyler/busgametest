using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AnkaraBus.Vehicle
{
    /// <summary>
    /// Yüksek görüntü kalitesinde otobüsün görselini tam kalite modelle değiştirir.
    /// İki modelde parça adları ve pivotlar aynı olduğu için yalnızca her parçanın mesh'i, materyalleri
    /// ve gölge ayarı değişir; kapılar, tekerlekler ve direksiyon aynı objelerde kalır, sürüş durumu bozulmaz.
    /// Tam kalite model Resources'tan yalnızca gerektiğinde yüklenir; düşük ayarda belleğe girmez.
    /// </summary>
    public class BusKaliteModeli : MonoBehaviour
    {
        [Tooltip("Mobil modelin kökü (prefabdaki 'Model').")]
        [SerializeField] private Transform model;
        [Tooltip("Resources altındaki tam kalite görsel prefabın adı.")]
        [SerializeField] private string tamKaliteKaynak;
        [SerializeField] private KaliteSeviyesi tamKaliteSeviyesi = KaliteSeviyesi.Yuksek;

        private struct Parca
        {
            public MeshFilter filter;
            public MeshRenderer renderer;
            public Mesh mesh;
            public Material[] materials;
            public ShadowCastingMode shadows;
            public bool enabled;
        }

        private readonly List<Parca> mobil = new List<Parca>();
        private bool tamKalite;

        public bool TamKaliteAktif => tamKalite;

        private void OnEnable()
        {
            GrafikAyarlari.Degisti += Uygula;
            Uygula(GrafikAyarlari.Mevcut);
        }

        private void OnDisable() => GrafikAyarlari.Degisti -= Uygula;

        private void Uygula(KaliteSeviyesi seviye)
        {
            bool istenen = seviye >= tamKaliteSeviyesi;
            if (istenen == tamKalite)
                return;
            if (istenen)
                TamKaliteyeGec();
            else
                MobileDon();
        }

        private void TamKaliteyeGec()
        {
            var kaynak = Resources.Load<GameObject>(tamKaliteKaynak);
            if (kaynak == null || model == null)
            {
                Debug.LogWarning($"[KaliteModeli] Tam kalite model bulunamadı: Resources/{tamKaliteKaynak}");
                return;
            }

            var parcalar = new Dictionary<string, MeshRenderer>();
            foreach (var r in kaynak.GetComponentsInChildren<MeshRenderer>(true))
                parcalar[r.name] = r;

            mobil.Clear();
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null)
                    continue;
                mobil.Add(new Parca
                {
                    filter = filter,
                    renderer = renderer,
                    mesh = filter.sharedMesh,
                    materials = renderer.sharedMaterials,
                    shadows = renderer.shadowCastingMode,
                    enabled = renderer.enabled,
                });

                ClearPropertyBlocks(renderer);
                if (parcalar.TryGetValue(filter.name, out var tam))
                {
                    filter.sharedMesh = tam.GetComponent<MeshFilter>().sharedMesh;
                    renderer.sharedMaterials = tam.sharedMaterials;
                    renderer.shadowCastingMode = tam.shadowCastingMode;
                }
                else
                {
                    // Tam kalite modelde karşılığı olmayan parça (ör. Golge_Govde gölge kabuğu)
                    renderer.enabled = false;
                }
            }

            tamKalite = true;
            RefreshLivery();
        }

        private void MobileDon()
        {
            foreach (var p in mobil)
            {
                if (p.filter == null)
                    continue;
                ClearPropertyBlocks(p.renderer);
                p.filter.sharedMesh = p.mesh;
                p.renderer.sharedMaterials = p.materials;
                p.renderer.shadowCastingMode = p.shadows;
                p.renderer.enabled = p.enabled;
            }
            mobil.Clear();
            tamKalite = false;
            RefreshLivery();
            Resources.UnloadUnusedAssets();
        }

        private static void ClearPropertyBlocks(Renderer renderer)
        {
            for (int i = 0; i < renderer.sharedMaterials.Length; i++)
                renderer.SetPropertyBlock(null, i);
        }

        /// <summary>Kaplama materyal sırasına göre uygulandığı için değişimden sonra yeniden uygulanır.</summary>
        private void RefreshLivery()
        {
            var livery = GetComponent<BusLivery>();
            if (livery != null)
                livery.Select(livery.Selected);
        }
    }
}
