using UnityEngine;
using UnityEngine.Rendering;

namespace CatWalk.Rendering
{
    /// <summary>Owns only a sky material and a visual clock; independent of world generation and gameplay.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class SpaceSky : MonoBehaviour
    {
        public Material Template;
        public SpaceSkySettings Settings;
        public MeshRenderer Dome;
        [Header("Visual input (manual when Demo Cycle is off)")]
        public SpaceSkyState TargetState = SpaceSkyState.Galaxy;
        [Min(.05f), Tooltip("Time to cover approximately 95% of a state transition.")]
        public float TransitionSeconds = 8;
        [Header("Temporary visual demonstration")]
        public bool DemoCycle = true;
        [Min(10)] public float CycleSeconds = 120;
        [Range(0, 4)] public float AnimationSpeed = 1;

        public SpaceSkyState CurrentState { get; private set; }
        public float VisualTime => (float)clock;
        public Material ActiveMaterial => material;
        Material material, previousSky;
        double clock;

        static readonly int Void = Shader.PropertyToID("_VoidColor"), Cool = Shader.PropertyToID("_NebulaCool"), Warm = Shader.PropertyToID("_NebulaWarm"),
            Resolution = Shader.PropertyToID("_PixelResolution"), Steps = Shader.PropertyToID("_ColorSteps"), Seed = Shader.PropertyToID("_Seed"),
            Exposure = Shader.PropertyToID("_Exposure"), Stars = Shader.PropertyToID("_StarDensity"), Galaxy = Shader.PropertyToID("_GalaxyVisibility"),
            Nebula = Shader.PropertyToID("_NebulaDensity"), Activity = Shader.PropertyToID("_Activity"), Warmth = Shader.PropertyToID("_Warmth"),
            Clock = Shader.PropertyToID("_SkyTime");
        static readonly int[] Directions = { Shader.PropertyToID("_GalaxyDirection0"), Shader.PropertyToID("_GalaxyDirection1"), Shader.PropertyToID("_GalaxyDirection2") },
            Shapes = { Shader.PropertyToID("_GalaxyShape0"), Shader.PropertyToID("_GalaxyShape1"), Shader.PropertyToID("_GalaxyShape2") },
            Colors = { Shader.PropertyToID("_GalaxyColor0"), Shader.PropertyToID("_GalaxyColor1"), Shader.PropertyToID("_GalaxyColor2") };

        void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += PrepareCamera;
            if (Dome) Dome.enabled = true;
            if (!Application.IsPlaying(gameObject) || !Template || !Settings || !Dome) return;
            previousSky = RenderSettings.skybox;
            material = new Material(Template) { name = "Procedural space (session)", hideFlags = HideFlags.HideAndDontSave };
            clock = 0;
            CurrentState = TargetState.Clamped();
            ApplyAppearance(material, Settings);
            ApplyState(material, CurrentState, VisualTime);
            Dome.sharedMaterial = material;
            RenderSettings.skybox = null;
        }

        void PrepareCamera(ScriptableRenderContext context, Camera camera)
        {
            if (!Dome) return;
            // The vertex shader centres the sphere per camera. Match its culling bounds without
            // moving a scene Transform or saving editor camera positions into the prefab.
            Dome.bounds = new Bounds(camera.transform.position, Vector3.one * Mathf.Max(10, camera.nearClipPlane * 4));
        }

        void Update() => Advance(Time.deltaTime);

        /// <summary>Called by a future world-state adapter. Changes smoothly, without reseeding.</summary>
        public void SetVisualState(SpaceSkyState state)
        {
            DemoCycle = false;
            TargetState = state.Clamped();
        }

        public void Advance(float deltaTime)
        {
            if (!material || !Settings || !isActiveAndEnabled) return;
            float dt = Mathf.Max(0, deltaTime) * Mathf.Max(0, AnimationSpeed);
            clock += dt;
            var desired = DemoCycle ? SpaceSkyState.Demo((float)(clock / Mathf.Max(10, CycleSeconds) % 1.0)) : TargetState.Clamped();
            CurrentState = SpaceSkyState.Lerp(CurrentState, desired, 1 - Mathf.Exp(-3 * dt / Mathf.Max(.05f, TransitionSeconds)));
            // Tuning changes are visible during Play without mutating the source material.
            ApplyAppearance(material, Settings);
            ApplyState(material, CurrentState, VisualTime);
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= PrepareCamera;
            if (Dome)
            {
                Dome.ResetBounds();
                Dome.enabled = false;
                if (Dome.sharedMaterial == material) Dome.sharedMaterial = Template;
            }
            if (!material) return;
            if (!RenderSettings.skybox) RenderSettings.skybox = previousSky;
            if (Application.isPlaying) Destroy(material); else DestroyImmediate(material);
            material = null;
            previousSky = null;
        }

        public static void ApplyState(Material target, SpaceSkyState state, float time)
        {
            state = state.Clamped();
            target.SetFloat(Stars, state.StarDensity); target.SetFloat(Galaxy, state.Galaxies);
            target.SetFloat(Nebula, state.Nebula); target.SetFloat(Activity, state.Activity);
            target.SetFloat(Warmth, state.Warmth); target.SetFloat(Clock, time);
        }

        public static void ApplyAppearance(Material target, SpaceSkySettings settings)
        {
            target.SetColor(Void, settings.VoidColor); target.SetColor(Cool, settings.NebulaCool); target.SetColor(Warm, settings.NebulaWarm);
            target.SetFloat(Resolution, Mathf.Clamp(settings.PixelResolution, 96, 1024)); target.SetFloat(Steps, Mathf.Clamp(settings.ColorSteps, 4, 32));
            target.SetFloat(Seed, settings.Seed); target.SetFloat(Exposure, Mathf.Clamp(settings.Exposure, 0, 3));
            for (int i = 0; i < 3; i++)
            {
                target.SetVector(Directions[i], Vector4.zero); target.SetVector(Shapes[i], Vector4.zero); target.SetVector(Colors[i], Vector4.zero);
                if (settings.Galaxies == null || i >= settings.Galaxies.Length || settings.Galaxies[i] == null) continue;
                var g = settings.Galaxies[i];
                Vector3 d = Quaternion.Euler(-g.Direction.y, g.Direction.x, 0) * Vector3.forward;
                target.SetVector(Directions[i], new Vector4(d.x, d.y, d.z, Mathf.Tan(Mathf.Clamp(g.Radius, 4, 55) * Mathf.Deg2Rad)));
                target.SetVector(Shapes[i], new Vector4(Mathf.Clamp(g.Flattening, .15f, 1), g.Roll * Mathf.Deg2Rad, Mathf.Clamp(g.Arms, 2, 5), Mathf.Clamp01(g.Visibility)));
                target.SetColor(Colors[i], g.Color);
            }
        }
    }
}
