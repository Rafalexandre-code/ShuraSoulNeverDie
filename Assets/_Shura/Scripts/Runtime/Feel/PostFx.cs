using UnityEngine;

namespace Shura
{
    /// <summary>A mood for the image pass: how it glows, how it is graded.</summary>
    public struct PostMood
    {
        public float threshold, knee, bloom;
        public Color bloomTint;
        public float exposure, contrast, saturation, vibrance;
        public Color shadowTint, highlightTint;
        public float vignette, aberration, grain;

        public static PostMood Lerp(PostMood a, PostMood b, float t)
        {
            return new PostMood
            {
                threshold = Mathf.Lerp(a.threshold, b.threshold, t), knee = Mathf.Lerp(a.knee, b.knee, t), bloom = Mathf.Lerp(a.bloom, b.bloom, t),
                bloomTint = Color.Lerp(a.bloomTint, b.bloomTint, t), exposure = Mathf.Lerp(a.exposure, b.exposure, t),
                contrast = Mathf.Lerp(a.contrast, b.contrast, t), saturation = Mathf.Lerp(a.saturation, b.saturation, t),
                vibrance = Mathf.Lerp(a.vibrance, b.vibrance, t), shadowTint = Color.Lerp(a.shadowTint, b.shadowTint, t),
                highlightTint = Color.Lerp(a.highlightTint, b.highlightTint, t), vignette = Mathf.Lerp(a.vignette, b.vignette, t),
                aberration = Mathf.Lerp(a.aberration, b.aberration, t), grain = Mathf.Lerp(a.grain, b.grain, t),
            };
        }

        /// <summary>The palette at any hour, flowing from one period's palette into the next.</summary>
        public static PostMood ForHour(float hour)
        {
            float[] h = { 0f, 4.6f, 6f, 7.5f, 12f, 13.5f, 16.5f, 18f, 19.3f, 20.5f, 24f };
            TimeOfDay[] p = { TimeOfDay.Night, TimeOfDay.Night, TimeOfDay.Dawn, TimeOfDay.Morning, TimeOfDay.Morning, TimeOfDay.Afternoon,
                              TimeOfDay.Afternoon, TimeOfDay.Dusk, TimeOfDay.Dusk, TimeOfDay.Night, TimeOfDay.Night };
            hour = Mathf.Repeat(hour, 24f);
            for (int i = 0; i < h.Length - 1; i++)
            {
                if (hour < h[i + 1]) return Lerp(For(p[i]), For(p[i + 1]), (hour - h[i]) / (h[i + 1] - h[i]));
            }
            return For(TimeOfDay.Night);
        }

        /// <summary>Each hour has its own palette: luminous morning, golden afternoon, rose dusk, blue night, pearl dawn.</summary>
        public static PostMood For(TimeOfDay t)
        {
            var m = new PostMood
            {
                threshold = 0.82f, knee = 0.22f, bloom = 0.65f, bloomTint = new Color(1f, 0.93f, 0.85f),
                exposure = 1.04f, contrast = 1.08f, saturation = 1.22f, vibrance = 0.3f,
                shadowTint = new Color(0.25f, 0.5f, 0.75f, 0.12f), highlightTint = new Color(1f, 0.82f, 0.55f, 0.1f),
                vignette = 0.28f, aberration = 0.012f, grain = 0.018f,
            };
            switch (t)
            {
                case TimeOfDay.Afternoon:
                    m.highlightTint = new Color(1f, 0.75f, 0.4f, 0.16f);
                    m.bloomTint = new Color(1f, 0.85f, 0.6f);
                    break;
                case TimeOfDay.Dusk:
                    m.shadowTint = new Color(0.45f, 0.3f, 0.7f, 0.18f);
                    m.highlightTint = new Color(1f, 0.55f, 0.45f, 0.2f);
                    m.bloomTint = new Color(1f, 0.7f, 0.6f);
                    m.bloom = 0.8f;
                    break;
                case TimeOfDay.Night:
                    m.shadowTint = new Color(0.15f, 0.35f, 0.85f, 0.22f);
                    m.highlightTint = new Color(1f, 0.75f, 0.45f, 0.12f);
                    m.bloomTint = new Color(1f, 0.85f, 0.7f);
                    m.threshold = 0.7f;
                    m.bloom = 1.0f;
                    m.saturation = 1.15f;
                    m.vignette = 0.38f;
                    break;
                case TimeOfDay.Dawn:
                    m.shadowTint = new Color(0.35f, 0.45f, 0.8f, 0.14f);
                    m.highlightTint = new Color(1f, 0.7f, 0.8f, 0.14f);
                    m.bloomTint = new Color(1f, 0.85f, 0.95f);
                    break;
            }
            return m;
        }
    }

    /// <summary>
    /// The camera's image pass (Built-in RP): a bloom chain on what shines — fire, sparks, steel
    /// in the light — and a colourful grade by the hour. The HUD (IMGUI) is drawn after it.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class PostFx : MonoBehaviour
    {
        const int MaxLevels = 6;

        Material mat;
        readonly RenderTexture[] levels = new RenderTexture[MaxLevels];
        PostMood current;
        PostMood target;
        bool hasMood;

        public bool Enabled { get; set; } = true;
        /// <summary>Extra bloom on a moment (finishers, the soul): eases back by itself.</summary>
        public float Flash { get; set; }

        static readonly int ThresholdId = Shader.PropertyToID("_Threshold");
        static readonly int KneeId = Shader.PropertyToID("_Knee");
        static readonly int BloomIntensityId = Shader.PropertyToID("_BloomIntensity");
        static readonly int BloomTintId = Shader.PropertyToID("_BloomTint");
        static readonly int ExposureId = Shader.PropertyToID("_Exposure");
        static readonly int ContrastId = Shader.PropertyToID("_Contrast");
        static readonly int SaturationId = Shader.PropertyToID("_Saturation");
        static readonly int VibranceId = Shader.PropertyToID("_Vibrance");
        static readonly int ShadowTintId = Shader.PropertyToID("_ShadowTint");
        static readonly int HighlightTintId = Shader.PropertyToID("_HighlightTint");
        static readonly int VignetteId = Shader.PropertyToID("_Vignette");
        static readonly int AberrationId = Shader.PropertyToID("_Aberration");
        static readonly int GrainId = Shader.PropertyToID("_Grain");
        static readonly int GrainSeedId = Shader.PropertyToID("_GrainSeed");
        static readonly int BloomId = Shader.PropertyToID("_Bloom");

        void Awake()
        {
            Shader shader = Resources.Load<Shader>("Shaders/ShuraPost");
            if (shader != null && shader.isSupported) mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            else Debug.LogWarning("[Shura] Pós-processamento indisponível neste computador.");
        }

        void OnDestroy()
        {
            if (mat != null) Destroy(mat);
        }

        /// <summary>The mood to ease toward (the room's hour).</summary>
        public void SetMood(PostMood mood)
        {
            target = mood;
            if (!hasMood)
            {
                current = mood;
                hasMood = true;
            }
        }

        void Update()
        {
            if (!hasMood) return;
            current = PostMood.Lerp(current, target, 1f - Mathf.Exp(-2.5f * Time.unscaledDeltaTime));
            Flash = Mathf.MoveTowards(Flash, 0f, Time.unscaledDeltaTime * 1.6f);
        }

        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (!Enabled || mat == null || !hasMood)
            {
                Graphics.Blit(src, dst);
                return;
            }
            PostMood m = current;
            mat.SetFloat(ThresholdId, m.threshold);
            mat.SetFloat(KneeId, m.knee);
            mat.SetFloat(BloomIntensityId, m.bloom + Flash);
            mat.SetColor(BloomTintId, m.bloomTint);
            mat.SetFloat(ExposureId, m.exposure);
            mat.SetFloat(ContrastId, m.contrast);
            mat.SetFloat(SaturationId, m.saturation);
            mat.SetFloat(VibranceId, m.vibrance);
            mat.SetColor(ShadowTintId, m.shadowTint);
            mat.SetColor(HighlightTintId, m.highlightTint);
            mat.SetFloat(VignetteId, m.vignette);
            mat.SetFloat(AberrationId, m.aberration);
            mat.SetFloat(GrainId, m.grain);
            mat.SetFloat(GrainSeedId, Time.frameCount % 97);

            int w = Mathf.Max(1, src.width / 2), h = Mathf.Max(1, src.height / 2);
            RenderTextureFormat fmt = src.format;
            RenderTexture cur = levels[0] = RenderTexture.GetTemporary(w, h, 0, fmt);
            Graphics.Blit(src, cur, mat, 0);
            int count = 1;
            for (; count < MaxLevels; count++)
            {
                w /= 2;
                h /= 2;
                if (w < 2 || h < 2) break;
                levels[count] = RenderTexture.GetTemporary(w, h, 0, fmt);
                Graphics.Blit(cur, levels[count], mat, 1);
                cur = levels[count];
            }
            for (int i = count - 2; i >= 0; i--)
            {
                Graphics.Blit(cur, levels[i], mat, 2);
                cur = levels[i];
            }
            mat.SetTexture(BloomId, cur);
            Graphics.Blit(src, dst, mat, 3);
            for (int i = 0; i < count; i++)
            {
                RenderTexture.ReleaseTemporary(levels[i]);
                levels[i] = null;
            }
        }
    }
}
