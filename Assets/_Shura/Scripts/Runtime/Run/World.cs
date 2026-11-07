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
        /// <summary>Passes through every enemy instead of stopping at the first.</summary>
        public bool Piercing;
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

    /// <summary>
    /// Owns the arena, the fighters, projectiles and the room/wave flow, and resolves every
    /// hit. Tick order is fixed (player, enemies, projectiles, effects, collisions, waves)
    /// so runs are reproducible from their seed.
    /// </summary>
    public sealed class World
    {
        struct Burst
        {
            public Vector2 position;
            public int framesLeft;
            public float radius;
            public float damage;
            public float posture;
            public Color color;
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

        readonly Transform root;
        Transform arenaRoot;
        readonly List<Burst> bursts = new List<Burst>();
        int adaptationTimer;
        readonly HashSet<HintId> hintsShown = new HashSet<HintId>();
        int waveDelay;
        bool roomActive;
        bool deathReported;

        public PlayerController Player { get; private set; }
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
        public event Action PlayerDiedEvent;

        public World(GameConfig config, IFeedback fx)
        {
            Config = config;
            Fx = fx;
            Coordinator = new AttackCoordinator(config.enemies.attackGapFrames);
            root = new GameObject("World").transform;
            RebuildArena(config.arena.width, config.arena.height, Biomes.All[0]);

            var playerObject = new GameObject("Player");
            playerObject.transform.SetParent(root, false);
            Player = playerObject.AddComponent<PlayerController>();
            Player.Init(this, config);
            Player.Teleport(PlayerSpawn);
        }

        public Vector2 PlayerSpawn => new Vector2(Bounds.xMin + 3f, 0f);

        public int ActiveEnemyCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < Enemies.Count; i++)
                {
                    if (!Enemies[i].IsGone) count++;
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
            Player.BeginRun(run, input);
        }

        public void EndRun()
        {
            ClearEnemies();
            roomActive = false;
        }

        public void StartRoom(RoomDef room, bool firstRoom)
        {
            ClearEnemies();
            CurrentRoom = room;
            RebuildArena(room.width, room.height, room.biome);
            Coordinator.MinGapFrames = DifficultyRules.AttackGapFrames(Config.enemies.attackGapFrames, room.depth);
            RefreshAdaptation();
            WaveIndex = -1;
            waveDelay = Config.enemies.firstWaveDelay;
            RoomCleared = false;
            roomActive = true;
            Player.ResetForRoom(PlayerSpawn);
            if (firstRoom) HintOnce(HintId.Controls);
        }

        public void Tick()
        {
            if (Run == null) return;
            Run.Profile.Tick(SimClock.Dt, ActiveEnemyCount > 0 && Player.IsAlive);
            if (++adaptationTimer >= SimClock.TickRate) RefreshAdaptation();
            Coordinator.Tick();
            Player.Tick();
            for (int i = 0; i < Enemies.Count; i++) Enemies[i].Tick();
            TickProjectiles();
            TickBursts();
            TickPickups();
            ResolveCollisions();
            RemoveDead();
            TickWaves();
            Run.Elapsed += SimClock.Dt;

            if (!Player.IsAlive && !deathReported)
            {
                deathReported = true;
                PlayerDiedEvent?.Invoke();
            }
        }

        void TickWaves()
        {
            if (!roomActive || RoomCleared || !Player.IsAlive) return;
            if (ActiveEnemyCount > 0) return;
            if (waveDelay > 0)
            {
                waveDelay--;
                return;
            }

            if (WaveIndex + 1 < CurrentRoom.waves.Length)
            {
                WaveIndex++;
                SpawnWave(CurrentRoom.waves[WaveIndex]);
                waveDelay = Config.enemies.waveDelayFrames;
                return;
            }

            RoomCleared = true;
            roomActive = false;
            Coordinator.Reset();
            ClearProjectiles();
            CollectAllPickups();
            RoomClearedEvent?.Invoke();
        }

        void SpawnWave(EnemySpawn[] spawns)
        {
            foreach (EnemySpawn spawn in spawns)
            {
                var go = new GameObject("Enemy_" + spawn.id);
                go.transform.SetParent(root, false);
                var enemy = go.AddComponent<Enemy>();
                enemy.Init(this, EnemyDefs.Get(spawn.id), FindSpawnPoint(), Config, Run.Rng, EnemyScaling.For(Depth, spawn.elite));
                Enemies.Add(enemy);
            }
        }

        void RefreshAdaptation()
        {
            adaptationTimer = 0;
            Adaptation = Run != null ? Run.Profile.Traits(DifficultyRules.AdaptStrength(Depth)) : default;
        }

        /// <summary>Each room has its own size and biome; the old arena (and its texture) is discarded.</summary>
        void RebuildArena(float width, float height, Biome biome)
        {
            if (arenaRoot != null)
            {
                if (Decor != null && Decor.FloorTexture != null) Object.Destroy(Decor.FloorTexture);
                Object.Destroy(arenaRoot.gameObject);
            }
            arenaRoot = new GameObject("Arena").transform;
            arenaRoot.SetParent(root, false);
            Bounds = new Rect(-width * 0.5f, -height * 0.5f, width, height);
            Decor = ArenaBuilder.Build(arenaRoot, Config, width, height, biome);
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
            Rect inner = new Rect(Bounds.xMin + 1.5f, Bounds.yMin + 1.5f, Bounds.width - 3f, Bounds.height - 3f);
            Vector2 best = inner.center;
            float bestScore = float.MinValue;
            for (int attempt = 0; attempt < 24; attempt++)
            {
                var p = new Vector2(Run.Rng.Range(inner.xMin, inner.xMax), Run.Rng.Range(inner.yMin, inner.yMax));
                float fromPlayer = Vector2.Distance(p, Player.Position);
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
            bursts.Clear();
            ClearProjectiles();
            ClearPickups();
            Coordinator.Reset();
        }

        // ── Combat resolution ───────────────────────────────────────────────────────────

        /// <summary>Called by an enemy on each of its active frames until it connects once.</summary>
        public bool TryHitPlayer(Enemy attacker, AttackDef attack)
        {
            if (!Player.IsAlive) return false;
            if (!CombatMath.Overlaps(attack.shape, attacker.Position, attacker.Facing, Player.Position, Player.Radius)) return false;
            var incoming = new IncomingAttack
            {
                attacker = attacker,
                attack = attack,
                source = attacker.Position,
                signature = attacker.CurrentMove != null && attacker.CurrentMove.isSignature,
                powerMult = attacker.PowerMult,
            };
            Player.ReceiveAttack(incoming);
            return true;
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

        public void ScheduleBurst(Vector2 position, int delayFrames, float radius, float damage, float posture, Color? color = null)
        {
            bursts.Add(new Burst
            {
                position = position, framesLeft = delayFrames, radius = radius, damage = damage, posture = posture,
                color = color ?? Config.palette.playerAccent,
            });
            Fx.Emit(FxType.Afterimage, position, Player.Facing, Player.Radius * 1.3f, Config.palette.gold);
        }

        void Strike(Vector2 origin, Vector2 direction, HitShape shape, float damage, float posture,
                    HashSet<Enemy> alreadyHit, bool unblockable, bool applyRewardHooks, float pull = 0f)
        {
            for (int i = 0; i < Enemies.Count; i++)
            {
                Enemy e = Enemies[i];
                if (!e.CanBeHit) continue;
                if (alreadyHit != null && alreadyHit.Contains(e)) continue;
                if (!CombatMath.Overlaps(shape, origin, direction, e.Position, e.Radius)) continue;
                alreadyHit?.Add(e);

                float finalDamage = damage;
                float finalPosture = posture;
                bool crit = false;
                if (applyRewardHooks)
                {
                    for (int r = 0; r < Run.Effects.Count; r++)
                    {
                        Run.Effects[r].ModifyAttackDamage(this, e, ref finalDamage);
                        Run.Effects[r].ModifyAttackPosture(this, e, ref finalPosture);
                    }
                    if (Run.Modifiers.critChance > 0f && Run.Rng.NextFloat() < Run.Modifiers.critChance)
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
                if (pull > 0f) e.Pull(Player.Position, pull);
                if (Run.Modifiers.lifesteal > 0f) Player.Heal(finalDamage * Run.Modifiers.lifesteal);
                for (int r = 0; r < Run.Effects.Count; r++) Run.Effects[r].OnDealtDamage(this, e, finalDamage);
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
                Fx.Emit(FxType.Burst, b.position, Vector2.up, b.radius, b.color);
                Strike(b.position, Vector2.right, HitShape.Circle(b.radius), b.damage, b.posture, null, true, false);
            }
        }

        /// <summary>A blow inside a finisher: blood, a freeze and a wet cut — but no death yet.</summary>
        public void FinisherImpact(Enemy enemy, Vector2 dir, float intensity)
        {
            Fx.Emit(FxType.FinisherHit, enemy.Position, dir, intensity, enemy.Def.bloodColor);
            Fx.Blood(enemy.Position, dir, 10f + 18f * intensity, enemy.Def.bloodColor);
        }

        public void ExecuteFinisher(Enemy enemy, FinisherStyle style, Vector2 dir)
        {
            if (enemy == null || enemy.CurrentState != Enemy.State.Finishing) return;

            Run.Finishers++;
            if (dir.sqrMagnitude < 1e-4f) dir = Player.Facing;
            // Magnitude 2 marks the last enemy of the room: the router plays the slow-motion kill.
            bool lastOne = ActiveEnemyCount <= 1 && IsLastWave;
            Fx.Emit(FxType.Finisher, enemy.Position, dir, lastOne ? 2f : 1f, enemy.Def.bloodColor);
            Fx.Popup(Run.CurrentWeapon.finisherName.ToUpperInvariant(), enemy.Position + Vector2.up * 1f, Color.white);
            Fx.Gore(style, enemy.Position, dir, enemy.BodySprite, enemy.BodyColor, enemy.Def.bloodColor, enemy.Radius);
            bool absorb = enemy.Mastered && Run.CanAbsorb(enemy.Def.techniqueId);
            enemy.DieByFinisher();
            AddFlow(Config.flow.finisherPoints);
            for (int r = 0; r < Run.Effects.Count; r++) Run.Effects[r].OnFinisher(this, enemy);

            float heal = Run.Modifiers.finisherHeal + (Run.Flow.Level >= Run.Flow.MaxLevel ? Config.flow.level3FinisherHeal : 0f);
            if (heal > 0f)
            {
                Player.Heal(heal);
                Fx.Popup("+" + Mathf.RoundToInt(heal), Player.Position, Config.palette.health);
            }

            if (!absorb) return;
            Run.AddPendingTechnique(enemy.Def.techniqueId);
            RewardDef preview = RewardRegistry.Create(enemy.Def.techniqueId);
            Fx.Emit(FxType.TechniqueAbsorbed, enemy.Position, Player.Position - enemy.Position, 1f, Config.palette.gold);
            Fx.Toast(string.Format(Strings.ToastTechnique, preview != null ? preview.Name : enemy.Def.techniqueId), Config.palette.gold);
        }

        /// <summary>Marks the enemy for absorption when the player gives its signature the Perfect Answer.</summary>
        public void TryMaster(Enemy attacker, SignatureAnswer answer, bool fromSignature)
        {
            if (Run == null || attacker == null || attacker.IsGone || attacker.Mastered || !fromSignature) return;
            if (attacker.Def.signatureAnswer != answer || !Run.CanAbsorb(attacker.Def.techniqueId)) return;
            attacker.MarkMastered();
            Fx.Emit(FxType.Mastered, attacker.Position, Vector2.up, 1f, Config.palette.gold);
            Fx.Popup(Strings.PopupMastered, attacker.Position + Vector2.up * 0.9f, Config.palette.gold);
            HintOnce(HintId.Mastery);
        }

        /// <summary>A perfect action extends the streak; milestones get their own fanfare.</summary>
        public void RegisterPerfect(Vector2 where)
        {
            if (Run == null) return;
            int streak = Run.RegisterPerfect();
            HintOnce(HintId.SwapParry);
            if (streak >= 5 && streak % 5 == 0) Fx.Emit(FxType.StreakMilestone, where, Vector2.up, streak, Config.palette.gold);
        }

        public void AddFlow(float points)
        {
            if (Run == null) return;
            int before = Run.Flow.Level;
            int after = Run.Flow.Add(points * Run.Modifiers.flowGainMult);
            if (after > before) Fx.Emit(FxType.FlowUp, Player.Position, Vector2.up, after, Config.palette.gold);
        }

        public void DropFlow(int levels)
        {
            if (Run == null) return;
            int before = Run.Flow.Level;
            int after = Run.Flow.DropLevels(levels);
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
            Vector2 behind = Player.Position - Player.Facing * Config.enemies.teleportBehindDistance;
            float r = enemy.Radius;
            behind = new Vector2(Mathf.Clamp(behind.x, Bounds.xMin + r, Bounds.xMax - r), Mathf.Clamp(behind.y, Bounds.yMin + r, Bounds.yMax - r));
            Fx.Emit(FxType.ShadowStep, from, Vector2.up, enemy.Radius, enemy.Def.color);
            enemy.Teleport(behind);
            Vector2 to = Player.Position - behind;
            if (to.sqrMagnitude > 1e-4f) enemy.Facing = to.normalized;
            Fx.Emit(FxType.ShadowStep, behind, Vector2.up, enemy.Radius, enemy.Def.color);
        }

        // ── Projectiles ─────────────────────────────────────────────────────────────────

        // ── Equipment effects ───────────────────────────────────────────────────────────

        /// <summary>Lightning jumps from the struck enemy to the nearest others.</summary>
        public void ChainLightning(Enemy from, int jumps, float damage, float posture)
        {
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
        }

        /// <summary>Shadow items: a blade of darkness falls on whoever was parried.</summary>
        public void ShadowStrike(Enemy target, float damage, float posture)
        {
            if (target == null || !target.CanBeHit) return;
            ScheduleBurst(target.Position, 12, 1.1f, damage, posture, Elements.Color(Element.Shadow));
        }

        // ── Loot ────────────────────────────────────────────────────────────────────────

        /// <summary>Called by every enemy death: may leave an item behind (Guardians always do).</summary>
        public void OnEnemyDied(Enemy enemy)
        {
            if (Run == null || CurrentRoom == null) return;
            if (Run.Rng.NextFloat() >= ItemGenerator.DropChance(Depth, enemy.IsElite)) return;
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
                Frame = SpriteUtil.Make("Frame", go.transform, ShapeLibrary.Get(Shape.Ring), rc, Sorting.Overlay - 18),
                Icon = SpriteUtil.Make("Icon", go.transform, ShapeLibrary.Get(ItemGenerator.IconShape(item.slot)), Elements.Color(item.element), Sorting.Overlay - 19),
            };
            // Rarer loot stands taller: you can read a drop's worth from across the arena.
            float beamHeight = 1.2f + 0.45f * tier;
            pickup.Beam.transform.localPosition = new Vector3(0f, beamHeight * 0.5f, 0f);
            pickup.Beam.transform.localScale = new Vector3(0.1f + 0.015f * tier, beamHeight, 1f);
            pickup.Glow.transform.localScale = Vector3.one * (1.2f + 0.15f * tier);
            pickup.Frame.transform.localScale = Vector3.one * 0.62f;
            pickup.Icon.transform.localScale = Vector3.one * 0.38f;
            Pickups.Add(pickup);
            Fx.Emit(FxType.ItemDrop, position, Vector2.up, tier, rc);
        }

        void TickPickups()
        {
            if (!Player.IsAlive) return;
            for (int i = Pickups.Count - 1; i >= 0; i--)
            {
                ItemPickup p = Pickups[i];
                p.Age += SimClock.Dt;
                if (p.Age < 0.4f || Vector2.Distance(p.Position, Player.Position) > PickupRadius) continue;
                Collect(p);
                Pickups.RemoveAt(i);
            }
        }

        void Collect(ItemPickup p)
        {
            bool equipped = Run.AddItem(p.Item);
            if (equipped) Player.ApplyStats(false);
            Fx.ItemCollected(p.Item, equipped, p.Position);
            Object.Destroy(p.Root.gameObject);
        }

        /// <summary>Room cleared: nothing on the floor is ever lost.</summary>
        void CollectAllPickups()
        {
            foreach (ItemPickup p in Pickups) Collect(p);
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
            Vector2 dir = owner.Facing;
            Vector2 start = owner.Position + dir * (owner.Radius + 0.3f);
            Projectile p = CreateProjectile(start, dir, attack.projectileSpeed, attack.projectileRadius, owner.Def.weaponColor);
            p.Attack = attack;
            p.Owner = owner;
            p.Signature = signature;
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
                p.Position += p.Direction * p.Speed * SimClock.Dt;
                p.Life--;

                Rect outer = new Rect(Bounds.xMin - 0.5f, Bounds.yMin - 0.5f, Bounds.width + 1f, Bounds.height + 1f);
                bool dead = p.Life <= 0 || !outer.Contains(p.Position);

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
            if (p.PassedPlayer || !Player.IsAlive) return false;

            if (!p.Cued && p.FramesToImpact(Player) <= Config.feel.timingCueLeadFrames)
            {
                p.Cued = true;
                Fx.Emit(FxType.TimingCue, p.Position, p.Direction, 0f);
            }

            if (Vector2.Distance(p.Position, Player.Position) > Player.Radius + p.Radius) return false;
            var incoming = new IncomingAttack
            {
                attacker = p.Owner,
                attack = p.Attack,
                source = p.Position - p.Direction,
                signature = p.Signature,
                projectile = true,
                powerMult = p.Owner != null ? p.Owner.PowerMult : 1f,
            };
            HitOutcome outcome = Player.ReceiveAttack(incoming);
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

        bool TickFriendlyProjectile(Projectile p)
        {
            for (int i = 0; i < Enemies.Count; i++)
            {
                Enemy e = Enemies[i];
                if (!e.CanBeHit || p.Struck.Contains(e)) continue;
                if (Vector2.Distance(p.Position, e.Position) > e.Radius + p.Radius) continue;
                p.Struck.Add(e);
                e.ReceivePlayerHit(p.Damage, p.Posture, p.Direction, true);
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
                if (!e.CanBeHit) continue;
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
                if (!e.IsBroken) continue;
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
            bool playerSolid = Player.CurrentState != PlayerController.State.Dodge && Player.IsAlive;
            for (int i = 0; i < Enemies.Count; i++)
            {
                Enemy a = Enemies[i];
                if (!IsSolid(a)) continue;
                for (int j = i + 1; j < Enemies.Count; j++)
                {
                    if (IsSolid(Enemies[j])) Separate(a, Enemies[j]);
                }
                if (playerSolid) Separate(Player, a);
            }

            ClampToArena(Player);
            for (int i = 0; i < Enemies.Count; i++) ClampToArena(Enemies[i]);
        }

        static bool IsSolid(Enemy e)
        {
            return !e.IsGone && e.CurrentState != Enemy.State.Spawning;
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

        void ClampToArena(Fighter f)
        {
            float r = f.Radius;
            f.Position = new Vector2(
                Mathf.Clamp(f.Position.x, Bounds.xMin + r, Bounds.xMax - r),
                Mathf.Clamp(f.Position.y, Bounds.yMin + r, Bounds.yMax - r));
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
                p.Frame.transform.localRotation = Quaternion.Euler(0f, 0f, Time.unscaledTime * 60f);
                Color b = p.Beam.color;
                b.a = 0.25f + 0.15f * Mathf.Sin(t * 1.7f);
                p.Beam.color = b;
            }
            Player.Render(alpha, visualDt, showHitboxes);
            for (int i = 0; i < Enemies.Count; i++) Enemies[i].Render(alpha, visualDt, showHitboxes);
            for (int i = 0; i < Projectiles.Count; i++) RenderProjectile(Projectiles[i], alpha);
        }

        /// <summary>Arrows carry their own "press now" ring that closes as they reach you.</summary>
        void RenderProjectile(Projectile p, float alpha)
        {
            Vector2 pos = Vector2.LerpUnclamped(p.PrevPosition, p.Position, alpha);
            Transform holder = p.View.transform.parent;
            holder.position = new Vector3(pos.x, pos.y, 0f);
            float angle = CombatMath.AngleDegrees(p.Direction);
            p.View.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            p.View.transform.localPosition = p.IsArrow ? -(Vector3)(p.Direction * p.Size) : Vector3.zero;
            p.View.transform.localScale = new Vector3(p.Size, p.Size, 1f);

            bool hostile = !p.Reflected && !p.PassedPlayer && Player.IsAlive;
            float frames = hostile ? p.FramesToImpact(Player) : float.PositiveInfinity;
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
