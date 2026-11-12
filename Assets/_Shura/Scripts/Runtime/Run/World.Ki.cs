using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>
    /// The world's side of the new combat layers: tools, the group war cry, posture
    /// resonance between teammates, feints that punish early parries, the Iai standoff and
    /// the Inner Asura — plus the rule knobs that biomes, weather and oaths turn.
    /// </summary>
    public sealed partial class World
    {
        // ── Rule knobs (biomes, weather, oaths and assists set these) ─────────────────
        /// <summary>Breath regenerates this much faster (cold drains it).</summary>
        public float StaminaRegenMult { get; set; } = 1f;
        /// <summary>Shura rises this much faster (the full moon).</summary>
        public float ShuraGainMult { get; set; } = 1f;
        /// <summary>Dodges travel this much farther (wet moss).</summary>
        public float DodgeDistanceMult { get; set; } = 1f;
        /// <summary>Accessibility assist: extra perfect-window frames for everyone.</summary>
        public int ParryAssistFrames { get; set; }
        /// <summary>How fast the enemies read you (the "Leitura Profunda" oath raises it).</summary>
        public float AdaptationSpeed { get; set; } = 1f;

        public readonly Standoff Standoff;

        // ── Group war cry ─────────────────────────────────────────────────────────────

        readonly Dictionary<PlayerController, float> kiaiTimes = new Dictionary<PlayerController, float>();

        /// <summary>Three duelists crying out within a second break the posture of every enemy in sight.</summary>
        public void RegisterKiai(PlayerController p)
        {
            if (Run == null) return;
            kiaiTimes[p] = Run.Elapsed;
            int together = 0;
            foreach (KeyValuePair<PlayerController, float> k in kiaiTimes)
            {
                if (Run.Elapsed - k.Value <= 1f) together++;
            }
            if (together < 3) return;
            kiaiTimes.Clear();
            foreach (Enemy e in Enemies)
            {
                if (e.CanBeHit) e.ApplyPosture(e.Posture.Max * 3f);
            }
            Fx.Emit(FxType.Kiai, CameraFocus.Position, Vector2.up, 12f, Config.palette.gold);
            Fx.Toast(Strings.ToastGroupKiai, Config.palette.gold);
        }

        // ── Posture resonance (several duelists parrying the same enemy) ──────────────

        readonly Dictionary<Enemy, Dictionary<PlayerController, float>> resonance = new Dictionary<Enemy, Dictionary<PlayerController, float>>();

        public void RegisterResonance(Enemy e, PlayerController p)
        {
            if (Run == null || e == null) return;
            if (!resonance.TryGetValue(e, out Dictionary<PlayerController, float> map))
            {
                map = new Dictionary<PlayerController, float>();
                resonance[e] = map;
            }
            map[p] = Run.Elapsed;
        }

        /// <summary>×1.25 per other duelist who parried this enemy in the last 3 s (up to ×2).</summary>
        public float ResonanceMult(Enemy e)
        {
            if (Run == null || e == null || !resonance.TryGetValue(e, out Dictionary<PlayerController, float> map)) return 1f;
            int n = 0;
            foreach (float t in map.Values)
            {
                if (Run.Elapsed - t <= 3f) n++;
            }
            if (n <= 1) return 1f;
            Fx.Popup(string.Format(Strings.PopupResonance, n), e.Position + Vector2.up * 1.6f, Config.palette.gold);
            return Mathf.Min(2f, Mathf.Pow(1.25f, n - 1));
        }

        // ── Feints ────────────────────────────────────────────────────────────────────

        /// <summary>A feint "lands": anyone who answered it with a parry is exposed.</summary>
        public void PunishFeintParry(Enemy e, AttackDef feint)
        {
            float reach = feint.shape.range + feint.lunge + 1.5f;
            foreach (PlayerController p in Players)
            {
                if (!p.IsAlive || !p.InPerfectWindow) continue;
                if (Vector2.Distance(p.Position, e.Position) > reach) continue;
                actor = p;
                p.FooledByFeint();
                actor = null;
            }
        }

        // ── Tools ─────────────────────────────────────────────────────────────────────

        public void ThrowShuriken(PlayerController p, Vector2 aim)
        {
            Sprite art = PixelArt.ProjectileSprite("fx_shuriken");
            Projectile pr = CreateProjectile(p.Position + aim * (p.Radius + 0.3f), aim, 17f, 0.25f, Color.white, art, 1f, false);
            pr.Reflected = true;
            pr.Damage = 8f;
            pr.Posture = 6f;
            pr.Art = "fx_shuriken";
            pr.Spin = 900f;
            pr.Interrupts = true;
            Fx.Emit(FxType.EnemySwing, p.Position, aim, 2f);
        }

        public void SmokeBomb(Vector2 at)
        {
            Fx.Emit(FxType.Smoke, at, Vector2.up, 2.4f, Color.white);
            Fx.Emit(FxType.Smoke, at + Vector2.right * 1.2f, Vector2.up, 1.6f, Color.white);
            Fx.Emit(FxType.Smoke, at - Vector2.right * 1.2f, Vector2.up, 1.6f, Color.white);
            foreach (Enemy e in Enemies)
            {
                if (Vector2.Distance(e.Position, at) <= 3.6f) e.Confuse(180);
            }
            // The smoke also clears what the enemies had read about you.
            Run?.Profile.Reset();
            RefreshAdaptation();
        }

        public void BronzeBell(Vector2 at)
        {
            Fx.Emit(FxType.Bell, at, Vector2.up, 4.5f, Config.palette.gold);
            foreach (Enemy e in Enemies)
            {
                if (!e.CanBeHit && !e.Def.ethereal) continue;
                if (Vector2.Distance(e.Position, at) > 4.5f) continue;
                e.ForceExpose(240);
                e.ApplyPosture(5f);
            }
        }

        public void Firecracker(Vector2 at)
        {
            at = Layout.NearestFloor(at, 0.2f);
            ScheduleBurst(at, 20, 1.7f, 6f, 10f, ColorUtil.Hex("#FF6B35"), true, 90);
        }

        public void HookChain(PlayerController p, Vector2 aim)
        {
            Enemy target = FindAssistTarget(p.Position, aim, 40f, 7.5f, false);
            if (target == null)
            {
                Fx.Popup(Strings.PopupMissed, p.Position + aim * 2f, Config.palette.uiDim);
                return;
            }
            Fx.Emit(FxType.Lightning, p.Position, (target.Position - p.Position).normalized, Vector2.Distance(p.Position, target.Position), ColorUtil.Hex("#C8C2B8"));
            target.Pull(p.Position, 6f);
            target.Stun(20);
        }

        // ── Iai standoff ──────────────────────────────────────────────────────────────

        /// <summary>The first wave of a standoff room: the leader faces the duelists; the rest wait.</summary>
        void BeginStandoffWith(List<Enemy> wave)
        {
            Enemy leader = null;
            foreach (Enemy e in wave)
            {
                e.HeldInSpawn = true;
                if (leader == null && !e.Def.minion && !e.IsElite) leader = e;
            }
            if (leader == null)
            {
                foreach (Enemy e in wave) e.HeldInSpawn = false;
                return;
            }
            int feints = Mathf.Min(3, Run.Rng.Range(0, 2 + Depth / 4));
            Standoff.Begin(leader, feints, Run.Rng);
        }

        /// <summary>A clean draw: the enemy falls in one cut.</summary>
        public void IaiKill(PlayerController p, Enemy e)
        {
            PlayerController before = actor;
            actor = p;
            Fx.Emit(FxType.Finisher, e.Position, (e.Position - p.Position).normalized, 1f, e.Def.bloodColor);
            Fx.Gore(FinisherStyle.CleanCut, e.Position, (e.Position - p.Position).normalized, e.BodySprite, e.BodyColor, e.Def.bloodColor, e.Radius);
            Fx.Popup(Strings.PopupIai, e.Position + Vector2.up * 1.4f, Config.palette.gold);
            e.HeldInSpawn = false;
            e.DieByFinisher();
            AddFlow(Config.flow.finisherPoints);
            actor = before;
        }

        // ── Elite affixes (depth 15+) ─────────────────────────────────────────────────

        /// <summary>From depth 15, Guardians carry 1 to 3 affixes that change how they fight.</summary>
        void ApplyEliteAffixes(Enemy e, EnemySpawn spawn)
        {
            if (!spawn.elite || Run == null) return;
            bool early = Run.HasRule(KalpaRule.EarlyAffixes);
            bool natural = Depth >= 15 || early;
            if (!natural && ExtraEliteAffixes == 0) return;
            int n = Mathf.Clamp((natural ? 1 + Mathf.Max(0, Depth - 15) / 10 : 0) + ExtraEliteAffixes + (early ? 1 : 0), 1, 4);
            var pool = new List<EliteAffix>((EliteAffix[])System.Enum.GetValues(typeof(EliteAffix)));
            for (int i = 0; i < n && pool.Count > 0; i++)
            {
                int k = Run.Rng.Range(0, pool.Count);
                e.AddAffix(pool[k]);
                pool.RemoveAt(k);
            }
            HintOnce(HintId.EliteAffixes);
        }

        /// <summary>The "Sino do Demônio" oath: elites carry one affix more.</summary>
        public int ExtraEliteAffixes { get; set; }

        /// <summary>Twin affix: a second body that shares the first one's health.</summary>
        public void SpawnTwin(Enemy original)
        {
            Vector2 at = Layout.NearestFloor(original.Position + CombatMath.Perpendicular(original.Facing) * 1.6f, original.Radius);
            pendingTwins.Add(original);
            pendingTwinPositions.Add(at);
        }

        readonly List<Enemy> pendingTwins = new List<Enemy>();
        readonly List<Vector2> pendingTwinPositions = new List<Vector2>();

        void FlushTwins()
        {
            for (int i = 0; i < pendingTwins.Count; i++)
            {
                Enemy original = pendingTwins[i];
                if (original.IsGone) continue;
                Enemy twin = CreateEnemy(original.Def, pendingTwinPositions[i], false, null);
                twin.TwinOf = original;
            }
            pendingTwins.Clear();
            pendingTwinPositions.Clear();
        }

        /// <summary>Mirrored affix: part of the blow comes back to whoever struck.</summary>
        public void ReflectToStriker(Enemy from, float damage)
        {
            PlayerController p = Player;
            if (p == null || !p.IsAlive) return;
            Vector2 dir = (p.Position - from.Position).normalized;
            p.ReceiveHazard(damage, 0f, dir);
        }

        // ── The Inner Asura ───────────────────────────────────────────────────────────

        /// <summary>After the fall into the Shuradō: a dark copy of the duelist, with their own build.</summary>
        public Enemy SpawnInnerAsura(PlayerController owner)
        {
            EnemyDef def = EnemyDefs.InnerAsura(owner);
            Vector2 at = Layout.NearestFloor(owner.Position + owner.Facing * 3f, def.radius);
            Enemy e = CreateEnemy(def, at, false, null);
            if (owner.Build != null) e.ScaleHealth(owner.Build.Modifiers.innerAsuraHealthMult);
            e.AsuraOwner = owner;
            e.Target = owner;
            Fx.Toast(string.Format(Strings.ToastInnerAsura, owner.DisplayName), Config.palette.danger);
            HintOnce(HintId.InnerAsura);
            return e;
        }
    }
}
