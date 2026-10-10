using AnkaraBus.Vehicle;
using UnityEngine;

namespace AnkaraBus.Diagnostics
{
    /// <summary>
    /// Testlerde otobüsü ışınlar. Körüklüde arka gövde ayrı Rigidbody'dir: yalnızca ön gövde taşınırsa mafsal arka gövdeyi
    /// metrelerce öteden çeker ve otobüs fırlar. Arka gövde ön gövdeye göre düz (mafsal 0°) duracak şekilde birlikte taşınır.
    /// </summary>
    public static class OtobusIsinla
    {
        public static void Tasi(BusVehicle bus, Vector3 konum, Quaternion donus)
        {
            var body = bus.GetComponent<Rigidbody>();
            var koruklu = bus.GetComponent<KorukluOtobus>();

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = konum;
            body.rotation = donus;
            bus.transform.SetPositionAndRotation(konum, donus);
            if (koruklu != null)
                koruklu.ArkayiHizala();
            Physics.SyncTransforms();
        }
    }
}
