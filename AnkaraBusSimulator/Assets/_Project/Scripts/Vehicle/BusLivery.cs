using System;
using UnityEngine;

namespace AnkaraBus.Vehicle
{
    /// <summary>
    /// Otobüsün kaplamasını (gövde ve tavan modülü dokusu) değiştirir. MaterialPropertyBlock kullanır:
    /// paylaşılan materyale dokunmaz, materyal kopyası da oluşturmaz.
    /// </summary>
    [ExecuteAlways]
    public class BusLivery : MonoBehaviour
    {
        [Serializable]
        public class Livery
        {
            public string name;
            public Texture2D body;
            public Texture2D roofModule;
        }

        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTex = Shader.PropertyToID("_MainTex");

        [Tooltip("Adında bu metin geçen materyallerin dokusu 'body' ile değişir.")]
        [SerializeField] private string bodyMaterialName = "caroserie";
        [Tooltip("Adında bu metin geçen materyallerin dokusu 'roofModule' ile değişir.")]
        [SerializeField] private string roofMaterialName = "cngtank";
        [SerializeField] private Livery[] liveries;
        [SerializeField] private int selected;

        public int Count => liveries?.Length ?? 0;
        public int Selected => selected;
        public string SelectedName => Count > 0 ? liveries[Mathf.Clamp(selected, 0, Count - 1)].name : null;

        public void Select(int index)
        {
            if (Count == 0)
                return;
            selected = ((index % Count) + Count) % Count;
            Apply();
        }

        public void SelectRandom() => Select(UnityEngine.Random.Range(0, Count));

        private void OnEnable() => Apply();

        private void OnValidate() => Apply();

        private void Apply()
        {
            if (Count == 0)
                return;
            var livery = liveries[Mathf.Clamp(selected, 0, Count - 1)];
            var block = new MaterialPropertyBlock();
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    var material = materials[i];
                    if (material == null)
                        continue;
                    Texture2D texture = null;
                    if (livery.body != null && material.name.Contains(bodyMaterialName))
                        texture = livery.body;
                    else if (livery.roofModule != null && material.name.Contains(roofMaterialName))
                        texture = livery.roofModule;
                    if (texture == null)
                        continue;
                    renderer.GetPropertyBlock(block, i);
                    block.SetTexture(material.HasProperty(BaseMap) ? BaseMap : MainTex, texture);
                    renderer.SetPropertyBlock(block, i);
                }
            }
        }
    }
}
