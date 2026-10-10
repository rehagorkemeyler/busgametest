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

        /// <summary>
        /// Otobüsün gövde kutuları (körüklüde arka gövdeninki dahil) şu anki yerinde bir binaya, durağa ya da araca değiyor mu?
        /// Zemin ve kaldırım (kutunun alt 1 m'si) sayılmaz. Değdiği ilk nesnenin adı, yoksa null.
        /// </summary>
        public static string Temas(BusVehicle bus)
        {
            var koruklu = bus.GetComponent<KorukluOtobus>();
            var arka = koruklu != null && koruklu.ArkaGovde != null ? koruklu.ArkaGovde.transform : null;
            foreach (var c in OtobusOlcusu.Olc(bus).Kutular)
            {
                if (c == null)
                    continue;
                var merkez = c.transform.TransformPoint(c.center);
                var yari = Vector3.Scale(c.size * 0.5f, c.transform.lossyScale);
                foreach (var h in Physics.OverlapBox(merkez, yari, c.transform.rotation, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (h is WheelCollider || h.transform.IsChildOf(bus.transform) || (arka != null && h.transform.IsChildOf(arka)))
                        continue;
                    if (bus.transform.InverseTransformPoint(h.ClosestPoint(merkez)).y < 1f || Zemin(h, merkez))
                        continue;
                    return h.name;
                }
            }
            return null;
        }

        /// <summary>Nesne kutunun altında mı (yol, eğimli durak cebi): yukarıdan inen ışın ona kutu merkezinin altında değiyor.</summary>
        private static bool Zemin(Collider h, Vector3 merkez)
        {
            // binalar, simge yapılar ve duraklar zemin değildir (altında kalsa bile: otobüs çatısına konmuş olur)
            for (var t = h.transform; t != null; t = t.parent)
                if (t.name == "Binalar" || t.name == "SimgeYapilar" || t.name == "Duraklar")
                    return false;
            return h.Raycast(new Ray(merkez + Vector3.up * 4f, Vector3.down), out var hit, 8f) && hit.point.y < merkez.y;
        }

        /// <summary>Otobüsü zemine oturtup taşır (yukarıdan aşağı ışın; otobüse ait olmayan ilk yüzey).</summary>
        public static bool ZemineTasi(BusVehicle bus, Vector3 konum, Quaternion donus, float yerdenYukseklik)
        {
            var koruklu = bus.GetComponent<KorukluOtobus>();
            var arka = koruklu != null && koruklu.ArkaGovde != null ? koruklu.ArkaGovde.transform : null;
            float enYakin = float.MaxValue;
            float y = float.NaN;
            foreach (var h in Physics.RaycastAll(konum + Vector3.up * 30f, Vector3.down, 80f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.collider is WheelCollider || h.transform.IsChildOf(bus.transform) || (arka != null && h.transform.IsChildOf(arka)))
                    continue;
                if (h.distance < enYakin)
                {
                    enYakin = h.distance;
                    y = h.point.y;
                }
            }
            if (float.IsNaN(y))
                return false;
            Tasi(bus, new Vector3(konum.x, y + yerdenYukseklik, konum.z), donus);
            return true;
        }
    }
}
