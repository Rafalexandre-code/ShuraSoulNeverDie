using UnityEngine;

namespace Shura
{
    /// <summary>
    /// A Lord of a biome (design doc §5.2): three phases, each one its own health and posture
    /// bar. A finisher (or a fall) in the first two only ends the phase; the Breaking Point
    /// (the master's answer to its signature) skips a whole phase and earns an exclusive item.
    /// </summary>
    public sealed partial class Enemy
    {
        public BossDef Boss { get; private set; }
        public bool IsBoss => Boss != null;
        /// <summary>1..3.</summary>
        public int Phase { get; private set; }
        /// <summary>A Breaking Point was found: the exclusive item drops at the end.</summary>
        public bool BrokeThrough { get; private set; }
        /// <summary>Jorōgumo: wrapped in silk, nearly untouchable until a Perfect Swap tears it.</summary>
        public bool Webbed { get; private set; }
        int webTorn;
        /// <summary>The Colossus: the skull stays shut while a hand still stands.</summary>
        public bool SkullClosed { get; set; }
        /// <summary>Yuki-onna: the ring stops this many frames at the middle of the windup.</summary>
        int frostHold;
        int frostAt = -1;
        int phaseGuard;

        public void MakeBoss(BossDef b)
        {
            Boss = b;
            Phase = 1;
            moves = b.phases[0];
            Webbed = b.kind == BossKind.Web;
        }

        /// <summary>Damage and posture a protected Lord actually takes (web, closed skull, the phase change).</summary>
        float BossShield()
        {
            if (Boss == null) return 1f;
            if (phaseGuard > 0) return 0f;
            if (Webbed || SkullClosed) return 0.12f;
            return 1f;
        }

        /// <summary>Instead of dying in the first two phases, the Lord rises into the next one.</summary>
        bool TryPhaseShift(bool breakThrough)
        {
            if (Boss == null || Phase >= BossDefs.Phases) return false;
            Phase++;
            if (breakThrough) BrokeThrough = true;
            moves = Boss.phases[Phase - 1];
            world.Coordinator.Release(this);
            CurrentMove = null;
            Health = MaxHealth;
            Posture = new PostureMeter(Posture.Max);
            phaseGuard = 90;
            staggerFrames = 90;
            Enter(State.Stagger);
            Squash(1.3f, 0.7f);
            if (Boss.kind == BossKind.Web) Webbed = true;
            world.OnBossPhase(this, breakThrough);
            return true;
        }

        /// <summary>The Breaking Point: a master's answer to the signature skips the phase (§5.2).</summary>
        public void BreakingPoint()
        {
            if (Boss == null || Phase >= BossDefs.Phases || phaseGuard > 0) return;
            TryPhaseShift(true);
        }

        /// <summary>Jorōgumo: a Perfect Swap against her tears the web for a while.</summary>
        public void TearWeb()
        {
            if (!Webbed) return;
            Webbed = false;
            webTorn = 6 * SimClock.TickRate;
            world.Fx.Popup(Strings.PopupWebTorn, Position + Vector2.up * 1.6f, pal.gold);
            world.Fx.Emit(FxType.SwapParry, Position, Vector2.up, 2f, Color.white);
        }

        void TickBoss()
        {
            if (Boss == null) return;
            if (phaseGuard > 0) phaseGuard--;
            if (webTorn > 0 && --webTorn == 0 && Boss.kind == BossKind.Web)
            {
                Webbed = true;
                world.Fx.Popup(Strings.PopupWebback, Position + Vector2.up * 1.6f, pal.uiDim);
            }
        }

        /// <summary>Yuki-onna's windups: shorter each phase, and some freeze halfway.</summary>
        int BossWindup(int windup)
        {
            if (Boss == null || Boss.kind != BossKind.Frost) return windup;
            float speed = Phase == 1 ? 1f : Phase == 2 ? 0.85f : 0.72f;
            windup = Mathf.Max(8, Mathf.RoundToInt(windup * speed));
            float chance = 0.25f + 0.15f * Phase;
            if (rng.NextFloat() < chance)
            {
                frostAt = windup / 2;
                frostHold = 12 + 6 * Phase;
            }
            else frostAt = -1;
            return windup;
        }

        /// <summary>True while the ring is frozen (the step does not advance).</summary>
        bool FrostHolding()
        {
            if (frostHold <= 0 || StepFrame != frostAt) return false;
            if (--frostHold == 0) frostAt = -1;
            Velocity = Vector2.zero;
            if (frostHold % 6 == 0) world.Fx.Emit(FxType.Kamae, Position, Facing, 0f, ColorUtil.Hex("#BFE3FF"));
            return true;
        }
    }
}
