using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shura
{
    /// <summary>A dropped item waiting on the floor, under a beam of its rarity's colour.</summary>
    public sealed class ItemPickup
    {
        public Item Item;
        public Vector2 Position;
        public float Age;
        public Transform Root;
        public SpriteRenderer Beam;
        public SpriteRenderer Glow;
        public SpriteRenderer Icon;
        public SpriteRenderer Frame;
    }

    /// <summary>An arrow in flight. A perfect parry turns it around.</summary>
    public sealed class Projectile
    {
        public Vector2 Position;
        public Vector2 PrevPosition;
        public Vector2 Direction;
        public float Speed;
        public float Radius;
        public AttackDef Attack;
        public Enemy Owner;
        public bool Signature;
        public bool Reflected;
        public bool PassedPlayer;
        public bool Cued;
        public float Damage;
        public float Posture;
        public int Life;
        public SpriteRenderer View;
        public SpriteRenderer Ring;
        public SpriteRenderer Glow;
        public float Size;
        /// <summary>Arrows are drawn from the tail; waves are centred.</summary>
        public bool IsArrow = true;
        /// <summary>Extra rotation of the art (the pack draws arrows on the diagonal).</summary>
        public float ArtAngle;
        /// <summary>Passes through every enemy instead of stopping at the first.</summary>
        public bool Piercing;
        /// <summary>A shuriken: catches leaps, charges and dives before they land.</summary>
        public bool Interrupts;
        /// <summary>Sprite name for browser players (empty = arrow).</summary>
        public string Art;
        /// <summary>The element it carries onto whoever it cuts (wind crescents).</summary>
        public Element Element;
        /// <summary>Degrees per second the art turns (shuriken).</summary>
        public float Spin;
        public readonly HashSet<Enemy> Struck = new HashSet<Enemy>();

        /// <summary>Simulation frames until it reaches the player (infinite if not heading there).</summary>
        public float FramesToImpact(PlayerController player)
        {
            Vector2 to = player.Position - Position;
            float along = Vector2.Dot(to, Direction);
            if (along <= 0f) return float.PositiveInfinity;
            float gap = to.magnitude - player.Radius - Radius;
            return Mathf.Max(0f, gap) / Mathf.Max(0.01f, Speed * SimClock.Dt);
        }
    }

    /// <summary>A marked circle on the floor that explodes when its fuse runs out (bombs, talismans).</summary>
    public sealed class EnemyBlast
    {
        public Vector2 Position;
        public float Radius;
        public int Fuse;
        public int Age;
        public float Damage;
        public float Posture;
        public BlastKind Kind;
        public bool HitsEnemies;
        public Enemy Owner;
        /// <summary>Released by a signature move: dodging through it can master the thrower.</summary>
        public bool Signature;
        public Color Color;
        public Transform Root;
        public SpriteRenderer Fill;
        public SpriteRenderer Ring;
        public SpriteRenderer Icon;
        public float Progress => Fuse > 0 ? Mathf.Clamp01(Age / (float)Fuse) : 1f;
    }

    /// <summary>
    /// Owns the arena, the fighters, projectiles and the room/wave flow, and resolves every
    /// hit. Tick order is fixed (player, enemies, projectiles, effects, collisions, waves)
    /// so runs are reproducible from their seed.
    /// </summary>
    public sealed partial class World
    {
        struct Burst
        {
            public Vector2 position;
            public int framesLeft;
            public float radius;
            public float damage;
            public float posture;
            public Color color;
            public bool explosion;
            /// <summary>A firecracker: beasts and Onis caught in it are stunned this long (others a third).</summary>
            public int stunFrames;
        }

        const float PickupRadius = 0.9f;
        const float CritMult = 1.8f;

        const float ArrowLength = 0.75f;
        const int ProjectileLifeFrames = 240;
        const float ReflectSpeedMult = 1.5f;
        const int RingApproachFrames = 30;

        public readonly GameConfig Config;
        public readonly IFeedback Fx;
        public readonly AttackCoordinator Coordinator;
        public readonly List<Enemy> Enemies = new List<Enemy>();
        public readonly List<Projectile> Projectiles = new List<Projectile>();
        public readonly List<ItemPickup> Pickups = new List<ItemPickup>();
        public readonly List<EnemyBlast> Blasts = new List<EnemyBlast>();

        readonly Transform root;
        Transform arenaRoot;
        readonly List<Burst> bursts = new List<Burst>();
        int adaptationTimer;
        readonly HashSet<HintId> hintsShown = new HashSet<HintId>();
        int waveDelay;
        bool roomActive;
        bool deathReported;

        /// <summary>Everyone in the duel: the host first, then players who joined over the network.</summary>
        public readonly List<PlayerController> Players = new List<PlayerController>();
        /// <summary>The player at this computer (camera, HUD, equipment screen).</summary>
        public PlayerController Host { get; private set; }
        PlayerController actor;

        /// <summary>
        /// The player acting right now: while a player ticks, strikes or is struck, rewards and
        /// effects that ask for "the player" get that one; otherwise the host.
        /// </summary>
        public PlayerController Player => actor != null ? actor : Host;
        public bool ActorIsRemote => actor != null && actor != Host;
        /// <summary>Who acts, as the network names them: -1 nobody (the world), 0 the host, otherwise the guest's id.</summary>
        public int ActorNetId => actor == null ? -1 : actor == Host ? 0 : actor.NetId;
        public int PartySize => Players.Count;

        public ArenaLayout Layout { get; private set; }
        /// <summary>The arena image as PNG for browser players (only built while hosting).</summary>
        public byte[] ArenaPng { get; private set; }
        public int ArenaVersion { get; private set; }
        public bool EncodeArenaPng { get; set; }
        public int TrapTick { get; private set; }
        readonly Dictionary<PlayerController, float> reviveProgress = new Dictionary<PlayerController, float>();
        readonly Dictionary<object, int> trapCooldown = new Dictionary<object, int>();
        readonly Dictionary<object, int> trapActivation = new Dictionary<object, int>();
        TrapViews trapViews;
        public const float ReviveSeconds = 2.4f;
        public const float ReviveRange = 1.4f;

        public ArenaDecor Decor { get; private set; }
        public RunState Run { get; private set; }
        public Rect Bounds { get; private set; }
        public RoomDef CurrentRoom { get; private set; }
        public int WaveIndex { get; private set; }
        public bool RoomCleared { get; private set; }
        public bool IsLastWave => CurrentRoom != null && WaveIndex >= CurrentRoom.waves.Length - 1;
        public bool PlayerInvincible { get; set; }
        /// <summary>What the enemies currently know about the player, scaled by depth.</summary>
        public AdaptiveTraits Adaptation { get; private set; }
        public int Depth => CurrentRoom != null ? CurrentRoom.depth : 0;
        /// <summary>Accessibility/readability option: the shrinking "press now" ring.</summary>
        public bool ShowTimingIndicator { get; set; } = true;

        public event Action RoomClearedEvent;
        /// <summary>A relic asks the game to slow time (seconds).</summary>
        public event Action<float> SlowMoRequested;
        /// <summary>The host's own weapon mastery went up (shown only on the host's screen).</summary>
        public event Action<WeaponDef, int> MasteryRaised;
        public void RaiseMastery(WeaponDef weapon, int level) => MasteryRaised?.Invoke(weapon, level);

        public void RequestSlowMo(float seconds) => SlowMoRequested?.Invoke(seconds);
        public event Action PlayerDiedEvent;

        public World(GameConfig config, IFeedback fx)
        {
            Config = config;
            Fx = fx;
            Standoff = new Standoff(this);
            Coordinator = new AttackCoordinator(config.enemies.attackGapFrames);
            root = new GameObject("World").transform;
            RebuildArena(RoomGenerator.Intro());

            Host = CreatePlayer("Player");
            Host.Teleport(PlayerSpawn);
        }

        PlayerController CreatePlayer(string name)
        {
            var playerObject = new GameObject(name);
            playerObject.transform.SetParent(root, false);
            var p = playerObject.AddComponent<PlayerController>();
            p.Init(this, Config);
            Players.Add(p);
            return p;
        }

        /// <summary>A player joining over the network. Mid-run they drop in next to the party.</summary>
        public PlayerController AddPlayer(string name, IPlayerInputSource input, WeaponDef a, WeaponDef b, int artIndex)
        {
            PlayerController p = CreatePlayer("Remote_" + name);
            p.DisplayName = name;
            p.SetArt(artIndex);
            p.SetLoadout(a, b);
            p.Input = input;
            if (Run != null)
            {
                // Joining mid-run: a fresh build of their own (ascension and meta still apply).
                p.BeginRun(Run, input, Run.CreateBuild());
                p.ApplyStats(true);
                Vector2 near = Host.IsAlive ? Host.Position : PlayerSpawn;
                p.Teleport(Layout.NearestFloor(near + Vector2.down * 1.2f, p.Radius));
            }
            else
            {
                p.Teleport(PartySpawn(Players.Count - 1));
            }
            return p;
        }

        public void RemovePlayer(PlayerController p)
        {
            if (p == null || p == Host) return;
            Players.Remove(p);
            reviveProgress.Remove(p);
            foreach (Enemy e in Enemies)
            {
                if (e.Target == p) e.Target = null;
            }
            Object.Destroy(p.gameObject);
        }

        public bool AnyPlayerAlive
        {
            get
            {
                foreach (PlayerController p in Players)
                {
                    if (p.IsAlive) return true;
                }
                return false;
            }
        }

        /// <summary>Who the camera watches: the host, or a living teammate while the host is down.</summary>
        public PlayerController CameraFocus
        {
            get
            {
                if (Host.IsAlive) return Host;
                foreach (PlayerController p in Players)
                {
                    if (p.IsAlive) return p;
                }
                return Host;
            }
        }

        public PlayerController NearestAlivePlayer(Vector2 from)
        {
            PlayerController best = null;
            float bestD = float.MaxValue;
            foreach (PlayerController p in Players)
            {
                if (!p.IsAlive) continue;
                float d = (p.Position - from).sqrMagnitude;
                if (d < bestD)
                {
                    bestD = d;
                    best = p;
                }
            }
            return best;
        }

        /// <summary>
        /// Picks who an enemy duels: close players, but spread out so nobody faces the whole
        /// wave alone while a teammate idles.
        /// </summary>
        public PlayerController ChooseTarget(Enemy enemy)
        {
            if (enemy.AsuraOwner != null && enemy.AsuraOwner.IsAlive) return enemy.AsuraOwner;
            PlayerController best = null;
            float bestScore = float.MaxValue;
            foreach (PlayerController p in Players)
            {
                if (!p.IsAlive) continue;
                int load = 0;
                foreach (Enemy e in Enemies)
                {
                    if (e != enemy && !e.IsGone && e.Target == p) load++;
                }
                float score = Vector2.Distance(p.Position, enemy.Position) + load * 4f;
                if (p.Exhausted && Adaptation.Pressure > AdaptiveTraits.ActiveThreshold) score -= 4f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = p;
                }
            }
            return best ?? Host;
        }

        public Vector2 PlayerSpawn => Layout.NearestFloor(new Vector2(Layout.FloorBounds.xMin + 3f, 0f), 0.5f);

        /// <summary>The party lines up in a loose column at the left of the arena.</summary>
        public Vector2 PartySpawn(int index)
        {
            if (index == 0) return PlayerSpawn;
            int col = index / 4;
            int row = index % 4;
            float y = (row - 1.5f) * 1.6f;
            var target = new Vector2(Layout.FloorBounds.xMin + 2f + col * 1.6f, y);
            return Layout.NearestFloor(target, 0.5f);
        }

        public int ActiveEnemyCount
        {
            get
            {
                int count = pendingSpawns.Count;
                for (int i = 0; i < Enemies.Count; i++)
                {
                    if (!Enemies[i].IsGone && !Enemies[i].IsAlly) count++;
                }
                return count;
            }
        }

        // ── Run / room flow ─────────────────────────────────────────────────────────────

        public void BeginRun(RunState run, IPlayerInputSource input)
        {
            ClearEnemies();
            Run = run;
            hintsShown.Clear();
            deathReported = false;
            // Every duelist gets their own build: the host the run's main one, teammates a new one.
            foreach (PlayerController p in Players) p.BeginRun(run, p == Host ? input : p.Input, p == Host ? run.Main : run.CreateBuild());
        }

        public void EndRun()
        {
            ClearEnemies();
            ClearEncounter();
            ClearConditions();
            roomActive = false;
        }

        public void StartRoom(RoomDef room, bool firstRoom)
        {
            ClearEnemies();
            // A bigger party gets a bigger arena and more enemies per wave.
            room = PartyScaling.Scale(room, PartySize);
            TutorialRoom = false;
            CurrentRoom = room;
            RebuildArena(room);
            ApplyConditions(room);
            ResetChallenge(room);
            if (room.IsEncounter) SetupEncounter(room);
            else ClearEncounter();
            Coordinator.MinGapFrames = PartyScaling.AttackGap(DifficultyRules.AttackGapFrames(Config.enemies.attackGapFrames, room.depth), PartySize);
            Coordinator.Capacity = PartyScaling.SimultaneousAttackers(PartySize);
            RefreshAdaptation();
            WaveIndex = -1;
            waveDelay = Config.enemies.firstWaveDelay;
            RoomCleared = false;
            roomActive = true;
            TrapTick = 0;
            trapCooldown.Clear();
            trapActivation.Clear();
            reviveProgress.Clear();
            deathReported = false;
            for (int i = 0; i < Players.Count; i++)
            {
                PlayerController p = Players[i];
                // Fallen teammates stand up again at the start of every room.
                if (!p.IsAlive) p.Revive(0.5f);
                p.ResetForRoom(PartySpawn(i));
            }
            if (firstRoom) HintOnce(HintId.Controls);
            if (Run != null && Run.AllyRooms > 0) SpawnAlly();
            if (room.weather != Weather.Clear) HintOnce(HintId.Weather);
            if (room.biome.rule != BiomeRule.Embers || room.depth >= Biomes.RoomsPerBiome) HintOnce(HintId.Biome);
        }

        /// <summary>The wounded ronin, by the party's side for a few rooms.</summary>
        void SpawnAlly()
        {
            Vector2 at = Layout.NearestFloor(PlayerSpawn + Vector2.down * 1.5f, EnemyDefs.Ronin.radius);
            Enemy ally = CreateEnemy(EnemyDefs.Ronin, at, false, null);
            ally.IsAlly = true;
            HintOnce(HintId.Ally);
        }

        public void Tick()
        {
            if (Run == null) return;
            Run.Profile.Tick(SimClock.Dt, ActiveEnemyCount > 0 && Host.IsAlive);
            if (++adaptationTimer >= SimClock.TickRate) RefreshAdaptation();
            Coordinator.Tick();
            for (int i = 0; i < Players.Count; i++)
            {
                actor = Players[i];
                Players[i].Tick();
            }
            actor = null;
            Standoff.Tick();
            for (int i = 0; i < Enemies.Count; i++) Enemies[i].Tick();
            TickProjectiles();
            TickBursts();
            TickBlasts();
            TickTraps();
            TickConditions();
            TickEncounter();
            TickChallenge();
            TickPickups();
            ResolveCollisions();
            RemoveDead();
            FlushDivides();
            FlushSpawns();
            FlushTwins();
            TickRevives();
            TickWaves();
            Run.Elapsed += SimClock.Dt;

            if (!AnyPlayerAlive && !deathReported)
            {
                deathReported = true;
                PlayerDiedEvent?.Invoke();
            }
        }

        /// <summary>
        /// A fallen player stands again when a teammate stands over them holding the revive
        /// button (E / gamepad X) long enough; letting go drains the progress quickly. Several
        /// helpers revive faster.
        /// </summary>
        void TickRevives()
        {
            if (Players.Count < 2) return;
            foreach (PlayerController down in Players)
            {
                if (down.IsAlive)
                {
                    reviveProgress.Remove(down);
                    continue;
                }
                int helpers = 0;
                foreach (PlayerController p in Players)
                {
                    if (p == down || !p.IsAlive || p.Input == null || !p.Input.InteractHeld) continue;
                    if (Vector2.Distance(p.Position, down.Position) <= ReviveRange) helpers++;
                }
                reviveProgress.TryGetValue(down, out float t);
                t = helpers > 0 ? t + SimClock.Dt * (1f + 0.5f * (helpers - 1)) : Mathf.Max(0f, t - SimClock.Dt * 2f);
                if (t <= 0f)
                {
                    reviveProgress.Remove(down);
                    continue;
                }
                if (t >= ReviveSeconds)
                {
                    down.Revive(0.35f);
                    Fx.Emit(FxType.FlowUp, down.Position, Vector2.up, 1f, Config.palette.gold);
                    Fx.Popup(Strings.PopupRevived, down.Position + Vector2.up, Config.palette.gold);
                    reviveProgress.Remove(down);
                    continue;
                }
                reviveProgress[down] = t;
            }
        }

        /// <summary>0..1 progress of a teammate reviving this fallen player.</summary>
        public float ReviveProgress(PlayerController p) => reviveProgress.TryGetValue(p, out float t) ? t / ReviveSeconds : 0f;

        /// <summary>A fallen teammate within reach of this player (for the "hold E" prompt).</summary>
        public PlayerController FallenNear(PlayerController helper)
        {
            if (helper == null || !helper.IsAlive) return null;
            foreach (PlayerController p in Players)
            {
                if (p != helper && !p.IsAlive && Vector2.Distance(p.Position, helper.Position) <= ReviveRange) return p;
            }
            return null;
        }

        void TickWaves()
        {
            if (!roomActive || RoomCleared || !AnyPlayerAlive) return;
            // The dojo: the tutorial brings its own partners and never "clears".
            if (TutorialRoom) return;
            // Encounters end at the torii, not when the (absent) waves run out.
            if (CurrentRoom != null && CurrentRoom.IsEncounter) return;
            if (ActiveEnemyCount > 0) return;
            if (waveDelay > 0)
            {
                waveDelay--;
                return;
            }

            if (WaveIndex + 1 < CurrentRoom.waves.Length)
            {
                WaveIndex++;
                List<Enemy> spawned = SpawnWave(CurrentRoom.waves[WaveIndex]);
                if (WaveIndex == 0 && CurrentRoom.standoff) BeginStandoffWith(spawned);
                waveDelay = Config.enemies.waveDelayFrames;
                return;
            }

            RoomCleared = true;
            roomActive = false;
            Coordinator.Reset();
            ClearProjectiles();
            ClearBlasts();
            CollectAllPickups();
            RewardChallenge();
            RoomClearedEvent?.Invoke();
        }

        List<Enemy> SpawnWave(EnemySpawn[] spawns)
        {
            var list = new List<Enemy>();
            foreach (EnemySpawn spawn in spawns)
            {
                Enemy e = CreateEnemy(EnemyDefs.Get(spawn.id), FindSpawnPoint(), spawn.elite, null);
                ApplyEliteAffixes(e, spawn);
                ConditionSpawn(e, spawn.elite);
                list.Add(e);
                // Kalpa "twin guardians": every Guardian comes with a brother.
                if (spawn.elite && Run.HasRule(KalpaRule.TwinGuardians))
                {
                    Enemy twin = CreateEnemy(EnemyDefs.Get(spawn.id), FindSpawnPoint(), true, null);
                    ApplyEliteAffixes(twin, spawn);
                    list.Add(twin);
                }
            }
            // A dispute: two factions already at each other's throats.
            if (CurrentRoom != null && CurrentRoom.kind == RoomKind.Dispute) StartDispute(list);
            return list;
        }

        /// <summary>The tutorial's dojo: no waves, no clearing; partners are brought in one at a time.</summary>
        public bool TutorialRoom { get; set; }

        /// <summary>Brings one fighter into the dojo (a training partner, or a real foe for the last trial).</summary>
        public Enemy SpawnPartner(EnemyDef def, Vector2 at, bool training)
        {
            Enemy e = CreateEnemy(def, Layout.NearestFloor(at, def.radius), false, null);
            e.Training = training;
            return e;
        }

        Enemy CreateEnemy(EnemyDef def, Vector2 at, bool elite, Enemy summoner)
        {
            var go = new GameObject("Enemy_" + def.id);
            go.transform.SetParent(root, false);
            var enemy = go.AddComponent<Enemy>();
            EnemyScaling scaling = EnemyScaling.For(Depth, elite);
            if (elite && CurrentRoom != null && CurrentRoom.kind == RoomKind.Event) scaling.health *= 0.8f;
            scaling.health *= elite ? PartyScaling.GuardianHealth(PartySize) : PartyScaling.EnemyHealth(PartySize);
            enemy.Init(this, def, at, Config, Run.Rng, scaling, summoner);
            Enemies.Add(enemy);
            return enemy;
        }

        // ── Summons, splits and clones ──────────────────────────────────────────────────

        struct PendingSpawn
        {
            public EnemyDef def;
            public Vector2 at;
            public Enemy summoner;
        }

        /// <summary>
        /// Enemies born mid-fight wait here until the end of the tick, so a death inside any
        /// loop over <see cref="Enemies"/> can safely call new ones.
        /// </summary>
        readonly List<PendingSpawn> pendingSpawns = new List<PendingSpawn>();
        const int MaxEnemiesAlive = 24;

        public int MinionsOf(Enemy owner)
        {
            int n = 0;
            foreach (Enemy e in Enemies)
            {
                if (e.Summoner == owner && !e.IsGone) n++;
            }
            foreach (PendingSpawn p in pendingSpawns)
            {
                if (p.summoner == owner) n++;
            }
            return n;
        }

        /// <summary>Calls minions in a ring around the caster (capped per caster and per arena).</summary>
        public void Summon(Enemy owner, string id, int count)
        {
            if (string.IsNullOrEmpty(id) || !EnemyDefs.TryGet(id, out EnemyDef def)) return;
            int room = Mathf.Min(owner.Def.summonMax - MinionsOf(owner), MaxEnemiesAlive - ActiveEnemyCount);
            int n = Mathf.Min(count, room);
            float start = Run.Rng.Range(0f, 360f);
            for (int i = 0; i < n; i++)
            {
                Vector2 at = owner.Position + CombatMath.FromAngle(start + i * 360f / Mathf.Max(1, n)) * 1.6f;
                pendingSpawns.Add(new PendingSpawn { def = def, at = Layout.NearestFloor(at, def.radius), summoner = owner });
                Fx.Emit(FxType.Smoke, at, Vector2.up, def.radius, owner.Def.color);
            }
            if (n > 0) HintOnce(owner.Def.clones ? HintId.Clones : HintId.Summoner);
        }

        /// <summary>A dying slime becomes several small ones where it fell.</summary>
        public void Split(Enemy from, string id, int count)
        {
            if (!EnemyDefs.TryGet(id, out EnemyDef def)) return;
            int n = Mathf.Min(count, MaxEnemiesAlive - ActiveEnemyCount + 1);
            for (int i = 0; i < n; i++)
            {
                Vector2 at = from.Position + CombatMath.FromAngle(90f + i * 360f / Mathf.Max(1, n)) * 0.7f;
                pendingSpawns.Add(new PendingSpawn { def = def, at = Layout.NearestFloor(at, def.radius) });
            }
            if (n > 0) HintOnce(HintId.Split);
        }

        /// <summary>Shadow clones vanish when the one who cast them falls.</summary>
        public void DispelMinionsOf(Enemy owner)
        {
            foreach (Enemy e in Enemies)
            {
                if (e.Summoner == owner) e.Dispel();
            }
            pendingSpawns.RemoveAll(p => p.summoner == owner);
        }

        void FlushSpawns()
        {
            if (pendingSpawns.Count == 0) return;
            var batch = new List<PendingSpawn>(pendingSpawns);
            pendingSpawns.Clear();
            foreach (PendingSpawn p in batch)
            {
                if (p.summoner != null && p.summoner.IsGone && p.summoner.Def.clones) continue;
                CreateEnemy(p.def, p.at, false, p.summoner);
            }
        }

        /// <summary>A floor point far from every player (sentinels blinking away).</summary>
        public Vector2 FarFloorPoint(Vector2 from, float radius)
        {
            Vector2 best = from;
            float bestScore = float.MinValue;
            for (int attempt = 0; attempt < 20; attempt++)
            {
                Vector2 p = Layout.RandomFloorPoint(Run.Rng, radius + 0.3f);
                PlayerController near = NearestAlivePlayer(p);
                float score = near != null ? Mathf.Min(Vector2.Distance(p, near.Position), 9f) : 0f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = p;
                }
            }
            return best;
        }

        void RefreshAdaptation()
        {
            adaptationTimer = 0;
            float learn = 1f;
            foreach (PlayerController p in Players) learn = Mathf.Max(learn, p.Build != null ? p.Build.Modifiers.adaptMult : 1f);
            Adaptation = Run != null ? Run.Profile.Traits(Mathf.Clamp01(DifficultyRules.AdaptStrength(Depth) * learn)) : default;
        }

        /// <summary>
        /// Each room has its own size, outline, traps and biome; the old arena (and its
        /// texture) is discarded.
        /// </summary>
        // ── Native online guest: the host's arena rebuilt here, nothing simulated ───────

        /// <summary>Builds the host's arena from its seed (same statues, lanterns and traps), with no fighters of its own.</summary>
        public void ShowRemoteArena(float width, float height, ArenaShape shape, int depth, int biome, ulong seed, bool encounter)
        {
            ClearEnemies();
            ClearEncounter();
            ClearConditions();
            roomActive = false;
            var room = new RoomDef("remote", depth, Biomes.All[Mathf.Clamp(biome, 0, Biomes.All.Length - 1)], width, height, false, new EnemySpawn[0][], shape, seed);
            room.kind = encounter ? RoomKind.Shrine : RoomKind.Combat;
            CurrentRoom = room;
            RebuildArena(room);
            foreach (PlayerController p in Players) p.gameObject.SetActive(false);
        }

        /// <summary>The guest's arena animates with the host's trap clock; broken statues and lanterns disappear.</summary>
        public void RenderRemote(int tick, List<int> brokenStatues, List<int> brokenLanterns)
        {
            if (Decor == null) return;
            Decor.Animate(Time.unscaledTime);
            TrapTick = tick;
            trapViews?.Render(tick, Time.unscaledTime);
            if (brokenStatues != null)
            {
                foreach (int i in brokenStatues)
                {
                    if (i < 0 || i >= Decor.StatueViews.Count) continue;
                    foreach (GameObject go in Decor.StatueViews[i]) if (go != null && go.activeSelf) go.SetActive(false);
                }
            }
            if (brokenLanterns != null)
            {
                foreach (int i in brokenLanterns)
                {
                    if (i >= 0 && i < Decor.LanternViews.Count && Decor.LanternViews[i] != null && Decor.LanternViews[i].activeSelf) Decor.LanternViews[i].SetActive(false);
                }
            }
        }

        /// <summary>Leaving a friend's duel: this world's own duelists come back.</summary>
        public void EndRemote()
        {
            foreach (PlayerController p in Players) p.gameObject.SetActive(true);
            CurrentRoom = null;
            RebuildArena(RoomGenerator.Intro());
            Host.Teleport(PlayerSpawn);
        }

        void RebuildArena(RoomDef room)
        {
            if (arenaRoot != null)
            {
                if (Decor != null && Decor.FloorTexture != null) Object.Destroy(Decor.FloorTexture);
                Object.Destroy(arenaRoot.gameObject);
            }
            arenaRoot = new GameObject("Arena").transform;
            arenaRoot.SetParent(root, false);
            int w = Mathf.Max(12, Mathf.RoundToInt(room.width));
            int h = Mathf.Max(8, Mathf.RoundToInt(room.height));
            Layout = new ArenaLayout(w, h, room.shape);
            var rng = new SeededRandom(room.layoutSeed ^ 0x5EEDUL);
            // Encounters are a pause on the road: no traps.
            if (!room.IsEncounter) Layout.PlaceTraps(rng, room.depth, PlayerSpawn);
            Bounds = Layout.FloorBounds;
            Decor = ArenaBuilder.Build(arenaRoot, Config, Layout, room.biome, EncodeArenaPng);
            ArenaPng = Decor.Png;
            ArenaVersion++;
            trapViews = new TrapViews(arenaRoot, Layout);
        }

        /// <summary>
        /// Profiles a parry press: was there a swing coming, and was the press too early to be perfect?
        /// This is how the enemies learn the player's rhythm.
        /// </summary>
        public void ProfileParryPress(int perfectWindowFrames)
        {
            if (Run == null) return;
            Enemy threat = null;
            int soonest = int.MaxValue;
            for (int i = 0; i < Enemies.Count; i++)
            {
                Enemy e = Enemies[i];
                AttackDef a = e.CurrentAttack;
                if (a == null || !a.Parryable || a.IsProjectile) continue;
                if (Vector2.Distance(e.Position, Player.Position) > a.shape.range + a.lunge + 2f) continue;
                int until = e.FramesUntilActive;
                if (until >= -a.active && until < soonest)
                {
                    soonest = until;
                    threat = e;
                }
            }
            if (threat == null || soonest > 45) return;
            Run.Profile.OnParryPress(soonest >= perfectWindowFrames);
        }

        Vector2 FindSpawnPoint()
        {
            Vector2 best = Bounds.center;
            float bestScore = float.MinValue;
            for (int attempt = 0; attempt < 24; attempt++)
            {
                Vector2 p = Layout.RandomFloorPoint(Run.Rng, 0.8f);
                PlayerController nearest = NearestAlivePlayer(p);
                float fromPlayer = nearest != null ? Vector2.Distance(p, nearest.Position) : 99f;
                float fromOthers = 3f;
                for (int i = 0; i < Enemies.Count; i++) fromOthers = Mathf.Min(fromOthers, Vector2.Distance(p, Enemies[i].Position));
                float score = Mathf.Min(fromPlayer / Config.enemies.minSpawnDistance, 1f) * 10f + fromOthers;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = p;
                }
            }
            return best;
        }

        void RemoveDead()
        {
            for (int i = Enemies.Count - 1; i >= 0; i--)
            {
                if (Enemies[i].CurrentState != Enemy.State.Dead) continue;
                Object.Destroy(Enemies[i].gameObject);
                Enemies.RemoveAt(i);
            }
        }

        void ClearEnemies()
        {
            foreach (Enemy e in Enemies)
            {
                if (e != null) Object.Destroy(e.gameObject);
            }
            Enemies.Clear();
            pendingSpawns.Clear();
            pendingDivides.Clear();
            resonance.Clear();
            kiaiTimes.Clear();
            bursts.Clear();
            ClearBlasts();
            ClearProjectiles();
            ClearPickups();
            Coordinator.Reset();
        }

        // ── Combat resolution ───────────────────────────────────────────────────────────

        /// <summary>Called by an enemy on each of its active frames until it connects once.</summary>
        public bool TryHitPlayer(Enemy attacker, AttackDef attack)
        {
            // The ally's blows only ever meet enemies.
            if (attacker.IsAlly) return false;
            // A sweep can catch several duelists at once; each answers it on their own.
            bool connected = false;
            bool groupAnswered = false;
            if (attack.groupStrike)
            {
                int answering = 0;
                foreach (PlayerController q in Players)
                {
                    if (q.IsAlive && q.InPerfectWindow && CombatMath.Overlaps(attack.shape, attacker.Position, attacker.Facing, q.Position, q.Radius)) answering++;
                }
                groupAnswered = answering >= 2;
                HintOnce(HintId.GroupBlow);
            }
            for (int i = 0; i < Players.Count; i++)
            {
                PlayerController p = Players[i];
                if (!p.IsAlive) continue;
                if (!CombatMath.Overlaps(attack.shape, attacker.Position, attacker.Facing, p.Position, p.Radius)) continue;
                var incoming = new IncomingAttack
                {
                    attacker = attacker,
                    attack = attack,
                    source = attacker.Position,
                    signature = attacker.CurrentMove != null && attacker.CurrentMove.isSignature,
                    powerMult = attacker.PowerMult * EnemyDamageMultFor(attacker),
                    groupAnswered = groupAnswered,
                };
                actor = p;
                HitOutcome outcome = p.ReceiveAttack(incoming);
                actor = null;
                connected = true;
                if (outcome == HitOutcome.Hit)
                {
                    attacker.OnDealtDamage(attack.damage * attacker.PowerMult);
                    attacker.OnLandedHitKalpa();
                    if (CurrentRoom != null && CurrentRoom.challenge == ChallengeKind.NoHit) FailChallenge();
                }
            }
            return connected;
        }

        public void PlayerStrike(HitShape shape, float damage, float posture, HashSet<Enemy> alreadyHit, float pull = 0f, bool unblockable = false)
        {
            Strike(Player.Position, Player.Facing, shape, damage, posture, alreadyHit, unblockable, true, pull);
        }

        /// <summary>Technique-driven strike that resolves immediately and cannot be blocked.</summary>
        public void PlayerInstantStrike(Vector2 origin, Vector2 direction, HitShape shape, float damage, float posture)
        {
            Fx.Emit(FxType.MirrorThrust, origin, direction, shape.range, Config.palette.playerAccent);
            Strike(origin, direction, shape, damage, posture, null, true, false);
        }

        public void ScheduleBurst(Vector2 position, int delayFrames, float radius, float damage, float posture, Color? color = null, bool explosion = false, int stunFrames = 0)
        {
            bursts.Add(new Burst
            {
                position = position, framesLeft = delayFrames, radius = radius, damage = damage, posture = posture,
                color = color ?? Config.palette.playerAccent, explosion = explosion, stunFrames = stunFrames,
            });
            Fx.Emit(FxType.Afterimage, position, Player.Facing, Player.Radius * 1.3f, Config.palette.gold);
        }

        void Strike(Vector2 origin, Vector2 direction, HitShape shape, float damage, float posture,
                    HashSet<Enemy> alreadyHit, bool unblockable, bool applyRewardHooks, float pull = 0f)
        {
            if (applyRewardHooks) StrikeArena(origin, direction, shape, damage);
            for (int i = 0; i < Enemies.Count; i++)
            {
                Enemy e = Enemies[i];
                if (!e.CanBeHit || e.IsAlly) continue;
                if (alreadyHit != null && alreadyHit.Contains(e)) continue;
                if (!CombatMath.Overlaps(shape, origin, direction, e.Position, e.Radius)) continue;
                alreadyHit?.Add(e);

                float finalDamage = damage;
                float finalPosture = posture;
                bool crit = false;
                if (applyRewardHooks)
                {
                    for (int r = 0; r < Player.Build.Effects.Count; r++)
                    {
                        Player.Build.Effects[r].ModifyAttackDamage(this, e, ref finalDamage);
                        Player.Build.Effects[r].ModifyAttackPosture(this, e, ref finalPosture);
                    }
                    if (Player.Build.Modifiers.critChance > 0f && Run.Rng.NextFloat() < Player.Build.Modifiers.critChance)
                    {
                        crit = true;
                        finalDamage *= CritMult;
                    }
                }
                Vector2 hitDir = e.Position - origin;
                hitDir = hitDir.sqrMagnitude > 1e-4f ? hitDir.normalized : direction;
                StrikeResult result = e.ReceivePlayerHit(finalDamage, finalPosture, hitDir, unblockable);
                if (result != StrikeResult.Hit || !applyRewardHooks) continue;

                if (crit) Fx.Emit(FxType.Crit, e.Position, hitDir, finalDamage, e.Def.bloodColor);
                if (crit && Player.Build.Modifiers.trailElement == Element.Shadow) e.Wound(WoundRegion.Eyes);
                if (pull > 0f) e.Pull(Player.Position, pull);
                // Plain steel on an element: Ressonância.
                if (Player.Build.Modifiers.trailElement == Element.None && e.ActiveElement != Element.None) e.Resonate();
                if (Player.ConsumeOil()) e.ApplyDot(8f, 3f, Element.Fire);
                float lifesteal = Player.Build.Modifiers.lifesteal + Player.KiLifesteal;
                if (lifesteal > 0f) Player.Heal(finalDamage * lifesteal);
                Player.OnStrikeLanded(e);
                for (int r = 0; r < Player.Build.Effects.Count; r++) Player.Build.Effects[r].OnDealtDamage(this, e, finalDamage);
            }
        }

        void TickBursts()
        {
            for (int i = bursts.Count - 1; i >= 0; i--)
            {
                Burst b = bursts[i];
                b.framesLeft--;
                if (b.framesLeft > 0)
                {
                    bursts[i] = b;
                    continue;
                }
                bursts.RemoveAt(i);
                Fx.Emit(b.explosion ? FxType.Explosion : FxType.Burst, b.position, Vector2.up, b.radius, b.color);
                Strike(b.position, Vector2.right, HitShape.Circle(b.radius), b.damage, b.posture, null, true, false);
                if (b.stunFrames > 0)
                {
                    for (int e = 0; e < Enemies.Count; e++)
                    {
                        Enemy en = Enemies[e];
                        if (en.IsGone || Vector2.Distance(en.Position, b.position) > b.radius + en.Radius) continue;
                        en.Stun(en.IsBeast ? b.stunFrames : b.stunFrames / 3);
                    }
                }
            }
        }

        // ── Floor blasts ────────────────────────────────────────────────────────────────

        public static Color BlastColor(BlastKind kind)
        {
            switch (kind)
            {
                case BlastKind.Talisman: return ColorUtil.Hex("#E63946");
                case BlastKind.Fire: return ColorUtil.Hex("#FF9F1C");
                case BlastKind.Spirit: return ColorUtil.Hex("#9AD1D4");
                case BlastKind.Lightning: return ColorUtil.Hex("#9FD8FF");
                default: return ColorUtil.Hex("#FF6B35");
            }
        }

        /// <summary>
        /// Marks a circle that explodes after <paramref name="fuse"/> frames. Players inside get hurt
        /// unless they dodge through the blast; a dying lantern's blast hurts enemies as well.
        /// </summary>
        public void ScheduleEnemyBlast(Enemy owner, Vector2 position, float radius, int fuse, float damage, float posture,
                                       BlastKind kind, bool hitsEnemies, bool signature = false)
        {
            var go = new GameObject("Blast");
            go.transform.SetParent(root, false);
            go.transform.position = position;
            Color color = BlastColor(kind);
            var b = new EnemyBlast
            {
                Position = position, Radius = radius, Fuse = Mathf.Max(1, fuse), Damage = damage, Posture = posture, Kind = kind,
                HitsEnemies = hitsEnemies, Owner = owner, Signature = signature, Color = color, Root = go.transform,
                Fill = SpriteUtil.Make("Fill", go.transform, ShapeLibrary.Get(Shape.SoftCircle), color.WithAlpha(0.2f), Sorting.Ground + 3),
                Ring = SpriteUtil.Make("Ring", go.transform, ShapeLibrary.Get(Shape.Ring), color, Sorting.Ground + 4),
            };
            Sprite icon = kind == BlastKind.Bomb ? PixelArt.Image("item_bomb") : kind == BlastKind.Talisman ? PixelArt.Image("item_scroll_fire") : null;
            if (icon != null) b.Icon = SpriteUtil.Make("Icon", go.transform, icon, Color.white, Sorting.Overlay - 12);
            b.Ring.transform.localScale = Vector3.one * radius * 2f;
            Blasts.Add(b);
            RenderBlast(b);
        }

        void TickBlasts()
        {
            for (int i = Blasts.Count - 1; i >= 0; i--)
            {
                EnemyBlast b = Blasts[i];
                if (++b.Age < b.Fuse) continue;
                Blasts.RemoveAt(i);
                Fx.Emit(FxType.Explosion, b.Position, Vector2.up, b.Radius, b.Color);
                foreach (PlayerController p in Players)
                {
                    if (!p.IsAlive || Vector2.Distance(p.Position, b.Position) > b.Radius + p.Radius * 0.6f) continue;
                    Vector2 dir = p.Position - b.Position;
                    actor = p;
                    bool dodged = p.ReceiveHazard(b.Damage, b.Posture, dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector2.up);
                    // Dodging through a bomb or talisman as it goes off is a perfect dodge.
                    if (dodged && b.Owner != null && !b.HitsEnemies) p.CreditPerfectDodge(b.Owner, b.Signature);
                    actor = null;
                }
                if (b.HitsEnemies)
                {
                    for (int e = 0; e < Enemies.Count; e++)
                    {
                        Enemy en = Enemies[e];
                        if (en == b.Owner || !en.CanBeHit || Vector2.Distance(en.Position, b.Position) > b.Radius + en.Radius * 0.6f) continue;
                        Vector2 dir = en.Position - b.Position;
                        en.ReceiveHazard(b.Damage * 1.5f, b.Posture * 1.5f, dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector2.up);
                    }
                }
                Object.Destroy(b.Root.gameObject);
            }
        }

        /// <summary>The circle fills as the fuse burns; it flickers in the last third.</summary>
        void RenderBlast(EnemyBlast b)
        {
            float t = b.Progress;
            float s = b.Radius * 2f * Mathf.Lerp(0.15f, 1f, t);
            b.Fill.transform.localScale = new Vector3(s, s, 1f);
            bool late = t > 0.66f;
            float flicker = late && Mathf.Repeat(Time.unscaledTime * 12f, 1f) < 0.5f ? 1f : 0.6f;
            b.Fill.color = b.Color.WithAlpha((0.18f + 0.3f * t) * flicker);
            b.Ring.color = Color.Lerp(b.Color, Color.white, late ? 0.5f * flicker : 0f).WithAlpha(0.5f + 0.5f * t);
            if (b.Icon != null)
            {
                float bob = b.Kind == BlastKind.Bomb ? 0.12f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 10f)) : 0f;
                b.Icon.transform.localPosition = new Vector3(0f, 0.15f + bob, 0f);
                b.Icon.transform.localScale = Vector3.one * (b.Kind == BlastKind.Bomb ? 1.1f : 0.9f) * (1f + (late ? 0.15f * flicker : 0f));
            }
        }

        void ClearBlasts()
        {
            foreach (EnemyBlast b in Blasts)
            {
                if (b.Root != null) Object.Destroy(b.Root.gameObject);
            }
            Blasts.Clear();
        }

        /// <summary>A blow inside a finisher: blood, a freeze and a wet cut — but no death yet.</summary>
        public void FinisherImpact(Enemy enemy, Vector2 dir, float intensity)
        {
            Fx.Emit(FxType.FinisherHit, enemy.Position, dir, intensity, enemy.Def.bloodColor);
            // A cut that followed the golden line opens the enemy up.
            float gush = 1f + 5f * Player.CutPrecision * Player.CutPrecision;
            Fx.Blood(enemy.Position, dir, (10f + 18f * intensity) * gush, enemy.Def.bloodColor);
        }

        public void ExecuteFinisher(Enemy enemy, FinisherStyle style, Vector2 dir)
        {
            if (enemy == null || enemy.CurrentState != Enemy.State.Finishing) return;

            Player.Build.Finishers++;
            Player.OnFinisherDone();
            if (dir.sqrMagnitude < 1e-4f) dir = Player.Facing;
            // Magnitude 2 marks the last enemy of the room: the router plays the slow-motion kill.
            bool lastOne = ActiveEnemyCount <= 1 && IsLastWave;
            Fx.Emit(FxType.Finisher, enemy.Position, dir, lastOne ? 2f : 1f, enemy.Def.bloodColor);
            Fx.Popup(Player.Weapon.finisherName.ToUpperInvariant(), enemy.Position + Vector2.up * 1f, Color.white);
            Fx.Gore(style, enemy.Position, dir, enemy.BodySprite, enemy.BodyColor, enemy.Def.bloodColor, enemy.Radius);
            float precise = Player.CutPrecision * Player.CutPrecision;
            if (precise > 0.05f)
            {
                Fx.Blood(enemy.Position, dir, 30f + 160f * precise, enemy.Def.bloodColor);
                Fx.Blood(enemy.Position, -dir, 40f * precise, enemy.Def.bloodColor);
            }
            bool absorb = enemy.Mastered && Player.Build.CanAbsorb(enemy.Def.techniqueId);
            enemy.DieByFinisher();
            AddFlow(Config.flow.finisherPoints);
            for (int r = 0; r < Player.Build.Effects.Count; r++) Player.Build.Effects[r].OnFinisher(this, enemy);

            float heal = Player.Build.Modifiers.finisherHeal + (Player.Build.Flow.Level >= Player.Build.Flow.MaxLevel ? Config.flow.level3FinisherHeal : 0f);
            if (heal > 0f)
            {
                Player.Heal(heal);
                Fx.Popup("+" + Mathf.RoundToInt(heal), Player.Position, Config.palette.health);
            }

            if (!absorb) return;
            Player.Build.AddPendingTechnique(enemy.Def.techniqueId);
            RewardDef preview = RewardRegistry.Create(enemy.Def.techniqueId);
            Fx.Emit(FxType.TechniqueAbsorbed, enemy.Position, Player.Position - enemy.Position, 1f, Config.palette.gold);
            Fx.Toast(string.Format(Strings.ToastTechnique, preview != null ? preview.Name : enemy.Def.techniqueId), Config.palette.gold);
        }

        /// <summary>Marks the enemy for absorption when the player gives its signature the Perfect Answer.</summary>
        public void TryMaster(Enemy attacker, SignatureAnswer answer, bool fromSignature)
        {
            if (Run == null || attacker == null || attacker.IsGone || attacker.Mastered || !fromSignature) return;
            if (attacker.Def.signatureAnswer != answer || !Player.Build.CanAbsorb(attacker.Def.techniqueId)) return;
            attacker.MarkMastered();
            Fx.Emit(FxType.Mastered, attacker.Position, Vector2.up, 1f, Config.palette.gold);
            Fx.Popup(Strings.PopupMastered, attacker.Position + Vector2.up * 0.9f, Config.palette.gold);
            HintOnce(HintId.Mastery);
        }

        /// <summary>A perfect action extends the streak; milestones get their own fanfare.</summary>
        public void RegisterPerfect(Vector2 where)
        {
            if (Run == null) return;
            int streak = Player.Build.RegisterPerfect();
            HintOnce(HintId.SwapParry);
            if (streak >= 5 && streak % 5 == 0) Fx.Emit(FxType.StreakMilestone, where, Vector2.up, streak, Config.palette.gold);
        }

        public void AddFlow(float points)
        {
            if (Run == null) return;
            int before = Player.Build.Flow.Level;
            int after = Player.Build.Flow.Add(points * Player.Build.Modifiers.flowGainMult);
            if (after > before) Fx.Emit(FxType.FlowUp, Player.Position, Vector2.up, after, Config.palette.gold);
        }

        public void DropFlow(int levels)
        {
            if (Run == null) return;
            int before = Player.Build.Flow.Level;
            int after = Player.Build.Flow.DropLevels(levels);
            if (after < before) Fx.Emit(FxType.FlowDown, Player.Position, Vector2.down, after);
        }

        public void HintOnce(HintId hint)
        {
            if (hintsShown.Add(hint)) Fx.Hint(hint);
        }

        /// <summary>The Sombra's signature: vanish and reappear behind the player.</summary>
        public void TeleportBehindPlayer(Enemy enemy)
        {
            Vector2 from = enemy.Position;
            PlayerController victim = enemy.Target != null && enemy.Target.IsAlive ? enemy.Target : Host;
            Vector2 behind = victim.Position - victim.Facing * Config.enemies.teleportBehindDistance;
            behind = Layout.NearestFloor(behind, enemy.Radius);
            Fx.Emit(FxType.ShadowStep, from, Vector2.up, enemy.Radius, enemy.Def.color);
            enemy.Teleport(behind);
            Vector2 to = victim.Position - behind;
            if (to.sqrMagnitude > 1e-4f) enemy.Facing = to.normalized;
            Fx.Emit(FxType.ShadowStep, behind, Vector2.up, enemy.Radius, enemy.Def.color);
        }

        // ── Projectiles ─────────────────────────────────────────────────────────────────

        // ── Equipment effects ───────────────────────────────────────────────────────────

        /// <summary>Lightning jumps from the struck enemy to the nearest others.</summary>
        public void ChainLightning(Enemy from, int jumps, float damage, float posture)
        {
            from?.MarkElement(Element.Lightning);
            Enemy current = from;
            var struck = new HashSet<Enemy> { from };
            for (int j = 0; j < jumps; j++)
            {
                Enemy next = null;
                float best = 5f;
                foreach (Enemy e in Enemies)
                {
                    if (!e.CanBeHit || struck.Contains(e)) continue;
                    float d = Vector2.Distance(current.Position, e.Position);
                    if (d < best)
                    {
                        best = d;
                        next = e;
                    }
                }
                if (next == null) return;
                struck.Add(next);
                Vector2 dir = next.Position - current.Position;
                Fx.Emit(FxType.Lightning, current.Position, dir.normalized, dir.magnitude, Elements.Color(Element.Lightning));
                next.ReceivePlayerHit(damage, posture, dir.normalized, true);
                next.MarkElement(Element.Lightning);
                current = next;
            }
        }

        /// <summary>Wind items: a crescent of air flies forward and cuts everything in its path.</summary>
        public void FireSlashWave(Vector2 origin, Vector2 direction, float damage, float posture)
        {
            Projectile p = CreateProjectile(origin + direction * (Player.Radius + 0.4f), direction, 13f, 0.55f,
                                            Elements.Color(Element.Wind), ShapeLibrary.ArcBand(110f), 1.6f, false);
            p.Reflected = true;
            p.Piercing = true;
            p.Damage = damage;
            p.Posture = posture;
            p.Life = 50;
            p.Element = Element.Wind;
        }

        /// <summary>Shadow items: a blade of darkness falls on whoever was parried.</summary>
        public void ShadowStrike(Enemy target, float damage, float posture)
        {
            if (target == null || !target.CanBeHit) return;
            target.MarkElement(Element.Shadow);
            ScheduleBurst(target.Position, 12, 1.1f, damage, posture, Elements.Color(Element.Shadow));
        }

        // ── Loot ────────────────────────────────────────────────────────────────────────

        /// <summary>Called by every enemy death: may leave an item behind (Guardians always do).</summary>
        public void OnEnemyDied(Enemy enemy)
        {
            if (Run == null || CurrentRoom == null) return;
            float chance = ItemGenerator.DropChance(Depth, enemy.IsElite) * (enemy.DropsDouble ? 2f : 1f);
            if (Run.Rng.NextFloat() >= chance) return;
            Item item = ItemGenerator.Roll(Run.Rng, Depth, enemy.IsElite);
            SpawnPickup(item, enemy.Position);
        }

        void SpawnPickup(Item item, Vector2 position)
        {
            var go = new GameObject("Loot");
            go.transform.SetParent(root, false);
            go.transform.position = position;
            Color rc = Rarities.Color(item.rarity);
            int tier = (int)item.rarity;
            var pickup = new ItemPickup
            {
                Item = item,
                Position = position,
                Root = go.transform,
                Glow = SpriteUtil.Make("Glow", go.transform, ShapeLibrary.Get(Shape.SoftCircle), rc.WithAlpha(0.35f), Sorting.Shadow + 4),
                Beam = SpriteUtil.Make("Beam", go.transform, ShapeLibrary.Get(Shape.Pixel), rc.WithAlpha(0.35f), Sorting.Overlay - 20),
                // A soft halo in the rarity colour behind the pixel-art item.
                Frame = SpriteUtil.Make("Halo", go.transform, ShapeLibrary.Get(Shape.SoftCircle), rc.WithAlpha(0.8f), Sorting.Overlay - 20),
                Icon = SpriteUtil.Make("Icon", go.transform, PixelArt.ItemIcon(item.slot, item.element, item.rarity), Color.white, Sorting.Overlay - 19),
            };
            // Rarer loot stands taller: you can read a drop's worth from across the arena.
            float beamHeight = 1.2f + 0.45f * tier;
            pickup.Beam.transform.localPosition = new Vector3(0f, beamHeight * 0.5f, 0f);
            pickup.Beam.transform.localScale = new Vector3(0.1f + 0.015f * tier, beamHeight, 1f);
            pickup.Glow.transform.localScale = Vector3.one * (1.2f + 0.15f * tier);
            pickup.Frame.transform.localScale = Vector3.one * 0.85f;
            pickup.Icon.transform.localScale = Vector3.one * 0.75f;
            Pickups.Add(pickup);
            Fx.Emit(FxType.ItemDrop, position, Vector2.up, tier, rc);
        }

        void TickPickups()
        {
            for (int i = Pickups.Count - 1; i >= 0; i--)
            {
                ItemPickup p = Pickups[i];
                p.Age += SimClock.Dt;
                if (p.Age < 0.4f) continue;
                PlayerController near = NearestAlivePlayer(p.Position);
                if (near == null || Vector2.Distance(p.Position, near.Position) > PickupRadius) continue;
                Collect(p, near);
                Pickups.RemoveAt(i);
            }
        }

        /// <summary>A teammate (not the host) picked something up: only they are told.</summary>
        public event Action<PlayerController, Item, bool> RemoteItemCollected;

        /// <summary>Loot is personal: whoever walks over it wears it (or bags it).</summary>
        void Collect(ItemPickup p, PlayerController who)
        {
            if (who == null || who.Build == null) who = Host;
            bool equipped = who.Build.AddItem(p.Item);
            if (equipped) who.ApplyStats(false);
            if (who == Host) Fx.ItemCollected(p.Item, equipped, p.Position);
            else
            {
                Fx.Emit(FxType.ItemDrop, p.Position, Vector2.up, (int)p.Item.rarity, Rarities.Color(p.Item.rarity));
                RemoteItemCollected?.Invoke(who, p.Item, equipped);
            }
            Object.Destroy(p.Root.gameObject);
        }

        /// <summary>Room cleared: nothing on the floor is ever lost (it goes to whoever is nearest).</summary>
        void CollectAllPickups()
        {
            foreach (ItemPickup p in Pickups) Collect(p, NearestAlivePlayer(p.Position) ?? Host);
            Pickups.Clear();
        }

        void ClearPickups()
        {
            foreach (ItemPickup p in Pickups)
            {
                if (p.Root != null) Object.Destroy(p.Root.gameObject);
            }
            Pickups.Clear();
        }

        public void SpawnProjectile(Enemy owner, AttackDef attack, bool signature)
        {
            int count = Mathf.Max(1, attack.projectileCount);
            Sprite art = string.IsNullOrEmpty(attack.projectileArt) ? null : PixelArt.ProjectileSprite(attack.projectileArt);
            for (int i = 0; i < count; i++)
            {
                // A fan spreads evenly across the arc, centred on where the thrower faces.
                float offset = count == 1 ? 0f : -attack.spreadDegrees * 0.5f + attack.spreadDegrees * i / (count - 1);
                if (owner.EyesWound) offset += Run.Rng.Range(-25f, 25f);
                Vector2 dir = CombatMath.FromAngle(CombatMath.AngleDegrees(owner.Facing) + offset);
                Vector2 start = owner.Position + dir * (owner.Radius + 0.3f);
                Projectile p = art != null
                    ? CreateProjectile(start, dir, attack.projectileSpeed, attack.projectileRadius, Color.white, art, 1f, false)
                    : CreateProjectile(start, dir, attack.projectileSpeed, attack.projectileRadius, PixelArt.Arrow() != null ? Color.white : owner.Def.weaponColor);
                p.Attack = attack;
                p.Owner = owner;
                p.Signature = signature;
                p.Art = attack.projectileArt;
                p.Spin = attack.projectileArt != null && attack.projectileArt.Contains("shuriken") ? 900f : 0f;
            }
        }

        /// <summary>Every enemy death reaches the party's techniques (Lanterna Viva, Fome do Lodo).</summary>
        public void NotifyKill(Enemy enemy)
        {
            if (Run == null) return;
            PlayerController killer = actor ?? Host;
            if (killer != null && killer.IsAlive) killer.OnKillCredited(enemy);
            if (enemy.IsAlly)
            {
                Run.AllyRooms = 0;
                return;
            }
            // Mon for whoever struck it down.
            if (killer?.Build != null) killer.Build.Mon += enemy.IsElite ? 12 : 3;
            enemy.OnDefeated?.Invoke(enemy);
            enemy.OnDefeated = null;
            // A duel won: the foe's technique is guaranteed, mastered or not.
            if (CurrentRoom != null && CurrentRoom.kind == RoomKind.Duel && !string.IsNullOrEmpty(enemy.Def.techniqueId))
            {
                foreach (PlayerController p in Players)
                {
                    if (p.Build == null || !p.Build.CanAbsorb(enemy.Def.techniqueId)) continue;
                    p.Build.AddPendingTechnique(enemy.Def.techniqueId);
                    Fx.Popup(Strings.PopupMastered, p.Position + Vector2.up * 1.4f, Config.palette.gold);
                }
            }
            // Kalpa "inheritance": the nearest enemy takes the fallen one's strength.
            if (Run.HasRule(KalpaRule.EnemyAbsorb))
            {
                Enemy heir = null;
                float best = 6f;
                foreach (Enemy e in Enemies)
                {
                    if (e == enemy || e.IsGone || e.IsAlly) continue;
                    float d = Vector2.Distance(e.Position, enemy.Position);
                    if (d < best)
                    {
                        best = d;
                        heir = e;
                    }
                }
                heir?.Inherit();
            }
            // Every duelist's own techniques hear about it (each heals or explodes for themselves).
            PlayerController before = actor;
            foreach (PlayerController p in Players)
            {
                if (p.Build == null || !p.IsAlive) continue;
                actor = p;
                for (int r = 0; r < p.Build.Effects.Count; r++) p.Build.Effects[r].OnKill(this, enemy);
            }
            actor = before;
        }

        /// <summary>A fan of player kunai (Leque de Kunai).</summary>
        public void FirePlayerKunai(Vector2 origin, Vector2 direction, int count, float spread, float damage, float posture)
        {
            Sprite art = PixelArt.ProjectileSprite("fx_kunai");
            for (int i = 0; i < count; i++)
            {
                float offset = count == 1 ? 0f : -spread * 0.5f + spread * i / (count - 1);
                Vector2 dir = CombatMath.FromAngle(CombatMath.AngleDegrees(direction) + offset);
                Projectile p = CreateProjectile(origin + dir * (Player.Radius + 0.3f), dir, 15f, 0.22f, Config.palette.playerAccent, art, 1f, false);
                p.Reflected = true;
                p.Damage = damage;
                p.Posture = posture;
                p.Art = "fx_kunai";
            }
            Fx.Emit(FxType.EnemySwing, origin, direction, 2f);
        }

        /// <summary>A player-owned arrow (Flecha Reflexa): flies like a reflected one.</summary>
        public void FirePlayerArrow(Vector2 origin, Vector2 direction, float damage, float posture)
        {
            Projectile p = CreateProjectile(origin + direction * (Player.Radius + 0.3f), direction, 16f, 0.22f, Config.palette.playerAccent);
            p.Reflected = true;
            p.Damage = damage;
            p.Posture = posture;
            Fx.Emit(FxType.EnemySwing, origin, direction, 2f);
        }

        Projectile CreateProjectile(Vector2 start, Vector2 dir, float speed, float radius, Color color,
                                    Sprite sprite = null, float size = ArrowLength, bool arrow = true)
        {
            var go = new GameObject("Projectile");
            go.transform.SetParent(root, false);
            float artAngle = 0f;
            if (sprite == null && arrow)
            {
                sprite = PixelArt.Arrow();
                if (sprite != null) artAngle = PixelArt.ArrowAngleOffset;
            }
            var p = new Projectile
            {
                Position = start,
                PrevPosition = start,
                Direction = dir,
                Speed = speed,
                Radius = radius,
                Life = ProjectileLifeFrames,
                View = SpriteUtil.Make("Arrow", go.transform, sprite != null ? sprite : ShapeLibrary.Get(Shape.Arrow), color, Sorting.Overlay - 10),
                Size = size,
                IsArrow = arrow,
                ArtAngle = artAngle,
                Ring = SpriteUtil.Make("ArrowRing", go.transform, ShapeLibrary.Get(Shape.Ring), Config.palette.playerAccent, Sorting.Overlay - 9),
                Glow = SpriteUtil.Make("ArrowGlow", go.transform, ShapeLibrary.Get(Shape.SoftCircle), Color.white, Sorting.Overlay - 11),
            };
            p.Ring.enabled = false;
            p.Glow.enabled = false;
            Projectiles.Add(p);
            return p;
        }

        void TickProjectiles()
        {
            for (int i = Projectiles.Count - 1; i >= 0; i--)
            {
                Projectile p = Projectiles[i];
                p.PrevPosition = p.Position;
                // Strong wind bends every arrow and blade in flight.
                if (Wind != Vector2.zero) p.Direction = (p.Direction + Wind * 0.9f * SimClock.Dt).normalized;
                p.Position += p.Direction * p.Speed * SimClock.Dt;
                p.Life--;

                // Walls and statues stop arrows.
                bool dead = p.Life <= 0 || !Layout.IsFloorAt(p.Position);

                if (!dead && !p.Reflected) dead = TickHostileProjectile(p);
                else if (!dead) dead = TickFriendlyProjectile(p);

                if (!dead) continue;
                Object.Destroy(p.View.transform.parent.gameObject);
                Projectiles.RemoveAt(i);
            }
        }

        /// <summary>Returns true when the projectile is spent.</summary>
        bool TickHostileProjectile(Projectile p)
        {
            if (p.PassedPlayer) return false;
            PlayerController victim = null;
            float soonest = float.PositiveInfinity;
            foreach (PlayerController pl in Players)
            {
                if (!pl.IsAlive) continue;
                float frames = p.FramesToImpact(pl);
                if (frames < soonest)
                {
                    soonest = frames;
                    victim = pl;
                }
            }
            if (victim == null) return false;

            if (!p.Cued && soonest <= Config.feel.timingCueLeadFrames)
            {
                p.Cued = true;
                Fx.Emit(FxType.TimingCue, p.Position, p.Direction, 0f);
            }

            if (Vector2.Distance(p.Position, victim.Position) > victim.Radius + p.Radius) return false;
            actor = victim;
            HitOutcome outcome = ReceiveProjectile(p, victim);
            actor = null;
            switch (outcome)
            {
                case HitOutcome.PerfectParry:
                case HitOutcome.SwapParry:
                    Reflect(p);
                    return false;
                case HitOutcome.PerfectDodge:
                case HitOutcome.Ignored:
                    p.PassedPlayer = true;
                    return false;
                default:
                    return true;
            }
        }

        HitOutcome ReceiveProjectile(Projectile p, PlayerController victim)
        {
            var incoming = new IncomingAttack
            {
                attacker = p.Owner,
                attack = p.Attack,
                source = p.Position - p.Direction,
                signature = p.Signature,
                projectile = true,
                powerMult = p.Owner != null ? p.Owner.PowerMult : 1f,
            };
            return victim.ReceiveAttack(incoming);
        }

        bool TickFriendlyProjectile(Projectile p)
        {
            for (int i = 0; i < Enemies.Count; i++)
            {
                Enemy e = Enemies[i];
                if (!e.CanBeHit || p.Struck.Contains(e)) continue;
                if (Vector2.Distance(p.Position, e.Position) > e.Radius + p.Radius) continue;
                p.Struck.Add(e);
                if (p.Interrupts) e.InterruptLeap();
                // A reflected arrow that hits someone else turns them on the archer.
                if (p.Owner != null && p.Owner != e) e.StartBrawl(p.Owner);
                e.ReceivePlayerHit(p.Damage, p.Posture, p.Direction, true);
                if (p.Element != Element.None) e.MarkElement(p.Element);
                if (!p.Piercing) return true;
            }
            return false;
        }

        /// <summary>A perfectly parried arrow flies back at whoever shot it.</summary>
        void Reflect(Projectile p)
        {
            Enemy target = p.Owner != null && !p.Owner.IsGone ? p.Owner : FindNearestEnemy(p.Position, null);
            Vector2 dir = target != null ? target.Position - p.Position : -p.Direction;
            p.Direction = dir.sqrMagnitude > 1e-4f ? dir.normalized : -p.Direction;
            p.Reflected = true;
            p.Speed *= ReflectSpeedMult;
            p.Damage = p.Attack.damage;
            p.Posture = p.Attack.postureOnParried;
            p.Life = ProjectileLifeFrames;
            p.View.color = Config.palette.playerAccent;
        }

        void ClearProjectiles()
        {
            foreach (Projectile p in Projectiles)
            {
                if (p.View != null) Object.Destroy(p.View.transform.parent.gameObject);
            }
            Projectiles.Clear();
        }

        // ── Queries ─────────────────────────────────────────────────────────────────────

        public Enemy FindAssistTarget(Vector2 from, Vector2 facing, float degrees, float range, bool preferThreat)
        {
            Enemy best = null;
            float bestScore = float.MaxValue;
            for (int i = 0; i < Enemies.Count; i++)
            {
                Enemy e = Enemies[i];
                if (!e.CanBeHit || e.IsAlly) continue;
                Vector2 to = e.Position - from;
                float dist = to.magnitude;
                if (dist > range + e.Radius) continue;
                float angle = Vector2.Angle(facing, to);
                if (angle > degrees) continue;
                float score = angle + dist * 8f;
                if (preferThreat && e.IsThreatening) score -= 1000f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = e;
                }
            }
            return best;
        }

        public Enemy FindFinisherTarget(Vector2 from, float range)
        {
            Enemy best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < Enemies.Count; i++)
            {
                Enemy e = Enemies[i];
                if (!e.IsBroken || e.IsAlly) continue;
                float dist = Vector2.Distance(from, e.Position) - e.Radius;
                if (dist > range || dist >= bestDist) continue;
                bestDist = dist;
                best = e;
            }
            return best;
        }

        public Enemy FindNearestEnemy(Vector2 from, Enemy exclude)
        {
            Enemy best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < Enemies.Count; i++)
            {
                Enemy e = Enemies[i];
                if (e == exclude || !e.CanBeHit) continue;
                float dist = Vector2.Distance(from, e.Position);
                if (dist >= bestDist) continue;
                bestDist = dist;
                best = e;
            }
            return best;
        }

        // ── Physics ─────────────────────────────────────────────────────────────────────

        void ResolveCollisions()
        {
            // Dodging passes through bodies: that is what makes the counter-thrust possible.
            // Teammates never block each other.
            for (int i = 0; i < Enemies.Count; i++)
            {
                Enemy a = Enemies[i];
                if (!IsSolid(a)) continue;
                for (int j = i + 1; j < Enemies.Count; j++)
                {
                    if (IsSolid(Enemies[j])) Separate(a, Enemies[j]);
                }
                foreach (PlayerController p in Players)
                {
                    if (p.CurrentState != PlayerController.State.Dodge && p.IsAlive) Separate(p, a);
                }
            }

            foreach (PlayerController p in Players) ClampToArena(p);
            for (int i = 0; i < Enemies.Count; i++) ClampToArena(Enemies[i]);
        }

        static bool IsSolid(Enemy e)
        {
            return !e.IsGone && e.CurrentState != Enemy.State.Spawning && e.CurrentState != Enemy.State.Reassembling && !e.IsBurrowed;
        }

        static void Separate(Fighter a, Fighter b)
        {
            Vector2 d = b.Position - a.Position;
            float minDist = a.Radius + b.Radius;
            float distSq = d.sqrMagnitude;
            if (distSq >= minDist * minDist) return;
            float dist = Mathf.Sqrt(distSq);
            Vector2 n = dist > 1e-4f ? d / dist : Vector2.right;
            float overlap = minDist - dist;
            float total = a.Mass + b.Mass;
            a.Position -= n * overlap * (b.Mass / total);
            b.Position += n * overlap * (a.Mass / total);
        }

        /// <summary>Walls and statues: fighters slide along them.</summary>
        void ClampToArena(Fighter f)
        {
            f.Position = Layout.Resolve(f.Position, f.Radius);
        }

        // ── Traps ───────────────────────────────────────────────────────────────────────

        /// <summary>Traps run on the room clock and hurt players and enemies alike.</summary>
        void TickTraps()
        {
            TrapTick++;
            List<TrapDef> traps = Layout.Traps;
            for (int t = 0; t < traps.Count; t++)
            {
                TrapDef trap = traps[t];
                int phase = trap.PhaseAt(TrapTick);
                if (phase != 2) continue;
                foreach (PlayerController p in Players)
                {
                    if (p.IsAlive && TrapTouches(trap, p)) HitByTrap(trap, p, t);
                }
                // Index loop: a trap kill can split a slime (new enemies join after the tick).
                for (int i = 0; i < Enemies.Count; i++)
                {
                    Enemy e = Enemies[i];
                    if (e.CanBeHit && !e.Def.flying && TrapTouches(trap, e)) HitByTrap(trap, e, t);
                }
            }
            // Cooldowns tick down (blades and flames hit repeatedly, but not every frame).
            if (trapCooldown.Count > 0)
            {
                var keys = new List<object>(trapCooldown.Keys);
                foreach (object k in keys)
                {
                    int v = trapCooldown[k] - 1;
                    if (v <= 0) trapCooldown.Remove(k);
                    else trapCooldown[k] = v;
                }
            }
        }

        bool TrapTouches(TrapDef trap, Fighter f)
        {
            switch (trap.type)
            {
                case TrapType.Spikes:
                    foreach (Vector2Int c in trap.cells)
                    {
                        Vector2 min = Layout.Origin + new Vector2(c.x, c.y);
                        var closest = new Vector2(Mathf.Clamp(f.Position.x, min.x, min.x + 1f), Mathf.Clamp(f.Position.y, min.y, min.y + 1f));
                        if ((closest - f.Position).sqrMagnitude < f.Radius * f.Radius * 0.5f) return true;
                    }
                    return false;
                case TrapType.FlameJet:
                    return CombatMath.Overlaps(HitShape.Line(trap.length, trap.width), trap.origin, trap.direction, f.Position, f.Radius);
                case TrapType.Blade:
                {
                    float a = trap.BladeAngle(TrapTick) * Mathf.Deg2Rad;
                    var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    // Two arms: the blade sweeps both ways from its pivot.
                    return CombatMath.Overlaps(HitShape.Line(trap.length, trap.width), trap.origin, dir, f.Position, f.Radius) ||
                           CombatMath.Overlaps(HitShape.Line(trap.length, trap.width), trap.origin, -dir, f.Position, f.Radius);
                }
            }
            return false;
        }

        void HitByTrap(TrapDef trap, Fighter f, int trapIndex)
        {
            var key = new TrapHitKey(trapIndex, f);
            if (trap.type == TrapType.Spikes)
            {
                // Spikes strike once per eruption.
                int activation = trap.ActivationIndex(TrapTick);
                if (trapActivation.TryGetValue(key, out int last) && last == activation) return;
                trapActivation[key] = activation;
            }
            else
            {
                if (trapCooldown.ContainsKey(key)) return;
                trapCooldown[key] = trap.type == TrapType.Blade ? 40 : 22;
            }

            Vector2 dir = f.Position - trap.origin;
            if (trap.type == TrapType.FlameJet) dir = trap.direction;
            dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector2.up;
            if (f is PlayerController p)
            {
                actor = p;
                p.ReceiveHazard(trap.damage, trap.posture, dir);
                actor = null;
            }
            else if (f is Enemy e)
            {
                // Luring enemies into traps pays: they take more than you do.
                e.ReceiveHazard(trap.damage * 1.5f, trap.posture * 1.5f, dir);
            }
        }

        readonly struct TrapHitKey : IEquatable<TrapHitKey>
        {
            readonly int trap;
            readonly Fighter who;

            public TrapHitKey(int trap, Fighter who)
            {
                this.trap = trap;
                this.who = who;
            }

            public bool Equals(TrapHitKey other) => trap == other.trap && ReferenceEquals(who, other.who);
            public override bool Equals(object obj) => obj is TrapHitKey k && Equals(k);
            // Reference hash: Unity's "== null" turns true once a fighter is destroyed, which would
            // change the hash of a key already in the dictionary and lose it.
            public override int GetHashCode() => trap * 397 ^ System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(who);
        }

        // ── Presentation & dev tools ────────────────────────────────────────────────────

        public void Render(float alpha, float visualDt, bool showHitboxes)
        {
            Decor.Animate(Time.unscaledTime);
            foreach (ItemPickup p in Pickups)
            {
                float t = Time.unscaledTime * 2f + p.Position.x;
                p.Icon.transform.localPosition = new Vector3(0f, 0.15f + 0.08f * Mathf.Sin(t), 0f);
                p.Frame.transform.localPosition = p.Icon.transform.localPosition;
                float halo = 0.85f + 0.12f * Mathf.Sin(t * 2.3f);
                p.Frame.transform.localScale = new Vector3(halo, halo, 1f);
                Color b = p.Beam.color;
                b.a = 0.25f + 0.15f * Mathf.Sin(t * 1.7f);
                p.Beam.color = b;
            }
            foreach (PlayerController p in Players) p.Render(alpha, visualDt, showHitboxes);
            for (int i = 0; i < Enemies.Count; i++) Enemies[i].Render(alpha, visualDt, showHitboxes);
            for (int i = 0; i < Projectiles.Count; i++) RenderProjectile(Projectiles[i], alpha);
            for (int i = 0; i < Blasts.Count; i++) RenderBlast(Blasts[i]);
            trapViews?.Render(TrapTick, Time.unscaledTime);
        }

        /// <summary>Arrows carry their own "press now" ring that closes as they reach you.</summary>
        void RenderProjectile(Projectile p, float alpha)
        {
            Vector2 pos = Vector2.LerpUnclamped(p.PrevPosition, p.Position, alpha);
            Transform holder = p.View.transform.parent;
            holder.position = new Vector3(pos.x, pos.y, 0f);
            float angle = CombatMath.AngleDegrees(p.Direction);
            p.View.transform.localRotation = Quaternion.Euler(0f, 0f, angle + p.ArtAngle + p.Spin * Time.unscaledTime);
            p.View.transform.localPosition = p.IsArrow ? -(Vector3)(p.Direction * p.Size) : Vector3.zero;
            p.View.transform.localScale = new Vector3(p.Size, p.Size, 1f);

            bool hostile = !p.Reflected && !p.PassedPlayer && Host.IsAlive;
            float frames = hostile ? p.FramesToImpact(Host) : float.PositiveInfinity;
            bool showRing = hostile && ShowTimingIndicator && frames <= RingApproachFrames + Config.feel.timingCueLeadFrames;
            p.Ring.enabled = showRing;
            p.Glow.enabled = showRing || p.Reflected;
            if (p.Reflected)
            {
                p.Glow.color = Config.palette.playerAccent.WithAlpha(0.5f);
                p.Glow.transform.localScale = new Vector3(0.9f, 0.9f, 1f);
                return;
            }
            if (!showRing) return;

            float lead = Config.feel.timingCueLeadFrames;
            float t = Mathf.Clamp01(1f - (frames - lead) / RingApproachFrames);
            bool now = frames <= lead;
            float s = now ? 0.9f : Mathf.Lerp(2.6f, 0.9f, t);
            p.Ring.transform.localScale = new Vector3(s, s, 1f);
            p.Ring.color = now ? Color.white : Config.palette.playerAccent.WithAlpha(Mathf.Lerp(0.2f, 0.95f, t));
            p.Glow.color = (now ? Config.palette.playerAccent : Color.white).WithAlpha(now ? 0.7f : 0.25f);
            p.Glow.transform.localScale = new Vector3(1.4f, 1.4f, 1f);
        }

        public void DevKillAllEnemies()
        {
            for (int i = 0; i < Enemies.Count; i++) Enemies[i].Die();
        }

        public void DevSkipRoom()
        {
            if (CurrentRoom == null || !roomActive) return;
            DevKillAllEnemies();
            WaveIndex = CurrentRoom.waves.Length - 1;
            waveDelay = 0;
        }
    }
}
