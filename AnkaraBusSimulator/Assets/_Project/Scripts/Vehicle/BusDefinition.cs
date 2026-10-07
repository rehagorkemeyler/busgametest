using UnityEngine;

namespace AnkaraBus.Vehicle
{
    /// <summary>
    /// Oyuna eklenebilir bir otobüs modeli. Yeni otobüs eklemek için:
    /// Create > Ankara Bus > Bus Definition ile bir tane oluşturup prefabını bağlamak yeterli.
    /// </summary>
    [CreateAssetMenu(menuName = "Ankara Bus/Bus Definition", fileName = "BusDefinition")]
    public class BusDefinition : ScriptableObject
    {
        public string displayName = "BMC Procity 12LF";
        public string manufacturer = "BMC";
        [Tooltip("Rigidbody + araç kontrolcüsü + BusDoorController içeren hazır prefab.")]
        public GameObject prefab;
        public Sprite preview;
        public Material[] liveries;
        [Min(1)] public int passengerCapacity = 90;
        public BusPhysicsSpec physics = new BusPhysicsSpec();
    }
}
