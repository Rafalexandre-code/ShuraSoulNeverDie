using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>
    /// The arena's light: the stone lanterns are its only fire. A low-resolution light map over
    /// the room (smoothed by the GPU) darkens it by the hour, warms the floor around every flame
    /// (each one flickering on its own), and keeps a small halo around the duelists. Every lantern
    /// knocked over pulls its light out of the room — darkest on its own side — and with all of
    /// them broken the courtyard is truly dark.
    /// </summary>
    public sealed partial class GameUI
    {
        const int LightTexelsPerUnit = 4;
        const float LanternRadius = 8f;
        const float BrokenGloomRadius = 9.5f;
        const float PlayerLightRadius = 3.2f;
        const float MaxDark = 0.93f;

        Texture2D lightTex;
        Color32[] lightPx;
        readonly List<Vector2> lightLanterns = new List<Vector2>();
        readonly List<Vector2> lightBroken = new List<Vector2>();
        readonly List<Vector2> lightPlayers = new List<Vector2>();
        int lightFrame = -1;

        static float Ambient(TimeOfDay t)
        {
            switch (t)
            {
                case TimeOfDay.Morning: return 0.08f;
                case TimeOfDay.Afternoon: return 0.06f;
                case TimeOfDay.Dusk: return 0.22f;
                case TimeOfDay.Night: return 0.4f;
                default: return 0.16f;
            }
        }

        /// <summary>Gathers the lights of this frame: lanterns (lit and broken) and the duelists.</summary>
        void CollectLights(World w, IList<int> broken)
        {
            lightLanterns.Clear();
            lightBroken.Clear();
            if (w.Decor == null) return;
            for (int i = 0; i < w.Decor.Lanterns.Count; i++)
            {
                Vector2 flame = w.Decor.Lanterns[i] + Vector2.up * 1.2f;
                if (broken != null && broken.Contains(i)) lightBroken.Add(flame);
                else lightLanterns.Add(flame);
            }
        }

        void DrawLighting(World w, float hour, IList<int> broken)
        {
            Camera cam = game.CameraRig.Cam;
            if (cam == null || w == null || w.Decor == null) return;
            CollectLights(w, broken);
            int total = lightLanterns.Count + lightBroken.Count;
            float brokenFrac = total > 0 ? lightBroken.Count / (float)total : 0f;
            float ambient = DayClock.Darkness(hour) + 0.62f * Mathf.Pow(brokenFrac, 1.1f);
            // The fireflies come out with the dark.
            Fireflies.Night = Mathf.Clamp01((ambient - 0.12f) / 0.3f);

            Rect b = w.Bounds;
            b.xMin -= 19f;
            b.yMin -= 19f;
            b.xMax += 19f;
            b.yMax += 19f;
            int tw = Mathf.Clamp(Mathf.CeilToInt(b.width * LightTexelsPerUnit), 8, 256);
            int th = Mathf.Clamp(Mathf.CeilToInt(b.height * LightTexelsPerUnit), 8, 256);
            if (lightTex == null || lightTex.width != tw || lightTex.height != th)
            {
                if (lightTex != null) Destroy(lightTex);
                lightTex = new Texture2D(tw, th, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "LightMap",
                };
                lightPx = new Color32[tw * th];
                lightFrame = -1;
            }

            // Once per rendered frame (OnGUI runs several times a frame).
            if (Event.current.type == EventType.Repaint && lightFrame != Time.frameCount)
            {
                lightFrame = Time.frameCount;
                BakeLight(b, tw, th, ambient);
            }

            PlaceLightMap(b, tw, th);
            // With lanterns broken, the edges of sight close in.
            if (vignette != null && brokenFrac > 0f) DrawTex(FullScreen, vignette, new Color(0f, 0f, 0.02f, 0.3f * brokenFrac));
        }

        // The light map lives in the world (above the duelists, under the timing rings and the
        // sparks), so the image pass blooms the fire and grades the dark with everything else.
        SpriteRenderer lightView;
        Sprite lightSprite;
        int lightDrawnFrame;

        void PlaceLightMap(Rect b, int tw, int th)
        {
            if (lightView == null)
            {
                var go = new GameObject("LightMap");
                lightView = go.AddComponent<SpriteRenderer>();
                lightView.sortingOrder = Sorting.Overlay - 100;
            }
            if (lightSprite == null || lightSprite.texture != lightTex)
            {
                lightSprite = Sprite.Create(lightTex, new Rect(0, 0, tw, th), Vector2.zero, 1f, 0, SpriteMeshType.FullRect);
                lightView.sprite = lightSprite;
            }
            lightView.transform.position = new Vector3(b.xMin, b.yMin, 0f);
            lightView.transform.localScale = new Vector3(b.width / tw, b.height / th, 1f);
            lightView.enabled = true;
            lightDrawnFrame = Time.frameCount;
        }

        void HideLightMapWhenUnused()
        {
            if (lightView != null && lightView.enabled && Time.frameCount - lightDrawnFrame > 2) lightView.enabled = false;
        }

        void BakeLight(Rect b, int tw, int th, float ambient)
        {
            float t = Time.time;
            int n = lightLanterns.Count;
            // Each flame breathes on its own: two slow waves and a quick shiver.
            var radius = new float[n];
            for (int i = 0; i < n; i++)
            {
                float k = i * 1.7f;
                radius[i] = LanternRadius * (1f + 0.05f * Mathf.Sin(t * 2.3f + k) + 0.03f * Mathf.Sin(t * 5.7f + k * 2.1f) + 0.02f * Mathf.Sin(t * 13f + k * 3.3f));
            }
            var dark = new Color(0.012f, 0.016f, 0.055f);
            var warm = new Color(1f, 0.6f, 0.24f);
            float sx = b.width / tw, sy = b.height / th;
            for (int y = 0; y < th; y++)
            {
                float wy = b.yMin + (y + 0.5f) * sy;
                for (int x = 0; x < tw; x++)
                {
                    float wx = b.xMin + (x + 0.5f) * sx;
                    float d = ambient, light = 0f;
                    for (int i = 0; i < n; i++)
                    {
                        float dx = wx - lightLanterns[i].x, dy = wy - lightLanterns[i].y;
                        float r = radius[i];
                        float dd = dx * dx + dy * dy;
                        if (dd >= r * r) continue;
                        float a = 1f - Mathf.Sqrt(dd) / r;
                        light += 0.95f * a * a * (3f - 2f * a);
                    }
                    for (int i = 0; i < lightBroken.Count; i++)
                    {
                        float dx = wx - lightBroken[i].x, dy = wy - lightBroken[i].y;
                        float dd = dx * dx + dy * dy;
                        if (dd >= BrokenGloomRadius * BrokenGloomRadius) continue;
                        float a = 1f - Mathf.Sqrt(dd) / BrokenGloomRadius;
                        d += 0.24f * a * a;
                    }
                    for (int i = 0; i < Fireflies.Lights.Count; i++)
                    {
                        Vector3 f = Fireflies.Lights[i];
                        float dx = wx - f.x, dy = wy - f.y;
                        float dd = dx * dx + dy * dy;
                        if (dd >= 2.6f) continue;
                        float a = 1f - Mathf.Sqrt(dd) / 1.6f;
                        light += 0.32f * f.z * a * a;
                    }
                    for (int i = 0; i < lightPlayers.Count; i++)
                    {
                        float dx = wx - lightPlayers[i].x, dy = wy - lightPlayers[i].y;
                        float dd = dx * dx + dy * dy;
                        if (dd >= PlayerLightRadius * PlayerLightRadius) continue;
                        float a = 1f - Mathf.Sqrt(dd) / PlayerLightRadius;
                        light += 0.5f * a * a;
                    }
                    float aDark = Mathf.Clamp01(d - light) * MaxDark;
                    float aWarm = 0.11f * Mathf.Clamp01(light) * (1f - aDark);
                    float alpha = aDark + aWarm;
                    Color c = alpha > 1e-4f ? (dark * aDark + warm * aWarm) / alpha : dark;
                    lightPx[y * tw + x] = new Color32((byte)(c.r * 255f), (byte)(c.g * 255f), (byte)(c.b * 255f), (byte)(Mathf.Clamp01(alpha) * 255f));
                }
            }
            lightTex.SetPixels32(lightPx);
            lightTex.Apply(false, false);
        }

        /// <summary>The host's room: its lanterns, the ones its own world broke, the party's halos.</summary>
        void DrawWorldLighting(World w)
        {
            lightPlayers.Clear();
            foreach (PlayerController p in w.Players)
            {
                if (p.IsAlive) lightPlayers.Add(p.RenderPosition);
            }
            DrawLighting(w, w.Hour, w.BrokenLanterns);
        }

        /// <summary>A guest: the host's room as the snapshot tells it.</summary>
        void DrawRemoteLighting(float hour)
        {
            World w = game.World;
            lightPlayers.Clear();
            if (Remote.HasOwn) lightPlayers.Add(Remote.OwnPosition);
            DrawLighting(w, hour, w.RemoteBrokenLanterns);
            DrawSkyMoments(hour);
        }

        // ── The sky's small moments ─────────────────────────────────────────────────────
        string ambienceText;
        float ambienceTime = -99f;
        float nextStar = 20f;
        float starTime = -99f;
        Vector2 starFrom, starDir;

        /// <summary>A line about the day, quietly over the fight for a few seconds.</summary>
        public void ShowAmbience(string text)
        {
            ambienceText = text;
            ambienceTime = Time.unscaledTime;
        }

        void DrawAmbience()
        {
            float age = Time.unscaledTime - ambienceTime;
            if (string.IsNullOrEmpty(ambienceText) || age > 7f) return;
            float a = Mathf.Clamp01(age / 0.8f) * Mathf.Clamp01((7f - age) / 1.2f);
            Label(new Rect(VW * 0.5f - 520f, 196f, 1040f, 40f), ambienceText, taglineStyle, UiKit.PaperText.WithAlpha(0.9f * a));
        }

        /// <summary>Dawn mist over the ground; at night, now and then, a shooting star.</summary>
        void DrawSkyMoments(float hour)
        {
            Rect full = FullScreen;
            // The mist of dawn: thickest at six.
            float mist = Mathf.Clamp01(1f - Mathf.Abs(hour - 6f) / 1.2f);
            if (mist > 0f) DrawRect(full, new Color(0.86f, 0.9f, 0.98f, 0.1f * mist));
            bool night = hour >= 20f || hour < 4.5f;
            float now = Time.unscaledTime;
            if (night && now > nextStar)
            {
                nextStar = now + Random.Range(25f, 60f);
                starTime = now;
                starFrom = new Vector2(Random.Range(VW * 0.15f, VW * 0.85f), Random.Range(20f, VH * 0.3f));
                starDir = new Vector2(Random.value < 0.5f ? -1f : 1f, 0.35f).normalized;
            }
            float t = (now - starTime) / 0.9f;
            if (t >= 0f && t < 1f)
            {
                Vector2 head = starFrom + starDir * (600f * t);
                for (int i = 0; i < 18; i++)
                {
                    Vector2 q = head - starDir * (i * 9f);
                    float a = (1f - i / 18f) * (1f - t);
                    float s = 6f - i * 0.25f;
                    kit.Dab(new Rect(q.x - s * 0.5f, q.y - s * 0.5f, s, s), new Color(1f, 0.97f, 0.85f, a));
                }
            }
        }
    }
}
