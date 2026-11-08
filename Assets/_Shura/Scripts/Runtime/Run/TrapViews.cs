using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>
    /// What traps look like (purely visual, driven by the room's trap clock): spike plates
    /// that flicker red before they erupt, fire vents that glow then breathe flame, and
    /// blades sweeping around a stone pivot with a faint circle showing their reach.
    /// </summary>
    public sealed class TrapViews
    {
        sealed class View
        {
            public TrapDef trap;
            public readonly List<SpriteRenderer> parts = new List<SpriteRenderer>();
            public SpriteRenderer glow;
        }

        readonly List<View> views = new List<View>();
        readonly Sprite spikesUp;
        readonly Sprite spikesDown;
        readonly Sprite[] flame;

        public TrapViews(Transform parent, ArenaLayout layout)
        {
            spikesUp = PixelArt.Region("tiles_dungeon", 4, 1, 1, 1, new Vector2(0.5f, 0.5f));
            spikesDown = PixelArt.Region("tiles_dungeon", 5, 1, 1, 1, new Vector2(0.5f, 0.5f));
            flame = PixelArt.Strip("fx_flame", 25, 30, new Vector2(0.5f, 0.15f));
            Sprite vent = PixelArt.Region("tiles_dungeon", 2, 3, 1, 1, new Vector2(0.5f, 0.5f));
            Sprite pivot = PixelArt.Region("tiles_dungeon", 2, 2, 1, 1, new Vector2(0.5f, 0.5f));
            Sprite blade = PixelArt.Weapon("weapon_BigSword");

            foreach (TrapDef trap in layout.Traps)
            {
                var v = new View { trap = trap };
                switch (trap.type)
                {
                    case TrapType.Spikes:
                        foreach (Vector2Int c in trap.cells)
                        {
                            var sr = SpriteUtil.Make("Spikes", parent, spikesDown, Color.white, Sorting.Floor + 6);
                            sr.transform.localPosition = layout.CellCenter(c.x, c.y);
                            v.parts.Add(sr);
                        }
                        break;
                    case TrapType.FlameJet:
                    {
                        var ventR = SpriteUtil.Make("Vent", parent, vent, Color.white, Sorting.Floor + 6);
                        ventR.transform.localPosition = trap.origin;
                        v.glow = SpriteUtil.Make("VentGlow", parent, ShapeLibrary.Get(Shape.SoftCircle), new Color(1f, 0.5f, 0.2f, 0f), Sorting.Shadow + 6);
                        v.glow.transform.localPosition = trap.origin;
                        v.glow.transform.localScale = Vector3.one * 1.6f;
                        int n = Mathf.Max(2, Mathf.RoundToInt(trap.length / 0.75f));
                        for (int i = 0; i < n; i++)
                        {
                            Vector2 p = trap.origin + trap.direction * (0.5f + i * trap.length / n);
                            var f = SpriteUtil.Make("Flame", parent, flame.Length > 0 ? flame[0] : ShapeLibrary.Get(Shape.SoftCircle), Color.white, -Mathf.RoundToInt(p.y * 20f) + 2);
                            f.transform.localPosition = p;
                            f.transform.localScale = Vector3.one * 0.9f;
                            f.enabled = false;
                            v.parts.Add(f);
                        }
                        break;
                    }
                    case TrapType.Blade:
                    {
                        var track = SpriteUtil.Make("Track", parent, ShapeLibrary.Get(Shape.Ring), new Color(1f, 0.35f, 0.25f, 0.12f), Sorting.Floor + 5);
                        track.transform.localPosition = trap.origin;
                        track.transform.localScale = Vector3.one * trap.length * 2.1f;
                        var baseR = SpriteUtil.Make("Pivot", parent, pivot, Color.white, -Mathf.RoundToInt(trap.origin.y * 20f));
                        baseR.transform.localPosition = trap.origin;
                        for (int i = 0; i < 2; i++)
                        {
                            var holder = new GameObject("BladeArm").transform;
                            holder.SetParent(parent, false);
                            holder.localPosition = trap.origin;
                            var arm = SpriteUtil.Make("Blade", holder, blade, Color.white, Sorting.Overlay - 40);
                            arm.transform.localPosition = new Vector3(0.2f, 0f, 0f);
                            arm.transform.localScale = Vector3.one * trap.length;
                            v.parts.Add(arm);
                        }
                        break;
                    }
                }
                views.Add(v);
            }
        }

        public void Render(int tick, float time)
        {
            foreach (View v in views)
            {
                TrapDef trap = v.trap;
                int phase = trap.PhaseAt(tick);
                switch (trap.type)
                {
                    case TrapType.Spikes:
                    {
                        bool flicker = phase == 1 && Mathf.Repeat(time * 10f, 1f) < 0.5f;
                        foreach (SpriteRenderer sr in v.parts)
                        {
                            sr.sprite = phase == 2 ? spikesUp : spikesDown;
                            sr.color = flicker ? new Color(1f, 0.45f, 0.4f) : Color.white;
                        }
                        break;
                    }
                    case TrapType.FlameJet:
                    {
                        float warm = phase == 1 ? 0.35f + 0.25f * Mathf.Sin(time * 18f) : phase == 2 ? 0.55f : 0f;
                        v.glow.color = new Color(1f, 0.5f, 0.2f, warm);
                        for (int i = 0; i < v.parts.Count; i++)
                        {
                            SpriteRenderer f = v.parts[i];
                            f.enabled = phase == 2;
                            if (phase == 2 && flame.Length > 0) f.sprite = flame[(Mathf.FloorToInt(time * 14f) + i) % flame.Length];
                        }
                        break;
                    }
                    case TrapType.Blade:
                    {
                        float a = trap.BladeAngle(tick);
                        for (int i = 0; i < v.parts.Count; i++)
                        {
                            v.parts[i].transform.parent.localRotation = Quaternion.Euler(0f, 0f, a + i * 180f);
                        }
                        break;
                    }
                }
            }
        }
    }
}
