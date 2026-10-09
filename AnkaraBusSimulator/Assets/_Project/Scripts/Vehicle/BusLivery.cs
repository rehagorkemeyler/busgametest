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
            [Tooltip("Dokusuz gövde parçalarının rengi (ör. körüklünün ön/arka yüzü); alfa 0 ise değişmez.")]
            public Color color;
        }

        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTex = Shader.PropertyToID("_MainTex");
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorProp = Shader.PropertyToID("_Color");

        [Tooltip("Adında bu metin geçen materyallerin dokusu 'body' ile değişir.")]
        [SerializeField] private string bodyMaterialName = "caroserie";
        [Tooltip("Adında bu metin geçen materyallerin dokusu 'roofModule' ile değişir.")]
        [SerializeField] private string roofMaterialName = "cngtank";
        [Tooltip("Adında bu metin geçen dokusuz materyallerin rengi kaplamanın 'color' değeriyle değişir (boşsa yok).")]
        [SerializeField] private string colorMaterialName = "";
        [SerializeField] private Livery[] liveries;
        [SerializeField] private int selected;
        [Tooltip("Oyunda ana menüde seçilen kaplamayı uygula (Gameplay.OyunSecimi.Kaplama).")]
        [SerializeField] private bool useMenuChoice = true;

        public int Count => liveries?.Length ?? 0;
        public int Selected => selected;
        public string NameAt(int index) => index >= 0 && index < Count ? liveries[index].name : null;
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

        private void Start()
        {
            if (!Application.isPlaying || !useMenuChoice)
                return;
            int choice = AnkaraBus.Gameplay.OyunSecimi.Kaplama;
            if (choice == -1)
                SelectRandom();
            else if (choice >= 0)
                Select(choice);
        }

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
                    bool renk = livery.color.a > 0f && !string.IsNullOrEmpty(colorMaterialName)
                                && material.name.Contains(colorMaterialName);
                    if (texture == null && !renk)
                        continue;
                    renderer.GetPropertyBlock(block, i);
                    if (texture != null)
                        block.SetTexture(material.HasProperty(BaseMap) ? BaseMap : MainTex, texture);
                    if (renk)
                        block.SetColor(material.HasProperty(BaseColor) ? BaseColor : ColorProp, livery.color);
                    renderer.SetPropertyBlock(block, i);
                }
            }
        }
    }
}
