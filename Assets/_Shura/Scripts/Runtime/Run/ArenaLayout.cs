using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>Outline of an arena's floor.</summary>
    public enum ArenaShape
    {
        /// <summary>Plain rectangular hall.</summary>
        Hall,
        /// <summary>Corners cut on the diagonal.</summary>
        Octagon,
        /// <summary>A plus sign: four corner blocks removed.</summary>
        Cross,
        /// <summary>Hall broken by statue pillars to fight around.</summary>
        Pillars,
        /// <summary>A ring around a central shrine.</summary>
        Courtyard,
    }

    public enum TrapType
    {
        /// <summary>Floor patch that erupts on a cycle, warned by red flicker.</summary>
        Spikes,
        /// <summary>Wall vent that breathes a line of fire on a cycle.</summary>
        FlameJet,
        /// <summary>A blade sweeping around a pivot, always turning.</summary>
        Blade,
    }

    /// <summary>One trap. Traps hurt everyone, enemies included: lure them in.</summary>
    public sealed class TrapDef
    {
        public TrapType type;
        /// <summary>Spikes: the cells they cover.</summary>
        public readonly List<Vector2Int> cells = new List<Vector2Int>();
        /// <summary>Flame vent / blade pivot, in world units.</summary>
        public Vector2 origin;
        /// <summary>Direction the flame breathes.</summary>
        public Vector2 direction;
        public float length;
        public float width;
        /// <summary>Blade angular speed (degrees per second, sign = direction).</summary>
        public float spin;
        public float startAngle;
        public int period;
        public int warn;
        public int active;
        public int offset;
        public float damage;
        public float posture;

        /// <summary>0 idle, 1 warning, 2 active (cycling traps; blades are always active).</summary>
        public int PhaseAt(int tick)
        {
            if (type == TrapType.Blade) return 2;
            int t = ((tick + offset) % period + period) % period;
            if (t >= period - active) return 2;
            if (t >= period - active - warn) return 1;
            return 0;
        }

        /// <summary>Ticks since the current active phase began (for "once per activation" hits).</summary>
        public int ActivationIndex(int tick) => (tick + offset) / Mathf.Max(1, period);

        public float BladeAngle(int tick) => startAngle + spin * tick * SimClock.Dt;
    }

    /// <summary>
    /// The arena as a grid of 1-unit cells: which are floor, where the statues stand and
    /// where the traps are. It is the authority for collision (fighters slide along walls and
    /// pillars), spawning and projectile walls. Built deterministically from the room's seed.
    /// The grid has a 1-cell wall border plus an extra row on top for banners.
    /// </summary>
    public sealed class ArenaLayout
    {
        public readonly int W;
        public readonly int H;
        public readonly ArenaShape Shape;
        /// <summary>World position of cell (0,0)'s lower-left corner.</summary>
        public readonly Vector2 Origin;
        readonly bool[] floor;
        public readonly List<RectInt> Statues = new List<RectInt>();
        public readonly List<TrapDef> Traps = new List<TrapDef>();
        public Rect FloorBounds { get; private set; }

        public ArenaLayout(int floorW, int floorH, ArenaShape shape)
        {
            W = floorW + 2;
            H = floorH + 3;
            Shape = shape;
            Origin = new Vector2(-W * 0.5f, -(floorH + 2) * 0.5f);
            floor = new bool[W * H];
            for (int y = 1; y <= floorH; y++)
            {
                for (int x = 1; x <= floorW; x++) floor[y * W + x] = true;
            }
            CarveShape(floorW, floorH);
            RecomputeBounds();
        }

        // ── Queries ─────────────────────────────────────────────────────────────────────

        public bool IsFloor(int x, int y) => x >= 0 && y >= 0 && x < W && y < H && floor[y * W + x];

        public Vector2Int CellAt(Vector2 p) => new Vector2Int(Mathf.FloorToInt(p.x - Origin.x), Mathf.FloorToInt(p.y - Origin.y));

        public bool IsFloorAt(Vector2 p)
        {
            Vector2Int c = CellAt(p);
            return IsFloor(c.x, c.y);
        }

        public Vector2 CellCenter(int x, int y) => Origin + new Vector2(x + 0.5f, y + 0.5f);

        /// <summary>True when a circle fits entirely on floor.</summary>
        public bool Fits(Vector2 p, float r)
        {
            return Resolve(p, r) == p && IsFloorAt(p);
        }

        /// <summary>
        /// Pushes a circle out of every wall or pillar cell it overlaps (it slides along them).
        /// Something thrown deep into a wall or past the border lands on the nearest floor instead.
        /// </summary>
        public Vector2 Resolve(Vector2 p, float r)
        {
            Vector2 q = Slide(p, r);
            return IsFloorAt(q) ? q : Slide(NearestFloorCell(p), r);
        }

        Vector2 Slide(Vector2 p, float r)
        {
            for (int iter = 0; iter < 3; iter++)
            {
                bool moved = false;
                int x0 = Mathf.FloorToInt(p.x - r - Origin.x), x1 = Mathf.FloorToInt(p.x + r - Origin.x);
                int y0 = Mathf.FloorToInt(p.y - r - Origin.y), y1 = Mathf.FloorToInt(p.y + r - Origin.y);
                for (int cy = y0; cy <= y1; cy++)
                {
                    for (int cx = x0; cx <= x1; cx++)
                    {
                        if (IsFloor(cx, cy)) continue;
                        Vector2 min = Origin + new Vector2(cx, cy);
                        Vector2 max = min + Vector2.one;
                        var closest = new Vector2(Mathf.Clamp(p.x, min.x, max.x), Mathf.Clamp(p.y, min.y, max.y));
                        Vector2 d = p - closest;
                        float dist = d.magnitude;
                        if (dist >= r) continue;
                        if (dist > 1e-5f)
                        {
                            p += d / dist * (r - dist);
                        }
                        else
                        {
                            // Centre inside the cell: leave by the shortest side.
                            float left = p.x - min.x, right = max.x - p.x, down = p.y - min.y, up = max.y - p.y;
                            float m = Mathf.Min(Mathf.Min(left, right), Mathf.Min(down, up));
                            if (m == left) p.x = min.x - r;
                            else if (m == right) p.x = max.x + r;
                            else if (m == down) p.y = min.y - r;
                            else p.y = max.y + r;
                        }
                        moved = true;
                    }
                }
                if (!moved) break;
            }
            return p;
        }

        /// <summary>A random spot where a fighter of the given radius stands clear of walls.</summary>
        public Vector2 RandomFloorPoint(SeededRandom rng, float clearance)
        {
            for (int attempt = 0; attempt < 64; attempt++)
            {
                int x = rng.Range(0, W);
                int y = rng.Range(0, H);
                if (!IsFloor(x, y)) continue;
                Vector2 p = CellCenter(x, y) + new Vector2(rng.Range(-0.3f, 0.3f), rng.Range(-0.3f, 0.3f));
                if (Resolve(p, clearance) == p) return p;
            }
            return FloorBounds.center;
        }

        /// <summary>The floor point nearest to a target (spawning a party in a column, teleports).</summary>
        public Vector2 NearestFloor(Vector2 target, float clearance) => Resolve(target, clearance);

        // ── Navigation ──────────────────────────────────────────────────────────────────

        readonly Dictionary<int, int[]> fields = new Dictionary<int, int[]>();
        static readonly Vector2Int[] Steps =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1),
            new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1),
        };

        /// <summary>
        /// The direction to walk from one point toward another without getting caught on statues
        /// or pillars: straight when the way is open, otherwise along the shortest path on the grid.
        /// Zero when already there or no path exists.
        /// </summary>
        public Vector2 Steer(Vector2 from, Vector2 to, float radius)
        {
            Vector2 straight = to - from;
            float dist = straight.magnitude;
            if (dist < 1e-3f) return Vector2.zero;
            if (Clear(from, to, radius)) return straight / dist;

            Vector2Int goal = CellAt(Resolve(to, 0.05f));
            Vector2Int here = CellAt(from);
            if (!IsFloor(here.x, here.y)) here = CellAt(Resolve(from, radius));
            int[] field = FieldTo(goal);
            if (field == null || !IsFloor(here.x, here.y) || field[here.y * W + here.x] < 0) return straight / dist;

            // Look a few cells down the path and head for the furthest one in plain sight:
            // fighters cut corners smoothly instead of zig-zagging through cell centres.
            Vector2Int cell = here;
            Vector2 aim = CellCenter(here.x, here.y);
            for (int hop = 0; hop < 6; hop++)
            {
                int cur = field[cell.y * W + cell.x];
                if (cur == 0) break;
                Vector2Int next = cell;
                int best = cur;
                foreach (Vector2Int s in Steps)
                {
                    Vector2Int n = cell + s;
                    if (!Walkable(cell, s)) continue;
                    int v = field[n.y * W + n.x];
                    if (v >= 0 && v < best)
                    {
                        best = v;
                        next = n;
                    }
                }
                if (next == cell) break;
                Vector2 c = CellCenter(next.x, next.y);
                if (hop > 0 && !Clear(from, c, radius)) break;
                aim = c;
                cell = next;
            }
            Vector2 d = aim - from;
            float m = d.magnitude;
            return m > 1e-3f ? d / m : straight / dist;
        }

        /// <summary>True when a circle can slide in a straight line between two points.</summary>
        public bool Clear(Vector2 a, Vector2 b, float radius)
        {
            Vector2 d = b - a;
            float len = d.magnitude;
            if (len < 1e-3f) return true;
            Vector2 dir = d / len;
            Vector2 side = new Vector2(-dir.y, dir.x) * (radius * 0.9f);
            for (float t = 0f; t <= len; t += 0.3f)
            {
                Vector2 p = a + dir * t;
                if (!IsFloorAt(p) || !IsFloorAt(p + side) || !IsFloorAt(p - side)) return false;
            }
            return IsFloorAt(b);
        }

        bool Walkable(Vector2Int from, Vector2Int step)
        {
            Vector2Int n = from + step;
            if (!IsFloor(n.x, n.y)) return false;
            // Diagonals only where both sides are open (no squeezing between corners).
            return step.x == 0 || step.y == 0 || (IsFloor(from.x + step.x, from.y) && IsFloor(from.x, from.y + step.y));
        }

        /// <summary>Path lengths (in steps) from every floor cell to the goal, cached per goal.</summary>
        int[] FieldTo(Vector2Int goal)
        {
            if (!IsFloor(goal.x, goal.y)) return null;
            int key = goal.y * W + goal.x;
            if (fields.TryGetValue(key, out int[] field)) return field;
            if (fields.Count > 48) fields.Clear();

            field = new int[W * H];
            for (int i = 0; i < field.Length; i++) field[i] = -1;
            field[key] = 0;
            var open = new Queue<Vector2Int>();
            open.Enqueue(goal);
            while (open.Count > 0)
            {
                Vector2Int c = open.Dequeue();
                int v = field[c.y * W + c.x];
                foreach (Vector2Int s in Steps)
                {
                    if (!Walkable(c, s)) continue;
                    Vector2Int n = c + s;
                    int ni = n.y * W + n.x;
                    if (field[ni] >= 0) continue;
                    field[ni] = v + 1;
                    open.Enqueue(n);
                }
            }
            fields[key] = field;
            return field;
        }

        Vector2 NearestFloorCell(Vector2 target)
        {
            float best = float.MaxValue;
            Vector2 result = FloorBounds.center;
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    if (!IsFloor(x, y)) continue;
                    Vector2 c = CellCenter(x, y);
                    float d = (c - target).sqrMagnitude;
                    if (d < best)
                    {
                        best = d;
                        result = c;
                    }
                }
            }
            return result;
        }

        // ── Shapes ──────────────────────────────────────────────────────────────────────

        void Solid(int x, int y)
        {
            if (x >= 0 && y >= 0 && x < W && y < H) floor[y * W + x] = false;
        }

        void CarveShape(int fw, int fh)
        {
            switch (Shape)
            {
                case ArenaShape.Octagon:
                {
                    int c = Mathf.Max(2, Mathf.Min(fw, fh) / 4);
                    for (int y = 0; y < fh; y++)
                    {
                        for (int x = 0; x < fw; x++)
                        {
                            int dx = Mathf.Min(x, fw - 1 - x);
                            int dy = Mathf.Min(y, fh - 1 - y);
                            if (dx + dy < c) Solid(x + 1, y + 1);
                        }
                    }
                    break;
                }
                case ArenaShape.Cross:
                {
                    int cx = Mathf.Max(3, fw / 4);
                    int cy = Mathf.Max(2, fh / 4);
                    for (int y = 0; y < fh; y++)
                    {
                        for (int x = 0; x < fw; x++)
                        {
                            bool edgeX = x < cx || x >= fw - cx;
                            bool edgeY = y < cy || y >= fh - cy;
                            if (edgeX && edgeY) Solid(x + 1, y + 1);
                        }
                    }
                    break;
                }
                case ArenaShape.Pillars:
                {
                    // Statues on the quarter points (never on the spawn line at the left).
                    int[] xs = { fw / 3, fw - fw / 3 - 2 };
                    int[] ys = { fh / 4, fh - fh / 4 - 2 };
                    foreach (int px in xs)
                    {
                        foreach (int py in ys) AddStatue(new RectInt(px + 1, py + 1, 2, 2));
                    }
                    break;
                }
                case ArenaShape.Courtyard:
                {
                    int bw = Mathf.Max(3, fw / 5);
                    int bh = Mathf.Max(2, fh / 4);
                    AddStatue(new RectInt(1 + (fw - bw) / 2, 1 + (fh - bh) / 2, bw, bh));
                    break;
                }
            }
        }

        void AddStatue(RectInt block)
        {
            Statues.Add(block);
            for (int y = block.yMin; y < block.yMax; y++)
            {
                for (int x = block.xMin; x < block.xMax; x++) Solid(x, y);
            }
        }

        void RecomputeBounds()
        {
            int minX = W, minY = H, maxX = -1, maxY = -1;
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    if (!IsFloor(x, y)) continue;
                    minX = Mathf.Min(minX, x);
                    minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x);
                    maxY = Mathf.Max(maxY, y);
                }
            }
            FloorBounds = new Rect(Origin.x + minX, Origin.y + minY, maxX - minX + 1, maxY - minY + 1);
        }

        // ── Traps ───────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Places traps for the depth: spikes from the first room after the intro, fire vents
        /// from depth 3, sweeping blades from depth 5. Nothing within reach of the spawn.
        /// </summary>
        public void PlaceTraps(SeededRandom rng, int depth, Vector2 spawn)
        {
            if (depth <= 0) return;
            float area = FloorBounds.width * FloorBounds.height;
            int spikes = Mathf.Clamp(Mathf.RoundToInt(area / 140f) + (depth >= 4 ? 1 : 0), 1, 5);
            int flames = depth >= 3 ? Mathf.Clamp(Mathf.RoundToInt(area / 260f), 1, 3) : 0;
            int blades = depth >= 5 ? (area > 420f ? 2 : 1) : 0;
            float power = 1f + depth * 0.06f;
            var used = new List<Vector2>();

            for (int i = 0; i < spikes; i++)
            {
                if (!FindSpot(rng, spawn, used, 6f, 1, out Vector2Int c)) break;
                var t = new TrapDef
                {
                    type = TrapType.Spikes, period = 200, warn = 42, active = 26, offset = rng.Range(0, 200),
                    damage = 13f * power, posture = 18f,
                };
                int w = rng.Range(2, 4), h = rng.Range(1, 3);
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        if (IsFloor(c.x + x, c.y + y)) t.cells.Add(new Vector2Int(c.x + x, c.y + y));
                    }
                }
                if (t.cells.Count == 0) continue;
                t.origin = CellCenter(c.x, c.y);
                Traps.Add(t);
                used.Add(t.origin);
            }

            for (int i = 0; i < flames; i++)
            {
                // A vent sits on floor against a wall and breathes into the room.
                for (int attempt = 0; attempt < 40; attempt++)
                {
                    int x = rng.Range(1, W - 1), y = rng.Range(1, H - 1);
                    if (!IsFloor(x, y)) continue;
                    Vector2Int dir;
                    if (!IsFloor(x - 1, y)) dir = Vector2Int.right;
                    else if (!IsFloor(x + 1, y)) dir = Vector2Int.left;
                    else if (!IsFloor(x, y + 1)) dir = Vector2Int.down;
                    else if (!IsFloor(x, y - 1)) dir = Vector2Int.up;
                    else continue;
                    Vector2 o = CellCenter(x, y);
                    if (Vector2.Distance(o, spawn) < 6f || TooClose(o, used, 4f)) continue;
                    int len = 0;
                    while (len < 7 && IsFloor(x + dir.x * (len + 1), y + dir.y * (len + 1))) len++;
                    if (len < 3) continue;
                    Traps.Add(new TrapDef
                    {
                        type = TrapType.FlameJet, origin = o, direction = new Vector2(dir.x, dir.y), length = len + 0.5f, width = 0.9f,
                        period = 260, warn = 55, active = 80, offset = rng.Range(0, 260), damage = 7f * power, posture = 10f,
                    });
                    used.Add(o);
                    break;
                }
            }

            for (int i = 0; i < blades; i++)
            {
                if (!FindSpot(rng, spawn, used, 7f, 3, out Vector2Int c)) break;
                Vector2 o = CellCenter(c.x, c.y);
                Traps.Add(new TrapDef
                {
                    type = TrapType.Blade, origin = o, length = rng.Range(2.2f, 2.9f), width = 0.5f,
                    spin = (rng.NextFloat() < 0.5f ? -1f : 1f) * rng.Range(85f, 125f), startAngle = rng.Range(0f, 360f),
                    damage = 15f * power, posture = 22f,
                });
                used.Add(o);
            }
        }

        bool FindSpot(SeededRandom rng, Vector2 spawn, List<Vector2> used, float minFromSpawn, int clearCells, out Vector2Int cell)
        {
            for (int attempt = 0; attempt < 60; attempt++)
            {
                int x = rng.Range(1, W - 1), y = rng.Range(1, H - 1);
                bool clear = true;
                for (int dy = -clearCells + 1; dy < clearCells && clear; dy++)
                {
                    for (int dx = -clearCells + 1; dx < clearCells && clear; dx++) clear = IsFloor(x + dx, y + dy);
                }
                if (!clear) continue;
                Vector2 c = CellCenter(x, y);
                if (Vector2.Distance(c, spawn) < minFromSpawn || TooClose(c, used, 4.5f)) continue;
                cell = new Vector2Int(x, y);
                return true;
            }
            cell = default;
            return false;
        }

        static bool TooClose(Vector2 p, List<Vector2> others, float min)
        {
            foreach (Vector2 o in others)
            {
                if (Vector2.Distance(p, o) < min) return true;
            }
            return false;
        }
    }
}
