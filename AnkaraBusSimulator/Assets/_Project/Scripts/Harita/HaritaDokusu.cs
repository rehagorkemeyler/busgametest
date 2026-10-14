using UnityEngine;

namespace AnkaraBus.Harita
{
    /// <summary>
    /// Önceden çizilmiş mini harita dokusu (Hat 3: tools/harita/hat3/yerlesim.py → Maps/Hat3/Hat3_Harita.png): yollar,
    /// kaldırımlar, binalar, parklar. MiniHarita bu bileşen varsa binaları/şeritleri kendisi çizmez, bu dokunun üstüne
    /// güzergâhı ve durakları ekler. Doku okunabilir (Read/Write) içe aktarılır; satır 0 = en küçük z.
    /// </summary>
    public class HaritaDokusu : MonoBehaviour
    {
        public Texture2D doku;
        [Tooltip("Dokunun sol alt köşesinin dünya (x, z) konumu.")]
        public Vector2 min;
        public float pikselMetre = 0.4f;
    }
}
