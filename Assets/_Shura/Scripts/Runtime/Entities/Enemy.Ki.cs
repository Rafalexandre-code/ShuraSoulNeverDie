using UnityEngine;

namespace Shura
{
    /// <summary>Affixes a deep Guardian may carry (design doc §5.5).</summary>
    public enum EliteAffix
    {
        /// <summary>Bursts into blades when it dies.</summary>
        Vengeful,
        /// <summary>Sends back a fifth of the damage it takes.</summary>
        Mirrored,
        /// <summary>A spirit leaves the body when it falls.</summary>
        Possessed,
        /// <summary>Two bodies, one health.</summary>
        Twin,
        /// <summary>Its rings only show within 3 m.</summary>
        Veiled,
        /// <summary>Fights with the moves of two warriors.</summary>
        Ancestral,
    }

    public static class EliteAffixes
    {
        static readonly string[] Names = { "VINGATIVO", "ESPELHADO", "POSSUÍDO", "GÊMEO", "VELADO", "ANCESTRAL" };
        public static string Name(EliteAffix a) => Names[(int)a];
    }

    /// <summary>What a timing ring means (each has its own colour AND pattern, for colour-blind players).</summary>
    public enum RingKind
    {
        /// <summary>Blue, solid: parry.</summary>
        Parry = 0,
        /// <summary>Red with a diamond: dodge (thrust, sweep, grab).</summary>
        Danger = 1,
        /// <summary>Gold, dashed: a feint — the real blow comes after; parrying it opens you.</summary>
        Feint = 2,
        /// <summary>Two concentric rings: a held, two-beat blow; answer when the inner one closes.</summary>
        Double = 3,
        /// <summary>Purple, dotted: a spirit blow through the blade — only a Troca Perfeita (or a dodge).</summary>
        Spirit = 4,
        /// <summary>Green, ticked: cursed — parry is safe, blocking curses you.</summary>
        Cursed = 5,
        /// <summary>White, broken: a group blow — two duelists must parry together.</summary>
        Group = 6,
    }

    /// <summary>
    /// The enemy's side of the new combat layers (design doc §3 and §5): Ki seals planted by
    /// perfect parries, its own breath, wounds by body region, being shoved, thrown, disarmed
    /// and interrupted, brawling with its own kind, and the Iai standoff.
    /// </summary>
    public sealed partial class Enemy
    {
        public const int MaxSeals = 5;
        const float SealSeconds = 8f;
        const float MaxEnemyStamina = 30f;
        const int WindedFrames = 180;
        const int InfightFrames = 240;

        // ── Seals ─────────────────────────────────────────────────────────────────────
        public int Seals { get; private set; }
        float sealTimer;

        // ── Breath ────────────────────────────────────────────────────────────────────
        public float Stamina { get; private set; } = MaxEnemyStamina;
        public float StaminaRatio => Stamina / MaxEnemyStamina;
        /// <summary>Out of breath: no red blows for a while.</summary>
        public int Winded { get; private set; }

        // ── Wounds ────────────────────────────────────────────────────────────────────
        readonly int[] regionHits = new int[4];
        readonly bool[] wounded = new bool[4];
        public bool ArmWound => wounded[(int)WoundRegion.Arm];
        public bool LegWound => wounded[(int)WoundRegion.Leg];
        public bool TorsoWound => wounded[(int)WoundRegion.Torso];
        public bool EyesWound => wounded[(int)WoundRegion.Eyes];
        public int WoundMask => (wounded[0] ? 1 : 0) | (wounded[1] ? 2 : 0) | (wounded[2] ? 4 : 0) | (wounded[3] ? 8 : 0);
        float bleedAccumulator;

        // ── Shove, brawl, loot, standoff ──────────────────────────────────────────────
        Vector2 shoveDir;
        float shoveLeft;
        public Enemy InfightTarget { get; private set; }
        int infightFrames;
        /// <summary>The finishing cut went through the sword arm: twice the chance of an item.</summary>
        public bool DropsDouble { get; set; }
        /// <summary>Waits, fully drawn, until the standoff ends.</summary>
        public bool HeldInSpawn { get; set; }
        /// <summary>A shadow of the player's own build (the Inner Asura).</summary>
        public PlayerController AsuraOwner { get; set; }

        SpriteRenderer[] sealViews;
        static Sprite sealSprite;

        public RingKind CurrentRingKind
        {
            get
            {
                AttackDef a = CurrentAttack;
                if (a == null) return RingKind.Parry;
                if (a.feint) return RingKind.Feint;
                if (a.groupStrike) return RingKind.Group;
                if (a.kind == AttackKind.Spirit) return RingKind.Spirit;
                if (a.IsDangerous) return RingKind.Danger;
                if (a.cursed) return RingKind.Cursed;
                if (stepAdapted) return RingKind.Double;
                return RingKind.Parry;
            }
        }

        /// <summary>It struck a duelist this life (the scroll's "win without its damage").</summary>
        public bool DamagedPlayer { get; set; }

        public void AddSeal(int count)
        {
            if (!world.Has(Unlockable.Seals)) return;
            if (!CanBeHit || count <= 0) return;
            Seals = Mathf.Min(MaxSeals, Seals + count);
            sealTimer = SealSeconds;
        }

        public void ClearSeals()
        {
            Seals = 0;
        }

        public void DrainStamina(float amount)
        {
            Stamina = Mathf.Max(0f, Stamina - amount);
            if (Stamina > 0f || Winded > 0) return;
            Winded = WindedFrames;
            world.Fx.Popup(Strings.PopupWinded, Position + Vector2.up * 1.3f, pal.playerAccent);
        }

        /// <summary>Per-tick upkeep of the new layers.</summary>
        void TickKi()
        {
            if (Seals > 0 && (sealTimer -= SimClock.Dt) <= 0f) Seals = 0;
            if (Winded > 0 && --Winded == 0) Stamina = MaxEnemyStamina * 0.5f;
            else if (Winded <= 0) Stamina = Mathf.Min(MaxEnemyStamina, Stamina + 6f * SimClock.Dt);
            if (infightFrames > 0 && --infightFrames == 0) InfightTarget = null;
            if (InfightTarget != null && InfightTarget.IsGone) InfightTarget = null;
            if (TorsoWound && CanBeHit)
            {
                bleedAccumulator += Mathf.Max(1f, MaxHealth * 0.01f) * SimClock.Dt;
                if (bleedAccumulator >= 2f)
                {
                    float dealt = bleedAccumulator;
                    bleedAccumulator = 0f;
                    Health -= dealt;
                    world.Fx.Emit(FxType.DotTick, Position, Vector2.up, dealt, Def.bloodColor);
                    if (Health <= 0f) Die();
                }
            }
            TickShove();
        }

        // ── Wounds ────────────────────────────────────────────────────────────────────

        /// <summary>Where a blow came from (relative to where this enemy faces) wounds that part.</summary>
        void RegisterHitRegion(Vector2 hitDir)
        {
            float angle = Vector2.Angle(Facing, -hitDir);
            WoundRegion region = angle < 50f ? WoundRegion.Torso : angle < 130f ? WoundRegion.Arm : WoundRegion.Leg;
            int r = (int)region;
            if (wounded[r] || ++regionHits[r] < 3) return;
            Wound(region);
        }

        public void Wound(WoundRegion region)
        {
            int r = (int)region;
            if (wounded[r]) return;
            wounded[r] = true;
            if (region == WoundRegion.Torso) Posture.SetMax(Posture.Max * 0.8f);
            world.Fx.Popup(Strings.WoundName(region), Position + Vector2.up * 1.5f, pal.gold);
            world.HintOnce(HintId.Wounds);
        }

        /// <summary>Wounded legs cannot leap, charge, burrow or vanish.</summary>
        bool LegsAllow(MoveDef m)
        {
            if (!LegWound) return true;
            foreach (AttackDef a in m.steps)
            {
                if (a.lunge >= 1.5f || a.teleportBehind || a.burrow || a.wallStun) return false;
            }
            return true;
        }

        // ── Being moved around ────────────────────────────────────────────────────────

        /// <summary>Pushed back: into a wall it loses posture; into another enemy, both go down; over an edge, it falls.</summary>
        public void Shove(Vector2 dir, float distance, PlayerController by)
        {
            if (!CanBeHit || dir.sqrMagnitude < 1e-4f) return;
            if (Def.superArmor || IsElite) distance *= 0.4f;
            shoveDir = dir.normalized;
            shoveLeft = Mathf.Max(shoveLeft, distance);
        }

        void TickShove()
        {
            if (shoveLeft <= 0f || IsGone) return;
            float step = Mathf.Min(shoveLeft, 0.3f);
            Vector2 next = Position + shoveDir * step;
            if (world.Layout.IsPitAt(next))
            {
                FallIntoPit();
                return;
            }
            if (!world.Layout.Clear(Position, next, Radius * 0.8f))
            {
                shoveLeft = 0f;
                world.Fx.Emit(FxType.SlamImpact, Position + shoveDir * Radius, -shoveDir, 1.4f);
                ApplyPosture(15f);
                return;
            }
            foreach (Enemy other in world.Enemies)
            {
                if (other == this || other.IsGone || !other.CanBeHit) continue;
                if (Vector2.Distance(other.Position, next) > other.Radius + Radius) continue;
                // Bowled over: both fall, and both blame each other.
                shoveLeft = 0f;
                Stun(50);
                other.Stun(50);
                if (Factions.Hostile(Def, other.Def))
                {
                    StartBrawl(other);
                    other.StartBrawl(this);
                }
                world.Fx.Emit(FxType.SlamImpact, (Position + other.Position) * 0.5f, shoveDir, 1.6f);
                world.Fx.Popup(Strings.PopupKnockedDown, other.Position + Vector2.up * 1.3f, pal.gold);
                return;
            }
            Position = next;
            shoveLeft -= step;
        }

        /// <summary>Over the edge: gone at once, with no technique and no item.</summary>
        void FallIntoPit()
        {
            shoveLeft = 0f;
            world.Fx.Emit(FxType.Smoke, Position, Vector2.down, Radius, Def.color);
            world.Fx.Popup(Strings.PopupFell, Position + Vector2.up, pal.gold);
            world.Coordinator.Release(this);
            CurrentMove = null;
            Health = 0f;
            gibbed = true;
            Enter(State.Dying);
            world.NotifyKill(this);
        }

        /// <summary>Reversão: thrown over the shoulder — into the floor, a trap or another enemy.</summary>
        public void Throw(Vector2 dir, PlayerController by)
        {
            Stun(70);
            ReceivePlayerHit(18f, 30f, dir, true);
            if (!IsGone) Shove(dir, 3.5f, by);
        }

        public void Stun(int frames)
        {
            if (!CanBeHit || IsBroken || CurrentState == State.Reassembling) return;
            world.Coordinator.Release(this);
            CurrentMove = null;
            staggerFrames = Mathf.Max(frames, CurrentState == State.Stagger ? staggerFrames - StateFrame : 0);
            Enter(State.Stagger);
        }

        /// <summary>Pisar na Lâmina: the weapon is pinned, the enemy defenceless for a moment.</summary>
        public void Disarm(int frames)
        {
            Stun(frames);
            world.Fx.Popup(Strings.PopupDisarmed, Position + Vector2.up * 1.6f, pal.gold);
        }

        /// <summary>Kiai: the cry breaks the blow in the air.</summary>
        public void Interrupt(float posture, int frames)
        {
            ApplyPosture(posture);
            if (!IsBroken && !IsGone) Stun(frames);
        }

        // ── Brawls between enemies ────────────────────────────────────────────────────

        /// <summary>Struck by one of its own (a reflected arrow, a thrown body): it turns on them for a while.</summary>
        public void StartBrawl(Enemy other)
        {
            if (other == null || other == this || other.IsGone || Def.minion) return;
            InfightTarget = other;
            infightFrames = InfightFrames;
            world.HintOnce(HintId.Infight);
        }

        /// <summary>Whom to steer toward and strike: a rival enemy while brawling, else the chosen duelist.</summary>
        Vector2 FoePosition => InfightTarget != null ? InfightTarget.Position : (Target != null ? Target.Position : world.Host.Position);

        /// <summary>A blow aimed at a rival enemy lands on them too.</summary>
        void HitBrawlTarget(AttackDef attack)
        {
            Enemy foe = InfightTarget;
            if (foe == null || foe.IsGone || !foe.CanBeHit) return;
            if (!CombatMath.Overlaps(attack.shape, Position, Facing, foe.Position, foe.Radius)) return;
            Vector2 dir = (foe.Position - Position).normalized;
            foe.ReceiveHazard(attack.damage * PowerMult, attack.postureDamage * PowerMult, dir);
            if (foe.IsGone)
            {
                Inherit();
                return;
            }
            foe.StartBrawl(this);
        }

        // ── Elite affixes ─────────────────────────────────────────────────────────────

        readonly System.Collections.Generic.List<EliteAffix> affixes = new System.Collections.Generic.List<EliteAffix>();
        public System.Collections.Generic.IReadOnlyList<EliteAffix> Affixes => affixes;
        public bool HasAffix(EliteAffix a) => affixes.Contains(a);

        string affixLabel = string.Empty;
        /// <summary>"VINGATIVO · GÊMEO": what the elite carries, for the HUDs.</summary>
        public string AffixLabel => affixLabel;
        /// <summary>The second body of a Twin: its wounds land on the original.</summary>
        public Enemy TwinOf { get; set; }

        static readonly AttackDef VengefulBlades = new AttackDef
        {
            id = "laminas_vingativas", windup = 1, active = 2, recovery = 1, damage = 12f, postureDamage = 10f, postureOnParried = 12f,
            shape = HitShape.Circle(0.22f), projectileSpeed = 10f, projectileCount = 8, spreadDegrees = 315f, projectileArt = "fx_kunai",
        };

        public void AddAffix(EliteAffix a)
        {
            if (affixes.Contains(a)) return;
            affixes.Add(a);
            var names = new string[affixes.Count];
            for (int i = 0; i < names.Length; i++) names[i] = EliteAffixes.Name(affixes[i]);
            affixLabel = string.Join(" · ", names);
            switch (a)
            {
                case EliteAffix.Veiled:
                    Veiled = true;
                    break;
                case EliteAffix.Ancestral:
                {
                    // Fights with the moves of two different warriors.
                    EnemyDef other = EnemyDefs.All[rng.Range(0, EnemyDefs.All.Length)];
                    if (other == Def) other = EnemyDefs.Ronin;
                    var merged = new MoveDef[moves.Length + other.moves.Length];
                    moves.CopyTo(merged, 0);
                    other.moves.CopyTo(merged, moves.Length);
                    moves = merged;
                    break;
                }
                case EliteAffix.Twin:
                    world.SpawnTwin(this);
                    break;
            }
            world.Fx.Popup(EliteAffixes.Name(a), Position + Vector2.up * (1.8f + 0.35f * affixes.Count), pal.gold);
        }

        /// <summary>A twin's wound lands on the shared body.</summary>
        public void TakeTwinDamage(float damage, Vector2 dir)
        {
            if (IsGone) return;
            Health -= damage;
            Flash(0.05f);
            if (Health <= 0f) Die();
        }

        /// <summary>Death throes of the affixes: blades, a spirit leaving the body, the twin fading.</summary>
        void OnDeathAffixes()
        {
            if (HasAffix(EliteAffix.Vengeful))
            {
                Facing = Vector2.right;
                world.SpawnProjectile(this, VengefulBlades, false);
            }
            if (HasAffix(EliteAffix.Possessed)) world.Split(this, "onryo", 1);
            foreach (Enemy e in world.Enemies)
            {
                if (e.TwinOf == this) e.Dispel();
            }
        }

        // ── Tools acting on enemies ───────────────────────────────────────────────────

        int confusedFrames;

        /// <summary>Smoke: loses sight of everyone and stands, blind, for a while.</summary>
        public void Confuse(int frames)
        {
            if (!CanBeHit) return;
            confusedFrames = Mathf.Max(confusedFrames, frames);
            world.Coordinator.Release(this);
            if (CurrentState == State.Attacking)
            {
                CurrentMove = null;
                EnterEngage(frames);
            }
            Target = null;
        }

        /// <summary>True while confused (the engage tick only wanders).</summary>
        bool TickConfused()
        {
            if (confusedFrames <= 0) return false;
            confusedFrames--;
            Velocity = Vector2.Lerp(Velocity, CombatMath.FromAngle(StateFrame * 3f + NetUid * 40f) * 0.6f, 0.05f);
            return true;
        }

        /// <summary>The bronze bell: ethereal things become solid for a while.</summary>
        public void ForceExpose(int frames)
        {
            if (!Def.ethereal) return;
            exposedFrames = Mathf.Max(exposedFrames, frames);
            world.Fx.Popup(Strings.PopupExposed, Position + Vector2.up * 1.2f, pal.gold);
        }

        /// <summary>A shuriken catches a leap, charge, dive or vanish before it lands.</summary>
        public bool InterruptLeap()
        {
            AttackDef a = CurrentAttack;
            if (a == null || StepFrame >= stepWindup) return false;
            if (a.lunge < 1.5f && !a.teleportBehind && !a.burrow && !a.wallStun) return false;
            Stun(40);
            world.Fx.Popup(Strings.PopupInterrupted, Position + Vector2.up * 1.3f, pal.gold);
            return true;
        }

        /// <summary>Beasts and Onis are the ones a firecracker truly stuns.</summary>
        public bool IsBeast => Def.superArmor || Def.radius >= 0.6f || Def.id == "kappa" || Def.id == "lodo" || Def.id == "lodo_pequeno" || Def.id == "berserker";

        // ── Standoff ──────────────────────────────────────────────────────────────────

        /// <summary>The leader bows before the standoff.</summary>
        public void StandoffBow()
        {
            Squash(1.2f, 0.78f);
        }

        public void StandoffTwitch()
        {
            Squash(0.82f, 1.18f);
            Position += Facing * 0.12f;
        }

        public void StandoffLunge(Vector2 toward)
        {
            Vector2 d = toward - Position;
            float dist = d.magnitude;
            if (dist < Radius + 0.6f) return;
            Facing = d / dist;
            Position += Facing * Mathf.Min(0.22f, dist - Radius - 0.6f);
            Squash(1.2f, 0.85f);
        }

        // ── Seal view ─────────────────────────────────────────────────────────────────

        /// <summary>Red hanko seals float over the enemy, one per Ki seal.</summary>
        void RenderSeals(float barY)
        {
            if (sealViews == null)
            {
                if (sealSprite == null)
                {
                    Texture2D tex = Resources.Load<Texture2D>("UI/seal");
                    if (tex != null) sealSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
                }
                sealViews = new SpriteRenderer[MaxSeals];
                for (int i = 0; i < MaxSeals; i++)
                {
                    sealViews[i] = SpriteUtil.Make("KiSeal", transform, sealSprite != null ? sealSprite : ShapeLibrary.Get(Shape.Square), pal.shu, Sorting.Overlay + 3);
                    sealViews[i].enabled = false;
                }
            }
            float pulse = 0.28f + 0.03f * Mathf.Sin(Time.unscaledTime * 6f);
            for (int i = 0; i < MaxSeals; i++)
            {
                bool on = i < Seals && !IsGone;
                sealViews[i].enabled = on;
                if (!on) continue;
                float x = (i - (Seals - 1) * 0.5f) * 0.32f;
                sealViews[i].transform.localPosition = new Vector3(x, barY + 0.55f, 0f);
                sealViews[i].transform.localScale = new Vector3(pulse, pulse, 1f);
                sealViews[i].color = Seals >= MaxSeals ? Color.Lerp(pal.shu, Color.white, 0.4f + 0.3f * Mathf.Sin(Time.unscaledTime * 12f)) : Color.white;
            }
        }
    }
}
