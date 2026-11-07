using UnityEngine;

namespace Shura
{
    /// <summary>
    /// Orthographic camera with smoothed follow + aim look-ahead, trauma-based shake (Perlin,
    /// so it feels heavy instead of jittery) and zoom punch. Both shake and punch scale with
    /// the player's "screen shake" setting.
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        FeelTuning feel;
        GameSettings settings;
        float trauma;
        float punch;
        float zoomOffset;
        float zoomTarget;
        Vector2 focus;
        readonly float seed = 17.3f;

        public Camera Cam { get; private set; }

        public void Init(FeelTuning feelTuning, GameSettings gameSettings, Color background)
        {
            feel = feelTuning;
            settings = gameSettings;
            Cam = gameObject.AddComponent<Camera>();
            Cam.orthographic = true;
            Cam.orthographicSize = feel.cameraOrthoSize;
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = background;
            Cam.nearClipPlane = 0.1f;
            Cam.farClipPlane = 50f;
            gameObject.AddComponent<AudioListener>();
            transform.position = new Vector3(0f, 0f, -10f);
        }

        public void Shake(float amount)
        {
            trauma = Mathf.Min(1f, trauma + amount);
        }

        public void Punch(float amount)
        {
            punch = Mathf.Max(punch, amount);
        }

        /// <summary>Persistent zoom-in (world units of ortho size), e.g. tighter framing at high Flow.</summary>
        public void SetZoomOffset(float amount)
        {
            zoomTarget = amount;
        }

        public void Follow(Vector2 target, Vector2 lookDirection, Rect arena, float dt)
        {
            Vector2 wanted = Vector2.Lerp(arena.center, target + lookDirection * feel.cameraLookAhead, feel.cameraFollowWeight);
            focus = Vector2.Lerp(focus, wanted, CombatMath.Damp(feel.cameraFollowSharpness, dt));
        }

        public void Tick(float dt)
        {
            trauma = Mathf.Max(0f, trauma - feel.shakeDecayPerSecond * dt);
            punch = Mathf.Lerp(punch, 0f, CombatMath.Damp(10f, dt));

            float strength = trauma * trauma * settings.shake;
            float t = Time.unscaledTime * feel.shakeFrequency;
            var offset = new Vector2(Mathf.PerlinNoise(seed, t) * 2f - 1f, Mathf.PerlinNoise(seed + 10f, t) * 2f - 1f)
                         * feel.shakeMaxOffset * strength;
            float angle = (Mathf.PerlinNoise(seed + 20f, t) * 2f - 1f) * feel.shakeMaxAngle * strength;

            transform.position = new Vector3(focus.x + offset.x, focus.y + offset.y, -10f);
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
            zoomOffset = Mathf.Lerp(zoomOffset, zoomTarget, CombatMath.Damp(2f, dt));
            Cam.orthographicSize = feel.cameraOrthoSize - zoomOffset - punch * settings.shake;
        }

        public Vector2 ScreenToWorld(Vector2 screen)
        {
            Vector3 w = Cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 10f));
            return new Vector2(w.x, w.y);
        }
    }
}
