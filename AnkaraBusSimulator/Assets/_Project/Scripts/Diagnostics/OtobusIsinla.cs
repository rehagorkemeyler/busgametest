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
            var arka = koruklu != null ? koruklu.ArkaGovde : null;
            Vector3 arkaYerel = arka != null ? bus.transform.InverseTransformPoint(arka.position) : Vector3.zero;

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = konum;
            body.rotation = donus;
            bus.transform.SetPositionAndRotation(konum, donus);
            if (arka != null)
            {
                arka.linearVelocity = Vector3.zero;
                arka.angularVelocity = Vector3.zero;
                arka.position = konum + donus * new Vector3(0f, arkaYerel.y, arkaYerel.z);
                arka.rotation = donus;
                arka.transform.SetPositionAndRotation(arka.position, arka.rotation);
            }
            Physics.SyncTransforms();
        }
    }
}
