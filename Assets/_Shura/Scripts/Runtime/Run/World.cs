using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shura
{
    /// <summary>
    /// Owns the arena, the fighters and the room/wave flow, and resolves every hit. Tick
    /// order is fixed (player, enemies, effects, collisions, waves) so runs are reproducible.
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
        }

        public readonly GameConfig Config;
        public readonly IFeedback Fx;
        public readonly AttackCoordinator Coordinator;
        public readonly List<Enemy> Enemies = new List<Enemy>();

        readonly Transform root;
        readonly List<Burst> bursts = new List<Burst>();
        readonly HashSet<HintId> hintsShown = new HashSet<HintId>();
        int waveDelay;
        bool roomActive;
        bool deathReported;

        public PlayerController Player { get; private set; }
        public RunState Run { get; private set; }
        public Rect Bounds { get; private set; }
        public RoomDef CurrentRoom { get; private set; }
        public int WaveIndex { get; private set; }
        public bool RoomCleared { get; private set; }
        public bool PlayerInvincible { get; set; }

        public event Action RoomClearedEvent;
        public event Action PlayerDiedEvent;

        public World(GameConfig config, IFeedback fx)
        {
            Config = config;
            Fx = fx;
            Coordinator = new AttackCoordinator(config.enemies.attackGapFrames);
            Bounds = new Rect(-config.arena.width * 0.5f, -config.arena.height * 0.5f, config.arena.width, config.arena.height);

            root = new GameObject("World").transform;
            ArenaBuilder.Build(root, config);

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

        public void StartRoom(int index)
        {
            ClearEnemies();
            CurrentRoom = RoomDefs.Prototype[Mathf.Clamp(index, 0, RoomDefs.Prototype.Length - 1)];
            WaveIndex = -1;
            waveDelay = Config.enemies.firstWaveDelay;
            RoomCleared = false;
            roomActive = true;
            Player.ResetForRoom(PlayerSpawn);
            if (index == 0) HintOnce(HintId.Controls);
        }

        public void Tick()
        {
            if (Run == null) return;
            Coordinator.Tick();
            Player.Tick();
            for (int i = 0; i < Enemies.Count; i++) Enemies[i].Tick();
            TickBursts();
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
            RoomClearedEvent?.Invoke();
        }

        void SpawnWave(string[] enemyIds)
        {
            foreach (string id in enemyIds)
            {
                var go = new GameObject("Enemy_" + id);
                go.transform.SetParent(root, false);
                var enemy = go.AddComponent<Enemy>();
                enemy.Init(this, EnemyDefs.Get(id), FindSpawnPoint(), Config, Run.Rng);
                Enemies.Add(enemy);
            }
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
            Coordinator.Reset();
        }

        // ── Combat resolution ───────────────────────────────────────────────────────────

        /// <summary>Called by an enemy on each of its active frames until it connects once.</summary>
        public bool TryHitPlayer(Enemy attacker, AttackDef attack)
        {
            if (!Player.IsAlive) return false;
            if (!CombatMath.Overlaps(attack.shape, attacker.Position, attacker.Facing, Player.Position, Player.Radius)) return false;
            Player.ReceiveAttack(attacker, attack);
            return true;
        }

        public void PlayerStrike(HitShape shape, float damage, float posture, HashSet<Enemy> alreadyHit)
        {
            Strike(Player.Position, Player.Facing, shape, damage, posture, alreadyHit, false, true);
        }

        /// <summary>Technique-driven strike that resolves immediately and cannot be blocked.</summary>
        public void PlayerInstantStrike(Vector2 origin, Vector2 direction, HitShape shape, float damage, float posture)
        {
            Fx.Emit(FxType.MirrorThrust, origin, direction, shape.range, Config.palette.playerAccent);
            Strike(origin, direction, shape, damage, posture, null, true, false);
        }

        public void ScheduleBurst(Vector2 position, int delayFrames, float radius, float damage, float posture)
        {
            bursts.Add(new Burst { position = position, framesLeft = delayFrames, radius = radius, damage = damage, posture = posture });
            Fx.Emit(FxType.Afterimage, position, Player.Facing, Player.Radius * 1.3f, Config.palette.gold);
        }

        void Strike(Vector2 origin, Vector2 direction, HitShape shape, float damage, float posture,
                    HashSet<Enemy> alreadyHit, bool unblockable, bool applyRewardHooks)
        {
            for (int i = 0; i < Enemies.Count; i++)
            {
                Enemy e = Enemies[i];
                if (!e.CanBeHit) continue;
                if (alreadyHit != null && alreadyHit.Contains(e)) continue;
                if (!CombatMath.Overlaps(shape, origin, direction, e.Position, e.Radius)) continue;
                alreadyHit?.Add(e);

                float finalPosture = posture;
                if (applyRewardHooks)
                {
                    for (int r = 0; r < Run.Owned.Count; r++) Run.Owned[r].ModifyAttackPosture(this, e, ref finalPosture);
                }
                Vector2 hitDir = e.Position - origin;
                hitDir = hitDir.sqrMagnitude > 1e-4f ? hitDir.normalized : direction;
                e.ReceivePlayerHit(damage, finalPosture, hitDir, unblockable);
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
                Fx.Emit(FxType.Burst, b.position, Vector2.up, b.radius, Config.palette.playerAccent);
                Strike(b.position, Vector2.right, HitShape.Circle(b.radius), b.damage, b.posture, null, true, false);
            }
        }

        public void ExecuteFinisher(Enemy enemy)
        {
            if (enemy == null || enemy.CurrentState != Enemy.State.Finishing) return;

            Run.Finishers++;
            Vector2 dir = enemy.Position - Player.Position;
            Fx.Emit(FxType.Finisher, enemy.Position, dir.sqrMagnitude > 1e-4f ? dir.normalized : Player.Facing, enemy.Radius, enemy.Def.color);
            bool absorb = enemy.Mastered && Run.CanAbsorb(enemy.Def.techniqueId);
            enemy.Die();
            AddFlow(Config.flow.finisherPoints);

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
        public void TryMaster(Enemy attacker, SignatureAnswer answer)
        {
            if (Run == null || attacker.Mastered || attacker.CurrentMove == null || !attacker.CurrentMove.isSignature) return;
            if (attacker.Def.signatureAnswer != answer || !Run.CanAbsorb(attacker.Def.techniqueId)) return;
            attacker.MarkMastered();
            Fx.Emit(FxType.Mastered, attacker.Position, Vector2.up, 1f, Config.palette.gold);
            Fx.Popup(Strings.PopupMastered, attacker.Position + Vector2.up * 0.9f, Config.palette.gold);
            HintOnce(HintId.Mastery);
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
            Player.Render(alpha, visualDt, showHitboxes);
            for (int i = 0; i < Enemies.Count; i++) Enemies[i].Render(alpha, visualDt, showHitboxes);
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
