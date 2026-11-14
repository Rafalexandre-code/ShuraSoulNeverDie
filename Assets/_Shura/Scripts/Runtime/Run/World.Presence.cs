using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shura
{
    /// <summary>
    /// Asynchronous presence in the arena (§9.5): friends' ink messages and bloodstains at the
    /// same depth, the ghost of their last five seconds, a friend's spirit fighting at your side
    /// once a run, and a friend who became the new Ashura waiting at the end.
    /// </summary>
    public sealed partial class World
    {
        sealed class PlacedMessage
        {
            public InkMessage msg;
            public Vector2 at;
            public SpriteRenderer view;
            public bool shown;
        }

        sealed class PlacedStain
        {
            public DeathStain stain;
            public Vector2 at;
            public SpriteRenderer view;
            public SpriteRenderer ghost;
            public int ghostFrame = -1;
        }

        readonly List<PlacedMessage> messages = new List<PlacedMessage>();
        readonly List<PlacedStain> stains = new List<PlacedStain>();
        readonly List<Vector2> hostTrail = new List<Vector2>();
        Transform presenceRoot;
        int trailTimer;
        bool hostInteractWas;

        /// <summary>The host's last five seconds of steps (for a bloodstain).</summary>
        public IList<Vector2> HostTrail => hostTrail;

        /// <summary>The friend's spirit fighting in this room, if any.</summary>
        public Enemy FriendSpirit { get; private set; }

        public int PresenceMessagesInRoom => messages.Count;
        public int PresenceStainsInRoom => stains.Count;

        bool PresenceAllowed(RoomDef room) =>
            Run != null && Run.Meta != null && room != null && !room.dojo && !TutorialRoom && Run.Mode != RunMode.Tower;

        void BeginPresence(RoomDef room)
        {
            messages.Clear();
            stains.Clear();
            hostTrail.Clear();
            FriendSpirit = null;
            trailTimer = 0;
            presenceRoot = null;
            if (!PresenceAllowed(room) || arenaRoot == null) return;
            presenceRoot = new GameObject("Presence").transform;
            presenceRoot.SetParent(arenaRoot, false);

            int salt = 0;
            foreach (InkMessage m in Presence.MessagesAt(Run.Meta, room.depth))
            {
                Vector2 at = Layout.NearestFloor(new Vector2(m.x, m.y), 0.4f);
                var view = SpriteUtil.Make("Ink", presenceRoot, ShapeLibrary.Get(Shape.Diamond), new Color(0.08f, 0.06f, 0.1f, 0.75f), Sorting.Ground + 5);
                view.transform.position = at;
                view.transform.localScale = new Vector3(0.7f, 0.42f, 1f);
                messages.Add(new PlacedMessage { msg = m, at = at, view = view });
                salt++;
            }
            foreach (DeathStain s in Presence.StainsAt(Run.Meta, room.depth, Host?.DisplayName))
            {
                Vector2 at = Layout.NearestFloor(new Vector2(s.x, s.y), 0.4f);
                var view = SpriteUtil.Make("Bloodstain", presenceRoot, ShapeLibrary.Get(Shape.SoftCircle), new Color(0.55f, 0.04f, 0.08f, 0.8f), Sorting.Ground + 4);
                view.transform.position = at;
                view.transform.localScale = new Vector3(1.1f, 0.6f, 1f);
                stains.Add(new PlacedStain { stain = s, at = at, view = view });
            }
            if (messages.Count + stains.Count > 0) HintOnce(HintId.Presence);
            TrySpawnFriendSpirit(room);
        }

        void TickPresence()
        {
            PlayerController h = Host;
            if (h == null || presenceRoot == null) return;
            if (h.IsAlive && ++trailTimer >= Presence.TrailEvery)
            {
                trailTimer = 0;
                hostTrail.Add(h.Position);
                if (hostTrail.Count > Presence.TrailPoints) hostTrail.RemoveAt(0);
            }
            bool interact = h.IsAlive && h.Input != null && h.Input.InteractHeld;
            bool pressed = interact && !hostInteractWas;
            hostInteractWas = interact;

            foreach (PlacedMessage pm in messages)
            {
                bool near = h.IsAlive && Vector2.Distance(h.Position, pm.at) < 1.3f;
                if (near && !pm.shown)
                {
                    pm.shown = true;
                    Fx.Popup("“" + pm.msg.Text + "” — " + pm.msg.author, pm.at + Vector2.up * 1.1f, new Color(0.92f, 0.88f, 0.78f));
                }
                if (!near) pm.shown = false;
                if (near && pressed && FallenNear(h) == null && !Presence.Rated(Run.Meta, pm.msg))
                {
                    Presence.Rate(Run.Meta, pm.msg);
                    Fx.Popup(Strings.PopupInkApproved, pm.at + Vector2.up * 1.5f, Config.palette.gold);
                    pm.view.color = new Color(0.55f, 0.42f, 0.12f, 0.85f);
                    pressed = false;
                }
            }
            foreach (PlacedStain ps in stains)
            {
                bool near = h.IsAlive && Vector2.Distance(h.Position, ps.at) < 1.1f;
                if (near && pressed && ps.ghostFrame < 0 && FallenNear(h) == null)
                {
                    pressed = false;
                    StartGhost(ps);
                }
                TickGhost(ps);
            }
        }

        // ── The ghost of a fall ──────────────────────────────────────────────────────

        void StartGhost(PlacedStain ps)
        {
            int art = Mathf.Clamp(ps.stain.art, 0, PlayerController.PartyArt.Length - 1);
            CharacterArt a = PixelArt.Character(PlayerController.PartyArt[art]);
            if (a == null || ps.stain.trail.Count < 2) return;
            ps.ghost = SpriteUtil.Make("Ghost", presenceRoot, a.walkWhite[(int)ArtDir.Down][0], new Color(0.75f, 0.85f, 1f, 0.55f), Sorting.Overlay - 20);
            ps.ghostFrame = 0;
            Fx.Popup(string.Format(Strings.PopupGhost, ps.stain.author, Presence.Phrase(0, Presence.WordFor(ps.stain.cause))), ps.at + Vector2.up * 1.3f, new Color(0.75f, 0.85f, 1f));
        }

        void TickGhost(PlacedStain ps)
        {
            if (ps.ghost == null || ps.ghostFrame < 0) return;
            List<float> t = ps.stain.trail;
            int points = t.Count / 2;
            float step = ps.ghostFrame / (float)Presence.TrailEvery;
            int i = Mathf.Min(points - 1, Mathf.FloorToInt(step));
            int j = Mathf.Min(points - 1, i + 1);
            float k = Mathf.Clamp01(step - i);
            var from = new Vector2(t[i * 2], t[i * 2 + 1]);
            var to = new Vector2(t[j * 2], t[j * 2 + 1]);
            Vector2 pos = Vector2.Lerp(from, to, k);
            int art = Mathf.Clamp(ps.stain.art, 0, PlayerController.PartyArt.Length - 1);
            CharacterArt a = PixelArt.Character(PlayerController.PartyArt[art]);
            Vector2 d = to - from;
            ArtDir dir = d.sqrMagnitude < 1e-4f ? ArtDir.Down : Mathf.Abs(d.x) > Mathf.Abs(d.y) ? (d.x > 0 ? ArtDir.Right : ArtDir.Left) : (d.y > 0 ? ArtDir.Up : ArtDir.Down);
            Sprite[] walk = a.walkWhite[(int)dir];
            bool done = i >= points - 1;
            ps.ghost.sprite = done ? (a.deadWhite ?? walk[0]) : walk[(ps.ghostFrame / 6) % walk.Length];
            ps.ghost.transform.position = pos;
            float fade = done ? Mathf.Clamp01(1f - (ps.ghostFrame - (points - 1) * Presence.TrailEvery) / 60f) : 1f;
            ps.ghost.color = new Color(0.75f, 0.85f, 1f, 0.55f * fade);
            ps.ghostFrame++;
            if (fade <= 0f)
            {
                Object.Destroy(ps.ghost.gameObject);
                ps.ghost = null;
                ps.ghostFrame = -1;
            }
        }

        // ── A friend's spirit ────────────────────────────────────────────────────────

        void TrySpawnFriendSpirit(RoomDef room)
        {
            if (Run.SpiritCame || room.kind != RoomKind.Combat || room.depth < 3) return;
            ulong h = RoomGenerator.Mix(Run.Seed, room.depth + 7700);
            if (h % 100UL >= (ulong)(Presence.SpiritChance * 100f)) return;
            SpiritProfile s = Presence.SpiritFor(Run.Meta, h / 100UL);
            if (s == null) return;
            Run.SpiritCame = true;
            EnemyDef def = SpiritDef(s);
            Vector2 at = Layout.NearestFloor(PlayerSpawn + Vector2.up * 1.5f, def.radius);
            Enemy ally = CreateEnemy(def, at, false, null);
            ally.IsAlly = true;
            FriendSpirit = ally;
            Fx.Toast(string.Format(Strings.ToastFriendSpirit, s.name, s.aggression > 0.6f ? Strings.SpiritRelentless : s.aggression < 0.4f ? Strings.SpiritPatient : Strings.SpiritBalanced),
                     new Color(0.75f, 0.85f, 1f));
        }

        /// <summary>The spirit fights with the friend's blade and pace (the patient wait longer between blows).</summary>
        public static EnemyDef SpiritDef(SpiritProfile s)
        {
            EnemyDef def = EnemyDefs.InnerAsura(WeaponDefs.Get(s.weaponA), s.art, s.name).Clone();
            def.id = "espirito_amigo";
            def.displayName = string.Format(Strings.SpiritName, s.name);
            def.color = new Color(0.6f, 0.75f, 1f);
            def.tint = new Color(0.8f, 0.9f, 1f, 0.8f);
            def.lifesteal = 0f;
            def.maxHealth = 140f;
            def.idleMin = Mathf.RoundToInt(Mathf.Lerp(60f, 20f, Mathf.Clamp01(s.aggression)));
            def.idleMax = def.idleMin + 30;
            def.lore = "Um amigo que lutou estas salas antes de você.";
            return def;
        }

        // ── The new Ashura ───────────────────────────────────────────────────────────

        /// <summary>A friend became the Ashura this week: the King wears their name and fights with their blade too.</summary>
        void ApplyNewAshura(Enemy e, BossDef lord)
        {
            if (lord == null || lord.kind != BossKind.Ashura || Run?.Meta == null) return;
            AshuraMark mark = Presence.NewAshura(Run.Meta, DateTime.UtcNow);
            if (mark == null) return;
            EnemyDef combo = EnemyDefs.InnerAsura(WeaponDefs.Get(mark.weapon), mark.art, mark.name);
            e.AddMoves(combo.moves);
            e.BossNameOverride = string.Format(Strings.NewAshuraName, mark.name);
            Fx.Toast(string.Format(Strings.ToastNewAshura, mark.name), Config.palette.danger);
        }

        /// <summary>The Shura's mirror: a friend's blade when one has fought beside you, else your own.</summary>
        public void MirrorFor(Enemy mirror, PlayerController target)
        {
            SpiritProfile s = Run?.Meta != null ? Presence.SpiritFor(Run.Meta, Run.Seed + (ulong)Depth) : null;
            if (s != null)
            {
                EnemyDef copy = EnemyDefs.InnerAsura(WeaponDefs.Get(s.weaponA), s.art, s.name);
                mirror.MirrorMoves(copy);
                Fx.Popup(string.Format(Strings.PopupMirrorFriend, s.name), mirror.Position + Vector2.up * 1.4f, new Color(0.75f, 0.85f, 1f));
                return;
            }
            mirror.MakeMirror(target);
        }
    }
}
