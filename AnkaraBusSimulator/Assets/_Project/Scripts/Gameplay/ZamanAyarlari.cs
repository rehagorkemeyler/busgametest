using UnityEngine;

namespace AnkaraBus.Gameplay
{
    /// <summary>
    /// Akşam ve gece ışık ayarları. Hem bake'te (Editor/HavaZamanKurucu) hem oyunda (HavaVeZaman) aynı değerler
    /// kullanılır: lightmap'teki ışık ile hareketli objelere düşen gerçek zamanlı ışık uyuşsun.
    /// </summary>
    public static class ZamanAyarlari
    {
        public struct Ayar
        {
            public Vector3 gunesAcisi;
            public Color gunesRengi;
            public float gunesSiddeti;
            public float golgeGucu;
            public Color gokRengi;
            public Color zeminRengi;
            public float gokParlaklik;
            public float atmosfer;
            public float gunesBoyu;
            public Color sisRengi;
            public float sisBaslangic, sisBitis;
            /// <summary>Sokak lambası (bake) ve bina pencereleri parlaklığı, 0 kapalı.</summary>
            public float lamba;
            /// <summary>Otobüs farları ve iç ışıkları açık mı?</summary>
            public bool farlar;
        }

        public static Ayar Al(OyunSecimi.Zaman zaman)
        {
            switch (zaman)
            {
                case OyunSecimi.Zaman.Aksam:
                    return new Ayar
                    {
                        // gün batımı: güneş alçakta, turuncu
                        gunesAcisi = new Vector3(7f, 100f, 0f),
                        gunesRengi = new Color(1f, 0.6f, 0.34f),
                        gunesSiddeti = 0.95f,
                        golgeGucu = 0.75f,
                        gokRengi = new Color(0.6f, 0.45f, 0.5f),
                        zeminRengi = new Color(0.32f, 0.26f, 0.24f),
                        gokParlaklik = 0.8f,
                        atmosfer = 1.45f,
                        gunesBoyu = 0.06f,
                        sisRengi = new Color(0.62f, 0.48f, 0.42f),
                        sisBaslangic = 120f,
                        sisBitis = 1100f,
                        lamba = 0.55f,
                        farlar = true,
                    };
                case OyunSecimi.Zaman.Gece:
                    return new Ayar
                    {
                        // ay ışığı: soluk mavi, yüksekte
                        gunesAcisi = new Vector3(55f, 200f, 0f),
                        gunesRengi = new Color(0.55f, 0.65f, 1f),
                        gunesSiddeti = 0.14f,
                        golgeGucu = 0.5f,
                        gokRengi = new Color(0.2f, 0.25f, 0.42f),
                        zeminRengi = new Color(0.04f, 0.04f, 0.05f),
                        gokParlaklik = 0.07f,
                        atmosfer = 0.7f,
                        gunesBoyu = 0.02f,
                        sisRengi = new Color(0.05f, 0.06f, 0.09f),
                        sisBaslangic = 80f,
                        sisBitis = 700f,
                        lamba = 1f,
                        farlar = true,
                    };
                default:
                    return new Ayar { lamba = 0f, farlar = false };
            }
        }

        /// <summary>Procedural gökyüzü malzemesine (Skybox/Procedural) ayarı uygular.</summary>
        public static void GokyuzuAyarla(Material gok, Ayar a, bool yagmur)
        {
            if (gok == null)
                return;
            Color tint = yagmur ? Color.Lerp(a.gokRengi, new Color(0.45f, 0.47f, 0.5f), 0.7f) : a.gokRengi;
            float exposure = a.gokParlaklik * (yagmur ? 0.6f : 1f);
            if (gok.HasProperty("_SkyTint")) gok.SetColor("_SkyTint", tint);
            if (gok.HasProperty("_GroundColor")) gok.SetColor("_GroundColor", a.zeminRengi);
            if (gok.HasProperty("_Exposure")) gok.SetFloat("_Exposure", exposure);
            if (gok.HasProperty("_AtmosphereThickness")) gok.SetFloat("_AtmosphereThickness", a.atmosfer);
            if (gok.HasProperty("_SunSize")) gok.SetFloat("_SunSize", yagmur ? 0f : a.gunesBoyu);
        }

        /// <summary>Sokak lambasının (Props/Lamba_Bulvar) iki başının yerel konumu.</summary>
        public static readonly Vector3[] LambaBaslari = { new Vector3(-1.6f, 8.7f, 0f), new Vector3(1.6f, 8.7f, 0f) };
        public static readonly Color LambaRengi = new Color(1f, 0.72f, 0.42f); // sodyum buharlı
    }
}
