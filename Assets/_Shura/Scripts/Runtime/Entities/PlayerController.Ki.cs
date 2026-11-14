using UnityEngine;

namespace Shura
{
    /// <summary>The four guard stances (Kamae).</summary>
    public enum Kamae
    {
        /// <summary>High: +25% damage, perfect window −2 frames, breaks frontal guards.</summary>
        Jodan = 0,
        /// <summary>Middle: balanced, stronger counter-thrust.</summary>
        Chudan = 1,
        /// <summary>Low: perfect window +2 frames, a shorter and quicker dodge.</summary>
        Gedan = 2,
        /// <summary>Hidden: the blade disappears from view; held strikes (ATRASO) never come.</summary>
        Waki = 3,
    }

    /// <summary>Tools spend spirit papers.</summary>
    public enum ToolKind
    {
        Shuriken = 0,
        Smoke = 1,
        Bell = 2,
        Firecracker = 3,
        Oil = 4,
        Hook = 5,
    }

    /// <summary>Where a blow landed (three hits in one place leave a wound).</summary>
    public enum WoundRegion
    {
        Arm = 0,
        Leg = 1,
        Torso = 2,
        Eyes = 3,
    }

    /// <summary>
    /// The duelist's inner resources (design doc §3): Breath (stamina), the guard stances,
    /// the war cry, Ki seals detonation, the gourd and tools, wounds, the Shura meter and its
    /// Asura, Mushin, the swap Links (Elos) and weapon charge, the directed finishing cut and
    /// the Iai standoff. They feed into each other and into Flow; reading still beats hitting.
    /// </summary>
    public sealed partial class PlayerController
    {
        public const float MaxStamina = 100f;
        public const float AttackStamina = 8f;
        public const float DodgeStamina = 15f;
        const int ExhaustionFrames = 90;
        const int StaminaRegenDelay = 24;
        const int BreathWindow = 6;
        const int KamaeCooldown = 30;
        const int DrinkFrames = 28;
        const int BandageFrames = 40;
        /// <summary>Cura de um gole (Balance.json → player.sipHeal).</summary>
        float SipHeal => t.sipHeal;
        public const int SipsPerRoom = 2;
        const int AsuraFrames = 600;
        const int CycleFrames = 240;
        const int FinisherAimFrames = 40;
        const int DescendFrames = 60;
        const float KiaiRange = 3.8f;
        const float DetonateRange = 7f;

        static readonly string[] KamaeNames = { "JŌDAN", "CHŪDAN", "GEDAN", "WAKI" };
        public static string KamaeName(Kamae k) => KamaeNames[(int)k];

        // ── Breath ────────────────────────────────────────────────────────────────────
        public float Stamina { get; private set; } = MaxStamina;
        public int ExhaustedFrames { get; private set; }
        public bool Exhausted => ExhaustedFrames > 0;
        int staminaDelay;
        float comboSpent;
        /// <summary>The 6-frame glint after a combo: dodge now to breathe instead.</summary>
        public int BreathFrames { get; private set; }

        // ── Stance, gourd, wounds, buffs ─────────────────────────────────────────────
        public Kamae Stance { get; private set; } = Kamae.Chudan;
        int kamaeCooldown;
        public int Sips { get; private set; } = SipsPerRoom;
        public int MaxSips => SipsPerRoom + (Build?.ExtraSips ?? 0);
        int finishersForSip;
        bool bandaging;
        readonly int[] woundHits = new int[4];
        readonly bool[] wounds = new bool[4];
        public bool HasWound(WoundRegion r) => wounds[(int)r];
        public int WoundMask => (wounds[0] ? 1 : 0) | (wounds[1] ? 2 : 0) | (wounds[2] ? 4 : 0) | (wounds[3] ? 8 : 0);
        /// <summary>Eyes wound or cursed sight: this duelist's timing rings are hidden.</summary>
        public int RingHiddenFrames { get; private set; }
        int descendFrames;
        int oilCharges;
        int kiaiCooldown;
        float curseTime;

        // ── Shura, Asura, Mushin ──────────────────────────────────────────────────────
        public int AsuraFramesLeft { get; private set; }
        public bool InAsura => AsuraFramesLeft > 0;
        public bool InMushin { get; private set; }
        Enemy innerAsura;
        public bool FacingInnerAsura => innerAsura != null && !innerAsura.IsGone;

        // ── Swap Links (Elos), charge, cycle ──────────────────────────────────────────
        readonly int[] charge = new int[2];
        bool chargedThisSwing;
        public int Charge => charge[activeWeapon];
        int eloChain;
        float lastEloTime = -99f;
        public int CycleFramesLeft { get; private set; }
        PlayerAttackDef eloStep;

        // ── Directed finishing cut ────────────────────────────────────────────────────
        public float AimLineAngle { get; private set; }
        /// <summary>Where the golden line ends: a point on the enemy's body.</summary>
        public Vector2 AimPoint { get; private set; }
        /// <summary>0..1 how close the last finishing cut followed the line (1 = exactly on it).</summary>
        public float CutPrecision { get; private set; }
        /// <summary>Degrees between where the duelist is aiming and the golden line, right now.</summary>
        public float AimError => Mathf.Abs(Mathf.DeltaAngle(CombatMath.AngleDegrees(Facing), AimLineAngle));
        public const float CutPerfectDegrees = 4f;
        public const float CutLineDegrees = 12f;
        public const float CutCleanDegrees = 24f;
        public bool AimingCut => CurrentState == State.FinisherAim;
        public Enemy AimTarget => finisherTarget;

        // ── Standoff ──────────────────────────────────────────────────────────────────
        bool standoffHeld;

        public int ShuraLevel => Build != null ? ShuraRules.Level(Build.Shura) : 0;

        /// <summary>Damage from stances, the Shura, Mushin, the Asura, wounds and the cycle.</summary>
        float KiDamageMult
        {
            get
            {
                float m = 1f;
                if (Stance == Kamae.Jodan) m *= 1.25f;
                if (ShuraLevel >= 1) m *= 1.15f;
                if (InMushin) m *= 1.5f;
                if (InAsura) m *= 1.5f;
                if (HasWound(WoundRegion.Arm)) m *= 0.85f;
                if (CycleFramesLeft > 0) m *= 1.2f;
                return m;
            }
        }

        int KiParryBonus
        {
            get
            {
                int b = 0;
                if (Stance == Kamae.Jodan) b -= 2;
                if (Stance == Kamae.Gedan) b += 2;
                if (InMushin) b += 3;
                return b;
            }
        }

        float KiMoveMult => (Exhausted ? 0.6f : 1f) * world.MoveMultWorld * world.WebMult(Position);
        float KiDodgeDistance => (Stance == Kamae.Gedan ? 0.8f : 1f) * (HasWound(WoundRegion.Leg) ? 0.7f : 1f) * world.WebMult(Position);
        int DodgeFrames => Mathf.Max(6, Mathf.RoundToInt(t.dodgeFrames * (Stance == Kamae.Gedan ? 0.8f : 1f)));
        public float HealMult =>
            (ShuraLevel >= 1 ? 0.8f : 1f) * (HasWound(WoundRegion.Torso) ? 0.5f : 1f) * (curseTime > 0f ? 0.5f : 1f) * (Build?.HealMultExtra ?? 1f) *
            world.HealMultWorld * (Build != null && Build.Cursed(RunCurse.NoHeal) ? 0f : 1f);
        public float KiLifesteal => (ShuraLevel >= 2 ? 0.05f : 0f) * world.LifestealMultWorld + (world.LifestealMultWorld > 1f ? 0.04f : 0f);
        bool PostureUnbreakable => ShuraLevel >= 3 || InAsura;
        /// <summary>This duelist no longer sees timing rings (Frenzy, Mushin, eyes wound).</summary>
        public bool RingsHidden => ShuraLevel >= 3 || InMushin || RingHiddenFrames > 0 || InAsura || Mods.hiddenRings;
        /// <summary>In Shura "Thirst" the rings look 15% smaller to this duelist.</summary>
        public float RingScale => ShuraLevel >= 2 ? 0.85f : 1f;

        // ── Per tick ──────────────────────────────────────────────────────────────────

        /// <summary>Breath, stance, buffs and states that run whatever the duelist is doing.</summary>
        void TickKi()
        {
            if (staminaDelay > 0) staminaDelay--;
            else if (Stamina < MaxStamina)
            {
                bool still = CurrentState == State.Free && Velocity.sqrMagnitude < 0.5f;
                float rate = still ? 30f : CurrentState == State.Parry ? 8f : 15f;
                rate *= world.StaminaRegenMult;
                Stamina = Mathf.Min(MaxStamina, Stamina + rate * SimClock.Dt);
            }
            if (ExhaustedFrames > 0) ExhaustedFrames--;
            if (BreathFrames > 0) BreathFrames--;
            if (kamaeCooldown > 0) kamaeCooldown--;
            if (descendFrames > 0) descendFrames--;
            if (kiaiCooldown > 0) kiaiCooldown--;
            if (grabCooldown > 0) grabCooldown--;
            if (RingHiddenFrames > 0) RingHiddenFrames--;
            if (CycleFramesLeft > 0) CycleFramesLeft--;
            if (curseTime > 0f)
            {
                curseTime -= SimClock.Dt;
                if (StateFrame % 20 == 0 && Health > 1f) Health = Mathf.Max(1f, Health - 1f);
            }
            // The Muramasa and the "restless" curse feed the Shura on their own.
            if (Build != null && world.ActiveEnemyCount > 0)
            {
                if (Build.Muramasa) AddShura(1.2f * SimClock.Dt);
                if (Build.Cursed(RunCurse.Restless)) AddShura(1f * SimClock.Dt);
            }

            int k = input.TakeKamae();
            if (k >= 0 && k <= 3 && (Kamae)k != Stance && kamaeCooldown <= 0 && world.Has(Unlockable.Kamae)) SetStance((Kamae)k);

            if (AsuraFramesLeft > 0 && --AsuraFramesLeft == 0) EndAsura();

            // Mushin: a long perfect streak at full Flow empties the mind.
            if (!InMushin && Build != null && Build.PerfectStreak >= (Mods.mushinStreak > 0 ? Mods.mushinStreak : Build.MushinStreak) && FlowLevel >= 3)
            {
                InMushin = true;
                world.Fx.Emit(FxType.Mushin, Position, Vector2.up, 1f, pal.gold);
                world.Fx.Popup(Strings.PopupMushin, Position + Vector2.up * 1.3f, pal.gold);
                world.HintOnce(HintId.Mushin);
            }
        }

        int grabCooldown;
        bool interactWas;

        /// <summary>
        /// Corrente do Carrasco: a press of Interact near an enemy throws the chain — it is
        /// dragged to you and its posture is jarred. A fallen ally to revive comes first.
        /// </summary>
        bool TryGrab()
        {
            bool pressed = input.InteractHeld && !interactWas;
            interactWas = input.InteractHeld;
            if (!pressed || !Mods.grab || grabCooldown > 0) return false;
            if (world.FallenNear(this) != null) return false;
            Enemy e = world.FindNearestEnemy(Position, null);
            if (e == null || e.IsGone || !e.CanBeHit || e.IsAlly || Vector2.Distance(e.Position, Position) > 2.6f + e.Radius) return false;
            grabCooldown = 50;
            Vector2 dir = (e.Position - Position).normalized;
            Facing = dir;
            e.Pull(Position + dir * (Radius + e.Radius + 0.2f), 3f);
            e.ApplyPosture(35f);
            world.Fx.Emit(FxType.SlamImpact, e.Position, -dir, 1.2f, Color.white);
            world.Fx.Popup(Strings.PopupGrab, e.Position + Vector2.up * 1.1f, pal.gold);
            Squash(1.15f, 0.9f);
            return true;
        }

        /// <summary>Tutorial: a controlled wound so the gourd has something to heal.</summary>
        /// <summary>Tutorial: a wound to bandage.</summary>
        public void TrainingInjure(WoundRegion region) => wounds[(int)region] = true;

        public void TrainingWound(float healthFraction)
        {
            if (IsAlive) Health = Mathf.Max(1f, MaxHealth * Mathf.Clamp01(healthFraction));
        }

        /// <summary>Tutorial: drops a swing or a swap in progress, so the button asked for answers at once.</summary>
        public void TrainingInterrupt()
        {
            if (!IsAlive) return;
            if (CurrentState == State.Attack || CurrentState == State.Deflected || CurrentState == State.Swap || CurrentState == State.Parry) Enter(State.Free);
            // A try just before the frozen moment must not shrink the answer's window.
            framesSinceSwap = 9999;
            lastSwapSucceeded = true;
        }

        /// <summary>Tutorial: a full gourd again.</summary>
        public void TrainingRefill()
        {
            Sips = MaxSips;
        }

        public void SetStance(Kamae k)
        {
            previousStance = Stance;
            Stance = k;
            kamaeCooldown = KamaeCooldown;
            world.Fx.Emit(FxType.Kamae, Position, Facing, (int)k, pal.playerAccent);
            world.Fx.Popup(KamaeName(k), Position + Vector2.up * 1.1f, pal.playerAccent);
            world.HintOnce(HintId.Kamae);
        }

        void SpendStamina(float amount)
        {
            if (amount <= 0f) return;
            Stamina = Mathf.Max(0f, Stamina - amount);
            staminaDelay = StaminaRegenDelay;
            if (Stamina > 0f || Exhausted) return;
            // Out of breath: slower, and the perfect window halves for a moment.
            ExhaustedFrames = ExhaustionFrames;
            world.Fx.Popup(Strings.PopupExhausted, Position + Vector2.up * 1.1f, pal.danger);
            world.HintOnce(HintId.Stamina);
        }

        /// <summary>Called when a combo's last blow finishes: a glint to breathe in.</summary>
        void OfferBreath()
        {
            if (comboSpent <= 0f) return;
            BreathFrames = BreathWindow;
            world.Fx.Emit(FxType.BreathGlint, Position, Vector2.up, Radius, Color.white);
        }

        /// <summary>Dodge pressed in the glint: breathe instead of dodging.</summary>
        bool TryBreathe()
        {
            if (BreathFrames <= 0 || !input.Buffer.Peek(BufferedAction.Dodge)) return false;
            input.Buffer.Consume(BufferedAction.Dodge);
            BreathFrames = 0;
            Stamina = Mathf.Min(MaxStamina, Stamina + comboSpent * 0.7f);
            comboSpent = 0f;
            int streak = Build.RegisterPerfect();
            world.Fx.Emit(FxType.Breath, Position, Vector2.up, Radius, pal.playerAccent);
            world.Fx.Popup(Strings.PopupBreath, Position + Vector2.up * 1.1f, pal.playerAccent);
            if (streak >= 5 && streak % 5 == 0) world.Fx.Emit(FxType.StreakMilestone, Position, Vector2.up, streak, pal.gold);
            return true;
        }

        // ── Actions that can interrupt free, guard and recovery ───────────────────────

        /// <summary>Seals, cry, gourd, bandage and tools. True when one of them took over.</summary>
        bool TryKiActions()
        {
            if (input.Buffer.Consume(BufferedAction.Detonate))
            {
                input.Buffer.Drop(BufferedAction.Attack);
                input.Buffer.Drop(BufferedAction.Parry);
                if (DetonateSeals()) return CurrentState != State.Free && CurrentState != State.Parry;
            }
            if (input.Buffer.Consume(BufferedAction.Kiai))
            {
                input.Buffer.Drop(BufferedAction.Parry);
                input.Buffer.Drop(BufferedAction.Dodge);
                Kiai();
            }
            if (input.Buffer.Consume(BufferedAction.Soul)) TrySoul();
            if (input.Buffer.Consume(BufferedAction.ToolCycle) && world.Has(Unlockable.Tools)) CycleTool();
            if (input.Buffer.Consume(BufferedAction.Tool) && world.Has(Unlockable.Tools)) UseTool();
            if (input.Buffer.Consume(BufferedAction.Gourd) && world.Has(Unlockable.Gourd) && StartDrink(false)) return true;
            if (input.Buffer.Consume(BufferedAction.Bandage) && StartDrink(true)) return true;
            return false;
        }

        /// <summary>Attack + Parry: every Ki seal on enemies nearby bursts inside them.</summary>
        bool DetonateSeals()
        {
            float dmgMult = Weapon.id == "montante" ? 1.5f : Weapon.id == "manoplas" ? 0.8f : Weapon.id == "kanabo" ? 1.3f : 1f;
            float postMult = Weapon.id == "manoplas" ? 2f : 1f;
            bool any = false;
            foreach (Enemy e in world.Enemies)
            {
                if (e.Seals <= 0 || !e.CanBeHit || Vector2.Distance(e.Position, Position) > DetonateRange) continue;
                int seals = e.Seals;
                float damage = 12f * seals * dmgMult * DamageDealtMult * (Build?.SealDamageMult ?? 1f);
                float posture = 8f * seals * postMult * PostureDealtMult;
                bool shatter = seals >= 5 && e.Posture.Ratio >= 0.7f;
                e.ClearSeals();
                Vector2 dir = e.Position - Position;
                dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Facing;
                world.Fx.Emit(FxType.SealBurst, e.Position, dir, seals, pal.shu);
                e.ReceivePlayerHit(damage, posture, dir, true);
                if (shatter && !e.IsGone) e.ApplyPosture(e.Posture.Max * 2f);
                // Lightning seals leap to the nearest other enemy.
                if (Mods.trailElement == Element.Lightning) world.ChainLightning(e, 1, damage * 0.5f, posture * 0.5f);
                any = true;
            }
            if (any)
            {
                world.Fx.Popup(Strings.PopupDetonate, Position + Vector2.up * 1.2f, pal.shu);
                Squash(1.2f, 0.85f);
                world.HintOnce(HintId.Seals);
            }
            return any;
        }

        /// <summary>Parry + Dodge: the war cry. Spends a level of Flow to break a red blow.</summary>
        void Kiai()
        {
            if (kiaiCooldown > 0) return;
            kiaiCooldown = 40;
            world.Fx.Emit(FxType.Kiai, Position, Facing, KiaiRange, pal.gold);
            world.RegisterKiai(this);
            if (Build == null || Build.Flow.Level < 1)
            {
                world.Fx.Popup(Strings.PopupKiaiNoFlow, Position + Vector2.up * 1.1f, pal.uiDim);
                return;
            }
            bool interrupted = false;
            foreach (Enemy e in world.Enemies)
            {
                AttackDef a = e.CurrentAttack;
                if (a == null || !a.IsDangerous || Vector2.Distance(e.Position, Position) > KiaiRange + e.Radius) continue;
                int until = e.FramesUntilActive;
                if (until > 18 || until < -2) continue;
                e.Interrupt(20f, 45);
                interrupted = true;
            }
            if (!interrupted) return;
            world.DropFlow(1);
            world.Fx.Popup(Strings.PopupKiai, Position + Vector2.up * 1.3f, pal.gold);
            world.RegisterPerfect(Position);
            world.HintOnce(HintId.Kiai);
        }

        /// <summary>Sake from the gourd (or a bandage over a wound): a moment of rest that costs the streak.</summary>
        bool StartDrink(bool bandage)
        {
            if (bandage)
            {
                if (WoundMask == 0 || Build == null || Build.Bandages <= 0)
                {
                    world.Fx.Popup(Build != null && Build.Bandages <= 0 ? Strings.PopupNoBandage : Strings.PopupNoWound, Position + Vector2.up, pal.uiDim);
                    return false;
                }
                Build.Bandages--;
            }
            else
            {
                if (Sips <= 0)
                {
                    world.Fx.Popup(Strings.PopupGourdEmpty, Position + Vector2.up, pal.uiDim);
                    return false;
                }
                Sips--;
                run?.Profile.OnDrink();
            }
            bandaging = bandage;
            Build?.BreakStreak();
            Velocity *= 0.3f;
            Enter(State.Drink);
            world.HintOnce(HintId.Gourd);
            return true;
        }

        void TickDrink()
        {
            Steer(input.Move * MoveSpeed * 0.25f);
            int length = bandaging ? BandageFrames : DrinkFrames;
            if (StateFrame == length / 2)
            {
                if (bandaging)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        wounds[i] = false;
                        woundHits[i] = 0;
                    }
                    world.Fx.Popup(Strings.PopupBandaged, Position + Vector2.up * 1.1f, pal.health);
                }
                else
                {
                    float heal = SipHeal * HealMult;
                    Heal(SipHeal);
                    world.Fx.Popup("+" + Mathf.RoundToInt(heal), Position + Vector2.up * 1.1f, pal.health);
                }
                world.Fx.Emit(FxType.Heal, Position, Vector2.up, Radius, pal.health);
            }
            if (StateFrame >= length) Enter(State.Free);
        }

        // ── Tools ─────────────────────────────────────────────────────────────────────

        public static readonly string[] ToolNames = { "Shuriken", "Bomba de fumaça", "Sino de bronze", "Petardo", "Óleo", "Corrente com gancho" };
        static readonly int[] ToolCost = { 1, 2, 1, 1, 1, 1 };
        public static int CostOf(ToolKind k) => ToolCost[(int)k];

        void CycleTool()
        {
            if (Build == null) return;
            Build.Tool = (ToolKind)(((int)Build.Tool + 1) % ToolNames.Length);
            world.Fx.Popup(ToolNames[(int)Build.Tool], Position + Vector2.up * 1.1f, pal.uiText);
        }

        void UseTool()
        {
            if (Build == null) return;
            ToolKind tool = Build.Tool;
            int cost = ToolCost[(int)tool];
            if (Build.SpiritPapers < cost)
            {
                world.Fx.Popup(Strings.PopupNoPapers, Position + Vector2.up, pal.uiDim);
                return;
            }
            Build.SpiritPapers -= cost;
            run?.Profile.OnTool();
            Vector2 aim = Facing;
            switch (tool)
            {
                case ToolKind.Shuriken:
                    world.ThrowShuriken(this, aim);
                    break;
                case ToolKind.Smoke:
                    world.SmokeBomb(Position);
                    break;
                case ToolKind.Bell:
                    world.BronzeBell(Position);
                    break;
                case ToolKind.Firecracker:
                    world.Firecracker(Position + aim * 3f);
                    break;
                case ToolKind.Oil:
                    oilCharges = 1;
                    world.Fx.Popup(Strings.PopupOil, Position + Vector2.up, Elements.Color(Element.Fire));
                    break;
                case ToolKind.Hook:
                    world.HookChain(this, aim);
                    break;
            }
            world.HintOnce(HintId.Tools);
        }

        /// <summary>Oil on the blade: the next hit burns twice as hard.</summary>
        public bool ConsumeOil()
        {
            if (oilCharges <= 0) return false;
            oilCharges--;
            return true;
        }

        // ── Wounds ────────────────────────────────────────────────────────────────────

        /// <summary>A blow that landed: where it came from decides which part of the body it wounds.</summary>
        void RegisterWound(Vector2 toAttacker, Enemy attacker)
        {
            // Nobody is maimed in the dojo (the bandage lesson's cut is given on purpose).
            if (world.TutorialRoom) return;
            if (Build != null && Build.IgnoreFirstWound && !Build.FirstWoundIgnored)
            {
                Build.FirstWoundIgnored = true;
                return;
            }
            float angle = Vector2.Angle(Facing, toAttacker);
            WoundRegion region = angle < 50f ? WoundRegion.Torso : angle < 130f ? WoundRegion.Arm : WoundRegion.Leg;
            if (attacker != null && (attacker.Def.id == "olho" || attacker.Def.id == "sombra")) region = WoundRegion.Eyes;
            int r = (int)region;
            if (wounds[r]) return;
            if (++woundHits[r] < 3) return;
            wounds[r] = true;
            world.Fx.Popup(Strings.WoundName(region), Position + Vector2.up * 1.2f, pal.danger);
            if (region == WoundRegion.Eyes) RingHiddenFrames = 180;
            world.HintOnce(HintId.Wounds);
        }

        /// <summary>Cursed blows (green ring): blocking or taking them poisons and halves healing.</summary>
        void Curse()
        {
            curseTime = 4f;
            world.Fx.Emit(FxType.Curse, Position, Vector2.up, Radius, ColorUtil.Hex("#7BC950"));
            world.HintOnce(HintId.Curse);
        }

        // ── Shura ─────────────────────────────────────────────────────────────────────

        /// <summary>The Shura only rises (kills, pain at low health, forbidden things) and never cools by itself.</summary>
        public void AddShura(float amount)
        {
            if (Build == null || amount <= 0f || InAsura || FacingInnerAsura || !world.Has(Unlockable.ShuraMeter)) return;
            int before = ShuraLevel;
            Build.Shura = Mathf.Min(ShuraRules.Max, Build.Shura + amount * Build.ShuraGainMult * world.ShuraGainMult * Mods.shuraGainMult);
            int after = ShuraLevel;
            if (after > before && after < 4)
            {
                world.Fx.Emit(FxType.ShuraRise, Position, Vector2.up, after, pal.danger);
                world.Fx.Popup(ShuraRules.LevelName(after), Position + Vector2.up * 1.4f, pal.danger);
                world.HintOnce(HintId.Shura);
            }
            if (Build.Shura >= ShuraRules.Max) EnterAsura();
        }

        public void CalmShura(float amount)
        {
            if (Build == null || InAsura || FacingInnerAsura) return;
            Build.Shura = Mathf.Max(0f, Build.Shura - amount);
            world.Fx.Popup(Strings.PopupShuraCalm, Position + Vector2.up * 1.2f, pal.playerAccent);
        }

        /// <summary>Shura 100: ten seconds as an Asura with three arms and blows that cannot be blocked.</summary>
        void EnterAsura()
        {
            AsuraFramesLeft = AsuraFrames;
            world.Fx.Emit(FxType.AsuraFall, Position, Vector2.up, 1f, pal.danger);
            world.Fx.Popup(Strings.PopupAsura, Position + Vector2.up * 1.6f, pal.danger);
            world.HintOnce(HintId.Asura);
        }

        /// <summary>After the fall, the dark copy rises: beat it to clear the Shura.</summary>
        void EndAsura()
        {
            innerAsura = world.SpawnInnerAsura(this);
        }

        public void OnInnerAsuraDefeated()
        {
            innerAsura = null;
            if (Build != null) Build.Shura = 0f;
            Heal(30f);
            world.AddFlow(6f);
            world.Fx.Popup(Strings.PopupAsuraBeaten, Position + Vector2.up * 1.4f, pal.gold);
        }

        public void OnKillCredited(Enemy e)
        {
            AddShura(e.IsElite ? 8f : 3f);
        }

        public void OnFinisherDone()
        {
            if (Stance == Kamae.Gedan) CalmShura(20f);
            if (++finishersForSip >= 3)
            {
                finishersForSip = 0;
                if (Sips < 3) Sips++;
            }
            if (Build != null && Build.FinishersForPaper++ % 2 == 1 && Build.SpiritPapers < 12) Build.SpiritPapers++;
            OnFinisherMastery();
        }

        // ── Swap Links (Elos) and charge ──────────────────────────────────────────────

        /// <summary>A combo blow that landed fills the weapon's charge (0–3), once per swing.</summary>
        public void OnStrikeLanded(Enemy e)
        {
            if (chargedThisSwing || CurrentState != State.Attack) return;
            chargedThisSwing = true;
            charge[activeWeapon] = Mathf.Min(3, charge[activeWeapon] + 1);
        }

        /// <summary>The outgoing weapon's charge becomes an effect on the swap.</summary>
        void ReleaseCharge(WeaponDef w, int c)
        {
            if (c <= 0) return;
            switch (w.id)
            {
                case "katana":
                    world.FireSlashWave(Position, Facing, 6f * c, 6f * c);
                    break;
                case "montante":
                    world.ScheduleBurst(Position, 1, 1.6f + 0.3f * c, 4f * c, 14f * c, pal.gold);
                    world.Fx.Emit(FxType.SlamImpact, Position, Facing, 2f);
                    break;
                case "kusarigama":
                {
                    Enemy target = world.FindAssistTarget(Position, Facing, 60f, 5.5f, false);
                    if (target != null)
                    {
                        target.Pull(Position, 2f + c);
                        target.Stun(20 * c);
                    }
                    break;
                }
                case "naginata":
                    world.PlayerInstantStrike(Position, Facing, HitShape.Line(3f + 0.5f * c, 0.7f), 6f * c, 8f * c);
                    break;
                case "gemeas":
                    foreach (Enemy e in world.Enemies)
                    {
                        if (e.CanBeHit && Vector2.Distance(e.Position, Position) < 2.4f) e.ApplyDot(3f * c, 3f, Element.Blood);
                    }
                    break;
                case "manoplas":
                case "kanabo":
                    foreach (Enemy e in world.Enemies)
                    {
                        if (e.CanBeHit && Vector2.Distance(e.Position, Position) < 2.4f) e.ApplyPosture(10f * c);
                    }
                    break;
                default:
                    world.ScheduleBurst(Position, 1, 1.5f, 4f * c, 8f * c, w.trail);
                    break;
            }
            world.Fx.Popup(string.Format(Strings.PopupCharge, c), Position + Vector2.up * 1.1f, w.trail);
        }

        /// <summary>Swapping at the end of a combo throws a Link: a blow of its own for each pair of weapons.</summary>
        void StartElo(WeaponDef from, WeaponDef to)
        {
            PlayerAttackDef baseStep = to.swapStrike;
            eloStep = new PlayerAttackDef
            {
                name = EloNames.For(from.id, to.id), style = baseStep.style, windup = Mathf.Max(3, baseStep.windup - 2),
                active = baseStep.active, recovery = baseStep.recovery, damage = baseStep.damage * 0.85f, posture = baseStep.posture,
                shape = baseStep.shape, lunge = baseStep.lunge, fromAngle = baseStep.fromAngle, toAngle = baseStep.toAngle,
                extend = baseStep.extend, hits = baseStep.hits, pull = baseStep.pull, dash = baseStep.dash,
            };
            if (world.Run.Elapsed - lastEloTime > 4f) eloChain = 0;
            lastEloTime = world.Run.Elapsed;
            eloChain++;
            hitThisSwing.Clear();
            Velocity = Vector2.zero;
            Enter(State.SwapStrike);
            world.Fx.Popup(eloStep.name.ToUpperInvariant(), Position + Vector2.up * 1.1f, pal.gold);
            world.Fx.Emit(FxType.Elo, Position, Facing, eloChain, to.trail);
            BeginSwapStrikeMastery();
            world.WeaponSwapPower(this, Weapon.swapPower);
            world.HintOnce(HintId.Elo);
            if (eloChain >= 3 && CycleFramesLeft <= 0)
            {
                // Three Links without being touched: both blades fight on their own for a while.
                CycleFramesLeft = CycleFrames;
                eloChain = 0;
                world.Fx.Popup(Strings.PopupCycle, Position + Vector2.up * 1.5f, pal.gold);
            }
        }

        // ── Directed finishing cut ────────────────────────────────────────────────────

        void TickFinisherAim()
        {
            Enemy e = finisherTarget;
            Velocity = Vector2.zero;
            if (e == null)
            {
                Enter(State.Free);
                return;
            }
            e.HoldAt(finisherAnchor, 1f);
            Vector2 aim = input.AimDirection(Position);
            if (aim.sqrMagnitude < 0.01f && input.Move.sqrMagnitude > 0.04f) aim = input.Move.normalized;
            if (aim.sqrMagnitude > 0.01f) Facing = aim.normalized;
            bool cut = StateFrame >= FinisherAimFrames || (StateFrame >= 8 && input.Buffer.Consume(BufferedAction.Attack));
            if (!cut) return;
            ResolveCut(e);
            finisherDir = Facing;
            Enter(State.Finisher);
        }

        /// <summary>Where the finishing cut went: along the golden line, through the sword arm, or the head.</summary>
        void ResolveCut(Enemy e)
        {
            float cutAngle = CombatMath.AngleDegrees(Facing);
            float armAngle = CombatMath.AngleDegrees(CombatMath.Perpendicular(e.Facing) * -1f);
            float error = AimError;
            world.NotifyFinisher(this, e, "dirigida", error <= CutPerfectDegrees ? "perfeito" : error <= CutLineDegrees ? "linha" : error <= CutCleanDegrees ? "limpo" : "falho");
            // The closer to the line, the more blood — and the better the reward.
            CutPrecision = Mathf.Clamp01(1f - error / (CutCleanDegrees + 6f));
            var along = (AimPoint - Position).normalized;
            float gush = CutPrecision * CutPrecision;
            world.Fx.Blood(AimPoint, along, 12f + 140f * gush, e.Def.bloodColor);
            if (gush > 0.3f)
            {
                world.Fx.Blood(AimPoint, CombatMath.Perpendicular(along), 60f * gush, e.Def.bloodColor);
                world.Fx.Blood(AimPoint, -CombatMath.Perpendicular(along), 60f * gush, e.Def.bloodColor);
            }
            if (error <= CutPerfectDegrees)
            {
                // Exactly on the line: the perfect cut.
                Heal(25f);
                world.AddFlow(12f);
                e.DropsDouble = true;
                if (Build != null) Build.Mon += 15;
                world.Run.BonusEcos += MetaRules.RoomEcos(world.Depth) / 2;
                CalmShura(10f);
                world.Fx.Popup(Strings.PopupCutPerfect, e.Position + Vector2.up * 1.6f, pal.gold);
                world.Fx.Emit(FxType.Mushin, AimPoint, along, 1f, pal.gold);
                world.Fx.Emit(FxType.Crit, AimPoint, along, 1f, e.Def.bloodColor);
                world.Fx.Toast(Strings.ToastCutPerfect, pal.gold);
            }
            else if (error <= CutLineDegrees)
            {
                Heal(10f);
                world.AddFlow(5f);
                if (Build != null) Build.Mon += 5;
                world.Fx.Popup(Strings.PopupCutLine, e.Position + Vector2.up * 1.4f, pal.gold);
            }
            else if (error <= CutCleanDegrees)
            {
                world.AddFlow(2f);
                world.Fx.Popup(Strings.PopupCutClean, e.Position + Vector2.up * 1.4f, pal.uiText);
            }
            else if (Mathf.Abs(Mathf.DeltaAngle(cutAngle, armAngle)) < 28f)
            {
                e.DropsDouble = true;
                world.Fx.Popup(Strings.PopupCutArm, e.Position + Vector2.up * 1.4f, pal.gold);
            }
            else if (world.Depth >= 12 && Mathf.Abs(Mathf.DeltaAngle(cutAngle, 90f)) < 20f)
            {
                world.Run.BonusEcos += MetaRules.RoomEcos(world.Depth) / 2;
                world.Fx.Popup(Strings.PopupCutHead, e.Position + Vector2.up * 1.4f, pal.gold);
            }
        }


        // ── Iai standoff ──────────────────────────────────────────────────────────────

        void TickStandoff()
        {
            Velocity = Vector2.zero;
            Enemy leader = world.Standoff.Leader;
            if (leader != null)
            {
                Vector2 to = leader.Position - Position;
                if (to.sqrMagnitude > 1e-4f) Facing = to.normalized;
            }
            bool held = input.AttackHeld;
            if (standoffHeld && !held) world.Standoff.OnRelease(this);
            standoffHeld = held;
            input.Buffer.Clear();
            if (!world.Standoff.Active) Enter(State.Free);
        }

        /// <summary>Drawn too early (or never): the leader's blade costs 30 health before the fight.</summary>
        public void StandoffCut(float damage)
        {
            if (!IsAlive) return;
            Health = Mathf.Max(1f, Health - damage);
            world.Fx.Emit(FxType.PlayerHurt, Position, Vector2.down, damage);
            world.Fx.Popup(Strings.PopupStandoffLost, Position + Vector2.up * 1.3f, pal.danger);
            Flash(0.08f);
        }

        /// <summary>Oath "Pressa": past the room's time the body wears down (never below 1).</summary>
        public void HasteDrain(float amount)
        {
            if (!IsAlive || Health <= 1f) return;
            Health = Mathf.Max(1f, Health - amount);
        }

        /// <summary>Inside a perfect parry (or swap) window right now.</summary>
        public bool InPerfectWindow =>
            (CurrentState == State.Parry && StateFrame < parryWindow) || (CurrentState == State.Swap && StateFrame < swapWindow);

        /// <summary>Parried a feint: the guard opened for nothing.</summary>
        public void FooledByFeint()
        {
            lastParrySucceeded = false;
            if (Posture.Add(25f) && !PostureUnbreakable) GuardBreak(Facing);
            world.Fx.Popup(Strings.PopupFooled, Position + Vector2.up * 1.2f, pal.gold);
            world.HintOnce(HintId.Feint);
        }

        public void BeginStandoff()
        {
            standoffHeld = input != null && input.AttackHeld;
            Velocity = Vector2.zero;
            Enter(State.Standoff);
        }

        /// <summary>Per-room reset of the Ki resources (wounds and the Shura persist).</summary>
        void ResetKiForRoom()
        {
            ResetRituals();
            Stamina = MaxStamina;
            ExhaustedFrames = 0;
            BreathFrames = 0;
            Sips = SipsPerRoom + (Build?.ExtraSips ?? 0);
            InMushin = false;
            charge[0] = charge[1] = 0;
            CycleFramesLeft = 0;
            eloChain = 0;
            curseTime = 0f;
            if (Build != null)
            {
                Build.FirstWoundIgnored = false;
                Stance = Build.DefaultStance;
            }
        }

        // ── Shrines, onsen and gifts ──────────────────────────────────────────────────

        public void ClearWounds()
        {
            for (int i = 0; i < wounds.Length; i++)
            {
                wounds[i] = false;
                woundHits[i] = 0;
            }
            RingHiddenFrames = 0;
        }

        public void RestoreBreath()
        {
            Stamina = MaxStamina;
            ExhaustedFrames = 0;
        }

        /// <summary>The onsen: the mind goes quiet (Flow and Shura back to zero).</summary>
        public void EmptyMind()
        {
            if (Build == null) return;
            Build.Shura = 0f;
            Build.Flow.Reset();
            InMushin = false;
        }

        /// <summary>The corrupted mist: breath drains away.</summary>
        public void DrainBreath(float amount)
        {
            Stamina = Mathf.Max(0f, Stamina - amount);
            if (Stamina <= 0f && !Exhausted) ExhaustedFrames = ExhaustionFrames;
        }

        public void GainSip()
        {
            Sips++;
        }

        /// <summary>Taking a hit ends Mushin and the Link chain, and wounds.</summary>
        /// <summary>Who dealt the last blow (it may become a rival).</summary>
        public Enemy LastKillerEnemy { get; private set; }

        /// <summary>Who dealt the last blow (for the death poem); null = the road itself.</summary>
        public string LastKillerName { get; private set; }

        void OnHurtKi(Vector2 toAttacker, Enemy attacker)
        {
            LastKillerName = attacker != null && attacker.Def != null ? (attacker.IsRival ? attacker.RivalName : attacker.Def.displayName) : null;
            LastKillerEnemy = attacker;
            if (InMushin)
            {
                if (Build != null && (Build.MushinSurvivesHit || Mods.mushinSurvivesHit) && !mushinGraceUsed) mushinGraceUsed = true;
                else
                {
                    InMushin = false;
                    mushinGraceUsed = false;
                    world.Fx.Popup(Strings.PopupMushinEnd, Position + Vector2.up * 1.2f, pal.uiDim);
                }
            }
            eloChain = 0;
            RegisterWound(toAttacker, attacker);
            if (Health < MaxHealth * 0.3f) AddShura(5f);
        }

        bool mushinGraceUsed;
    }
}
