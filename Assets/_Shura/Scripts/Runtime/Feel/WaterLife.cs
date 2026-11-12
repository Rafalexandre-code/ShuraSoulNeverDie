using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>
    /// What lives on a lake's skin: everyone who steps into the water is mirrored under their
    /// feet (their own frame, upside down, cooler, waving more the farther from the feet and
    /// fading near the shore), and every step, turn and dodge sends rings out over the water.
    /// </summary>
    public sealed class WaterLife : MonoBehaviour
    {
        const float StepRipple = 0.24f;

        sealed class Mirror
        {
            public SpriteRenderer view;
            public MaterialPropertyBlock props;
            public Vector2 lastPos;
            public float rippleTimer;
        }

        sealed class Ripple
        {
            public SpriteRenderer view;
            public float age, life, size;
        }

        World world;
        Material reflectMat;
        readonly Dictionary<Fighter, Mirror> mirrors = new Dictionary<Fighter, Mirror>();
        readonly List<Ripple> ripples = new List<Ripple>();
        readonly List<Fighter> fighters = new List<Fighter>();
        readonly List<Fighter> stale = new List<Fighter>();
        static readonly int FeetId = Shader.PropertyToID("_FeetY");
        static readonly int HeightId = Shader.PropertyToID("_Height");
        static readonly int FadeId = Shader.PropertyToID("_Fade");

        public void Init(World owner)
        {
            world = owner;
            Shader s = Resources.Load<Shader>("Shaders/ShuraReflection");
            if (s != null && s.isSupported) reflectMat = new Material(s) { hideFlags = HideFlags.HideAndDontSave };
        }

        void OnDestroy()
        {
            if (reflectMat != null) Destroy(reflectMat);
        }

        void LateUpdate()
        {
            if (world == null || world.Layout == null) return;
            ArenaLayout layout = world.Layout;
            fighters.Clear();
            foreach (PlayerController p in world.Players) fighters.Add(p);
            foreach (Enemy e in world.Enemies) if (!e.IsGone) fighters.Add(e);

            foreach (Fighter f in fighters)
            {
                if (f == null) continue;
                SpriteRenderer body = f.BodyRenderer;
                if (body == null) continue;
                if (!mirrors.TryGetValue(f, out Mirror m))
                {
                    m = new Mirror { props = new MaterialPropertyBlock(), lastPos = f.RenderPosition };
                    var go = new GameObject("Reflection");
                    go.transform.SetParent(transform, false);
                    m.view = go.AddComponent<SpriteRenderer>();
                    if (reflectMat != null) m.view.sharedMaterial = reflectMat;
                    m.view.sortingOrder = Sorting.Floor + 3;
                    mirrors[f] = m;
                }
                Vector2 feet = f.RenderPosition;
                float wet = layout.LakeDepth(feet);
                bool show = wet > 0f && body.enabled && body.sprite != null;
                m.view.enabled = show;
                if (show)
                {
                    // The same frame, upside down, hanging from the feet.
                    m.view.sprite = body.sprite;
                    m.view.flipX = body.flipX;
                    m.view.flipY = !body.flipY;
                    Transform bt = body.transform;
                    m.view.transform.position = bt.position;
                    m.view.transform.localScale = bt.lossyScale;
                    float footY = body.bounds.min.y;
                    float top = m.view.bounds.max.y;
                    m.view.transform.position += new Vector3(0f, footY - top, 0f);
                    float height = body.bounds.size.y;
                    m.view.GetPropertyBlock(m.props);
                    m.props.SetFloat(FeetId, footY);
                    m.props.SetFloat(HeightId, height);
                    m.props.SetFloat(FadeId, Mathf.Clamp01(wet * 2.2f));
                    m.view.SetPropertyBlock(m.props);
                    m.view.color = new Color(1f, 1f, 1f, 0.55f * body.color.a);
                }

                // Rings: a step every so often while walking, a big one on a fast move.
                float moved = Vector2.Distance(feet, m.lastPos);
                m.lastPos = feet;
                m.rippleTimer -= Time.deltaTime;
                if (wet > 0f && moved > 0.004f && m.rippleTimer <= 0f)
                {
                    float speed = moved / Mathf.Max(1e-3f, Time.deltaTime);
                    SpawnRipple(feet, speed > 9f ? 1.9f : 1.05f, speed > 9f ? 1.1f : 0.85f);
                    m.rippleTimer = speed > 9f ? 0.1f : StepRipple;
                }
            }

            // Mirrors of those who left.
            stale.Clear();
            foreach (KeyValuePair<Fighter, Mirror> kv in mirrors)
            {
                if (kv.Key == null || !fighters.Contains(kv.Key)) stale.Add(kv.Key);
            }
            foreach (Fighter f in stale)
            {
                if (mirrors[f].view != null) Destroy(mirrors[f].view.gameObject);
                mirrors.Remove(f);
            }

            for (int i = ripples.Count - 1; i >= 0; i--)
            {
                Ripple r = ripples[i];
                r.age += Time.deltaTime;
                float t = r.age / r.life;
                if (t >= 1f)
                {
                    r.view.enabled = false;
                    continue;
                }
                float s = Mathf.Lerp(0.2f, r.size, 1f - (1f - t) * (1f - t));
                r.view.transform.localScale = new Vector3(s, s * 0.45f, 1f);
                r.view.color = new Color(0.85f, 0.95f, 1f, 0.55f * (1f - t));
            }
        }

        void SpawnRipple(Vector2 at, float size, float life)
        {
            Ripple r = null;
            foreach (Ripple x in ripples)
            {
                if (x.view.enabled) continue;
                r = x;
                break;
            }
            if (r == null)
            {
                if (ripples.Count >= 48) return;
                r = new Ripple { view = SpriteUtil.Make("Ripple", transform, ShapeLibrary.Get(Shape.Ring), Color.white, Sorting.Floor + 4) };
                ripples.Add(r);
            }
            r.view.enabled = true;
            r.view.transform.position = at;
            r.age = 0f;
            r.life = life;
            r.size = size;
        }
    }
}
