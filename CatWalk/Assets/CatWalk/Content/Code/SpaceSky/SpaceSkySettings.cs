using System;
using UnityEngine;

namespace CatWalk.Rendering
{
    /// <summary>Visual input only. A future world-state presenter can supply these values.</summary>
    [Serializable]
    public struct SpaceSkyState
    {
        [Range(0, 1)] public float StarDensity, Galaxies, Nebula, Activity, Warmth;

        public SpaceSkyState(float stars, float galaxies, float nebula, float activity, float warmth)
        { StarDensity = stars; Galaxies = galaxies; Nebula = nebula; Activity = activity; Warmth = warmth; }

        public SpaceSkyState Clamped() => new SpaceSkyState(Mathf.Clamp01(StarDensity), Mathf.Clamp01(Galaxies),
            Mathf.Clamp01(Nebula), Mathf.Clamp01(Activity), Mathf.Clamp01(Warmth));

        public static SpaceSkyState Lerp(SpaceSkyState a, SpaceSkyState b, float t) => new SpaceSkyState(
            Mathf.Lerp(a.StarDensity, b.StarDensity, t), Mathf.Lerp(a.Galaxies, b.Galaxies, t),
            Mathf.Lerp(a.Nebula, b.Nebula, t), Mathf.Lerp(a.Activity, b.Activity, t), Mathf.Lerp(a.Warmth, b.Warmth, t));

        public static SpaceSkyState Quiet => new SpaceSkyState(.16f, 0, 0, .4f, .15f);
        public static SpaceSkyState Starfield => new SpaceSkyState(.95f, .15f, .12f, .6f, .25f);
        public static SpaceSkyState Galaxy => new SpaceSkyState(.55f, 1, .3f, .65f, .4f);
        public static SpaceSkyState Cloud => new SpaceSkyState(.4f, .65f, 1, .8f, .55f);

        public static SpaceSkyState Demo(float phase)
        {
            float p = Mathf.Repeat(phase, 1) * 4;
            float t = Mathf.SmoothStep(0, 1, p - Mathf.Floor(p));
            if (p < 1) return Lerp(Galaxy, Cloud, t);
            if (p < 2) return Lerp(Cloud, Quiet, t);
            if (p < 3) return Lerp(Quiet, Starfield, t);
            return Lerp(Starfield, Galaxy, t);
        }
    }

    [Serializable]
    public sealed class SkyGalaxy
    {
        [Tooltip("Yaw and elevation in degrees. The sky stays at infinity during flight and origin shifts.")]
        public Vector2 Direction;
        [Range(4, 55)] public float Radius = 24;
        [Range(.15f, 1)] public float Flattening = .55f;
        [Range(-180, 180)] public float Roll = -28;
        [Range(2, 5)] public int Arms = 2;
        [Range(0, 1)] public float Visibility = 1;
        public Color Color = new Color(.72f, .79f, .68f);
    }

    [CreateAssetMenu(menuName = "CatWalk/Rendering/Procedural Space Sky", fileName = "SpaceSky")]
    public sealed class SpaceSkySettings : ScriptableObject
    {
        [Header("Sphere / screen pixels / no texture inputs")]
        [Tooltip("Approximate vertical resolution of the sky. Screen-aligned square pixels, independent of sky direction.")]
        [Range(96, 1024)] public int PixelResolution = 270;
        [Range(4, 32)] public int ColorSteps = 14;
        [Range(0, 9999)] public int Seed = 108;
        [Range(0, 3)] public float Exposure = 1;
        public Color VoidColor = new Color(.004f, .007f, .016f);
        public Color NebulaCool = new Color(.075f, .28f, .32f);
        public Color NebulaWarm = new Color(.28f, .085f, .19f);
        [Header("Sun (scene directional light)")]
        [Tooltip("Angular radius in degrees. Direction, color and intensity follow the scene's main Directional Light.")]
        [Range(.2f, 15)] public float SunRadius = 5;
        [Range(0, 2)] public float SunCorona = .9f;
        [Header("Up to three independent spiral galaxies")]
        public SkyGalaxy[] Galaxies = {
            new SkyGalaxy { Direction = new Vector2(-18, 14) },
            new SkyGalaxy { Direction = new Vector2(126, -12), Radius = 17, Flattening = .36f, Roll = 32, Arms = 3, Color = new Color(.55f, .72f, .85f) },
            new SkyGalaxy { Direction = new Vector2(-132, 38), Radius = 21, Flattening = .72f, Roll = 64, Color = new Color(.88f, .61f, .43f) }
        };
    }
}
