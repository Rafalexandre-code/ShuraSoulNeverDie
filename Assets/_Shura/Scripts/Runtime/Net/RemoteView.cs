using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Shura
{
    /// <summary>
    /// The host's duel drawn in a guest's window: a puppet for every duelist and enemy (the
    /// same pixel art, frames, blade angles, glows and timing rings the host draws), arrows,
    /// bombs, loot, zones, props and offerings. Nothing here simulates: every frame poses the
    /// puppets between two snapshots of the host.
    /// </summary>
    public sealed class RemoteView
    {
        /// <summary>A duelist or an enemy, posed from the snapshot rows.</summary>
        public sealed class Puppet
        {
            public GameObject go;
            public Transform t, body, weaponPivot;
            public SpriteRenderer shadow, sprite, glow, weapon, marker, aura;
            public CharacterArt art;
            public string artId, weaponArt;
            public float radius = 0.38f, feetY = -0.228f, scale = 1f;
            public Vector2 pos, facing = Vector2.right;
            public List<object> row;
            public bool alive = true;
            public bool isMe;
            public float bobPhase;
            public int lastWalk = -1;
            public float top;
            // Enemy marks.
            public SpriteRenderer area, ringPreview, line, danger, barBack, hp, posture, mastered, broken, eliteAura, frost;
            public SpriteRenderer tGlow, tTarget, tApproach, tInner;
            public SpriteRenderer[] seals;
            public int wedge = -1;
            public bool seen;
        }

        sealed class Shot
        {
            public GameObject go;
            public SpriteRenderer view, ring, glow;
            public string art;
        }

        sealed class Blast
        {
            public GameObject go;
            public SpriteRenderer fill, ring, icon;
            public int kind = -1;
        }

        sealed class Loot
        {
            public GameObject go;
            public SpriteRenderer glow, beam, halo, icon;
            public string key;
        }

        const float BarWidth = 1.1f;
        const int MaxSeals = 5;
        const float RingApproachFrames = 30f;

        readonly Game game;
        readonly RemoteSession session;
        Transform root;
        readonly Dictionary<int, Puppet> players = new Dictionary<int, Puppet>();
        readonly Dictionary<int, Puppet> enemies = new Dictionary<int, Puppet>();
        readonly List<Shot> shots = new List<Shot>();
        readonly List<Blast> blasts = new List<Blast>();
        readonly List<Loot> loot = new List<Loot>();
        readonly List<GameObject> zones = new List<GameObject>();
        readonly List<GameObject> props = new List<GameObject>();
        readonly List<GameObject> offerings = new List<GameObject>();
        readonly StringBuilder key = new StringBuilder(256);
        readonly List<int> drop = new List<int>();
        string zoneKey, propKey, offeringKey;
        static Sprite sealSprite;

        public RemoteView(Game owner, RemoteSession owner2)
        {
            game = owner;
            session = owner2;
        }

        Palette Pal => game.Config.palette;
        public IEnumerable<Puppet> Players => players.Values;
        public IEnumerable<Puppet> Enemies => enemies.Values;

        Transform Root
        {
            get
            {
                if (root == null) root = new GameObject("RemoteDuel").transform;
                return root;
            }
        }

        public void Clear()
        {
            if (root != null) Object.Destroy(root.gameObject);
            root = null;
            players.Clear();
            enemies.Clear();
            shots.Clear();
            blasts.Clear();
            loot.Clear();
            zones.Clear();
            props.Clear();
            offerings.Clear();
            zoneKey = propKey = offeringKey = null;
        }

        /// <summary>A new arena: what lay on the old floor goes with it.</summary>
        public void OnRoom()
        {
            foreach (Puppet e in enemies.Values) Object.Destroy(e.go);
            enemies.Clear();
            foreach (Shot s in shots) Object.Destroy(s.go);
            shots.Clear();
            foreach (Blast b in blasts) Object.Destroy(b.go);
            blasts.Clear();
            foreach (Loot l in loot) Object.Destroy(l.go);
            loot.Clear();
            DestroyAll(zones);
            DestroyAll(props);
            DestroyAll(offerings);
            zoneKey = propKey = offeringKey = null;
        }

        static void DestroyAll(List<GameObject> list)
        {
            foreach (GameObject go in list)
            {
                if (go != null) Object.Destroy(go);
            }
            list.Clear();
        }

        /// <summary>The body sprite of whatever enemy stands nearest (a finisher's gore uses its art).</summary>
        public Sprite BodyNear(Vector2 at)
        {
            Sprite best = null;
            float bestD = 4f;
            foreach (Puppet p in enemies.Values)
            {
                float d = (p.pos - at).sqrMagnitude;
                if (d < bestD && p.sprite != null && p.sprite.sprite != null)
                {
                    bestD = d;
                    best = p.sprite.sprite;
                }
            }
            return best;
        }

        // ── Per frame ───────────────────────────────────────────────────────────────────

        public void Render(RemoteSession.Snap a, RemoteSession.Snap b, float t, double renderMs, RemoteSession.Snap latest, float dt)
        {
            List<object> me = session.Me;
            var mePos = session.OwnPosition;
            RenderPlayers(a, b, t, latest, dt);
            RenderEnemies(a, b, t, me, mePos, dt);
            RenderShots(b, renderMs, me, mePos);
            RenderBlasts(b);
            RenderLoot(b);
            RenderZones(b);
            RenderProps(b);
            RenderOfferings(b);
            game.World.RenderRemote(MiniJson.I(b.d, "k"), Ints(b.SB), Ints(b.LB));
        }

        static List<int> Ints(List<object> list)
        {
            if (list == null || list.Count == 0) return null;
            var r = new List<int>(list.Count);
            foreach (object o in list) r.Add(o is double d ? (int)d : 0);
            return r;
        }

        // ── Duelists ────────────────────────────────────────────────────────────────────

        void RenderPlayers(RemoteSession.Snap a, RemoteSession.Snap b, float t, RemoteSession.Snap latest, float dt)
        {
            foreach (Puppet p in players.Values) p.seen = false;
            if (latest.P != null)
            {
                foreach (object o in latest.P)
                {
                    if (!(o is List<object> rowLatest)) continue;
                    int id = MiniJson.I(rowLatest, 0);
                    bool isMe = id == session.MyId;
                    // Teammates on the host's timeline (interpolated); this duelist from the newest word, predicted.
                    List<object> row = rowLatest, prev = null;
                    float k = 1f;
                    if (!isMe && b.players.TryGetValue(id, out List<object> rb))
                    {
                        row = rb;
                        a.players.TryGetValue(id, out prev);
                        k = t;
                    }
                    if (!players.TryGetValue(id, out Puppet p))
                    {
                        p = MakeFighter("Duelist " + id);
                        players[id] = p;
                    }
                    p.seen = true;
                    p.isMe = isMe;
                    p.row = row;
                    string art = MiniJson.S(row, 2);
                    if (art != p.artId) SetArt(p, art);
                    SetWeapon(p, MiniJson.S(row, 14));
                    p.radius = RemoteSession.PlayerRadius;
                    p.feetY = -RemoteSession.PlayerRadius * 0.6f;
                    p.scale = 1f;
                    p.alive = MiniJson.B(row, 21);

                    Vector2 pos = Lerp2(prev, row, 3, 4, k);
                    Vector2 facing = new Vector2(MiniJson.F(row, 5), MiniJson.F(row, 6));
                    float weaponAngle = LerpAngle(prev, row, 10, k);
                    int state = MiniJson.I(row, 59);
                    int dir = MiniJson.I(row, 7);
                    if (isMe && session.HasOwn)
                    {
                        pos = session.OwnPosition;
                        // Turning is felt at once: the body and the resting blade follow the aim here.
                        if (p.alive && state == (int)PlayerController.State.Free)
                        {
                            Vector2 aim = session.Aim;
                            float delta = Mathf.DeltaAngle(CombatMath.AngleDegrees(facing.sqrMagnitude > 0.01f ? facing : Vector2.right), CombatMath.AngleDegrees(aim));
                            weaponAngle += delta;
                            facing = aim;
                            dir = (int)CharacterArt.DirOf(aim);
                        }
                    }
                    p.pos = pos;
                    p.facing = facing;
                    float tint = Mathf.Max(0.35f, MiniJson.F(row, 23, 1f));
                    Pose(p, pos, dir, p.alive ? MiniJson.I(row, 8) : 3, MiniJson.I(row, 9),
                         MiniJson.Color(MiniJson.S(row, 16), Color.white).WithAlpha(MiniJson.F(row, 15)), p.alive ? tint : 0.9f, dt);
                    Weapon(p, weaponAngle, Lerp(prev, row, 11, k), Lerp(prev, row, 12, k), p.alive && MiniJson.B(row, 13), tint);
                    if (isMe && MiniJson.I(row, 8) == 1 && p.alive)
                    {
                        // Footsteps of this duelist only (the host plays its own).
                        int walk = MiniJson.I(row, 9);
                        if (walk != p.lastWalk && (walk == 0 || walk == 2)) game.Feedback.Footstep(pos);
                        p.lastWalk = walk;
                    }

                    // Under the feet: a gold ring marks you; Asura burns red, Mushin glows.
                    p.marker.enabled = isMe && p.alive;
                    if (p.marker.enabled) p.marker.color = Pal.gold.WithAlpha(0.35f);
                    bool asura = MiniJson.B(row, 45), mushin = MiniJson.B(row, 44);
                    p.aura.enabled = p.alive && (asura || mushin);
                    if (p.aura.enabled)
                    {
                        float s = 1.8f + 0.16f * Mathf.Sin(Time.unscaledTime * (asura ? 14f : 3f));
                        p.aura.transform.localScale = new Vector3(s, s, 1f);
                        p.aura.color = (asura ? Pal.shu : Pal.gold).WithAlpha(asura ? 0.28f : 0.18f);
                    }
                    p.top = p.feetY + (p.art != null ? p.art.visibleHeight : 1f);
                }
            }
            Sweep(players);
        }

        // ── Enemies ─────────────────────────────────────────────────────────────────────

        void RenderEnemies(RemoteSession.Snap a, RemoteSession.Snap b, float t, List<object> me, Vector2 mePos, float dt)
        {
            foreach (Puppet p in enemies.Values) p.seen = false;
            List<object> env = b.env;
            float ringRange = env != null ? MiniJson.F(env, 4) : 0f;
            float fogRange = env != null ? MiniJson.F(env, 5) : 0f;
            bool ringsHidden = me != null && (MiniJson.B(me, 50) || MiniJson.B(me, 57));
            float ringScale = me != null ? Mathf.Max(0.3f, MiniJson.F(me, 51, 1f)) : 1f;
            bool showRings = game.Settings.timingIndicator && !ringsHidden;
            if (b.E != null)
            {
                foreach (object o in b.E)
                {
                    if (!(o is List<object> row)) continue;
                    int id = MiniJson.I(row, 0);
                    a.enemies.TryGetValue(id, out List<object> prev);
                    if (!enemies.TryGetValue(id, out Puppet p))
                    {
                        p = MakeFighter("Enemy " + id);
                        MakeEnemyMarks(p);
                        enemies[id] = p;
                    }
                    p.seen = true;
                    p.row = row;
                    string art = MiniJson.S(row, 1);
                    if (art != p.artId) SetArt(p, art);
                    SetWeapon(p, MiniJson.S(row, 13));
                    p.scale = Mathf.Max(0.2f, MiniJson.F(row, 7, 1f));
                    p.feetY = MiniJson.F(row, 8, -0.24f);
                    p.radius = Mathf.Max(0.2f, -p.feetY / 0.6f);
                    bool gone = MiniJson.B(row, 32);
                    Vector2 pos = Lerp2(prev, row, 2, 3, t);
                    p.pos = pos;
                    p.facing = new Vector2(MiniJson.F(row, 25), MiniJson.F(row, 26));
                    if (p.facing.sqrMagnitude < 1e-4f) p.facing = Vector2.right;

                    float fog = 1f;
                    if (fogRange > 0f && session.HasOwn) fog = Mathf.Clamp01(1f - (Vector2.Distance(mePos, pos) - fogRange) / 2f);
                    float alpha = MiniJson.F(row, 16, 1f) * fog;
                    bool kalpa = MiniJson.B(row, 40);
                    Color glow = kalpa ? Pal.danger.WithAlpha(0.4f) : MiniJson.Color(MiniJson.S(row, 15), Color.white).WithAlpha(MiniJson.F(row, 14));
                    Pose(p, pos, MiniJson.I(row, 4), MiniJson.I(row, 5), MiniJson.I(row, 6), glow, alpha, dt);
                    Weapon(p, LerpAngle(prev, row, 9, t), Lerp(prev, row, 10, t), Lerp(prev, row, 11, t), MiniJson.B(row, 12) && !gone, alpha);
                    p.shadow.enabled = !gone && alpha > 0.05f;
                    p.marker.enabled = false;
                    p.aura.enabled = false;
                    p.frost.enabled = MiniJson.B(row, 39) && !gone;
                    if (p.frost.enabled) p.frost.color = new Color(0.63f, 0.84f, 1f, 0.45f * fog);
                    float barY = p.feetY + (p.art != null ? p.art.visibleHeight * p.scale : p.radius * 2f) + 0.25f;
                    p.top = barY;

                    EnemyMarks(p, row, gone, fog, barY);
                    bool ringVisible = showRings && !gone && (ringRange <= 0f || !session.HasOwn || Vector2.Distance(mePos, pos) <= ringRange) &&
                                       !(MiniJson.B(row, 35) && session.HasOwn && Vector2.Distance(mePos, pos) > 3f);
                    TimingRing(p, row, ringVisible, ringScale);
                }
            }
            Sweep(enemies);
        }

        void EnemyMarks(Puppet p, List<object> row, bool gone, float fog, float barY)
        {
            int tele = gone ? 0 : MiniJson.I(row, 22);
            float range = MiniJson.F(row, 23), width = MiniJson.F(row, 24), alpha = MiniJson.F(row, 27);
            bool dangerous = MiniJson.B(row, 28);
            Color tint = dangerous ? Pal.danger : Color.white;
            float rot = CombatMath.AngleDegrees(p.facing);
            p.area.enabled = tele == 1 || tele == 2;
            p.ringPreview.enabled = tele == 2;
            p.line.enabled = tele == 3;
            if (p.area.enabled)
            {
                int deg = tele == 2 ? 360 : Mathf.Clamp(Mathf.RoundToInt(width > 0f ? width : 110f), 1, 360);
                if (deg != p.wedge)
                {
                    p.area.sprite = ShapeLibrary.Wedge(deg);
                    p.wedge = deg;
                }
                p.area.color = tint.WithAlpha(tele == 2 ? alpha * 0.6f : alpha);
                p.area.transform.localRotation = Quaternion.Euler(0f, 0f, rot);
                p.area.transform.localScale = new Vector3(range * 2f, range * 2f, 1f);
            }
            if (p.ringPreview.enabled)
            {
                p.ringPreview.color = tint.WithAlpha(Mathf.Min(1f, alpha * 2f));
                p.ringPreview.transform.localScale = new Vector3(range * 2f, range * 2f, 1f);
            }
            if (p.line.enabled)
            {
                p.line.color = tint.WithAlpha(alpha);
                p.line.transform.localRotation = Quaternion.Euler(0f, 0f, rot);
                p.line.transform.localScale = new Vector3(range, Mathf.Max(0.1f, width), 1f);
            }
            p.danger.enabled = tele != 0 && dangerous;
            if (p.danger.enabled)
            {
                float s = 0.42f + 0.08f * Mathf.Sin(Time.unscaledTime * 18f);
                p.danger.transform.localPosition = new Vector3(0f, barY + 0.45f, 0f);
                p.danger.transform.localScale = new Vector3(s, s * 1.25f, 1f);
            }

            bool bars = !gone && MiniJson.I(row, 5) != 3 && fog > 0.3f && MiniJson.F(row, 16, 1f) > 0.05f;
            p.barBack.enabled = p.hp.enabled = p.posture.enabled = bars;
            if (bars)
            {
                p.barBack.transform.localPosition = new Vector3(0f, barY, 0f);
                p.barBack.transform.localScale = new Vector3(BarWidth + 0.08f, 0.22f, 1f);
                p.hp.transform.localPosition = new Vector3(-BarWidth * 0.5f, barY + 0.045f, 0f);
                p.hp.transform.localScale = new Vector3(BarWidth * Mathf.Clamp01(MiniJson.F(row, 17)), 0.07f, 1f);
                float pr = Mathf.Clamp01(MiniJson.F(row, 18));
                bool brokenPosture = MiniJson.B(row, 20);
                p.posture.transform.localPosition = new Vector3(0f, barY - 0.05f, 0f);
                p.posture.transform.localScale = new Vector3(BarWidth * pr, 0.06f, 1f);
                p.posture.color = brokenPosture ? Pal.gold : Color.Lerp(Pal.postureLow, Pal.postureHigh, pr);
                p.hp.color = MiniJson.B(row, 38) ? Pal.blessing : Pal.health;
            }
            p.mastered.enabled = MiniJson.B(row, 21) && !gone;
            if (p.mastered.enabled)
            {
                p.mastered.transform.localPosition = new Vector3(BarWidth * 0.5f + 0.25f, barY, 0f);
                p.mastered.transform.localRotation = Quaternion.Euler(0f, 0f, Time.unscaledTime * 90f);
            }
            bool broken = MiniJson.B(row, 20) && !gone;
            p.broken.enabled = broken;
            if (broken)
            {
                float s = p.radius * 2f * (1.5f + 0.15f * Mathf.Sin(Time.unscaledTime * 10f));
                p.broken.transform.localScale = new Vector3(s, s, 1f);
            }
            p.eliteAura.enabled = MiniJson.B(row, 19) && !gone;
            if (p.eliteAura.enabled)
            {
                float pulse = p.radius * 2f * (2.2f + 0.2f * Mathf.Sin(Time.unscaledTime * 3f));
                p.eliteAura.transform.localScale = new Vector3(pulse, pulse, 1f);
                p.eliteAura.color = Pal.gold.WithAlpha(0.22f);
            }

            int seals = gone ? 0 : MiniJson.I(row, 34);
            float sp = 0.28f + 0.03f * Mathf.Sin(Time.unscaledTime * 6f);
            for (int i = 0; i < MaxSeals; i++)
            {
                bool on = i < seals;
                p.seals[i].enabled = on;
                if (!on) continue;
                float x = (i - (seals - 1) * 0.5f) * 0.32f;
                p.seals[i].transform.localPosition = new Vector3(x, barY + 0.55f, 0f);
                p.seals[i].transform.localScale = new Vector3(sp, sp, 1f);
                p.seals[i].color = seals >= MaxSeals ? Color.Lerp(Pal.shu, Color.white, 0.4f + 0.3f * Mathf.Sin(Time.unscaledTime * 12f)) : Color.white;
            }
        }

        /// <summary>The "answer now" ring, exactly as the host draws it for its own duelist.</summary>
        void TimingRing(Puppet p, List<object> row, bool visible, float ringScale)
        {
            int ring = MiniJson.I(row, 29);
            if (ring == 0 || !visible)
            {
                p.tGlow.enabled = p.tTarget.enabled = p.tApproach.enabled = p.tInner.enabled = false;
                return;
            }
            int kind = MiniJson.I(row, 33);
            float size = MiniJson.F(row, 30) * ringScale;
            float k = MiniJson.F(row, 31);
            Color c = RingColor((RingKind)kind);
            Sprite pattern = ShapeLibrary.RingPattern(kind);
            p.tTarget.sprite = pattern;
            p.tApproach.sprite = pattern;
            bool twoBeat = (RingKind)kind == RingKind.Double;
            float closeAt = twoBeat ? size * 0.6f : size;
            p.tInner.enabled = twoBeat;
            if (twoBeat)
            {
                p.tInner.transform.localScale = new Vector3(size * 0.6f, size * 0.6f, 1f);
                p.tInner.color = c.WithAlpha(0.6f);
            }
            p.tTarget.enabled = true;
            if (ring == 1)
            {
                p.tApproach.enabled = true;
                p.tGlow.enabled = false;
                float s = Mathf.Lerp(size * 3.4f, closeAt, k);
                p.tApproach.transform.localScale = new Vector3(s, s, 1f);
                p.tApproach.color = c.WithAlpha(Mathf.Lerp(0.15f, 0.95f, k));
                p.tTarget.transform.localScale = new Vector3(size, size, 1f);
                p.tTarget.color = c.WithAlpha(0.35f);
                return;
            }
            p.tApproach.enabled = false;
            p.tGlow.enabled = true;
            float pulse = closeAt * (1.18f - 0.18f * k);
            p.tTarget.transform.localScale = new Vector3(pulse, pulse, 1f);
            p.tTarget.color = Color.Lerp(Color.white, c, 0.35f + 0.4f * k);
            float g = closeAt * 1.9f;
            p.tGlow.transform.localScale = new Vector3(g, g, 1f);
            p.tGlow.color = c.WithAlpha(0.55f * (1f - k * 0.5f));
        }

        Color RingColor(RingKind kind)
        {
            switch (kind)
            {
                case RingKind.Danger: return Pal.danger;
                case RingKind.Feint: return Pal.gold;
                case RingKind.Spirit: return ColorUtil.Hex("#B07CFF");
                case RingKind.Cursed: return ColorUtil.Hex("#7BC950");
                case RingKind.Group: return Color.white;
                default: return Pal.playerAccent;
            }
        }

        // ── Puppet building ─────────────────────────────────────────────────────────────

        Puppet MakeFighter(string name)
        {
            var p = new Puppet { go = new GameObject(name) };
            p.t = p.go.transform;
            p.t.SetParent(Root, false);
            p.bobPhase = Random.Range(0f, 10f);
            p.shadow = SpriteUtil.Make("Shadow", p.t, ShapeLibrary.Get(Shape.SoftCircle), new Color(0f, 0f, 0f, 0.45f), Sorting.Shadow);
            p.marker = SpriteUtil.Make("Marker", p.t, ShapeLibrary.Get(Shape.Ring), Pal.gold, Sorting.Ground + 6);
            p.aura = SpriteUtil.Make("Aura", p.t, ShapeLibrary.Get(Shape.SoftCircle), Pal.gold, Sorting.Shadow + 3);
            p.body = new GameObject("Body").transform;
            p.body.SetParent(p.t, false);
            p.sprite = SpriteUtil.Make("Sprite", p.body, ShapeLibrary.Get(Shape.Circle), Color.white, 0);
            p.glow = SpriteUtil.Make("Glow", p.body, ShapeLibrary.Get(Shape.Circle), Color.clear, 1);
            p.weaponPivot = new GameObject("WeaponPivot").transform;
            p.weaponPivot.SetParent(p.t, false);
            p.weapon = SpriteUtil.Make("Weapon", p.weaponPivot, ShapeLibrary.Get(Shape.Blade), Color.white, 2);
            p.frost = SpriteUtil.Make("Frost", p.t, ShapeLibrary.Get(Shape.Circle), Color.clear, Sorting.Shadow + 2);
            p.frost.transform.localScale = new Vector3(1.1f, 1.1f, 1f);
            p.frost.enabled = false;
            return p;
        }

        void MakeEnemyMarks(Puppet p)
        {
            Transform t = p.t;
            p.eliteAura = SpriteUtil.Make("GuardianAura", t, ShapeLibrary.Get(Shape.SoftCircle), Pal.gold, Sorting.Shadow + 3);
            p.barBack = SpriteUtil.Make("BarBack", t, ShapeLibrary.Get(Shape.Pixel), new Color(0f, 0f, 0f, 0.65f), Sorting.Overlay);
            p.hp = SpriteUtil.Make("Health", t, ShapeLibrary.Get(Shape.PixelLeft), Pal.health, Sorting.Overlay + 1);
            p.posture = SpriteUtil.Make("Posture", t, ShapeLibrary.Get(Shape.Pixel), Pal.postureLow, Sorting.Overlay + 1);
            p.danger = SpriteUtil.Make("Danger", t, ShapeLibrary.Get(Shape.Diamond), Pal.danger, Sorting.Overlay + 2);
            p.area = SpriteUtil.Make("AreaPreview", t, ShapeLibrary.Wedge(110f), Color.white, Sorting.Ground + 1);
            p.ringPreview = SpriteUtil.Make("RingPreview", t, ShapeLibrary.Get(Shape.Ring), Pal.danger, Sorting.Ground + 2);
            p.line = SpriteUtil.Make("LinePreview", t, ShapeLibrary.Get(Shape.PixelLeft), Color.white, Sorting.Ground + 1);
            p.mastered = SpriteUtil.Make("Seal", t, ShapeLibrary.Get(Shape.DiamondOutline), Pal.gold, Sorting.Overlay + 2);
            p.mastered.transform.localScale = new Vector3(0.4f, 0.4f, 1f);
            p.broken = SpriteUtil.Make("BrokenRing", t, ShapeLibrary.Get(Shape.Ring), Pal.gold, Sorting.Overlay - 2);
            p.tGlow = SpriteUtil.Make("TimingGlow", t, ShapeLibrary.Get(Shape.SoftCircle), Color.white, Sorting.Overlay - 4);
            p.tTarget = SpriteUtil.Make("TimingTarget", t, ShapeLibrary.Get(Shape.Ring), Color.white, Sorting.Overlay - 3);
            p.tApproach = SpriteUtil.Make("TimingApproach", t, ShapeLibrary.Get(Shape.Ring), Color.white, Sorting.Overlay - 3);
            p.tInner = SpriteUtil.Make("TimingInner", t, ShapeLibrary.Get(Shape.Ring), Color.white, Sorting.Overlay - 3);
            if (sealSprite == null)
            {
                Texture2D tex = Resources.Load<Texture2D>("UI/seal");
                if (tex != null) sealSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
            }
            p.seals = new SpriteRenderer[MaxSeals];
            for (int i = 0; i < MaxSeals; i++)
            {
                p.seals[i] = SpriteUtil.Make("KiSeal", t, sealSprite != null ? sealSprite : ShapeLibrary.Get(Shape.Square), Pal.shu, Sorting.Overlay + 3);
                p.seals[i].enabled = false;
            }
            p.eliteAura.enabled = p.barBack.enabled = p.hp.enabled = p.posture.enabled = p.danger.enabled = false;
            p.area.enabled = p.ringPreview.enabled = p.line.enabled = p.mastered.enabled = p.broken.enabled = false;
            p.tGlow.enabled = p.tTarget.enabled = p.tApproach.enabled = p.tInner.enabled = false;
        }

        void SetArt(Puppet p, string id)
        {
            p.artId = id;
            p.art = string.IsNullOrEmpty(id) ? null : PixelArt.Character(id);
        }

        void SetWeapon(Puppet p, string art)
        {
            if (art == p.weaponArt) return;
            p.weaponArt = art;
            Sprite s = string.IsNullOrEmpty(art) ? null : art == "weapon_Bow" ? PixelArt.WeaponCentered(art) : PixelArt.Weapon(art);
            p.weapon.sprite = s != null ? s : ShapeLibrary.Get(Shape.Blade);
        }

        /// <summary>Body frame, glow, breathing and the walk's little hop; lower on screen draws in front.</summary>
        void Pose(Puppet p, Vector2 pos, int dir, int pose, int frame, Color glow, float alpha, float dt)
        {
            p.t.position = new Vector3(pos.x, pos.y, 0f);
            int order = -Mathf.RoundToInt(pos.y * 20f);
            p.sprite.sortingOrder = order;
            p.glow.sortingOrder = order + 1;
            dir = Mathf.Clamp(dir, 0, 3);
            CharacterArt art = p.art;
            if (art != null)
            {
                Sprite body, white;
                if (pose == 3 && art.dead != null)
                {
                    body = art.dead;
                    white = art.deadWhite;
                }
                else if (pose == 2)
                {
                    body = art.attack[dir];
                    white = art.attackWhite[dir];
                }
                else if (pose == 1)
                {
                    Sprite[] frames = art.walk[dir];
                    int i = ((frame % frames.Length) + frames.Length) % frames.Length;
                    body = frames[i];
                    white = art.walkWhite[dir][i];
                }
                else
                {
                    body = art.idle[dir];
                    white = art.idleWhite[dir];
                }
                p.sprite.sprite = body;
                p.glow.sprite = white;
            }
            bool moving = pose == 1;
            p.bobPhase += dt * (moving ? 9f : 2.2f);
            float breath = pose != 3 ? 0.025f * Mathf.Sin(p.bobPhase) : 0f;
            float hop = moving ? Mathf.Abs(Mathf.Sin(p.bobPhase * 1.6f)) * 0.05f : 0f;
            float size = art != null ? p.scale : p.radius * 2f;
            p.body.localPosition = new Vector3(0f, p.feetY + hop, 0f);
            p.body.localScale = new Vector3(size * (1f + breath), size * (1f - breath * 0.5f), 1f);
            p.sprite.color = new Color(1f, 1f, 1f, alpha);
            glow.a *= alpha;
            p.glow.color = glow;
            p.shadow.transform.localPosition = new Vector3(0f, p.feetY + 0.04f, 0f);
            p.shadow.transform.localScale = new Vector3(p.radius * 2.6f, p.radius * 0.95f, 1f);
            p.shadow.color = new Color(0f, 0f, 0f, 0.45f * Mathf.Clamp01(alpha * 1.5f));
            p.marker.transform.localPosition = new Vector3(0f, p.feetY + 0.02f, 0f);
            p.marker.transform.localScale = new Vector3(1.1f, 0.5f, 1f);
            p.weaponPivot.localPosition = new Vector3(0f, hop, 0f);
        }

        /// <summary>The blade at its world angle, gripped at its reach; up the screen it passes behind the body.</summary>
        void Weapon(Puppet p, float angle, float reach, float length, bool visible, float alpha)
        {
            p.weapon.enabled = visible && length > 0.01f;
            if (!p.weapon.enabled) return;
            int order = p.sprite.sortingOrder;
            p.weaponPivot.localRotation = Quaternion.Euler(0f, 0f, angle);
            p.weapon.transform.localPosition = new Vector3(reach, 0f, 0f);
            p.weapon.transform.localScale = new Vector3(length, length, 1f);
            p.weapon.sortingOrder = Mathf.Sin(angle * Mathf.Deg2Rad) > 0.2f ? order - 1 : order + 2;
            p.weapon.flipY = Mathf.Cos(angle * Mathf.Deg2Rad) < 0f;
            p.weapon.color = new Color(1f, 1f, 1f, alpha);
        }

        void Sweep(Dictionary<int, Puppet> map)
        {
            drop.Clear();
            foreach (KeyValuePair<int, Puppet> kv in map)
            {
                if (!kv.Value.seen) drop.Add(kv.Key);
            }
            foreach (int id in drop)
            {
                Object.Destroy(map[id].go);
                map.Remove(id);
            }
        }

        // ── Arrows, bombs, loot ─────────────────────────────────────────────────────────

        void RenderShots(RemoteSession.Snap b, double renderMs, List<object> me, Vector2 mePos)
        {
            int n = b.R != null ? b.R.Count : 0;
            while (shots.Count > n)
            {
                Object.Destroy(shots[shots.Count - 1].go);
                shots.RemoveAt(shots.Count - 1);
            }
            // Arrows fly straight: drawn ahead from the snapshot along their line, never behind.
            float ahead = Mathf.Clamp((float)((renderMs - b.hostMs) / 1000.0), 0f, 0.1f);
            bool meAlive = me != null && MiniJson.B(me, 21);
            for (int i = 0; i < n; i++)
            {
                List<object> r = b.R[i] as List<object>;
                if (r == null) continue;
                if (i >= shots.Count) shots.Add(MakeShot());
                Shot s = shots[i];
                string art = MiniJson.S(r, 6);
                bool arrow = MiniJson.B(r, 5);
                if (art != s.art)
                {
                    s.art = art;
                    Sprite sprite = string.IsNullOrEmpty(art) ? (arrow ? PixelArt.Arrow() : null) : PixelArt.ProjectileSprite(art);
                    s.view.sprite = sprite != null ? sprite : ShapeLibrary.Get(Shape.Arrow);
                }
                var dir = new Vector2(MiniJson.F(r, 2), MiniJson.F(r, 3));
                float speed = MiniJson.F(r, 10, 12f);
                var pos = new Vector2(MiniJson.F(r, 0), MiniJson.F(r, 1)) + dir * speed * ahead;
                float size = MiniJson.F(r, 7, 0.75f);
                s.go.transform.position = pos;
                s.view.transform.localRotation = Quaternion.Euler(0f, 0f, CombatMath.AngleDegrees(dir) + MiniJson.F(r, 8) + MiniJson.F(r, 9) * Time.unscaledTime);
                s.view.transform.localPosition = arrow ? -(Vector3)(dir * size) : Vector3.zero;
                s.view.transform.localScale = new Vector3(size, size, 1f);
                s.view.color = MiniJson.Color(MiniJson.S(r, 11), Color.white);

                // The arrow's own ring, for this duelist when it is coming at them.
                bool reflected = MiniJson.B(r, 4);
                float frames = float.PositiveInfinity;
                if (!reflected && !MiniJson.B(r, 12) && meAlive)
                {
                    Vector2 to = mePos - pos;
                    float along = Vector2.Dot(to, dir);
                    float gap = to.magnitude - RemoteSession.PlayerRadius - 0.2f;
                    if (along > 0f && Mathf.Abs(Vector2.Dot(to, CombatMath.Perpendicular(dir))) < 1.2f) frames = Mathf.Max(0f, gap) / Mathf.Max(0.01f, speed * SimClock.Dt);
                }
                float lead = game.Config.feel.timingCueLeadFrames;
                bool showRing = game.Settings.timingIndicator && frames <= RingApproachFrames + lead;
                s.ring.enabled = showRing;
                s.glow.enabled = showRing || reflected;
                if (reflected)
                {
                    s.glow.color = Pal.playerAccent.WithAlpha(0.5f);
                    s.glow.transform.localScale = new Vector3(0.9f, 0.9f, 1f);
                    continue;
                }
                if (!showRing) continue;
                float k = Mathf.Clamp01(1f - (frames - lead) / RingApproachFrames);
                bool now = frames <= lead;
                float rs = now ? 0.9f : Mathf.Lerp(2.6f, 0.9f, k);
                s.ring.transform.localScale = new Vector3(rs, rs, 1f);
                s.ring.color = now ? Color.white : Pal.playerAccent.WithAlpha(Mathf.Lerp(0.2f, 0.95f, k));
                s.glow.color = (now ? Pal.playerAccent : Color.white).WithAlpha(now ? 0.7f : 0.25f);
                s.glow.transform.localScale = new Vector3(1.4f, 1.4f, 1f);
            }
        }

        Shot MakeShot()
        {
            var go = new GameObject("Projectile");
            go.transform.SetParent(Root, false);
            var s = new Shot
            {
                go = go,
                view = SpriteUtil.Make("Arrow", go.transform, ShapeLibrary.Get(Shape.Arrow), Color.white, Sorting.Overlay - 10),
                ring = SpriteUtil.Make("ArrowRing", go.transform, ShapeLibrary.Get(Shape.Ring), Pal.playerAccent, Sorting.Overlay - 9),
                glow = SpriteUtil.Make("ArrowGlow", go.transform, ShapeLibrary.Get(Shape.SoftCircle), Color.white, Sorting.Overlay - 11),
            };
            s.ring.enabled = s.glow.enabled = false;
            return s;
        }

        void RenderBlasts(RemoteSession.Snap b)
        {
            int n = b.B != null ? b.B.Count : 0;
            while (blasts.Count > n)
            {
                Object.Destroy(blasts[blasts.Count - 1].go);
                blasts.RemoveAt(blasts.Count - 1);
            }
            for (int i = 0; i < n; i++)
            {
                List<object> r = b.B[i] as List<object>;
                if (r == null) continue;
                if (i >= blasts.Count)
                {
                    var go = new GameObject("Blast");
                    go.transform.SetParent(Root, false);
                    blasts.Add(new Blast
                    {
                        go = go,
                        fill = SpriteUtil.Make("Fill", go.transform, ShapeLibrary.Get(Shape.SoftCircle), Color.white, Sorting.Ground + 3),
                        ring = SpriteUtil.Make("Ring", go.transform, ShapeLibrary.Get(Shape.Ring), Color.white, Sorting.Ground + 4),
                        icon = SpriteUtil.Make("Icon", go.transform, null, Color.white, Sorting.Overlay - 12),
                    });
                }
                Blast bl = blasts[i];
                int kind = MiniJson.I(r, 4);
                if (kind != bl.kind)
                {
                    bl.kind = kind;
                    bl.icon.sprite = kind == (int)BlastKind.Bomb ? PixelArt.Image("item_bomb") : kind == (int)BlastKind.Talisman ? PixelArt.Image("item_scroll_fire") : null;
                }
                float radius = MiniJson.F(r, 2), t = MiniJson.F(r, 3);
                Color c = MiniJson.Color(MiniJson.S(r, 5), Pal.danger);
                bl.go.transform.position = new Vector2(MiniJson.F(r, 0), MiniJson.F(r, 1));
                bl.ring.transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);
                float s = radius * 2f * Mathf.Lerp(0.15f, 1f, t);
                bl.fill.transform.localScale = new Vector3(s, s, 1f);
                bool late = t > 0.66f;
                float flicker = late && Mathf.Repeat(Time.unscaledTime * 12f, 1f) < 0.5f ? 1f : 0.6f;
                bl.fill.color = c.WithAlpha((0.18f + 0.3f * t) * flicker);
                bl.ring.color = Color.Lerp(c, Color.white, late ? 0.5f * flicker : 0f).WithAlpha(0.5f + 0.5f * t);
                if (bl.icon.sprite != null)
                {
                    bool bomb = kind == (int)BlastKind.Bomb;
                    float bob = bomb ? 0.12f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 10f)) : 0f;
                    bl.icon.transform.localPosition = new Vector3(0f, 0.15f + bob, 0f);
                    bl.icon.transform.localScale = Vector3.one * (bomb ? 1.1f : 0.9f) * (1f + (late ? 0.15f * flicker : 0f));
                }
            }
        }

        void RenderLoot(RemoteSession.Snap b)
        {
            int n = b.I != null ? b.I.Count : 0;
            while (loot.Count > n)
            {
                Object.Destroy(loot[loot.Count - 1].go);
                loot.RemoveAt(loot.Count - 1);
            }
            for (int i = 0; i < n; i++)
            {
                List<object> r = b.I[i] as List<object>;
                if (r == null) continue;
                if (i >= loot.Count) loot.Add(MakeLoot());
                Loot l = loot[i];
                var rarity = (Rarity)MiniJson.I(r, 3);
                var slot = (ItemSlot)MiniJson.I(r, 4);
                var element = (Element)MiniJson.I(r, 5);
                string k = (int)rarity + ":" + (int)slot + ":" + (int)element;
                if (k != l.key)
                {
                    l.key = k;
                    Color rc = Rarities.Color(rarity);
                    int tier = (int)rarity;
                    l.icon.sprite = PixelArt.ItemIcon(slot, element, rarity);
                    l.glow.color = rc.WithAlpha(0.35f);
                    l.beam.color = rc.WithAlpha(0.35f);
                    l.halo.color = rc.WithAlpha(0.8f);
                    float beamHeight = 1.2f + 0.45f * tier;
                    l.beam.transform.localPosition = new Vector3(0f, beamHeight * 0.5f, 0f);
                    l.beam.transform.localScale = new Vector3(0.1f + 0.015f * tier, beamHeight, 1f);
                    l.glow.transform.localScale = Vector3.one * (1.2f + 0.15f * tier);
                    l.icon.transform.localScale = Vector3.one * 0.75f;
                }
                var pos = new Vector2(MiniJson.F(r, 0), MiniJson.F(r, 1));
                l.go.transform.position = pos;
                float t = Time.unscaledTime * 2f + pos.x;
                l.icon.transform.localPosition = new Vector3(0f, 0.15f + 0.08f * Mathf.Sin(t), 0f);
                l.halo.transform.localPosition = l.icon.transform.localPosition;
                float halo = 0.85f + 0.12f * Mathf.Sin(t * 2.3f);
                l.halo.transform.localScale = new Vector3(halo, halo, 1f);
                Color bc = l.beam.color;
                bc.a = 0.25f + 0.15f * Mathf.Sin(t * 1.7f);
                l.beam.color = bc;
            }
        }

        Loot MakeLoot()
        {
            var go = new GameObject("Loot");
            go.transform.SetParent(Root, false);
            return new Loot
            {
                go = go,
                glow = SpriteUtil.Make("Glow", go.transform, ShapeLibrary.Get(Shape.SoftCircle), Color.white, Sorting.Shadow + 4),
                beam = SpriteUtil.Make("Beam", go.transform, ShapeLibrary.Get(Shape.Pixel), Color.white, Sorting.Overlay - 20),
                halo = SpriteUtil.Make("Halo", go.transform, ShapeLibrary.Get(Shape.SoftCircle), Color.white, Sorting.Overlay - 20),
                icon = SpriteUtil.Make("Icon", go.transform, null, Color.white, Sorting.Overlay - 19),
            };
        }

        // ── Zones, props, offerings (rebuilt when they change) ──────────────────────────

        string KeyOf(List<object> list, int fields)
        {
            key.Clear();
            if (list == null) return string.Empty;
            foreach (object o in list)
            {
                if (!(o is List<object> r)) continue;
                for (int i = 0; i < fields && i < r.Count; i++) key.Append(r[i] is double d ? d.ToString("0.#") : r[i]?.ToString()).Append(',');
                key.Append('|');
            }
            return key.ToString();
        }

        void RenderZones(RemoteSession.Snap b)
        {
            string k = KeyOf(b.Z, 4);
            if (k == zoneKey) return;
            zoneKey = k;
            DestroyAll(zones);
            if (b.Z == null) return;
            foreach (object o in b.Z)
            {
                if (!(o is List<object> r)) continue;
                var kind = (ZoneKind)MiniJson.I(r, 0);
                Color c = World.ZoneColor(kind);
                float a = kind == ZoneKind.Mist ? 0.28f : kind == ZoneKind.BloodPool ? 0.75f : kind == ZoneKind.ThinIce ? 0.35f : 0.4f;
                int order = kind == ZoneKind.Spores || kind == ZoneKind.Mist ? Sorting.Overlay - 30 : Sorting.Ground + 1;
                SpriteRenderer view = SpriteUtil.Make("Zone", Root, ShapeLibrary.Get(Shape.SoftCircle), c.WithAlpha(a), order);
                view.transform.position = new Vector2(MiniJson.F(r, 1), MiniJson.F(r, 2));
                view.transform.localScale = Vector3.one * MiniJson.F(r, 3) * 2.4f;
                zones.Add(view.gameObject);
            }
        }

        void RenderProps(RemoteSession.Snap b)
        {
            string k = KeyOf(b.PR, 4);
            if (k == propKey) return;
            propKey = k;
            DestroyAll(props);
            if (b.PR == null) return;
            foreach (object o in b.PR)
            {
                if (!(o is List<object> r)) continue;
                var kind = (PropKind)MiniJson.I(r, 0);
                var pos = new Vector2(MiniJson.F(r, 1), MiniJson.F(r, 2));
                Sprite s;
                Color c = Color.white;
                int order = -Mathf.RoundToInt(pos.y * 20f);
                Vector3 offset = Vector3.zero;
                switch (kind)
                {
                    case PropKind.Bamboo:
                        s = PropArt.Bamboo();
                        offset = new Vector3(0f, -0.5f, 0f);
                        break;
                    case PropKind.Shoji:
                        s = PropArt.Shoji();
                        offset = new Vector3(0f, -0.5f, 0f);
                        break;
                    case PropKind.Core:
                        s = PixelArt.Region("tiles_dungeon", 2, 2, 1, 1, new Vector2(0.5f, 0f));
                        c = ColorUtil.Hex("#D9A6FF");
                        offset = new Vector3(0f, -0.4f, 0f);
                        break;
                    case PropKind.FallenBamboo:
                        s = PropArt.FallenBamboo();
                        order = Sorting.Ground + 3;
                        break;
                    default:
                        s = PropArt.Pit();
                        order = Sorting.Ground;
                        break;
                }
                if (s == null) continue;
                SpriteRenderer view = SpriteUtil.Make(kind.ToString(), Root, s, c, order);
                view.transform.position = (Vector3)pos + offset;
                if (kind == PropKind.FallenBamboo) view.transform.rotation = Quaternion.Euler(0f, 0f, MiniJson.F(r, 3));
                props.Add(view.gameObject);
            }
        }

        void RenderOfferings(RemoteSession.Snap b)
        {
            string k = KeyOf(b.O, 8);
            if (k == offeringKey) return;
            offeringKey = k;
            DestroyAll(offerings);
            if (b.O == null) return;
            foreach (object o in b.O)
            {
                if (!(o is List<object> r)) continue;
                var pos = new Vector2(MiniJson.F(r, 0), MiniJson.F(r, 1));
                bool exit = MiniJson.B(r, 13);
                Color color = MiniJson.Color(MiniJson.S(r, 14), Pal.gold);
                var go = new GameObject("Offering");
                go.transform.SetParent(Root, false);
                go.transform.position = pos;
                int order = -Mathf.RoundToInt(pos.y * 20f);
                var ring = SpriteUtil.Make("Ring", go.transform, ShapeLibrary.Get(Shape.Ring), color.WithAlpha(0.45f), Sorting.Ground + 2);
                ring.transform.localScale = Vector3.one * (exit ? 3.2f : 1.6f);
                World.DressOffering(go.transform, MiniJson.S(r, 2), new RectInt(MiniJson.I(r, 3), MiniJson.I(r, 4), MiniJson.I(r, 5), MiniJson.I(r, 6)),
                                    MiniJson.S(r, 7), exit, order);
                offerings.Add(go);
            }
        }

        // ── Interpolation helpers ───────────────────────────────────────────────────────

        static float Lerp(List<object> a, List<object> b, int i, float t)
        {
            float vb = MiniJson.F(b, i);
            return a == null ? vb : Mathf.LerpUnclamped(MiniJson.F(a, i), vb, t);
        }

        static Vector2 Lerp2(List<object> a, List<object> b, int ix, int iy, float t)
        {
            var vb = new Vector2(MiniJson.F(b, ix), MiniJson.F(b, iy));
            if (a == null) return vb;
            var va = new Vector2(MiniJson.F(a, ix), MiniJson.F(a, iy));
            // A teleport (blink, respawn): no smear across the arena.
            if ((vb - va).sqrMagnitude > 9f) return vb;
            return Vector2.LerpUnclamped(va, vb, t);
        }

        static float LerpAngle(List<object> a, List<object> b, int i, float t)
        {
            float vb = MiniJson.F(b, i);
            if (a == null) return vb;
            float va = MiniJson.F(a, i);
            return va + Mathf.DeltaAngle(va, vb) * t;
        }
    }
}
